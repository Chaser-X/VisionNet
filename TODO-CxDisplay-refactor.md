# CxDisplay（3D）重构待办

> 范围：`src/Controls/CxControl/3D/`（不含 CxDisplay2d）。
> 原则：小步、行为不变；每阶段独立提交并在 `CHANGELOG.md` 归档。
> 本轮执行 **S0 / S1 / S2**；S3–S6 仅登记，不在本轮。

## S0 安全网

- [x] 特征测试：`GetNearestSurfacePoint` 纯算法部分（`SurfacePickNeighbourhoodTests`，10 项）
- [x] 特征测试：`CxColorBarItem` 刻度文本与 `divisions` 行为（`CxColorBarTickTests`，7 项）
- [x] 特征测试：`CxExtension` 三处包围盒收尾结果一致性（`CxBoundingBoxConsistencyTests`，5 项）
- [x] 性能基线：手动基准（每帧耗时 + Gen0 次数），结果记入 CHANGELOG（`CxDisplayCpuBenchmarkTests`）

## S1 低风险去重

- [x] `CxExtension.cs` 三处包围盒收尾抽 `BuildBox(...)`（并复用到 `CxDisplay.GetCombinedBoundingBox`）
- [x] 缓冲扩容统一为 `AbstractRenderItem.EnsureExactCapacity`（**精确**分配，非 grow-only —— 见 CHANGELOG 说明）
- [x] `PointToSegDistSq`（Polygon/Segment 各一份）抽到 `AbstractRenderItem`
- [x] 菜单 foreach 抽 `SyncMenuSelection` / `UncheckAllMenuItems`
- [x] `ModernGLRenderer.Geometry` 三份绘制流程抽 `BeginGeometryDraw` / `EndGeometryDraw`
- [x] 魔法数字提常量：`InitialPointSize`、色条 `TickDivisions`、`SphereSlices/Stacks`、`CxExtension.NoDataValue`
- [ ] 5×5 邻域与阈值常量 —— 挪到 S2 与拾取算法抽取一起做

## S2 拆长方法

- [x] `CxDisplay.Render.cs` 的 `Render` 拆为 `AggregateFrameRanges` / `PropagateFrameRanges` / `DrawSurfaceItems` / `DrawOverlays` / `ResolvePendingPick` / `DrawHud`，色条 4 处重复 `SetRange+Draw` 收敛为 `TryResolveColorBarRange`
- [x] `CxDisplay.GLResources.cs` 的 `CreateGLResources` 抽 `UploadVertexBuffer(...)`（5 段样板合一）
- [x] `CxDisplay.Input.cs` 的 `GetNearestSurfacePoint` 拆出纯函数 `SearchSurfaceNeighbourhood`（去 GL 依赖，可直接单测）

## 后续待办（不在本轮）

- [ ] S3 多态替换类型分支：`Input.cs`、`CxDisplay.cs` 的 `is` 判断改为虚方法；共享程序判断改显式 `ProgramKey`
- [ ] S4 结构拆分：`Text / Programs / State` 提升为独立类；抽出 `CxPickingService`、`CxGLResourceManager`；打破 `Display ⇄ GroupManager` 循环
- [ ] S5 线程与锁：registry 回调移出锁内、缩小 `_renderLock` 范围、`Dispose` 非 UI 线程改 `BeginInvoke`
- [ ] S6 效能：色条/标签脏标记、Uniforms 强类型化、projection/view 每帧一次、overlay 合批、缓冲复用、遍历合并、拾取节流
- [ ] 清理 `[Obsolete]` 代理（`CxDisplay.cs`）与 `CxControl.csproj_bak` —— 需先确认无外部调用
