# M2-2 YukiMakoto SeizeMomentum 完整生命周期阶段交接报告

日期：2026-11-10

## 阶段状态

**PARTIAL（本次选定内容单元已 Covered）**。

本续作解决了当前 blocker。根因是旧 probe 在 CardReward 后仍把已结束的 `CombatRoom` 当作当前房间，`EnterNextCombat` 没有推进到下一个合法 map child，因此第二次 `CombatManager.SetUpCombat` 没有发生，牌组仍是 10 张。诊断确认后，仅在 ignored 的诊断 probe 中加入通用“从已结束 combat room 推进到下一合法节点”的流程修复；未修改核心模拟器、Mod、snapshot、游戏二进制或共享 adapter。

修复后，真实第二场 combat 重新从 11 张永久牌组构建，`SeizeMomentum` 真实经历 `Draw → Hand → Play → Exhaust`。其 OneMore 前置条件由真实战斗状态满足；原生效果抽取 1 张牌并使能量增加 3；随后真实 native Death 和 cleanup 完成。

## 分支、HEAD、工作区和修改

- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 既有 dirty 修改全部保留；未执行 `reset`、`clean`、`checkout`、覆盖式复制、删除、提交或推送。
- 新聚合证据：[m2-2-yuki-content-card-covered-20261110](../../../.build/runs/m2-2-yuki-content-card-covered-20261110/)。
- 新运行：`card-primary`、`card-repeat`、`card-replay-r2`、`card-failure`，均使用新的 `.build/runs` 目录和 canonical input。
- ignored 诊断 probe/build 位于 `.build/diag/m2-2-yuki-card-effect-probe/` 与 `.build/native-game/m2-2-yuki-card-effect-20261110/`；不属于核心或 Mod 代码边界。

## 固定输入和 registration identity

- 游戏：`v0.111.0` / commit `41cef1ea`。
- snapshot：`workshop-snapshot-20261106-163352`。
- SnapshotContentSha256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- YukiMakoto：`v0.0.7-hot`，Workshop ID `3829924471`，exact `lib/0.111.0`，SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- RitsuLib：exact `compat/0.111.0`，Runtime SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，loader SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- registration identity：`REG.YUKI.lib/0.111.0#1@0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_CARD_01`。

## 执行命令和退出码

| 命令 | 退出码 | 结果 |
|---|---:|---|
| `dotnet build .build/diag/m2-2-yuki-card-effect-probe/M22YukiCardEffectProbe.csproj -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false` | 0 | 0 warnings、0 errors |
| `run-game.ps1 -RunKind card-primary` | 0 | `Death / Completed` |
| `run-game.ps1 -RunKind card-repeat` | 0 | `Death / Completed` |
| `run-game.ps1 -RunKind card-replay -ReplayPath ...card-primary.../route.json` | 0 | `Death / Completed` |
| `run-game.ps1 -RunKind card-failure` | 0 | `Unknown / BudgetExceeded` |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| 新 JSON 解析、append-only 计数、`git diff --check` | 0 | 586 rows retained；无新增 whitespace error |

## Foundation、flow、content、stability

- Foundation：**PASS**，沿用 r260 Acheron/Remilia generic repair evidence。
- Flow：**PASS**；content primary/repeat/replay 真实 `Death / Completed`，failure 保持 `BudgetExceeded`。
- Content：**PARTIAL（聚合 inventory）；SeizeMomentum=Covered**。
- Stability：**PASS**；primary/repeat/replay 的 state、trace、route digest 完全一致。

## Content unit、lifecycle matrix 和 interaction risk

- 总 inventory：586 个身份：Card 270、Character 1、Other 30、Power 281、Relic 4。
- 本次唯一闭合单元：`CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM`。
- registration：`REG.YUKI.lib/0.111.0#1@0.111.0`。
- lifecycle 已闭合：
  - discovery/registration；
  - CardReward 构造与选择；
  - 永久牌组 acquisition；
  - 第二场 `SetUpCombat` 从 11 张牌组传播到 Draw；
  - Draw→Hand；
  - 真实 OneMore 前置条件；
  - 合法 self-target play；
  - native draw 1；
  - energy delta `+3`；
  - Play→Exhaust；
  - native Death；
  - combat cleanup。
- interaction risk：foundation、registration、reward、combat-pile propagation、OneMore、card effect、Exhaust、cleanup 均 `Covered`；formal save/load 为 `Partial`；visible-policy 为 `Unsupported`；UI、map presentation、network、Steam/cloud 为平台能力缺口。

完整内容见 [content-matrix.json](../../../.build/runs/m2-2-yuki-content-card-covered-20261110/content-matrix.json)、[lifecycle-matrix.json](../../../.build/runs/m2-2-yuki-content-card-covered-20261110/lifecycle-matrix.json) 和 [interaction-risk.json](../../../.build/runs/m2-2-yuki-content-card-covered-20261110/interaction-risk.json)。

## 四路终态、digest 和错误分类

| 路径 | 终态/停止 | state digest | trace chain digest | route digest | 错误分类 |
|---|---|---|---|---|---|
| card-primary | `Death / Completed` | `3cabb41b2bda52e0b84bdaaabae15ab1c0c55251dd646b24cb5bf48906948206` | `161a9eba0e6a12528585822aa805c5d6757dcbbd4fb72829e6967ffb50864c5c` | `8752e519658b4ecd2ec8c830b4fd86ab09588a0ab8796c75728b6ee1900d8416` | native Death |
| card-repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | native Death |
| card-replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | native Death；引用 primary route |
| card-failure | `Unknown / BudgetExceeded` | — | `4686e9b72af22316cdbfeee0c03e29b38dbfde6db294840867e39a5648ff5b38` | — | controlled budget stop；不能提升为 Death |

新 primary/repeat/replay 每路 513 trace events；failure 16 events。collector 没有完整可审计 RNG digest，`rngDigest` 保持空值。

## 分母和未覆盖语义

- `Covered`：1。
- `Partial`：8。
- `Unrun`：577。
- `Unsupported`：0 个 content unit；visible-policy 单独记录。
- `RuntimeFailure`：0 个 content unit；旧 generic worker 的 FileAccess crash 和本次之前的 card-pile failure evidence 均保留但未污染 matrix。

## 下一唯一入口

当前 blocker 已解决，`SeizeMomentum` 单元可保持 `Covered`。如果继续 M2-2，只能从剩余 8 个 `Partial` 或一个明确选定的 `Unrun` 单元中选择下一最小切片；不得回退既有证据、接入第二角色或开始 M2-3。

本阶段仍不提交、不推送、不关闭 #4。
