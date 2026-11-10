# M2-2 YukiMakoto foundation/content 续作启动提示词

将下面整段粘贴到新对话：

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

继续 M2-2 的 YukiMakoto foundation/content 缺口，只处理票据 #4。先读取并遵守：
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
- docs/mods/stage-reports/m2-2-yuki-foundation-content-blocked-20261108.md
- docs/mods/stage-reports/m2-2-yuki-foundation-blocked-handoff-20261108.md
- docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md

开始前：
1. 执行 `. .\tools\maintenance\Set-GitHubSession.ps1`，用 `gh issue view 4 --json number,title,body,labels,state,assignees` 确认 #4 仍为 `OPEN`、标签 `ready-for-agent`、阻塞 #3 已关闭。不要运行 /triage，不要创建重复票据；#4 保持 OPEN。
2. 读取当前分支、HEAD、git status，保护已有 dirty 修改。禁止 reset、clean、checkout、覆盖式复制和删除。
3. 确认固定快照、manifest、游戏 `0.111.0` / commit `41cef1ea`、RitsuLib `compat/0.111.0` 和 Yuki `lib/0.111.0` 精确变体存在且摘要匹配。
4. 读取 `.build/runs/m2-2-yuki-20261108/` 下的 `contract-summary.json`、`runs.json`、`content-matrix.json`、`lifecycle-matrix.json`、`interaction-risk.json`，以及 `.build/runs/m2-2-yuki-foundation-audit-20261108/` 下的审计 JSON。

已接受事实，不能重跑、替换或升级：
- YukiMakoto `v0.0.7-hot`，Workshop ID `3829924471`，精确 `lib/0.111.0`。
- 依赖闭包：`STS2-RitsuLib compat/0.111.0 -> YukiMakoto lib/0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_01`。
- primary/repeat/replay：真实 `Death / Completed`，state/RNG/trace/route digest 一致。
- failure：`Unknown / BudgetExceeded`，不能提升为 Death。
- runtime inventory：586 个 content unit；`Covered=0`、`Partial=8`、`Unrun=578`、content `RuntimeFailure=0`。
- 现阶段 foundation gate 对依赖 Mod 的完整接受为 `BLOCKED`。

必须先做共享 foundation 审计：
1. 检查共享注册（重复注册、reload、注册失败、teardown）。
2. 检查 callback/hook 顺序、异常中断、before-death 和解绑。
3. 检查奖励后跨房间的 secondary resource、Power/Relic 状态、注册身份和 callback ownership。
4. 检查共享 hook/state 清理、callback detach、注册表卸载、ALC teardown 和 failure cleanup。

如果任一项没有跨 Mod/内容族可复用的真实证据，立即报告 `BLOCKED`，只写新的审计证据目录和交接报告；不要启动 Yuki 内容 probe，不要用 Yuki 专用 adapter，不要修改 Mod/快照/二进制/核心外部类型边界。

只有 foundation 全部闭合，才可以：
- 用新的 `.build/runs` 目录；
- 选择一个最小可复核的 Yuki 内容族；
- 记录真实 registration identity、lifecycle matrix、interaction risk；
- 可达内容用真实合法 run，不可达内容用真实注册/回调/效果/cleanup 的 controlled probe；
- 未知语义保留 `Partial`、`Unsupported`、`Unrun` 或 `RuntimeFailure`；不得空实现、静默跳过、注入结果、预测死亡或替换原版角色；
- append-only 更新 content matrix，不删除或折叠既有 586 个身份。

共同边界：
- Mod DLL 只在独立 native worker/game process 加载；核心模拟器不得引用 Mod、Godot 或外部 DTO。
- 只允许跨 Mod/内容族可复用的 generic interface；禁止 Yuki 名称、Workshop ID 或单包分支 adapter。
- TestMode/Oracle/internal state 只能证明 execution compatibility，不能证明 visible-policy compatibility。
- UI、正式 save、map presentation、network、Steam/cloud 单独记录为 platform capability。
- 所有新运行写 canonical input：seed、快照摘要、游戏/Mod/依赖版本、worker、配置、adapter、collector、证据目录。
- 默认不提交、不推送、不重写历史；不要开始 M2-3，不要接入第二个角色。

完成后写新的阶段交接报告，至少包含：
- PASS、PARTIAL 或 BLOCKED；
- 分支、HEAD、git status、实际修改和未修改输入；
- 执行命令、退出码、新证据目录；
- foundation/flow/content/stability 状态；
- content unit、registration identity、lifecycle matrix、interaction risk table、平台能力缺口；
- primary/repeat/replay/failure 终态、digest、错误分类；
- Partial/Unsupported/Unrun/RuntimeFailure 分母和未覆盖语义；
- 下一唯一入口。

报告完成后停止。
```
