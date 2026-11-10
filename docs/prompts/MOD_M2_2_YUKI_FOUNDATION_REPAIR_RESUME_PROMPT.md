# M2-2 YukiMakoto foundation 修复启动提示词

将下面整段粘贴到新对话：

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

继续 M2-2 的 YukiMakoto foundation/content 缺口，只处理票据 #4。你的当前工作单元是修复共享 foundation；先不要进入 Yuki 内容 probe。先读取并遵守：
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
- docs/mods/stage-reports/m2-2-yuki-foundation-blocked-handoff-20261108-r2.md
- docs/mods/stage-reports/m2-2-yuki-foundation-repair-handoff-20261108.md
- docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md

开始前：
1. 执行 `. .\tools\maintenance\Set-GitHubSession.ps1`。用 `gh issue view 4 --json number,title,body,labels,state,assignees` 确认 #4 仍为 `OPEN`、标签为 `ready-for-agent`；另用 `gh issue view 3 --json number,title,labels,state` 确认 #3 已 `CLOSED`。不要运行 /triage、不要创建重复票据、不要关闭 #4。
2. 读取当前分支、HEAD、git status，保护已有 dirty 修改。禁止 reset、clean、checkout、覆盖式复制和删除。
3. 确认固定快照、manifest、游戏 `0.111.0` / commit `41cef1ea`、RitsuLib `compat/0.111.0` 和 Yuki `lib/0.111.0` exact variant 存在且 SHA-256 与 manifest/既有报告匹配。
4. 读取 `.build/runs/m2-2-yuki-20261108/` 的五个既有 JSON，以及 `.build/runs/m2-2-yuki-foundation-audit-20261108-r2/` 的 `canonical-input.json`、`foundation-audit.json`、`evidence-index.json`。既有 primary/repeat/replay/failure 不重跑、不覆盖、不重分类。

已接受事实：
- YukiMakoto `v0.0.7-hot`，Workshop ID `3829924471`，精确 `lib/0.111.0`。
- 依赖闭包：`STS2-RitsuLib compat/0.111.0 -> YukiMakoto lib/0.111.0`。
- registration identity：`REG.YUKI.lib/0.111.0#1@0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_01`。
- primary/repeat/replay：真实 `Death / Completed`，state/RNG/trace/route digest 一致。
- failure：`Unknown / BudgetExceeded`，不能提升为 Death。
- runtime inventory：586 个 content unit；`Covered=0`、`Partial=8`、`Unrun=578`、content `RuntimeFailure=0`。
- foundation gate 对依赖 Mod 的完整接受当前为 `BLOCKED`。

第一阶段：只修复共享 foundation
1. 先检查现有 native worker、GameProbe、ModWorker、run manifest 和最高稳定 seam，确定 generic fixture 的放置点；核心模拟器继续不引用 Mod、Godot 或外部 DTO。
2. 建立不含 Yuki 名称、Workshop ID 或单包分支的 generic foundation fixture/worker。fixture 必须能在独立 native worker/game process 中加载真实 exact compatibility variant，并在至少两个可复用的 Mod/内容族边界上产生真实生命周期证据。若没有安全的第二个真实内容族，保留 BLOCKED 并写清原因。
3. 覆盖共享注册：真实 registration identity、重复注册策略、reload、注册失败回滚、正常注册表 teardown。
4. 覆盖 callback/hook：真实 ownership、turn/combat/reward/before-death 顺序、异常中断、callback detach；不得跳过回调、注入结果、预测死亡或使用空实现。
5. 覆盖奖励后跨房间：secondary resource、Power/Relic 状态、registration identity、callback ownership 的连续性、丢失和重复注册边界。
6. 覆盖清理：正常和 failure path 的 hook/state 清理、callback detach、注册表卸载、运行时 ALC teardown、残留检查和新进程可重复启动。
7. 所有新运行使用新的 `.build/runs/m2-2-yuki-foundation-repair-<date>/` 目录，写 canonical input：seed、快照摘要、游戏/Mod/依赖版本、worker、配置、adapter、collector、registration identity、lifecycle matrix、interaction risk、清理结果和证据目录。
8. generic interface 只能表达跨 Mod/内容族语义；不得添加 Yuki 专用 adapter，不修改 Mod/快照/DLL/PCK/二进制，不改变核心外部类型边界。

阻塞规则：
- 四类共享语义中任一项没有跨 Mod/内容族可复用的真实证据，立即停止在 `BLOCKED`；只写新的 audit evidence 目录和阶段交接报告，不启动 Yuki 内容 probe。
- 静态加载、assembly metadata、单 Mod trace、单场战斗、collectible ALC 的静态 unload 都不能单独闭合 foundation。
- TestMode/Oracle/internal state 只证明 execution compatibility；visible-policy 另行记录。UI、正式 save、map presentation、network、Steam/cloud 是 platform capability。

第二阶段：仅在 foundation 全部闭合后
- 用新的 `.build/runs` 目录选择一个最小可复核 Yuki 内容族。
- 记录真实 registration identity、lifecycle matrix、interaction risk。
- 可达内容用真实合法 run；不可达内容才用真实注册/回调/效果/cleanup 的 controlled probe。
- 未知语义保留 `Partial`、`Unsupported`、`Unrun` 或 `RuntimeFailure`；content matrix append-only，既有 586 个身份不得删除或折叠。
- 不接入第二个角色，不开始 M2-3。

完成后写新的阶段交接报告，至少包含：
- PASS、PARTIAL 或 BLOCKED；
- 分支、HEAD、git status、实际修改和未修改输入；
- 执行命令、退出码、新证据目录；
- foundation/flow/content/stability 状态；
- generic fixture 的 registration identity、content unit、lifecycle matrix、interaction risk table、平台能力缺口；
- primary/repeat/replay/failure 终态、digest、错误分类；
- Partial/Unsupported/Unrun/RuntimeFailure 分母和未覆盖语义；
- 下一唯一入口。

默认不提交、不推送、不关闭 #4。报告完成后停止。
```
