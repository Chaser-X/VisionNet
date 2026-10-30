# 更新记录（CxControl / CxDisplay）

> 用途：记录影响**公共契约、渲染行为、架构边界**的变更，便于后续维护与交接。
> 约定：新变更先写入「未发布」；提交后把条目移到「已提交」并补上提交哈希。
> 相关文档：`README.md`（对外 API 说明）。

## 未发布（工作区，尚未提交）

### 重命名与归档：`ModernOverlayRenderer` → `ModernGLRenderer` @ `3D\RenderItem\Shared\`

- 该类型实际服务**全部**渲染项 —— 既服务 `ComplexGeometry` 表面项（`EnsureSurfaceProgram` 提供共享 surface 程序、`UploadSceneMatrices` 上传矩阵、`CaptureState`/`RestoreState` 提供状态作用域），也服务 overlay 项；旧名 `ModernOverlayRenderer` 容易让人误以为只针对叠加层。改名并归入独立文件夹。
- `3D\RenderItem\` 下的 6 个 partial 文件 → `3D\RenderItem\Shared\ModernGLRenderer{.Geometry,.Text,.Programs,.State,.Matrix}.cs`；`ShaderProgramCompiler.cs` 一并移入同一文件夹（它是这套管线的 shader 编译工具）。
- 类名替换：全项目 **40 处引用**（19 个调用文件 + 6 个 partial 自身），残留 **0**；`CxControl.csproj` 的 7 个 `Compile Include` 同步；README 架构树补出 `RenderItem/Shared/` 一行；CHANGELOG「架构现状」补出 `ModernGLRenderer` 与 `CxItemRegistry` 两项，并注明旧名易误导。
- 类的 XML 文档改写：由「Shared renderer for the small, immediate-data overlays」改为「per-context rendering services shared by every render item」，并明确它不只为叠加层服务。
- **纯重命名 + 文件移动，无行为改动**：除类名与注释/文档外没有其他代码变更。
- 验证：`CxControl` / `DemoFrom` / `CxControl.Tests` MSBuild `/t:Rebuild`（Debug/Release）→ 0 错误、0 C# 警告（仅预存在 MSB3270）；`dotnet test` → **65/65 通过**（Debug 与 Release）。

## 已提交

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

## 架构现状与维护要点（交接用）

**分层**

- `CxDisplay`（partial）：渲染管线（`CxDisplay.Render.cs`）、GL 资源生命周期（`CxDisplay.GLResources.cs`）、表面与叠加层队列 / key / GL 池（全部收在 `CxItemRegistry` 内，见 `CxDisplay.cs` 的 `_registry`）、输入与拾取（`CxDisplay.Input.cs`）。
- `CxItemRegistry`（`3D\CxItemRegistry.cs`）：**唯一**持有 surface 队列 + key、overlay 队列 + key、per-item GL 池与延迟释放队列，以及守护它们的锁；对外只暴露类型化方法，绕锁访问在编译期不可能。
- `CxGroupManager`（经 `CxDisplay.Groups` 访问）：组拓扑 + 成员关系 + 姿态；surface 成员按 key 解析、overlay 按引用。
- `CxGroup` / `CxGroupLeaf`：纯数据结构（叶子只存 key 或直接引用 + 局部姿态），遍历与姿态运算都在 manager 里。
- RenderItem：`CxSurfaceItem` / `CxMeshItem` / `CxPointCloudItem`（`ICxObjRenderItem`）与 `AbstractRenderItem` 派生叠加项。
- `3D\RenderItem\Shared\ModernGLRenderer`（6 个 partial 文件）：**全部渲染项共用**的 per-context GL 服务 —— GLSL 程序（geometry / text / surface / sphere）+ 可复用 VAO/VBO、文本纹理与字形图集缓存、GL 状态快照（`CaptureState` / `RestoreState`）、相机矩阵与模型栈、CPU 侧 project/unproject；`ShaderProgramCompiler` 是它的 shader 编译工具。（旧名 `ModernOverlayRenderer` 容易让人误以为只服务叠加层，已改名。）

**线程与锁**

- 锁顺序恒为 `_renderLock → _resourceLock`；渲染为**按需刷新**（`SafeInvalidate()`，`RenderTrigger = Manual`，无渲染定时器）。
- 渲染发生在 **UI 线程**（SharpGL 在 `OnPaint` 中绘制并 `MakeCurrent`），因此 `glReadPixels` / `glGetInteger` 等只能出现在渲染帧内 —— 悬停拾取为此改为帧内延迟执行（Phase 4b）。
- 组 API 跨线程调用会 `BeginInvoke` 到 UI 线程（返回值按现有约定为 `false` / `null`）。
- GL 删除只能在渲染帧内：surface 走 `_pendingRelease` 队列延迟释放；overlay 的 GL 对象由共享 `ModernGLRenderer` 持有（item 只持有托管数组）。

**已知坑**

- `Get*` 拿到 item 后**直接改底层数据不会更新**（CPU 缓存与 GPU 缓冲都不会重建）：必须重新 `Set*(..., key)`。只改姿态时用 `Set*Pose` / `Groups.SetMemberLocalTransform`，或改完自行触发重绘（`Invalidate()`）。
- 直接改 `MaxPointCount` 不会重建采样；直接赋值 `ColorMin` / `ColorMax` 不生效（Draw 上传的是缓存 uniform，由显示层每帧聚合下发）。
- overlay 成员无法重绑（没有 key 概念）；如需替换 overlay 成员，需要另加资源层 API。
- 根目录 `end` 是历史 `git log` 转储文件（内容含旧 API 名，非代码），全仓库检索时注意排除。

**提交前检查清单（防文档漂移）**

- 新增/删除枚举成员 → 同步 README「渲染模式」表，并确认右键菜单项的 `Text` 与枚举名一致（`UpdateMenuItems` / `Enum.Parse` 依赖它）。
- 新增/删除公开成员 → 同步 README「API 参考」与 CHANGELOG「未发布」。
- 改动渲染 / 线程行为 → 同步 README「线程安全与重绘」与本文件「架构现状」。
- CHANGELOG：本次条目先留在「未发布」；**下一次**提交时移入「已提交」并补提交哈希（避免「未发布」段长期写着已提交内容）。

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
10. FBO 尺寸缓存：resize 路径（基类 `OnSizeChanged` 已 `SetDimensions`，首帧是否重复）行为正常。
11. `Lit` 纯色模式：`SolidColor` 生效（Mesh 下保留光照立体感、PointCloud 下为纯平色），且纯色 Item 不再触发颜色条。
12. 按需重绘（E1）：相机旋转/平移/缩放、数据加载、选中与拖拽、组变换、GL 上下文重建后画面是否都及时刷新（不再有 10 FPS 定时兜底）；后台线程准备完数据后未经过 `Set*` / `Redraw()` 时不刷新（预期行为，README 已注明）。
13. 延迟拾取（E2）：悬停坐标标签是否随鼠标及时更新、与旧行为一致；`CoordinatesChanged` 现在在渲染帧内触发，处理器若用同步 `Invoke` 会卡帧。
14. 半透明（M2）：`Opacity < 1` 的 Item 是否正确混合在不透明场景之上；全不透明场景的渲染结果与改动前逐像素一致；`CxBox3DItem` 等半透明 overlay 不再遮挡其后绘制的几何。
15. 注册/替换/释放路径（M1 第 2/3 步）：
    - **CPU 层已用临时脚本覆盖**（不新增项目、不进仓库）：驱动公开 API + 反射校验注册表内部不变量，55 项断言全过 —— 含新 key 注册、同 key 替换、改名保持槽位、跨类型替换、同实例重复 Set 为空操作、按 key 移除、清空、overlay 注册/替换/移除/清空、`Dispose` 后状态清空，以及渲染帧侧 API（`CopyFrameSnapshot` 索引对齐、`ForEachPoolEntry`、`GetKeyOf`、`MarkNeedsUpdate`、`DrainReleases` 恰好一次）。**该脚本只证明当前语义自洽，不能证明与重构前等价**（无 oracle）。
    - **仍需 GL 侧人工回归**（脚本无 GL 上下文，不覆盖绘制与真实资源释放）：同 key 反复 `Set*`（含不同类型互替）后绘制顺序与画面正常；`Set*(otherKey, 已登记实例)` 不重复绘制；`RemoveSurfaceItem` / `ClearSurfaceItems` / `ClearOverlayItems` / `SetOverlayItem` 同 key 替换后无残留句柄；控件 `Dispose` 后无 GL 资源泄漏或异常；后台线程 Set 与渲染并发时不抛异常（脚本为单线程，未覆盖锁竞争）。
