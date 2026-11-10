# M2-2 YukiMakoto Card effect boundary 续作启动提示词

将下面整段粘贴到新对话，继续当前 M2-2 唯一入口。不要重新规划 M2、不要接入第二个角色、不要开始 M2-3。

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

继续 M2-2 的 YukiMakoto 续作，只处理票据 #4 当前 foundation/content 缺口。先读取并遵守：
- AGENTS.md
- GLOSSARY.md
- docs/agents/domain.md
- docs/agents/issue-tracker.md
- docs/agents/triage-labels.md
- docs/adr/README.md 及涉及 M2 的 ADR
- docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md
- docs/mods/stage-reports/m2-1-full-run-coordinator-report.md
- docs/mods/stage-reports/m2-2-yuki-role-slice-report.md
- docs/mods/stage-reports/m2-2-yuki-role-slice-handoff.md
- docs/mods/stage-reports/m2-2-yuki-card-slice-report-20261110.md
- docs/mods/stage-reports/m2-2-yuki-card-effect-boundary-report-20261110.md
- docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md

开始前：
1. 执行 `. .\tools\maintenance\Set-GitHubSession.ps1`，用 `gh issue view 4 --json number,title,body,labels,state,assignees` 确认 #4 仍为 `OPEN`、标签为 `ready-for-agent`；另确认 #3 已 `CLOSED`。不要运行 `/triage`、不要创建重复票据、不要关闭 #4。
2. 读取当前 branch、HEAD、git status；保护已有 dirty 修改。禁止 reset、clean、checkout、覆盖式复制和删除。
3. 确认固定 snapshot `D:\STS2-Simulator-Lab\local-mods\workshop-snapshot-20261106-163352`、manifest、游戏 `0.111.0` / commit `41cef1ea`、RitsuLib `compat/0.111.0`、Yuki `lib/0.111.0` exact variant 存在且 SHA-256 与报告匹配。
4. 读取既有证据索引：
   - `.build/runs/m2-2-yuki-20261108/`
   - `.build/runs/m2-2-yuki-content-card-20261110/`
   - `.build/runs/m2-2-yuki-content-card-effect-20261110/`
   - `.build/runs/m2-2-yuki-content-card-effect-20261110-card-effect-final/`
   不得覆盖或重分类既有 primary/repeat/replay/failure。

已接受事实：
- branch `main`，HEAD `7718d76399da8b515794fe5d68c2bfdaba59ebbd`；工作区已有 dirty 修改，必须保留。
- #4 为 `OPEN/ready-for-agent`，#3 为 `CLOSED`。
- 游戏 `v0.111.0` / commit `41cef1ea`。
- snapshot content SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`，manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- Yuki `v0.0.7-hot` / Workshop `3829924471` / exact `lib/0.111.0`，SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- RitsuLib exact `compat/0.111.0`，runtime SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，loader SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- registration identity `REG.YUKI.lib/0.111.0#1@0.111.0`。
- 已接受 primary/repeat/replay 为真实 `Death / Completed` 且 state/trace/route 摘要一致；failure 为 `Unknown / BudgetExceeded`，不能改写为 Death。
- runtime inventory 仍为 586 个身份：`Covered=0`、`Partial=9`、`Unrun=577`、`Unsupported=0`、`RuntimeFailure=0`。
- foundation gate 已由 generic Acheron/Remilia r260 repair evidence 证明为 `PASS`；不要回退到 foundation repair，也不要使用 Yuki 专用 adapter。
- `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM` 已真实从 CardReward 领取并写入永久牌组。
- 最新 effect probe 真实观察到：下一场战斗的 Hand、Draw、Discard、Exhaust、Play 均没有 `SeizeMomentum`；probe 以 `Unknown / RuntimeFailure`、`TimeoutException` 停止。该 process-level failure 不能把 content row 改成 RuntimeFailure，也不能把卡手动塞入 combat pile。

本续作唯一入口：
1. 先审计并证明“永久牌组 → 下一场 combat pile”的真实原生传播语义；保留当前 RuntimeFailure 证据。不得调用 `CardPileCmd.Add` 手动注入 `SeizeMomentum`，不得修改 Mod DLL/PCK/资源、snapshot、游戏二进制或原有 Mod 代码。
2. 只能在真实合法 run 中观察可达内容。只有真实卡实例进入合法 Hand/Draw 并满足原生 `IsPlayable` 后，才可尝试 OneMore prerequisite、合法 play、native draw/energy effect、target/effect-generated state 和 card-specific cleanup。
3. 任何未知语义停在 `Unrun`、`Unsupported` 或 `RuntimeFailure`；不空实现、不静默跳过、不注入结果、不预测死亡、不替换原版角色。
4. 每次新运行使用新的 `.build/runs` 目录，并写 canonical input：seed、snapshot 摘要、游戏/Mod/依赖版本、worker、配置、adapter、collector 和 evidence 目录。
5. 继续维护 append-only content matrix；不得删除或折叠 586 个身份。只有适用 lifecycle 与关键 interaction contract 全部真实闭合，才可把该单元从 `Partial` 提升为 `Covered`。
6. 完成后写新的阶段交接报告，包含状态、branch/HEAD/status、实际修改与未修改输入、命令/退出码/证据目录、foundation/flow/content/stability、content unit、registration identity、lifecycle、interaction risk、平台能力、四路终态/digest/error、Partial/Unsupported/Unrun/RuntimeFailure 分母和下一唯一入口。

共同边界：
- 核心模拟器不得引用 Mod、Godot 或外部 DTO；Mod DLL 只能在独立 native worker/game process 加载。
- 只允许跨 Mod/内容族可复用的 generic interface；不要把 Yuki 名称、Workshop ID 或单包分支放进共享 adapter。
- TestMode/Oracle/internal state 只证明 execution compatibility，不能证明 visible-policy compatibility。
- UI、正式 save/load、map presentation、network、Steam/cloud 单独记录为 platform capability。
- 默认不提交、不推送、不关闭 #4。

完成交接报告后停止；不要开始 M2-3。
```
