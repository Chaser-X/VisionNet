# CxDisplay（3D）重构待办

> 范围：`src/Controls/CxControl/3D/`（不含 CxDisplay2d）。
> 原则：小步、行为不变；每阶段独立提交并在 `CHANGELOG.md` 归档。
> 本轮执行 **S0 / S1 / S2**；S3–S6 仅登记，不在本轮。

## S0 安全网

- [ ] 特征测试：`GetNearestSurfacePoint` 纯算法部分（S2 抽出后覆盖）
- [ ] 特征测试：`CxColorBarItem` 刻度文本与 `divisions` 行为
- [x] 特征测试：`CxExtension` 三处包围盒收尾结果一致性（`CxBoundingBoxConsistencyTests`，5 项）
- [x] 性能基线：手动基准（每帧耗时 + Gen0 次数），结果记入 CHANGELOG（`CxDisplayCpuBenchmarkTests`）

## S1 低风险去重

- [ ] `CxExtension.cs` 三处包围盒收尾抽 `BuildBox(min, max)`
- [ ] Point/Segment/Plane/Polygon 缓冲扩容统一用 `ModernGLRenderer.Matrix.cs` 的 `EnsureCapacity`
- [ ] `PointToSegDistSq`（Polygon/Segment 各一份）抽共享静态 helper
- [ ] 菜单 foreach（`CxDisplay.cs`、`CxDisplay.Input.cs`）抽 `ForEachMenuItem`
- [ ] `ModernGLRenderer.Geometry` 三份绘制流程抽状态作用域（基于 State partial 的 Capture/Restore）
- [ ] 魔法数字提常量：`PointSize 2.0f`、色条 `divisions`（`7` / `7+1`）、球体分段、`-32768`、5×5 邻域与阈值

## S2 拆长方法

- [ ] `CxDisplay.Render.cs` 的 `Render` 拆为 `UpdateTransforms / UploadPending / DrawSurfaces / DrawOverlays / DrawColorBar`，色条 4 处重复 `SetRange+Draw` 循环化
- [ ] `CxDisplay.GLResources.cs` 的 `CreateGLResources` 抽 `UploadVertexBuffer(...)`
- [ ] `CxDisplay.Input.cs` 的 `GetNearestSurfacePoint` 拆为取深度 / 邻域搜索 / 候选筛选 / 变换，纯数学部分去 GL 依赖

## 后续待办（不在本轮）

- [ ] S3 多态替换类型分支：`Input.cs`、`CxDisplay.cs` 的 `is` 判断改为虚方法；共享程序判断改显式 `ProgramKey`
- [ ] S4 结构拆分：`Text / Programs / State` 提升为独立类；抽出 `CxPickingService`、`CxGLResourceManager`；打破 `Display ⇄ GroupManager` 循环
- [ ] S5 线程与锁：registry 回调移出锁内、缩小 `_renderLock` 范围、`Dispose` 非 UI 线程改 `BeginInvoke`
- [ ] S6 效能：色条/标签脏标记、Uniforms 强类型化、projection/view 每帧一次、overlay 合批、缓冲复用、遍历合并、拾取节流
- [ ] 清理 `[Obsolete]` 代理（`CxDisplay.cs`）与 `CxControl.csproj_bak` —— 需先确认无外部调用
