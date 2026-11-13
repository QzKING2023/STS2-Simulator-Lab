# M2-3 figure_Saya 全流程交接报告

日期：2026-11-10  
子票：#7 `M2-3: Close figure_Saya’s flow/content slice`  
阶段状态：**PARTIAL**

figure_Saya 在固定游戏 `0.111.0`、固定 Workshop 快照和精确 `BaseLib@3.4.7` 闭包下完成了真实自定义角色选择、初始状态、首战胜利、奖励选择、跨房间推进、第二场战斗、原生 `Death` 和 cleanup。随后对 115 张 Card、69 个 Power、6 个 Relic 逐个启动 fresh process，全部完成对应的真实构造/效果/清理生命周期。primary、repeat、replay 使用同一 canonical input，三路的 state/RNG/trace/route digest 一致；failure 独立停在 `BudgetExceeded`。Content gate 已达到 `PASS`；共享 foundation gate 仍为 `PARTIAL`，所以阶段总状态保留为 `PARTIAL`。

## 分支、工作区和固定输入

- 分支：`main`
- HEAD：`4cae8b4cf90d1be358619bdbf5a9e79d53c896b4`
- 开始时已有 dirty 修改，全部保留：`README.md`、`docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md`、`tools/maintenance/cleanup-workspace.ps1`、`tools/maintenance/register-workspace-cleanup.ps1`、`tools/maintenance/run-workspace-cleanup-task.ps1`，以及既有 Aemeath 报告。
- 本阶段新增长期文件：本报告。探针源码、runtime hardlink overlay、manifest 和运行证据均在 `.build/` 下，未改固定快照、Mod 源码、DLL、PCK、游戏二进制或正式数据。
- 固定快照目录：`local-mods/workshop-snapshot-20261106-163352`，存在。
- 固定 manifest：`local-mods/workshop-snapshot-20261106-163352.manifest.json`，SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- snapshot content SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 目标游戏：`0.111.0`，commit `41cef1ea`。
- Mod：`figure_Saya`，Workshop ID `3747508952`，版本 `2.1.0`。
- 精确变体：`3747508952/figure_Saya.dll`，SHA-256 `7558a0d041b74fbe1ac88767be1a0d88a95e4efe61ba645b257e0dc9af54cbe7`。
- PCK SHA-256：`c2df2ad9aac081c81529001367b5465eaf188219d97c7cf663297d545084ede6`。
- 依赖闭包：`BaseLib@3.4.7`；没有静默选择其他游戏版本目录。
- registration identity：`REG.FIGURE_SAYA.ModelDb#196@0.111.0`；运行时角色程序集为 `figure_Saya.Impl`。
- seed：`LAB_M2_3_FIGURE_SAYA_01`。
- worker：`SimulatorLab.NativeLifecycleProbe/figure_Saya-M2-3`。
- adapter：`figure-saya-m2-3-probe/v1-no-runtime-patch`。
- collector：真实 Godot .NET 进程、`SimulatorLab.NativeLifecycleProbe`、`native-trace/v1` 和 sentry-disabled hardlink overlay；collector 没有 Harmony、运行时补丁、结果注入、回调跳过或假 DTO。

固定输入和四路结果的索引见 [contract-summary.json](../../../.build/runs/m2-3-figure-saya-20261110/contract-summary.json)、[evidence-index.json](../../../.build/runs/m2-3-figure-saya-20261110/evidence-index.json) 和 [collector-summary.json](../../../.build/runs/m2-3-figure-saya-20261110/collector-summary.json)。

## 执行命令和证据

| 命令 | 退出码 | 证据 |
| --- | ---: | --- |
| `. .\tools\maintenance\Set-GitHubSession.ps1 -Verify; gh issue view 7 --json number,title,body,labels,state,assignees` | 0 | #7 为 `OPEN`、`ready-for-agent`，唯一 blocker #4 已关闭 |
| `gh issue edit 7 --add-assignee '@me'` | 0 | #7 已认领 |
| `dotnet restore .build/diag/m2-3-figure-saya-probe/M23FigureSayaProbe.csproj --ignore-failed-sources -p:NuGetAudit=false` | 0 | 探针资产生成 |
| `dotnet build .build/diag/m2-3-figure-saya-probe/M23FigureSayaProbe.csproj -c Release --no-restore -p:NuGetAudit=false` | 0 | 0 error；3 个 nullable warning，未影响运行证据 |
| `pwsh ... prepare-root.ps1 -Suffix '-r22'` | 0 | `.build/native-game/figure-saya-m2-3-20261110-r22/`；只读输入使用 hardlink |
| `pwsh ... run-game.ps1 -Output ...primary-r22 -RunKind primary -RootSuffix '-r22'` | 0 | `.build/runs/m2-3-figure-saya-20261110-primary-r22/` |
| 同上 `-RunKind repeat` | 0 | `.build/runs/m2-3-figure-saya-20261110-repeat-r22/` |
| 同上 `-RunKind replay -ReplayPath ...primary-r22/game-output/route.json` | 0 | `.build/runs/m2-3-figure-saya-20261110-replay-r22/`，`replay-check.json` matched |
| 同上 `-RunKind failure` | 0 | `.build/runs/m2-3-figure-saya-20261110-failure-r22/`，`BudgetExceeded` |
| `run-content-batch.ps1 -Kind Card -RootSuffix '-r13'` | 0 | 115 Card fresh processes：113 首次通过，2 个 AnyAlly 单元经真实第二角色重试通过 |
| `run-content-batch.ps1 -Kind Power -RootSuffix '-r13'` | 0 | 69/69 Power fresh processes通过 Apply/observe/Remove |
| `run-content-batch.ps1 -Kind Relic -RootSuffix '-r13'` | 0 | 6/6 Relic fresh processes通过 Obtain/Remove |
| `python .build/diag/m2-3-figure-saya-probe/build-evidence.py` | 0 | `.build/runs/m2-3-figure-saya-20261110/` 五个矩阵/索引 JSON |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| `git diff --check` | 0 | 无新增 whitespace error |

早期无补丁 observer 在 inventory 过滤修正前的 `.build/runs/m2-3-figure-saya-20261110-clean-primary-r8/` 以 `RuntimeFailure`（ModelDb discovery timeout）结束；它保留为诊断证据，没有进入接受 cohort，也没有被重分类为玩法结果。

## 四路终态、digest 和错误分类

四路接受 cohort 的 canonical input digest 均为 `457f2a857f04a4ce7c6906c29148dc6fdf6da9718f27fbe4940dab062e8ab41c`。

| 路径 | 终态 / stop | state digest | RNG digest | trace digest | route digest | 错误分类 |
| --- | --- | --- | --- | --- | --- | --- |
| primary | `Death / Completed` | `eb5cf335530a922641b3f30b1d86fb31298da0f2954890daab64b8e318e37adb` | `96b7511467b69cadaa9c1b39bc42da897c30c1cbaa120b6bb612233009918e64` | `b2e854c4fc93194cf3bcdd35be81f92d192b23f83a145ebbc917bf8526e46be8` | `59ced4e9cd05f8c3a5f29f7b63f0f0a4fed496649025e810f806d9c108c0419a` | 无 run-level error |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 无 run-level error |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | `replay-check.json` route digest matched |
| failure | `Unknown / BudgetExceeded` | `f00585e3f1e0623e94fb9a6320f3358a5168a303bbe91f9bcaa90d179d1b8f5f` | `adaeec2f819911d037ab1d784b3e724412a2dcdf4dea1969829842c833950db6` | `1eb9592c526e3c4585c6a377f82ac5abf0671d5a022ce4ab5e73636516d18e34` | `null` | 受控预算停点，非 native terminal |

primary trace 见证角色选择、初始状态、Neow、两次 map transition、两次 combat enter、首战 `battle-victory`、Gold/Card reward、第二场合法结束回合、native `Death` 和 cleanup。角色初始 HP 为 `70`；起始卡组为 11 张，起始遗物为 4 个。奖励后的牌组增加 `CARD.FIGURE_SAYA-LIFE_CATALYSIS_CARD`，并进入第二场。

## Foundation、flow、content gate

- **Foundation gate：PARTIAL。** figure_Saya 的 BaseLib 精确闭包、Mod 初始化、ModelDb 注册和 native 生命周期已观察；共享 BaseLib/RitsuLib/MinionLib 的跨房间状态、callback/before-death 顺序和 teardown 仍未全局闭合，没有用 figure_Saya 专用 adapter 绕过 foundation。
- **Flow gate：PASS。** primary/repeat/replay 都是真实 native `Death / Completed`，经过自定义角色选择、首战胜利、奖励、第二场推进和合法死亡；首战胜利、预算停点、预测死亡和 `Unsupported` 没有被当作 Run 终态。
- **Content gate：PASS。** 191 个标准 content unit 的状态为 `Covered=191`、`Partial=0`、`Unrun=0`、`Unsupported=0`、`RuntimeFailure=0`。raw inventory 另有 5 个 `Other` identity，保留在 `content-inventory.json`，不进入标准分母。
- **Stability：PASS。** 四路输入一致，primary/repeat/replay 的 state、RNG、trace、route digest 一致；failure 是独立 `BudgetExceeded` 控制。

逐项矩阵：

- [content-matrix.json](../../../.build/runs/m2-3-figure-saya-20261110/content-matrix.json)：196 个 raw identity；标准分母 191 个，`Character=1`、`Card=115`、`Power=69`、`Relic=6` 全部为 `Covered`。每个单元都有独立 fresh-process trace、manifest 和 final state。
- [lifecycle-matrix.json](../../../.build/runs/m2-3-figure-saya-20261110/lifecycle-matrix.json)：发现/注册、角色选择、初始状态、战斗、奖励、跨房间、死亡、cleanup、全部 Card effect、全部 Power Apply/Remove、全部 Relic Obtain/Remove 均为 `Covered`；正式 save/load 仍是平台 `Unsupported`。
- [interaction-risk.json](../../../.build/runs/m2-3-figure-saya-20261110/interaction-risk.json)：逐项记录 foundation、角色身份、卡牌目标/效果、奖励持久化、Relic ownership、Power stacking、死亡 cleanup、visible-policy 和平台能力。
- [capability-matrix.json](../../../.build/runs/m2-3-figure-saya-20261110/capability-matrix.json)：记录 assembly、角色 identity、初始牌组/遗物、已运行表面和平台缺口。

## 平台能力和已知缺口

- visible-policy：`Unsupported`。collector 使用 `TestMode`、`DebugOnlyGetState` 和 native 内部状态，只证明 execution compatibility。
- UI presentation、正式 save/load、地图 presentation、network replay、Steam 和 cloud：`Unsupported`，单独计为 platform capability。
- 自定义 PCK 的 UI/表现路径没有作为角色玩法内容门通过；没有把 headless 可见性当作玩家策略边界。
- 所有 191 个标准单元均已运行并有真实 lifecycle 证据；两个 AnyAlly Card（`BURST_INTO_BLOSSOM`、`WALK_TO_WORLDS_END_WITH_BELOVED`）通过真实第二角色和合法目标重试闭合。
- 内容门覆盖的是每个单元的正常构造/激活/效果/清理路径；Power 多层 stack、turn expiry、Relic before-death 和跨房间 hook 变体仍在 interaction risk 表中作为后续边界，不降低单元覆盖状态。
- `RunVictory=0`；本 Mod 的 flow gate 由真实 `Death` 满足。accepted run-level 分母为 `Death=3`、`BudgetExceeded=1`、`RunVictory=0`、`Unsupported=0`、`RuntimeFailure=0`；另有 1 条早期诊断 observer `RuntimeFailure`，已独立保留。
- 未覆盖语义：figure_Saya 的演化/选择卡全量效果、Power stack/expiry、Relic hook/before-death、完整自定义奖励集合、所有 Other identity 的玩法归属、正式平台能力和 visible-policy。

## 验收标准逐项结果

| 验收标准 | 结果 |
| --- | --- |
| figure_Saya identity 和 initial state 在 native lifecycle 中见证 | 通过；真实 `CHARACTER.FIGURE_SAYA-SAYA_CHARACTER`、11-card starting deck、4 starting Relics、HP70 |
| 真实 RunVictory 或 Death | 通过；primary/repeat/replay 均为真实 native `Death` |
| 战斗、奖励、推进、清理 | 通过；首战 Victory、Gold/Card reward、第二场推进、Death 和 cleanup 均有 trace |
| primary/repeat/replay/failure | 通过；四个新进程、同 canonical input；failure 为 `BudgetExceeded` |
| content lifecycle 和 interaction | 通过；191 个标准单元全部 `Covered`，每个单元都有 fresh-process lifecycle evidence；content gate PASS |
| foundation gate | 部分；共享基础仍为 `PARTIAL` |
| 固定快照、exact game version、只读边界 | 通过；manifest、Mod DLL/PCK、BaseLib 和游戏版本摘要已锁定，输入未修改 |
| UI/save/map/network/Steam/cloud/visible-policy | 明确缺口；单列为 platform capability，不伪装成 gameplay 通过 |

## 下一阶段唯一建议入口

先闭合 **M2-0 共享 foundation gate 的跨房间状态、callback/before-death 顺序和 teardown 语义**；figure_Saya 的 191 个标准 content unit 已完成覆盖，后续只需在该 foundation 入口闭合后复核这些已接受证据。

是否提交或推送：**否**。没有提交、推送、重写历史或创建远程 PR。
