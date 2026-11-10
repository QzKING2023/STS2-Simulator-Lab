# M2-2 Yuki foundation 修复交接 — 2026-11-09

## 接手结论

当前阶段仍为 **BLOCKED**。下一 Agent 只能继续修复共享 foundation，不能进入 Yuki 内容 probe，不能接入第二角色，不能开始 M2-3。

最新阶段报告：

- [m2-2-yuki-foundation-repair-20261109.md](/D:/STS2-Simulator-Lab/docs/mods/stage-reports/m2-2-yuki-foundation-repair-20261109.md)

最新 aggregate evidence：

- [.build/runs/m2-2-yuki-foundation-repair-20261109/canonical-input.json](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109/canonical-input.json)
- [.build/runs/m2-2-yuki-foundation-repair-20261109/foundation-audit.json](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109/foundation-audit.json)
- [.build/runs/m2-2-yuki-foundation-repair-20261109/evidence-index.json](/D:/STS2-Simulator-Lab/.build/runs/m2-2-yuki-foundation-repair-20261109/evidence-index.json)

## 当前仓库与票据状态

- 工作目录：`D:\STS2-Simulator-Lab`
- 分支：`main`
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`
- #4：`OPEN`，标签 `ready-for-agent`
- #3：`CLOSED`
- 不要运行 `/triage`，不要创建重复票据，不要关闭 #4。
- 当前工作区有已有 dirty 修改，必须保护。禁止 `reset`、`clean`、`checkout`、覆盖式复制和删除。

## 固定输入

- 游戏：`v0.111.0` / commit `41cef1ea`
- snapshot：`workshop-snapshot-20261106-163352`
- snapshot content SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- snapshot manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- RitsuLib exact `compat/0.111.0`
  - Runtime：`3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`
  - loader：`6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`
- Yuki exact `lib/0.111.0)：`442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`
- Yuki identity：`REG.YUKI.lib/0.111.0#1@0.111.0`
- canonical seed：`LAB_M2_2_YUKI_01`
- 既有 Yuki runtime inventory：586 units，`Covered=0`、`Partial=8`、`Unrun=578`、`Unsupported=0`、`RuntimeFailure=0`

既有 Yuki primary/repeat/replay/failure evidence 是冻结输入，禁止重跑、覆盖或重分类：

- primary/repeat/replay：真实 `Death / Completed`，state/RNG/trace/route digest 一致
- failure：`Unknown / BudgetExceeded`，不能提升为 Death

## 已完成的共享 fixture

generic worker 为 `SimulatorLab.NativeFoundationWorker`，adapter 为 `native-foundation/v1`，collector 为 `isolated NativeRun/GameProbe; foundation-trace/v1`。

实现入口：

- [NativeFoundationModLoop.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeFoundationModLoop.cs)
- [AssemblyResolver.cs](/D:/STS2-Simulator-Lab/tools/native-run/AssemblyResolver.cs)
- [NativeLifecycle.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeLifecycle.cs)
- [NativePerformancePilot.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativePerformancePilot.cs)
- [NativeScenario.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeScenario.cs)
- [NativeSolverProfile.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeSolverProfile.cs)
- [Program.cs](/D:/STS2-Simulator-Lab/tools/native-run/Program.cs)

fixture 不含 Yuki 名称、Workshop ID 或 Yuki 专用 adapter；没有修改 Mod、snapshot、DLL、PCK、游戏二进制或核心外部类型边界。

最新真实运行：

1. Acheron：`.build/runs/m2-2-yuki-foundation-repair-20261109-r52-primary2/`
   - registration：`REG.Acheron.lib/0.111.0#1@0.111.0`
   - units：Character 1 / Card 98 / Relic 14 / Power 35 / Other 4
   - 29 actions confirmed，真实 reward→next-room boundary
   - 28 callback events，patch ownership 91 rows
   - `observerDetached=true`、`registryCleared=true`
   - `runtimeAlcTeardown=false`、`alcAlive=true`、`assemblyAlive=true`
   - post-cleanup 仍有 Ritsu `VariableMethod/VariableProperty`、Acheron `SpireField` 与 generated static roots

2. Remilia：`.build/runs/m2-2-yuki-foundation-repair-20261109-r52-remilia2/`
   - registration：`REG.Remilia.lib/0.111.0#1@0.111.0`
   - units：Character 1 / Card 88 / Relic 8 / Power 24 / Other 3
   - 同一 generic worker 加载 RitsuLib dependency closure
   - 26 callback events
   - 在真实 reward pool 处记录 EngineError，随后 raw process exit `-1073741819`
   - 没有最终 failure cleanup 证明

## 四类 foundation 的当前状态

| 语义 | 状态 | 接手时必须知道的缺口 |
|---|---|---|
| registration identity / duplicate / reload / failure rollback / teardown | PARTIAL | duplicate rollback 已观察；reload/failure 有旧 generic evidence；运行时 registry unload 未闭合 |
| callback/hook order / exception / before-death / ownership / detach | PARTIAL | Acheron 正常 detach 已观察；Remilia failure detach 未证明 |
| reward 后跨房间 secondary resource / Power / Relic / identity / ownership | PARTIAL | Acheron 到达边界，但跨族连续性未闭合；Remilia reward pool failure |
| normal/failure cleanup / registry unload / runtime ALC teardown | BLOCKED | 正常 Acheron ALC 仍存活；Remilia failure path 崩溃，cleanup 未证明 |

静态加载、assembly metadata、单 Mod trace、单场战斗、load-only collectible ALC unload 都不能替代正常运行时 teardown。当前唯一硬阻塞是：必须找到安全、generic、跨族可复核的 runtime Mod teardown 机制，证明正常和 failure path 都能清理静态根、hook/state、callback、registry，并让 collectible ALC 真正卸载。

## 接手后允许做的事

1. 先读取本交接、最新 repair report、aggregate evidence，以及用户原始要求列出的全部 AGENTS/domain/ADR/M2 文档。
2. 重新 dot-source GitHub session，确认 #4/#3 状态、branch/HEAD/status、固定输入 hash。
3. 只在新的 `.build/runs/m2-2-yuki-foundation-repair-<date>/` 子目录生成 evidence；不得覆盖旧目录。
4. 先检查 Acheron post-cleanup roots、RitsuLib reflection caches、Harmony/runtime caches、RunManager/ActionExecutor/CombatManager 的 pending task/state，以及 failure-path cleanup；所有修复必须保持 generic。
5. 用至少两个独立内容族重新验证 foundation。若任一共享语义没有真实跨族证据，立即写新的 audit evidence 和阶段报告并保持 BLOCKED。
6. 只有四类 foundation 全部 PASS，才允许启动最小 Yuki 内容族 probe。

## 交接结束条件

新 Agent 必须写新的阶段报告，包含 PASS/PARTIAL/BLOCKED、branch/HEAD/status、实际修改和未修改输入、命令与退出码、新证据目录、foundation/flow/content/stability、registration identity、content units、lifecycle matrix、interaction risk、平台能力缺口、primary/repeat/replay/failure 终态、分母和下一唯一入口。

默认不提交、不推送、不关闭 #4。

