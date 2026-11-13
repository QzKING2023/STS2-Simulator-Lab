# M2-3 Ryoshu 全流程交接报告

日期：2026-11-10

## 阶段状态

**BLOCKED**。

Ryoshu 在固定 `0.111.0` 原生进程中完成真实 DLL/PCK 加载、ModelDb 注册、角色身份发现、角色选择、初始状态、Neow 处理、首个地图点进入和首战进入。第一次合法尝试起始攻击牌 `CARD.RYOSHU_STRIKE` 时，原始 Mod 的 `RyoshuMod.RyoshuStrike.OnPlay` 触发：

```text
System.MissingMethodException:
MegaCrit.Sts2.Core.Commands.Builders.AttackCommand
MegaCrit.Sts2.Core.Commands.Builders.AttackCommand.FromCard(
  MegaCrit.Sts2.Core.Models.CardModel)
```

这是固定游戏 `0.111.0` 与 Ryoshu `0.3.6` 已编译 API 的真实运行时不匹配。没有使用原版卡替代、伤害注入、运行时补丁、回调跳过或假 DTO。由于首战未完成，奖励、跨房间推进、真实 Victory/Death、终态清理和全部内容生命周期不能安全声称通过；保留为 `RuntimeFailure`/`Unrun`，阶段停止在该边界。

## 票据、分支和工作区

- 子票：[#8](https://github.com/QzKING2023/STS2-Simulator-Lab/issues/8)，开始前确认为 `OPEN`、`ready-for-agent`；唯一阻塞票 #4 已 `CLOSED`；已按仓库协议认领 #8，当前仍保持 OPEN，因为验收边界被真实 RuntimeFailure 阻塞。
- 分支：`main`；HEAD：`4cae8b4cf90d1be358619bdbf5a9e79d53c896b4`。
- 开始前已有未提交修改全部保留：`README.md`、`docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md`、三个 workspace cleanup 脚本，以及已有的 Watcher/Aemeath/figure_Saya 阶段报告；没有 reset、clean、checkout、覆盖或删除。
- 本阶段新增的持久化报告为本文件；probe、运行器、运行目录和矩阵均在 `.build/` 下。`.build/` 证据入口：[m2-3-ryoshu-20261110](../../../.build/runs/m2-3-ryoshu-20261110/evidence-index.json)。
- 没有提交、推送、重写历史或创建远程 PR。

## 固定输入和能力矩阵

- 目标游戏：`0.111.0`，commit `41cef1ea`。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`；目录摘要 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`；142 个文件、`6,974,558,103` bytes。
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- Mod：Ryoshu，Workshop ID `3747528329`，版本 `0.3.6`，精确输入 `3747528329/Ryoshu.dll`。
- Ryoshu DLL SHA-256：`0ff08a3187df4969a6d4ecc9a3c79e28bbb6ab51ba6feb7bc0cc6ffb2605de9a`；PCK SHA-256：`91c699a8d79d0b3d17c5b01470165d12370ab2ac96144d0428afbb0770205210`。
- 没有外部依赖声明；canonical dependency closure 记录为 `Ryoshu@0.3.6 (no external dependency declared)`。没有可选择的 `lib/0.111.0` 依赖目录，未静默回退其他游戏版本。
- registration identity：`REG.RYOSHU.assembly#1@0.111.0`。
- canonical seed：`LAB_M2_3_RYOSHU_01`；四路共用 input digest：`da8880d113f0c9baf44fd2bc98a52d4b72702875867b110b91466ed317110995`。
- worker：`SimulatorLab.NativeLifecycleProbe/Ryoshu-M2-3`；adapter：`ryoshu-m2-3-probe/v1-no-runtime-patch`；collector：真实 Godot .NET 进程 + `SimulatorLab.NativeLifecycleProbe`、`native-trace/v1`、`sentry-disabled-overlay/v1`。
- native inventory：`Character=1`、`Card=107`、`Power=35`、`Relic=13`、`Other=4`，原始总数 160，标准内容分母 156；inventory digest `986f0a59d2e3d26cea91803f9c8c9d351a0f96e2e404454f7065d55fae77b554`。
- 真实角色身份：`CHARACTER.RYOSHU` (`RyoshuMod.Ryoshu`)；75 HP；12 张起始牌：5×`CARD.RYOSHU_STRIKE`、5×`CARD.RYOSHU_DEFEND`、`CARD.SACRIFICE_MEMORY`、`CARD.RECLAIM_MEMORY`；起始遗物 `RELIC.TIANSHA_STAR_BLADE_ARAYASHIKI`。
- PCK 在原生进程中真实加载；Godot 日志记录了 38 个原始 Harmony patch 应用、角色本地化表合并和 Ryoshu 资源发现。能力矩阵只引用真实 native inventory、trace 和 Godot 日志；Mod DLL/PCK/资源未修改。
- Watcher 的负 canonical-cost token Card 不适用于 Ryoshu；没有把该项混入本 Mod 分母。

能力矩阵和 native 证据见：[capability-matrix.json](../../../.build/runs/m2-3-ryoshu-20261110/capability-matrix.json)。

## 执行命令、退出码和证据

| 命令/批次 | 退出码 | 证据 |
| --- | ---: | --- |
| `. .\tools\maintenance\Set-GitHubSession.ps1; gh issue view 8 --json number,title,body,labels,state` | 0 | #8 OPEN、`ready-for-agent` |
| `. .\tools\maintenance\Set-GitHubSession.ps1; gh issue view 4 --json number,title,body,labels,state` | 0 | #4 CLOSED |
| `dotnet restore .build/diag/m2-3-ryoshu-probe/M23RyoshuProbe.csproj --ignore-failed-sources -p:NuGetAudit=false` | 0 | `.build/diag/m2-3-ryoshu-probe/obj/project.assets.json` |
| `dotnet build .build/diag/m2-3-ryoshu-probe/M23RyoshuProbe.csproj -c Release --no-restore --ignore-failed-sources -p:NuGetAudit=false` | 0 | 3 nullable warnings、0 errors；probe DLL 在 `.build/diag/m2-3-ryoshu-probe/bin/Release/net9.0/` |
| `prepare-root.ps1 -Suffix '-r5'` | 0 | `.build/native-game/ryoshu-m2-3-20261110-r5/`；仅使用 hardlink overlay，固定输入未写入 |
| `run-game.ps1 -RunKind primary` | 0 | `.build/runs/m2-3-ryoshu-20261110-primary-r7/` |
| `run-game.ps1 -RunKind repeat` | 0 | `.build/runs/m2-3-ryoshu-20261110-repeat-r7/` |
| `run-game.ps1 -RunKind replay -ReplayPath ...primary-r7/game-output/route.json` | 0 | `.build/runs/m2-3-ryoshu-20261110-replay-r7/`；引用 route 不存在，因为 primary 在首战失败 |
| `run-game.ps1 -RunKind failure` | 0 | `.build/runs/m2-3-ryoshu-20261110-failure-r7/`；受控 `BudgetExceeded` |
| `python .build/diag/m2-3-ryoshu-probe/build-evidence.py` | 0 | `.build/runs/m2-3-ryoshu-20261110/` 汇总矩阵 |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| `git diff --check` | 0 | 无新增 whitespace error；仅保留仓库既有换行提示 |

## primary/repeat/replay/failure 终态和 digest

四路均为新进程；所有 canonical input digest 相同。RuntimeFailure 的 `stateDigest` 是失败 envelope 的摘要，`completeStateEvidence=false`、`completeRngEvidence=false`；不能把它当作完整终态状态/RNG 证明。

| 路径 | 终态 / stop | error classification | state digest | RNG digest | trace digest | route digest |
| --- | --- | --- | --- | --- | --- | --- |
| primary | `Unknown / RuntimeFailure` | `MissingMethodException` | `28141ef485ee8f9d358b34f9a57467ea28c2337017a29151b622f10400428ccb` | — | `f7255b2f5c9096673c17d6d37104683a87f30cca01af0262a57864e3241ec17c` | — |
| repeat | `Unknown / RuntimeFailure` | `MissingMethodException` | 同 primary | — | 同 primary | — |
| replay | `Unknown / RuntimeFailure` | `MissingMethodException` | 同 primary | — | 同 primary | — |
| failure | `Unknown / BudgetExceeded` | `BudgetExceededException` | `2cdb2c44ad66a2216492d1428eb28d8c67497dd2d0c489caedfc2bcc99ee2f34` | — | `2064b3d44c6555375f50121ab18f18740de4ebcae680d9f23c5d645ff643e8ff` | — |

primary/repeat/replay trace 都保留：`role-selected`、`initial-state`、`room-opening-complete`、首个 `map-transition`、`combat-enter`、尝试播放 `CARD.RYOSHU_STRIKE`、`runtime-failure`。failure trace 在相同首战入口保留受控 `budget-stop`，没有伪造 Death/Victory。

## foundation、flow、content gate

- **Foundation gate：PARTIAL。** M2-0 共享 BaseLib/RitsuLib/MinionLib 的跨房间、callback/before-death、registry 和 teardown 语义仍未全局闭合；本阶段没有用 Ryoshu 特判绕过。Ryoshu 额外暴露了游戏 API 版本不匹配，属于真实 RuntimeFailure。
- **Flow gate：BLOCKED。** 真实角色和首战入口已观察，但首张合法攻击牌的原始 `OnPlay` 在 effect-resolution 前失败；没有真实 native `RunVictory` 或 `Death`。
- **Content gate：BLOCKED。** 156 个标准 content unit 的状态为 `Covered=0`、`Partial=1`、`RuntimeFailure=1`、`Unrun=154`、`Unsupported=0`。Character 只到角色选择/初始状态/首战入口；`CARD.RYOSHU_STRIKE` 的 activation 已真实触发但 RuntimeFailure；其余 154 个单元只完成发现。
- **Stability：PARTIAL。** primary/repeat/replay 的 failure boundary、input、state envelope 和 trace digest 一致；由于没有完整状态/RNG/route，Replay 不能被报告为通过。failure 独立保持 `BudgetExceeded`。

完整矩阵入口：[content-matrix.json](../../../.build/runs/m2-3-ryoshu-20261110/content-matrix.json)、[lifecycle-matrix.json](../../../.build/runs/m2-3-ryoshu-20261110/lifecycle-matrix.json)、[interaction-risk.json](../../../.build/runs/m2-3-ryoshu-20261110/interaction-risk.json)、[contract-summary.json](../../../.build/runs/m2-3-ryoshu-20261110/contract-summary.json)。

## lifecycle matrix、interaction risk 和平台能力

已覆盖的生命周期检查点：

- DLL/PCK discovery、Mod initialization、ModelDb registration；
- Character construction、`CHARACTER.RYOSHU` 选择、12-card starting state、75 HP、starting relic；
- Neow legal option、opening map transition、首个 Monster combat entry；
- `RyoshuStrike` native card activation attempt and exact callback failure。

未覆盖或保留缺口：

- 首战胜利、reward offer/selection、deck persistence、跨房间 secondary state；
- 其余 106 Cards 的构造/target/effect/cleanup；35 Powers 和 13 Relics 的 apply/trigger/remove；
- native Victory/Death、before-death、combat cleanup、registry/ALC teardown；
- 正式 UI、map presentation、save/load、network replay、Steam 和 cloud；
- visible-policy compatibility。collector 使用 TestMode/internal state，只证明 execution boundary，不能证明玩家可见策略兼容。

交互风险表将注册/初始化和角色初始身份列为 `Covered`，首张攻击牌列为 `RuntimeFailure`，其余卡牌/奖励/跨房间/Power/Relic/terminal/cleanup 列为 `Unrun`；共享 foundation 为 `Partial`；visible-policy 与正式平台承载能力列为 `Unsupported`。

## 验收标准逐项结果

| 验收标准 | 结果 |
| --- | --- |
| Ryoshu identity 和 initial state 在 native lifecycle 中见证 | **部分通过**；`CHARACTER.RYOSHU`、起始牌、75 HP、起始遗物和首战入口均已见证 |
| 真实 RunVictory 或 Death、无 hidden stop | **未通过**；首张真实攻击牌触发 `RuntimeFailure`，没有 native terminal |
| Content lifecycle 和 interaction evidence 对冻结 inventory 完整 | **未通过**；156 个单元为 `Partial=1 / RuntimeFailure=1 / Unrun=154` |
| Primary、Repeat、Replay、Failure | **部分通过**；四路新进程和稳定 failure boundary 已记录，Replay 没有完整 state/RNG/route 可通过 |
| fixed snapshot、manifest、exact 0.111.0 input 只读 | **通过**；原始 DLL/PCK/manifest 未修改，runtime root 只用 hardlink overlay |
| foundation gate | **部分**；共享 foundation 仍为 `PARTIAL`，没有 Mod-specific 绕过 |
| UI/save/map/network/Steam/cloud | **明确平台缺口**；单独列为 `Unsupported`，未计入 gameplay 通过 |

## 已知缺口、分母和未覆盖语义

- Content denominator：156（Character 1、Card 107、Power 35、Relic 13）。raw `Other=4` 保留在 inventory，不计入标准分母。
- Content states：`Covered=0`、`Partial=1`、`Unrun=154`、`Unsupported=0`、`RuntimeFailure=1`。
- Run states：`primary=RuntimeFailure`、`repeat=RuntimeFailure`、`replay=RuntimeFailure`、`failure=BudgetExceeded`；`Victory=0`、`Death=0`。
- Foundation denominator：共享 gate `PARTIAL`；Ryoshu 只证明 BaseLib/Mod registration 的窄路径，未覆盖全局 RitsuLib/MinionLib 语义。
- RuntimeFailure 分母只包括真实尝试的 `CARD.RYOSHU_STRIKE`，没有把未尝试内容猜测为同一错误；其余内容保留 `Unrun`。
- Unsupported 分母（content unit）为 0；平台能力 Unsupported 包括 visible-policy、UI、正式 save/load、map presentation、network replay、Steam/cloud。
- 没有覆盖 `AttackCommand.FromCard(CardModel)` 的泛用兼容实现，因为这会要求改变原始 Mod/运行时语义或运行时补丁；没有新增单 Mod adapter。

## 下一阶段唯一建议入口

下一阶段唯一建议入口：在共享 foundation/游戏版本兼容边界建立一个跨 Mod、可审计的 `AttackCommand` 旧签名兼容 seam，并先用 RyoshuStrike 与至少一个其他内容族 fixture 证明真实 callback、effect、failure cleanup 和 ALC teardown；在该 foundation gate 关闭前，不继续 Ryoshu 其余 155 个单元，也不接入其他角色。

是否提交或推送：**否**。



## 用户续作：blocker-resolution 复核

用户要求消除全部 blocker 并把 156 个标准内容单元提升为 `Covered`。本次续作先在不可变输入边界内复核了可行的泛用兼容 seam：

- 固定 `0.111.0` 的全部 31 份本地 `sts2.dll` 副本均为同一 SHA-256：`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`。
- 反射读取 `MegaCrit.Sts2.Core.Commands.Builders.AttackCommand` 只发现一个 `FromCard`：`FromCard(CardModel, CardPlay)`；没有 `FromCard(CardModel)`。
- Ryoshu 的四路新进程在原始 `RyoshuStrike.OnPlay` 中稳定请求单参数签名并得到 `MissingMethodException`。API 证据见 [attack-command-api.json](../../../.build/runs/m2-3-ryoshu-20261110/attack-command-api.json)。
- 固定快照中只有一个 Ryoshu DLL/PCK/manifest 变体；没有可切换的精确 `0.111.0` 兼容程序集。
- 普通外部 adapter、C# extension method 或 probe 侧 wrapper 无法满足已编译进 `Ryoshu.dll` 的 CLR 成员引用。能够满足它的方案只能是修改/替换 `sts2.dll`、重写 Ryoshu 二进制，或运行时改写方法/调用点；这些动作都违反本阶段共同边界。

因此本次复核没有伪造成功，也没有把 154 个 `Unrun` 内容单元改成 Unsupported 或 Covered。flow/content gate 仍为 `BLOCKED`，#8 继续保持 OPEN；只有获得一个由项目维护者批准、跨 Mod 可复用且不改变原始 Mod 语义的精确 API 兼容输入后，才可继续真实 reward、跨房间、terminal、cleanup 和逐单元内容覆盖。
