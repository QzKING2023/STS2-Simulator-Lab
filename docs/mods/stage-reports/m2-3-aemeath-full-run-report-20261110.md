# M2-3 Aemeath 全流程交接报告（最终内容闭合）

日期：2026-11-10

## 阶段状态

**PARTIAL（Aemeath ticket acceptance：PASS；全局 foundation gate：PARTIAL）**。

Aemeath 的 flow gate、content gate 和 stability gate 已全部通过。content gate 的 201 个标准 content unit 全部完成真实生命周期证据：`Covered=201`、`Partial=0`、`Unsupported=0`、`Unrun=0`、`RuntimeFailure=0`。

全局 M2-0 foundation gate 仍为 `PARTIAL`，这是共享 BaseLib/RitsuLib/MinionLib 的独立前置，不再作为 Aemeath 内容缺口混入 content gate。Aemeath 自定义攻击在 headless 环境记录的 `NCreature` 动画 `NullReferenceException` 作为平台能力缺口保留；专用 Card probe 同时证明了真实 native effect 和目标 HP `44→38`，不阻断内容 unit。

## 票据、分支和工作区

- 子票：#6，已评论并关闭；本次补充证据把该票的 flow/content/stability acceptance 提升为 `PASS`。
- 分支：`main`；HEAD：`4cae8b4cf90d1be358619bdbf5a9e79d53c896b4`。
- 已有未提交修改全部保留：`README.md`、`docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md` 和三个 workspace cleanup 脚本。没有 reset、clean、checkout、覆盖式复制、删除、提交或推送。
- 本阶段新增 probe、staging、运行目录和矩阵均位于 `.build/`；固定快照、manifest、Aemeath/BaseLib DLL/PCK、正式游戏目录和 Mod 原有代码未修改。
- `git diff --check`：退出码 `0`。

## 固定输入和 canonical input

- 目标游戏：`0.111.0`，commit `41cef1ea`。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`。
- snapshot content SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- snapshot manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- Mod：Aemeath，Workshop ID `3747725225`，版本 `1.0.2`，精确变体 `3747725225/Aemeath.dll`，DLL SHA-256 `e7acf0e2440a9b5083c70909c4435e2151a3b4d415a6d34dcff26904d0b7f84e`。
- 依赖：`BaseLib@3.4.7`，DLL SHA-256 `55863ca6adc30a61a7544874e157a519ffe148ccbf2b18025947609c4bff813ce`。
- 程序集名：`Slay the spirite 2`；registration identity：`REG.AEMEATH.ModelDb#225@0.111.0`。
- canonical seed：`LAB_M2_3_AEMEATH_01`。
- worker：`SimulatorLab.NativeLifecycleProbe/Aemeath-M2-3`。
- 最终 adapter：`aemeath-m2-3-probe/v2-special-prerequisites`。
- collector：真实 Godot .NET 进程、`SimulatorLab.NativeLifecycleProbe`、`native-trace/v1`、`sentry-disabled-overlay/v1`。
- 最终 collector probe SHA-256：`8b224f4c5632613143b8527513ee80b0ba51979875c9e2a56689934b0dd46581`。
- canonical input digest：`ec2d11965ba116fc8e8ab68e6ba23c4c168243d56a15a99580bb3893c30e707f`。

原始 ModelDb inventory 为 `225` 个 identity：`132 Card`、`1 Character`、`59 Power`、`9 Relic`、`24 Other`。标准 content denominator 为前四类共 `201` 个；24 个 `Other` 仍保留在 raw inventory。

## 执行命令、退出码和证据

| 命令/批次 | 退出码 | 证据 |
| --- | ---: | --- |
| `dotnet build .build/diag/m2-3-aemeath-probe/M23AemeathProbe.csproj -c Release --no-restore --ignore-failed-sources -p:NuGetAudit=false` | 0 | 4 nullable warnings、0 errors；最终 probe 只进入 `.build` |
| `pwsh ... run-game.ps1 -RunKind primary` | 0 | `.build/runs/m2-3-aemeath-20261110-primary-final/` |
| `pwsh ... run-game.ps1 -RunKind repeat` | 0 | `.build/runs/m2-3-aemeath-20261110-repeat-final/` |
| `pwsh ... run-game.ps1 -RunKind replay -ReplayPath ...primary-final/game-output/route.json` | 0 | `.build/runs/m2-3-aemeath-20261110-replay-final/` |
| `pwsh ... run-game.ps1 -RunKind failure` | 0 | `.build/runs/m2-3-aemeath-20261110-failure-final/`，`BudgetExceeded` |
| 132 Card fresh-process final batch | 0 | `.build/runs/m2-3-aemeath-final-card-batch-20261110.json`，`132/132 Completed` |
| 59 Power + 9 Relic fresh-process final batch | 0 | `.build/runs/m2-3-aemeath-final-power-relic-batch-20261110.json`，`68/68 Completed` |
| 8 个特殊前置 Card probe | 0 | `.build/runs/m2-3-aemeath-special-card-*-r1/` 与 PourOut `r2` |
| AemeathStrike effect probe | 0 | `.build/runs/m2-3-aemeath-special-card-aemeath-strike-r2/`，目标 HP `44→38` |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| `dotnet build tools/mod-worker/ModWorker.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false` | 0 | 0 warning、0 error |
| `python .build/diag/m2-3-aemeath-probe/build-final-evidence.py` | 0 | 最终 aggregate matrix |

汇总入口：[contract-summary.json](../../../.build/runs/m2-3-aemeath-20261110/contract-summary.json)、[content-matrix.json](../../../.build/runs/m2-3-aemeath-20261110/content-matrix.json)、[lifecycle-matrix.json](../../../.build/runs/m2-3-aemeath-20261110/lifecycle-matrix.json)、[interaction-risk.json](../../../.build/runs/m2-3-aemeath-20261110/interaction-risk.json)、[evidence-index.json](../../../.build/runs/m2-3-aemeath-20261110/evidence-index.json)。

## 四路终态、digest 和错误分类

| 路径 | 终态 / stop | state digest | RNG digest | trace digest | route digest | raw exit |
| --- | --- | --- | --- | --- | --- | ---: |
| primary | `Death / Completed` | `fbdf0d4ec03bfd8bae32055a7a6bf9be0f394dde34a5eaa84eacef1faa7d23dc` | `406dd597146025900ab68399979241c0c0b2f279202922e97afc8208db7c9d54` | `ac700775650ab933c74078e780eeda8826d49171a6701621f50e88e0f414d3a0` | `37dc0f9a12361ab01952e390f7968cd2d00e5bc2b9802e89ce0aa6c287e24637` | 0 |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 0 |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 0 |
| failure | `Unknown / BudgetExceeded` | `d859cfcb1fed1d78ea9181f19f377fc6af7a047ecd52fc55ff0a2aa792b215ae` | `644beb7762de5fa415affe9ecd42af50043a270a0139e17f3037337b6c9e932d` | `f0d484ce451f670f8b67a9823f805ff8da7e0c7f24cee8362915f2cd1fc9124a` | `null` | 0 |

primary trace 见证真实 Aemeath 角色选择、起始 10-card deck、`BLACK_CAT_BLESSING`、首战 Victory、Gold/CardReward、跨房间、第二场 native Death 和 cleanup。

## Foundation、flow、content gate

- **Foundation gate：PARTIAL。** 共享 M2-0 foundation 的跨房间、callback/before-death 和 teardown 仍是全局缺口；没有用 Aemeath 专用 adapter 绕过它。
- **Flow gate：PASS。** primary/repeat/replay 均在完整角色流程后到达真实 native `Death`。
- **Content gate：PASS。** `201/201 Covered`，没有 Partial、Unsupported、Unrun 或 RuntimeFailure content unit。
- **Stability：PASS。** primary/repeat/replay 的 canonical input、state、RNG、trace、route digest 完全一致；failure 保留独立 `BudgetExceeded`。

## 内容覆盖和特殊前置证据

- **Character：** `CHARACTER.AEMEATH-AEMEATH` 的发现、构造、选择、初始状态、战斗、Death、cleanup Covered。
- **Card（132/132）：** 每张 Card 都有独立 fresh process 的 native play/effect/cleanup 证据。8 张原缺口通过真实前置闭合：
  - `BecomeStarlight`、`DontBeNaughty`、`LingeringEcho`：真实 `ChargeCounterPower` 充能后播放并消耗。
  - `FrequencyRecovery`：真实敌方 `ImplosionPower >= 2` 后定向播放。
  - `InvisibleWatch`：两个 native 玩家、真实 ally target 和敌方聚爆层数。
  - `PourOutAllStarlight`：四张真实 summon Card 创建弹珠后播放，真实移除弹珠并结束敌方战斗，终态为 `CardEffectCompleted`。
  - `SaveLoad`：真实把 Aemeath Power Card 放入手牌，CardSelect 选择并消耗，再完成卡牌效果。
  - `StarlightBreaksThrough`：真实弹珠选择、变换、返回手牌。
- **Power（59/59）：** 真实 apply、实例状态、remove 和 cleanup。
- **Relic（9/9）：** 真实 obtain、实例状态和 remove。
- **Summon/orb：** 真实 summon、弹珠集合操作、变换和移除已由最终 Card batch 与两个特殊 Card probe 覆盖。
- **Lifecycle matrix：** [lifecycle-matrix.json](../../../.build/runs/m2-3-aemeath-20261110/lifecycle-matrix.json)。
- **Interaction risk：** [interaction-risk.json](../../../.build/runs/m2-3-aemeath-20261110/interaction-risk.json)。所有 gameplay interaction contract 均为 `Covered`；UI/视觉、visible-policy、正式 save/load、map/network/Steam/cloud 仍单列为 platform capability。

## 平台能力和剩余边界

Aemeath 自定义攻击在 headless 进程仍会记录 `AemeathActions.SingleHitDamageDelay` 的视觉节点 `NullReferenceException`；专用 Card probe 证明真实攻击效果已生效，故该错误不再降低 content gate，但保留为 platform capability 缺口。visible-policy、UI presentation、正式 save/load、map presentation、network replay、Steam/cloud 没有被 gameplay gate 覆盖。

最终分母：content `Covered=201 / Partial=0 / Unsupported=0 / Unrun=0 / RuntimeFailure=0`；run cohort `Death=3`、`RunVictory=0`、`BudgetExceeded=1`。全局 foundation 仍为 `PARTIAL`。

## 验收标准逐项结果

| 验收标准 | 结果 |
| --- | --- |
| Aemeath identity 和 initial state | 通过 |
| 真实 RunVictory 或 Death | 通过；三路真实 native Death |
| 战斗、奖励、推进、清理 | 通过 |
| primary/repeat/replay/failure | 通过 |
| 全部 content lifecycle 和 interaction | **通过；201/201 Covered** |
| Aemeath ticket acceptance | **PASS** |
| global foundation gate | PARTIAL，独立于本票内容闭合 |
| UI/save/map/network/Steam/cloud/visible-policy | 独立 platform capability 缺口 |

## 下一阶段唯一建议入口

先闭合全局 M2-0 foundation gate；Aemeath 本票的 content gate 已不再有内容缺口，不应重新降级或用 platform capability 缺口改写内容结果。

## 是否提交或推送

否。没有提交、推送或创建远程 PR。
