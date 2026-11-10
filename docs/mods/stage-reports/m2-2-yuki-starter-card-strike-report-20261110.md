# M2-2 YukiMakoto starter-card slice 交接报告

日期：2026-11-10

## 阶段状态

**PARTIAL**。本续作只闭合一个剩余可达内容单元：`CARD.YUKI_MAKOTO_CARD_STRIKE`。它在真实第二场战斗中从 Yuki 起始牌组传播到 Hand，合法攻击 `MONSTER.FUZZY_WURM_CRAWLER`，原生造成 7 点伤害并进入 `Discard`，随后同一 native run 到达 `Death` 和 combat cleanup。Yuki 的聚合 content gate 仍未通过，因为其余 7 个 `Partial` 和 577 个 `Unrun` 内容身份仍保留。

## 票据、分支和工作区

- 开始前执行 `. .\tools\maintenance\Set-GitHubSession.ps1`；`gh issue view 4` 确认 #4 为 `OPEN`、`ready-for-agent`，`gh issue view 3` 确认 #3 为 `CLOSED`。
- 未执行 `/triage`、未创建重复票据、未认领或关闭 #4。
- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- `git status --short --branch` 保留了开始前全部 dirty 修改；本续作没有修改核心、共享 adapter、Mod 源码、固定 snapshot、游戏二进制或既有证据目录。新增阶段报告为 `docs/mods/stage-reports/m2-2-yuki-starter-card-strike-report-20261110.md`；`.build/` 诊断和运行目录被忽略。
- 未执行 `reset`、`clean`、`checkout`、覆盖式复制、删除、提交或推送。

## 固定输入和 exact variant

- Workshop snapshot：`local-mods/workshop-snapshot-20261106-163352`。
- snapshot manifest：`local-mods/workshop-snapshot-20261106-163352.manifest.json`；`ItemCount=21`、`FileCount=142`、`TotalBytes=6,974,558,103`。
- SnapshotContentSha256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- 游戏：`v0.111.0` / commit `41cef1ea`；pinned `sts2.dll` SHA-256 `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`。
- RitsuLib exact `compat/0.111.0`：Runtime `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，loader `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- YukiMakoto exact `lib/0.111.0`：`442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- registration identity：`REG.YUKI.lib/0.111.0#1@0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_STARTER_STRIKE_01`。
- 本切片的 canonical input、run manifest 和四路运行索引位于 [.build/runs/m2-2-yuki-content-starter-card-strike-20261110](../../../.build/runs/m2-2-yuki-content-starter-card-strike-20261110/)。

## 实际修改与边界

- 新增 ignored 诊断 probe：`.build/diag/m2-2-yuki-starter-card-probe/`，collector 为 `SimulatorLab.NativeStarterCardProbe`；它只记录真实 native 生命周期，不加入核心共享接口。
- 新增 ignored hardlink/junction game root：`.build/native-game/m2-2-yuki-starter-card-20261110-r3/`。游戏 EXE/PCK、RitsuLib、Yuki、BaseLib 均复用固定输入的只读 bytes；probe DLL SHA-256 为 `3d5813224ccdaf3ed576a81e3e796f3a1ae3702ba1245e3029d6c2a2015f0c9b`。
- 新路径只选择 `GoldReward`；CardReward 未选择，因此没有重新取得、注入或重跑 `SeizeMomentum`。
- 诊断失败路径 `primary`、`primary-r2`、`primary-r3`、`primary-r4` 均保留在独立目录，状态没有写入 content matrix，也没有被重分类覆盖。

## 命令和退出码

| 命令 | 退出码 | 结果 |
|---|---:|---|
| `. .\tools\maintenance\Set-GitHubSession.ps1; gh issue view 4 ...; gh issue view 3 ...` | 0 | #4 `OPEN/ready-for-agent`；#3 `CLOSED` |
| `dotnet build .build/diag/m2-2-yuki-starter-card-probe/M22YukiStarterCardProbe.csproj -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false` | 0 | 0 warning、0 error |
| `run-game-r3.ps1 -RunKind primary` | 0 | native `Death / Completed` |
| `run-game-r3.ps1 -RunKind repeat` | 0 | native `Death / Completed` |
| `run-game-r3.ps1 -RunKind replay -ReplayPath ...primary-r5.../route.json` | 0 | native `Death / Completed`；引用 primary route |
| `run-game-r3.ps1 -RunKind failure` | 0 | `Unknown / BudgetExceeded`；独立控制 |
| append-only matrix verifier（586 rows；仅 Strike 发生状态变化） | 0 | `Covered=2, Partial=7, Unrun=577`；其余身份无意外变更 |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| `git diff --check` | 0 | 无新增 whitespace error；仅保留既有换行提示 |

## Foundation、flow、content、stability

- **Foundation gate：PASS（沿用）**。使用既有 generic Acheron/Remilia foundation repair evidence；本续作没有重新做 foundation repair，也没有用 Yuki 特判绕过 foundation。
- **Flow gate：PASS**。新 primary/repeat/replay 都在真实角色、真实房间、真实战斗动作后到达 native `Death / Completed`；failure 保持 `BudgetExceeded`，没有提升为终态成功。
- **Content gate：PARTIAL**。本次把一个 `Partial` 身份变为 `Covered`；聚合 inventory 仍有 7 个 `Partial`、577 个 `Unrun`。
- **Stability：PASS**。primary/repeat/replay 的 state、trace chain、route digest 一致；collector 没有提供完整 RNG digest，`rngDigest=null` 保持明确缺口。

## Content unit、registration 和 lifecycle

唯一新内容单元：`CARD.YUKI_MAKOTO_CARD_STRIKE`，类型 `YukiMakotoMod.Cards.Strike`，registration `REG.YUKI.lib/0.111.0#1@0.111.0`，状态 **Covered**。

已观察的 lifecycle checkpoint：

- 586 个 runtime identities 的 discovery/registration；
- Yuki 起始牌组中的四个 Strike identity；
- 第二次 `SetUpCombat` 后，Strike 进入 combat deck 和 Hand；
- 对 `MONSTER.FUZZY_WURM_CRAWLER` 的合法 native target play；
- 原生伤害 `55 -> 48`，damage `7`，energy `3 -> 2`；
- 结果 pile 为 `Discard`；
- native `Death` 和 combat cleanup。

证据见 `primary-r5`、`repeat`、`replay` 的 `game-output/trace.jsonl`、`final-state.json`、`route.json`、`reward.json` 和 `run-manifest.json`；聚合矩阵为 `content-matrix.json`、`lifecycle-matrix.json`、`interaction-risk.json`。

## Interaction risk 和平台能力

- foundation gate、registration identity、starting-card propagation、legal target/effect、Discard/cleanup、GoldReward-only isolation：`Covered`。
- formal save/load：`Partial`；本切片仍未执行正式 save/load。
- visible-policy compatibility：`Unsupported`；collector 使用 `TestMode/internal state`，只证明 execution compatibility。
- UI、map presentation、network、Steam/cloud：独立 `PlatformGap`，不计入 gameplay content gate。

## 四路终态、digest 和错误分类

| 路径 | 终态/停止 | state digest | trace chain digest | route digest | error |
|---|---|---|---|---|---|
| primary | `Death / Completed` | `304aac9b1bf6302cafb5f56d95436fb72fe8cb63c2eb8e9002c27ba1c692c686` | `1e4ab19542b4e20c178b072f51676f85c8741c87de1b3e006dba126006ac11ea` | `3246dcb7d23c24c2bafd84588f9391111bcfca2986807352eb94345b949adfd2` | native Death |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | native Death |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | native Death；primary route replay |
| failure | `Unknown / BudgetExceeded` | — | `16faaa62abd1a64c942f454c274868d499649aac28f0c5c72efd32d883ba5b19` | — | controlled `BudgetExceeded` |

四路新运行均使用独立 process 和独立 `.build/runs` 子目录。failure 的 `final-state.json` 只保留 budget stop，不作为 state digest。

## 分母和保留缺口

- content inventory 总数：586。
- `Covered=2`：`SeizeMomentum`（既有接受证据）和本次 `Strike`。
- `Partial=7`：其余既有 Partial identities 保留原状态。
- `Unrun=577`。
- `Unsupported=0`、`RuntimeFailure=0` 个 content unit；早期诊断 process failure 只在独立 evidence 目录保留。
- visible-policy 的 `Unsupported` 和平台能力缺口为独立维度，未混入 content-unit 分母。
- 没有覆盖完整正式 save/load、玩家可见策略、UI、地图 presentation、network、Steam/cloud。

## 下一唯一入口

若继续 M2-2，只选择并验证 `CARD.YUKI_MAKOTO_CARD_DEFEND` 这一单一剩余 `Partial` starting-card 单元；不得回退 `SeizeMomentum` 或 `Strike`，不得接入第二角色、开始 M2-3 或关闭 #4。

是否提交或推送：**否**。
