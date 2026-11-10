# M2-2 YukiMakoto foundation 修复交接（票据 #4）

这份交接文档用于新对话继续修复共享 foundation。当前工作停止在审计阻塞点；下一 Agent 的第一工作单元是跨 Mod 可复用的 foundation fixture，完成前不得进入 Yuki 内容族。

## 当前状态

阶段聚合为 **BLOCKED**。

- #4：`OPEN`、`ready-for-agent`；#3：`CLOSED`。#4 保持 `OPEN`，不要运行 `/triage`、创建重复票据、评论关闭票据或开始 M2-3。
- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 工作区已有 dirty 修改；新对话必须先读取并保护，不得 `reset`、`clean`、`checkout`、覆盖式复制或删除。
- 最新审计报告：[m2-2-yuki-foundation-blocked-handoff-20261108-r2.md](./m2-2-yuki-foundation-blocked-handoff-20261108-r2.md)。
- 最新审计证据目录：`.build/runs/m2-2-yuki-foundation-audit-20261108-r2/`。

## 已接受输入和既有证据

- 游戏：`0.111.0` / commit `41cef1ea`。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`；内容 SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`；manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- YukiMakoto：`v0.0.7-hot`、Workshop ID `3829924471`、精确 `lib/0.111.0/YukiMakoto.dll`，SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- RitsuLib：精确 `compat/0.111.0`；Runtime SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`；loader SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- 依赖闭包：`STS2-RitsuLib compat/0.111.0 -> YukiMakoto lib/0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_01`。
- 已接受 Yuki primary/repeat/replay：真实 `Death / Completed`，state/RNG/trace/route digest 一致；failure 为 `Unknown / BudgetExceeded`，不得提升为 Death。
- Yuki runtime inventory：`586`；`Covered=0`、`Partial=8`、`Unrun=578`、content `RuntimeFailure=0`。
- registration identity：`REG.YUKI.lib/0.111.0#1@0.111.0`。

既有 flow/content 证据只作输入，不能重跑、替换或升级：

- `.build/runs/m2-2-yuki-20261108/contract-summary.json`
- `.build/runs/m2-2-yuki-20261108/runs.json`
- `.build/runs/m2-2-yuki-20261108/content-matrix.json`
- `.build/runs/m2-2-yuki-20261108/lifecycle-matrix.json`
- `.build/runs/m2-2-yuki-20261108/interaction-risk.json`

## 阻塞原因

四类共享语义仍没有跨 Mod/内容族可复用的真实闭环：

1. 注册：重复注册、reload、注册失败回滚、运行时注册表 teardown。
2. callback/hook：turn/combat/reward/before-death 顺序、异常中断、ownership、detach。
3. 跨房间：奖励后 secondary resource、Power/Relic 状态、registration identity、callback ownership。
4. 清理：共享 hook/state 清理、callback detach、注册表卸载、运行时 ALC teardown、failure cleanup。

LexNinja2 Stage 7、Minion/Shadowverse 窄片和 Yuki flow 只能证明局部执行边界。静态 worker 的 collectible ALC unload 不等于运行时 Mod teardown；Yuki 的战斗 cleanup 不等于共享 foundation cleanup。当前 foundation gate 对依赖 Mod 的完整接受为 `BLOCKED`。

## 下一工作单元：共享 foundation 修复

先在最高稳定 seam 建立一个不含 Yuki 名称、Workshop ID 或单包分支的 generic foundation fixture/worker。fixture 必须在独立 native worker/game process 中加载真实兼容变体，并用至少两个可复用的 Mod/内容族边界证明共享语义；若现有证据不足以选择第二个真实内容族，应保留 `BLOCKED` 并说明缺口。

修复与证据必须覆盖：

| 语义 | 通过条件 |
|---|---|
| 注册 | 记录真实 registration identity；重复注册按声明策略稳定处理；reload 保持身份；失败注册回滚，不污染注册表；正常 teardown 卸载注册身份 |
| callback/hook | 真实记录 callback ownership、顺序、异常中断和 before-death；detach 后不再收到回调；不能跳过、注入或预测结果 |
| 跨房间 | 真实奖励→下一房间路径验证 secondary resource、Power/Relic 状态、registration identity 和 callback ownership 的连续性、丢失与重复注册边界 |
| 清理 | 正常终态和 failure path 都验证 hook/state 清理、callback detach、注册表卸载、运行时 ALC teardown、无残留和可重复新进程启动 |

所有新运行使用新的 `.build/runs/m2-2-yuki-foundation-repair-<date>/` 目录，写入 canonical input、worker、配置、adapter、collector、依赖摘要、registration identity、lifecycle matrix、interaction risk 和清理结果。generic interface 只能表达跨 Mod/内容族语义；不能出现 Yuki 名称、Workshop ID 或单包分支。

## 修复后的门槛

- foundation 四项全部有真实跨 Mod/内容族证据，`foundationGate=PASS`，才允许回到 Yuki。
- foundation 未全闭合：只写新的审计证据目录和交接报告，状态为 `BLOCKED`，不启动 Yuki 内容 probe。
- foundation 闭合后：选择一个最小可复核 Yuki 内容族，记录真实 registration identity、lifecycle matrix、interaction risk；可达内容用合法真实 run，不可达内容才用真实注册/回调/效果/cleanup controlled probe。
- 未知语义保留 `Partial`、`Unsupported`、`Unrun` 或 `RuntimeFailure`；不空实现、不静默跳过、不注入结果、不替换原版角色。
- content matrix 只能 append-only；现有 586 个身份不得删除、折叠或重写。
- TestMode/Oracle/internal state 只证明 execution compatibility；visible-policy、UI、save、map、network、Steam/cloud 单独记录为 platform capability。

## 交接完成标准

新对话结束时必须写新的阶段交接报告，说明：阶段状态、分支/HEAD/git status、实际修改和未修改输入、命令与退出码、新证据目录、foundation/flow/content/stability、registration identity、content unit/lifecycle/interaction 矩阵、平台能力、primary/repeat/replay/failure 终态与 digest、Partial/Unsupported/Unrun/RuntimeFailure 分母、未覆盖语义和下一唯一入口。

默认不提交、不推送、不关闭 #4。报告完成后停止。
