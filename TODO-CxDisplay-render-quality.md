# CxDisplay（3D）渲染质量提升待办（PBR / 高光球）

> 范围：`src/Controls/CxControl/3D/`（不含 CxDisplay2D）。
> 目标：提升有序点云与 Mesh 的立体观感，同时不破坏计量读数的可回归性。
> 原则：小步、按阶段、每阶段独立可回归；提交代码前先与负责人确认。
> 已确认的前提：
> - 目标硬件/驱动能提供 ≥3.3 兼容上下文（现有 `#version 330 core` + VAO 已实证）。
> - 允许光照/高光调制数据映射颜色（`Color` / `Intensity` / `Diff`）。
> - **不做 EDL**（点云是有序的，且高光球已提供立体感，边际收益不足）。
> - IBL 资产**离线预烘焙**并随程序分发（不引入运行期预滤波与 HDR/DDS 解码器）。
> - 结论：**不需要引入自建 FBO / 额外渲染 pass**，渲染目标仍是 SharpGL 的 FBO。

## 背景（现状基线）

- 共享 surface program：Lambert + Blinn-Phong，光向硬编码，法线靠 `cross(dFdx,dFdy)`（仅 Mesh）。
- 顶点属性：0=pos、1=uv/color、2=diff、3=intensity；**无法线**。
- `GLResourceHandle.VboIds` 固定长度 4；`RenderData` 无 `Normals`。
- SharpGL 3.1.1 能力：有 EXT FBO / `DrawBuffers` / 实例化 / cubemap 常量 / `UniformMatrix3`；
  **无 `glBlitFramebuffer`、无 UBO、无 `glTexStorage2D`、无图像加载器**。

## P1 法线通道 + Legacy 光照改用真法线 ✅（代码完成，待目视回归 + 提交确认）

- [x] `RenderData` 新增 `Normals`（`float[n×3]`）
- [x] `GLResourceHandle.VboIds` 4 → 5（含 `ResetForContextRecreate`），槽位 4 = 法线
- [x] `CxGLResourceManager.Create` 上传法线到 attribute 4
- [x] `CxExtension` 新增 `ComputeGridNormals`（有序网格）与 `ComputeTriangleNormals`（索引网格）
- [x] `CxSurfaceItem` / `CxPointCloudItem`：Mesh 模式下按采样网格算法线
- [x] `CxMeshItem`：Mesh 模式下由索引累积面法线
- [x] 共享 shader：`layout(location=4) in vec3 aNormal`、`uniform mat3 normalMatrix`、
      `uniform int useVertexNormal`；Mesh 光照优先用真法线，缺省回退 `dFdx/dFdy`
- [x] `ModernGLRenderer.UploadNormalMatrix`（用 item 缓存的 `ModelMatrixInverse`，不额外求逆）
- [x] `AbstractCxObjRenderItem.PrepareRenderData` 统一注入 `useVertexNormal` uniform
- [x] 新增 CPU 特征测试 `NormalGenerationTests`（10 项）
- [x] `dotnet test` 113/113 通过；`CxControl` / `DemoFrom` 0 错误
- [ ] **待办**：本机目视回归（Mesh 光照、带姿态 item、点云不受影响）后提交

> 注：P1 未新增公开枚举/方法；`Normals` 为 `RenderData`（内部管线数据）的新属性。

## P2 PBR / IBL ✅（代码完成，待目视回归 + 提交确认）

> 资产方式改为**程序内程序化生成**（`ProceduralIbl`，进程级缓存），不引入外部文件/解码器。

- [x] 新增 `enum SurfaceLightingMode { Legacy, Pbr }` + `CxDisplay.SurfaceLightingMode`（默认 `Legacy`，帧级全局）
- [x] 材质参数：`Metallic`（默认 0）/ `Roughness`（默认 0.5），item 级、纯 uniform
- [x] `ProceduralIbl`：解析式环境 → irradiance cubemap（16²）+ prefilter cubemap（32²×5 mip）+ BRDF LUT（128²）
- [x] `ModernGLRenderer.Ibl.cs`：IBL 纹理上传/释放、`EnvironmentPreset` 切换，`ReleaseIbl` 纳入 `ReleasePrograms`
- [x] 片元 split-sum GGX：`albedo` = 现有数据映射色，`F0 = mix(0.04, albedo, metallic)`，
      漫反射 = irradiance × albedo，镜面 = prefilter(`textureLod` roughness) × `(F*brdf.x + brdf.y)`
- [x] 纹理单元：0 = intensity，1 = irradiance cube，2 = prefilter cube，3 = brdf LUT
- [x] 新增测试 `ProceduralIblTests`（6）+ `PbrMaterialTests`（4）；`dotnet test` 123/123
- [x] 文档（README / CHANGELOG）
- [ ] **待办**：本机目视回归（`Pbr` 出图、`Legacy` 与 P1 一致）后提交
- [ ] **未做**：右键菜单的照明模式开关（`Enum.Parse` 依赖 Text == 枚举名）；`SpecularIntensity` / per-item 覆盖

## P3 点云伪 3D 高光球（point-sprite impostor）✅（代码完成，待目视回归 + 提交确认）

- [x] 点云路径保持 `GL_POINTS` + `gl.PointSize`，片元用 `gl_PointCoord` 反解球面法线
- [x] `discard` 圆盘外像素（方形 sprite 裁成圆）
- [x] 与 PBR / Legacy 共用光照（`pbrShade` / `legacyLightFactor` 改为接收法线）
- [x] `glEnable(GL_POINT_SPRITE)`（兼容 profile 必需，否则 `gl_PointCoord` 全 0 → 点全被裁）；`CxDisplay.PointImpostor`（默认 true）
- [x] `CxDisplay.PbrMetallic` / `PbrRoughness`（全局材质，新 item 继承）
- [x] Demo 新增「光照/PBR 测试」Tab：左侧控件（Legacy/PBR、Studio/Neutral、Impostor、**Metallic / Roughness / IBL 强度滑条**、Mesh/点云/**文件加载**）+ **右侧内嵌实时预览 CxDisplay**；控件同时作用于预览与两个主 CxDisplay
- [ ] **待办**：目视确认 `gl_PointCoord` 的 y 方向（不对就翻转 `pc.y` 符号，一行）后提交
- [ ] **未做（P3.5）**：`gl_FragDepth` 球面深度修正（会关 early-Z，密集点云有代价）；大点超过驱动尺寸上限时降级到实例化 quad

## P2/P3 修复：环境轴向 / 法线 / 预览 / 环境跟随相机 ✅

- [x] **A（bug）** 环境上方向 +Y → **+Z**，与场景/相机的 Z-up 一致
- [x] **B** 索引绕序统一为 **CCW（从 +Z 看）**（`GenerateMeshIndices` + Demo `BuildBumpyMesh` / Diff Demo）
- [x] **C** Demo 预览：新增球体预览 + 「Mesh 预览」斜置 3/4
- [x] **D** `CxDisplay.EnvironmentFollowsCamera`（默认 false，视空间采样）+ Demo 勾选框
- [x] 默认值保留调试值（metallic 0.2 / pointSize 3 / iblIntensity 1.5），测试与 Demo 滑条同步
- [x] `dotnet test` 123/123；`CxControl` / `DemoFrom` 0 错误
- [ ] **待办**：上机确认（顶面最亮 / 环境跟随相机）后提交

## 默认切换为 PBR/IBL + Demo 读 cxpc ✅

- [x] `CxDisplay` 默认：`Pbr` / `Neutral` / `IblIntensity 1.3` / `EnvironmentFollowsCamera true` / `PointImpostor true` / `PbrMetallic 0.2`
- [x] Demo「加载文件…」支持 `.cxpc` / `.pcd` 点云（`VisionOperator.LoadPointCloud`）
- [x] Demo 控件初值同步默认（PBR / Neutral / 环境跟随相机 / 1.3 / 0.2）
- [x] `dotnet test` 123/123；`CxControl` / `DemoFrom` 0 错误
- [ ] **待办**：上机确认后提交

## P4（独立专项）回读性能

- [ ] 量化每帧全分辨率 `glReadPixels` + `BitBlt` 开销（4K ≈ 33 MB/帧）
- [ ] 评估 `RenderContextType.NativeWindow` 的可行性与 GDI/半透明语义代价
- [ ] 结论可能反向影响 P2/P3 的取舍

## 开放项 / 待确认

- [ ] PBR 开关粒度：先做 `CxDisplay` 全局（默认），per-item 覆盖留后续
- [ ] 环境预设：2~3 套内置；是否需要「加载外部 cubemap」API
- [ ] P3 是否直接带大点实例化降级
