# M2-3 Watcher 全流程交接报告

日期：2026-11-10

## 阶段状态

**PARTIAL**。

Watcher 在固定 `0.111.0` 游戏、固定 Workshop 快照和精确 BaseLib 闭包下完成了真实角色选择、初始状态、首战胜利、奖励领取、下一房间推进、第二战合法结束回合、真实 native `Death` 和 combat cleanup。`primary`、`repeat`、`replay` 三路稳定一致，`failure` 保留独立 `BudgetExceeded`。

content gate 已完成真实逐项批次：129 个标准运行时 identity 中 `Covered=127`、`Partial=0`、`Unsupported=2`、`Unrun=0`、`RuntimeFailure=0`。两个 Unsupported 是真实多人队友目标卡，单人流程没有合法目标；没有注入假队友。六个负 canonical-cost token 已完成真实激活观察，其中 `DEUS_EX_MACHINA` 走了真实 `AfterCardDrawn` 路径，只有“由其他内容生成”的来源链仍保留为 Partial。共享 foundation gate 沿用 M2-0 的 `PARTIAL`，所以本 Mod 不升级为 PASS。

## 票据、分支和工作区

- 子票：#5，开始前确认 `OPEN`、`ready-for-agent`；唯一阻塞 #4 已 `CLOSED`；本阶段已认领 #5。
- 分支：`main`；HEAD：`4a97ee7d08baed86fe153c49ac9838269d5474b3`。
- 开始前已有未提交修改全部保留：`README.md`、`docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md` 和三个维护脚本；没有 reset、clean、checkout、覆盖式复制、删除、提交或推送。
- 固定快照和 manifest 均存在且未修改。manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- 本阶段新增的 probe、Godot staging、运行目录和矩阵均位于 `.build/`；持久化文档和运行器修复为本报告及 `tools/native-run/run-native-slice.ps1`。没有修改 `SimulatorLab.Core`、Watcher 原始代码、Watcher DLL/PCK、BaseLib DLL/PCK、固定快照或正式游戏目录。

## 固定输入和能力矩阵

- 目标游戏：`v0.111.0`，commit `41cef1ea`。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`；内容 SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- Mod：Watcher `1.5.14`，Workshop ID `3747492505`，精确变体 `3747492505/Watcher.dll`。
- 依赖：`BaseLib@3.4.7` → Watcher；registration identity：`REG.WATCHER.ModelDb#129@0.111.0`。
- canonical seed：`LAB_M2_3_WATCHER_01`。
- worker：`SimulatorLab.NativeLifecycleProbe/Watcher-M2-3`；adapter：`watcher-m2-3-probe/v2-sentry-disabled`；collector：真实 Godot .NET 进程和 `SimulatorLab.NativeLifecycleProbe`，`native-trace/v1`，`sentry-disabled-overlay/v1`。
- Watcher DLL SHA-256：`0888e0eef851ba65300dc8485095e89cd0c2bdb49ae67915f7363ae81c6085c1`。
- BaseLib DLL SHA-256：`55863ca6adc30a61a7544874e157a519ffe148ccbf2b18025947609c4bff813ce`。
- 本阶段 collector probe SHA-256：`334b6501b75a17aa9e68e3d019af368ee18951af36c20a5563564e566a395f8c`。

## Crashpad 崩溃诊断与修复

截图中的 `crashpad_handler.exe` `0x80000003` 已在真实 Godot worker 中复现并归因。失败运行的 `godot.log` 先记录 `Sentry: Failed to initialize native SDK. Error code: 1`，随后每个隔离 profile 的 `sentry/**/crashpad-handler.log` 记录 `exception_handler_server.cc:347 Check failed: pipe != INVALID_HANDLE_VALUE. CreateNamedPipe: 拒绝访问 (5)`。进程采样还观察到 Watcher 版 Crashpad handler 在游戏退出后继续存活；因此弹窗是 Crashpad 自身的 `CHECK` 断点，是二次崩溃收集器故障，不是 Watcher 卡牌或角色逻辑。

修复位于最高稳定的 worker 启动 seam：`tools/native-run/run-native-slice.ps1` 为游戏根创建只含 hardlink 的独立 overlay，并写入该 overlay 的 `override.cfg`，将 `sentry/config/dsn` 设为空且 `sentry/options/auto_init=false`。游戏 EXE/PCK、BaseLib、Watcher 和 probe bytes 仍从原始 staging hardlink 读取；overlay 不改固定快照或正式游戏目录。当前 M2-3 probe 使用同一策略，adapter 升为 `watcher-m2-3-probe/v2-sentry-disabled`。

失败证据和修复前后对照见：[crashpad-summary.json](../../../.build/diag/crashpad-20261110/crashpad-summary.json)。修复后的四路新进程均无 `crashpad-handler.log`、raw exit 为 0，并保持原契约终态：primary/repeat/replay 为 `Death / Completed`，failure 为 `Unknown / BudgetExceeded`。其 canonical input digest 为 `941f454ddea806260afd5cd4a839cd9cd404d1e6984557bfe89cc75b84e8c0ca`，override SHA-256 为 `07a590739f9038b477187bebd422f37943986bac4d2b750e531b109b5fd42536`。

既有能力矩阵确认 Watcher 有 95 Card、25 Power、8 Relic、1 Character 的标准 identity；另有 11 个 Potion/Stance/Pool/Singleton ModelDb 行，只保留在原始 inventory，不冒充标准 content unit。六个负 canonical-cost token 为 `BECOME_ALMIGHTY`、`DEUS_EX_MACHINA`、`ENTER_CALM`、`ENTER_WRATH`、`FAME_AND_FORTUNE`、`LIVE_FOREVER`。本阶段没有新增 Mod-specific adapter；依赖的标准 `IsGeneratedCard` sentinel 继续作为最高稳定 seam。

## 执行命令和证据

| 命令 | 退出码 | 证据 |
| --- | ---: | --- |
| `dotnet build .build/diag/m2-3-watcher-probe/M23WatcherProbe.csproj -c Release --no-restore --ignore-failed-sources -p:NuGetAudit=false` | 0 | 临时 Godot probe；1 个 nullable warning，不进入跟踪文件 |
| fixed Omega/Deus probe rebuild（同上命令） | 0 | 2 个 nullable warning、0 error；新 DLL 只进入 `.build` overlay |
| `pwsh -NoProfile -ExecutionPolicy Bypass -File .build/diag/m2-3-watcher-probe/run-game.ps1 -Output .build/runs/m2-3-watcher-20261110-primary-r9 -RunKind primary` | 0 | `.build/runs/m2-3-watcher-20261110-primary-r9/` |
| 同上 `-RunKind repeat` | 0 | `.build/runs/m2-3-watcher-20261110-repeat-r2/` |
| 同上 `-RunKind replay -ReplayPath ...primary-r9/game-output/route.json` | 0 | `.build/runs/m2-3-watcher-20261110-replay-r2/` |
| 同上 `-RunKind failure` | 0 | `.build/runs/m2-3-watcher-20261110-failure-r2/`，`BudgetExceeded` |
| `dotnet build tools/mod-worker/WatcherStage6.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false` | 0 | 0 warning、0 error |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| `pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestWatcherStage6.ps1 -WorkerRunManifestPath .build/runs/mod-matrix-20261108/3747492505-Watcher/run-manifest.json` | 0 | 既有无 probe `Blocked/MOD-010` deterministic regression |
| `git diff --check` | 0 | 无新增 whitespace error；仅保留仓库既有换行提示 |
| Crashpad 修复前后对照（四个新进程） | 0 | `.build/diag/crashpad-20261110/crashpad-summary.json`；fixed primary/repeat/replay/failure |
| `tools/native-run/run-native-slice.ps1` watcher-stage6 overlay gate | 1 | 在进入 worker 前命中既有 build manifest 输入哈希阻塞：`Changed source/input: .build/pinned-inputs/watcher-native-host/MainLoopContext.cs`；未绕过、未重写输入 |
| 固定 overlay 的 95-card batch | 0 | `.build/runs/m2-3-watcher-fixed-card-batch-20261110.json`；91 个 Death、Deus recheck、Omega recheck、2 个真实 Unsupported |
| 固定 overlay 的 25-power/7-relic batch | 0 | `.build/runs/m2-3-watcher-fixed-power-relic-batch-20261110.json`；32 个 `ContentLifecycleCompleted` |
| starting Pure Water recheck | 0 | `.build/runs/m2-3-watcher-fixed-relic-recheck-pure-water-20261110/`；真实 remove/obtain/remove |
| 固定 content matrix 汇总 | 0 | `.build/runs/m2-3-watcher-fixed-content-summary-20261110/content-matrix.json` |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`（修复后复核） | 0 | `PASS all 137 contract checks` |
| `dotnet build tools/mod-worker/WatcherStage6.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false`（修复后复核） | 0 | 0 warning、0 error |

汇总入口：[contract-summary.json](../../../.build/runs/m2-3-watcher-20261110/contract-summary.json)、[evidence-index.json](../../../.build/runs/m2-3-watcher-20261110/evidence-index.json)、[content-matrix.json](../../../.build/runs/m2-3-watcher-20261110/content-matrix.json)、[lifecycle-matrix.json](../../../.build/runs/m2-3-watcher-20261110/lifecycle-matrix.json)、[interaction-risk.json](../../../.build/runs/m2-3-watcher-20261110/interaction-risk.json)。

固定内容汇总入口：[content-matrix/v2](../../../.build/runs/m2-3-watcher-fixed-content-summary-20261110/content-matrix.json)、[lifecycle-matrix/v2](../../../.build/runs/m2-3-watcher-fixed-content-summary-20261110/lifecycle-matrix.json)、[interaction-risk/v2](../../../.build/runs/m2-3-watcher-fixed-content-summary-20261110/interaction-risk.json)。

## 真实生命周期和四路终态

primary 的 trace 依次记录 `role-discovered`、`role-selected`、`initial-state`、Neow opening、两次 map transition、两次 `combat-enter`、首战 `battle-victory`、`reward-offer`、`reward-select`、`reward-complete`、第二战合法 `end-turn`、`death` 和 `cleanup`。

Watcher 身份为 `CHARACTER.WATCHER-WATCHER`。初始卡组为 4×Strike、4×Defend、Vigilance、Eruption；起始遗物为 `RELIC.WATCHER-PURE_WATER`。首战真实动作使用 Eruption、Strike、Defend 和生成的 Miracle；奖励领取了 Gold 和 CardReward，CardReward 取得 `CARD.WATCHER-TANTRUM`；跨房间后第二战只发送合法 `EndPlayerTurnAction`，玩家 HP 到 0，随后观察到 native Death 和 combat cleanup。

primary、repeat、replay 的 canonical input digest 均为 `028ce20de0425f0433d8834477f4f04461f90fb5216e85b05b114f163629905a`，并保持以下 digest：

| 路径 | 终态 / stop | state digest | RNG digest | trace digest | route digest | raw exit |
| --- | --- | --- | --- | --- | --- | ---: |
| primary | `Death / Completed` | `1116b97bb95a82db1a0cadd07218a822096213f86536d5cc1f207b86938d72d2` | `5bd6a49226a508cae635dd73787c559c8a16c032270e3353d6cbc204464661ef` | `a24110811c0e77d92ab32a35f8cd5fec6bf7d66249329316ccbf13f62d44783c` | `077276400d609cda13422fbe87f2289f817ae20bdfd9307c4a0964c90d84d911` | 0 |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 0 |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 0 |
| failure | `Unknown / BudgetExceeded` | `074608a9e405466b126db0d09b160a11b4399243615bfd785703196c49494c54` | `615e282c3a3f1054df4d2c42d586ea796380889cecb2396bbb0cac385e60b11e` | `dea1973ed3fcc783c1ce9215b1f5a078951bcd6e1c36318bb91f2ecfde8af2b3` | 无 | 0 |

`failure` 在首战奖励完成后、第二房间之前独立预算停止；没有产生 Death，也没有被提升为成功终态。早期 probe/staging 诊断目录（`primary`、`primary-r2` 至 `primary-r7`）保留了 manifest 配置错误、shuffle FTUE 运行时异常、过早 quit-after 和错误房间边界等原始证据，没有写入最终 content matrix，也没有被重分类为成功。

Crashpad 修复后的四路同契约 cohort 使用新 adapter，input digest 为 `941f454ddea806260afd5cd4a839cd9cd404d1e6984557bfe89cc75b84e8c0ca`；primary/repeat/replay 的 state/trace/route digest 与上表成功 cohort 一致，failure 的终态和 digest 也一致。四路 raw exit 均为 0，Crashpad 日志计数均为 0；failure 的错误分类仍为 `BudgetExceeded`，不是 `RuntimeFailure`。

## foundation、flow、content gate

- **Foundation gate：PARTIAL。** Watcher 的 BaseLib 精确闭包、ModManager 顺序、ModelDb 注册和真实 Godot 初始化均已观察；M2-0 冻结的共享 BaseLib/RitsuLib/MinionLib 跨房间、回调和 teardown 语义仍未全局闭合。没有用 Watcher 特判绕过该缺口。
- **Flow gate：PASS。** 三条成功 cohort 路径都完成真实角色 run 并到达 native `Death / Completed`；这是实际终态，不是预测死亡、预算停止或首战 SliceBoundary。
- **Content gate：PARTIAL。** 固定 overlay 批次覆盖 129 个标准 content unit：`Covered=127`（1 Character、93 Card、25 Power、8 Relic）、`Partial=0`、`Unsupported=2`（Hold Fast、Shared Wisdom 的多人队友目标）、`Unrun=0`、`RuntimeFailure=0`。六个负 canonical-cost token 均有真实激活证据；其“由其他内容生成”的来源链仍是 Partial。
- **Stability：PASS。** primary/repeat/replay 的 canonical input、state、RNG、trace、route digest 一致；failure 为独立 BudgetExceeded 控制。

## lifecycle matrix、interaction risk 和平台能力

角色发现/选择、初始状态、首战胜利、奖励生成与领取、跨房间推进、真实 Death、combat cleanup、全量 Card effect、8 个 Relic 生命周期和 25 个 Power 生命周期为 `Covered`。负-cost token generation 为 `Partial`；Hold Fast/Shared Wisdom 的单人 target seam 为 `Unsupported`；正式 save/load 为 `Unsupported` 平台能力。

交互风险表中，角色选择、Death/cleanup、Card target/effect（除两个多人目标）、8 个 Relic 生命周期和 Power apply/remove 为 `Covered`；reward/deck persistence、cross-room state、负 canonical-cost token generation 为 `Partial`；Hold Fast/Shared Wisdom 的单人 target 为 `Unsupported`；visible-policy 为 `Unsupported`，因为 probe 使用 TestMode/internal state，只证明 execution compatibility。UI、正式 save/load、地图 presentation、network、Steam/cloud 仍为独立平台能力缺口，没有计入 gameplay gate。

## 验收标准逐项结果

| 验收标准 | 结果 |
| --- | --- |
| Watcher identity 和 initial state | 通过；真实 `CHARACTER.WATCHER-WATCHER`、10 张起始牌和 Pure Water 已见证 |
| 真实 RunVictory 或 Death | 通过；primary/repeat/replay 均为 native Death |
| 战斗、奖励、推进、清理 | 通过；首战 victory、Gold/CardReward、第二房间和 native cleanup 已观察 |
| primary/repeat/replay/failure | 通过；四个新进程、同 canonical digest cohort 和独立 BudgetExceeded |
| foundation gate | 部分；仍继承全局 foundation `PARTIAL` |
| 所有 content unit 生命周期 | 部分通过；`127 Covered / 0 Partial / 2 Unsupported / 0 Unrun / 0 RuntimeFailure`，保留固定 overlay 逐行矩阵 |
| 固定版本、快照和输入只读 | 通过；固定 snapshot、manifest、Watcher/BaseLib DLL/PCK 未写入或替换 |
| visible-policy、UI、save/map/network/Steam/cloud | 明确缺口；单列为 Unsupported/platform capability |

## 分母、已知缺口和未覆盖语义

- Content unit 分母：129（95 Card、1 Character、8 Relic、25 Power）。`Other` 的 11 个 ModelDb 行不进入本阶段标准 content denominator，但保留在 raw inventory。
- Content 状态分母：`Covered=127`、`Partial=0`、`Unrun=0`、`Unsupported=2`、`RuntimeFailure=0`。
- Run 状态分母：成功 cohort `primary=1 / repeat=1 / replay=1`，failure `1`；成功终态为 `Death=3`、`RunVictory=0`。
- Foundation 分母：共享 foundation gate `PARTIAL`；Watcher 只证明 BaseLib 路径上的窄语义，未覆盖全局 RitsuLib/MinionLib 语义。
- 没有闭合负-cost token 的来源生成链、多人队友目标卡的合法目标运行、正式 save/load、visible-policy、UI presentation、完整 map policy、network replay、Steam/cloud。
- 运行器和 collector 的早期失败证据已保留在独立目录；最终 accepted cohort 没有把这些诊断失败计入 content unit `RuntimeFailure`。

## 下一阶段唯一建议入口

下一阶段唯一建议入口：先闭合 M2-0 foundation gate；Watcher 的剩余缺口已经精确收敛为 token 来源生成和多人队友目标平台能力，不接入 Aemeath、figure_Saya、Ryoshu 或其他 Mod。

是否提交或推送：**否**。
