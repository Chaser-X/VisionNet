# ToDo

> 本文件是工作区任务的**汇总视图**，已合并原 「重构专项」（重构 S0–S6）与 「渲染质量专项」（渲染质量 P1–P4 / AA / 半透明）。
> 接入说明见 [Agents.md](Agents.md)，变更记录见 [Changlog.md](Changlog.md)。
> 快照日期：**2026-11-06** · 分支：`feature/cxdisplay-opentk-port`（OpenTK 迁移基线，自 `feature/cxdisplay-modern-pipeline` 拉出）

## 待办（当前活跃）

### A. 上机验证（本机无 OpenGL 环境，`dotnet test` 不覆盖任何渲染）

- [ ] **OpenTK 迁移回归**（最高优先，见 `Changlog.md` → `3c61519`）：
  - [ ] **兼容 profile 3.3 是否成立** —— 决定 `LineWidth > 1`（Box 边线 / 多边形）与 point sprite 是否生效（本次最大运行期风险）。**已加固**：`CxDisplay` 在上下文创建时（DEBUG 构建）输出 `[CxControl] GL context: <version> | renderer: … | aliased line width: [min, max]`，上机直接看这一行即可判定（见 `Changlog.md`「未发布」）
  - [ ] 点云 impostor、Mesh PBR/IBL（默认 `Neutral` + 环境跟随相机）、色带、坐标轴、悬停 tooltip、选择拾取、半透明、resize、多控件
  - [ ] 上下文重建（最小化/恢复、改分辨率）无花屏 / 崩溃 / 句柄泄漏
  - [ ] `CxExtension.IsOpenGLAvailable` / `GetOpenGLVersion`（一次性隐藏 `GameWindow` 探测）可用
  - [ ] 4K 帧率相对旧版的实际提升（量化）
  - [ ] **透明背景移除**行为确认并取得产品方签字（破坏性变更）
- [ ] PBR/IBL 正常出图（默认 `Neutral` + `EnvironmentFollowsCamera`）；Top 视角下物体**顶面应最亮**（环境天空在 +Z）
- [ ] 点云伪 3D 高光球的 `gl_PointCoord` y 方向（若高光在球下方 → 翻转 `pc.y` 符号，一行）
- [ ] 半透明（`Opacity < 1`）可选中、有坐标标签、不再前后颠倒
- [ ] Demo「加载文件…」能读 `.cxpc` / `.pcd` 点云并显示高光球
- [ ] 确认**目标机 GPU 级别**（是否集显）——影响抗锯齿与大点云的性能取舍
- [ ] 左手系预览：勾选后初始视图**居中**、拖动/平移自洽；取消勾选（右手系）无回归

### B. 库侧技术债

- [ ] S5 线程与锁：registry 回调移出锁内、缩小 `_renderLock` 范围、`Dispose` 非 UI 线程改 `BeginInvoke`（`MakeCurrent` 纪律已由 `CxGlContext.RunOnContext` 收敛，见「已完成」）
- [ ] S6 效能：色条/标签脏标记、Uniforms 强类型化、projection/view 每帧一次、overlay 合批、缓冲复用、遍历合并、拾取节流
- [ ] 清理 `[Obsolete]` 代理（`CxDisplay.cs`：`SurfaceViewMode` / `IsLeftHanded`，需先确认无外部调用）

### C. 渲染质量（未做）

- [ ] **抗锯齿 AA-1（SSAA 2×）/ AA-2（MSAA 4×）**：迁移到 OpenTK 3.3.3 后 `glBlitFramebuffer` 与多重采样 renderbuffer 已可用，可重新评估；需自建离屏 FBO + resolve pass 并动拾取链路
- [ ] **半透明对策 B**：按 item 距离排序 + 不写深度 + 悬停帧 depth-only pass（单深度缓冲下与「正确拾取」不可兼得）
- [ ] P2 未做：右键菜单照明模式开关（`Enum.Parse` 依赖菜单 Text == 枚举名）；`SpecularIntensity` / per-item 覆盖
- [ ] P3.5 未做：`gl_FragDepth` 球面深度修正（会关 early-Z）；大点超驱动尺寸上限时降级为实例化 quad
- [ ] 开放项：PBR 开关粒度（先全局、per-item 留后续）；环境预设 2~3 套 / 是否提供「加载外部 cubemap」API

## 进行中

- （无）

## 已完成

- [x] **OpenTK 迁移**：`CxDisplay` 渲染后端 SharpGL → OpenTK 3.3.3（直绘，去掉 FBO 回读）（提交 `3c61519`，归档 `07e0ec2`）
- [x] **迁移后加固（critic-in-the-loop 审计 P1/P2/P3/P5）**：`CxGlContext.RunOnContext` 收敛帧外 GL 绑定（P1）；删除残留 FFP 回退 `ReadCurrentGLMatrices` 与 `LookAtMatrix` 的矩阵栈段（P2）；`OnPaint` 改为先 `base.OnPaint` 再渲染（P3）；`CxGlContext` 单实例守卫（P5）；新增上下文能力日志，使「兼容 3.3 是否成立」可观测
- [x] **内部重构：`ModernGLRenderer` 自持 `_gl`**，移除 45 个实例方法的冗余 `CxGlContext` 参数（另含 `ShaderProgramCompiler.CompileProgram` / `CxGLResourceManager.Release` / `GLResourceHandle.GetUniformLocation` / `CxDisplay.ProcessPendingRelease` / `DisposeResources`）；纯内部、无公共 API 变更
- [x] **移除 SharpGL 残留**：删除 `3rd/SharpGL.dll` / `SharpGL.SceneGraph.dll` / `SharpGL.WinForms.dll`（已无任何引用）及 `bin` 下的陈旧副本
- [x] **清理 SharpGL 时代遗留文件**：删除 `CxControl.csproj_bak` / `.CxControl.csproj.swp_bak`（旧 csproj 备份，无任何引用）；更新 `src/Controls/CxControl/README.md`（SharpGL / `OpenGLControl` → OpenTK / `GLControl`，并补齐新增核心文件）
- [x] **工作区文档初始化**（2026-11-05）：创建 `Agents.md` / `Changlog.md` / `ToDo.md`，并合并 `CHANGELOG.md` 与两份 `TODO-CxDisplay-*.md` 后删除原文件
- [x] **P1 逐顶点法线**：Mesh 光照改用真法线（提交 `be069ee`）
- [x] **P2 PBR / IBL**：`SurfaceLightingMode` / `EnvironmentPreset` / `IblIntensity`；程序化 IBL（`ProceduralIbl`）；item 级 `Metallic` / `Roughness`（提交 `f98e426`）
- [x] **P3 点云伪 3D 高光球**：`gl_PointCoord` 球面法线 + `discard`；`PointImpostor`（提交 `f98e426`）
- [x] **修复**：环境轴向 Y-up→Z-up、索引绕序统一 CCW、Demo 预览球体/斜置（提交 `f98e426`）
- [x] **默认切换为 PBR/IBL**：`Neutral` + 环境跟随相机 + `IblIntensity 1.3` + `PbrMetallic 0.2`；Demo 支持读 `.cxpc`（提交 `8e6ab72`）
- [x] **`CxDisplay.SolidColor` / `Opacity`** + Demo「纯色 / 透明度」测试（提交 `b14d176`）
- [x] **半透明拾取修复（对策 A）**：`TranslucentWritesDepth`（默认 true）（提交 `6ac5042`）
- [x] **左手系相机修复** + Demo 手系开关（提交 `d89e081`）
- [x] **`Changlog.md` 归档积压清理**（2026-11-06）：`未发布` 中 8 条早已提交的条目已移入「已提交」并补哈希

## 阻塞 / 待确认

- [ ] **抗锯齿 AA-1 / AA-2（暂缓）**：AA-0（逐点 rim alpha）已试并回撤（小点云变虚）；SSAA 2× / MSAA 4× 已评估未做。约束：原 SharpGL 后端无 `glBlitFramebuffer` / MSAA provider；**迁移到 OpenTK 3.3.3 后这些限制已解除**，AA 可重新评估。详见 「渲染质量专项」 的「抗锯齿（AA）」
- [ ] **半透明对策 B（暂缓）**：若需「半透明互相透出且顺序正确」，需 ①按 item 距离排序 ②半透明不写深度 ③悬停帧 depth-only pass 供拾取。单深度缓冲下与「正确拾取」不可兼得
- [x] ~~**P4 回读性能专项**~~：**已由 OpenTK 迁移解决**——`CxDisplay` 改为直绘 `SwapBuffers`，删除 FBO + 每帧全分辨率 `glReadPixels` + `BitBlt`（4K ≈ 33 MB/帧）；拾取仍按需 `ReadPixels` 单像素。见 `Changlog.md`「已提交」→ `3c61519`
- [ ] **右键菜单照明模式开关**（`Enum.Parse` 依赖菜单 Text == 枚举名）
- [ ] 清理 `[Obsolete]` 代理（需先确认无外部调用）
- [x] ~~`RotateCamera` / `PanCamera` 补偿 `IsLeftHanded`~~：**已试并回退**——分析性共轭实测无效；根因是 Demo **数据约定不匹配**（右手系 Mesh 用了左手渲染），非相机缺陷，库保持原样

---

## 重构专项（S0–S6）

> 范围：`src/Controls/CxControl/3D/`（不含 CxDisplay2d）。
> 原则：小步、行为不变；每阶段独立提交并在 `Changlog.md` 归档。
> 本轮执行 **S0 / S1 / S2**；S3–S6 仅登记，不在本轮。

### S0 安全网

- [x] 特征测试：`GetNearestSurfacePoint` 纯算法部分（`SurfacePickNeighbourhoodTests`，10 项）
- [x] 特征测试：`CxColorBarItem` 刻度文本与 `divisions` 行为（`CxColorBarTickTests`，7 项）
- [x] 特征测试：`CxExtension` 三处包围盒收尾结果一致性（`CxBoundingBoxConsistencyTests`，5 项）
- [x] 性能基线：手动基准（每帧耗时 + Gen0 次数），结果记入 CHANGELOG（`CxDisplayCpuBenchmarkTests`）

### S1 低风险去重

- [x] `CxExtension.cs` 三处包围盒收尾抽 `BuildBox(...)`（并复用到 `CxDisplay.GetCombinedBoundingBox`）
- [x] 缓冲扩容统一为 `AbstractRenderItem.EnsureExactCapacity`（**精确**分配，非 grow-only —— 见 CHANGELOG 说明）
- [x] `PointToSegDistSq`（Polygon/Segment 各一份）抽到 `AbstractRenderItem`
- [x] 菜单 foreach 抽 `SyncMenuSelection` / `UncheckAllMenuItems`
- [x] `ModernGLRenderer.Geometry` 三份绘制流程抽 `BeginGeometryDraw` / `EndGeometryDraw`
- [x] 魔法数字提常量：`InitialPointSize`、色条 `TickDivisions`、`SphereSlices/Stacks`、`CxExtension.NoDataValue`
- [x] 5×5 邻域与阈值常量 —— 已挪到 `CxExtension`（`PickNeighbourhoodRadius` / `PickNeighbourhoodTolerance`），随 S2/S3 完成

### S3 多态替换类型分支

- [x] `AbstractCxObjRenderItem.PickAt` 虚方法 + 三个 item 各自实现，移除拾取路径的 `is CxMeshItem/CxPointCloudItem/CxSurfaceItem` 分支
- [x] `CxExtension.SearchSurfaceNeighbourhood` 从 `CxDisplay` 移到 `CxExtension`（纯几何工具，消除 item → display 依赖）
- [x] 共享程序判断改显式 `UsesSharedSurfaceProgram` 契约，删除 `IsSharedSurfaceShader` 的着色器文本比较
- [x] 删除 `GetItemInverseModelMatrix`（唯一调用点在拾取路径，逻辑已并入 `CxSurfaceItem.PickAt`）
- [x] 新增 `ItemPickAtTests`（7 项）

### S4 结构拆分

- [x] 抽出 `CxPickingService`（深度读回、反投影、最近曲面搜索），`CxDisplay.Input.cs` 只负责"何时问"
- [x] 抽出 `CxGLResourceManager`（`CxDisplay.GLResources.cs` → 独立静态类；`Create` 返回 bool 表示着色器失败）
- [x] 打破 `Display ⇄ GroupManager` 循环：`CxGroupManager(CxItemRegistry, IDisplayHost)`，不再持有 `CxDisplay`
- [x] `ModernGLRenderer._states` 冗余层清理（改为单一 `State` 字段）+ `Release` 吞异常改为 `Debug.WriteLine`
- [x] 新增 `CxPickingServiceTests`（6 项）

### S2 拆长方法

- [x] `CxDisplay.Render.cs` 的 `Render` 拆为 `AggregateFrameRanges` / `PropagateFrameRanges` / `DrawSurfaceItems` / `DrawOverlays` / `ResolvePendingPick` / `DrawHud`，色条 4 处重复 `SetRange+Draw` 收敛为 `TryResolveColorBarRange`
- [x] `CxDisplay.GLResources.cs` 的 `CreateGLResources` 抽 `UploadVertexBuffer(...)`（5 段样板合一）
- [x] `CxDisplay.Input.cs` 的 `GetNearestSurfacePoint` 拆出纯函数 `SearchSurfaceNeighbourhood`（去 GL 依赖，可直接单测）

### 后续待办

> 已并入顶部「待办 → B. 库侧技术债」，此处不再重复维护。

## 渲染质量专项（P1–P4 / AA / 半透明）

> 范围：`src/Controls/CxControl/3D/`（不含 CxDisplay2D）。
> 目标：提升有序点云与 Mesh 的立体观感，同时不破坏计量读数的可回归性。
> 原则：小步、按阶段、每阶段独立可回归；提交代码前先与负责人确认。
> 已确认的前提：
> - 目标硬件/驱动能提供 ≥3.3 兼容上下文（现有 `#version 330 core` + VAO 已实证）。
> - 允许光照/高光调制数据映射颜色（`Color` / `Intensity` / `Diff`）。
> - **不做 EDL**（点云是有序的，且高光球已提供立体感，边际收益不足）。
> - IBL 资产**离线预烘焙**并随程序分发（不引入运行期预滤波与 HDR/DDS 解码器）。
> - 结论（2026-11-06 更新）：迁移到 OpenTK 后渲染目标为**窗口默认帧缓冲（直绘）**；AA 等需要离屏 pass 的能力已具备（见「抗锯齿（AA）」）。

### 背景（现状基线）

- 共享 surface program：Lambert + Blinn-Phong，光向硬编码，法线靠 `cross(dFdx,dFdy)`（仅 Mesh）。
- 顶点属性：0=pos、1=uv/color、2=diff、3=intensity；**无法线**。
- `GLResourceHandle.VboIds` 固定长度 4；`RenderData` 无 `Normals`。
- ~~SharpGL 3.1.1 能力~~（历史）：有 EXT FBO / `DrawBuffers` / 实例化 / cubemap 常量 / `UniformMatrix3`；
  **无 `glBlitFramebuffer`、无 UBO、无 `glTexStorage2D`、无图像加载器**。
  2026-11-06 起后端为 **OpenTK 3.3.3**，上述缺失均已补齐。

### P1 法线通道 + Legacy 光照改用真法线 ✅（代码完成，待目视回归 + 提交确认）

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
- [x] 已提交（`be069ee`）；**待办**：本机目视回归（Mesh 光照、带姿态 item、点云不受影响）

> 注：P1 未新增公开枚举/方法；`Normals` 为 `RenderData`（内部管线数据）的新属性。

### P2 PBR / IBL ✅（代码完成，待目视回归 + 提交确认）

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
- [x] 已提交（`f98e426`）；**待办**：本机目视回归（`Pbr` 出图、`Legacy` 与 P1 一致）
- [ ] **未做**：右键菜单的照明模式开关（`Enum.Parse` 依赖 Text == 枚举名）；`SpecularIntensity` / per-item 覆盖

### P3 点云伪 3D 高光球（point-sprite impostor）✅（代码完成，待目视回归 + 提交确认）

- [x] 点云路径保持 `GL_POINTS` + `gl.PointSize`，片元用 `gl_PointCoord` 反解球面法线
- [x] `discard` 圆盘外像素（方形 sprite 裁成圆）
- [x] 与 PBR / Legacy 共用光照（`pbrShade` / `legacyLightFactor` 改为接收法线）
- [x] `glEnable(GL_POINT_SPRITE)`（兼容 profile 必需，否则 `gl_PointCoord` 全 0 → 点全被裁）；`CxDisplay.PointImpostor`（默认 true）
- [x] `CxDisplay.PbrMetallic` / `PbrRoughness`（全局材质，新 item 继承）
- [x] Demo 新增「光照/PBR 测试」Tab：左侧控件（Legacy/PBR、Studio/Neutral、Impostor、**Metallic / Roughness / IBL 强度滑条**、Mesh/点云/**文件加载**）+ **右侧内嵌实时预览 CxDisplay**；控件同时作用于预览与两个主 CxDisplay
- [x] 已提交（`f98e426`）；**待办**：目视确认 `gl_PointCoord` 的 y 方向（不对就翻转 `pc.y` 符号，一行）
- [ ] **未做（P3.5）**：`gl_FragDepth` 球面深度修正（会关 early-Z，密集点云有代价）；大点超过驱动尺寸上限时降级到实例化 quad

### P2/P3 修复：环境轴向 / 法线 / 预览 / 环境跟随相机 ✅

- [x] **A（bug）** 环境上方向 +Y → **+Z**，与场景/相机的 Z-up 一致
- [x] **B** 索引绕序统一为 **CCW（从 +Z 看）**（`GenerateMeshIndices` + Demo `BuildBumpyMesh` / Diff Demo）
- [x] **C** Demo 预览：新增球体预览 + 「Mesh 预览」斜置 3/4
- [x] **D** `CxDisplay.EnvironmentFollowsCamera`（默认 false，视空间采样）+ Demo 勾选框
- [x] 默认值保留调试值（metallic 0.2 / pointSize 3 / iblIntensity 1.5），测试与 Demo 滑条同步
- [x] `dotnet test` 123/123；`CxControl` / `DemoFrom` 0 错误
- [x] 已提交（`f98e426`）；**待办**：上机确认（顶面最亮 / 环境跟随相机）

### 默认切换为 PBR/IBL + Demo 读 cxpc ✅

- [x] `CxDisplay` 默认：`Pbr` / `Neutral` / `IblIntensity 1.3` / `EnvironmentFollowsCamera true` / `PointImpostor true` / `PbrMetallic 0.2`
- [x] Demo「加载文件…」支持 `.cxpc` / `.pcd` 点云（`VisionOperator.LoadPointCloud`）
- [x] Demo 控件初值同步默认（PBR / Neutral / 环境跟随相机 / 1.3 / 0.2）
- [x] `dotnet test` 123/123；`CxControl` / `DemoFrom` 0 错误
- [x] 已提交（`8e6ab72`）；**待办**：上机确认

### 半透明拾取修复（对策 A）✅

- [x] `AbstractCxObjRenderItem.TranslucentWritesDepth`（默认 true）：半透明默认写深度 → 深度拾取/遮挡正确、不再前后颠倒
- [x] `CxDisplay.TranslucentWritesDepth`（默认 true）应用到所有 item + 新 item 继承
- [x] Demo 勾选框「半透明写深度（拾取 / 标签可用，默认开）」
- [x] `dotnet test` 123/123；`CxControl` / `DemoFrom` 0 错误
- [x] 已提交（`6ac5042`）；**待办**：上机确认（`Opacity < 1` 可选中有标签、不颠倒）
- [ ] **未做（对策 B）**：若要"半透明互相透出且顺序正确"，需 ①半透明按 item 距离从远到近排序 ②半透明不写深度 ③悬停帧跑一次 depth-only pass 供拾取。单深度缓冲下"正确拾取"与"半透明互相混合"不可兼得

### 抗锯齿（AA）

- [x] **AA-0 已试 → 已回撤**：点云高光球边缘按 `fwidth(r)` 做覆盖率 alpha（约 1 像素）软化。
  - **结论：效果变差，已 `git restore` 回撤（未提交）**。原因：默认 `PointSize = 3` 时 `fwidth(r) ≈ 0.67`，`smoothstep(1 - fw, 1, r)` 的软化区覆盖了半径外侧约 **2/3**，点被糊开、整体变暗、密集点云互相混合发虚。
  - **教训**：按像素的**逐点 rim AA 不适合小点云**。若以后再做，应窄化到 ~0.35 像素，或仅当点足够大（`fwidth(r)` 足够小，如 ≥ ~7px）才启用。
- [ ] **AA-1 SSAA 2×**：**已评估，暂不做**（决定）。真超采样，不会让点发虚；代价 fill ×4 + 自建离屏 FBO + 下采样 pass + 拾取坐标 ×2。
- [ ] **AA-2 MSAA 4×**：自建多重采样 FBO（`TexStorage2DMultisample` + `RenderbufferStorageMultisampleEXT`）+ `sampler2DMS` resolve + `GL_SAMPLE_ALPHA_TO_COVERAGE`；**风险**：MS 附件深度不能用 `glReadPixels` 直接读，拾取需解析深度。
- **约束备忘（已更新）**：迁移到 OpenTK 3.3.3 后，`glBlitFramebuffer` 与多重采样 renderbuffer 均可用；渲染目标改为**窗口默认帧缓冲（直绘）**，不再有每帧 `glReadPixels` + `BitBlt`。AA 若要恢复，需自建离屏 FBO + resolve pass，并动拾取链路。

### P4（独立专项）回读性能

- [x] ~~量化每帧全分辨率 `glReadPixels` + `BitBlt` 开销（4K ≈ 33 MB/帧）~~：已随 OpenTK 迁移**移除**该路径。
- [x] ~~评估 `RenderContextType.NativeWindow` 的可行性与 GDI/半透明语义代价~~：迁移后直接使用窗口默认帧缓冲；代价是**放弃透明背景**（见 `Changlog.md`「未发布」破坏性变更）。
- [ ] 量化迁移后 4K 帧率相对旧版的实际提升（需上机）。

### 开放项 / 待确认

- [ ] PBR 开关粒度：先做 `CxDisplay` 全局（默认），per-item 覆盖留后续
- [ ] 环境预设：2~3 套内置；是否需要「加载外部 cubemap」API
- [ ] P3 是否直接带大点实例化降级

## 待验证（无 OpenGL 环境、仓库无渲染测试）

1. 组内替换：运行 Group Sim → 点击 "Replace Vehicle"，确认新载具出现在原位置、继续跟随平台/载具动画、相机不跳。
2. 只 `RemoveSurfaceItem("Vehicle")` 后组内 leaf 被跳过（不绘制、不崩溃、动画继续）；同 key 再 `SetMesh(..., "Vehicle", ...)` 自动重绑并恢复原局部姿态。
3. `ClearOverlayItems()` 摘除 overlay 成员但不影响表面对象与内置 HUD；选中项被清后继续点击/拖拽不崩溃。
4. `ResetView(true)` 清空组与表面；`ResetView(false)` 保留表面对象。
5. 3-Axis Stage（X / Y / Z 三组联动）行为不变。
6. 着色器编译失败路径（`ShaderFailed`）与 Box 边线不透明表现。
7. 字形图集排版保真度：坐标提示 / 色条标签与旧的"整串纹理"排版是否像素级一致（`Advance` / `LegacyLeftBearing` 为启发式）。
8. 球体实例化：`Sphere` 模式外观与旧实现一致（半径 = `Size / 2`、模型矩阵直传）。
9. 共享 surface 程序 + 上下文重建：GL 上下文重建后三个内建项正常绘制、无残留/重复释放的句柄。
10. ~~FBO 尺寸缓存：resize 路径~~ —— **已失效**：OpenTK 迁移后 FBO 路径（`SetDimensions`）已删除，改为直绘窗口默认帧缓冲；resize 只需确认 `Viewport` 与画面同步（已并入第 9 条上下文重建）。
11. `Lit` 纯色模式：`SolidColor` 生效（Mesh 下保留光照立体感、PointCloud 下为纯平色），且纯色 Item 不再触发颜色条。
12. 按需重绘（E1）：相机旋转/平移/缩放、数据加载、选中与拖拽、组变换、GL 上下文重建后画面是否都及时刷新（OpenTK `GLControl` 只在 `Invalidate()` 时重绘，无定时兜底）；后台线程准备完数据后未经过 `Set*` / `Redraw()` 时不刷新（预期行为，README 已注明）。
13. 延迟拾取（E2）：悬停坐标标签是否随鼠标及时更新、与旧行为一致；`CoordinatesChanged` 现在在渲染帧内触发，处理器若用同步 `Invoke` 会卡帧。
14. 半透明（M2）：`Opacity < 1` 的 Item 是否正确混合在不透明场景之上；全不透明场景的渲染结果与改动前逐像素一致；`CxBox3DItem` 等半透明 overlay 不再遮挡其后绘制的几何。
15. 注册/替换/释放路径（M1 第 2/3 步）：
    - **CPU 层已用临时脚本覆盖**（不新增项目、不进仓库）：驱动公开 API + 反射校验注册表内部不变量，55 项断言全过 —— 含新 key 注册、同 key 替换、改名保持槽位、跨类型替换、同实例重复 Set 为空操作、按 key 移除、清空、overlay 注册/替换/移除/清空、`Dispose` 后状态清空，以及渲染帧侧 API（`CopyFrameSnapshot` 索引对齐、`ForEachPoolEntry`、`GetKeyOf`、`MarkNeedsUpdate`、`DrainReleases` 恰好一次）。**该脚本只证明当前语义自洽，不能证明与重构前等价**（无 oracle）。
    - **仍需 GL 侧人工回归**（脚本无 GL 上下文，不覆盖绘制与真实资源释放）：同 key 反复 `Set*`（含不同类型互替）后绘制顺序与画面正常；`Set*(otherKey, 已登记实例)` 不重复绘制；`RemoveSurfaceItem` / `ClearSurfaceItems` / `ClearOverlayItems` / `SetOverlayItem` 同 key 替换后无残留句柄；控件 `Dispose` 后无 GL 资源泄漏或异常；后台线程 Set 与渲染并发时不抛异常（脚本为单线程，未覆盖锁竞争）。
