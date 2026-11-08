# 阶段 10：MinionLib Shadowverse Beyond 真实战斗纵切片交接

## 阶段状态

**PASS**。阶段 10 在固定快照、目标游戏 `0.111.0` 和 RitsuLib 精确变体 `compat/0.111.0` 下完成了一个可重复的真实 Shadowverse/MinionLib 战斗纵切片。

证据目录：

- 主运行：`.build/runs/minion-stage10-native-primary-20261108e/`
- replay：`.build/runs/minion-stage10-native-replay-20261108e/`
- 失败隔离：`.build/runs/minion-stage10-native-failure-20261108e/`
- worker 评估：`.build/runs/minion-stage10-evaluation-20261108/`
- native 构建：`.build/native-build/minion-stage10-v5/`

主运行和 replay 的规范化摘要相同：

`d10748bef819ea570c820e0749a15bcb080fc8cf23cc4c32e5861b70d50a8279`

worker 报告确定性摘要：

`0731c8af4ea1323971580234d3f46325a6aebf50be74e6e13721e2fde626d87d`

## 分支与工作区

- 当前分支：`main`
- 没有提交、推送或远程 PR。
- 阶段开始时已保留所有已有未提交修改；本阶段没有 reset、clean、checkout 覆盖或删除已有项目文件。
- `git status --short` 仍包含阶段开始时已有的 `README.md` 修改、历史 `docs/*` 删除、既有 `tools/native-run/*` 修改，以及阶段 9/10 的新增文件。它们未被清理或重写。

## 固定输入

- 快照目录：`local-mods/workshop-snapshot-20261106-163352`
- manifest：`local-mods/workshop-snapshot-20261106-163352.manifest.json`
- 快照内容 SHA256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- 目标游戏：`0.111.0`
- MinionLib：`0.6.3`
- Shadowverse Beyond Mod：`1.6.0`
- RitsuLib：`compat/0.111.0`，版本 `0.6.10`

阶段末重新核对的关键哈希未改变：manifest `09A553D42F860A7AD22FA7D62BD66D4614CC618D06756218E6BE068181E8E5D3`；MinionLib DLL/PCK `CB4CB7D57CD47D318CE485E67C19BCE3E06CF91CC77B76B6FD0E520D868A4113` / `E3D343C4A6556C6F906428509846F793B380B72098FB2A4BD88F0ABE32B7B540`；Shadowverse DLL/PCK `638D89DCCE30139EE4E978A170FB4625AEDFAF2CF38E4F3BB5C17D0BD6338749` / `6B948B2961E896A2904A333A5710448BD60396BB72523493C37C73F319C9F2A9`。

## 实际修改文件

- `src/SimulatorLab.Core/Mods/MinionStage10.cs`：阶段 10 请求、报告、标量适配、能力矩阵、确定性和失败隔离验证。
- `src/SimulatorLab.Core/Mods/MinionStage9.cs`：为真实战斗探针增加核心标量观察值和事件记录；没有引入外部 Mod/Godot 类型。
- `tools/mod-worker/MinionStage10.csproj`
- `tools/mod-worker/MinionStage10Program.cs`
- `tools/native-run/MinionNativeCombatSlice.cs`：真实 Minion 注册、召唤、组件卡实际使用、受伤、死亡、移除和战斗结束清理。
- `tools/native-run/MinionNativeModLoop.cs`：阶段 10 探针输出与真实观察转接。
- `tools/native-run/Program.cs`：阶段 10 主运行/replay 入口。
- `tools/validation/TestMinionStage10.ps1`
- `docs/mods/stage-reports/10-minion-real-combat-handoff.md`

关键未修改输入：固定 Workshop 快照目录及 manifest、快照内 MinionLib/RitsuLib/Shadowverse DLL/PCK、`0.111.0` pinned game input、Watcher native host、正式玩家存档、正式设置和正式游戏数据。

## 实际执行的命令及结果

- `dotnet build src/SimulatorLab.Core/SimulatorLab.Core.csproj -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false`：通过，0 警告/0 错误。
- `dotnet build tools/mod-worker/MinionStage10.csproj -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false`：通过，0 警告/0 错误。
- `pwsh tools/native-run/build-native-run.ps1 ... -OutputRoot .build/native-build/minion-stage10-v5`：通过，0 错误。
- `pwsh tools/native-run/run-native-slice.ps1 ... -Scenario minion-stage10`：通过并生成真实主运行证据。
- `pwsh tools/native-run/run-native-slice.ps1 ... -Scenario minion-stage10-replay -ReplayPath .../route.json`：通过并生成 replay 证据。
- `pwsh tools/native-run/run-native-slice.ps1 ... -Scenario minion-stage10 -Control budget`：按预期以退出码 3 产生 `runtime-failed` 失败探针，且没有真实战斗观察。
- `dotnet tools/mod-worker/bin/Release/net9.0/MinionStage10.dll ...`：通过，报告状态 `Pass`。
- `pwsh tools/validation/TestMinionStage10.ps1`：通过。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`：通过，全部 137 项 contract checks。
- `pwsh tools/validation/TestModWorker.ps1`：通过。
- `pwsh tools/validation/TestWatcherStage6.ps1`：通过（Watcher 的预期 unsupported 场景保持 `Blocked`，验证脚本通过）。
- `pwsh tools/validation/TestRitsuStage7.ps1`：通过。
- `pwsh tools/validation/TestKaguyaStage8.ps1`：通过。
- `pwsh tools/validation/TestMinionStage9.ps1`：通过。

## 验收标准逐项结果

| 标准 | 结果 | 证据 |
|---|---|---|
| 真实 Shadowverse/MinionLib 运行路径执行 | PASS | 主/replay probe 的 `nativeRuntimeAvailable`、`nativeGameplayObserved`、MinionLib 初始化和 Shadowverse content registration |
| 真实 Minion 注册 | PASS | `minion-real-register`，类型 `shadowversebydmod.Scripts.Monsters.KnightSoldierMinion` |
| 真实 Minion 召唤/生成 | PASS | `minion-real-summon`，`summonedHp=3`，模型 `MONSTER.KNIGHT_SOLDIER_MINION` |
| 攻击或受伤结果 | PASS | `minion-real-damaged`，HP `3 -> 2`；组件目标 HP `39 -> 28` |
| 死亡或移除结果 | PASS | `minion-real-death`、`minion-real-removal`，HP `2 -> 0`，从 combat 和 pets 移除 |
| 战斗结束清理 | PASS | `minion-combat-end-cleanup`，`combatEnded=true`、`cleanupConfirmed=true` |
| 一个真实组件语义 | PASS | `CARD.ANCIENT_ARTIFACT` 的 `shadowversebydmod.StrikeComponent`，amount `11`，实际降低目标 HP |
| 主运行/replay 稳定摘要 | PASS | primary/replay `primaryDigest` 完全一致 |
| 失败隔离 | PASS | budget 失败为 `runtime-failed`，无 `realCombat`，快照/正式数据写入均阻断 |
| 核心不依赖外部类型 | PASS | Core 项目无 MinionLib/Shadowverse/Godot assembly reference 或 using；核心只接收标量记录 |
| 既有回归 | PASS | Core、ModWorker、Watcher、Ritsu、Kaguya、MinionStage9 全部通过 |
| 固定快照哈希未改变 | PASS | manifest、MinionLib 和 Shadowverse 关键 DLL/PCK 哈希均与固定值一致 |
| 未支持语义有证据 | PASS | 报告能力矩阵显式标记 UI、Map、Save、ComplexComponent、ReplayNetwork 为 Unsupported |
| 无提交/推送 | PASS | 当前工作区无本阶段提交；未执行 commit、push 或远程 PR |

## 已知缺口和未支持语义

本阶段只关闭一个最小真实战斗入口：`KnightSoldierMinion` + `AncientArtifact/StrikeComponent`。UI/Godot 场景、地图和 run 路由、正式保存/设置、复杂组件族、网络/多人 replay、完整 Shadowverse 内容包仍明确为 `Unsupported`。这些边界没有被“加载成功”或空实现掩盖。

## 下一阶段唯一建议入口

若启动下一阶段，只从本报告已闭合的 `MinionNativeCombatSlice` 和 `MinionStage10Adapter` 入口扩展一个额外真实组件或状态语义，并沿用同一固定快照、primary/replay/failure 三路证据；先不要扩大到 UI、地图、保存或网络语义。

