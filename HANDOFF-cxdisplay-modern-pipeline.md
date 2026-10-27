# CxDisplay Modern Pipeline Handoff

## 状态

- 分支：`feature/cxdisplay-modern-pipeline`
- 目标：将 CxDisplay 的渲染资源、几何绘制、叠加层和相机矩阵整理到现代渲染路径。
- 本地审查结论：最近一轮 `review-agent` 审查为 `No findings.`，满足提交标准。
- 远程状态：未推送。

## 本轮完成内容

- 重构 CxDisplay 的现代 OpenGL 渲染资源和绘制流程。
- 抽取独立的叠加层绘制器及 shader 编译/资源管理逻辑。
- 统一几何、网格、曲面、点云和 2D 叠加项的矩阵与颜色处理。
- 增加 `maxPointCount` 的范围校验，并限制采样预算，避免异常配置导致资源或内存超限。
- 修复独立文本辅助方法复用过期矩阵的问题。
- 修复独立文本纹理缓存无界增长的问题。
- 修复独立几何绘制缺少场景矩阵的问题。
- 修复 `Lit` 回退到 `Color` 时的颜色范围处理。
- 增加 Advanced 相机 `LookAtMatrix` 对固定管线的兼容适配。
- 修复 shader 编译失败后的资源清理与重试路径。

## 已确认的兼容性决策

- 默认渲染路径与 OpenGL 2.1 的兼容性已完成测试，可接受。
- 本次程序集二进制兼容性破坏已明确允许。
- Legacy `CxTrackBallCamera` 的 2D 旋转矩阵问题不再处理；Legacy 相机本身已不作为目标路径。

## 验证结果

已通过：

```text
dotnet build src/Controls/CxControl/CxControl.csproj -c Release --no-restore
0 warnings, 0 errors

dotnet build Test/DemoFrom/DemoFrom.csproj -c Release --no-restore
0 warnings, 0 errors

git diff --check
无格式错误
```

整体 `VisionNet.sln` 未作为通过条件：当前机器缺少 `VCTargetsPath`，且现有 Test 工程存在目标框架/引用配置问题。尚未进行真实 OpenGL/WinForms 运行验证。

## 后续建议

1. 在具备完整 Visual C++ 构建环境的机器上验证整体解决方案。
2. 在实际 OpenGL/WinForms 宿主中覆盖 shader 编译失败重试、文本纹理缓存、点数上限和相机矩阵场景。
3. 发布前保留本次已确认的兼容性决策，避免将 Legacy 2D 相机问题重新纳入现代管线验收范围。

## 提交

- 提交信息：`Modernize CxDisplay rendering pipeline`
- 提交哈希：以当前分支 `HEAD` 为准（文档不嵌入自身提交哈希，避免 amend 后失效）。
