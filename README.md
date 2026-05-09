# VisionNet

基于 .NET 4.8 的 3D 视觉处理库，提供结构化点云/网格的数据模型、视觉算子 API，以及一个基于 OpenGL（SharpGL）的高性能 3D 渲染控件 **CxDisplay**。

---

## 项目结构

```
VisionNet/
├── src/
│   ├── VisionNet/              # 核心库（数据类型 + 算子）
│   │   ├── DataType/
│   │   │   ├── Geometry3D/     # 3D 几何类型
│   │   │   ├── Geometry2D/     # 2D 几何类型
│   │   │   └── Models/         # 主数据模型（Surface / Mesh / Image）
│   │   ├── VisionOperator.cs   # 静态算子 API
│   │   └── Export.cs           # P/Invoke 本地库声明
│   └── Controls/
│       └── CxControl/          # 3D 渲染控件库
│           ├── Camera/         # 相机系统
│           ├── RenderItem/     # 渲染对象体系
│           │   ├── Surface/    # 点云 / 结构化表面渲染
│           │   ├── Mesh/       # 网格渲染
│           │   ├── Geometry/   # 几何图元（点、线、面、框）
│           │   └── Overlay/    # HUD 叠加（颜色条、坐标轴、标签）
│           ├── CxDisplay.cs            # 核心控件（状态 / 初始化）
│           ├── CxDisplay.Api.cs        # 公共 API
│           ├── CxDisplay.Render.cs     # 渲染管线
│           ├── CxDisplay.GLResources.cs# GL 资源管理
│           ├── CxDisplay.Input.cs      # 鼠标 / 菜单交互
│           └── CxDisplay.Designer.cs   # WinForms UI 初始化
├── Test/
│   ├── Test/                   # 控制台测试程序
│   └── DemoFrom/               # WinForms 演示应用
├── 3rd/                        # 第三方 DLL（SharpGL、GoSdk 等）
└── bin/                        # 编译输出（Debug / Release）
```

---

## 依赖

| 依赖 | 说明 |
|------|------|
| .NET Framework 4.8 | 目标框架 |
| SharpGL | OpenGL 封装（VAO / VBO / Shader / Texture） |
| VisionLib.dll | 本地 C++ 算法库（P/Invoke，仅 x64） |
| System.Numerics | Vector3 / Matrix4x4（相机运算） |

---

## VisionNet 核心库

### 数据类型

#### 3D 几何（`VisionNet.DataType`）

| 类型 | 说明 |
|------|------|
| `CxPoint3D` | 3D 点（X, Y, Z），`StructLayout.Explicit`，可直接与 C++ 互操作 |
| `CxPoint3DI` | 带强度的 3D 点 |
| `CxVector3D` | 3D 向量，支持 +、-、×、÷、Dot、Cross、Normalize |
| `CxSize3D` | 3D 尺寸（Width, Height, Depth） |
| `Box3D` | 轴对齐包围盒（Center + Size） |
| `Plane3D` | 平面（Point + Normal） |
| `Sphere` | 球体（Center + Radius） |
| `Circle3D` | 3D 圆（Center + Normal + Radius） |
| `Segment3D` | 线段（Start + End） |
| `Polygon3D` | 多边形顶点序列（可闭合） |
| `CxCoordination3D` | 3D 坐标系（Origin + XAxis + YAxis + ZAxis） |
| `TextInfo` | 世界坐标锚定文本标签 |

#### 2D 几何

| 类型 | 说明 |
|------|------|
| `CxPoint2D` / `CxVector2D` | 2D 点 / 向量 |
| `Segment2D` / `Polygon2D` / `Circle2D` | 2D 线段 / 多边形 / 圆 |
| `Text2D` | 屏幕空间文本（坐标 + 字号） |

#### 主数据模型

**`CxSurface`** — 结构化高度图或无序点云：
```csharp
// 结构化表面：Data 为 Width×Length 的 Z 高度数组（short），-32768 表示无效点
// 点云：Data 为 Width×Length×3 的 (X,Y,Z) 数组
var surface = new CxSurface(width, length, data, intensity,
    xOffset, yOffset, zOffset, xScale, yScale, zScale);

CxPoint3D[] points = surface.ToPoints();   // 转换为世界坐标点数组
surface.SetData(nativePtr);                // 从非托管内存加载高度数据
surface.SetIntensity(nativePtr);           // 从非托管内存加载强度数据
```

**`CxMesh`** — 三角面片网格：
```csharp
var mesh = new CxMesh
{
    Vertices     = new CxPoint3D[...],  // 顶点位置
    Indices      = new uint[...],       // 三角形索引缓冲
    UVs          = new CxPoint2D[...],  // UV 坐标
    Intensity    = new byte[...],       // 逐顶点强度（0–255）
    TextureWidth = 512,
    TextureHeight = 512,
};
```

**`CxImage<T>`** — 通用 2D 图像（泛型像素类型）：
```csharp
var image = new CxImage<byte>(640, 480);
byte pixel = image.Data[row * image.Width + col];
```

**`CxMatrix4X4`** — 4×4 列主序矩阵（OpenGL 约定）

### 算子 API（`VisionOperator`）

```csharp
// 计算点云重心
CxPoint3D center = VisionOperator.GetPoint3DArrayCenter(points);

// 将无序点云均匀重采样为结构化表面
CxSurface surface = VisionOperator.UniformSurface(
    points, intensity, width, height,
    xScale, yScale, zScale, xOffset, yOffset, zOffset);

// 4×4 矩阵变换（OpenGL 列主序）
CxPoint3D transformed = VisionOperator.TransformPoint3D(point, matrix);
```

---

## CxControl 渲染控件

### CxDisplay 控件

`CxDisplay` 继承自 `OpenGLControl`（SharpGL WinForms），实现了完整的 3D 渲染管线和 GL 资源生命周期管理。

**设计原则：**
- **GL 资源由 CxDisplay 统一持有**（VBO / VAO / Shader / Texture），渲染 Item 只提供 CPU 侧数据（`RenderData`）
- **延迟释放**：GL 资源通过 `ConcurrentQueue` 在下一渲染帧内安全释放，避免跨线程调用 GL API
- **线程安全**：`_resourceLock` 保护资源池，`Render()` 以快照方式遍历，不持锁执行 GL 调用

#### 基本用法

```csharp
// 在 WinForms 中添加 CxDisplay 控件后：

// 显示点云（替换语义）
cxDisplay1.SetPointCloud(surface);

// 显示网格（高性能 Shader 路径）
cxDisplay1.SetMeshAdvancedItem(mesh);

// 叠加多个对象（追加语义）
cxDisplay1.AddPointCloud(surface2);
cxDisplay1.AddMesh(mesh2);

// 添加几何叠加层
cxDisplay1.SetSegment(segments, Color.Red, 2f);
cxDisplay1.SetPoint(points, Color.Yellow, 5f);
cxDisplay1.SetBox(boxes, Color.Green);
cxDisplay1.SetPolygon(polygons, Color.Cyan);

// 清空表面对象
cxDisplay1.ClearSurfaceItems();

// 重置视图（含几何叠加层）
cxDisplay1.ResetView();
```

#### 视图控制

```csharp
// 设置视图模式
cxDisplay1.SurfaceViewMode = ViewMode.Top;    // Top / Front / Left / Right / None

// 切换渲染模式
cxDisplay1.SurfaceMode      = SurfaceMode.Mesh;
cxDisplay1.SurfaceColorMode = SurfaceColorMode.ColorWithIntensity;

// 设置相机焦点和上向量
cxDisplay1.SetViewCenter(new CxPoint3D(0, 0, 0));
cxDisplay1.SetViewUpDirection(new CxVector3D(0, 0, 1));

// 显示世界坐标轴
cxDisplay1.ShowCoordinateSystem = true;
```

#### 渲染模式说明

| 枚举 | 值 | 说明 |
|------|----|------|
| `SurfaceMode.PointCloud` | 1 | 每个顶点渲染为点精灵 |
| `SurfaceMode.Mesh` | 2 | 渲染为三角面片 |
| `SurfaceColorMode.Color` | — | 按 Z 高度映射彩虹色 |
| `SurfaceColorMode.Intensity` | — | 按逐点强度值渲染灰度 |
| `SurfaceColorMode.ColorWithIntensity` | — | 高度色 × 强度混合 |

#### 颜色映射

彩虹色梯度（7 档，Z 从低到高）：

```
深蓝 → 天空蓝 → 绿 → 黄 → 红 → 粉 → 白
```

多个表面叠加时，颜色条和各 Item 的颜色映射统一使用**全局 Z 范围**，保证颜色一致性。

#### 鼠标交互

| 操作 | 功能 |
|------|------|
| 左键拖拽 | 追踪球旋转（绕最近表面点） |
| 中键拖拽 | 平移（自适应速度） |
| 滚轮 | 缩放（5% 步长） |
| 左键双击 | 焦点对准点击位置 |
| 鼠标悬停 | 显示 X / Y / Z / 强度坐标标签 |
| 右键菜单 | 切换视图模式 / 渲染模式 / 颜色模式 |

### 渲染 Item 体系

| 类 | 渲染路径 | 适用场景 |
|----|---------|---------|
| `CxSurfaceItem` | 固定管线（VBO + 颜色数组） | 中小规模结构化表面 |
| `CxSurfaceAdvancedItem` | VAO + GLSL Shader + 强度纹理 | 大规模点云（自动降采样至 ≤200 万点）|
| `CxMeshItem` | 固定管线（VBO + 颜色数组） | 中小规模三角网格 |
| `CxMeshAdvancedItem` | VAO + GLSL Shader + 强度纹理 | 高性能三角网格 |
| `CxPoint3DItem` | 固定管线点 / 实例化球体 | 离散点集 |
| `CxSegment3DItem` | GL_LINES | 线段集合 |
| `CxPolygon3DItem` | GL_LINE_LOOP / GL_LINE_STRIP | 多边形 |
| `CxPlane3DItem` | GL_QUADS | 平面区域 |
| `CxBox3DItem` | GL_QUADS + GL_LINES | 半透明包围盒 + 线框 |
| `CxColorBarItem` | 2D 正交 HUD | Z 高度颜色条 |
| `CxCoordinateSystemItem` | 圆柱 + 圆锥 | 坐标轴指示器（世界 + 屏幕左下角）|
| `CxCoordinationTagItem` | 2D 正交 HUD | 鼠标悬停坐标标签 |
| `CxTextInfoItem` | 投影到屏幕坐标 | 世界锚定文本 |
| `CxText2DItem` | 2D 正交 HUD | 屏幕固定文本 |

### 相机系统

`CxAdvancedTrackBallCamera`（默认，当前使用）：
- 基于 `System.Numerics.Vector3 / Matrix4x4`
- 透视投影（FOV 60°）/ 正交投影（2D 模式）
- `FitView(Box3D?)` 自动调整距离以适配场景
- 旋转绕最近表面点（而非场景中心），操作更自然

`CxTrackBallCamera`（旧版，保留参考）：
- 基于原始旋转矩阵累积
- 行为与新版相机有差异，不建议用于新开发

---

## 快速上手

### 1. 添加引用

在 WinForms 项目中引用以下程序集：
- `bin\Debug\VisionNet.dll`
- `bin\Debug\CxControl.dll`
- `3rd\SharpGL.dll` / `SharpGL.WinForms.dll` / `SharpGL.SceneGraph.dll`

### 2. 在工具箱中添加 CxDisplay

将 `CxControl.dll` 注册到 Visual Studio 工具箱，拖放 `CxDisplay` 控件到窗体。

### 3. 显示点云

```csharp
using VisionNet;
using VisionNet.DataType;
using VisionNet.Controls;

// 创建结构化表面（高度图）
var surface = new CxSurface(
    width: 500, length: 500,
    data: heightData,            // short[] 高度数组，-32768 = 无效
    intensity: intensityData,    // byte[] 强度数组（可为 null）
    xOffset: 0f, yOffset: 0f, zOffset: 0f,
    xScale: 0.1f, yScale: 0.1f, zScale: 0.001f);

// 显示
cxDisplay1.SurfaceMode      = SurfaceMode.PointCloud;
cxDisplay1.SurfaceColorMode = SurfaceColorMode.ColorWithIntensity;
cxDisplay1.SetPointCloud(surface);
```

### 4. 叠加几何图形

```csharp
// 在点云上叠加包围盒
var box = new Box3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 5));
cxDisplay1.SetBox(new[] { box }, Color.Yellow);

// 添加线段
var seg = new Segment3D(new CxPoint3D(0, 0, 0), new CxPoint3D(5, 5, 5));
cxDisplay1.SetSegment(new[] { seg }, Color.Red, 2f);
```

---

## 注意事项

- **GL 资源只能在 GL 线程释放**：不要在 Item 的 `Dispose()` 中直接调用 GL 删除函数；通过 `OnRenderDataChanged` 事件让 `CxDisplay` 在下一帧统一回收。
- **大点云自动降采样**：`SetPointCloud / AddPointCloud` 在点数超过 1 亿时自动降至 1000 万；`SetSurfaceAdvancedItem` 最大处理 200 万点。
- **多 Item 颜色一致性**：叠加多个表面时，颜色条和各 Item 颜色映射自动同步到全局 Z 范围。
- **线程安全**：`SetPointCloud` 等 API 可在后台线程调用；GL 资源的创建和释放由 `CxDisplay` 在渲染线程内完成。
