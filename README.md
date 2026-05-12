<div align="center">

# VisionNet

**面向工业视觉的 .NET 3D 点云 / 网格处理与渲染库**

[![.NET Framework](https://img.shields.io/badge/.NET%20Framework-4.8-blue?logo=dotnet)](https://dotnet.microsoft.com/)
[![Platform](https://img.shields.io/badge/Platform-Windows%20x64-lightgrey?logo=windows)](https://www.microsoft.com/windows)
[![OpenGL](https://img.shields.io/badge/OpenGL-3.3+-green?logo=opengl)](https://www.opengl.org/)
[![OpenCL](https://img.shields.io/badge/OpenCL-GPU%20Compute-orange)](https://www.khronos.org/opencl/)
[![License](https://img.shields.io/badge/License-MIT-yellow)](LICENSE)

[快速上手](#快速上手) · [API 文档](#api-参考) · [架构说明](#架构)

</div>

---

## 简介

VisionNet 由两个互相独立的库组成：

| 库 | 说明 |
|----|------|
| **VisionNet.dll** | 核心数据类型（点云、网格、几何体）+ 静态算子 API（通过 P/Invoke 调用本地 C++ 库） |
| **CxControl.dll** | 基于 SharpGL/OpenGL 的高性能 3D 渲染控件 `CxDisplay`，支持点云、网格、几何叠加层的实时渲染 |

### 主要特性

- 🗂️ 丰富的 3D/2D 几何数据类型，内存布局与 C++ 互操作兼容
- ⚡ 高性能渲染：VAO + GLSL Shader + 强度纹理，支持百万级点云实时显示
- 🎨 多 Item 叠加：点云、网格、几何图元可同时显示，颜色条自动同步全局 Z 范围
- 🖱️ 完整鼠标交互：追踪球旋转、平移、缩放、双击对焦、悬停坐标标签
- 🔒 线程安全：GL 资源延迟释放机制，数据更新可在后台线程执行
- 🚀 **OpenCL GPU 计算**：并行包围盒计算、GPU 点云重采样（`CxUniformSurface`）、GPU 表面变换（`CxTransformSurface`）

---

## 目录

- [环境要求](#环境要求)
- [快速上手](#快速上手)
- [架构](#架构)
- [API 参考](#api-参考)
  - [数据类型](#数据类型)
  - [算子 API](#算子-api)
  - [CxDisplay 控件](#cxdisplay-控件)
- [渲染 Item 体系](#渲染-item-体系)
- [注意事项](#注意事项)

---

## 环境要求

| 依赖 | 版本 | 说明 |
|------|------|------|
| Windows | 10 / 11 | 仅支持 Windows |
| .NET Framework | 4.8 | 目标框架 |
| Visual Studio | 2019+ | 推荐开发环境 |
| OpenGL | 3.3+ | 需要独立显卡或支持 OpenGL 3.3 的集成显卡 |
| OpenCL | 1.2+ | GPU 计算（可选，无 OpenCL 设备时自动降级到 CPU 路径） |
| VisionLib.dll | — | 本地 C++ 算法库，仅 x64 |
| OpenCL.Net.dll | — | OpenCL .NET 绑定，位于 `3rd/` 目录 |

---

## 快速上手

### 1. 添加引用

编译项目后，在 WinForms 工程中引用以下程序集：

```
bin\Debug\VisionNet.dll
bin\Debug\CxControl.dll
3rd\SharpGL.dll
3rd\SharpGL.WinForms.dll
3rd\SharpGL.SceneGraph.dll
```

### 2. 添加 CxDisplay 控件

将 `CxControl.dll` 注册到 Visual Studio 工具箱，拖放 `CxDisplay` 控件到窗体，或在代码中创建：

```csharp
var display = new CxDisplay(ViewMode.Top, SurfaceMode.PointCloud, SurfaceColorMode.ColorWithIntensity);
display.Dock = DockStyle.Fill;
this.Controls.Add(display);
```

### 3. 显示点云

```csharp
using VisionNet;
using VisionNet.DataType;
using VisionNet.Controls;

// 构造结构化高度图（short[] 编码，-32768 = 无效点）
var surface = new CxSurface(
    width: 500, length: 500,
    data: heightData,          // short[]，Z 高度，-32768 表示无效
    intensity: intensityData,  // byte[]，强度 0–255（可为 null）
    xOffset: 0f, yOffset: 0f, zOffset: 0f,
    xScale: 0.1f, yScale: 0.1f, zScale: 0.001f);

cxDisplay1.SetPointCloud(surface);
```

### 4. 叠加多个对象

```csharp
// 追加第二个点云（不清空已有内容）
cxDisplay1.AddPointCloud(surface2);

// 叠加包围盒
cxDisplay1.SetBox(new[] {
    new Box3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 5))
}, Color.Yellow);

// 叠加线段
cxDisplay1.SetSegment(new[] {
    new Segment3D(new CxPoint3D(0, 0, 0), new CxPoint3D(5, 5, 5))
}, Color.Red, size: 2f);
```

### 5. 视图控制

```csharp
cxDisplay1.SurfaceViewMode  = ViewMode.Top;                        // 切换视角
cxDisplay1.SurfaceMode      = SurfaceMode.Mesh;                    // 点云 / 网格
cxDisplay1.SurfaceColorMode = SurfaceColorMode.ColorWithIntensity; // 颜色模式
cxDisplay1.ShowCoordinateSystem = true;                            // 显示坐标轴

cxDisplay1.SetViewCenter(new CxPoint3D(0, 0, 0));                  // 设置相机焦点
cxDisplay1.ResetView();                                            // 重置视图
```

---

## 架构

```
VisionNet/
├── src/
│   ├── VisionNet/                  # 核心库
│   │   ├── DataType/
│   │   │   ├── Geometry3D/         # CxPoint3D, CxVector3D, Box3D, Plane3D ...
│   │   │   ├── Geometry2D/         # CxPoint2D, Segment2D, Polygon2D ...
│   │   │   └── Models/             # CxSurface, CxMesh, CxImage, CxMatrix4X4
│   │   ├── VisionOperator.cs       # 静态算子 API
│   │   └── Export.cs               # P/Invoke 声明（VisionLib.dll）
│   └── Controls/
│       └── CxControl/              # 渲染控件库
│           ├── Camera/             # ICamera, CxAdvancedTrackBallCamera
│           ├── RenderItem/
│           │   ├── Surface/        # CxSurfaceItem, CxSurfaceAdvancedItem
│           │   ├── Mesh/           # CxMeshItem, CxMeshAdvancedItem
│           │   ├── Geometry/       # Point / Segment / Polygon / Plane / Box
│           │   └── Overlay/        # ColorBar, CoordinateSystem, Tag, Text
│           ├── CxDisplay.cs            # 状态 / 初始化 / 共用私有工具
│           ├── CxDisplay.Api.cs        # 公共 API（Set* / Add* / 视图管理）
│           ├── CxDisplay.Render.cs     # 渲染管线（帧驱动）
│           ├── CxDisplay.GLResources.cs# GL 资源生命周期
│           ├── CxDisplay.Input.cs      # 鼠标 / 菜单 / 坐标拾取
│           └── CxDisplay.Designer.cs   # WinForms UI 初始化
├── Test/
│   ├── Test/                       # 控制台单元测试
│   └── DemoFrom/                   # WinForms 演示应用
└── 3rd/                            # 第三方 DLL（SharpGL, GoSdk 等）
```

### CxDisplay 设计原则

```
┌──────────────────────────────────────┐
│             CxDisplay                │
│  ┌──────────┐    ┌────────────────┐  │
│  │ CPU 数据  │    │   GL 资源池    │  │
│  │(RenderData)──►│(VBO/VAO/Shader)│  │
│  └──────────┘    └────────────────┘  │
│    RenderItem         _resourcePool  │
│  （只管数据）    （CxDisplay 统一持有）  │
└──────────────────────────────────────┘
         ↓ GL 资源延迟释放
    _pendingRelease（ConcurrentQueue）
         ↓ 下一帧 GL 上下文中安全回收
```

- **职责分离**：渲染 Item 只持有 CPU 数据，GL 对象由 `CxDisplay` 统一创建和释放
- **延迟释放**：`Dispose` / 替换触发的 GL 资源回收推迟到下一渲染帧，避免跨线程 GL 调用
- **线程安全**：`_resourceLock` 保护资源池；`Render()` 取快照遍历，不持锁执行 GL 调用

---

## API 参考

### 数据类型

<details>
<summary><b>3D 几何类型（VisionNet.DataType）</b></summary>

| 类型 | 说明 |
|------|------|
| `CxPoint3D` | 3D 点（X, Y, Z）—— `StructLayout.Explicit`，可直接与 C++ 互操作 |
| `CxPoint3DI` | 带强度的 3D 点 |
| `CxVector3D` | 3D 向量，支持 `+` `-` `×` `÷` `Dot` `Cross` `Normalize` |
| `CxSize3D` | 3D 尺寸（Width, Height, Depth） |
| `Box3D` | 轴对齐包围盒（Center + Size） |
| `Plane3D` | 平面（Point + Normal） |
| `Sphere` | 球体（Center + Radius） |
| `Circle3D` | 3D 圆（Center + Normal + Radius） |
| `Segment3D` | 线段（Start + End） |
| `Polygon3D` | 多边形顶点序列（`IsClosed` 控制是否闭合） |
| `CxCoordination3D` | 3D 坐标系（Origin + XAxis + YAxis + ZAxis） |
| `TextInfo` | 世界坐标锚定文本标签（Location + Text + Size） |

</details>

<details>
<summary><b>2D 几何类型</b></summary>

| 类型 | 说明 |
|------|------|
| `CxPoint2D` / `CxVector2D` | 2D 点 / 向量 |
| `Segment2D` | 2D 线段 |
| `Polygon2D` | 2D 多边形 |
| `Circle2D` | 2D 圆 |
| `Text2D` | 屏幕空间文本（Location + Text + FontSize） |

</details>

<details>
<summary><b>主数据模型</b></summary>

**`CxSurface`** — 结构化高度图 或 无序点云

```csharp
// 结构化高度图：Data 长度 = Width × Length，每个元素为 Z 高度（short）
// 无序点云：    Data 长度 = Width × Length × 3，每个点为 (X, Y, Z) 三元组
// 无效点：      对应位置值为 -32768

var surface = new CxSurface(width, length, data, intensity,
    xOffset, yOffset, zOffset, xScale, yScale, zScale);

surface.SetData(nativePtr);      // 从非托管内存加载数据
surface.SetIntensity(nativePtr); // 从非托管内存加载强度
CxPoint3D[] pts = surface.ToPoints(); // 转换为世界坐标点数组
```

**`CxMesh`** — 三角面片网格

```csharp
var mesh = new CxMesh
{
    Vertices      = new CxPoint3D[n],  // 顶点世界坐标
    Indices       = new uint[m * 3],   // 三角形索引（每三个为一个面片）
    UVs           = new CxPoint2D[n],  // 强度纹理 UV 坐标
    Intensity     = new byte[w * h],   // 强度纹理像素（W×H 网格）或逐顶点（压缩）
    TextureWidth  = w,
    TextureHeight = h,
};
```

`SurfaceToMesh` 生成的 mesh：
- `generateUVs=false`：`Intensity` 为压缩逐顶点格式（`length = validCount`），供 `CxMeshItem` 固定管线渲染
- `generateUVs=true`：`Intensity` 为 W×H 网格格式（`length = W×H`，无效格填 0），供 `CxMeshAdvancedItem` Shader 路径渲染

**`CxImage<T>`** — 泛型 2D 图像

```csharp
var img = new CxImage<byte>(640, 480);
// 行主序访问：img.Data[row * img.Width + col]
```

**`CxMatrix4X4`** — 4×4 行主序矩阵（`Data[i*4+j]` = 第 i 行第 j 列；传给 OpenGL 时需先转置）

</details>

### 算子 API

```csharp
// 计算点云重心（调用本地 C++ 库）
CxPoint3D center = VisionOperator.GetPoint3DArrayCenter(points);

// 无序点云 → 均匀结构化表面（CPU，本地库）
CxSurface result = VisionOperator.UniformSurface(
    points, intensity, width, height,
    xScale, yScale, zScale, xOffset, yOffset, zOffset);

// 4×4 矩阵变换（OpenGL 列主序）
CxPoint3D transformed = VisionOperator.TransformPoint3D(point, matrix);

// 并行包围盒（Parallel.ForEach）
Box3D? box = VisionOperator.CalculateBoundingBox(points);

// SIMD 包围盒（Vector3.Min / Max）
Box3D? boxFast = VisionOperator.CalculateBoundingBoxSIMD(points);

// GPU 表面变换（OpenCL，需先调用 InitialLib）
VisionOperator.InitialLib();
CxSurface transformed = VisionOperator.TransformSurface(surface, matrix, SampleMode.Max);
VisionOperator.DestroyLib();

// Mesh → Surface 高度图投影（全自动，Box3D 范围从 mesh 包围盒推导）
CxSurface heightMap = VisionOperator.MeshToSurface(mesh, matrix, 0.01f, 0.01f);

// Mesh → Surface 高度图投影（指定固定 Box3D 范围，用于对齐多帧）
CxSurface heightMapFixed = VisionOperator.MeshToSurface(mesh, matrix, bounds, 0.01f, 0.01f);

// Surface → Mesh 三角网格转换（generateUVs=true 时含 UV + W×H 纹理强度）
CxMesh mesh = VisionOperator.SurfaceToMesh(surface, generateUVs: true);
```

#### OpenCL GPU 计算模块（`VisionNet.Compute`）

| 类 | 说明 |
|----|------|
| `OpenCLEnvironment` | 单例，管理 OpenCL 上下文、命令队列、已编译程序和 Kernel |
| `OpenCLComputation` | 抽象基类，封装 Buffer 分配、Kernel 参数绑定和 NDRange 执行 |
| `CxUniformSurface` | GPU 点云均匀重采样，支持 Max / Min / Average 聚合模式 |
| `CxTransformSurface` | GPU 结构化表面矩阵变换，返回变换后的点云 |
| `CxMeshToSurface` | GPU 网格自动栅格化，将三角 mesh 投影到指定位姿和分辨率的 CxSurface |

```csharp
// 直接使用底层 GPU 采样（绕过 VisionOperator 包装）
VisionOperator.InitialLib();

var sampler = new CxUniformSurface();
CxSurface gridded = sampler.Sample(points, intensity,
    width: 500, height: 500,
    xScale: 0.1f, yScale: 0.1f, zScale: 0.001f,
    xOffset: 0f, yOffset: 0f, zOffset: 0f,
    SampleMode.Average);

var transformer = new CxTransformSurface(matrix);
var (pts, intensities) = transformer.Transform(surface);

// GPU 网格 → 高度图投影
var projector = new CxMeshToSurface(mesh);
CxSurface projected = projector.Project(matrix, 0.01f, 0.01f);

VisionOperator.DestroyLib();
```

### CxDisplay 控件

#### 公共 API 速查

| 方法 | 语义 | 说明 |
|------|------|------|
| `SetPointCloud(surface)` | 替换 | 显示点云，清空原有表面 |
| `SetMesh(mesh)` | 替换 | 显示网格 |
| `SetSurfaceAdvancedItem(surface)` | 替换 | 高性能 Shader 路径，≤ 200 万点 |
| `SetMeshAdvancedItem(mesh)` | 替换 | 高性能 Shader 路径网格 |
| `AddPointCloud(surface)` | 追加 | 叠加点云，不清空已有内容 |
| `AddMesh(mesh)` | 追加 | 叠加网格 |
| `AddSurfaceAdvancedItem(surface)` | 追加 | 叠加高性能点云 |
| `AddMeshAdvancedItem(mesh)` | 追加 | 叠加高性能网格 |
| `AddSurfaceItem(item)` | 追加 | 叠加自定义渲染 Item |
| `ClearSurfaceItems()` | — | 清空所有表面对象 |
| `SetSegment(segs, color, size)` | 追加 | 添加线段叠加层 |
| `SetPoint(pts, color, size, shape)` | 追加 | 添加点叠加层 |
| `SetPolygon(polys, color, size)` | 追加 | 添加多边形叠加层 |
| `SetPlane(planes, color, size)` | 追加 | 添加平面叠加层 |
| `SetBox(boxes, color, size)` | 追加 | 添加包围盒叠加层 |
| `SetTextInfo(infos, color)` | 追加 | 添加世界坐标文本标签 |
| `SetText2D(texts, color)` | 追加 | 添加屏幕空间文本 |
| `SetCoordinate3DSystem(coord, length)` | 追加 | 添加坐标系指示器 |
| `ResetView(resetAll)` | — | 重置视图（可选是否清空表面） |
| `SetViewCenter(point)` | — | 设置相机旋转焦点 |
| `SetViewUpDirection(dir)` | — | 设置相机上向量 |

#### 渲染模式

| 属性 | 可选值 | 说明 |
|------|--------|------|
| `SurfaceMode` | `PointCloud` `Mesh` | 点云或三角面片 |
| `SurfaceColorMode` | `Color` `Intensity` `ColorWithIntensity` | 颜色来源 |
| `SurfaceViewMode` | `Top` `Front` `Left` `Right` `None` | 预设视角 |
| `ShowCoordinateSystem` | `bool` | 显示世界坐标轴 |

颜色梯度（`Color` / `ColorWithIntensity` 模式，Z 由低到高）：

```
■ 深蓝  →  ■ 天空蓝  →  ■ 绿  →  ■ 黄  →  ■ 红  →  ■ 粉  →  ■ 白
```

多个表面叠加时，颜色条与各 Item 自动同步至**全局 Z 范围**。

#### 鼠标交互

| 操作 | 效果 |
|------|------|
| 左键拖拽 | 追踪球旋转（以最近表面点为轴心） |
| 中键拖拽 | 平移（速度自适应场景大小） |
| 滚轮 | 缩放（±5% 每格） |
| 左键双击 | 焦点对准点击处 |
| 鼠标悬停 | 显示世界坐标 X / Y / Z / 强度标签 |
| 右键菜单 | 切换视角 / 渲染模式 / 颜色模式 |

---

## 渲染 Item 体系

| 类 | 渲染路径 | 适用场景 |
|----|---------|---------|
| `CxSurfaceItem` | 固定管线（VBO + 颜色数组） | 中小规模结构化表面 |
| `CxSurfaceAdvancedItem` | VAO + GLSL + 强度纹理 | 大规模点云（自动降采样至 ≤ 200 万）|
| `CxMeshItem` | 固定管线（VBO + 颜色数组） | 中小规模三角网格 |
| `CxMeshAdvancedItem` | VAO + GLSL + 强度纹理 | 高性能三角网格 |
| `CxPoint3DItem` | 点精灵 / 实例化球体 | 离散点集 |
| `CxSegment3DItem` | `GL_LINES` | 线段集合 |
| `CxPolygon3DItem` | `GL_LINE_LOOP` / `GL_LINE_STRIP` | 开放 / 闭合多边形 |
| `CxPlane3DItem` | `GL_QUADS` | 平面区域 |
| `CxBox3DItem` | `GL_QUADS` + `GL_LINES` | 半透明填充 + 线框包围盒 |
| `CxColorBarItem` | 2D 正交 HUD | Z 高度颜色条 |
| `CxCoordinateSystemItem` | 圆柱 + 圆锥 | 坐标轴（世界空间 + 屏幕左下角）|
| `CxCoordinationTagItem` | 2D 正交 HUD | 鼠标悬停坐标标签 |
| `CxTextInfoItem` | 世界坐标投影 | 世界锚定文本 |
| `CxText2DItem` | 2D 正交 HUD | 屏幕固定文本 |

---

## 注意事项

> **GL 资源线程约束**
> 所有 OpenGL 对象（VBO / VAO / Shader / Texture）只能在创建它们的 GL 线程中释放。
> 不要在 `Dispose()` 中直接调用 GL 删除函数——`CxDisplay` 通过内部的 `_pendingRelease` 队列在下一渲染帧内统一回收。

> **大数据自动降采样**
> `SetPointCloud` / `AddPointCloud`：点数超过 **1 亿** 时自动降采样至 1000 万。
> `SetSurfaceAdvancedItem` / `AddSurfaceAdvancedItem`：最大处理 **200 万** 点。
> GPU 纹理超过硬件最大尺寸时也会自动进行双线性下采样。

> **多 Item 颜色一致性**
> 叠加多个表面时，颜色条与每个 Item 的颜色映射统一采用**所有 Item 的全局 Z 范围**，无需手动同步。

> **线程安全**
> `SetPointCloud` 等数据设置 API 可在后台线程调用；GL 资源的创建与释放由 `CxDisplay` 在渲染线程内完成。

---

## License

[MIT](LICENSE)
