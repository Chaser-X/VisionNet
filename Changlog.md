# 更新记录（CxControl / CxDisplay）

> 用途：记录影响**公共契约、渲染行为、架构边界**的变更，便于后续维护与交接。
> 约定：新变更先写入「未发布」；提交后把条目移到「已提交」并补上提交哈希。
> 相关文档：`README.md`（对外 API 说明）、[Agents.md](Agents.md)（接入说明）、[ToDo.md](ToDo.md)（任务清单）。
>
> 合并说明（2026-11-05）：本文件已并入原 `CHANGELOG.md` 的产品变更历史，以及工作区文档变更记录。
> 原「架构现状与维护要点」已迁至 [Agents.md](Agents.md)；原「待验证（人工 GL 回归）」已迁至 [ToDo.md](ToDo.md)。

## 未发布（工作区，尚未提交）

（无）

## 已提交


### e92efa2（2026-11-09）修复（库）：HUD 字体过大 —— 色条标签压条 / 坐标标签溢出

**现象**：色条刻度标签与位置标签字号偏大 —— 色条标签（字号 10）从左向右延伸，数字位数多时压到色条上；位置标签（字号 12）文字超出 80px 的框。

**改动**
- `ModernGLRenderer.Text.cs`：新增 `internal int MeasureText(float fontSize, string text)`（字形图集路径累加 `Advance`，否则用字符串纹理宽度），供 HUD 右对齐 / 按内容定框宽。
- `CxColorBarItem`：刻度标签字号 `10` → `8`，并**右对齐**（右端固定在色条左侧 `startX - 8`），不再压条。
- `CxCoordinationTagItem`：坐标字号 `12` → `9`；框宽按最宽行 `MeasureText` 计算（`max(80, 宽 + 20)`），框高按行数 × 行距（18）计算 —— 文字不再溢出。

**验证**：`CxControl` / `DemoFrom` 0 error 0 warning；`dotnet test` **128/128**（`CxColorBarTickTests` 只测刻度值/位置，不受字号影响）。

- **需上机目视确认**：色条标签不再压条、多位数右对齐；坐标标签文字落在框内、不溢出。


### b9a18b3（2026-11-09）修复（库）：MSAA 关时 hover 拾取也走 PBO（回读与 FBO 解耦）

**背景**：上一轮把 MSAA 关改为直绘窗口，但同时 `Release()` 把整个 `CxMsaaRenderTarget`（含 PBO）销毁，`ResolvePendingPick` 遂退回**同步**窗口深度读 —— MSAA 关时 hover 又会阻塞 UI 线程。

**改动**
- `CxMsaaRenderTarget`：`Release()` 拆为 `ReleaseFramebuffer()`（只销毁多重采样 FBO 及其颜色 / 深度 / 1×1 附件，**保留 PBO**）与 `Dispose()`（全销毁）；新增 `CanReadDepth => _pbo != 0`。
- `BeginDepthRead(x, y, viewportWidth, viewportHeight)` 分两路：MSAA FBO 在 → 抠 1×1 深度后从该 FBO 读；否则绑 FB0 从窗口深度读。
- `CxDisplay.OnPaint`：MSAA 关时调 `ReleaseFramebuffer()`（而非 `Release()`）。
- `CxDisplay.Render.cs`：`ResolvePendingPick` 的异步判据由 `_renderTarget.IsCreated` 改为 `_renderTarget.CanReadDepth`。

**行为变化**：MSAA 开 / 关两种模式 hover 拾取都异步（PBO），不再阻塞 UI；点击与相机双击聚焦仍同步（每次一次）。

**验证**：`CxControl` / `DemoFrom` 0 error 0 warning；`dotnet test` **128/128**。

- **需上机目视确认**：MSAA 关时 hover 坐标标签是否与开时一样跟手、无卡顿。


### d929066（2026-11-09）渲染（库）：离屏 4× MSAA 渲染目标 + 深度拾取改为异步 PBO 回读

**背景**：MSAA 与深度拾取原本互斥 —— 多重采样深度缓冲不能用 `glReadPixels` 读回（边缘像素读到的是"某个样本"，不可靠）。本次把两者解耦：画面走离屏多重采样目标，拾取读该目标的**单采样 resolve**，并用 PBO 异步回读消除 UI 阻塞。

**改动**
- 新增 `3D/CxMsaaRenderTarget.cs`：离屏 **MSAA FBO**（`RGBA8` 颜色 + `DEPTH_COMPONENT24` 深度，`RenderbufferStorageMultisample`，请求 4×、按 `GL_MAX_SAMPLES` 钳制）→ 每帧 `glBlitFramebuffer` resolve 颜色到窗口；另有 **1×1 单采样深度 FBO** 供拾取。
- **拾取深度源**：从 `MSAA FBO` 用 `glBlitFramebuffer` 抠出 **1×1 深度**（深度 resolve 取单样本）到单采样 FBO 再读。**场景仍只画一遍**（不是重绘第二遍）。
- **异步回读**：`CxPickingService` 的 hover 路径改为 `BeginDepthRead`（`glReadPixels` 进 `GL_PIXEL_PACK_BUFFER`，立即返回）+ 隔帧 `TryEndDepthRead`（`glFenceSync`/`glClientWaitSync(0)` 轮询后 `glGetBufferSubData`）。**UI 线程不再等 GPU 画完**。点击路径仍走同步读（每次点击一次，可接受）。
- `CxGlContext.DepthTarget` + `CxMsaaRenderTarget.ReadDepthAtPixel`：把"读某像素深度"收敛为**唯一入口**，两个相机的双击聚焦也改用它（否则会读到窗口缓冲的空深度）。
- `CxDisplay.OnPaint`：绑定 MSAA FBO 渲染 → resolve 到窗口 → `Present`；无可用 FBO 时回退直绘窗口。
- **新增开关 `CxDisplay.MsaaEnabled`（默认 `true`）**：关闭后**直接绘制窗口**（不建离屏目标、不做 resolve blit），拾取回退读窗口深度；Demo 的「3D 显示」页与「光照/PBR 测试」页各加勾选框。
- **修复 hover 帧率失控**：异步拾取的补帧改为经 hover 节流（`RequestHoverPickRepaint`），不再直接 `SafeInvalidate` —— 此前鼠标持续移动时"每帧发起一次读 → 又立刻排下一帧"，绕过了 `HoverPickMinIntervalMs`，帧率变成"GPU 画多快画多快"，GPU 打满 100%（也因此看不出 MSAA 开关的差别）。
- **节流**：`HoverPickMinIntervalMs = 33` 现在同时约束 hover 重绘与异步拾取补帧，把 hover 帧率钳在 ≈30fps（如需更省电可调大）。

**行为变化**
- 显示走 FBO + blit；窗口帧缓冲不再承担深度拾取（拾取源是离屏目标的单采样深度）。
- MSAA 只平滑 **Mesh / 线框** 边缘；点云圆盘由片元 `discard` 裁出，**MSAA 对其无效**（与 A0 一致）。
- 深度 resolve 取单个样本，边缘像素的取舍仍由既有 5×5 邻域搜索兜底；精度与迁移前一致。

**验证**：`VisionNet` / `CxControl` / `DemoFrom` 0 error 0 warning；`dotnet test` **128/128**（新增 `FarPlaneDepth_ReportsNoHit`）。

- **需上机目视确认**：① DEBUG 日志 `[CxControl] MSAA render target: … samples=N` 是否为 4（驱动未授予则为 0，功能不崩、只是无 AA）；② Mesh / 线框边缘是否平滑、点云圆盘应无变化；③ hover 时 UI 是否不再随鼠标移动卡顿；④ 悬停坐标标签是否正常（含双击聚焦）。

### d929066（2026-11-09）新增（库）：`CxDisplayDefaults` —— 渲染 / 光照默认值集中管理 + 显示级 `PointSize`

**背景**：默认值原先散落在三处（`CxDisplay` 字段初始化、`AbstractCxObjRenderItem` 字段初始化、Demo 光照页硬编码的 `20/50/130/100`）；「点云点大小」只有 item 级 `PointSize`、不传播也不被新 item 继承，且默认值出现漂移（item `3f` / `InitialPointSize` `2` / README `2`）。

**改动**（移植自 `feature/cxdisplay-modern-pipeline` 的 `fc2dc50`，并按本分支适配）
- 新增静态类 `CxDisplayDefaults`（`src/Controls/CxControl/3D/CxDisplayDefaults.cs`，已加入 csproj）：集中所有默认值（`ViewMode` / `IsLeftHanded` / `ShowCoordinateSystem` / `IsColorBarVisible` / `SurfaceMode` / `SurfaceColorMode` / `SurfaceLightingMode` / `EnvironmentPreset` / `IblIntensity` / `EnvironmentFollowsCamera` / `PbrMetallic` / `PbrRoughness` / `PointImpostor` / `PointSize` / `MsaaEnabled` / `SolidColor` / `Opacity` / `TranslucentWritesDepth`），并提供 `Reset()` 与 `ApplyTo(CxDisplay)`。
- `CxDisplay`：字段初始化改引用 `CxDisplayDefaults.*`；无参构造链到 `this(CxDisplayDefaults.ViewMode, CxDisplayDefaults.SurfaceMode, CxDisplayDefaults.SurfaceColorMode)`。
- **新增显示级 `CxDisplay.PointSize`**：下发到所有 item；`SetSurfaceItem` 的新 item 继承。
- `AbstractCxObjRenderItem`：材质 / 颜色 / 透明 / 点大小字段默认值改引用 `CxDisplayDefaults.*`；`PointSize` 默认统一为 `3`。
- `CxDisplay.Render.cs`：`InitialPointSize` 由 `const 2.0f` 改为 `static readonly = CxDisplayDefaults.PointSize`。
- Demo 光照页：滑块 / 勾选框初值改读 `CxDisplayDefaults`（去掉硬编码 `20/50/130/100`），并新增「点大小」滑块。
- README / ToDo 同步。

**本分支适配**：把本分支新增的显示级开关 `MsaaEnabled` 也纳入 `CxDisplayDefaults`（`Reset()` / `ApplyTo` 一并覆盖）；Demo 的「MSAA 抗锯齿」勾选框初值改读默认值。

**语义**：`CxDisplay`（及 item）**构造时读一次**静态默认值；改静态值只影响之后创建的控件，已存在的控件用 `CxDisplayDefaults.ApplyTo(display)` 下发。

**验证**：`CxControl` / `DemoFrom` 0 error 0 warning；`dotnet test` **128/128**。

- **需上机目视确认**：Demo 光照页默认观感不变（仅多一个「点大小」滑块）；拖动该滑块能实时改变点云点尺寸。


### 2bc721d（2026-11-06）清理（库）：删除 SharpGL 时代的遗留文件 + 更新嵌套 README

- 删除 `src/Controls/CxControl/CxControl.csproj_bak` 与 `src/Controls/CxControl/.CxControl.csproj.swp_bak`：迁移前的 csproj 备份（仍带 `SharpGL` 引用），**无任何引用**（未被 `CxControl.csproj` 的 `Compile`/`None`/`Content` 引用，`git grep` 亦无外部引用）。
- 更新 `src/Controls/CxControl/README.md`（项目结构说明）：`基于 OpenGL（SharpGL）` → `OpenTK 3.3.3`；`CxDisplay` 基类 `OpenGLControl` → `OpenTK.GLControl`；补齐迁移后新增的核心文件（`CxGlContext` / `CxItemRegistry` / `CxGroupManager` / `CxGLResourceManager` / `CxPickingService` / `RenderItem/Shared` / `2D/`）。

**验证**：`git grep -l SharpGL` 仅剩 4 处**有意保留**的引用 —— `Changlog.md`（历史记录）、`ToDo.md`（历史记录）、`OpenTkMigrationTests.cs`（迁移守卫测试）、`CxGlContext.cs`（一句解释性注释）；`CxControl` / `DemoFrom` 0 error 0 warning；`dotnet test` 127/127。




### 55dbf1b（2026-11-06）清理（库）：移除 SharpGL 残留 DLL

- 删除 `3rd/SharpGL.dll` / `3rd/SharpGL.SceneGraph.dll` / `3rd/SharpGL.WinForms.dll`。自迁移提交 `3c61519` 起，三者已无任何 csproj / 源码引用，且 `OpenTkMigrationTests` 持续守卫「`CxControl.dll` 不引用 SharpGL、公共 API 不暴露 SharpGL 类型」。
- 同步清理 `bin/Debug` 下由旧构建复制的陈旧副本（`bin/` 已被 `.gitignore`，不影响版本库）。

**验证**：`CxControl` / `DemoFrom` 0 error 0 warning；`dotnet test` **127/127 通过**；`git ls-files "3rd/SharpGL*"` 为空；`bin` / `Test` 下无任何 SharpGL 文件残留。




### 9562d55（2026-11-06）内部重构（库）：`ModernGLRenderer` 自持上下文，移除冗余的 `CxGlContext` 参数

**背景（坏味道）**：迁移后 `CxGlContext` 被当作参数穿过大量方法，但其中 **45 个实例方法根本不用它** —— 只做 `GL.*` 静态调用，或继续往下一层转发。参数既噪声又误导（看起来"需要上下文"，其实不需要）。

**改动**
- `ModernGLRenderer` 新增 `private readonly CxGlContext _gl`（私有构造注入；`For(gl)` 创建实例时传入）。该类本就是 per-context 的（`For` 以 `CxGlContext` 为键），"自持上下文"是事实的自然表达。
- **移除 45 个实例方法的 `CxGlContext gl` 参数**；原先读 `gl.Width` / `gl.Height` / `gl.IsValid` 的 7 处改用 `_gl`。保留参数的仅剩 3 个静态入口 `For` / `ResetForContextRecreate` / `Release`（需要它作为字典键；`Release` 还要 `IsValid` / `EnsureCurrent`）。
- 一并移除（同样完全不用该参数）：`ShaderProgramCompiler.CompileProgram`、`CxGLResourceManager.Release`、`GLResourceHandle.GetUniformLocation`、`CxDisplay.ProcessPendingRelease`。
- `CxDisplay.DisposeResources` 改用 `_gl` 字段，去掉参数。
- 调用点同步简化，例如 `renderer.Draw(gl, PrimitiveType.Triangles, …)` → `renderer.Draw(PrimitiveType.Triangles, …)`。

**未改（并说明理由）**：item 的 `Draw(CxGlContext)`（`IRenderItem` / `ICxObjRenderItem` / `AbstractRenderItem` / `AbstractCxObjRenderItem` / 10 个 item）**保留** —— 它是获取 `ModernGLRenderer.For(gl)` 的唯一途径，去掉就得把内部类型放进公共接口签名。`CxDisplay` 的私有帧方法（`Render` / `DrawSurfaceItems` / `DrawOverlays` / `DrawHud` / `ProcessResourcePool`）同样保留：它们把 `gl` 转发给 item `Draw`。

**影响**：纯内部重构，**无公共 API 变更**（`ModernGLRenderer` / `ShaderProgramCompiler` / `CxGLResourceManager` 为 `internal`；`GLResourceHandle.GetUniformLocation` 为 `internal`；帧方法为 `private`）；行为不变（只删参数与转发，不改任何 GL 调用顺序或状态语义）。

**验证**：`CxControl` / `DemoFrom` 0 error 0 warning；`dotnet test` **127/127 通过**；grep 复核：`ModernGLRenderer*.cs` 中 `CxGlContext gl` 仅剩私有构造 + 3 个静态入口。




### 0648ba2（2026-11-06）加固（库）：迁移后审计修复 —— GL 绑定纪律 / 去 FFP / OnPaint 顺序 / 门面单例 + 兼容 3.3 可观测

**来源**：迁移后对 `src/Controls/CxControl/3D/` 做 critic-in-the-loop 审计（Top 5 坏味道），本轮处理 **P1/P2/P3/P5**；**P4**（拆分 `ModernGLRenderer`）按负责人决定**不做**，并从 `ToDo.md` 移除。

**改动**
- **P1 `MakeCurrent` 纪律收敛**（`CxGlContext`）：新增 `EnsureCurrent()` 与 `RunOnContext(Action)` / `RunOnContext<T>(Func<T>)`，作为「帧外 GL 工作」的唯一入口；`EnsureCurrent` 在 DEBUG 下断言绑定生效。原散落的 `MakeCurrent` 全部改走该入口：`CxPickingService.ScreenToWorld`、`CxAdvancedTrackBallCamera.ScreenToWorld` / `LookAtMatrix`、`CxTrackBallCamera.GetMouseWorldPosition` / `LookAtMatrix`、`CxExtension.DrawTextLabel3D` / `DrawTextLabel2D`、`CxDisplay.DisposeResources`、`ModernGLRenderer.Release`、`CxDisplay.OnPaint`（帧首）。
- **P2 删除残留 FFP 依赖**：删除 `ModernGLRenderer.ReadCurrentGLMatrices`（读 `GL_PROJECTION_MATRIX` / `GL_MODELVIEW_MATRIX`）与 `_projectionQuery` / `_viewQuery` 字段；`EnsureSceneMatrices` 不再回退，改为「缺矩阵则一次性 `Debug.WriteLine` 警告 + 用单位矩阵」。`CxAdvancedTrackBallCamera.LookAtMatrix` 删除 `MatrixMode` / `LoadMatrix` 矩阵栈段，只保留 `SetSceneMatrices`。**破坏性**：依赖 `LookAtMatrix` 设置固定管线矩阵的外部调用方需改走 `ModernGLRenderer` / `CxGlContext`。
- **P3 `OnPaint` 顺序**：改为**先** `base.OnPaint(e)`（让 `GLControl.ValidateState()` 的句柄/上下文重建发生在使用上下文之前）**再**渲染，消除「画完才可能重建句柄」的窗口。副作用：外部 `Paint` 订阅者的 GDI 绘制不再覆盖 GL 画面（`Opaque` 控件下本就是死代码）。
- **P5 `CxGlContext` 单实例守卫**：以 `ConditionalWeakTable<GLControl, CxGlContext>` 保证一个控件只有一个门面，重复创建抛 `InvalidOperationException`（避免 `ModernGLRenderer` 的 per-context 状态被分裂成两份 program/VAO）。
- **兼容 3.3 可观测**：`CxDisplay.InitializeGlState` 在 DEBUG 构建输出一行 `[CxControl] GL context: <version> | renderer: … | shading language: … | aliased line width: [min, max]`，把「兼容 profile 3.3 是否成立」从不可答问题变成日志可判定。

**行为影响**
- 独立使用 `CxExtension.DrawTextLabel3D` / `DrawTextLabel2D` 的调用方：帧外必须先 `ICamera.LookAtMatrix(ctx)`（或 `ModernGLRenderer.SetSceneMatrices`）——旧的固定管线回退已移除。
- `ICamera.LookAtMatrix` 不再写固定管线矩阵（同上）。

**验证**：`CxControl` / `DemoFrom` 0 error 0 warning；`dotnet test` **127/127 通过**。

- **需上机目视确认**：宽线（Box 边线 / 多边形）与点精灵是否生效（直接看新增的上下文日志）；独立文本助手在 `LookAtMatrix` 之后仍能正确投影。

（无）



### 3c61519（2026-11-06）渲染后端迁移：CxDisplay 由 SharpGL 切换到 OpenTK（直绘，去掉 FBO 回读）

**动机**：SharpGL 已停止维护；其 FBO 渲染 + 每帧 `glReadPixels` + `BitBlt` 回读是性能大头（4K ≈ 33 MB/帧）。

**约束**：OpenTK 4.x+ **不支持 .NET Framework 4.x**，故保持 **net48**，采用 **OpenTK 3.3.3 + OpenTK.GLControl 3.3.3**（放入 `3rd/`）。

**改动**
- 依赖：`CxControl.csproj` / `DemoFrom.csproj` / `CxControl.Tests.csproj` 移除 `SharpGL*`，改为 `OpenTK` + `OpenTK.GLControl`。
- 新增绑定无关门面 `CxGlContext`（`3D/CxGlContext.cs`）：公共面只有 `Width` / `Height` / `IsValid`，不暴露任何 OpenTK 类型。
- `CxDisplay` 基类 `OpenGLControl` → `OpenTK.GLControl`：`DoOpenGLInitialized` → `OnHandleCreated`（经 `InitializeGlState`）；`DoOpenGLDraw` / `DoGDIDraw` → `OnPaint` + `SwapBuffers`。**删除 FBO 路径**（`RenderContextType.FBO` / `SetDimensions` / `_fboWidth` / `_fboHeight`）。
- GL 调用点机械迁移到 `OpenTK.Graphics.OpenGL.GL` 静态入口 + 强类型枚举；`ReadPixels` 改为 `float[]`；`GetBooleanv` → `GetBoolean`。
- 相机 `LookAtMatrix` 的 FFP 段改用 `OpenTK.Matrix4` + `GL.LoadMatrix`；`ICamera.LookAtMatrix(CxGlContext)`。
- 公共 API 去 SharpGL：`IRenderItem.Draw` / `ICxObjRenderItem.Draw` / `AbstractCxObjRenderItem.Draw` / `CxExtension.DrawTextLabel*` / 相机构造函数均改为 `CxGlContext`。
- 请求 **兼容 profile 3.3**（`GraphicsMode(32bpp, 24 depth, 8 stencil, 0 samples, double-buffered)`），以保留 `LineWidth > 1` 与 point sprite 行为。
- `CxExtension.IsOpenGLAvailable` / `GetOpenGLVersion` 改用一次性隐藏 `GameWindow` 探测。

**破坏性变更（需知会使用方）**
1. **透明背景不再支持**：直绘窗口无法与 WinForms 父容器做 alpha 合成；默认清屏色由透明黑改为**不透明黑**。
2. `CxDisplay` 不再暴露 SharpGL 继承来的 `OpenGLVersion` / `DrawFPS` / `FrameRate` / `RenderTrigger` / `RenderContextType` / `OpenGL` 属性，也不再实现 `ISupportInitialize`。
3. 实现 `IRenderItem` / `ICxObjRenderItem` / `ICamera` 的调用方需按新签名（`CxGlContext`）适配。

**验证**
- `CxControl` / `DemoFrom` 0 错误 0 警告；`CxControl.dll` 引用程序集仅 `OpenTK` / `OpenTK.GLControl`（无 SharpGL）。
- `dotnet test` → **127/127 通过**（新增 4 项 `OpenTkMigrationTests`：不引用 SharpGL、引用 OpenTK、基类为 `OpenTK.GLControl`、公共 API 不暴露 SharpGL 类型）。
- **需上机目视确认**：点云 impostor、Mesh PBR/IBL、色带、坐标轴、悬停 tooltip、选择拾取、半透明、resize、多控件、上下文重建；以及宽线/点精灵在目标驱动器的兼容 profile 下是否生效。


### d89e081（2026-11-06）修复（库）：左手系（IsLeftHanded）下相机未随镜像世界 —— 初始不居中 / 拖动不自洽

**现象**：Demo 光照/PBR 测试页预览切到左手系后，初始 Top 视图的 Mesh **不居中**（偏出画面），拖动手感也不自洽。

**根因**：`IsLeftHanded` 在视图矩阵里镜像了**世界**（`LookAt(camera) · M`，`M = diag(1,-1,1,1)`），但相机自身的世界状态（`_target` / `_position` / 旋转支点）仍按**未镜像世界**设置。于是物体中心 `c` 被画到 `M c`，而相机盯着 `c` → 不在视轴 → 不居中；同理相机世界与屏幕显示的世界差一个 `M`，拖动也不自洽。`FocusOnPoint` 其实**早已**按此约定补偿（`if (_isLeftHanded) target.Y = -target.Y;`），但 `FitView` 与旋转支点漏了。

**改动**（`CxAdvancedTrackBallCamera`）
- 新增私有 `Mirror(v)`：`IsLeftHanded` 时返回 `(x, -y, z)`。
- `FitView`：把预设算出的 `_target` 用 `Mirror` 映射到相机世界，`_position = _target + 逻辑偏移`；`_up` **不镜像**（镜像 up 会同时翻转屏幕 X 轴）。
- `RotateCamera`：支点 `RotationPoint` / `_sceneCenter` 先 `Mirror` 再参与轨道旋转。
- 与既有 `FocusOnPoint` 的约定保持一致。

**Demo**：光照/PBR 测试页新增勾选框「左手系（镜像显示，仅预览）」——切换后自动 `FitView()` 重新取景（取景依赖手性）。

- 验证：`CxControl` / `DemoFrom` 0 错误；`dotnet test` → **123/123 通过**。
- **需本机目视确认**：勾选左手系后初始视图**居中**、拖动/平移方向自洽；取消勾选（右手系）无回归。

### d89e081（2026-11-06）修复（Demo）：光照/PBR 测试页预览拖动旋转反向 —— 手性约定不匹配

**现象**：光照/PBR 测试页右侧预览 `CxDisplay` 加载 3D 对象后，鼠标拖动旋转方向与 3D 页（`cxDisplay2`）不一致，出现「反转 / 不跟手」。

**根因（修正结论）**：**数据约定不匹配，不是相机 bug**。预览最初被配置为 `Camera.IsLeftHanded = true`（照抄 `cxDisplay1`），但预览加载的是 Demo 自建的 Mesh——它们按**右手系**约定构建。`IsLeftHanded` 会给视图矩阵追加一个世界 Y 镜像（`GetMatrices` 里 `view[1] / view[5] / view[9] / view[13]` 取反），于是**对象被镜像渲染**，拖动旋转的表观方向也随之反向。对照组全部自洽：`cxDisplay1`（左手渲染 + Gocator 左手数据）正常、`cxDisplay2`（右手渲染 + Demo 右手数据）正常——**渲染约定必须与数据约定一致**。

**改动**
- `Test/DemoFrom/DemoFrom.cs`：预览 `_cxDisplayPbr.Camera.IsLeftHanded` 由 `true` 改为 `false`，与数据约定（及 `cxDisplay2`）对齐。

**已试并回退（库侧共轭）**：曾尝试在库侧对 `IsLeftHanded` 做旋转/平移共轭（`RotateCamera` 的 `R → M·R·M`、`PanCamera` 的 `offset → M·offset`），**实测无效**——因为"左手系本身是相机缺陷"这一前提不成立；若保留反而会破坏 `cxDisplay1` 这类"左手数据 + 左手渲染"的正确组合。已 `git restore` 回退 `CxAdvancedTrackBallCamera.cs`，**库保持原样**。

- 验证：`CxControl` / `DemoFrom` 0 错误；`dotnet test` → **123/123 通过**。
- **需本机目视确认**：预览对象不再被镜像、拖动旋转方向与 `cxDisplay2` 一致。

### 6ac5042（2026-11-05）修复：半透明（Opacity < 1）物体无法拾取 / 无标签 / 深度错位

**根因**：`AbstractCxObjRenderItem.Draw` 对半透明 item 关闭深度写（`gl.DepthMask(0)`），而拾取（`CxPickingService.ScreenToWorld`）完全依赖 `glReadPixels(GL_DEPTH_COMPONENT)`——半透明物体不在深度缓冲里，于是选不中、无坐标标签，且背后物体的深度被误当作命中点。半透明之间都不写深度还会让"后画的盖在前面"，造成前后颠倒。

**改动（对策 A）**
- `AbstractCxObjRenderItem` 新增 `TranslucentWritesDepth`（默认 **true**）：半透明 item 默认**照常写深度**。深度测试（`GL_LESS`）因此保证近者优先，不再出现"后面的物体看起来在前面"；拾取、选择、坐标标签恢复。
- 设为 `false` 可回到旧行为（半透明之间按绘制顺序互相混合，但对拾取不可见）。
- `CxDisplay.TranslucentWritesDepth`（默认 true）应用到所有表面 item，新加入的 item 继承。
- Demo「光照/PBR 测试」页新增勾选框「半透明写深度（拾取 / 标签可用，默认开）」。

**取舍**：默认（写深度）下，重叠的两个半透明物体之间**不再互相透出**（远者被深度测试剔除）；要"半透明互相透出且顺序正确"需要排序 + 不写深度 + 独立拾取深度（对策 B，本次未做，见 TODO）。

- 验证：`dotnet test` → **123/123 通过**；`CxControl` / `DemoFrom` 0 错误。
- **需本机目视确认**：`Opacity < 1` 时可选中、有坐标标签；两个半透明物体不再前后颠倒；关掉勾选恢复旧的混合观感。

### b14d176（2026-11-05）新增 `CxDisplay.SolidColor` / `CxDisplay.Opacity` + Demo 纯色/透明度测试

- `CxDisplay` 新增 `SolidColor`（默认 `RGB(217,217,217)`，配合 `SurfaceColorMode.Lit`）与 `Opacity`（默认 1，clamp [0,1]，`NaN` → 1）——设置后推送到所有表面 item，且**新加入的 item 在 `SetSurfaceItem` 里继承**。
- 内部：`ApplyPbrMaterialToItems` 收敛到通用 `ApplyToItems(Action<AbstractCxObjRenderItem>)`；`SetSurfaceItem` 一并套用 `SolidColor` / `Opacity`。
- Demo「光照/PBR 测试」页新增「纯色 / 透明度（测试）」区：`纯色模式（SurfaceColorMode = Lit）` 勾选、`选择纯色…`（`ColorDialog` → `SolidColor`）、`Opacity` 滑条（0–1，初值 1），均作用于本页预览与两个主 CxDisplay。
- README 更正：PBR/IBL 默认已由 `Legacy` 改为 `Pbr`（补 `EnvironmentFollowsCamera` / `SolidColor` / `Opacity`）。

- 验证：`dotnet test` → **123/123 通过**；`CxControl` / `DemoFrom` 0 错误。
- **需本机目视确认**：`Lit` + `SolidColor` 生效；`Opacity < 1` 走半透明（不写深度、与不透明场景混合）；新加载的数据继承当前纯色/透明度。

### 8e6ab72（2026-11-03）默认切换为 PBR/IBL（Neutral + 环境跟随相机）+ Demo 支持读 cxpc

**默认渲染（`CxDisplay`）**：`SurfaceLightingMode` = **Pbr**、`EnvironmentPreset` = **Neutral**、`IblIntensity` = **1.3**、`EnvironmentFollowsCamera` = **true**、`PointImpostor` = **true**、`PbrMetallic` = **0.2**。即开箱即用 PBR/IBL + 点云高光球；需要旧观感时显式设 `SurfaceLightingMode.Legacy`。

**Demo（`Test/DemoFrom`）**
- 「加载文件…」支持 **`.cxpc` / `.pcd`** 点云（`VisionOperator.LoadPointCloud`），加载后切到 `SurfaceMode.PointCloud`，直接看高光球。
- 光照/PBR 测试页的初始控件状态与库默认一致：PBR / Neutral / 环境跟随相机 / IBL 1.3 / Metallic 0.2。

- 验证：`dotnet test` → **123/123 通过**；`CxControl` 与 `DemoFrom` 0 错误。

### f98e426（2026-11-03）P2/P3 修复：环境轴向 / 法线朝向 / 预览物体 / 环境跟随相机

**A. 环境轴向 Y-up → Z-up（bug 修复）**
`ProceduralIbl.SampleEnvironment` 此前把 **+Y** 当"上"，而场景与相机是 **Z-up**（曲面高度是 Z，`FitView(Top)` 把相机放在 +Z）——于是环境的"天空"指向场景的水平方向，顶面采到的是地平线色，看起来像被侧后方打光、转动视角也没有明显变化。现改为以 **+Z** 为"上"：天空/地面渐变、地平线与两个软箱光方向全部改为 Z-up（key `(0.35,0.50,0.80)`、fill `(-0.60,-0.35,0.45)`）。

**B. 法线朝向统一**
`CxSurfaceItem` / `CxPointCloudItem` 的 `GenerateMeshIndices` 与 Demo 的 `BuildBumpyMesh` / Diff Demo 索引绕序由 CW 改为 **CCW（从 +Z 看）**，使面法线与 `CxExtension.ComputeGridNormals` 的 +Z 约定一致，减少对片元 `dot(N,V)<0` 翻转的依赖。

**C. 预览物体 / 视角（Demo）**
新增 `BuildSphereMesh` 与「球体预览」按钮；「Mesh 预览」改为斜置 3/4 pose（`RotationX(35)*RotationY(25)`），避免正对相机的平板看不出明暗。

**D. 环境跟随相机**
新增 `CxDisplay.EnvironmentFollowsCamera`（默认 `false`）。为 `true` 时片元在**视空间**采样环境（`mat3(view) * N/R`），旋转视角即重新打光；默认保持世界空间固定（面的明暗是物体自身的属性，不随视角变化）。Demo 增加「环境跟随相机」勾选框（仅 PBR 可用）。

**默认值调整（保留调试值）**：`AbstractCxObjRenderItem._metallic` 0 → **0.2**、`_pointSize` 2 → **3**；`CxDisplay._iblIntensity` → **1.3**（后续条目）。测试与 Demo 滑条初值已同步。

- 验证：`dotnet test` → **123/123 通过**；`CxControl` 与 `DemoFrom` 0 错误。
- **需本机目视确认**：PBR 下 Top 视角时物体**顶面应最亮**（天空在 +Z）；勾选「环境跟随相机」后旋转视角应重新打光；点云高光球的 y 方向。

### f98e426（2026-11-03）P3 点云伪 3D 高光球（渲染质量提升第 3 阶段）

**目标**：点云模式下每个点渲染为「伪 3D 高光球」（point-sprite impostor），复用 P2 的照明模型。

**库侧改动**
- 共享 surface program：片元着色器新增 `uniform int impostor`。当 `surfaceMode == 0`（点云）且 `impostor == 1` 时，用 `gl_PointCoord` 反解视空间球面法线 `N = normalize(vec3(pc.x, -pc.y, sqrt(1 - |pc|²)))`，`|pc|² > 1` 的像素 `discard`（方形 sprite 裁成圆盘），再走 `legacyLightFactor(N)` 或 `pbrShade(albedo, transpose(mat3(view)) * N)`。
- `legacyLightFactor` / `pbrShade` 改为接收调用方传入的法线（Mesh 路径传真实/导数法线，点云路径传球面法线），光照结果不变。
- `ModernGLRenderer.BeginSurfaceLighting` 新增 `impostor` 参数：写 `impostor` uniform，并在开启时 `glEnable(GL_POINT_SPRITE)`（`0x8861`，SharpGL 未导出该常量）。兼容 profile 下不启用则 `gl_PointCoord` 全为 0、所有点会被裁掉；核心 profile 下该调用无害。
- `CxDisplay` 新增 `PointImpostor`（默认 `true`），帧级全局。
- **未做**（记入 TODO）：`gl_FragDepth` 球面深度修正（会关闭 early-Z，密集点云有性能代价，P3 先保留 early-Z）；大点超过驱动点尺寸上限时的实例化 quad 降级。

**Demo（`Test/DemoFrom`）**
- 新增「光照/PBR 测试」Tab：左侧控制面板 + **右侧内嵌一个独立 `CxDisplay` 作实时预览**（代码创建，需 `ISupportInitialize` 往返才会建 GL 上下文）。
- 控件：Legacy/PBR、Studio/Neutral、Point Impostor 开关、**Metallic / Roughness / IBL 强度三个滑条**，以及 Mesh / 点云预览加载、「加载文件…（Mesh / Surface，`.obj/.cxmesh/.stl/.stla/.cxsurface`）」与「两个主 CxDisplay 都加载 Mesh」按钮。
- 所有控件**同时作用于本页预览与两个主 CxDisplay**；`FormClosing` 中一并 `Dispose` 预览控件。
- 修复：两个单选组各自放入独立内层容器（WinForms 按**父容器**分组，`System.Windows.Forms.RadioButton` 没有 `GroupName`，那是 WPF）——此前四者同容器会被当成一个互斥组，点 Studio 会取消 Legacy 的勾选，表现为"Legacy 下切环境没反应、必须先切 PBR"。
- Legacy 模式下把"仅 PBR 生效"的控件（环境单选 + Metallic/Roughness/IBL 强度滑条）灰显，避免误以为失效；Point Impostor 两种模式都生效，保持可用。

- 验证：`dotnet test` → **123/123 通过**；`CxControl` 与 `DemoFrom` 0 错误。
- **需本机目视确认**：`gl_PointCoord` 的 y 方向——若高光出现在球的下方而非上方，去掉 `pc.y` 前的负号即可（仅一行）；点云高光球观感与性能；`PointImpostor = false` 时点云与改动前一致。

### f98e426（2026-11-03）P2 PBR / IBL（渲染质量提升第 2 阶段）

**目标**：Mesh 模式支持 PBR/IBL 光照。环境贴图**程序化生成**（不引入外部资产文件与 HDR/DDS 解码器），计划见 `TODO-CxDisplay-render-quality.md`。

**库侧改动**
- 新增 `enum SurfaceLightingMode { Legacy = 0, Pbr = 1 }` 与 `enum SurfaceEnvironmentPreset { Studio, Neutral }`。
- `CxDisplay` 新增 `SurfaceLightingMode`、`EnvironmentPreset`、`IblIntensity`（默认值见后续「默认切换为 PBR/IBL」条目）。三者是**帧级全局**设置：`DoOpenGLDraw` 每帧在 `ModernGLRenderer.BeginSurfaceLighting` 里一次性写入共享 surface program，不逐 item 传播（新建 item 无需继承）。
- `AbstractCxObjRenderItem` 新增 `Metallic`（默认 0.2）、`Roughness`（默认 0.5），clamp 到 [0,1]、NaN 有回退；纯 uniform，不重建 GL 资源。
- `CxDisplay` 新增 `PbrMetallic` / `PbrRoughness`（默认 0.2 / 0.5）：设置后推送到所有表面 item；且**新加入的 item 在 `SetSurfaceItem` 里继承当前值**（`SetSurfaceItem` 是所有 `Set*` 的公共 upsert 入口）。
- 新增 `ProceduralIbl`（纯 CPU）：解析式环境（Studio = 冷色天空 + 两个软箱高光；Neutral = 中性灰穹顶 + 单高光）→ 余弦卷积 irradiance cubemap（16²，2048 方向）、GGX 重要性采样 prefilter cubemap（32²，5 级 mip，64 采样）、split-sum BRDF LUT（128²，64 采样）。按 preset **进程级缓存**，只生成一次；GL 上传每个上下文一次。
- 新增 `ModernGLRenderer.Ibl.cs`：IBL 纹理（unit 1 = irradiance cube、2 = prefilter cube、3 = brdf LUT）懒生成/上传；`ReleaseIbl` 并入 `ReleasePrograms`，随上下文一起释放。
- 共享 surface program：顶点着色器新增 `vWorldPos` / `vWorldNormal`；片元着色器新增 `lightingMode` / `metallic` / `roughness` / `iblIntensity` / `prefilterMaxMip` 与三个 IBL sampler，以及 `pbrShade`（世界空间 split-sum IBL：`F0 = mix(0.04, albedo, metallic)`、`kD = (1-F)(1-metallic)`、漫反射 = irradiance×albedo、镜面 = prefilter×`(F·brdf.x + brdf.y)`）。原 Blinn-Phong 抽为 `legacyLightFactor()`。

**行为**：默认 `Legacy`，画面与 P1 完全一致；仅当 `SurfaceLightingMode.Pbr` 且 `SurfaceMode.Mesh` 时才走 IBL。点云模式仍不受光照影响（`surfaceMode == 0`）。

- 验证：`dotnet test` → **123/123 通过**（新增 `ProceduralIblTests` 6 项 + `PbrMaterialTests` 4 项）；`CxControl` 与 `DemoFrom` 0 错误。
- **需本机目视确认**（本机无 GL 环境，GLSL 只能上机编译）：切到 `Pbr` 后 Mesh 是否正常出图（若 shader 编译失败会整片空白）、`Studio`/`Neutral` 观感、`Metallic`/`Roughness`/`IblIntensity` 效果、`Legacy` 与 P1 逐像素一致。
- 未做（记入 TODO）：`CxDisplay` 右键菜单的照明模式开关（切换测试改在 `DemoFrom` 的「光照/PBR 测试」Tab 提供）；`SpecularIntensity` / per-item 覆盖。

### be069ee（2026-11-03）P1 逐顶点法线通道

**目标**：Mesh 光照从「屏幕空间导数法线」升级为「真法线」，为后续 PBR/高光球打基础。计划见 `TODO-CxDisplay-render-quality.md`。

**库侧改动**
- `RenderData` 新增 `Normals`（`float[n×3]`，局部空间单位法线）。
- `GLResourceHandle.VboIds` 由 4 扩到 5，槽位 4 = 法线；`ResetForContextRecreate` 同步。`CxGLResourceManager.Create` 在 intensity 之后上传法线到 attribute 4（`layout(location=4)`）。
- 共享 surface program：顶点着色器新增 `aNormal` / `uniform mat3 normalMatrix` / `out vec3 vNormal`；片元着色器新增 `in vec3 vNormal` / `uniform int useVertexNormal`。Mesh 光照的 `N` 在 `useVertexNormal==1` 时取 `normalize(vNormal)`，否则回退到原来的 `cross(dFdx,dFdy)`。`vNormal` 在顶点着色器内用 `mat3(view) * normalMatrix * aNormal` 转到视空间，与 `viewPos` / `lightDirView` 保持一致。
- `ModernGLRenderer.UploadNormalMatrix(gl, handle, modelInverse)`：用 item **已缓存**的 `ModelMatrixInverse`（拾取路径同一份）构造 `transpose(inverse(model))` 的上 3×3，不额外求逆；每帧每 item 一次。
- `CxExtension.ComputeGridNormals(vertices, width, height)`：有序网格（结构化曲面 / 有序点云）叉积法线，边界单侧差分，邻居含无效点或退化时回退 `+Z`。
- `CxExtension.ComputeTriangleNormals(vertices, indices)`：索引网格面法线累积平滑；无有效三角面时返回 `null`（回退导数法线）。
- 三个内建 item：**仅 Mesh 模式**生成法线（点云模式不参与光照，省一份数组）。`AbstractCxObjRenderItem.PrepareRenderData` 统一注入 `useVertexNormal` uniform（`Normals != null ? 1 : 0`）。

**公开 API 未变**：`Normals` 是 `RenderData` 上的新公开属性，但 `RenderData` 属内部管线数据；未新增枚举/属性/方法。

- 验证：`dotnet test` → **113/113 通过**（新增 `NormalGenerationTests` 10 项：网格法线方向/单位长度/无效点回退/不可用输入、三角面法线、三个 item 的 Mesh/PointCloud 模式联动）；`CxControl` 与 `DemoFrom` 均 0 错误（DemoFrom 3 个警告为预存在 MSB3270）。
- **需本机目视确认**：Mesh 模式光照是否更平滑（曲率处不应再有导数法线的噪点）；点云模式外观与改动前逐像素一致；带姿态（非均匀缩放）的 item 光照法线朝向正确。

### 392f952（2026-11-02）双 CxDisplay 卡顿：悬停重绘节流 + Demo 仿真改用后台驱动

**现象**：Demo 里左侧 `cxDisplay1` 跑 Group 仿真，右侧 `cxDisplay2` 加载 mesh；在右侧持续移动鼠标时，左侧动画冻结，停下即恢复。

**根因**：两个控件的数据完全独立（各自的 `CxItemRegistry` / `CxGroupManager` / `_renderLock` / GL 上下文），但**共用同一条 UI 线程消息队列**。左侧动画由 `System.Windows.Forms.Timer` 驱动，它跑在 `WM_TIMER` 上 —— 消息队列里优先级最低的一类。而右侧每次鼠标移动都会请求一次完整重绘（相机 `GlControl_MouseMove` 末尾无条件 `Invalidate()`，悬停拾取又 `SafeInvalidate()` 一次），`WM_PAINT` 优先级高于 `WM_TIMER`，于是只要鼠标在动，队列里几乎总有待处理的 `WM_PAINT`，`WM_TIMER` 不再生成，`SimTick` 不再触发，姿态停止更新。

**库侧改动（渲染行为变更）**
- `CxAdvancedTrackBallCamera.GlControl_MouseMove`：只在**确实发生了旋转/平移**时 `Invalidate()`。原先无条件重绘，纯悬停也会触发一次完整帧。
- `CxDisplay` 悬停拾取节流：新增 `HoverPickMinIntervalMs = 33`。未被节流时立即 `SafeInvalidate()`；被节流时改为启动一次性定时器（每次节流移动都重启，从而把一串移动合并到最新位置）。这样连续悬停会在消息队列里留出空档，宿主应用的 `WM_TIMER` 得以继续工作；同时一次性定时器保证**鼠标停住后仍会补一帧**，tooltip 不会停在过期位置。
- 节流定时器懒创建（`HoverPickThrottle`），`CancelPendingPick()` 会停掉它（按下/抬起时不再残留一次针对已取消拾取的重绘），`Dispose` 中释放。
- **公开 API 未变**：`HoverPickMinIntervalMs` 是私有常量，定时器是私有字段。

**Demo 侧改动**（`Test/DemoFrom/DemoFrom.cs`）
- Group Sim 与 3-Axis Stage 不再用 `System.Windows.Forms.Timer`，改为新增的私有 `SimDriver`：后台 `System.Threading.Timer` + `BeginInvoke` 到 UI 线程执行回调。
- `SimDriver` 做背压合并：`BeginInvoke` 队列没有背压，所以同一时刻最多只排队一次应用；上一次还没执行完时新 tick 直接丢弃，把突发合并到最新状态而不是堆积过期姿态。定时器为一次性、每 tick 自行重置，慢 UI 线程不会叠出回调积压。
- `SimTick` / `StageTick` 签名由 `(object, EventArgs)` 改为无参（由 driver 在 UI 线程调用），原有的 `_simWatch == null` / `_stageWatch == null` 早退守卫现在实际承担"已停止则丢弃在途回调"的职责。

**曾怀疑、已排除**：一度认为 `CxPickingService.ScreenToWorld` 在帧外（`OnMouseDown` / 拖拽路径）调用 `ReadPixels` 会读到另一个控件的深度缓冲，因为本项目源码里没有任何 `MakeCurrent`。查 SharpGL 3.1.1 的 IL 后确认**不成立**：生成的 GL 入口点内建了 `PreGLCall()`（`if (currentOpenGLInstance != this) MakeCurrent();`），而 `OpenGL.MakeCurrent()` 会 `wglMakeCurrent` 到本实例的上下文并回写该静态字段。`ReadPixels` 的 IL 正是 `PreGLCall → glReadPixels → PostGLCall`，所以帧外调用会先自动绑好正确的上下文。原有代码无需改动。

- 验证：`dotnet test` → **103/103 通过**（本次未新增用例：改动集中在 GL 帧路径与消息循环，无法单测）；`CxControl` 与 `DemoFrom` 均 0 错误（DemoFrom 3 个警告为预存在 MSB3270）。
- **需本机实测确认**：右侧持续移动鼠标时左侧动画应保持流畅（SimTick 间隔稳定在 ~16 ms）；悬停 tooltip 仍跟随鼠标、停住后位置正确；旋转/平移/缩放、拖拽 overlay item 不受影响。

### 8811088（2026-11-02）S4 结构拆分（行为不变）

**抽出 `CxPickingService`**（新增 `3D/CxPickingService.cs`）
- 深度读回（`ReadPixels` + `BitConverter`）、反投影（`ModernGLRenderer.Unproject`）、最近曲面搜索从 `CxDisplay.Input.cs` 移出，连同 `_pickViewport` / `_pickDepthBytes` / `_pickSnapshot` 三块 scratch 缓冲。显示层只剩 `private readonly CxPickingService _picking`，三处 `ScreenToWorldCoordinate(...)` 调用改为 `_picking.ScreenToWorld(OpenGL, ...)`。
- 依赖以参数显式传入（`CxItemRegistry` / `OpenGL` / `SurfaceColorMode`），不使用回调或属性注入，避免引入间接层。
- 待处理拾取的状态机（`_hasPendingPick` / `ApplyPendingPick` / `CancelPendingPick` / `RaisePendingCoordinatesChanged`）**留在显示层**：那是"何时问"的策略，与"怎么答"分开正是这次拆分的边界。
- `CxDisplay.Input.cs` 中已不再使用的 `using System.Collections.Generic;` / `using SharpGL;` 一并删除。

**抽出 `CxGLResourceManager`**（`3D/CxDisplay.GLResources.cs` → `3D/CxGLResourceManager.cs`，改为 `internal static class`）
- `Create` 取代 `CreateGLResources`，**返回值改为 `bool`**：`false` 仅表示着色器编译失败。原先它直接往显示层的 `_failedItems` 里塞失败项，现在由 `ProcessResourcePool` 收到 `false` 后自行登记 —— 保持"失败项在帧锁释放后统一上报"的既有契约。`data == null` 的早退路径仍返回 `true`（是空操作，不是失败）。
- `Release` 取代 `ReleaseGLResources`。类无状态，全部方法静态。
- 旧 partial 文件删除，`CxControl.csproj` 的 `Compile Include` 同步（含 `<SubType>UserControl</SubType>` 与 `<DependentUpon>` 的清理）。

**打破 `Display ⇄ GroupManager` 循环**
- 新增 `3D/IDisplayHost.cs`：`InvokeRequired` / `BeginInvoke(Action)` / `SafeInvalidate()` 三个成员。
- `CxGroupManager` 的构造函数改为 `CxGroupManager(CxItemRegistry registry, IDisplayHost host)`，字段 `_display` 删除。键查询直接走注册表（`GetKeyOf` / `ContainsOverlay` / `GetByKey`），UI 线程封送与重绘走 `_host`。验证：`CxGroupManager.cs` 中对 `CxDisplay` 的**代码**引用为 0（仅剩 XML 文档引用）。
- `CxDisplay : IDisplayHost`：`InvokeRequired` 由继承的公共属性隐式实现；`BeginInvoke` 与 `SafeInvalidate` 用**显式实现**，因为 `SafeInvalidate` 是 `internal`（隐式实现要求 public），且 `Control.BeginInvoke` 是 `Delegate` 签名，需要适配到 `Action`。**公开面没有扩大**。
- `CxDisplay.Group.cs` 里只服务于旧耦合的 `ResolveSurfaceItem` / `GetSurfaceKeyOf` / `ContainsOverlayItem` 三个转发方法删除（全项目已无调用点）。

**`ModernGLRenderer` 清理**
- `_states`（`Dictionary<OpenGL, State>`）删除。`For(gl)` 已保证一个 `OpenGL` 包装器对应一个 renderer 实例，字典再按 gl 套一层是纯冗余；改为单个 `State _state` 字段，`GetState(gl)` 删除。`ReleasePrograms` 里的 `_states.Clear()` 改为 `_state.Reset()`（新增）。
- `Release` 的 `catch { return; }` 改为 `catch (Exception ex)` + `Debug.WriteLine`（与 `ShaderProgramCompiler` 记录着色器编译错误的做法一致）。异常仍然吞掉并提前返回 —— 只增加可诊断性，不改变控制流。

**新增测试（6 项）**：`CxPickingServiceTests` —— 空注册表返回空、单曲面命中网格点、无网格项（网格/点云）以查询点胜出且与注册顺序无关、Diff 模式取 diff 值而非 intensity、已释放项被跳过、重复查询复用 scratch 缓冲不泄漏。
- 其中 `DiffMode_ReportsDiffValuesInsteadOfIntensity` 首轮失败：我把命中格索引算成 `(2,1)`，实际精确命中格是 `(1,2)`（`idx = 2*3+1 = 7`）。是**测试期望写错**，已修正为断言命中格坐标 + `diff[7]`。

- 验证：`dotnet test` Debug 与 Release 均 **103/103 通过**；`CxControl` / `CxControl.Tests` 0 错误 0 C# 警告（仅预存在 MSB3270）；`DemoFrom` 0 错误。
- **按你的要求跳过**：`ModernGLRenderer` 的 `Text / Programs / State` 提升为独立类（M3 第 2 步），已记入 TODO 后续项。

### 559e706（2026-11-02）S3 多态替换类型分支（行为不变）

- **拾取路径去类型分支**：`CxDisplay.GetNearestSurfacePoint` 原先按 `is CxMeshItem` / `is CxPointCloudItem` / `is CxSurfaceItem` 三分支派发。现改为 `AbstractCxObjRenderItem` 上的 `internal virtual void PickAt(world, useDiff, ref bestDist, ref bestPoint, ref bestIntensity)`，三个内建 item 各自 override —— 网格/点云返回查询点本身，结构曲面做 5×5 网格搜索。显示层只剩一句 `if (!(cur is AbstractCxObjRenderItem item)) continue;`。
- **`SearchSurfaceNeighbourhood` 迁到 `CxExtension`**：它是对 `CxSurface` 的纯几何运算，留在 `CxDisplay` 上会让 `CxSurfaceItem` 反过来依赖控件类。`PickNeighbourhoodRadius` / `PickNeighbourhoodTolerance` 一并迁移。测试同步改调用点。
- **共享程序判断改显式契约**：原先 `IsSharedSurfaceShader` 靠 `string.Equals` 逐字比较顶点/片元着色器源码来决定"用上下文共享程序还是编译私有程序"。现改为 `AbstractCxObjRenderItem.UsesSharedSurfaceProgram`（内建 item 默认 `true`；自备 GLSL 的派生类必须 override 为 `false`）。`CxDisplay` 不再持有 `AbstractCxObjRenderItem.SharedVertexShaderSource` 的引用，字符串比较随之删除。
- **删除 `GetItemInverseModelMatrix`**：它唯一的调用点就是拾取路径，逻辑已并入 `CxSurfaceItem.PickAt`（直接用该 item 自己缓存的 `ModelMatrixInverse`）。`GetItemModelMatrix` 保留，仍被 `GetCombinedBoundingBox` 使用。
- 新增 `ItemPickAtTests`（7 项）：曲面按网格命中、带姿态时先用逆矩阵还原网格坐标并以世界坐标回报、网格/点云返回查询点本身、已有精确命中不被覆盖、已释放 item 不贡献、三个内建 item 的 `UsesSharedSurfaceProgram` 均为 true。
- 验证：`dotnet test` → **97/97 通过**；0 错误 0 C# 警告（仅预存在 MSB3270）。

### bde4b73（2026-11-02）S2 拆长方法（行为不变）+ S0 收尾测试

**`CxDisplay.Render` 拆解**（原约 150 行，单方法承担 Phase 1–5）：
- 相位边界原样保留，但每一相位现在是一个具名方法：`AggregateFrameRanges`（Phase 1，返回新的私有 `FrameRanges` 结构）、`PropagateFrameRanges`（Phase 2/2b，含 `SetGlobalZRangeOnAll` / `SetGlobalDiffRangeOnAll`）、`DrawSurfaceItems`（Phase 3，返回 `anyDrawn`）、`DrawOverlays`（Phase 4）、`ResolvePendingPick`（Phase 4b）、`DrawHud`（Phase 5）。
- **色条分支**：原先 4 个 `else if` 各自重复一遍 `SetRange + Draw`，现由 `TryResolveColorBarRange` 归一为"Diff 模式用 diff 范围、其余用 Z 范围；两者都是手动覆盖优先于自动聚合"，`DrawHud` 只保留一次 `SetRange + Draw`。`uniformMode && 模式不是 Intensity/Lit && IsColorBarVisible` 的门控条件原样保留。
- `AggregateFrameRanges` 的 `mixedMode` / `singleMode` 语义逐字保留（含"混合后 `SingleMode` 仍保留首个模式、由 `UniformMode=false` 兜底"这一细节）。

**`CreateGLResources` 拆解**：5 段"`GenBuffers` → `BindBuffer` → `UploadPinned` → `VertexAttribPointer` → `EnableVertexAttribArray`"样板收敛为 `UploadVertexBuffer(gl, handle, vboIndex, data, attributeIndex, componentCount, stride)`，返回下一个空槽位。字节数一律 `data.Length * sizeof(float)`（原先 5 处写法相同）。**唯一差异**：每个 VBO 各分配一个 `uint[1]` 临时 id 数组，而非 5 段共用同一个 —— 这只发生在资源创建期（每次数据变更一次），不在每帧路径上。

**`GetNearestSurfacePoint` 拆解**：
- 5×5 邻域搜索抽为 `internal static SearchSurfaceNeighbourhood(...)`，**不依赖 GL、不依赖 display 实例**，因此可直接单测。`-32768` 改用 `CxExtension.NoDataValue`，`-2..2` 与阈值系数 `5 *` 提为 `PickNeighbourhoodRadius` / `PickNeighbourhoodTolerance`。
- 世界坐标重建语义不变：候选点先经 `model` 变换到世界空间再比距离，重叠曲面按世界距离而非局部坐标消歧。
- 循环里逐次读取的 `SurfaceColorMode` 属性提升为循环外的 `useDiff` 局部量（该属性在单次调用内不可能变化）。Diff 分支的取值优先级不变。

**新增 S0 收尾测试（17 项）**：
- `SurfacePickNeighbourhoodTests`（10 项）：命中所在格、±2 窗口边界（同时验证"窗口内找得到 / 窗口外找不到"）、越界与全空洞返回空、超出容差的候选被拒、多候选取最近、intensity/diff 取值切换、缺值数组返回 null 而非抛异常、模型矩阵把候选点还原到世界空间、已有更优候选不被覆盖。
- `CxColorBarTickTests`（7 项）：刻度数 = 分割数 + 1、端点值恰为范围上下界、等距、倒置范围下单调、屏幕位置从条底开始且落在条内。`TickY` 用单精度 `i * (barHeight / 7f)`，整除时会有 1 像素截断，测试显式允许该截断而非断言精确端点。

- 验证：`dotnet test` Debug 与 Release 均 **90/90 通过**；`CxControl` / `CxControl.Tests` 0 错误 0 C# 警告（仅预存在 MSB3270）；`DemoFrom` 0 错误（3 个警告均为预存在的 MSB3270）。
- **测试过程中发现的坑**：`CxSurface` 的构造函数**不接收 `Diff`** —— `Diff` 只是普通自动属性，必须构造后单独赋值。首轮 `ReportsIntensityByDefault_AndDiffInDiffMode` 因此失败（diff 路径静默拿到 null），已修正测试夹具；这也说明该断言不是恒真。

### f0f883d（2026-11-02）S1 低风险去重（行为不变）

- **包围盒收尾**：`CxExtension` 的三个 `CalculateBoundingBox` 重载各自重复一遍"中心 + 尺寸"构造，现统一走新的 `CxExtension.BuildBox(min,max)`；`CxDisplay.GetCombinedBoundingBox` 的同一段收尾也改用它。`CxBoundingBoxConsistencyTests` 先于本次改动落地，用于守住等价性。
- **缓冲扩容**：新增 `AbstractRenderItem.EnsureExactCapacity(ref float[], int)`，替换 `CxPoint3DItem` / `CxSegment3DItem` / `CxPlane3DItem` / `CxPolygon3DItem` 中手写的 `if (buf.Length != required) buf = new float[required]`（共 6 处）。
  - ⚠️ **没有**沿用 `ModernGLRenderer.Matrix.cs` 里既有的 `EnsureCapacity`：那个是 **grow-only**（`buffer.Length < required` 才扩容），而 `gl.DrawArrays(..., positions.Length / 3)` 的顶点数**取自数组长度**，缓冲区一旦大于需要量就会多画尾部残留顶点。因此这里必须是**精确**分配语义，两者不可互换。`EnsureExactCapacity` 额外接受 `null`（`CxPolygon3DItem._polygonBuffers[i]` 初值为 null）。
- **`PointToSegDistSq`**：`CxSegment3DItem` 与 `CxPolygon3DItem` 各有一份逐字相同的私有实现，上提到 `AbstractRenderItem` 作为 `protected static`，两处私有副本删除。
- **`CxPoint3DItem.Draw`**：Point 与 Sphere 两个分支各自重复一遍相同的"分配 + 逐点填充"循环（共 14 行重复），抽为私有 `FillPositionBuffer()`。注意保留了两分支的既有差异 —— Point 分支 `PushModelMatrix`/`PopModelMatrix`，Sphere 分支不推栈（它通过 `DrawSpheres` 的 `model` uniform 自行应用姿态，推栈会导致重复应用）。
- **菜单遍历**：`UpdateMenuItems` 的三段"按文本勾选"循环与 4 个点击处理器里的"全部取消勾选"循环，抽为 `CxDisplay.SyncMenuSelection` / `CxDisplay.UncheckAllMenuItems`。
- **`ModernGLRenderer.Geometry`**：`Draw` / `DrawScreenGeometry` / `DrawScreenGeometryColored` 三份方法的前置（取程序、建 VAO/VBO、捕获状态）与后置（解绑、`UseProgram(0)`、恢复状态）样板抽为 `BeginGeometryDraw` / `EndGeometryDraw`。着色器上传顺序与逐顶点颜色属性（attrib 1）的启用/关闭时机均保持不变。
- **魔法数字提常量**：`CxDisplay.InitialPointSize`（原 `PointSize(2.0f)`）、`CxColorBarItem.TickDivisions`（原 53 行的 `divisions` 与 65 行硬编码的 `7 + 1` 合并）、`ModernGLRenderer.SphereSlices` / `SphereStacks`（原 `GenerateUnitSphereData(10, 10, …)`）、`CxExtension.NoDataValue`（原散落的 `-32768`）。
- 验证：MSBuild 构建 `CxControl` + `CxControl.Tests` → **0 错误、0 C# 警告**（仅预存在 MSB3270）；`dotnet test` → **73/73 通过**。
- **未做**：`-32768` 在 `CxDisplay.Input.cs` 的拾取路径里也出现，但它随 5×5 邻域常量一起属于 S2 的拾取算法抽取，本轮先留在原处。

### c384234（2026-11-02）S0 安全网：包围盒三路一致性测试 + 每帧 CPU 基准

- 新增 `CxBoundingBoxConsistencyTests`（5 项）：`CxExtension.CalculateBoundingBox` 的三个重载（`IEnumerable<CxPoint3D>` / `CxSurface` / `CxPointCloud`）是手写重复实现，现断言它们在**同一组点**上给出逐项相等的包围盒，覆盖全填充、整行空洞、全空洞三种情形；并单独钉住点云扫描"任一通道为 `-32768` 即整点作废"的语义（不是按轴各算各的）。这组测试是后续抽出共享收尾逻辑前的护栏。
- 新增 `CxDisplayCpuBenchmarkTests`（1 项）：记录渲染帧中纯 CPU 热点的基线 —— 颜色映射（色条每帧每行一次）、曲面包围盒扫描（每帧聚合）、`CxPoint3D[]` → `float[]` 位置拷贝。**只记录不断言耗时**，断言的是被测代码的真实不变量（颜色落在 `[0,1]`、包围盒非退化、拷贝末值正确），因此不会因机器差异产生偶发失败。
- 本轮基线（Debug/net48，本机）：

  | 基准 | 耗时 | Gen0 |
  | --- | --- | --- |
  | `GetColorByHeight` ×200k | 5.56 ms | 0 |
  | `CalculateBoundingBox(surface 512×512)` ×20 | 69.94 ms（≈3.5 ms/次） | 0 |
  | `Point3D[]` → `float[]`（200k 点）×20 | 12.90 ms（≈0.65 ms/次） | 0 |

- 验证：`dotnet test` → **73/73 通过**（原 67 + 新增 6）。
- 说明：`CxDisplayCpuBenchmarkTests` 会随测试套件一起运行（约 0.5 s）。用 `--logger "console;verbosity=detailed"` 可读到上表数字。

### b35d2ef（2026-10-30）评审修复（P1–P9）：拾取事件移出帧锁、NaN 透明度、注册表整洁与文档

- **P1（行为修复）**：`CoordinatesChanged` 原先在渲染帧内、`_renderLock` 持有期间触发（hover 拾取被延迟到 `Render` 后引入）。现在 `ApplyPendingPick` 只**暂存**结果（tooltip 仍在本帧绘制），事件由 `DoOpenGLDraw` **在帧锁释放后**触发 —— 与 `RenderItemFailed` 的既定契约一致（处理器可安全回调 display）。
- **P3（缺陷修复）**：`OnMouseDown` / 拖拽分支 / `OnMouseUp` 原先只隐藏 tooltip、不清待处理拾取，导致按下前最后一次 move 留下的拾取会在下一帧让 tooltip 重新可见并触发事件。新增 `CancelPendingPick()` 在这三处调用。
- **P8（缺陷修复）**：`Opacity` 的钳制对 `NaN` 不生效（两个比较都为 false），NaN 会被存下并在 shader 中作为 alpha 输出；现在 `NaN` 视为完全不透明（`1`），并补充"uniform-only 设置需自行 `Redraw()`"的说明。
- **P4**：`_pickSnapshot` 在两条返回路径上 `Clear()`，不再滞留已移除（可能已 Dispose）的 item 引用；并在 XML 文档注明该路径**仅限 UI 线程**（需要 GL 深度读回）。
- **P5**：删除注册表中零生产调用的 `TryGetHandle` / `CopyKeys`（此前只有测试在用）；测试改用生产 API（`CopyFrameSnapshot` / `GetByKey`）断言。
- **P6**：补 `RegisterSurfaceItem` 的槽位规则文档（含"key 已占用 **且** item 已以另一 key 注册"这一组合分支：item 取**被替换者**的槽位，原槽位丢弃），并新增对应用例。
- **P7**：注册表类注释写明 **回调在锁内执行**（与旧 `_resourceLock` 行为一致），并说明回调不得阻塞、同线程可重入。
- **P2**：`ModelMatrix` 的逆矩阵缓存只在属性赋值时失效，而 `CxMatrix4X4.Data` 是公开可写的 `float[]`（原地修改会让缓存过期）；在两个 `ModelMatrix` 属性上写明"视为不可变，请整体重新赋值"，并加入「已知坑」。
- **P9 整洁性**：删除 `CxItemRegistry` 中空的 `// ── Critical section ──` 段头；把承载延迟释放方法的段头由误标的 `// ── Surface mutations ──` 改为 `// ── Deferred release ──`（同时消除重复段头）；段头前补空行；`CxDisplay.Api.cs` 的 `RefreshViewSize` 去掉行尾空白与 3 行失效注释代码。
- 验证：`CxControl` / `DemoFrom` / `CxControl.Tests` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）；`dotnet test` → **67/67 通过**（新增 P6 组合分支与 P8 NaN 两项用例）。
- **未处理**：P2 的"缓存时保存 `Data` 快照/版本号"方案（成本高于收益，改为文档契约）；P4 的并发安全（与既有 `_pickViewport` / `_pickDepthBytes` 的取舍一致，属待验证）。

### fe522eb（2026-10-30）重命名：`ModernOverlayRenderer` → `ModernGLRenderer` @ `3D\RenderItem\Shared\`

- 该类型实际服务**全部**渲染项 —— 既服务 `ComplexGeometry` 表面项（`EnsureSurfaceProgram` 提供共享 surface 程序、`UploadSceneMatrices` 上传矩阵、`CaptureState`/`RestoreState` 提供状态作用域），也服务 overlay 项；旧名 `ModernOverlayRenderer` 容易让人误以为只针对叠加层。改名并归入独立文件夹。
- `3D\RenderItem\` 下的 6 个 partial 文件 → `3D\RenderItem\Shared\ModernGLRenderer{.Geometry,.Text,.Programs,.State,.Matrix}.cs`；`ShaderProgramCompiler.cs` 一并移入同一文件夹（它是这套管线的 shader 编译工具）。
- 类名替换：全项目 **40 处引用**（19 个调用文件 + 6 个 partial 自身），残留 **0**；`CxControl.csproj` 的 7 个 `Compile Include` 同步；README 架构树补出 `RenderItem/Shared/` 一行；CHANGELOG「架构现状」补出 `ModernGLRenderer` 与 `CxItemRegistry` 两项，并注明旧名易误导。
- 类的 XML 文档改写：由「Shared renderer for the small, immediate-data overlays」改为「per-context rendering services shared by every render item」，并明确它不只为叠加层服务。
- **纯重命名 + 文件移动，无行为改动**：除类名与注释/文档外没有其他代码变更。
- 验证：`CxControl` / `DemoFrom` / `CxControl.Tests` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）；`dotnet test` → **65/65 通过**（Debug 与 Release）。

### 0a283a4（2026-10-30）M4 续：测试覆盖扩展到 `CxDisplay` 编排层（+18 项，共 65 项）

- 新增 `CxDisplayApiTests`（18 项）：在 **STA 线程**上构造真实 `CxDisplay`（不显示、不渲染、不创建 GL 上下文），覆盖 M1 重写的 display 级流程 —— `SetMesh`/`SetSurface` 注册与返回值、同 key 替换、跨类型替换、`SetSurfaceItem(同实例)` 的**改名**与**空操作**（后者断言不 Dispose 调用方实例）、按 key / 按实例移除、`ClearSurfaceItems`；overlay 的 upsert（被替换者被 Dispose）与同实例空操作、按 key 移除、`ClearOverlayItems`；`SurfaceMode`/`SurfaceColorMode` 写穿到每个 item；`Opacity`/`SolidColor` 的逐项隔离；`Groups` 的 Add / Remove / FindGroupOf；`FitView` / `Redraw` 不抛异常；`Dispose` 幂等。
- 测试项目新增 `SharpGL` / `SharpGL.SceneGraph` / `SharpGL.WinForms` 引用（`CxDisplay` 的基类是 `OpenGLControl`）与 `UseWindowsForms`。
- 验证：`dotnet test`（Debug 与 Release）→ **65/65 通过**；`CxControl` 与 `DemoFrom` 未受影响。
- 首轮 2 项失败同样是**测试用错 API**：`SetMesh(data, key)` 每次都会新建 item，因此"改名 / 同实例空操作"只能通过 `SetSurfaceItem(key, item)` 观察；已修正。
- 仍未覆盖：任何 GL / 绘制（含 M3 拆分的 6 个文件）；并发（测试单线程）。

### fc5a707（2026-10-30）M4：新增纯 CPU 回归测试项目 `CxControl.Tests`（47 项断言）

- 新增 `Test/CxControl.Tests/`（SDK 风格 csproj，net48，xUnit 2.9.2 + Microsoft.NET.Test.Sdk 17.11.1 + xunit.runner.visualstudio 2.8.2），并加入 `VisionNet.sln`（Test 解决方案文件夹下）。`PlatformTarget=x64`（CxControl 引用的是 AMD64 的 VisionNet）；对 VisionNet 采用 HintPath 引用（与 CxControl 一致，避免与 CxControl 的二进制引用冲突）。
- `CxControl/Properties/AssemblyInfo.cs` 新增 `[assembly: InternalsVisibleTo("CxControl.Tests")]`，使测试可直接驱动 `CxItemRegistry`（internal）——**本轮唯一的产物侧改动**，纯附加、不影响运行行为。
- 覆盖（全部不依赖 GL、不做任何渲染）：
  - `CxItemRegistryTests`（18 项）：新 key 注册；同 key **原位替换并保持槽位**、上报 `dispose=true`；改名**保持槽位**且 `dispose=false`；同实例重复 Set 为空操作；按 key / 按引用移除；清空上报每个条目；`CopyFrameSnapshot` 索引对齐；`ForEachPoolEntry` 遍历完整；`MarkNeedsUpdate`；`GetKeyOf`；`CopyItemsOfType<T>`；`DrainReleases` 恰好一次且 `null` 不入队；overlay 注册/替换/改名不重复/追加/按 key 与按引用移除/清空；`ContainsOverlay`；`CopyOverlayItems` 返回独立副本。
  - `CxExtensionTests`（11 项）：**新包围盒重载与旧 `ToPoints()` 路径逐项等值**（surface 与 point cloud，含 `-32768` 空洞与全空洞）、空输入返回 `null`、`ComputeWorldZRange`（含模型矩阵与 null box 的 `[-0.5, 0.5]` 兜底）、`GetColorByHeight` 端点、`IEnumerable` 重载忽略 Infinity。
  - `ComplexGeometryItemTests`（10 项）：`Opacity` 钳制并写入 uniform（且不重建渲染数据）、`SolidColor` 默认 217 灰与就地更新、`PointSize` 默认 2、**`Lit` 在 PointCloud 下也发 `colorMode = 4`（不再回退为 Color）**、颜色模式切换只改 uniform、PointCloud→Mesh 重建带索引数据、Mesh→PointCloud 复用缓存。
  - `CxMatrix4X4Tests`（5 项）：单位矩阵求逆、平移/缩放/乘法的组合顺序、`GLResourceHandle.ResetForContextRecreate` 清空名称并置 `NeedsUpdate`、uniform 位置缓存。
- 验证：`dotnet test`（Debug 与 Release）→ **47/47 通过**；`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）。
- 首轮有 3 项断言失败，全部是**测试期望写错**（把 `ComputeWorldZRange` 的 null-box 兜底当成 `0`、忘了平移叠加、把空洞点的 X/Y 跨度算成 0），已按真实语义修正 —— 说明断言不是恒真。
- 未覆盖：任何 GL / 绘制；以及"后台线程 Set 与渲染并发"的锁竞争（测试单线程、无上下文）。这两项仍需人工回归（见「待验证」第 15 项）。

### f02bcce（2026-10-30）M1 第 2/3 步：消除 `Sync` / `*InLock`，注册表状态完全私有

- 13 处 `_registry.Sync(() => { …*InLock… })` 全部替换为**目的明确的注册表方法**；`Sync` / `Sync<T>` 与 5 个 `*InLock` 访问器**已删除**（全项目残留 0 处）。注册表的 `_lock` / `_items` / `_keys` / `_renderItems` / `_overlayKeys` / `_pool` / `_pendingRelease` 现在全部 `private`，外部**只能**通过 31 个类型化成员访问 —— 绕锁访问在编译期已不可能。
- 复合流程整体搬进注册表，调用方需要为每个受影响项做的事通过回调上报：
  - `RegisterSurfaceItem(key, item, handle, displaced)`：upsert（append / 原位替换 / 改名保持槽位）整体在锁内完成；被顶掉的注册通过 `displaced(item, handle, dispose)` 上报（handle 已从池中摘除）。
  - `ClearSurfaceItems(displaced)`、`RemoveSurfaceItem(key|item, out …)`、`DisposeAll(releaseSurface, disposeOverlay)`、`ForEachPoolEntry(action)`、`ClearOverlayItems(dispose)`、`RegisterOverlayItem(key, item, out replaced)`、`RemoveOverlayItem(key|item)`、`AppendOverlayItem`、`GetKeyOf`、`MarkNeedsUpdate`。
  - `CxDisplay` 侧新增 `OnSurfaceRegistrationDisplaced`（退订 + 入延迟释放队列 + 可选 Dispose），替代原先的 `ReleaseSurfaceItem` / `DetachSurfaceRegistration` / `RemoveKeyForItem` / `DetachOverlayRegistration` / `RemoveOverlayKeyForItem`（均已删除）。
- **行为等价性核对**（逐方法比对操作顺序）：`SetSurfaceItem` 的 detach→原位替换→插入→key→池→订阅顺序一致（handle 由调用方先创建后传入，注册表内插池；订阅在 `_renderLock` 内完成，渲染帧无法插入）；`ClearSurfaceItems` / `RemoveSurfaceItem` ×2 / `SetOverlayItem` / `RemoveOverlayItem` ×2 / `ClearOverlayItems` / `ProcessResourcePool` / `GetSurfaceKeyOf` / `OnItemRenderDataChanged` 的步骤与副作用顺序均与改前相同；`DisposeResources` 保持「drain → Groups.Clear → surface（退订 + 直接释放 + Dispose）→ overlay Dispose → 清快照 → Release」的顺序（Dispose 时上下文 current，故句柄直接释放而非入队）。
- 回调在注册表锁内触发（`RegisterSurfaceItem` / `ClearSurfaceItems` / `DisposeAll`），与改前 `ReleaseSurfaceItem` 在 `_resourceLock` 内调用一致；`Dispose` 触发的回调若回头调用 display，`_renderLock` 与注册表锁对同线程可重入，无新死锁面。
- 验证：`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）。`CxDisplay.Api.cs` 1010 → 829 行。
- **未做运行时/OpenGL 验证** —— 本轮改的是最敏感的注册/替换/释放路径，且仓库无测试（M4 已推迟），**必须做一次交互回归**：见「待验证」第 15 项。

### 88e4511（2026-10-30）M1 第 1 步：CxItemRegistry 收拢队列与 GL 池；M3 第 1 步：ModernOverlayRenderer 纯文件移动

#### M1 第 1 步：把 surface/overlay 队列与 GL 资源池收进 `CxItemRegistry`

- 新增 `internal sealed class CxItemRegistry`：集中持有 surface 队列 + key 映射、overlay 队列 + key 映射、per-item GL 资源池、延迟释放队列，以及**守护它们的唯一锁**。
- `CxDisplay` 不再声明这些集合与 `_resourceLock`（`CxDisplay.*` 合计 −72 行）；锁顺序（`_renderLock → registry`）只在 registry 的 XML 注释里声明一次。
- 需要原子性的复合流程（`SetSurfaceItem` / `RemoveSurfaceItem` / `ClearSurfaceItems` / `SetOverlayItem` / `RemoveOverlayItem` / `ClearOverlayItems` / `DisposeResources` / `ProcessResourcePool` 等，共 13 处）改走 `_registry.Sync(...)`；块内状态访问走 `*InLock` 访问器（53 处）。
- 所有**锁外**访问改为线程安全的类型化方法：`GetByKey` / `SnapshotItems` / `CopyItems` / `CopyItemsOfType<T>` / `CopyFrameSnapshot` / `GetOverlayByKey` / `SnapshotOverlayItems` / `CopyOverlayItemsInto` / `ContainsOverlay` / `TryGetHandle` / `ForEachPoolHandle` / `EnqueueRelease` / `DrainReleases` / `DropReleases` —— 调用方无法漏加锁。
- 行为不变：复合流程的步骤顺序逐一保留；`GetCombinedBoundingBox` 改为内部取项快照，因此不再要求调用方持锁（3 处调用点去掉了锁包裹）。
- 验证：`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）；旧标识符（`_resourceLock` / `_surfaceItems` / `_surfaceKeys` / `_resourcePool` / `_pendingRelease` / `_renderItems` / `_overlayKeys`）在 registry 之外**零残留**（已核对）。
- 未做：第 2/3 步 —— 把 13 处 `Sync(() => { …*InLock… })` 换成目的明确的 registry 方法，并删除 `*InLock` 访问器（届时任何调用方都无法绕过锁）。
- 未做运行时/OpenGL 验证。

#### M3 第 1 步：`ModernOverlayRenderer` 纯文件移动（partial class）

- 1257 行的 `ModernOverlayRenderer.cs` 拆为 6 个 `partial class` 文件（**零逻辑改动**）：
  - `ModernOverlayRenderer.cs`（322 行）：嵌套类型、字段、GLSL 源码常量、生命周期（ctor / `For` / `ResetForContextRecreate` / `Release`）、模型栈、场景矩阵、帧窗口（`BeginFrame` / `EndFrame`）。
  - `ModernOverlayRenderer.Geometry.cs`（135 行）：`Draw` / `DrawScreenGeometry` / `DrawScreenGeometryColored`。
  - `ModernOverlayRenderer.Text.cs`（386 行）：文本纹理缓存、字形图集、文本绘制；`Project` / `Unproject` 因在源文件中夹在文本方法之间，一并落在此文件。
  - `ModernOverlayRenderer.Programs.cs`（246 行）：`UploadSceneMatrices` ×2、`GetState`、surface 程序、球体绘制与球体程序/缓冲/网格、geometry/text 的 `Ensure*Program` / `Ensure*Buffers`。
  - `ModernOverlayRenderer.State.cs`（69 行）：GL 状态快照与应用（`CaptureState` / `RestoreState` / `SetRenderState` / `RestoreRenderState` / `GetDepthWrite`）。
  - `ModernOverlayRenderer.Matrix.cs`（184 行）：`ReleasePrograms`、矩阵/视口/投影数学（`Multiply4x4` / `Ortho` / `To3D` / `IdentityData` / `RefreshViewport` 等）。
- 唯一非搬移改动：类声明加 `partial`；每个文件补 usings / namespace / 类头与收尾大括号。
- **等价性证据（可复现）**：切分脚本在写盘前校验"6 个文件正文拼接 == 原类体"逐行相同；另用独立脚本从 `git show HEAD:<原文件>` 取原类体，与磁盘上 6 个文件的正文再比对一次 → **1342 行逐行相同**。
- 验证：`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）。
- 未做：第 2 步 —— 把 `Text` / `Programs` / `State` 从 partial 提升为**独立 internal 类**（真正的职责解耦，需要 M4 单测网兜底）。
- 已知不纯：文件边界取在成员之间的空行，因此 `Text.cs` 含 `Project`/`Unproject`、`Programs.cs` 含球体绘制；第 2 步会做真正的职责切分。

### 6478e3f（2026-10-30）CxDisplay 渲染与交互效率、半透明支持、公开面收敛

- **E1 按需重绘**：`CxDisplay.Designer.cs` 设 `RenderTrigger = Manual`。此前只设 `FrameRate = 10` 而未设 `RenderTrigger`（默认 `TimerBased`），空闲时也按 10 FPS 全量重绘（Phase 1 全 Item 范围聚合 → Phase 3 全部绘制 → Phase 5 HUD 含色条交错与上传）；现无渲染定时器，悬停等入口改为显式 `SafeInvalidate()`。与文档「手动刷新」一致。另：左键点击的相机轴心复用选中路径已解算的世界坐标（`ResolveRotationPoint`），一次点击由两次深度读回降为一次。
  - 未做（有风险）：把 `FrameRate` 设为 `0` 以停掉空转定时器。SharpGL 的 `SetupDrawingTimer` 仅在定时器**已启用**时才跳过 `Interval = (int)(1000.0 / FrameRate)`；定时器已禁用时会走到 `(int)double.PositiveInfinity`，而 WinForms `Timer.Interval` 对 <1 的值抛 `ArgumentOutOfRangeException`。保留 `FrameRate = 10` 时该 tick 在 `Manual` 下只是一次比较，无实际开销。
- **E2 延迟拾取**：鼠标移动只记录坐标，深度读回 + 最近表面搜索改到渲染帧内（`CxDisplay.Render` 新增 Phase 4b）执行，每帧最多一次，且保证 GL 上下文 current；`CoordinatesChanged` 因此在**渲染帧内**触发（README 契约已同步，处理器不得同步 `Invoke`）。
- **E3 拾取路径**：`ModelMatrix` 的逆改为惰性缓存（`AbstractCxObjRenderItem` / `AbstractRenderItem` 各持一份，姿态变更时失效），`GetNearestSurfacePoint` 复用遍历缓冲 —— 不再每次鼠标移动对每个 Item 求逆并分配 List。
- **U1 点大小**：新增 `AbstractCxObjRenderItem.PointSize`（默认 2），`SurfaceMode.PointCloud` 下可配；此前只有 `CxDisplay.Render` 里一次性 `gl.PointSize(2.0f)`，无法调整。
- **M2 半透明与 GL 状态统一**：
  - 新增逐项 `Opacity`（`0..1`，默认 `1`，越界钳制）与着色器 `uniform float opacity` —— **所有**颜色模式的片元 alpha 都由它决定（此前硬编码 `1.0`）。
  - 渲染 Phase 3 改为**两趟**：先不透明、后半透明；半透明项关闭深度写入。全部不透明时顺序与结果逐像素不变。
  - 状态快照从 `ModernOverlayRenderer` 暴露为 internal（`CaptureState` / `RestoreState`，`RenderStateSnapshot` 改 internal），surface 路径现在也捕获/恢复 GL 状态；`SetRenderState` 在开启混合时**同时关闭深度写入** → 修掉 `CxBox3DItem` 等半透明 overlay 仍写深度、遮挡其后绘制几何的问题。
  - 已知限制（README 已记录）：半透明项之间不按远近排序、闭合网格不做背面剔除。
- **M5 公开面收敛**：`GLResourceHandle` 全部状态（含构造函数）改为 `internal`，类型与接口注释明确 `ICxObjRenderItem` 是内建项的实现契约、不支持程序集外实现；删除从未使用的 `RenderData.UseVAO`（`[Obsolete]`）。仓库内 `CxControl` 之外对这两个类型**零引用**（已核对）。
- **M6 文档漂移**：README 补 `PointSize` / `Opacity` / 半透明已知限制 / 按需重绘与 `Redraw()` 提醒；把「渲染线程」措辞修正为「渲染帧内（UI 线程）」（SharpGL 在 `OnPaint` 中绘制）；新增「提交前检查清单」（见文末维护要点）。
- 验证：`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）。
- 未做运行时/OpenGL 验证（见文末「待验证」第 12–14 项）。

### e23af5a（2026-10-30）Lit 纯色化：ComplexGeometry 新增纯色模式（SolidColor）

- `SurfaceColorMode.Lit` 由"固定 0.85 灰 + 光照"改为**纯色模式**：新增 `AbstractCxObjRenderItem.SolidColor`（`System.Drawing.Color`，默认 `RGB(217,217,217)`），着色器 `colorMode == 4` 分支改读 `solidColor.rgb`，不再硬编码 0.85。
- 着色器新增 `uniform vec4 solidColor`；`RenderData.Uniforms` 新增对 `System.Drawing.Color` 的支持（按 `vec4` 上传，`AbstractCxObjRenderItem.Draw` 的 uniform 循环扩展）。
- `PrepareRenderData()` 集中注入 `solidColor`，三个派生 Item（`CxSurfaceItem` / `CxPointCloudItem` / `CxMeshItem`）无需改动。
- **行为变化**：`EffectiveColorMode` 取消"`Lit` 非 `Mesh` 时回退为 `Color`"——`Lit` 在 `PointCloud` 模式下也生效（纯平色）；`SetGlobalZRange` 对 `Lit` 无条件早退；`CxDisplay.Render` Phase 1 移除同一回退 → 纯色 Item 不再显示颜色条。**混合场景**（`Lit` 与其他数据色模式共存）颜色条同样整条隐藏：Phase 1 的模式共识把 `Lit` 视为独立模式而不再折算为 `Color`；此前只有 `Lit` + `Mesh` 的混合场景如此，现在 `Lit` + `PointCloud` 也如此。
- `SolidColor` 是 uniform-only 更新，修改/切换不重建 GPU 资源。
- 验证：`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）。
- 未做运行时/OpenGL 验证（见文末「待验证」第 11 项）。

### 2585526（2026-10-29）渲染资源层重构：共享 surface 程序、球体实例化、字形图集与上下文重建

- 共享程序：三个内建 `ComplexGeometry` 渲染项每个 GL 上下文共用一份 surface 着色器程序（`ModernOverlayRenderer.EnsureSurfaceProgram`），句柄以 `OwnsShader` 标记归属，避免逐句柄编译/删除；是否共享按着色器源码文本判定。
- 上传：顶点/索引/纹理上传改为 `GCHandle` 固定内存直传（`UploadPinned`），去掉 `Marshal.AllocHGlobal` + `BlockCopy` 中转。
- 球体实例化：`CxPoint3DItem` 的 `Sphere` 模式改用上下文级单位球 + 每帧流式实例缓冲（`DrawSpheres`），不再每帧重建 VAO/VBO/着色器（半径仍为 `Size / 2`）。
- 字形图集：数字/符号文本（坐标提示、色条标签）改用按字号缓存的字形图集批量绘制（`GL_TRIANGLES`），不再为每个字符串生成整串纹理。
- 上下文重建：`DoOpenGLInitialized` 不再 `Delete` 旧上下文的程序，改为复位全部句柄（`ResetForContextRecreate`）并排空待释放队列，交由首帧重建。
- 重绘合并：`SafeInvalidate` 合并同一帧内的重复失效请求，`OnHandleCreated` / `OnHandleDestroyed` 复位合并标记。
- FBO 尺寸：缓存上次 `SetDimensions` 的尺寸，仅在控件尺寸变化时重设（原先每帧重设）。
- 验证：`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）。
- 未做运行时/OpenGL 验证（见「待验证」第 7–10 项）。

### 7900d29（2026-10-29）CxDisplay 交互/拾取与颜色条性能修复，包围盒改为直接扫描源数据

- 拾取：`GetNearestSurfacePoint` 路径复用实例级视口/深度读回缓冲，避免每次鼠标移动分配短生命周期数组；同一像素的连续移动不再重复读回、也不再重复触发 `CoordinatesChanged`（`CxDisplay.Api.cs` 与 `README.md` 的契约说明已同步为"同一像素的连续移动会被合并"）。
- 颜色条：`CxColorBarItem` 按 (Z 范围, 视口尺寸) 缓存几何，仅在变化时重算渐变色与刻度位置；**每帧仍会交错复制并流式上传**（约 800 顶点 / 22 KB），本轮未消除这部分。
- 包围盒：新增 `CxExtension.CalculateBoundingBox(CxSurface)` / `CalculateBoundingBox(CxPointCloud)`，直接从紧凑源数组扫描，避免为求包围盒构造整份 `CxPoint3D[]`；不再经 `ToPoints()`（也不再持有 `CxSurface._lock`）。数值结果与旧路径逐点等价。
- 模式切换：`SurfaceMode` setter 对相同值直接返回；PointCloud→Mesh 切换时惰性重建带索引的渲染数据，`Draw` 在 Mesh 模式且索引为空时跳过 `DrawElements(0)`。
- 输入：拖拽期间不再逐帧做工具提示的深度读回；按下鼠标时隐藏工具提示。
- 数据准备：`SetSurface` / `SetPointCloud` / `SetMesh` 在登记前显式调用 `PrepareRenderData()`，把 `BuildRenderData()`（大顶点/UV 数组与纹理字节）从渲染线程提前到调用线程执行；构造时 `Initialize()` 已在同一线程全量扫描，增量有限，属有意取舍。
- 验证：`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）。

### cfdab2b（2026-10-29）重组 3D 渲染项目录：表面类渲染项聚合为 `ComplexGeometry`

- `3D\RenderItem` 下 `Mesh` / `PointCloud` / `Surface` 三个目录合并为 `ComplexGeometry`，并移入只服务表面类渲染项的 `ICxObjRenderItem` / `AbstractCxObjRenderItem` / `Enums` / `RenderData` / `GLResourceHandle`。
- `ModernOverlayRenderer` / `ShaderProgramCompiler` 移入 `3D\RenderItem`（全部渲染项共用的 GLSL 管线与 shader 编译工具）。
- `IRenderItem` / `ICxPoseable` / `ICxGroupMember` 留在 `RenderItem` 根级（与叠加层共用）。
- 同步 `CxControl.csproj` 编译项与 `README.md` 架构树；纯文件移动，命名空间与公开 API 不变。
- 验证：`CxControl` 与 `DemoFrom` MSBuild `/t:Rebuild` → 0 错误、0 C# 警告（仅预存在 MSB3270）。

### ff08025（2026-10-28）CxDisplay 可用性与性能提升：公开事件、overlay key、渲染项基类抽取

**内部重构（A 批）**

- 性能：`SetSurfaceItem` 仅在 `autoFit` 时计算合并包围盒（批量加载由 O(n²) 降为 O(n)）；`Render` 每帧只取一次 `_resourceLock`（原先每个 Item 一次）。
- 脏标记精确化：`GLResourceHandle` 记录订阅委托，单个 Item 数据失效只重建它自己的 GPU 资源（原先会标记全部 Item 重传）。

**契约收敛（C 批，本轮只做 C5）**

- C5（渲染项去重，方案 A：public 抽象基类）：新增 `AbstractCxObjRenderItem`（`public abstract`，承载 `ICxObjRenderItem` / `ICxGroupMember` 的共享实现：姿态、颜色模式与颜色范围、着色器、uniform、`Draw` / `Dispose` 骨架、采样预算 `MaxPointCountCore`）；`CxSurfaceItem` / `CxMeshItem` / `CxPointCloudItem` 改为派生类，只保留数据相关部分，行数由 553/469/557 降到 **233/148/238**（基类 427 行）。
- 公开面变化：**+1 公开抽象类型**（`AbstractCxObjRenderItem`）；三个渲染项的**有效公开成员集合（含继承）与重构前完全一致**（反射核对 0 差异）；着色器文本、`Draw`、颜色模式与范围策略各只保留一份。
- 说明：`CxSurfaceItem` / `CxPointCloudItem` 仍公开 `MaxPointCount`（基类提供 `protected MaxPointCountCore`），`CxMeshItem` 不暴露该属性；此前"internal 静态辅助类"方案（A1）已回撤，本方案取代它。

**新增公开 API（B 批，向后兼容）**

- 事件：`SelectionChanged`、`CoordinatesChanged`（均 UI 线程）、`RenderItemFailed`（渲染线程，每次上传失败触发一次）。
- 叠加层 key：8 个 `Set*` 增加可选 `string key = null`（null 保持"追加"旧行为，非 null 为原位 upsert）；新增 `SetOverlayItem(key, item)` / `GetOverlayItem(key)` / `RemoveOverlayItem(key|item)`；overlay key 与 surface key 为独立命名空间。
- 视图入口：`Redraw()`（线程安全重绘）、`FitView()` / `FitView(CxBox3D?)`。
- 重复代理加 `[Obsolete]` 指引（未删除）：`SurfaceViewMode`、`IsLeftHanded`、`SetCoordinateSystemLeftHanded`、`SetZScale`、`SetViewCenter`、`SetViewUpDirection` → 统一指向 `Camera.*`；Demo 已迁到 `Camera.*`。
- Demo：新增 "Overlay Key" 按钮（追加 → 同 key 替换 → 移除）与 `SelectionChanged` → `lbl_markPos`。
- 评审修复：`RenderItemFailed` 改为在**资源锁释放、资源池遍历结束之后**触发（处理器可安全回调 display；WinForms 控件仍需 `BeginInvoke`，同步 `Invoke` 可能卡住渲染帧）；`SetOverlayItem` / `SetSurfaceItem` 现在保证**同一实例只登记一次**——对已登记实例再次 Set 视为改 key（保持原队列位置），避免重复绘制、GL 句柄泄漏与悬空的已释放条目。

### 6943291（2026-10-28）组关系收归 `CxGroupManager`，组员按 key 绑定

**接口变更**

- 新增 `CxDisplay.Groups`（`CxGroupManager`），作为组关系与姿态的**唯一入口**：
  `Create` / `Add`（surface、overlay、子组三个重载）/ `Remove`（item、overlay、按 key 三个重载）/ `RemoveGroup` / `SetTransform` / `SetMemberLocalTransform` / `FindGroupOf`（两个重载）。
- 删除 `CxDisplay` 上的 `CreateGroup` / `AddToGroup`（3 个）/ `RemoveFromGroup`（2 个）/ `RemoveGroup` / `SetGroupTransform` / `FindGroupOf`（2 个），不留别名。
  迁移对照：`cxDisplay1.CreateGroup(...)` → `cxDisplay1.Groups.Create(...)`；`cxDisplay1.AddToGroup(g, item)` → `cxDisplay1.Groups.Add(g, item)`；`cxDisplay1.SetGroupTransform(g, m)` → `cxDisplay1.Groups.SetTransform(g, m)`；`RemoveFromGroup` → `Groups.Remove`。

**行为变更（重要）**

- surface 类成员（Mesh / PointCloud / Surface）改为**按 key 绑定**（`CxGroupLeaf.Key`），overlay 成员仍按直接引用绑定。
  - **组内替换 = 同 key 再 Set**：`SetMesh(newMesh, key: "Vehicle", mode: ..., colorMode: ..., autoFit: false)` 一条调用即完成替换，新对象自动接替组内局部姿态；不需要也没有新增 Replace API。
  - key 无对应资源（被同 key 覆盖、`RemoveSurfaceItem`、`ClearSurfaceItems`）时，组内 leaf **只跳过、不摘除、不报错**；之后同 key 再 Set 会自动重新绑定并恢复原局部姿态。
  - 需要彻底移除悬空 leaf 时用 `Groups.Remove(group, key)`。
- `SetSurfaceItem` / `RemoveSurfaceItem(key|item)` / `ClearSurfaceItems` 不再摘除组 leaf；`ClearOverlayItems` 仍摘除 overlay leaf；`ResetView(true)` 与控件 `Dispose` 改为 `Groups.Clear()`。
- 姿态写入收紧：`CxGroup.Transform`、`CxGroupLeaf.LocalMatrix` 的 setter 改为 `internal`，只能经 manager 修改。
- 职责边界：manager 只做**关系 + 姿态**（不 Dispose、不释放 GL、不改 key、不动渲染队列）；资源生命周期（队列、key、GL 释放）仍归 `CxDisplay`。
- 示例同步：`Test/DemoFrom/DemoFrom.cs` 的 18 处组调用迁移到 `cxDisplay1.Groups.*`（另 1 处注释同步），并新增 "Replace Vehicle" 按钮，演示"同 key 再 Set 即完成组内替换"。

**验证**

- `CxControl.csproj`、`DemoFrom.csproj`：MSBuild `/t:Rebuild` → 0 错误、0 C# 警告。
- **未做运行时/OpenGL 验证**（见文末「待验证」）。

### 8a7ec19（2026-10-27）重构表面项管理为按 key 的 Set/Get/Remove，并修复渲染问题

- 合并 `CxSurfaceAdvancedItem` / `CxMeshAdvancedItem` / `CxPointCloudAdvancedItem` 到 `CxSurfaceItem` / `CxMeshItem` / `CxPointCloudItem`，删除三个 Advanced 文件与空壳继承；三类直接实现 `ICxObjRenderItem, ICxGroupMember`。
- 表面 API 收敛为**按 key 的 upsert**（合并原 Add 语义），迁移对照：

| 旧 API | 新 API |
| --- | --- |
| `SetSurface` / `SetPointCloud` / `SetMesh`（替换全部内容） | `SetSurface(s, key, pose, mode, colorMode, maxPointCount, autoFit)` / `SetPointCloud(...)` / `SetMesh(m, key, pose, mode, colorMode, autoFit)`；按 key upsert，默认 key 为 `"Surface"` / `"PointCloud"` / `"Mesh"` |
| `Set*WithPose`、`Add*`、`Add*WithPose` | 同上，位姿改用 `pose:` 参数，并存对象改用不同 key |
| `Set*AdvancedItem`、`Add*AdvancedItem`、`Set*AdvancedItemPose`、`AddSurfaceItem` | 已删除；通用入口 `SetSurfaceItem(key, item, autoFit)` |
| （原无） | `GetSurface` / `GetPointCloud` / `GetMesh` / `GetSurfaceItem`、`RemoveSurfaceItem(key)` |

- key 不变量：**一个 item 最多对应一个 key**；`_surfaceKeys` 与 `_surfaceItems` 同步维护，锁顺序恒为 `_renderLock → _resourceLock`。
- 新增 `ClearOverlayItems()`（清空叠加层，不影响表面对象与内置 HUD；会先清选中项并复位拖拽状态），`ResetView` 改为复用它。
- `CxDisplay.Api.cs` / `CxDisplay.Group.cs` 使用 `#region` 分块，替换原 `// ──` 标题。
- 渲染修复：着色器编译失败记录 `ShaderFailed`，不再每帧重传几何/纹理；`ModernOverlayRenderer` 编译失败后记录失败标志并跳过绘制；Box 线框边线与顶点标记保持不透明。
- 同步更新 `Test/DemoFrom/DemoFrom.cs` 与 `README.md`。

### de03ec9（2026-10-27）Modernize CxDisplay rendering pipeline

- 现代 OpenGL 渲染资源与绘制流程重构，抽取独立叠加层绘制器与 shader 编译/资源管理；`maxPointCount` 校验、文本纹理缓存、`Lit` 回退等修复。
- 该轮确认的兼容性决策：程序集二进制兼容性破坏已获批准；Legacy `CxTrackBallCamera` 的 2D 旋转问题不再纳入现代管线验收范围。

### 更早

- `77dbbc7`（2026-10-21）优化表面项管理和更新方法。
- `33ad837`（2026-10-21）合并 Daily 分支更新（排除 Demo）。
- `02f9933`（2026-10-21）增加 ply 导入导出。

## 工作区文档变更

> 按 Keep a Changelog 风格，ISO 日期，最新在上。记录工作区文档与协作层面的变更。

### 2026-11-05

**Added**
- 初始化工作区文档：`Agents.md`（接入说明）、`Changlog.md`（本文件）、`ToDo.md`（任务清单）。
- 合并既有文档到上述三个文件并删除原文件：
  - `CHANGELOG.md` → 产品变更历史并入本文件；「架构现状与维护要点」并入 `Agents.md`；「待验证」并入 `ToDo.md`。
  - `TODO-CxDisplay-refactor.md` → 并入 `ToDo.md` 的「重构专项（S0–S6）」。
  - `TODO-CxDisplay-render-quality.md` → 并入 `ToDo.md` 的「渲染质量专项（P1–P4 / AA / 半透明）」。

**验证**
- 三个文件均存在，相对链接可解析；除测试注释里的一处文档引用外未改动代码。
- 未执行 Git 提交或推送。
