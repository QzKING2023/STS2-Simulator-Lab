# M2-4 Remilia 全流程交接报告 — 2026-11-11

## 阶段状态

**PASS**。Remilia 在固定 0.111.0 游戏和固定 Workshop 快照下完成了 exact RitsuLib compat/0.111.0 加载、真实注册发现、RitsuLib 战斗通知 seam、完整 full-run、四路稳定性和全部标准内容单元生命周期。

原生音频已关闭。每条最终 full-run 和内容 probe 都使用 --audio-driver Dummy，并在 probe 初始化阶段将 Master 与 SFX 总线设为静音。最终证据中的 audio-policy.json 都报告 muted=true。

## 票据、分支和工作区

- 子票 #9 在开始时为 OPEN，标签为 ready-for-agent，#5、#6、#7、#8 已关闭。本阶段已认领 #9。
- 分支 main，HEAD 052a458e0a89384e75ada5a826e5c34df56fdbd8。
- 既有用户修改全部保留。未执行 reset、clean、checkout、删除、提交或推送。
- 新增阶段报告 [m2-4-remilia-full-run-report-20261111.md](/D:/STS2-Simulator-Lab/docs/mods/stage-reports/m2-4-remilia-full-run-report-20261111.md)。
- .build/runs/m2-4-remilia-20261111/ 保存运行器、worker、runtime overlay、四路 full-run、120 个内容进程和机器可读矩阵。
- tools/native-run/run-native-slice.ps1 已加入 --audio-driver Dummy，后续 headless Godot slice 默认使用静音音频驱动。
- 固定快照、Remilia/RitsuLib DLL/PCK、正式游戏目录、正式存档/设置和 SimulatorLab.Core 未修改。
- git diff --check 退出码 0。

## 固定输入和 canonical input

| 输入 | 值 |
|---|---|
| 游戏 | 0.111.0，commit 41cef1ea |
| 游戏 DLL | 0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9 |
| 快照 | local-mods/workshop-snapshot-20261106-163352 |
| 快照摘要 | 7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73 |
| manifest 摘要 | 09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3 |
| Remilia | Workshop 3747515571，版本 v1.0.3 |
| RitsuLib | 0.6.10，exact 3747602295/compat/0.111.0 |
| 依赖顺序 | STS2-RitsuLib → Remilia → isolated probe |
| registration identity | REG.Remilia.lib/0.111.0#1@0.111.0 |
| canonical seed | LAB_M2_4_REMILIA_01 |
| canonical digest | ef41019571494829f9d86133aad8edd109a39ad313cf2af6df367b16e4f8dabc |
| worker | SimulatorLab.NativeLifecycleProbe/generic-v1 |
| adapter | native-lifecycle-configured-owner/v3-combat-notification-silent-audio-native-resource-preconditions |
| collector | godot-native-observed-state-rng-live-trace/v3 |

固定快照逐文件 142 项审计通过，大小为 6,974,558,103 bytes。输入审计见 [snapshot-audit.json](/D:/STS2-Simulator-Lab/.build/runs/m2-4-remilia-20261111/snapshot-audit.json) 和 [runtime-input-audit.json](/D:/STS2-Simulator-Lab/.build/runs/m2-4-remilia-20261111/runtime-input-audit.json)。

## 依赖顺序、加载身份和 RitsuLib 语义

load-identity.json 记录三项 ModManager 状态均为 Loaded。RitsuLib assemblies 均来自 exact compat/0.111.0 路径。日志观察到 15/15 framework dynamic patches、432 个 Core patches、ContentAssets、CharacterAssets、ContentRegistry、Persistence 和 Unlocks patch groups。Remilia ModelDb 注册清单为 Character=1、Card=88、Power=24、Relic=8、Other=3。

Remilia full-run 使用了通用 CombatStateChanged 延迟 seam。该 seam 只作用于 native CombatStateTracker 的 TestMode 回调时序，没有修改 Remilia 原始代码、游戏 DLL 或快照输入。既有 exact-closure foundation fixture 也观察到 RitsuLib reward callback、registry clear、observer detach 和 runtime ALC teardown。

## Primary、Repeat、Replay、Failure

| 路径 | 终态 / stop | raw exit | state digest | RNG digest | trace digest | route digest | replay |
|---|---|---:|---|---|---|---|---|
| primary | Death / Completed | 0 | 5f1f0e78974da95b21865421f3def1bfa829b2732ad069d36f7aa3e864cf6261 | 47fc62de48042f6233c67e6e018d9a9c32f46579bc094e18b436406d8034082a | ffb83646b48ec0c83bcfcb896f3c7d7981dcb40728e5ad627f2d60acc2e2fe50 | 79221b2b4512c86a24d3cc8218673d17a9fe51febc23c0ce5dabb2fd4a05b0bd | — |
| repeat | Death / Completed | 0 | same as primary | same as primary | same as primary | same as primary | — |
| replay | Death / Completed | 0 | same as primary | same as primary | same as primary | same as primary | matched=true |
| failure | Unknown / BudgetExceeded | 0 | 64ac4945d476af1bffdfcb1023e97344671c79c7536833fae4abd3b5307bb5c0 | 47fc62de48042f6233c67e6e018d9a9c32f46579bc094e18b436406d8034082a | d727f8b6f4bff2bbc594accf49bdf63c6185a5c4dec0d05994597e163723b319 | — | — |

Primary、Repeat、Replay 都包含完整 state、RNG 和 trace。Replay route digest 完全匹配。Failure 在首场战斗入口前按声明预算停止，没有产生伪终态。

## 内容单元和生命周期矩阵

标准分母为 121 个单元。

- Character=1
- Card=88
- Power=24
- Relic=8
- Other=3 保留在 raw inventory，属于非标准内容表面

最终状态为 Covered=121、Partial=0、Unsupported=0、Unrun=0、RuntimeFailure=0。

120 个 Card、Power 和 Relic 内容进程均以 ContentLifecycleCompleted / Completed 结束。四个带有 BloodCost 或高能量前置的卡牌通过真实 native secondary-resource 和 energy command 前置后完成 play、effect 和 cleanup。没有将空实现或原版替代行为写入结果。

机器可读证据见 [content-matrix.json](/D:/STS2-Simulator-Lab/.build/runs/m2-4-remilia-20261111/content-matrix.json)、[lifecycle-matrix.json](/D:/STS2-Simulator-Lab/.build/runs/m2-4-remilia-20261111/lifecycle-matrix.json)、[content-run-summary.json](/D:/STS2-Simulator-Lab/.build/runs/m2-4-remilia-20261111/content-run-summary.json)。

## Foundation、flow、content、stability 和平台能力

- Foundation gate 为 PASS，沿用 generic exact-closure foundation fixture，并在 Remilia reward callback boundary 复核。
- Flow gate 为 PASS，primary、repeat、replay 均到达真实 native Death。
- Content gate 为 PASS，121 个标准内容单元全部 Covered。
- Stability gate 为 PASS，primary、repeat、replay 的 canonical、state、RNG、trace、route digest 一致。
- Failure gate 为 PASS，BudgetExceeded 被保留为非终态。
- Interaction risk table 为 PASS，覆盖 RitsuLib registration/hook、card target/effect/pile cleanup、Power apply/remove、Relic obtain/remove、reward/cross-room persistence。
- Audio capability 为 PASS，最终每个运行目录都记录 Master 和 SFX bus muted。
- visible-policy、windowed rendering、正式 save/load、map presentation、network、Steam/cloud 仍属于独立平台能力。这些能力不改变本票 gameplay gate 结果。

## 执行命令和退出码

| 命令 | 退出码 | 证据 |
|---|---:|---|
| dotnet build .build/runs/m2-4-remilia-20261111/worker/NativeLifecycleProbe.csproj -c Release --no-restore --ignore-failed-sources -p:NuGetAudit=false -p:EnableModDeploy=false | 0 | worker build |
| prepare-runtime.ps1 | 0 | exact game/Ritsu runtime audit |
| run-path.ps1 primary/repeat/replay/failure final cohort | 0 | [evidence-index.json](/D:/STS2-Simulator-Lab/.build/runs/m2-4-remilia-20261111/evidence-index.json) |
| run-content-matrix.ps1 plus four native resource retries | 0 | [content-run-summary.json](/D:/STS2-Simulator-Lab/.build/runs/m2-4-remilia-20261111/content-run-summary.json) |
| dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore | 0 | all 137 contract checks |
| git diff --check | 0 | no new whitespace errors |

## 已知缺口和分母

Gameplay content 分母没有 Partial、Unsupported、Unrun 或 RuntimeFailure。平台能力分母保留 visible-policy、windowed rendering、save/load、map presentation、network、Steam/cloud 缺口。历史四个 card Unsupported 运行目录保持原样，并由新的 native resource precondition 证据补齐。

## 下一阶段唯一建议入口

进入 LexNinja2 的 M2-4 子票 #10，复用已验证的 silent audio policy、CombatStateChanged 通用 seam 和内容矩阵模板。

是否提交或推送：否。

