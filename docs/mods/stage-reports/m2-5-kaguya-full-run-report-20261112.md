# M2-5 Kaguya 全流程交接报告

日期：2026-11-12

## 阶段状态

**PARTIAL**。

Kaguya 的 `MixedSoldiersEliteEncounter` 和 `BattleSoldierA` 在当前 `0.111.0` 原生新进程中被真实注册、进入战斗、观察到敌人意图与行为，并在该内容参与后到达真实 native `Death`。独立 `primary`、`repeat`、`replay` 稳定一致，`failure` 保留为 `Unknown / BudgetExceeded`。

当前受控 profile 的 639 个静态声明模型类型中，运行时登记 220 个。当前选择的事件内容 `TaketoriMonogatariEvent` 和奖励 `REWARD.KAGUYA_TAKETORI_OPTION` 具有当前构建的完整事件、结果、资源账本、清理和 teardown 证据。Card/Relic 及其它未进入选定生命周期的内容逐项保留为 `Unsupported` 或 `Unrun`，没有提升为 Covered。

## 票据、分支和工作区

- 子票：#11，开工前确认 `OPEN`、`ready-for-agent`；阻塞票 #9、#10 均为 `CLOSED`，本阶段已认领 #11。
- 分支：`main`。
- HEAD：`9f6a6fc9c37c2c895777ea48d50910cd46be7f98`。
- 现有用户修改均保留。`README.md`、`docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md` 以及既有阶段报告和维护脚本没有被覆盖、重置或删除。
- 本阶段新增或修改的跟踪文件：
  - `tools/native-run/Program.cs`
  - `tools/native-run/KaguyaNativeModLoop.cs`
  - `tools/native-run/KaguyaNativeEncounter.cs`
  - `tools/validation/BuildKaguyaM25Evidence.ps1`
  - `tools/validation/TestKaguyaM25.ps1`
  - 本报告
- `src/SimulatorLab.Core` 未修改，核心没有引用 Mod、Godot 或外部 DTO。
- 未提交、未推送、未创建远程 PR。

## 固定输入和隔离边界

| 输入 | 值 |
| --- | --- |
| 游戏 | `0.111.0`，commit `41cef1ea` |
| 固定快照 | `local-mods/workshop-snapshot-20261106-163352` |
| 快照内容摘要 | `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73` |
| manifest SHA-256 | `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3` |
| Kaguya | Workshop `3747548629`，版本 `3.2.4` |
| Kaguya.json SHA-256 | `0961dc7c5e98235ebfc40220bcebb3735ee57ec55bff078adb9c77742c458532` |
| Kaguya.dll SHA-256 | `f565b7232975212e59088bebc2e2ef169c78f0c5ef36e0f698807f9c9942abce` |
| Kaguya.pck SHA-256 | `f60ba6e6975245de6c8ed98aa0c69a92864fe608f15329b7c8f17d246ffdcb8a` |
| 依赖闭包 | `BaseLib@3.4.7 -> Kaguya@3.2.4` |
| registration identity | `REG.KAGUYA.ENCOUNTER.MIXED_SOLDIERS_ELITE_ENCOUNTER@0.111.0`、`REG.KAGUYA.MONSTER.BATTLE_SOLDIER_A@0.111.0` |
| canonical seed | `LAB_M2_5_KAGUYA_01` |
| canonical input digest | `a9118c4ea2081d5dd575731ccc16765c6a11978f609031fe0060514d1b433c42` |
| worker | `SimulatorLab.NativeLifecycleProbe/Kaguya-M2-5` |
| adapter | `kaguya-m2-5-native-encounter/v1` |
| collector | `isolated NativeRun/GameProbe; native-trace/v1` |
| build | `.build/native-build/kaguya-m2-5-20261112-r4`，build ID `215971AC55BE33BB39BD2FB42AB169A4ECBAFCB1CE6979B67E28E4C811E335F1` |

固定快照、Kaguya/BaseLib DLL、PCK、JSON 和固定 manifest 均保持只读。所有运行输出位于新的 `.build/runs` 子目录，没有把 Mod 二进制复制到聚合证据目录。

## 选定内容表面

- 自定义遭遇：`ENCOUNTER.MIXED_SOLDIERS_ELITE_ENCOUNTER`，类型 `Kaguya.Encounters.MixedSoldiersEliteEncounter`。
- 自定义敌人：`MONSTER.BATTLE_SOLDIER_A`，类型 `Kaguya.Monsters.BattleSoldierA`。
- 当前构建事件证据：`EVENT.TAKETORI_MONOGATARI_EVENT`，类型 `Kaguya.Events.TaketoriMonogatariEvent`。
- 当前构建奖励证据：`REWARD.KAGUYA_TAKETORI_OPTION`。

原生 trace 记录了 `new-run`、`kaguya-encounter-entered`、`kaguya-enemy-intent`、`kaguya-enemy-behavior`、`death` 和 `kaguya-death-boundary`。事件 trace 记录了 `kaguya-event-entered`、`kaguya-event-option`、`kaguya-event-result`、`kaguya-resource-ledger`、`kaguya-cleanup` 和 `kaguya-teardown`。

## Primary、Repeat、Replay、Failure

最终 cohort 目录：`.build/runs/m2-5-kaguya-20261112-primary-final`、`repeat-final`、`replay-final`、`failure-final`。

| 路径 | 终态 / stop | raw exit | state digest | RNG digest | trace digest | route digest | choice digest |
| --- | --- | ---: | --- | --- | --- | --- | --- |
| primary | `Death / Completed` | 0 | `eb24500efb68aef37afc15ed992697692583bbb90016425821da977586bc7787` | `02ce18c320b8af2e0510741361389acfa2b01309b55b311a7b318be56357828c` | `5312426f0426e829fc8a5d4669e4a3571abbb5d90255442c038c482e4addb85c` | `2ada1edc202f683c0bb49a92c9696c716b93f8420f83ae62af8449f767fe4337` | `7fd4e016371de497a92597d81b13a6f06d9ce54ae1e7b084efa4e3e11d1db4ef` |
| repeat | `Death / Completed` | 0 | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 |
| replay | `Death / Completed` | 0 | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 |
| failure | `Unknown / BudgetExceeded` | 3 | `d637ea856d25f5f45c2ffb58b5e44f2f107284203a4afc7d2407ffb872a26a28` | `eef3af9e01ae67f24878e6d61c5eea6375ab9d379bae33cdeca85d2e9124c54b` | `18ef895122cf4ee85cb7c342097bea38bcb46ebc3d3129fe4c6e083ea8edef74` | 无 | `4f53cda18c2baa0c0354bb5f9a3ecbe5ed12ab4d8e11ba873c2f11161202b945` |

Replay 的 canonical input digest 因 replay 场景和输入路径字段不同为 `6ee50ca4aff99e1918b0378af1906941e4019583f0e6e4941cc1b191d0ebba3b`，完整 state、RNG、trace、route 和 choice digest 均匹配。Failure 没有产生 `Death` 或 `RunVictory`。

事件内容单独的当前构建 probe 位于 `.build/runs/m2-5-kaguya-20261112-event-primary-final`，终态为 `Ongoing / SliceBoundary`，probe digest 为 `ff10b0b77b276e03d8770519926a5e694f08ac03a55c631743eb6bc667cdde69`。它只作为事件、奖励、账本和清理证据，不冒充 full-run 终态。

## Foundation、内容、生命周期和交互矩阵

- Foundation gate：`PASS`。当前 Kaguya profile 只接受 exact `BaseLib@3.4.7` 闭包，未使用单 Mod 特判绕过依赖加载。全局共享 foundation 语义仍沿用 M2-4 已接受的边界。
- Flow gate：`PASS`。primary、repeat、replay 均在自定义遭遇参与后到达真实 native `Death`。
- Stability gate：`PASS`。三路成功 cohort 的 state、RNG、trace、route、choice digest 完全一致。
- Content gate：`PARTIAL`。完整机器矩阵见 `.build/runs/m2-5-kaguya-20261112/content-matrix.json`，生命周期见 `lifecycle-matrix.json`，交互风险见 `interaction-risk.json`。

内容分母为 640，包含静态声明的 639 个模型类型和当前事件路径追加发现的 1 个奖励身份。

| 状态 | 数量 |
| --- | ---: |
| Covered | 2 |
| Partial | 2 |
| Unsupported | 419 |
| Unrun | 217 |
| RuntimeFailure | 0 |

`Covered=2` 为当前事件和事件奖励。`Partial=2` 为自定义遭遇和 `BattleSoldierA`，它们在真实死亡路径上完成进入、意图和行为观察，但没有到达奖励和胜利清理边界。运行时登记但没有进入生命周期的身份保留为 `Unrun`。未进入 scoped profile 的声明类型保留为 `Unsupported`。一次扩大 Card/Relic 注册尝试触发了真实 `DuplicateModelException`，证据保留在 `.build/runs/m2-5-kaguya-20261112-r3-event-primary/result.json`，没有把它改写成缺失或成功。

## 平台能力矩阵

平台能力见 `.build/runs/m2-5-kaguya-20261112/platform-capabilities.json`。

- Headless native execution：`Covered`。
- Audio：`Unrun`，本票没有独立音频策略声明。
- UI rendering、map presentation、正式 save/load、network、Steam、cloud：`Unsupported`。
- visible-policy：`Unsupported`。本票使用原生内部状态来证明 execution compatibility，没有把它升级为 visible-policy compatibility。

## 执行命令和退出码

| 命令 | 退出码 | 证据 |
| --- | ---: | --- |
| `build-native-run.ps1 ... -OutputRoot .build/native-build/kaguya-m2-5-20261112-r4` | 0 | NativeRun build，3 个既有 warning、0 error |
| `run-native-slice.ps1 ... -Scenario kaguya-m2-5 -RunKind primary` | 0 | primary-final |
| `run-native-slice.ps1 ... -Scenario kaguya-m2-5 -RunKind repeat` | 0 | repeat-final |
| `run-native-slice.ps1 ... -Scenario kaguya-m2-5-replay -RunKind replay` | 0 | replay-final |
| `run-native-slice.ps1 ... -Control budget -RunKind failure` | 3 | failure-final，预期 BudgetExceeded |
| `run-native-slice.ps1 ... -Scenario kaguya-stage8` | 0 | event-primary-final |
| `BuildKaguyaM25Evidence.ps1` | 0 | 聚合矩阵和 evidence-index |
| `TestKaguyaM25.ps1` | 0 | `PASS Kaguya M2-5 ... replayExact=true` |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| `dotnet build tools/mod-worker/KaguyaStage8.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false` | 0 | 0 warning、0 error |
| `TestKaguyaStage8.ps1` | 0 | 既有 Stage 8 event/reward 回归通过 |
| `git diff --check` | 0 | 无新增 whitespace error |

## 验收标准逐项结果

| 验收标准 | 结果 |
| --- | --- |
| 自定义内容身份实际见证 | 通过；遭遇、敌人、事件和奖励身份均有当前构建证据 |
| 真实 RunVictory 或 Death | 通过；三路成功 cohort 均为真实 native Death |
| 所有冻结内容单元有生命周期证据或明确状态 | 通过；640 行矩阵逐项为 Covered、Partial、Unsupported 或 Unrun |
| primary、repeat、replay、failure 稳定 | 通过；成功 digest 全匹配，failure 为独立 BudgetExceeded |
| foundation gate | 通过；exact BaseLib 3.4.7 闭包，未使用单 Mod 绕过 |
| content gate | 部分；当前 content gate 为 PARTIAL，未注册 Card/Relic 和其它未触发语义已保留状态 |
| 固定输入只读 | 通过；固定快照、manifest、Kaguya/BaseLib 二进制和正式游戏输入未修改 |
| UI、地图、保存、网络、Steam、cloud、visible-policy | 明确缺口；单列平台能力和策略能力状态 |

## 已知缺口和下一阶段唯一建议入口

当前未覆盖 Kaguya Card/Relic 实例化、Card/Relic effect、其它敌人和遭遇的真实战斗、Boss/第四幕、多角色切换、跨房间内容持久化、正式 save/load、UI/map presentation、network、Steam/cloud 和 visible-policy。`Partial=2`、`Unsupported=419`、`Unrun=217`、`RuntimeFailure=0` 已写入机器矩阵，没有把这些缺口从分母中删除。

下一阶段唯一建议入口：先建立一个不触发 `CARD.CLASH` 重复身份的通用 exact registration seam，再只选择一个 Kaguya Card/Relic 内容单元做真实 lifecycle probe，并在保持当前 Death cohort 不变的前提下重新验证 content matrix。不要扩大到第二个复杂 Mod，也不要把完整第四幕作为默认前置。

是否提交或推送：**否**。
