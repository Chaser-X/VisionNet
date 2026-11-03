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

## P2 PBR / IBL（预烘焙资产）

- [ ] 新增 `enum SurfaceLightingMode { Legacy, Pbr }` + `CxDisplay.SurfaceLightingMode`（默认 `Legacy`）
- [ ] 材质参数：`Metallic` / `Roughness` / `SpecularIntensity`（item 级，纯 uniform）
- [ ] 离线烘焙并内嵌 `irradiance` cubemap、`prefilter` cubemap（带 mip）、`brdf` LUT（原始二进制 + 极简头）
- [ ] `ModernGLRenderer`：IBL 纹理上传/释放、环境预设切换（2~3 套），纳入 `ReleasePrograms`
- [ ] 片元 Cook-Torrance GGX：`albedo` = 现有数据映射色，`F0 = mix(0.04, albedo, metallic)`，
      漫反射 = irradiance × albedo，镜面 = prefilter(`textureLod` roughness) × `(F0*brdf.x + brdf.y)`
- [ ] 纹理单元：0 = intensity，1 = irradiance cube，2 = prefilter cube，3 = brdf LUT
- [ ] 文档（README「渲染模式」/ CHANGELOG）+ 右键菜单项（`Enum.Parse` 依赖 Text == 枚举名）

## P3 点云伪 3D 高光球（point-sprite impostor）

- [ ] 点云路径保持 `GL_POINTS` + `gl.PointSize`，片元用 `gl_PointCoord` 反解球面法线
- [ ] `gl_FragDepth` 球面深度修正；`discard` 圆形外像素
- [ ] 与 PBR 共用光照；`Legacy` 下也给出简单高光
- [ ] （P3.5 可选）大点超过驱动点尺寸上限时，降级到已有实例化球体路径（`DrawSpheres`）
- [ ] 密集点云性能实测（`gl_FragDepth` 关闭 early-Z 的影响）

## P4（独立专项）回读性能

- [ ] 量化每帧全分辨率 `glReadPixels` + `BitBlt` 开销（4K ≈ 33 MB/帧）
- [ ] 评估 `RenderContextType.NativeWindow` 的可行性与 GDI/半透明语义代价
- [ ] 结论可能反向影响 P2/P3 的取舍

## 开放项 / 待确认

- [ ] PBR 开关粒度：先做 `CxDisplay` 全局（默认），per-item 覆盖留后续
- [ ] 环境预设：2~3 套内置；是否需要「加载外部 cubemap」API
- [ ] P3 是否直接带大点实例化降级
