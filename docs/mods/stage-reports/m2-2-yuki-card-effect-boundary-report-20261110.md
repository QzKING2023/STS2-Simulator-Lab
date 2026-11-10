# M2-2 YukiMakoto Card effect boundary 阶段交接报告

日期：2026-11-10

## 阶段状态

**PARTIAL**。

共享 foundation 继续保持 **PASS**：r260 Acheron 与 Remilia generic fixture 均已证明正常和 failure path 的 callback/registry/collectible ALC teardown，`runtimeAlcTeardown=true`、`alcAlive=false`、`assemblyAlive=false`。本续作沿唯一入口对 `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM` 做了真实合法 play/effect 预检，没有把未知语义升级成覆盖。

新的完整 Godot native probe 观察到：CardReward 真实领取后，`SeizeMomentum` 出现在永久牌组；进入下一场真实战斗时，它不在 Hand、Draw、Discard、Exhaust 或 Play 任一 combat pile，因而没有合法可打出的卡实例。probe 以 `TimeoutException` 停在 RuntimeFailure；未手动插入卡、未伪造 OneMore、未调用卡 OnPlay、未使用 Yuki 专用 adapter。内容行保持 `Partial`，#4 保持 `OPEN` / `ready-for-agent`。

## 分支、HEAD、工作区和修改

- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 既有 dirty 修改全部保留；本续作没有 `reset`、`clean`、`checkout`、覆盖式复制、删除、提交或推送。
- 本续作新增交接报告：`docs/mods/stage-reports/m2-2-yuki-card-effect-boundary-report-20261110.md`。
- 本续作新增启动提示词：`docs/prompts/MOD_M2_2_YUKI_CARD_EFFECT_RESUME_PROMPT_20261110.md`。
- 新的 ignored evidence：`.build/runs/m2-2-yuki-content-card-effect-20261110/`；effect probe 运行目录为 `.build/runs/m2-2-yuki-content-card-effect-20261110-card-effect-final/`。
- 诊断 probe/build 只位于 ignored `.build/diag/` 与 `.build/native-game/m2-2-yuki-card-effect-20261110/`；固定 snapshot、manifest、游戏二进制、Mod DLL/PCK/资源、既有 Yuki evidence 和 generic native-run tracked source 均未修改。

## 固定输入与 registration identity

- 游戏：`v0.111.0` / commit `41cef1ea`。
- snapshot：`workshop-snapshot-20261106-163352`。
- SnapshotContentSha256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- YukiMakoto：`v0.0.7-hot`，Workshop ID `3829924471`，exact `lib/0.111.0`，SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- RitsuLib：exact `compat/0.111.0`，Runtime SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，loader SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- registration identity：`REG.YUKI.lib/0.111.0#1@0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_CARD_01`。

## 执行命令与退出码

| 命令 | 退出码 | 结果 |
|---|---:|---|
| `dotnet build .build/diag/m2-2-yuki-card-effect-probe/M22YukiCardEffectProbe.csproj -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false` | 0 | 0 warnings、0 errors；probe SHA-256 `a177726ae497f1c4975342ee2f801ee037af8c81e6d4b006e2a40201e1978591` |
| `pwsh -NoProfile -File .build/diag/m2-2-yuki-card-effect-probe/run-game.ps1 -Output .build/runs/m2-2-yuki-content-card-effect-20261110-card-effect-final -RunKind card-effect -Seed LAB_M2_2_YUKI_CARD_01` | 0 | raw process exit `0`；result `Unknown / RuntimeFailure`，`TimeoutException` |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| 新 JSON 解析、append-only 计数、`git diff --check` | 0 | 11 个新 JSON 可解析；586 rows；无新增 whitespace error |

新的证据索引见 [evidence-index.json](../../../.build/runs/m2-2-yuki-content-card-effect-20261110/evidence-index.json)。

## Foundation、flow、content、stability

- Foundation：**PASS**，沿用 [foundation repair final](m2-2-yuki-foundation-repair-20261110-final.md)；本续作没有改写 foundation 结论。
- Flow：**PASS（接受的四路）**；primary/repeat/replay 仍为 `Death / Completed`，failure 仍为 `Unknown / BudgetExceeded`。新 effect probe 是独立的内容边界尝试，按真实运行结果分类为 RuntimeFailure。
- Content：**PARTIAL**；真实 registration、CardReward 领取和永久牌组写入成立；combat-pile propagation、legal play、effect、cleanup 未闭合。
- Stability：**PASS（接受的四路）**；既有 primary/repeat/replay 的 terminal、state、trace、route 摘要一致。新 probe 只证明一次确定的失败边界，不生成成功 digest。

## Content unit、lifecycle matrix 和 interaction risk

- 总 inventory：586 个身份；Card 270、Character 1、Other 30、Power 281、Relic 4。
- 本续作继续只处理 `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM`；没有接入第二角色，也没有开始 M2-3。
- `SeizeMomentum` lifecycle：
  - 已观察：discovery/registration、CardReward 构造与选择、永久牌组 acquisition、下一场战斗的 combat-pile inspection。
  - 未闭合：combat-pile propagation、legal play、OneMore prerequisite、native draw/energy effect、card-specific cleanup。
  - 状态：`Partial`。
- 关键 interaction risk：
  - `Yuki registration identity`：`Covered`。
  - `Card reward acquisition`：`Covered`。
  - `combat-pile propagation`：`RuntimeFailure` evidence；永久牌组含卡，但 Hand/Draw/Discard/Exhaust/Play 均不含卡。
  - `Card target/effect`：`Unrun`；没有合法 play，因此没有 target/effect 结论。
  - `cleanup`：`Partial`；native Death cleanup 已在接受的四路观察，卡特有 cleanup 未观察。
  - visible-policy：`Unsupported`；TestMode/internal collector 不能证明可见策略。
  - 平台能力缺口：UI、正式 save/load、map presentation、network/replay network、Steam、cloud。

完整矩阵与风险表见 [lifecycle-matrix.json](../../../.build/runs/m2-2-yuki-content-card-effect-20261110/lifecycle-matrix.json) 和 [interaction-risk.json](../../../.build/runs/m2-2-yuki-content-card-effect-20261110/interaction-risk.json)。

## 四路终态、digest 和错误分类

接受的本切片四路保持原证据：

| 路径 | 终态/停止 | state digest | trace chain digest | route digest | 错误分类 |
|---|---|---|---|---|---|
| primary | `Death / Completed` | `3cabb41b2bda52e0b84bdaaabae15ab1c0c55251dd646b24cb5bf48906948206` | `9c85226523ea3c43777f33948ffeec9136844c334e40707821cd6ea202d56482` | `bf2ca5bbb43cce725e7b236294a936d30c6c93dfc79af4b41530a9276f0ed542` | native Death |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | native Death |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | native Death；基于 primary route |
| failure | `Unknown / BudgetExceeded` | — | `b9d38c0a089d872499da312dbeba22d859955ddc53365aec1073e292d07cab55` | — | controlled budget stop；不能提升为 Death |
| card-effect probe | `Unknown / RuntimeFailure` | — | `f44d2060e1325fb8b4adf2cd5d9e92db7333f3ba96814fd80e9aa01878cda1de` | — | `TimeoutException`：SeizeMomentum 不在任何 combat pile；不能提升为 legal play |

新 probe 的 trace seal 为 80 events；本 collector 没有完整可审计 RNG digest，`rngDigest` 保持空值。既有 Yuki M2-2 primary/repeat/replay/failure evidence 没有被替换或重分类。

## 分母和未覆盖语义

- `Covered`：content unit 0。
- `Partial`：content unit 9；包括原有 8 个 partial 和 `SeizeMomentum`。
- `Unrun`：content unit 577。
- `Unsupported`：content unit 0；visible-policy 和平台能力缺口单独记录。
- `RuntimeFailure`：content unit 0；effect probe 的 process-level RuntimeFailure 保留在证据中，但没有把内容行错误归类为 RuntimeFailure。
- 既有 generic Yuki foundation worker 的 `FileAccess` native crash 仍保留为单独 RuntimeFailure evidence，没有计入 content row。

## 下一唯一入口

继续同一 registration identity，先解决并重新证明 `CARD.YUKI_MAKOTO_CARD_SEIZE_MOMENTUM` 从永久牌组到下一场 combat pile 的真实传播；只有观察到真实合法卡实例后，才允许尝试 OneMore prerequisite、native play/effect、draw/energy result 和 card-specific cleanup。不得手动 `CardPileCmd.Add`、注入结果、伪造 OneMore、替换原版角色或引入 Yuki 专用 adapter；不得接入第二角色或开始 M2-3。

本阶段仍不提交、不推送、不关闭 #4。
