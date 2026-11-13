# M2-5 Shadowverse/Minion full-run handoff — 2026-11-13

## 结果

**PASS（固定 `v0.111.0 / 41cef1ea` 基线）**。

本次纵切片先在真实 native combat 中注册并召唤
`shadowversebydmod.Scripts.Monsters.KnightSoldierMinion`，实际使用
`CARD.ANCIENT_ARTIFACT` / `shadowversebydmod.StrikeComponent`，完成 Minion 受伤、死亡、移除和战斗清理；随后通过原生奖励、地图和战斗路径继续到真实 native `Death`。

Primary、Repeat、Replay 均为独立新进程，终点均为 `Death / Completed`。Failure 路径保留为 `BudgetExceeded`，没有产生 gameplay 证据，也没有把控制失败提升为终局。

## 固定输入

- 游戏：`0.111.0 / 41cef1ea`
- Workshop 快照：`workshop-snapshot-20261106-163352`
- 快照摘要：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- MinionLib：`0.6.3`
- RitsuLib：`compat/0.111.0`，`0.6.10`
- Shadowverse：`1.6.0`
- Seed：`LAB_M25_MINION_FULLRUN_01`
- Native build：`.build/native-build/minion-m25-v1/build-manifest.json`

固定快照、Mod DLL/PCK、manifest 和正式游戏输入均未修改。

## 四路证据

| 路径 | 结果 | 证据 |
|---|---|---|
| Primary | `Death / Completed` | `.build/runs/m2-5-shadowverse-minion-20261113-primary/` |
| Repeat | `Death / Completed` | `.build/runs/m2-5-shadowverse-minion-20261113-repeat/` |
| Replay | `Death / Completed` | `.build/runs/m2-5-shadowverse-minion-20261113-replay/` |
| Failure | `Unknown / BudgetExceeded` | `.build/runs/m2-5-shadowverse-minion-20261113-failure/` |

Primary、Repeat、Replay 的 Minion probe digest 相同：

`c0baca7c12192e424bb49335022f8fe96a96131269d415202c8d2336be7ce45d`

三路的 state、RNG、trace、route 和 choice digest 也一致。Failure 仅保留加载、隔离和预算停止证据，不包含真实战斗。

## 内容与边界

已覆盖的真实内容身份：

- `MONSTER.KNIGHT_SOLDIER_MINION`
- `CARD.ANCIENT_ARTIFACT`
- `shadowversebydmod.StrikeComponent`

现阶段未覆盖的复杂组件、UI、地图/完整路由、正式保存和网络语义保留在 Stage 10 capability matrix 中，状态为 `Unsupported`，没有从分母中删除。聚合报告位于：

`.build/runs/m2-5-shadowverse-minion-20261113-evaluation/stage10-report.json`

## 验证

- `pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestMinionM25.ps1`：应输出 `PASS`。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`：`PASS all 137 contract checks`。
- `git diff --check`：通过。

本切片没有声明完整 Shadowverse 内容包兼容；它关闭的是固定 Minion/组件真实战斗、真实 native Death、稳定四路证据和明确保留边界。
