# 更新记录（CxControl / CxDisplay）

> 用途：记录影响**公共契约、渲染行为、架构边界**的变更，便于后续维护与交接。
> 约定：新变更先写入「未发布」；提交后把条目移到「已提交」并补上提交哈希。
> 相关文档：`README.md`（对外 API 说明）。

## 未发布（工作区，尚未提交）

### A 批：内部重构
- 性能：`SetSurfaceItem` 仅在 `autoFit` 时计算合并包围盒（批量加载由 O(n²) 降为 O(n)）；`Render` 每帧只取一次 `_resourceLock`（原先每个 Item 一次）。
- 脏标记精确化：`GLResourceHandle` 记录订阅委托，单个 Item 数据失效只重建它自己的 GPU 资源（原先会标记全部 Item 重传）。

### C 批：契约收敛（本轮只做 C5）
- C5（渲染项去重，方案 A：public 抽象基类）：新增 `AbstractCxObjRenderItem`（`public abstract`，承载 `ICxObjRenderItem` / `ICxGroupMember` 的共享实现：姿态、颜色模式与颜色范围、着色器、uniform、`Draw` / `Dispose` 骨架、采样预算 `MaxPointCountCore`）；`CxSurfaceItem` / `CxMeshItem` / `CxPointCloudItem` 改为派生类，只保留数据相关部分，行数由 553/469/557 降到 **233/148/238**（基类 427 行）。
- 公开面变化：**+1 公开抽象类型**（`AbstractCxObjRenderItem`）；三个渲染项的**有效公开成员集合（含继承）与重构前完全一致**（反射核对 0 差异）；着色器文本、`Draw`、颜色模式与范围策略各只保留一份。
- 说明：`CxSurfaceItem` / `CxPointCloudItem` 仍公开 `MaxPointCount`（基类提供 `protected MaxPointCountCore`），`CxMeshItem` 不暴露该属性；此前"internal 静态辅助类"方案（A1）已回撤，本方案取代它。

### B 批：新增公开 API（向后兼容）
- 事件：`SelectionChanged`、`CoordinatesChanged`（均 UI 线程）、`RenderItemFailed`（渲染线程，每次上传失败触发一次）。
- 叠加层 key：8 个 `Set*` 增加可选 `string key = null`（null 保持"追加"旧行为，非 null 为原位 upsert）；新增 `SetOverlayItem(key, item)` / `GetOverlayItem(key)` / `RemoveOverlayItem(key|item)`；overlay key 与 surface key 为独立命名空间。
- 视图入口：`Redraw()`（线程安全重绘）、`FitView()` / `FitView(CxBox3D?)`。
- 重复代理加 `[Obsolete]` 指引（未删除）：`SurfaceViewMode`、`IsLeftHanded`、`SetCoordinateSystemLeftHanded`、`SetZScale`、`SetViewCenter`、`SetViewUpDirection` → 统一指向 `Camera.*`；Demo 已迁到 `Camera.*`。
- Demo：新增 "Overlay Key" 按钮（追加 → 同 key 替换 → 移除）与 `SelectionChanged` → `lbl_markPos`。
- 评审修复：`RenderItemFailed` 改为在**资源锁释放、资源池遍历结束之后**触发（处理器可安全回调 display；WinForms 控件仍需 `BeginInvoke`，同步 `Invoke` 可能卡住渲染帧）；`SetOverlayItem` / `SetSurfaceItem` 现在保证**同一实例只登记一次**——对已登记实例再次 Set 视为改 key（保持原队列位置），避免重复绘制、GL 句柄泄漏与悬空的已释放条目。

## 已提交

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

## 已提交

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
