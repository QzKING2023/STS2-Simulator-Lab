# M2-2 YukiMakoto 续作启动提示词

把下面整段粘贴到新会话。它用于继续当前 M2-2，不是重新规划 M2，也不是选择第二个角色。

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

执行 M2-2 的 YukiMakoto 续作，只处理当前票据 #4 的 foundation/content 缺口。先读取并遵守：
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
- docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md

开始前：
1. 执行 `. .\tools\maintenance\Set-GitHubSession.ps1` 后，用 `gh issue view 4 --json number,title,body,labels,state` 确认 #4 仍为 open、标签为 ready-for-agent，并确认 Blocked by 已关闭。不要运行 /triage，不要创建重复票据。
2. 读取当前分支、HEAD、git status；保护已有 dirty 修改。禁止 reset、clean、checkout、覆盖式复制和删除。
3. 确认固定快照 `D:\STS2-Simulator-Lab\local-mods\workshop-snapshot-20261106-163352`、manifest、游戏 `0.111.0` 和精确变体存在。
4. 读取既有证据索引：`.build/runs/m2-2-yuki-20261108/contract-summary.json`、`runs.json`、`content-matrix.json`、`lifecycle-matrix.json`、`interaction-risk.json`。

已接受事实：
- YukiMakoto `v0.0.7-hot`，Workshop ID `3829924471`，精确 `lib/0.111.0`。
- 依赖闭包 `STS2-RitsuLib compat/0.111.0 -> YukiMakoto lib/0.111.0`。
- canonical seed `LAB_M2_2_YUKI_01`。
- primary/repeat/replay 已真实到达 `Death / Completed`，并且 state/RNG/trace/route digest 一致；不要把这些 flow 证据改写或替换。
- failure 已明确为 `BudgetExceeded`，不能提升为 Death。
- runtime inventory 是 586 个 content unit：`Covered=0`、`Partial=8`、`Unrun=578`。
- foundation gate 仍为 `PARTIAL`。

本续作必须完成：
1. 先审计共享 RitsuLib foundation 是否足以安全推进。缺少共享注册、callback、跨房间或 cleanup 语义时，报告 BLOCKED，保留原始证据，不用 Yuki 专用 adapter 绕过。
2. 若 foundation 可推进，选择一个最小且可复核的 Yuki 内容族切片，补充真实 registration identity、lifecycle matrix、interaction risk evidence，并使用新的 `.build/runs` 目录。
3. 可达内容必须在真实合法 run 中观察；不可达内容只能用真实注册、真实回调、真实效果和真实 cleanup 的 controlled probe。
4. 任何未知语义必须停在 Unsupported、Unrun 或 RuntimeFailure；不得空实现、静默跳过、注入结果、预测死亡或替换原版角色。
5. 继续更新 append-only content matrix；不得删除或折叠既有 586 个身份。
6. 只有当所有适用生命周期和关键 interaction contract 真正闭合，才能把对应单元改为 Covered。否则保持 Partial/Unrun，并保持 #4 OPEN。

共同边界：
- 不修改固定快照、Mod DLL/PCK/资源、Mod 原有代码、二进制或核心外部类型边界。
- Mod DLL 只能在独立 native worker/game process 加载；核心模拟器不得引用 Mod、Godot 或外部 DTO。
- 只允许跨 Mod/内容族可复用的 generic interface；禁止包含 Yuki 名称、Workshop ID 或单包分支的 adapter。
- TestMode/Oracle/internal state 只能证明 execution compatibility，不能证明 visible-policy compatibility。
- UI、正式 save、map presentation、network、Steam/cloud 单独记录为 platform capability。
- 所有新运行必须写 canonical input：seed、快照摘要、游戏/Mod/依赖版本、worker、配置、adapter、collector 和证据目录。
- 本续作默认不提交、不推送、不重写历史。

完成后写新的阶段交接报告，至少包含：
- PASS、PARTIAL 或 BLOCKED。
- 分支、HEAD、git status、实际修改和未修改输入。
- 执行命令、退出码、新证据目录。
- foundation/flow/content/stability 状态。
- content unit、registration identity、lifecycle matrix、interaction risk table 和平台能力缺口。
- primary/repeat/replay/failure 终态、digest、错误分类。
- Partial/Unsupported/Unrun/RuntimeFailure 分母和未覆盖语义。
- 下一唯一入口。

不要开始 M2-3，不要接入第二个角色。完成报告后停止。
```
