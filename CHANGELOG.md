# 更新记录（CxControl / CxDisplay）

> 用途：记录影响**公共契约、渲染行为、架构边界**的变更，便于后续维护与交接。
> 约定：新变更先写入「未发布」；提交后把条目移到「已提交」并补上提交哈希。
> 相关文档：`README.md`（对外 API 说明）。

## 未发布（工作区，尚未提交）

（暂无）

## 已提交

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

- `CxDisplay`（partial）：渲染管线（`CxDisplay.Render.cs`）、GL 资源生命周期（`CxDisplay.GLResources.cs` + `_resourcePool` / `_pendingRelease`）、表面队列与 key（`_surfaceItems` / `_surfaceKeys`）、叠加层队列（`_renderItems`）、输入与拾取（`CxDisplay.Input.cs`）。
- `CxGroupManager`（经 `CxDisplay.Groups` 访问）：组拓扑 + 成员关系 + 姿态；surface 成员按 key 解析、overlay 按引用。
- `CxGroup` / `CxGroupLeaf`：纯数据结构（叶子只存 key 或直接引用 + 局部姿态），遍历与姿态运算都在 manager 里。
- RenderItem：`CxSurfaceItem` / `CxMeshItem` / `CxPointCloudItem`（`ICxObjRenderItem`）与 `AbstractRenderItem` 派生叠加项。

**线程与锁**

- 锁顺序恒为 `_renderLock → _resourceLock`；渲染为**手动刷新**（`SafeInvalidate()`，无渲染定时器）。
- 组 API 跨线程调用会 `BeginInvoke` 到 UI 线程（返回值按现有约定为 `false` / `null`）。
- GL 删除只能在渲染线程：surface 走 `_pendingRelease` 队列延迟释放；overlay 的 GL 对象由共享 `ModernOverlayRenderer` 持有（item 只持有托管数组）。

**已知坑**

- `Get*` 拿到 item 后**直接改底层数据不会更新**（CPU 缓存与 GPU 缓冲都不会重建）：必须重新 `Set*(..., key)`。只改姿态时用 `Set*Pose` / `Groups.SetMemberLocalTransform`，或改完自行触发重绘（`Invalidate()`）。
- 直接改 `MaxPointCount` 不会重建采样；直接赋值 `ColorMin` / `ColorMax` 不生效（Draw 上传的是缓存 uniform，由显示层每帧聚合下发）。
- overlay 成员无法重绑（没有 key 概念）；如需替换 overlay 成员，需要另加资源层 API。
- 根目录 `end` 是历史 `git log` 转储文件（内容含旧 API 名，非代码），全仓库检索时注意排除。

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
