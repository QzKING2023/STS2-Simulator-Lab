# M2-2 YukiMakoto Covered slice 续作启动提示词

把下面整段粘贴到新对话，继续当前 M2-2。`SeizeMomentum` 的完整 execution lifecycle 已闭合；不要重复回退 blocker，也不要开始 M2-3。

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

继续 M2-2 的 YukiMakoto 续作，只处理票据 #4 的剩余 foundation/content 缺口。先读取并遵守：
- AGENTS.md
- GLOSSARY.md
- docs/agents/domain.md
- docs/agents/issue-tracker.md
- docs/agents/triage-labels.md
- docs/adr/README.md 及涉及 M2 的 ADR
- docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md
- docs/mods/stage-reports/m2-1-full-run-coordinator-report.md
- docs/mods/stage-reports/m2-2-yuki-card-covered-report-20261110.md
- docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md

开始前：
1. `. .\tools\maintenance\Set-GitHubSession.ps1` 后确认 #4 仍为 `OPEN/ready-for-agent`，#3 已 `CLOSED`；不要 `/triage`、不要创建重复票据、不要关闭 #4。
2. 读取 branch、HEAD、git status；保护所有已有 dirty 修改。禁止 reset、clean、checkout、覆盖式复制和删除。
3. 确认固定 snapshot、manifest、游戏 `0.111.0` / commit `41cef1ea`、RitsuLib `compat/0.111.0`、Yuki `lib/0.111.0` exact variant 与 SHA-256 匹配。
4. 读取既有证据：
   - `.build/runs/m2-2-yuki-20261108/`
   - `.build/runs/m2-2-yuki-content-card-20261110/`
   - `.build/runs/m2-2-yuki-content-card-effect-20261110/`
   - `.build/runs/m2-2-yuki-content-card-covered-20261110/`
   既有 primary/repeat/replay/failure 不得覆盖或重分类。

已接受事实：
- branch `main`，HEAD `7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- #4 `OPEN/ready-for-agent`；#3 `CLOSED`。
- registration identity `REG.YUKI.lib/0.111.0#1@0.111.0`。
- foundation gate `PASS`，不要重新做 foundation repair。
- `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM` 已 Covered：真实 registration、CardReward、永久牌组、第二 `SetUpCombat`、Draw→Hand、真实 OneMore、合法 play、native draw 1、energy +3、Exhaust、Death cleanup 均有证据。
- 最新内容矩阵 586 identities：`Covered=1`、`Partial=8`、`Unrun=577`、`Unsupported=0`、`RuntimeFailure=0`。
- accepted card primary/repeat/replay 为 `Death / Completed`，state/trace/route digest 一致；card failure 为 `Unknown / BudgetExceeded`。

本续作要求：
1. 只选择剩余 inventory 中一个最小、可复核的 Yuki 内容族切片；不要回退或重跑 `SeizeMomentum`。
2. 可达内容必须真实合法运行；不可达内容只能用真实注册、回调、效果和 cleanup 的 controlled probe。
3. 未知语义停在 `Partial`、`Unrun`、`Unsupported` 或 `RuntimeFailure`；不得空实现、静默跳过、注入结果、预测死亡、替换原版角色。
4. 所有新运行使用新的 `.build/runs` 目录和 canonical input；append-only 更新 586-unit matrix。
5. 共享接口必须保持跨 Mod/内容族 generic；不要把 Yuki 名称、Workshop ID 或单包分支放入共享 adapter。
6. 继续记录 UI、正式 save/load、map presentation、network、Steam/cloud 为独立 platform capability。
7. 完成后写新的阶段交接报告，包含状态、branch/HEAD/status、命令/退出码、证据目录、foundation/flow/content/stability、content unit、registration、lifecycle、interaction risk、平台缺口、四路终态/digest/error、分母和下一唯一入口。

默认不提交、不推送、不关闭 #4；不要开始 M2-3、不要接入第二个角色。完成报告后停止。
```
