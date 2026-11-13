# M2-6 固定角色双门矩阵（2026-11-13）

## 结论

M2-6 已完成固定 12 条角色分母的逐条汇总，但总体状态为 **PARTIAL**，不能宣称固定阵容完成。矩阵入口为 [`fixed-roster-matrix.json`](../../../.build/runs/m2-6-fixed-roster-20261113/fixed-roster-matrix.json)。

固定边界仍是游戏 `0.111.0 / 41cef1ea` 和快照 `workshop-snapshot-20261106-163352`。这份矩阵只汇总既有四路 native 证据、内容矩阵和明确保留的 `Partial`/`Blocked`/`Unsupported`/`Unrun` 状态；没有把静态 worker 成功、预测终态或平台能力缺口升级为 gameplay gate 通过。

| Mod | Foundation | Flow | Content | Stability | Platform | Mod 状态 | 证据 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Watcher | PARTIAL | PASS | PARTIAL | PASS | PARTIAL | PARTIAL | [报告](m2-3-watcher-full-run-report-20261110.md) |
| Downfall | BLOCKED | BLOCKED | PARTIAL | BLOCKED | BLOCKED | BLOCKED | [阻塞解析](m2-6-downfall-blocker-resolution-20261113.md) |
| figure_Saya | PARTIAL | PASS | PASS | PASS | PARTIAL | PARTIAL | [报告](m2-3-figure-saya-full-run-report-20261110.md) |
| Remilia | PASS | PASS | PASS | PASS | PARTIAL | PASS | [报告](m2-4-remilia-full-run-report-20261111.md) |
| Ryoshu | PARTIAL | BLOCKED | BLOCKED | PARTIAL | BLOCKED | BLOCKED | [报告](m2-3-ryoshu-full-run-report-20261110.md) |
| LexNinja2 | PASS | PASS | PASS | PASS | PARTIAL | PASS | [报告](m2-4-lexninja2-full-run-report-20261111.md) |
| Kaguya | PASS | PASS | PARTIAL | PASS | PARTIAL | PARTIAL | [报告](m2-5-kaguya-full-run-report-20261112.md) |
| Acheron | PASS | BLOCKED | PARTIAL | BLOCKED | PARTIAL | BLOCKED | [矩阵交接](m2-6-acheron-matrix-handoff-20261113.md) |
| shadowversebydmod | PASS | PASS | PARTIAL | PASS | PARTIAL | PARTIAL | [交接](m2-5-shadowverse-minion-handoff-20261113.md) |
| Aemeath | PARTIAL | PASS | PASS | PASS | PARTIAL | PARTIAL | [报告](m2-3-aemeath-full-run-report-20261110.md) |
| St2bs | BLOCKED | BLOCKED | BLOCKED | BLOCKED | BLOCKED | BLOCKED | [诊断报告](m2-6-st2bs-foundation-20261113.md) |
| YukiMakoto | PASS | PASS | PARTIAL | PASS | PARTIAL | PARTIAL | [验收](m2-2-yuki-ticket-4-acceptance-20261110.md) |

## 阻塞与边界

- `Downfall`、`Ryoshu`、`Acheron` 和 `St2bs` 仍有真实运行阻塞，不能关闭为通过。
- `Watcher`、`figure_Saya`、`Aemeath`、`Kaguya`、`shadowversebydmod` 和 `YukiMakoto` 保留各自的内容或共享 foundation 缺口；这些缺口没有从分母中删除。
- `Remilia` 和 `LexNinja2` 的固定输入下双门证据已通过，但不改变整个固定分母的总体状态。
- M2-7 的 Workshop 样本不属于这份固定分母。由于测试版本已经从 `0.111.0` 迁移到 `0.113.0`，本轮不再启动新的 Workshop 样本选择或样本验收；对应 Issue 以 `wontfix` 关闭，并在 M2-8 中记录为范围取消，而不是通过。

## 验证

运行 `pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestM26FixedRosterMatrix.ps1`，应检查固定 12 条目、快照身份、证据文件存在和状态聚合规则。核心契约检查继续运行 `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`。
