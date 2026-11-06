# CxControl 项目结构说明

## 1. 总体结构
`CxControl` 主要包含 3D/2D 渲染、网格、几何体等可视化能力，基于 OpenGL（**OpenTK 3.3.3**）实现。核心控件为 `CxDisplay`，负责渲染管线与数据管理。

## 2. 主要文件与职责

- **CxDisplay.cs / .Render.cs / .Input.cs / .Api.cs / .Group.cs / .Designer.cs**
  核心控件。派生自 `OpenTK.GLControl`，负责渲染管线（直绘 `OnPaint` + `SwapBuffers`）、数据管理、用户交互（鼠标、菜单等），并协调 RenderItem 层显示。

- **CxGlContext.cs**
  绑定无关的 GL 上下文门面：对外只暴露 `Width` / `Height` / `IsValid`，隐藏 OpenTK 类型；`EnsureCurrent` / `RunOnContext` 是「帧外 GL 工作」的唯一入口。

- **CxItemRegistry.cs**
  唯一持有 surface / overlay 队列与 key、per-item GL 资源池与延迟释放队列（含守护它们的锁）。

- **CxGroupManager.cs / CxGroup.cs**
  组拓扑、成员关系与姿态。

- **CxGLResourceManager.cs**
  VAO / VBO / 索引缓冲 / 纹理 / shader 的创建与释放（静态、无状态）。

- **CxPickingService.cs**
  深度读回、反投影、最近曲面搜索（UI 线程、渲染帧内）。

- **Camera/**
  `CxAdvancedTrackBallCamera.cs`：现代 3D 相机，支持旋转、缩放、平移、2D/3D 切换与预设视图；`CxTrackBallCamera.cs` 为遗留实现；`ICamera.cs` 为接口。

- **RenderItem/**
  渲染元素（点、线、面、Mesh、Box、文本、坐标系等）：
  - `ComplexGeometry/`：`CxSurfaceItem.cs`（曲面）、`CxPointCloudItem.cs`（点云）、`CxMeshItem.cs`（三角网格）；`AbstractCxObjRenderItem.cs` 为共享基类。
  - `Geometry/`：`CxSegment3DItem.cs`（3D 线段）、`CxPoint3DItem.cs`（3D 点）、`CxBox3DItem.cs`（3D 包围盒）、`CxPolygon3DItem.cs`、`CxPlane3DItem.cs`。
  - `Overlay/`：`CxColorBarItem.cs`（色条）、`CxCoordinationTagItem.cs`（坐标标签）、`CxCoordinateSystemItem.cs`（坐标系）、`CxTextInfoItem.cs`（3D 文本）、`CxText2DItem.cs`（2D 文本）。
  - `Shared/`：`ModernGLRenderer*.cs`（全部渲染项共用的 per-context GL 服务）、`ShaderProgramCompiler.cs`、`ProceduralIbl.cs`。

- **CxExtension.cs**
  扩展工具（包围盒、颜色映射、3D/2D 文本绘制、OpenGL 能力探测）。

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
