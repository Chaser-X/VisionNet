# CxControl 组件结构梳理

## 1. 总体结构
`CxControl` 主要用于3D/2D点云、网格、几何体等可视化，基于 OpenGL（SharpGL）实现。核心控件为 `CxDisplay`，负责渲染、交互和数据管理。

## 2. 主要文件与职责

- **CxDisplay.cs**  
  主控件，继承自 `OpenGLControl`，负责渲染流程、数据管理、用户交互（鼠标、菜单等），并协调各 RenderItem 的显示。

- **Camera/CxAdvancedTrackBallCamera.cs**  
  现代化3D相机，支持旋转、缩放、平移、2D/3D切换，负责视图矩阵设置。

- **RenderItem/**  
  渲染元素基类和各类图元（点、线、面、Mesh、Box、文本、坐标系等）：
  - `CxSegment3DItem.cs`：3D线段
  - `CxSurfaceItem.cs`：点云/表面
  - `CxMeshItem.cs`：三角网格
  - `CxSurfaceAdvancedItem.cs`：大规模点云/表面
  - `CxColorBarItem.cs`：色条
  - `CxCoordinationTagItem.cs`：坐标标签
  - `CxCoordinateSystemItem.cs`：坐标系
  - `CxTextInfoItem.cs`：3D文本
  - `CxText2DItem.cs`：2D文本
  - `CxPoint3DItem.cs`：3D点
  - `CxBox3DItem.cs`：3D包围盒

- **ICamera.cs**  
  相机接口，定义视图操作。

- **CxExtension.cs**  
  扩展方法、辅助工具。

- **CxDisplay.Designer.cs / .resx**  
  WinForms 设计器自动生成文件。

- **CxControl.csproj**  
  项目文件。

## 3. 推荐梳理建议

- 各 RenderItem 只负责自身图元的渲染与资源管理，便于扩展。
- `CxDisplay` 只负责数据流转、交互和渲染调度。
- 相机与渲染解耦，便于后续支持多种视图模式。
- 代码内建议补充 XML 注释和 `#region`，提升可读性。
- 可在本文件持续补充架构、用法、扩展说明。

---
