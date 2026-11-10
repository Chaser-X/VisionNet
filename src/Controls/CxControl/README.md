# CxControl 项目结构说明

## 1. 总体结构
`CxControl` 主要包含 3D/2D 渲染、网格、几何体等可视化能力，基于 OpenGL（**OpenTK 3.3.3**）实现。核心控件为 `CxDisplay`，负责渲染管线与数据管理。

## 2. 主要文件与职责

`3D/` 顶层只保留 `CxDisplay` 本体（`.cs` / `.Render` / `.Input` / `.Api` / `.Group` / `.Designer` + `.resx`），其余按职责归入子目录：

- **3D/CxDisplay.*（控件本体）**
  核心控件。派生自 `OpenTK.GLControl`，负责渲染管线（渲染到离屏 MSAA 目标 → resolve 到窗口 → `SwapBuffers`）、数据管理、用户交互（鼠标、菜单等），并协调 RenderItem 层显示。

- **3D/Camera/**
  `CxTrackBallCamera.cs`：3D 相机，支持旋转、缩放、平移、2D/3D 切换与预设视图；`ICamera.cs` 为接口。

- **3D/RenderItem/**
  渲染元素（点、线、面、Mesh、Box、文本、坐标系等）：
  - `ComplexGeometry/`：`CxSurfaceItem.cs`（曲面）、`CxPointCloudItem.cs`（点云）、`CxMeshItem.cs`（三角网格）；`AbstractCxObjRenderItem.cs` 为共享基类。
  - `Geometry/`：`CxSegment3DItem.cs`（3D 线段）、`CxPoint3DItem.cs`（3D 点）、`CxBox3DItem.cs`（3D 包围盒）、`CxPolygon3DItem.cs`、`CxPlane3DItem.cs`。
  - `Overlay/`：`CxColorBarItem.cs`（色条）、`CxCoordinationTagItem.cs`（坐标标签）、`CxCoordinateSystemItem.cs`（坐标系）、`CxTextInfoItem.cs`（3D 文本）、`CxText2DItem.cs`（2D 文本）。
  - `Shared/`：`ModernGLRenderer*.cs`（全部渲染项共用的 per-context GL 服务）、`ShaderProgramCompiler.cs`、`ProceduralIbl.cs`。

- **3D/GL/**
  GL 后端基础设施：`CxGlContext.cs`（绑定无关的上下文门面；`EnsureCurrent` / `RunOnContext` 是「帧外 GL 工作」的唯一入口；`DepthTarget` 指向离屏深度源）、`CxGLResourceManager.cs`（VAO / VBO / 索引 / 纹理 / shader 的创建与释放）、`CxMsaaRenderTarget.cs`（离屏 MSAA 渲染目标 + 1×1 单采样深度目标 + PBO 异步回读）。

- **3D/Scene/**
  场景模型：`CxItemRegistry.cs`（唯一持有 surface / overlay 队列与 key、per-item GL 资源池与延迟释放队列）、`CxGroup.cs` / `CxGroupManager.cs`（组拓扑、成员关系与姿态）、`IDisplayHost.cs`（display ↔ 组管理器的窄接口）。

- **3D/Interaction/**
  `CxPickingService.cs`：深度读回（hover 走 PBO 异步、点击同步）、反投影、最近曲面搜索（UI 线程、渲染帧内）。

- **3D/Common/**
  公共类型与工具：`CxExtension.cs`（包围盒、颜色映射、3D/2D 文本绘制、OpenGL 能力探测）、`CxDisplayDefaults.cs`（新建 `CxDisplay` 及其 item 的默认值集中管理，含 `PointSize` / `MsaaEnabled` / `Background`）、`CxBackground.cs`（背景值类型：纯色 / 垂直渐变）。

- **2D/**
  `CxDisplay2D` 及其 RenderItem（基于 ScottPlot / SkiaSharp）。

- **CxControl.csproj**
  项目文件（.NET Framework 4.8；引用 `3rd/OpenTK.dll` 与 `3rd/OpenTK.GLControl.dll`）。

## 3. 推荐扩展方式

- 新 RenderItem 只负责几何与渲染，GL 资源生命周期交给 `CxDisplay`。
- `CxDisplay` 只负责相机、绘制与渲染调度。
- 复杂渲染项优先复用 `RenderItem/Shared/ModernGLRenderer` 的共享 program / VAO，不要各自创建 GL 对象。
- 建议补全 XML 注释和 `#region`，提高可读性。
- 本文档是项目内部结构说明；对外 API 与线程/重绘约定以根目录 `README.md` 为准。

---
