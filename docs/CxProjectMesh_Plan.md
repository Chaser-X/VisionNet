# CxProjectMesh：Mesh → CxSurface 高度图投影

## Context

将三角网格（`CxMesh`）投影到指定位姿和分辨率的高度图（`CxSurface`, `SurfaceType=Surface`），用于模拟结构光/深度相机在任意视角下扫描三维物体的结果。这是 `CxTransformSurface` + `CxUniformSurface` 管道的"mesh 输入版"，需要 GPU 并行三角形栅格化而非点云采样。

---

## 新增文件

| 文件 | 说明 |
|------|------|
| `src/VisionNet/Compute/Kernels/ProjectMesh.cl` | OpenCL kernel（嵌入资源） |
| `src/VisionNet/Compute/CxProjectMesh.cs` | 计算类，继承 `OpenCLComputation` |
| `src/VisionNet/VisionNet.csproj` | 补充 `.cl` 嵌入资源和 `.cs` 编译项 |

---

## GPU 两轮 kernel 设计

```
Pass 1 — TransformVertices（每 work-item 处理一个顶点）
  输入 : vertexBuf (float3×N)，matrixBuf (float[16] row-major)
  输出 : transformedBuf (float3×N，perspective divide 已完成)

Pass 2 — RasterizeTriangles（每 work-item 处理一个三角形）
  输入 : transformedBuf，indexBuf (uint3×T)，grid params，intensityBuf
  输出 : heightMap (int[]，atomic), intensityMap (int[]，atomic)
```

Pass 2 核心流程（per triangle）：

1. 读三顶点变换后坐标
2. 计算 XY 包围盒 → 映射到 `[xMin,xMax] × [yMin,yMax]` grid 单元
3. 对包围盒内每个单元中心 `(px, py)`，计算重心坐标 `(w0,w1,w2)`
4. 若三分量均 ≥ 0（点在三角形内），插值 Z 和 intensity
5. `atomic_max`（Max 模式）或 `atomic_min`（Min 模式）写入 `heightMap`
6. 若新 Z 赢得原子竞争，写入插值后的 intensity

### kernel 签名

```c
// ProjectMesh.cl

__kernel void TransformVertices(
    __global const float* vertices,    // 3 floats/vertex
    __global const float* matrix,      // 16 floats row-major 4x4
    int                   vertexCount,
    __global       float* transformed) // 3 floats/vertex output

__kernel void RasterizeTriangles(
    __global const float* vertices,      // 已变换 3 floats/vertex
    __global const uint*  indices,       // 3 uints/triangle
    int   triangleCount,
    int   width, int height,
    float xOffset, float yOffset, float zOffset,
    float xScale,  float yScale,  float zScale,
    __global int*         heightMap,     // ReadWrite，哨兵初值
    __global int*         intensityMap,  // ReadWrite，零初始化
    __global const uchar* intensityData, // 顶点强度 or 贴图像素
    __global const float* uvs,           // float2/vertex (mode 2 时有效)
    int   textureWidth,                  // 贴图宽 (mode 2)
    int   textureHeight,                 // 贴图高 (mode 2)
    int   intensityMode,                 // 0=无, 1=逐顶点, 2=UV贴图
    int   mode)                          // 0=Max Z, 1=Min Z
```

**mode 2 强度采样（双线性）：**

```c
float u = clamp(w0*uvs[i0*2] + w1*uvs[i1*2] + w2*uvs[i2*2], 0.0f, 1.0f);
float v = clamp(w0*uvs[i0*2+1] + w1*uvs[i1*2+1] + w2*uvs[i2*2+1], 0.0f, 1.0f);
float fx = u*(textureWidth-1), fy = v*(textureHeight-1);
int x0=(int)fx, y0=(int)fy;
int x1=min(x0+1,textureWidth-1), y1=min(y0+1,textureHeight-1);
float wx=fx-x0, wy=fy-y0;
int sampled = (int)(
    (1-wx)*(1-wy)*intensityData[y0*textureWidth+x0] +
    wx    *(1-wy)*intensityData[y0*textureWidth+x1] +
    (1-wx)*wy    *intensityData[y1*textureWidth+x0] +
    wx    *wy    *intensityData[y1*textureWidth+x1]);
```

---

## C# 类设计 `CxProjectMesh`

### 继承结构

```
OpenCLComputation (base)
  └── CxProjectMesh
```

### 强度来源模式（`intensityMode`）

`CxMesh` 有三种强度情形，kernel 用整数区分：

| `intensityMode` | 条件 | 采样方式 |
|-----------------|------|---------|
| `0` | 无强度 | 不写入 intensityMap |
| `1` | `Intensity != null` 且 `TextureWidth == 0` | 重心坐标插值顶点强度 |
| `2` | `UVs != null` 且 `TextureWidth > 0` | 插值 UV → 双线性采样贴图 |

### 缓冲区生命周期

| 缓冲区 | 层级 | 条件 | 说明 |
|--------|------|------|------|
| `_vertexBuf` | Persistent | 始终 | mesh 顶点 |
| `_indexBuf` | Persistent | 始终 | mesh 索引 |
| `_intensityBuf` | Persistent | mode 1 或 2 | mode 1：顶点强度；mode 2：贴图像素 |
| `_uvBuf` | Persistent | mode 2（dummy 用于 0/1） | UV 坐标（float2 × N）|
| `matrixBuf` | Transient | 始终 | 每次 Project() 上传 pose |
| `transformedBuf` | Transient | 始终 | Pass 1 输出 |
| `heightBuf` | Transient | 始终 | 高度图累加器 |
| `intensMapBuf` | Transient | mode 1/2 | 强度累加器 |

### 构造函数

```csharp
public CxProjectMesh(CxMesh mesh) : base("CxProjectMeshProgram")
// 保存 mesh 引用，记录 _vertexCount, _triangleCount
// 计算 _intensityMode：
//   mesh.UVs != null && mesh.TextureWidth > 0 → 2（UV 贴图）
//   mesh.Intensity != null && len == vertexCount → 1（逐顶点）
//   否则 → 0（无强度）
// GPU 缓冲区在首次 Project() 调用时懒惰上传（EnsureMeshBuffers）
```

### 主 API

```csharp
// 完整控制（主接口）
public CxSurface Project(
    CxMatrix4X4 pose,
    float xOffset, float yOffset, float zOffset,
    float xScale,  float yScale,  float zScale,
    int   width,   int   height,
    ProjectionMode mode = ProjectionMode.Max)

// 便捷重载：xScale/yScale 指定分辨率，grid 范围从变换后 BBox 自动推导
public CxSurface Project(
    CxMatrix4X4 pose,
    float xScale, float yScale,
    ProjectionMode mode = ProjectionMode.Max)

// 辅助：CPU 计算变换后顶点的 BBox（用于便捷重载 + 外部工具）
public Box3D ComputeTransformedBounds(CxMatrix4X4 pose)
```

### `ProjectionMode` 枚举

放在 `VisionNet.Compute` 命名空间（与计算类同文件）：

```csharp
public enum ProjectionMode { Max = 0, Min = 1 }
```

### EnsureMeshBuffers 懒惰上传逻辑

```
_vertexBuf  ← Parallel.For 展开 CxPoint3D[] → float[]，AllocatePersistent
_indexBuf   ← AllocatePersistent<uint>
mode 1: _intensityBuf ← AllocatePersistent<byte>(mesh.Intensity)
mode 2: _intensityBuf ← AllocatePersistent<byte>(mesh.Intensity，即贴图像素)
        _uvBuf        ← Parallel.For 展开 CxPoint2D[] → float[]，AllocatePersistent
mode 0: _intensityBuf ← AllocatePersistent<byte>(new byte[1])  // dummy
        _uvBuf        ← AllocatePersistent<float>(new float[2]) // dummy
```

### Project() 内部流程

```
1. 参数校验（zScale > 0, width/height > 0）
2. EnsureInitialized()
3. EnsureMeshBuffers()（懒惰上传顶点/索引/强度/UV 持久缓冲）
4. AllocateTransient: matrixBuf, transformedBuf
5. SetKernelArgs(KTransformVertices) + ExecuteKernel
6. CPU 初始化 heightRaw[]（Max→INT_MIN, Min→INT_MAX）
7. AllocateTransient: heightBuf(CopyHostPtr), intensMapBuf(零初始化)
8. SetKernelArgs(KRasterizeTriangles，含 uvBuf, textureWidth/Height, intensityMode) + ExecuteKernel
9. ReadBuffer → heightRaw[], intensityRaw[]
10. ReleaseTransient()
11. BuildSurface()（Parallel.For，short 编码，Intensity 仅 mode>0 时有效）
12. 返回 CxSurface（SurfaceType.Surface）
```

### BuildSurface 后处理

```csharp
// Parallel.For over cellCount
// heightRaw[i] == sentinel → data[i] = short.MinValue（invalid）
// 否则 clamp 到 [short.MinValue+1, short.MaxValue] → (short)
// intensityRaw[i] clamp 到 [0, 255] → (byte)
// 无强度时返回 new byte[0] 给 CxSurface.Intensity
```

---

## 复用现有基础设施

| 现有代码 | 复用方式 |
|---------|---------|
| `OpenCLComputation.AllocatePersistent/Transient` | 缓冲区分层 |
| `OpenCLComputation.SetKernelArgs(params object[])` | 绑定 kernel 参数 |
| `OpenCLComputation.ExecuteKernel` | kernel 调度 |
| `OpenCLComputation.LoadEmbeddedResource` | 加载 `.cl` 文件 |
| `VisionOperator.CalculateBoundingBoxSIMD` | 便捷重载内部调用（CPU BBox） |
| dummy `_uvBuf`（mode 0/1） | 作为占位符传入，kernel 内通过 `intensityMode` 跳过访问 |
| `CxSurface(width, height, data, intensity, ...)` 构造函数 | 构建输出 |
| `short.MinValue` 作为 invalid 哨兵 | 与其余 Surface 语义一致 |

---

## .csproj 变更（2 处）

```xml
<Compile Include="Compute\CxProjectMesh.cs" />
<EmbeddedResource Include="Compute\Kernels\ProjectMesh.cl" />
```

---

## 约束与已知限制

- **强度 atomic race**：`atomic_max/min` 只保证 Z 的正确性；intensity 可能偶发来自非最高/最低 Z 的三角形（与 `CxUniformSurface` 同等精度）
- **int 溢出**：scaledZ 超出 short 范围时 `BuildSurface` 做 clamp，不报错
- **背面三角形**：不剔除（统一光栅化），由 Max/Min 模式自然处理
- **退化三角形**：`denom < 1e-9f` 时跳过

---

## 验证方法

1. 单元测试：用已知尺寸的立方体 mesh + 正交位姿，验证输出 CxSurface 中心行列高度值与预期一致
2. 便捷重载测试：`Project(pose, 0.01f, 0.01f)` 输出 grid 包含所有顶点
3. 与 `CxTransformSurface` + `CxUniformSurface` 对比：同一 mesh 转点云后走现有管道，两者 Z 差值应在 zScale 量级内
4. Intensity 验证：渐变强度 mesh，输出 `CxSurface.Intensity` 平滑过渡无异常
