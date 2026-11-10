# M2-2 YukiMakoto Card 内容切片阶段交接报告

日期：2026-11-10

## 阶段状态

**PARTIAL**。

共享 foundation 已由 generic fixture 在 Acheron 与 Remilia 两个独立内容族完成修复验收；最新 r260 证据显示 `runtimeAlcTeardown=true`、`alcAlive=false`、`assemblyAlive=false`，foundation gate 为 `PASS`。本续作只推进一个最小 Yuki Card 内容族切片：真实注册并领取 `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM`。该卡进入最终牌组，但本路由没有合法打出它，因此保持 `Partial`。

#4 仍保持 `OPEN`、`ready-for-agent`；没有接入第二个角色，没有开始 M2-3。

## 分支、HEAD、工作区和修改

- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 开始前已有 dirty 修改全部保留；未执行 `reset`、`clean`、`checkout`、覆盖式复制、删除、提交或推送。
- 本续作新增证据配置：`.build/yuki-card-foundation-20261110.json`。
- 本续作生成新 build：`.build/native-build/m2-2-yuki-content-card-20261110/`。
- 本续作新增聚合证据：`.build/runs/m2-2-yuki-content-card-20261110/`。
- 本续作新增四路 Godot 运行目录：
  - `.build/runs/m2-2-yuki-content-card-20261110-godot-primary/`
  - `.build/runs/m2-2-yuki-content-card-20261110-godot-repeat/`
  - `.build/runs/m2-2-yuki-content-card-20261110-godot-replay/`
  - `.build/runs/m2-2-yuki-content-card-20261110-godot-failure/`
- 固定快照、manifest、游戏二进制、Mod DLL/PCK、既有 Yuki 四路证据和核心外部类型边界均未修改。

## 固定输入

- 游戏：`v0.111.0` / commit `41cef1ea`。
- 快照：`workshop-snapshot-20261106-163352`。
- SnapshotContentSha256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- YukiMakoto：`v0.0.7-hot`，Workshop ID `3829924471`，exact `lib/0.111.0`，SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- RitsuLib：exact `compat/0.111.0`，Runtime SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，loader SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- registration identity：`REG.YUKI.lib/0.111.0#1@0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_CARD_01`。
- worker：独立完整 Godot native game process + `SimulatorLab.NativeLifecycleProbe`；adapter `native-mod-loop/v1`；collector `native-trace/v1`。

## 执行命令与退出码

| 命令 | 退出码 | 结果 |
|---|---:|---|
| `gh issue view 4 ...` | 0 | #4 `OPEN` / `ready-for-agent` |
| `gh issue view 3 ...` | 0 | #3 `CLOSED` |
| `build-native-run.ps1 -OutputRoot .build/native-build/m2-2-yuki-content-card-20261110` | 0 | build id `4AB241F1A0757CCEF3DED58EB22349128BE108F26AF23100CF6124C02C996692`；3 warnings、0 errors |
| generic `run-native-slice.ps1` foundation Yuki 尝试 | 1 | raw child exit `-1073741819`，真实 Godot `FileAccess` native crash；证据保留在 `m2-2-yuki-content-card-20261110-primary/`，未计为内容成功 |
| Godot `run-game.ps1 -RunKind primary` | 0 | `Death / Completed` |
| Godot `run-game.ps1 -RunKind repeat` | 0 | `Death / Completed` |
| Godot `run-game.ps1 -RunKind replay` | 0 | `Death / Completed` |
| Godot `run-game.ps1 -RunKind failure` | 0 | `Unknown / BudgetExceeded` |
| Core checks | 0 | `PASS all 137 contract checks` |
| 新 JSON 验证、append-only 计数、`git diff --check` | 0 | 586 rows；无新增 whitespace error |

generic worker 崩溃属于执行边界 RuntimeFailure 证据：堆栈落在 Yuki 初始化的 `ModDataStore -> Godot.FileAccess`，没有生成 result，也没有被升级为内容通过。真实完整 Godot host 的四路运行成功完成了本切片。

## Foundation、flow、content、stability

- Foundation：**PASS**，依据 [m2-2-yuki-foundation-repair-20261110-final.md](m2-2-yuki-foundation-repair-20261110-final.md) 及 r260 Acheron/Remilia fixture；本续作没有改变 foundation 结论。
- Flow：**PASS（本切片四路）**；新 primary/repeat/replay 均真实 `Death / Completed`，failure 保留预算停止。
- Content：**PARTIAL**；`SeizeMomentum` 获得了真实 registration/reward/deck lifecycle，但未完成 legal play/effect/完整 cleanup。
- Stability：**PASS（四路边界）**；primary/repeat/replay 的 terminal、state、trace、route 摘要一致。
- Mod 聚合：`PARTIAL`，因为 content gate 未闭合。

## Content unit 与 append-only matrix

- 总 inventory：586 个身份；Card 270、Character 1、Other 30、Power 281、Relic 4。
- registration identity：`REG.YUKI.lib/0.111.0#1@0.111.0`。
- 本续作追加的唯一身份是既有 `Unrun` 行 `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM`；没有删除或折叠任何身份。
- 新 matrix 分母：`Covered=0`、`Partial=9`、`Unrun=577`、`Unsupported=0`、`RuntimeFailure=0`。
- `SeizeMomentum` 状态：`Partial`；真实 evidence 是 `godot-primary/game-output/trace.jsonl`、`final-state.json`、`content-inventory.json` 和 `godot.log`。

## Lifecycle matrix

| 内容单元 | 已观察 | 未闭合 | 状态 |
|---|---|---|---|
| `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM` | discovery/registration、CardReward 构造与选择、永久加入牌组、跨到第二战前的 deck state | legal play、target/effect、effect-generated state、card-specific cleanup | `Partial` |
| 起始 Strike/Defend/Dark Hour | starting-deck construction | legal play/effect、完整 card cleanup | `Partial` |
| Yuki registration inventory | 586 identity discovery、exact variant load、role selection | full lifecycle for remaining 577 identities | `Partial/Unrun` |

完整切片矩阵见 [lifecycle-matrix.json](../../../.build/runs/m2-2-yuki-content-card-20261110/lifecycle-matrix.json)。

## Interaction risk table 和平台能力

- `Yuki registration identity`：`Covered`，exact `lib/0.111.0` loader 与 586 inventory 已观察。
- `Card reward acquisition`：`Covered`，真实 CardReward 领取 `SeizeMomentum` 并写入最终牌组。
- `Card target/effect`：`Unrun`，没有把 reward acquisition 冒充 card play。
- `Card persistence`：`Partial`，卡在第二战前仍存在于 native deck；正式 save/load 未覆盖。
- `cleanup`：`Partial`，native Death 后 combat cleanup 已观察；卡特有 effect cleanup 未观察。
- visible-policy：`Unsupported`，collector 使用 TestMode/internal state。
- 平台能力缺口：UI、正式 save/load、map presentation、network/replay network、Steam、cloud。

原始风险与新切片风险见 [interaction-risk.json](../../../.build/runs/m2-2-yuki-content-card-20261110/interaction-risk.json)。

## 四路终态、digest 和错误分类

| 路径 | 终态/停止 | state digest | trace digest | route digest | 错误分类 |
|---|---|---|---|---|---|
| primary | `Death / Completed` | `3cabb41b2bda52e0b84bdaaabae15ab1c0c55251dd646b24cb5bf48906948206` | `9c85226523ea3c43777f33948ffeec9136844c334e40707821cd6ea202d56482` | `bf2ca5bbb43cce725e7b236294a936d30c6c93dfc79af4b41530a9276f0ed542` | native Death |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | native Death |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | native Death；基于 primary route |
| failure | `Unknown / BudgetExceeded` | — | `b9d38c0a089d872499da312dbeba22d859955ddc53365aec1073e292d07cab55` | — | controlled budget stop；不能提升为 Death |

本切片 collector 没有提供可审计的完整 RNG digest，因此 `rngDigest` 保持空值；没有用 state hash 伪造 RNG 证据。既有 Yuki M2-2 primary/repeat/replay/failure digest 保持原值，未被本续作替换。

## 分母和未覆盖语义

- `Covered`：content unit 0。
- `Partial`：content unit 9；包括原有 8 个 partial 和本次 `SeizeMomentum`。
- `Unrun`：content unit 577；包括全部 Power 281、Other 30、未触发 Card/Relic 及本次卡牌 effect。
- `Unsupported`：content unit 0；visible-policy、UI、正式 save/load、map presentation、network、Steam/cloud 为独立 capability gap。
- `RuntimeFailure`：content unit 0；generic Yuki foundation worker 有 1 条 `ProcessCrashed` 证据，但它没有把任何 content row 计为 RuntimeFailure。
- `RunVictory`：本切片未运行；四路终态仍按真实 Death / BudgetExceeded 分类。

## 下一唯一入口

继续同一 Yuki registration identity，优先为 `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM` 建立真实合法 play/effect/target 与 effect cleanup 证据；完成后再决定是否将该单元从 `Partial` 提升。不要把起始牌、其余 Card/Power/Relic、第二角色或 M2-3 混入本阶段。

是否提交或推送：**否**。
