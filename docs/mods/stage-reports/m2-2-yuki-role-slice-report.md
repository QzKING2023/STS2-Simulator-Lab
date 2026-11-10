# M2-2 YukiMakoto 角色全流程纵切片交接报告

## 阶段状态

**PARTIAL**。

YukiMakoto 的 flow gate 和稳定性证据通过：精确 `lib/0.111.0` 变体在精确 RitsuLib `compat/0.111.0` 闭包下完成真实角色选择、首战胜利、奖励领取、地图/房间进入、第二战合法动作和真实 native `Death`。`primary`、`repeat`、`replay` 的 canonical input、终态、state/RNG/trace/route digest 一致，独立 `failure` 保留 `BudgetExceeded`。

content gate 仍为 `PARTIAL`：运行时注册清单冻结出 586 个身份，其中只有本纵切片涉及的角色、起始卡、起始遗物和奖励卡收到部分生命周期证据，578 个单元仍为 `Unrun`。foundation gate 继承 M2-0 的 `PARTIAL`，没有用 Yuki 特判或单 Mod adapter 绕过共享语义缺口。

## 票据、分支和工作区

- 子票：#4，开始前确认 `OPEN`、`ready-for-agent`；唯一阻塞 #3 已 `CLOSED`；已按约定认领。
- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 开始前已有未提交修改全部保留；未执行 reset、clean、checkout、覆盖式复制或删除。
- 本阶段没有修改固定快照、快照 manifest、Mod DLL/PCK/资源、SimulatorLab.Core、Mod 原有代码或二进制。新增的采集器、派生游戏目录和运行证据位于 `.build/`；交接文档为本文件。
- 是否提交或推送：**否**。

## 固定输入和 Mod 选择

- 目标游戏：`0.111.0`，commit `41cef1ea`。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`。
- 快照内容 SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 快照 manifest SHA-256：由每条 `canonical-input.json` 写入，固定为同一 sibling manifest。
- Mod：YukiMakoto `v0.0.7-hot`，Workshop ID `3829924471`。
- 精确变体：`3829924471/lib/0.111.0/YukiMakoto.dll`，SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- 依赖闭包：`STS2-RitsuLib -> YukiMakoto`。
- RitsuLib 精确变体：`3747602295/compat/0.111.0/STS2-RitsuLib.Runtime.dll`，SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`；兼容入口 `STS2-RitsuLib.dll` SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- 静态 worker 已成功解析闭包；既有原生初始化日志确认 Yuki loader 选择 numeric `0.111.0`，RitsuLib 版本 `0.6.10`、compat branch `0.111.0`，Yuki 注册完成。因此没有改选 Aemeath。
- canonical seed：`LAB_M2_2_YUKI_01`。
- worker/collector：独立 native game 进程加载 `YukiMakoto`，采集器为通用 `SimulatorLab.NativeLifecycleProbe`，adapter schema `native-mod-loop/v1`，trace schema `native-trace/v1`。核心模拟器未引用 Mod、Godot 或外部 DTO。

固定输入、依赖文件摘要、游戏构建、worker 身份、配置、adapter、collector 和 replay reference 见 [contract-summary.json](../../../.build/runs/m2-2-yuki-20261108/contract-summary.json) 和各运行目录的 `canonical-input.json`。

## 执行命令和证据

| 命令 | 退出码 | 证据 |
| --- | ---: | --- |
| `dotnet build .build/diag/m2-2-yuki-probe/M22YukiProbe.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false` | 0 | 通用原生生命周期采集器，0 warning / 0 error |
| `run-game.ps1 -RunKind primary` | 0 | `.build/runs/m2-2-yuki-20261108-primary-10/` |
| `run-game.ps1 -RunKind repeat` | 0 | `.build/runs/m2-2-yuki-20261108-repeat-02/` |
| `run-game.ps1 -RunKind replay -ReplayPath ...primary-10/game-output/route.json` | 0 | `.build/runs/m2-2-yuki-20261108-replay-02/` |
| `run-game.ps1 -RunKind failure` | 0 | `.build/runs/m2-2-yuki-20261108-failure-02/`，结果为 `BudgetExceeded` |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| `git diff --check` | 0 | 无 whitespace error；仅保留既有换行提示 |

## 角色、初始状态和真实生命周期

真实角色身份为 `CHARACTER.YUKI_MAKOTO_CHARACTER_YUKI_MAKOTO`，类型 `YukiMakotoMod.Characters.YukiMakoto`。

- 起始卡组：4×`CARD.YUKI_MAKOTO_CARD_STRIKE`、5×`CARD.YUKI_MAKOTO_CARD_DEFEND`、`CARD.YUKI_MAKOTO_CARD_DARK_HOUR`。
- 起始遗物：`RELIC.YUKI_MAKOTO_RELIC_WILD_CARD`、`RELIC.YUKI_MAKOTO_RELIC_VELVET_ROOM`、`RELIC.YUKI_MAKOTO_RELIC_MEANING_OF_SEEKING`。
- 起始 HP：70。
- Neow 选项通过真实同步器选择；从 Neow 进入 `MapCoord (3, 0)` 的 Monster 节点，随后进入真实 CombatRoom。
- 首战使用合法卡牌/回合动作，真实战斗胜利后收到 GoldReward 和 CardReward；奖励边界选择 GoldReward 与 CardReward，CardReward 给出 `CARD.YUKI_MAKOTO_CARD_DEFENSE_MEMORY`，reward set 完成。
- 第二战使用合法 `EndPlayerTurnAction`，玩家 HP 真实降到 0；trace 观察 `death`，随后 `combatInProgress=false`、`playerDead=true` 的 `cleanup`。

关键生命周期事件在 `primary-10/game-output/trace.jsonl`：`role-discovered`、`role-selected`、`initial-state`、`room-opening-complete`、`map-transition`、`combat-enter`、`battle-victory`、`reward-offer`、`reward-select`、`reward-complete`、`death`、`cleanup`。

## 四路终态和 digest

`primary`、`repeat`、`replay` 使用同一 canonical input digest `73ecc49093727f9036a390326fcae06742e2c29a9ed638d1fdaf147da49c1f8a`，并保持以下稳定值：

| 路径 | 终态 / stop | state digest | RNG digest | trace digest | route digest | raw exit |
| --- | --- | --- | --- | --- | --- | ---: |
| primary | `Death / Completed` | `79ca17c18aadeac4b979bca04eceb87f4711a5f6a7500941ee8f63ba89e3d8e2` | `620a655bd2e4574686aa860b3b01af30ebc16020f4e9b556db479b1d3e1a4f09` | `dd8513bec6fa299b3f474a04c9b8a63bcebeb7762755b5a957443e0c3fe26f5b` | `b6a490a8c33fbd471f9457bc284c63dd0b7a4d4c83c20ad37e1fa4b1184070e7` | 0 |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 0 |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 0 |
| failure | `Unknown / BudgetExceeded` | `a54b5f510189b75e2e579087059defd4d52c19288fd421616e227542e193d5ba` | `dbcbe2b7e6f06c4ed23e2b24cf9212c373cf8059f2cbfe6b55ed782706893b30` | `b079bbeaf580370e99d734f950aabd476277cdb3e7430f116d921560e0f7c9b8` | 无 | 0 |

failure 在完成首战奖励边界后独立停止，trace 没有 `death`，没有把预算停止提升为终态。

## 验收标准逐项结果

| 验收标准 | 结果 |
| --- | --- |
| 精确依赖闭包和 Mod 选择 | 通过；Yuki exact `lib/0.111.0`，没有静默回退或改选角色 |
| 自定义角色选择和初始卡组/遗物 | 通过；真实 `CharacterModel`、起始卡和遗物已记录 |
| 合法战斗动作、奖励、房间/地图推进和清理 | 部分通过；Neow→Monster→Combat、首战胜利、奖励完成、第二战清理已观察；奖励后继续进入新 map point 的跨房间证据仍未闭合 |
| 真实 RunVictory 或真实 Death | 通过；三条成功路径均为真实 `Death` |
| 两个新进程 primary/repeat、一个 replay、失败隔离 | 通过；四路均为新进程，raw exit 0，failure 为明确 `BudgetExceeded` |
| 所有冻结 content unit 生命周期和关键 interaction contract | 未通过；content gate 为 `PARTIAL`，见下方矩阵 |
| foundation gate | `PARTIAL`；沿用共享 BaseLib/RitsuLib/MinionLib 未闭合项，没有单 Mod 绕过 |
| 固定快照、核心边界和 Mod 原始输入未破坏 | 通过；快照和 Mod 输入只读，核心未引用 Mod/Godot |

## Foundation、content unit、lifecycle 和 interaction 状态

- Foundation gate：`PARTIAL`。RitsuLib exact registration/initialization 和 Yuki loader 依赖顺序已观察；共享跨房间、全量回调、清理和跨 Mod 语义仍未闭合。
- Runtime content inventory：Card 270、Character 1、Other 30、Power 281、Relic 4，共 586。
- Content unit 状态分母：`Covered=0`、`Partial=8`、`Unrun=578`、`RuntimeFailure=0`。8 个 Partial 是 1 个角色、3 个起始卡 identity、3 个起始遗物 identity 和 1 个奖励卡 identity；它们参与了本片但没有完成所有适用检查点。
- 生命周期矩阵见 [lifecycle-matrix.json](../../../.build/runs/m2-2-yuki-20261108/lifecycle-matrix.json)。Character 的选择、起始状态、战斗进入、死亡和 cleanup 已有证据；Card/Relic 只覆盖起始/选中边界；Power 和 Other 仍 Unrun。
- 交互风险表见 [interaction-risk.json](../../../.build/runs/m2-2-yuki-20261108/interaction-risk.json)。依赖加载、角色选择、奖励领取、死亡 cleanup 为 Covered；卡牌效果和 map/room 为 Partial；跨房间 persistence 为 Unrun；visible-policy 为 Unsupported。
- 完整 append-only content matrix 见 [content-matrix.json](../../../.build/runs/m2-2-yuki-20261108/content-matrix.json)。

## 已知缺口和错误分类

- `Partial`：foundation gate 1 项；content unit 8 项；card/effect、post-reward map transition、跨房间 persistence 和 platform carrier 仍未闭合。
- `Unsupported`：本采集器使用 TestMode/internal state，只证明 execution compatibility，不证明 visible-policy compatibility；UI、正式 save、Steam/cloud、network/replay network 也未纳入 gameplay gate。
- `Unrun`：578 个 Yuki runtime content unit；Power 281 和 Other 30 的生命周期；未观察的复杂生成卡、全量 Relic trigger、跨房间状态和失败回调。
- `RuntimeFailure`：最终四路为 0。早期探索目录保留了 card selector 未安装、headless screen-shake/FTUE、未观察 Death 等原始失败，未被改写为成功；最终 accepted cohort 使用 `primary-10`、`repeat-02`、`replay-02`、`failure-02`。
- `RunVictory`：本片未运行；真实 `Death` 已满足 flow gate，但不代表 Victory 路径或全包 content gate。
- 平台能力缺口：headless 采集器提供了通用 screen-shake target 以让原生 callback 继续执行；该平台设置写入 canonical configuration，不能外推 UI/保存/Steam/cloud 等能力。

## 下一阶段唯一建议入口

先闭合 **共享 foundation gate 中 Yuki 所依赖的 RitsuLib 注册、callback、跨房间和 cleanup 语义**，然后在同一 Yuki canonical input 下按风险顺序补齐剩余 Card/Power/Relic content unit；不要接入第二个角色或把这条 Partial 结果升级为 PASS。


