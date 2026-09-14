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

| 库                 | 说明                                                            |
| ----------------- | ------------------------------------------------------------- |
| **VisionNet.dll** | 核心数据类型（点云、网格、几何体）+ 静态算子 API（通过 P/Invoke 调用本地 C++ 库）           |
| **CxControl.dll** | 基于 SharpGL/OpenGL 的高性能 3D 渲染控件 `CxDisplay` + 基于 ScottPlot 的 2D 渲染控件 `CxDisplay2D`，支持点云、网格、2D/3D 几何叠加层的实时渲染 |

### 主要特性

- 🗂️ 丰富的 3D/2D 几何数据类型，内存布局与 C++ 互操作兼容
- ⚡ 高性能 3D 渲染：VAO + GLSL Shader + 强度纹理，支持百万级点云实时显示
- 📈 高性能 2D 渲染：基于 ScottPlot，支持坐标轴自动缩放、自适应视野裁剪
- 🎨 多 Item 叠加：点云、网格、2D/3D 几何图元可同时显示，颜色条自动同步全局 Z / Diff 范围
- 🎨 **差分伪彩（Diff）**：为 Mesh 提供每顶点差分值，按彩虹色映射显示偏差，颜色条自动跟随差分范围；混合场景自动隐藏颜色条以避免误导
- 🖱️ 完整鼠标交互：追踪球旋转、平移、缩放、双击对焦、悬停坐标标签
- 🔒 线程安全：GL 资源延迟释放机制，数据更新可在后台线程执行
- 💾 **文件 I/O**：自定义紧凑二进制格式（`.cxsurface` / `.cxpc` / `.cxmesh`）及标准工业格式 OBJ（`.obj`）、STL（`.stl` / `.stla`）的保存/加载
- ✂️ **ROI 裁剪**：`ClipMesh` / `ClipPointCloud` / `ClipSurface` — 以 `CxBox3D` 为 ROI 对三种数据类型做空间裁剪，全程并行加速
- 🚀 **OpenCL GPU 计算**：并行包围盒计算、GPU 点云重采样（`CxUniformSurface`）、GPU 表面变换（`CxTransformSurface` / `CxTransformPointCloud`）、GPU 网格栅格化（`CxMeshToSurface`）
- 🔄 **坐标系切换**：一行代码在右手系与左手系之间切换，视角预设（Top / Front / Left / Right）自动适配，无需修改数据
- 🔧 **几何算子**：2D/3D 几何构造（直线/平面）、求交、投影、距离计算；坐标系对齐（Align 正向/反向）与坐标变换
- 🖼️ **图像处理**：`CxImage ↔ Bitmap` 互转（自适应像素格式）、OpenCV 缩放/缩略图、`Surface → Image` 导出
- 📐 **最小二乘拟合**：2D 圆拟合、2D/3D 直线拟合、平面拟合、球拟合

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
- [文件格式](#文件格式)
- [注意事项](#注意事项)

---

## 环境要求

| 依赖             | 版本      | 说明                                  |
| -------------- | ------- | ----------------------------------- |
| Windows        | 10 / 11 | 仅支持 Windows                         |
| .NET Framework | 4.8     | 目标框架                                |
| Visual Studio  | 2019+   | 推荐开发环境                              |
| OpenGL         | 3.3+    | 需要独立显卡或支持 OpenGL 3.3 的集成显卡          |
| OpenCL         | 1.2+    | GPU 计算（可选，无 OpenCL 设备时自动降级到 CPU 路径） |
| VisionLib.dll  | —       | 本地 C++ 算法库，仅 x64                    |
| OpenCL.Net.dll | —       | OpenCL .NET 绑定，位于 `3rd/` 目录         |

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

### 3. 显示结构化表面 / 点云

```csharp
using VisionNet;
using VisionNet.DataType;
using VisionNet.Controls;

// 构造结构化高度图（short[] 编码，-32768 = 无效点）
var surface = new CxSurface(
    width: 500, length: 500,
    data: heightData,          // short[]，Z 高度
    intensity: intensityData,  // byte[]，强度 0–255（可为 null）
    xOffset: 0f, yOffset: 0f, zOffset: 0f,
    xScale: 0.1f, yScale: 0.1f, zScale: 0.001f);

cxDisplay1.SetSurface(surface);              // 结构化表面
// 或：有序点云
cxDisplay1.SetPointCloud(cloud);            // CxPointCloud
// 或：高性能 Shader 路径
cxDisplay1.SetSurfaceAdvancedItem(surface); // 结构化
cxDisplay1.SetPointCloudAdvancedItem(cloud);// 点云
```

### 4. 叠加多个对象

```csharp
// 追加第二个结构化表面（不清空已有内容）
cxDisplay1.AddSurface(surface2);

// 叠加包围盒
cxDisplay1.SetBox(new[] {
    new CxBox3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 5))
}, Color.Yellow);

// 叠加线段
cxDisplay1.SetSegment(new[] {
    new CxSegment3D(new CxPoint3D(0, 0, 0), new CxPoint3D(5, 5, 5))
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
│   │   │   ├── Geometry3D/         # CxPoint3D, CxVector3D, CxBox3D, CxPose3D ...
│   │   │   ├── Geometry2D/         # CxPoint2D, CxCoordination2D, CxMatrix3X3, CxCircle2D, CxRegion2D ...
│   │   │   └── Models/             # CxSurface, CxPointCloud, CxMesh, CxImage, CxMatrix4X4
│   │   ├── Analysis/               # 统计分析 / 拟合算子
│   │   │   └── VisionOperator.Analysis.cs  # FitPointsToCircle2D, FitPointsToPlane ...
│   │   ├── Compute/                # OpenCL GPU 计算模块
│   │   │   ├── Kernels/            # .cl 内核源码（嵌入资源）
│   │   │   ├── OpenCLEnvironment   # 单例：上下文 / 命令队列 / 编译程序
│   │   │   ├── CxUniformSurface    # GPU 点云均匀重采样
│   │   │   ├── CxTransformSurface  # GPU 表面变换
│   │   │   ├── CxTransformPointCloud# GPU 点云变换
│   │   │   └── CxMeshToSurface     # GPU 网格栅格化
│   │   ├── Filter/                 # 滤波器算子
│   │   │   ├── VisionOperator.Filter.cs
│   │   │   └── VisionOperator.Clip.cs
│   │   ├── Geometry/               # 几何构造 / 求交 / 距离算子
│   │   │   └── VisionOperator.Geometry.cs  # CreateLine2D, IntersectPlanePlane ...
│   │   ├── Image/                  # 图像处理算子
│   │   │   └── VisionOperator.Image.cs     # ResizeImage, ToBitmap, FromBitmap
│   │   ├── IO/                     # 文件序列化
│   │   │   └── VisionOperator.IO.cs        # SaveSurface / LoadMesh / LoadObj / STL 等
│   │   ├── Surface/                # 表面处理算子
│   │   │   └── VisionOperator.Surface.cs   # SurfaceToMesh, CreateImageFromSurface
│   │   ├── Transform/              # 坐标系对齐算子（2D/3D）
│   │   │   ├── VisionOperator.Transform.cs     # TransformPoint3D, TransformSurface
│   │   │   ├── VisionOperator.Transform.2D.cs  # AlignCircle2D, AlignPolygon2D ...
│   │   │   └── VisionOperator.Transform.3D.cs  # AlignPlane3D, AlignSphere ...
│   │   ├── VisionOperator.cs       # 静态算子 API 入口
│   │   └── Export.cs               # P/Invoke 声明（VisionLib.dll）
│   └── Controls/
│       └── CxControl/              # 渲染控件库
│           ├── 3D/                 # 3D 渲染（OpenGL）
│           │   ├── Camera/         # ICamera, CxAdvancedTrackBallCamera
│           │   ├── RenderItem/
│           │   │   ├── Surface/    # CxSurfaceItem, CxSurfaceAdvancedItem
│           │   │   ├── PointCloud/ # CxPointCloudItem, CxPointCloudAdvancedItem
│           │   │   ├── Mesh/       # CxMeshItem, CxMeshAdvancedItem
│           │   │   ├── Geometry/   # Point / Segment / Polygon / Plane / Box
│           │   │   └── Overlay/    # ColorBar, CoordinateSystem, Tag, Text
│           │   └── CxDisplay.cs   # 状态 / 初始化 / 框架
│           │       ├── CxDisplay.Api.cs
│           │       ├── CxDisplay.Render.cs
│           │       ├── CxDisplay.GLResources.cs
│           │       ├── CxDisplay.Input.cs
│           │       └── CxDisplay.Designer.cs
│           └── 2D/                 # 2D 渲染（ScottPlot）
│               ├── CxDisplay2D.cs           # 2D 控件状态 / 渲染
│               ├── CxDisplay2D.Api.cs       # 公共 API（Set* / Add* / 视图管理）
│               ├── CxDisplay2D.Render.cs    # 轴范围 / 缩放控制
│               └── RenderItem2D/
│                   ├── Image/      # CxImageItem, CxImageItemAdvance
│                   └── Geometry/   # Point / Segment / Line / Circle / Arc / Polygon / FittingField / Region ...
├── Test/
│   ├── Test/                       # 控制台单元测试
│   └── DemoFrom/                   # WinForms 演示应用
└── 3rd/                            # 第三方 DLL（SharpGL, OpenCvSharp, ScottPlot 等）
```

### CxDisplay / CxDisplay2D 设计原则

#### CxDisplay（3D，OpenGL）

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

#### CxDisplay2D（2D，ScottPlot）

- **基于 ScottPlot**：每个 2D 渲染 Item 包装为 ScottPlot `IPlottable`，由 `CxDisplay2D` 统一管理
- **自适应裁剪**：无限长直线（`CxLine2DItem`）根据当前 `AxisLimits` 动态计算线段端点，始终刚好覆盖视野 + 10% 边距
- **交互式几何 Item**：圆弧 / 矩形 / 线段 / 多边形 / 拟合场等支持鼠标拖拽编辑（控制点、宽度手柄、旋转手柄），命中优先级：宽度手柄 → 顶点/中点 → 图形内部 → 边缘
- **像素空间命中**：控制点与选中态标记按**像素大小**绘制（不随缩放变化），命中阈值同样以像素计
- **虚拟画布图像（`CxImageItemAdvance`）**：大图渲染采用"全局缩略图 + 视口高清瓦片"双层结构，`RefreshViewport()` 只在视口移动超过 50% 时重新裁剪瓦片，高倍缩放保持清晰且无错位
- **双击对焦**：双击任意位置将视图中心移动到点击处（`DoubleClick` 事件 + `Control.MousePosition` 换算局部坐标，移除默认双击基准线）
- **非侵入式**：不修改 ScottPlot 内部逻辑，仅通过标准 API（`Add.Scatter` / `Add.Arrow` / `Add.Text` / `Add.ImageRect`）叠加图元

---

## API 参考

### 数据类型

<details>
<summary><b>3D 几何类型（VisionNet.DataType）</b></summary>

| 类型                 | 说明                                                   |
| ------------------ | ---------------------------------------------------- |
| `CxPoint3D`        | 3D 点（X, Y, Z）—— `StructLayout.Explicit`，可直接与 C++ 互操作 |
| `CxPoint3DI`       | 带强度的 3D 点                                            |
| `CxVector3D`       | 3D 向量，支持 `+` `-` `×` `÷` `Dot` `Cross` `Normalize` `FromSpherical` |
| `CxSize3D`         | 3D 尺寸（Width, Height, Depth）                          |
| `CxBox3D`          | 轴对齐包围盒（Center + Size）                                |
| `CxPlane3D`        | 平面（Point + Normal）                                   |
| `CxSphere`         | 球体（Center + Radius）                                  |
| `CxCircle3D`       | 3D 圆（Center + Normal + Radius）                       |
| `CxSegment3D`      | 线段（Start + End）                                      |
| `CxPolygon3D`      | 多边形顶点序列（`IsClosed` 控制是否闭合）                           |
| `CxCoordination3D` | 3D 坐标系（Origin + XAxis + YAxis + ZAxis）               |
| `CxPose3D`         | 6-DOF 位姿（X/Y/Z + Rx/Ry/Rz，旋转为度，外在 Z-Y-X 约定）     |
| `CxTextInfo`       | 世界坐标锚定文本标签（Location + Text + Size）                   |

</details>

<details>
<summary><b>2D 几何类型</b></summary>

| 类型                         | 说明                                 |
| -------------------------- | ---------------------------------- |
| `CxPoint2D` / `CxVector2D`  | 2D 点 / 向量                          |
| `CxCoordination2D`         | 2D 坐标系（Origin + Scale + Angle 度）    |
| `CxMatrix3X3`              | 3×3 行主序矩阵（旋转 / 缩放 / 平移 / 求逆 / 克莱默 2×2） |
| `CxSegment2D`               | 2D 线段                              |
| `CxLine2D`                 | 2D 无限长直线（`FromTwoPoints` / `FromGeneralForm(A,B,C)` / `FromPointSlope`） |
| `CxArc2D`                  | 2D 圆弧（Center + Radius + StartAngle + SweepAngle） |
| `CxPolygon2D`               | 2D 多边形                             |
| `CxCircle2D`                | 2D 圆                               |
| `CxBox2D`                  | 2D 轴对齐包围盒（Center + Size）           |
| `CxRectangle2D`            | 2D 带角度的矩形（Center + Size + Angle）   |
| `CxRegion2D`               | RLE 区域（`CxRun` 行程 + 面积/包围盒/包含判定、轮廓与区域互转） |
| `CxSegment2DFittingField` / `CxArc2DFittingField` / `CxPolygon2DFittingField` / `CxCircle2DFittingField` | 边缘查找拟合场（ROI 带 + 宽度，供寻边算子使用） |
| `CxSize2D`                 | 2D 尺寸（Width + Height）                |
| `CxText2D`                  | 屏幕空间文本（Location + Text + FontSize） |

</details>

<details>
<summary><b>主数据模型</b></summary>

**`CxSurface`** — 结构化高度图

```csharp
// Data 长度 = Width × Length，每个元素为 Z 高度（short），-32768 = 无效点

var surface = new CxSurface(width, length, data, intensity,
    xOffset, yOffset, zOffset, xScale, yScale, zScale);

surface.SetData(nativePtr);      // 从非托管内存加载数据
surface.SetIntensity(nativePtr); // 从非托管内存加载强度
CxPoint3D[] pts = surface.ToPoints(); // 转换为世界坐标点数组
```

**`CxPointCloud`** — 有序点云

```csharp
// Data 长度 = Width × Length × 3，每格存 (X, Y, Z) 三元组，-32768 = 无效点
// 保留完整网格拓扑（Width × Length），可生成三角形索引和强度纹理

var cloud = new CxPointCloud(width, length, data, intensity,
    xOffset, yOffset, zOffset, xScale, yScale, zScale);

cloud.SetData(nativePtr);       // 从非托管内存加载 XYZ 三元组
cloud.SetIntensity(nativePtr);  // 从非托管内存加载强度
CxPoint3D[] pts = cloud.ToPoints(); // 解码为世界坐标点数组
```

**`CxMesh`** — 三角面片网格

```csharp
var mesh = new CxMesh
{
    Vertices      = new CxPoint3D[n],  // 顶点世界坐标
    Indices       = new uint[m * 3],   // 三角形索引（每三个为一个面片）
    UVs           = new CxPoint2D[n],  // 强度纹理 UV 坐标
    Intensity     = new byte[w * h],   // 强度纹理像素（W×H 网格）或逐顶点（压缩）
    Diff          = new float[n],      // 每顶点差分值（可选），用于 SurfaceColorMode.Diff 模式
    TextureWidth  = w,
    TextureHeight = h,
};
```

> **差分伪彩（Diff 模式）**：`CxMesh` / `CxSurface` / `CxPointCloud` 各自带可选
> `float[] Diff` 字段（与 `Intensity` 同位同模式；Mesh 为 `float[Vertices.Length]`
> 逐顶点，Surface / PointCloud 为 `float[Width*Length]` 网格点）。设为
> `SurfaceColorMode.Diff` 即按差分值伪彩；需要多对象对齐同一量程时，调用
> `CxDisplay.SetColorRange` / `SetDiffRange`（配合 `ClearColorRange` /
> `ClearDiffRange` 复位）。

`SurfaceToMesh` 生成的 mesh：

- `generateUVs=false`：`Intensity` 为压缩逐顶点格式（`length = validCount`），供 `CxMeshItem` 固定管线渲染
- `generateUVs=true`：`Intensity` 为 W×H 网格格式（`length = W×H`，无效格填 0），供 `CxMeshAdvancedItem` Shader 路径渲染

**`CxImage`** — 2D 图像容器

```csharp
// 支持 byte[] / short[] / int[] / float[] 四种像素类型
var img = new CxImage(w, h, PlainType.UInt8, channel: 1);
var img = new CxImage(w, h, data, channel: 3);   // 从已有数组构造

img.ToBitmap()                                    // → System.Drawing.Bitmap (Format32bppArgb)
img.GetThumbnail(maxW, maxH)                      // → 等比例缩略图
```

**`CxMatrix4X4`** — 4×4 行主序矩阵（`Data[i*4+j]` = 第 i 行第 j 列；传给 OpenGL 时需先转置），旋转角度均为**度**

```csharp
var m = CxMatrix4X4.RotationZ(45f);                      // Z 轴旋转 45°
var r = CxMatrix4X4.RotationAxis(axis, 30f);             // 罗德里格任意轴旋转 30°
var t = CxMatrix4X4.Translation(1, 2, 3);                // 平移
var s = CxMatrix4X4.Scale(0.5f, 1f, 2f);                 // 缩放
var v = CxMatrix4X4.LookAt(eye, center, up);             // 视图矩阵

var product = m * t;                                      // 矩阵乘法
var inv     = product.Inverse();                          // 高斯-约旦求逆
var trans   = product.Transpose();                        // 转置
var pt      = product.TransformPoint3D(point);            // 点变换（含透视除除）
var vec     = product.TransformVector3D(vector);          // 向量变换（仅 3×3 子块）
var x       = product.Solve3x3(b);                        // 克莱默法则求解 3×3 线性方程组
```

**`CxPose3D`** — 6 自由度位姿（平移 + 旋转），旋转角度均为**度**；旋转约定为外在 Z-Y-X（`R = Rz·Ry·Rx`）

```csharp
var pose = new CxPose3D(1, 2, 3, 45f, 0f, 0f);           // 平移 (1,2,3) + 绕 X 旋转 45°
CxPose3D t = CxPose3D.FromTranslation(1, 2, 3);          // 仅平移
CxPose3D r = CxPose3D.FromRotation(0f, 0f, 90f);         // 仅旋转

CxMatrix4X4 mat = pose.ToMatrix();                       // 位姿 → 4×4 矩阵（度自动转弧度）
CxPose3D back  = CxPose3D.FromMatrix(mat);               // 矩阵 → 位姿（含万向锁处理，返回度）
```

**`CxMatrix3X3`** — 3×3 行主序矩阵，用于 2D 仿射变换（角度为度）

```csharp
var m = CxMatrix3X3.Rotation(45f);                        // 旋转 45°（逆时针，角度制）
var t = CxMatrix3X3.Translation(10, 20);                  // 平移
var s = CxMatrix3X3.Scale(0.5f, 2f);                      // 缩放
var i = CxMatrix3X3.Identity();                           // 单位矩阵

var product = m * t;                                      // 矩阵乘法
var inv     = product.Inverse();                          // 伴随矩阵求逆
var det     = product.Determinant;                        // 行列式
var trans   = product.Transpose();                        // 转置
var pt      = product.TransformPoint2D(point);            // 点变换（齐次坐标）
var vec     = product.TransformVector2D(vector);          // 向量变换（仅 2×2 子块）
var x       = product.Solve2x2(b);                        // 克莱默法则求解 2×2 线性方程组
```

</details>

### 算子 API

```csharp
// 计算点云重心（调用本地 C++ 库）
CxPoint3D center = VisionOperator.GetPoint3DArrayCenter(points);

// 无序点云 → 均匀结构化表面（CPU，本地库）
CxSurface result = VisionOperator.UniformSurface(
    points, intensity, width, height,
    xScale, yScale, zScale, xOffset, yOffset, zOffset);

// 4×4 矩阵变换（行主序 M·v，与 TransformPoint3D 实例方法 / OpenCL kernel 语义一致）
CxPoint3D transformed = VisionOperator.TransformPoint3D(point, matrix);

// 并行包围盒（Parallel.ForEach）
CxBox3D? box = VisionOperator.CalculateBoundingBox(points);

// SIMD 包围盒（Vector3.Min / Max）
CxBox3D? boxFast = VisionOperator.CalculateBoundingBoxSIMD(points);

// GPU 表面变换（OpenCL，需先调用 InitialLib）
VisionOperator.InitialLib();
CxSurface transformed = VisionOperator.TransformSurface(surface, matrix);
// 指定输出网格分辨率：
CxSurface transformedFine = VisionOperator.TransformSurface(surface, matrix,
    SampleMode.Max, xScale: 0.005f, yScale: 0.005f);
VisionOperator.DestroyLib();

// GPU 点云矩阵变换（方案 A：复用 TransformVertices kernel）
var (pts, intensities) = VisionOperator.TransformPointCloud(cloud, matrix);

// Mesh → Surface 高度图投影（全自动，CxBox3D 范围从 mesh 包围盒推导）
CxSurface heightMap = VisionOperator.MeshToSurface(mesh, matrix, 0.01f, 0.01f);

// Mesh → Surface 高度图投影（指定固定 CxBox3D 范围，用于对齐多帧）
CxSurface heightMapFixed = VisionOperator.MeshToSurface(mesh, matrix, bounds, 0.01f, 0.01f);

// Surface → Mesh 三角网格转换（结构化表面）
CxMesh mesh = VisionOperator.SurfaceToMesh(surface, generateUVs: true);

// PointCloud → Mesh 三角网格转换（有序点云）
CxMesh meshFromCloud = VisionOperator.PointCloudToMesh(cloud, generateUVs: true);

// ── ROI 裁剪（CxBox3D，CPU 并行） ──────────────────────────────────

var roi = new CxBox3D(new CxPoint3D(0, 0, 0), new CxSize3D(10, 10, 5));

// 保留三顶点全在 ROI 内的三角形，压缩顶点索引，UV/Intensity 随顶点保留
CxMesh clippedMesh = VisionOperator.ClipMesh(mesh, roi);

// 越界点设为 -32768（无效），保留 Width × Length 网格结构
CxPointCloud clippedCloud = VisionOperator.ClipPointCloud(cloud, roi);

// 按 XY 网格裁剪（返回更小网格），Z 越界单元设为 -32768
CxSurface clippedSurface = VisionOperator.ClipSurface(surface, roi);

// ── 2D/3D 几何变换（VisionOperator.Transform.*） ───────────────

// 2D 坐标系对齐（CxCoordination2D）
var l2w = VisionOperator.AlignCircle2D(circle, coord, false);     // 反向 Local→World
var w2l = VisionOperator.AlignCircle2D(circle, coord, true);      // 正向 World→Local

// 3D 坐标系对齐（CxCoordination3D）
var l2wPt = VisionOperator.AlignPoint3D(pt, coord3d, false);      // 反向
var w2lLn = VisionOperator.AlignLine3D(line, coord3d, true);      // 正向

// 支持：Point2D/3D, Vector2D/3D, Segment2D/3D, Line2D/3D,
//       Circle2D/Arc2D/Polygon2D/Rectangle2D,
//       Plane3D/Sphere/Circle3D/Polygon3D/Box3D/TextInfo,
//       Segment/Arc/Polygon/Circle2DFittingField

// ── 2D 几何构造与计算（VisionOperator.Geometry） ────────────────

VisionOperator.CreateLine2D(p1, p2, out var line);                  // 两点构造直线
VisionOperator.Line2DOrientation(line, AngleMode.Signed180, out var a); // 直线角度（-180~180）
VisionOperator.Segment2DLength(seg, out var len);                   // 线段长度
VisionOperator.Segment2DMidpoint(seg, out var mid);                 // 线段中点

if (VisionOperator.IntersectLineLine2D(l1, l2, out var pt))         // 线线求交
    Console.WriteLine($"Intersection: ({pt.X}, {pt.Y})");

VisionOperator.ProjectPointToLine2D(p, line, out var proj);         // 点投影到直线
VisionOperator.DistancePointToLine2D(p, line, out var dist);        // 点线距

// ── 2D 包围盒 / 凸包（VisionOperator.Geometry） ──────────────────

VisionOperator.RectangleBoundingBox2D(rect, out var rbox);          // 旋转矩形 → 轴对齐包围盒
VisionOperator.PolygonBoundingBox2D(poly, out var pbox);            // 多边形 → 轴对齐包围盒
VisionOperator.PointsBoundingBox2D(pts, out var ptbox);             // 点集 → 轴对齐包围盒
VisionOperator.PointsBoundingRectangle2D(pts, out var orect);       // 点集 → 最小面积有向矩形（角度=度）
VisionOperator.PolygonBoundingRectangle2D(poly, out var orect2);    // 多边形 → 最小面积有向矩形
VisionOperator.ConvexHull2D(pts, out var hull);                     // 点集凸包（Andrew 单调链，逆时针）

// 3D
VisionOperator.CreatePlane(p1, p2, p3, out var plane);             // 三点构造平面
if (VisionOperator.IntersectPlanePlane(p1, p2, out var line3d))    // 平面求交线
    { /* line3d.Point, line3d.Direction */ }
VisionOperator.ProjectPointToPlane(pt, plane, out var projPt);     // 点投影到平面
VisionOperator.DistancePointToPlane3D(pt, plane, out var d);       // 点面距
VisionOperator.ClosestPointOnLine(pt, line3d, out var closest);     // 点到直线最近点

// ── 统计拟合（VisionOperator.Analysis） ──────────────────────────

if (VisionOperator.FitPointsToCircle2D(pts, out var circle))       // 圆拟合
    Console.WriteLine($"Center=({circle.Center.X},{circle.Center.Y}) R={circle.Radius}");

if (VisionOperator.FitPointsToLine2D(pts, out var line2d))         // 直线拟合（PCA）
    Console.WriteLine($"Dir=({line2d.Direction.X},{line2d.Direction.Y})");

if (VisionOperator.FitPointsToPlane(pts, out var plane3d))         // 平面拟合（PCA）
    Console.WriteLine($"Normal=({plane3d.Normal.X},{plane3d.Normal.Y},{plane3d.Normal.Z})");

if (VisionOperator.FitSphere(pts, out var sphere))                 // 球拟合
    Console.WriteLine($"Center=({sphere.Center.X},...) R={sphere.Radius}");

// ── 图像处理（VisionOperator.Image） ─────────────────────────────

var thumb = image.GetThumbnail(128, 128);                          // 等比例缩略图
using (var bmp = VisionOperator.ToBitmap(image))                   // CxImage → Bitmap
    bmp.Save(@"C:\output.bmp");

using (var src = new Bitmap(@"C:\input.bmp"))                      // Bitmap → CxImage
{
    var img = VisionOperator.FromBitmap(src);                      // 自适应像素格式
    // img.Type == UInt8/Int16, img.Channel == 1/3/4
}

// ── Surface → Image ─────────────────────────────────────────────

VisionOperator.CreateImageFromSurface(surface, true, out var img);  // Real 浮点（Z = ZOffset+Data×ZScale）
VisionOperator.CreateImageFromSurface(surface, false, out var img); // Int16 原始高度

// ── 文件 I/O（自动按扩展名分支） ─────────────────────────────────

// 保存
VisionOperator.SaveSurface(surface, @"C:\data.cxsurface");     // 二进制 .cxsurface
VisionOperator.SavePointCloud(cloud, @"C:\data.cxpc");          // 二进制 .cxpc
VisionOperator.SavePointCloud(cloud, @"C:\data.pcd");           // PCL PCD ASCII
VisionOperator.SaveMesh(mesh, @"C:\data.cxmesh");               // 二进制 .cxmesh
VisionOperator.SaveMesh(mesh, @"C:\data.obj");                  // Wavefront OBJ
VisionOperator.SaveMesh(mesh, @"C:\data.stl");                  // STL 二进制
VisionOperator.SaveMesh(mesh, @"C:\data.stla");                 // STL ASCII

// 加载（文件不存在返回 null，格式错误抛 InvalidDataException）
CxSurface s = VisionOperator.LoadSurface(@"C:\data.cxsurface");
CxPointCloud c = VisionOperator.LoadPointCloud(@"C:\data.cxpc");
CxPointCloud c2 = VisionOperator.LoadPointCloud(@"C:\data.pcd");// 自动识别 .pcd
CxMesh m = VisionOperator.LoadMesh(@"C:\data.cxmesh");
CxMesh m2 = VisionOperator.LoadMesh(@"C:\data.obj");            // 自动识别 .obj
CxMesh m3 = VisionOperator.LoadMesh(@"C:\data.stl");            // 自动识别 .stl
```

#### OpenCL GPU 计算模块（`VisionNet.Compute`）

| 类                       | 说明                                          |
| ----------------------- | ------------------------------------------- |
| `OpenCLEnvironment`     | 单例，管理 OpenCL 上下文、命令队列、已编译程序和 Kernel         |
| `OpenCLComputation`     | 抽象基类，封装 Buffer 分配、Kernel 参数绑定和 NDRange 执行   |
| `CxUniformSurface`      | GPU 点云均匀重采样，支持 Max / Min / Average 聚合模式     |
| `CxTransformSurface`    | GPU 结构化表面矩阵变换，返回变换后的点云                      |
| `CxTransformPointCloud` | GPU 有序点云矩阵变换（复用 TransformVertices kernel）   |
| `CxMeshToSurface`       | GPU 网格自动栅格化，将三角 mesh 投影到指定位姿和分辨率的 CxSurface |

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

| 方法                                     | 语义  | 说明                  |
| -------------------------------------- | --- | ------------------- |
| `SetSurface(surface)`                  | 替换  | 显示结构化表面，清空原有内容      |
| `SetPointCloud(cloud)`                 | 替换  | 显示有序点云，清空原有内容       |
| `SetSurfaceAdvancedItem(surface)`      | 替换  | 高性能结构化表面路径，≤ 200 万点 |
| `SetSurfaceAdvancedItem(surface, pose)`| 替换  | 同上，带初始位姿（`ModelMatrix`） |
| `SetPointCloudAdvancedItem(cloud)`     | 替换  | 高性能有序点云路径，≤ 200 万点  |
| `SetPointCloudAdvancedItem(cloud, pose)`| 替换  | 同上，带初始位姿             |
| `SetMesh(mesh)`                        | 替换  | 显示网格                |
| `SetMeshAdvancedItem(mesh)`            | 替换  | 高性能 Shader 路径网格     |
| `SetMeshAdvancedItem(mesh, pose)`      | 替换  | 同上，带初始位姿             |
| `AddSurface(surface)`                  | 追加  | 叠加结构化表面             |
| `AddPointCloud(cloud)`                 | 追加  | 叠加有序点云              |
| `AddSurfaceAdvancedItem(surface)`      | 追加  | 叠加高性能结构化表面          |
| `AddSurfaceAdvancedItem(surface, pose)`| 追加  | 同上，带初始位姿             |
| `AddPointCloudAdvancedItem(cloud)`     | 追加  | 叠加高性能有序点云           |
| `AddPointCloudAdvancedItem(cloud, pose)`| 追加  | 同上，带初始位姿             |
| `AddMesh(mesh)`                        | 追加  | 叠加网格                |
| `AddMeshAdvancedItem(mesh)`            | 追加  | 叠加高性能网格             |
| `AddMeshAdvancedItem(mesh, pose)`      | 追加  | 同上，带初始位姿             |
| `SetSurfaceAdvancedItemPose(pose)`     | —    | 运行时更新所有 Surface 位姿    |
| `SetPointCloudAdvancedItemPose(pose)`  | —    | 运行时更新所有 PointCloud 位姿 |
| `SetMeshAdvancedItemPose(pose)`        | —    | 运行时更新所有 Mesh 位姿      |
| `AddSurfaceItem(item)`                 | 追加  | 叠加自定义渲染 Item        |
| `ClearSurfaceItems()`                  | —   | 清空所有表面对象            |
| `SetSegment(segs, color, size)`        | 追加  | 添加线段叠加层             |
| `SetPoint(pts, color, size, shape)`    | 追加  | 添加点叠加层              |
| `SetPolygon(polys, color, size)`       | 追加  | 添加多边形叠加层            |
| `SetPlane(planes, color, size)`        | 追加  | 添加平面叠加层             |
| `SetBox(boxes, color, size)`           | 追加  | 添加包围盒叠加层            |
| `SetTextInfo(infos, color)`            | 追加  | 添加世界坐标文本标签          |
| `SetText2D(texts, color)`              | 追加  | 添加屏幕空间文本            |
| `SetCoordinate3DSystem(coord, length)` | 追加  | 添加坐标系指示器            |
| `SetCoordinateSystemLeftHanded(bool)`  | —   | 切换左手 / 右手坐标系（默认右手）  |
| `ResetView(resetAll)`                  | —   | 重置视图（可选是否清空表面）      |
| `SetViewCenter(point)`                 | —   | 设置相机旋转焦点            |
| `SetViewUpDirection(dir)`              | —   | 设置相机上向量             |

#### 渲染模式

| 属性                     | 可选值                                      | 说明      |
| ---------------------- | ---------------------------------------- | ------- |
| `SurfaceMode`          | `PointCloud` `Mesh`                      | 点云或三角面片 |
| `SurfaceColorMode`     | `Color` `Intensity` `ColorWithIntensity` `Diff` | 颜色来源；Diff 为每顶点差分彩虹伪彩 |
| `SurfaceViewMode`      | `Top` `Front` `Left` `Right` `None`      | 预设视角    |
| `ShowCoordinateSystem` | `bool`                                   | 显示世界坐标轴 |
| `IsLeftHanded`         | `bool`                                   | 左手坐标系（默认 `false`） |

`Diff` 模式复用 `getColorByHeight` 彩虹映射，`ColorMin/ColorMax`  自动切换为差分范围；SetGlobalZRange 早返回，不受外部 Z 同步影响。

颜色梯度（`Color` / `ColorWithIntensity` / `Diff` 模式）：

```
■ 深蓝  →  ■ 天空蓝  →  ■ 绿  →  ■ 黄  →  ■ 红  →  ■ 粉  →  ■ 白
```

多个表面叠加时，颜色条与各 Item 自动同步至**全局 Z 范围**。

#### 鼠标交互

| 操作   | 效果                      |
| ---- | ----------------------- |
| 左键拖拽 | 追踪球旋转（以最近表面点为轴心）        |
| 中键拖拽 | 平移（速度自适应场景大小）           |
| 滚轮   | 缩放（±5% 每格）              |
| 左键双击 | 焦点对准点击处                 |
| 鼠标悬停 | 显示世界坐标 X / Y / Z / 强度标签 |
| 右键菜单 | 切换视角 / 渲染模式 / 颜色模式      |

### CxDisplay2D 控件

2D 渲染控件，基于 ScottPlot，面向机器视觉 ROI 标注与测量：

```csharp
var display2D = new CxDisplay2D();
display2D.Dock = DockStyle.Fill;
this.Controls.Add(display2D);

display2D.SetImage(image);                      // 显示图像（等比例适配，DisplayMode.None 时不自动适配）
display2D.SetImageAdvance(image);               // 虚拟画布大图模式（缩略图 + 视口高清瓦片）
display2D.SetScaleAndOffset(new CxPoint3D(1, 1, 1), new CxPoint3D(0, 0, 0)); // 像素→世界缩放 / 偏移
display2D.SetAspectLock(true);                  // 锁定 X/Y 宽高比（避免像素变形）
display2D.SetBackgroundColor(Color.Black);
```

支持叠加以下可交互 2D Item：线段、直线、圆弧、圆、矩形、包围盒、多边形、点、坐标系、边缘查找拟合场、RLE 区域、文本；图像 Item 支持鼠标取像素值（`GetPixelFloat`）。双击视图居中到点击处。

---

## 渲染 Item 体系

| 类                          | 渲染路径                             | 适用场景                     |
| -------------------------- | -------------------------------- | ------------------------ |
| `CxSurfaceItem`            | 固定管线（VBO + 颜色数组）                 | 中小规模结构化表面                |
| `CxSurfaceAdvancedItem`    | VAO + GLSL + 强度纹理                | 大规模结构化表面（自动降采样至 ≤ 200 万） |
| `CxPointCloudItem`         | 固定管线（VBO + 颜色数组）                 | 中小规模有序点云                 |
| `CxPointCloudAdvancedItem` | VAO + GLSL + 强度纹理                | 大规模有序点云（自动降采样至 ≤ 200 万）  |
| `CxMeshItem`               | 固定管线（VBO + 颜色数组）                 | 中小规模三角网格                 |
| `CxMeshAdvancedItem`       | VAO + GLSL + 强度纹理                | 高性能三角网格                  |
| `CxPoint3DItem`            | 点精灵 / 实例化球体                      | 离散点集                     |
| `CxSegment3DItem`          | `GL_LINES`                       | 线段集合                     |
| `CxPolygon3DItem`          | `GL_LINE_LOOP` / `GL_LINE_STRIP` | 开放 / 闭合多边形               |
| `CxPlane3DItem`            | `GL_QUADS`                       | 平面区域                     |
| `CxBox3DItem`              | `GL_QUADS` + `GL_LINES`          | 半透明填充 + 线框包围盒            |
| `CxColorBarItem`           | 2D 正交 HUD                        | Z 高度颜色条                  |
| `CxCoordinateSystemItem`   | 圆柱 + 圆锥                          | 坐标轴（世界空间 + 屏幕左下角）        |
| `CxCoordinationTagItem`    | 2D 正交 HUD                        | 鼠标悬停坐标标签                 |
| `CxTextInfoItem`           | 世界坐标投影                           | 世界锚定文本                   |
| `CxText2DItem`             | 2D 正交 HUD                        | 屏幕固定文本                   |
| `CxCoordination2DItem`    | X/Y 箭头 + 标签                      | 2D 坐标系指示器                  |
| `CxImageItem`            | ScottPlot ImageRect                | 2D 图像（全局缩略图渲染）           |
| `CxImageItemAdvance`     | 虚拟画布 + 视口裁剪（双层 ImageRect）       | 2D 大图高倍缩放（全局缩略图 + 视口高清瓦片） |
| `CxLine2DItem`            | ScottPlot Scatter                  | 2D 无限长直线（自适应视野裁剪）    |
| `CxSegment2DItem`         | ScottPlot Scatter                  | 2D 线段（选中态显示端点 + 方向箭头） |
| `CxCircle2DItem`          | ScottPlot Ellipse                  | 2D 圆（可交互缩放半径）           |
| `CxArc2DItem`             | ScottPlot Ellipse 圆弧               | 2D 圆弧（三点交互）               |
| `CxPolygon2DItem`         | ScottPlot Polygon                  | 2D 多边形 / 折线                |
| `CxRectangle2DItem`       | ScottPlot Rectangle                | 2D 旋转矩形（顶点 + 顶部旋转手柄交互） |
| `CxBox2DItem`             | ScottPlot Rectangle                | 2D 轴对齐包围盒                 |
| `CxPoint2DItem`           | ScottPlot Scatter                  | 2D 离散点集                    |
| `CxSegment2DFittingFieldItem` / `CxArc2DFittingFieldItem` / `CxPolygon2DFittingFieldItem` / `CxCircle2DFittingFieldItem` | ScottPlot Polygon/Scatter | 边缘查找拟合场（ROI 带 + 宽度手柄，可拖动调节） |
| `CxRegion2DItem`          | ScottPlot Polygon                  | 2D RLE 区域（轮廓渲染，可选填充）  |
| `CxText2DItem`            | ScottPlot Text                     | 2D 屏幕空间文本                  |

---

## 注意事项

> **角度约定**
> 全库统一使用**度**作为角度单位：`CxRectangle2D.Angle`、`CxArc2D.StartAngle/SweepAngle`、`CxCoordination2D.Angle`、`CxPose3D.Rx/Ry/Rz`、`CxMatrix4X4.RotationX/Y/Z/RotationAxis` 及 2D/3D 方向角均为度。
> `CxMatrix4X4.Rotation*` 内部自动完成度 → 弧度换算；`CxPose3D.ToMatrix/FromMatrix` 度 → 弧度互转。

> **GL 资源线程约束**
> 所有 OpenGL 对象（VBO / VAO / Shader / Texture）只能在创建它们的 GL 线程中释放。
> 不要在 `Dispose()` 中直接调用 GL 删除函数——`CxDisplay` 通过内部的 `_pendingRelease` 队列在下一渲染帧内统一回收。

> **大数据自动降采样**
> `SetPointCloud` / `AddPointCloud`：点云点数超过 **1 亿** 时自动降采样至 1000 万结构化表面。
> `SetSurfaceAdvancedItem` / `AddSurfaceAdvancedItem` / `SetPointCloudAdvancedItem` / `AddPointCloudAdvancedItem。
> GPU 纹理超过硬件最大尺寸时也会自动进行双线性下采样。

> **多 Item 颜色一致性**
> 叠加多个表面时，颜色条与每个 Item 的颜色映射统一采用**所有 Item 的全局 Z 范围**，无需手动同步。
> Diff 模式 Item 使用差分范围而非 Z 范围；混合场景（Diff + 非 Diff 共存）时颜色条自动隐藏。

> **线程安全**
> `SetPointCloud` 等数据设置 API 可在后台线程调用；GL 资源的创建与释放由 `CxDisplay` 在渲染线程内完成。

---

---

## 文件格式

VisionNet 使用自定义紧凑二进制格式和标准工业格式（OBJ / STL）。

### 二进制格式（紧凑、快速、零依赖）

| 扩展名 | 数据类型 | Magic |
|--------|----------|-------|
| `.cxsurface` | `CxSurface` | `CXSRF1` |
| `.cxpc` | `CxPointCloud` | `CXPC01` |
| `.cxmesh` | `CxMesh` | `CXMSH1` |

### PCD 格式（PCL 点云互通）

- 导出：`SavePointCloud(cloud, "*.pcd")` → ASCII v0.7，含可选的 intensity 字段
- 导入：`LoadPointCloud("*.pcd")` → 支持 **ASCII** 和 **binary** 两种 DATA 模式
- 类型自适应：binary 模式按 `TYPE`/`SIZE`（`F`/`U`/`I`，1/2/4 字节）自动解码字段
- 坐标编码：加载时自动推导 Offset/Scale 阈值，编码为内部 `short[]`，NaN 映射为无效点
- 不支持 `binary_compressed` |

- 使用 `BinaryWriter` / `BinaryReader`，无外部依赖
- `short[]` / `uint[]` 通过 `Buffer.BlockCopy` 批量读写，性能高
- 6 字节 Magic 头 + 校验，可防御格式混淆

### OBJ 格式（工业互通）

- 导出：`SaveMesh(mesh, "*.obj")` → UV 正确映射、无 BOM 的 UTF-8
- 导入：`LoadMesh("*.obj")` → 支持 `v` / `v/vt` / `v//vn` / `v/vt/vn` 四种面格式、负索引、多边形三角化
- 法线自动忽略

### STL 格式（3D 打印 / CAD 互通）

| 扩展名 | 格式 | 说明 |
|--------|------|------|
| `.stl`  | Binary | 50 字节 / 三角面片，工业软件通用格式 |
| `.stla` | ASCII | 可读文本，`solid/facet/vertex/endfacet/endsolid` 结构 |

- 法线由三角面片两条边的叉积自动计算，导出时自动归一化
- 导入时顶点不共享（STL 规范），生成 3N 顶点 + 顺序索引 `[0,1,2, 3,4,5, ...]`
- 仅支持三角形网格（`Indices` 长度须为 3 的倍数）

---

## License

[MIT](LICENSE)
