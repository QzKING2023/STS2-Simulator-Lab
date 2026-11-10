# M2-2 Yuki foundation 修复续作启动提示词

将下面整段粘贴到新对话：

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

继续 M2-2 的 YukiMakoto foundation/content 缺口，只处理票据 #4。当前工作单元是修复共享 foundation；先不要进入 Yuki 内容 probe、不要接入第二角色、不要开始 M2-3。先读取并遵守：
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
- docs/mods/stage-reports/m2-2-yuki-foundation-repair-20261109.md
- docs/mods/stage-reports/m2-2-yuki-foundation-repair-handoff-20261109.md
- docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md

开始前：
1. 执行 `. .\tools\maintenance\Set-GitHubSession.ps1`。用 `gh issue view 4 --json number,title,body,labels,state,assignees` 确认 #4 仍为 `OPEN`、标签为 `ready-for-agent`；另用 `gh issue view 3 --json number,title,labels,state` 确认 #3 已 `CLOSED`。不要运行 /triage、不要创建重复票据、不要关闭 #4。
2. 读取当前 branch、HEAD、git status，保护已有 dirty 修改。禁止 reset、clean、checkout、覆盖式复制和删除。
3. 确认固定 snapshot、manifest、游戏 `0.111.0` / commit `41cef1ea`、RitsuLib `compat/0.111.0`、Yuki `lib/0.111.0` exact variant 存在且 SHA-256 与 manifest/报告匹配。
4. 读取既有 `.build/runs/m2-2-yuki-20261108/` 五个 JSON，以及 `.build/runs/m2-2-yuki-foundation-audit-20261108-r2/` 的 `canonical-input.json`、`foundation-audit.json`、`evidence-index.json`。既有 Yuki primary/repeat/replay/failure 不重跑、不覆盖、不重分类。
5. 读取最新 repair evidence：
   - `.build/runs/m2-2-yuki-foundation-repair-20261109/canonical-input.json`
   - `.build/runs/m2-2-yuki-foundation-repair-20261109/foundation-audit.json`
   - `.build/runs/m2-2-yuki-foundation-repair-20261109/evidence-index.json`
   - `.build/runs/m2-2-yuki-foundation-repair-20261109-r52-primary2/`
   - `.build/runs/m2-2-yuki-foundation-repair-20261109-r52-remilia2/`

已接受事实：
- branch `main`，HEAD `7718d76399da8b515794fe5d68c2bfdaba59ebbd`；工作区已有 dirty 修改，必须保留。
- #4 为 `OPEN/ready-for-agent`，#3 为 `CLOSED`。
- 游戏 `v0.111.0` / commit `41cef1ea`。
- snapshot `workshop-snapshot-20261106-163352`，content SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`，manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- RitsuLib exact `compat/0.111.0)：runtime SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，loader SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- Yuki exact `lib/0.111.0)：SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- Yuki identity `REG.YUKI.lib/0.111.0#1@0.111.0`，canonical seed `LAB_M2_2_YUKI_01`。
- 既有 Yuki primary/repeat/replay 为真实 `Death / Completed` 且 state/RNG/trace/route 一致；failure 为 `Unknown / BudgetExceeded`，不能提升为 Death。
- Yuki inventory 586 units：`Covered=0`、`Partial=8`、`Unrun=578`、`Unsupported=0`、`RuntimeFailure=0`。
- foundation gate 当前为 `BLOCKED`。

当前 generic fixture：
- worker `SimulatorLab.NativeFoundationWorker`
- adapter `native-foundation/v1`
- collector `isolated NativeRun/GameProbe; foundation-trace/v1`
- source [tools/native-run/NativeFoundationModLoop.cs](/D:/STS2-Simulator-Lab/tools/native-run/NativeFoundationModLoop.cs) 不含 Yuki 名称/Workshop ID，也没有 Yuki-specific adapter。
- Acheron r52：真实 registration、duplicate rollback、28 callback events、reward→next-room；`observerDetached=true`、`registryCleared=true`，但 `runtimeAlcTeardown=false`、`alcAlive=true`、`assemblyAlive=true`。
- Remilia r52：同一 generic worker 加载 RitsuLib closure，26 callback events；真实 reward pool EngineError 后 raw process exit `-1073741819`，failure cleanup 未证明。

当前唯一 blocker：
- 不能把静态根清理、load-only unload 或单 Mod trace 当作 foundation PASS。
- 必须用安全的 generic mechanism 证明正常和 failure path 的 Mod hook/state、callback detach、registration registry、pending task/state 和 collectible ALC teardown。
- Acheron post-cleanup roots 仍包含 Ritsu `VariableMethod/VariableProperty`、Acheron `SpireField` 与 generated static roots；Remilia failure path 没有最终 cleanup。
- 若四类共享语义中任何一项没有真实跨族证据，立即停止为 `BLOCKED`，只写新的 audit evidence 和阶段交接报告，不启动 Yuki probe。

工作要求：
1. 先检查当前 worker、GameProbe/ModWorker、run manifest 和最高稳定 seam；核心模拟器继续不引用 Mod、Godot 或外部 DTO。
2. 诊断并修复 runtime teardown 根因；优先检查 RunManager/ActionExecutor/CombatManager pending task/state、RitsuLib reflection caches、Harmony/runtime caches、Mod static fields 和 failure-path cleanup。不要使用危险的全局静态清零、动态方法偷写 readonly、预测结果、空回调或注入结果。
3. 每次新运行使用新的 `.build/runs/m2-2-yuki-foundation-repair-<date>/...` 目录，并写 canonical input、registration identity、lifecycle matrix、interaction risk 和 cleanup evidence。
4. 至少用两个独立 Mod/内容族边界重新验证。Acheron 与 Remilia 的旧 evidence 只能作为诊断输入，不能假设已 PASS。
5. 不修改 Mod、snapshot、DLL、PCK、游戏二进制、既有 Yuki evidence 或 586-unit matrix；generic interface 不得出现 Yuki-specific branch。
6. 只有 foundation 四项全部 PASS，才允许选择最小 Yuki 内容族 probe；否则保持 BLOCKED。
7. 完成后写新的阶段交接报告，包含 PASS/PARTIAL/BLOCKED、branch/HEAD/status、实际修改和未修改输入、命令/退出码/新证据目录、foundation/flow/content/stability、registration identity/content units/lifecycle matrix/interaction risks/platform gaps、primary/repeat/replay/failure 终态/digest/error、Partial/Unsupported/Unrun/RuntimeFailure 分母和下一唯一入口。

默认不提交、不推送、不关闭 #4。报告完成后停止。
```

