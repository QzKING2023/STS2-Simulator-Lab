# M2-4 LexNinja2 全流程交接报告

## 阶段状态

**PASS**。本阶段冻结的 162 个 LexNinja2 content unit 已全部完成真实注册和生命周期 probe，content matrix 为 `Covered=162`、`Partial=0`、`Unsupported=0`、`Unrun=0`、`RuntimeFailure=0`。Primary、Repeat、Replay 和 Failure 四路已用最新 worker 重跑并保留最终证据。

交互风险表仍单独记录 1 项 Partial 和 1 项 Unrun。它们是 Relic hook-specific trigger 的独立覆盖，以及 Lex Kela secondary-resource 增减语义。两项不再形成 content unit 的 Unsupported 或 Unrun 分母。

## 票据、分支和工作区

- 子票为 #10。开工前票据为 open，标签为 `ready-for-agent`，阻塞票 #5、#6、#7、#8 均已关闭。
- 当前分支为 `main`。
- HEAD 为 `9f6a6fc9c37c2c895777ea48d50910cd46be7f98`。
- 已有用户修改保持原样。`README.md` 和 `docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md` 仍有未提交修改。
- 本阶段更新了本报告，并保留了供电暂停期间的 [进度记录点](D:/STS2-Simulator-Lab/docs/mods/stage-reports/m2-4-lexninja2-progress-checkpoint-20261111.md)。
- 未修改核心模拟器源码，未修改 Mod DLL、PCK、资源和 manifest，未提交，未推送，未创建远程 PR。
- #10 已处于 CLOSED。最终证据评论已送达 [issue comment](https://github.com/QzKING2023/STS2-Simulator-Lab/issues/10#issuecomment-6811191261)。本机用户级代理为 `http://127.0.0.1:10808`。先前的 `127.0.0.1:9` 来自未加载 helper 的新 PowerShell 进程，已通过同一命令内 dot-source `Set-GitHubSession.ps1` 修正。

## 固定输入和运行证据

- 游戏版本：`0.111.0`，commit `41cef1ea`。
- 固定 Workshop 快照：`local-mods/workshop-snapshot-20261106-163352`。
- 快照 content sha256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 快照 manifest sha256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- LexNinja2：Workshop `3747530622`，版本 `v3.0.11-beta-0-g358712f`，DLL sha256 `9653a3419a07002fadb4267621ec2469554555cff971045330864f7cd9e75274`。
- RitsuLib：`0.6.10`，精确变体 `3747602295/compat/0.111.0`，无回退。
- 依赖顺序：BaseLib `3.4.7` → STS2-RitsuLib `0.6.10/compat/0.111.0` → LexNinja2 `v3.0.11-beta-0-g358712f` → isolated worker。
- canonical seed：`LAB_M2_4_LEXNINJA2_01`。
- 最新 cohort canonical digest：`08077f58b49b36dd9db9a0b85fcc68463ef0635c62ae1cc2de4103abe819acb3`。
- 最新 cohort 根目录：`.build/runs/m2-4-lexninja2-completion12-20261111`。
- 矩阵和索引：`.build/runs/m2-4-lexninja2-20261111/content-matrix-final.json`、`.build/runs/m2-4-lexninja2-20261111/interaction-risk-final.json`、`.build/runs/m2-4-lexninja2-20261111/evidence-index.json`。

## Primary、Repeat、Replay、Failure

四路使用同一 canonical digest。Primary、Repeat 和 Replay 的终态、状态、RNG、trace 和 route digest 相同。

| 路径 | 终态或 stop | raw exit | state digest | RNG digest | trace digest | route digest | replay |
| --- | --- | ---: | --- | --- | --- | --- | --- |
| `primary-final-20261112` | `Death / Completed` | 0 | `53b5225154169ef25697733176ef3ba1d7782c22deb4d0237362547ca3ee2aea` | `bc0c03bd457d0df44cb5ca7eea89ff7c4cde6b7927b192a75963bcc2781f34ca` | `d2b4ff82964130a7543ed1accc68215ac4c83dba5600c64c3dcd430b7f400632` | `f6064f3db05ab3a897bb9186e672b6e55115bd164a0b08d18de06abb504b3243` | — |
| `repeat-final-20261112` | `Death / Completed` | 0 | same as primary | same as primary | same as primary | same as primary | — |
| `replay-final2-20261112` | `Death / Completed` | 0 | same as primary | same as primary | same as primary | same as primary | `matched=true` |
| `failure-final-20261112` | `Unknown / BudgetExceeded` | 0 | `ec5f3bab7488cda9b45de7c2405402101a51d9b93a4fc2163d53d131498cf005` | `bc0c03bd457d0df44cb5ca7eea89ff7c4cde6b7927b192a75963bcc2781f34ca` | `5a647f912e53fde603117f6ec88b155b570758e14033e706ca18b4065f8a850f` | — | — |

Failure 在声明边界停止，没有以预测死亡或原版替代行为冒充成功。旧 cohort 和早期失败目录保留在 `.build/runs` 中，没有混入最新索引。

## Foundation gate、加载身份和 RitsuLib 语义

Foundation gate 为 `PASS`。最终加载身份见最新 primary 的 `game-output/load-identity.json`。Host 和 runtime 均为 `0.111.0`，exact compat/0.111.0 被选中，BaseLib、RitsuLib、LexNinja2 和 isolated worker 均为 Loaded，LexNinja2 在 RitsuLib 之后加载。ModelDb 发现 162 个 LexNinja2 identity，包含角色、103 Card、9 Relic、37 Power 和 12 Other。

真实执行的 RitsuLib 相关语义包括角色注册、content registry、reward 边界、combat hook listener 生命周期、LexNinja2 singleton hook owner、native card lifecycle 和跨房间清理。RitsuLib 真实注册 `LEX_NINJA2_SECONDARY_RESOURCE_LEX_KELA` 及两个 node attachment，但本阶段没有触发资源增减回调。

## Content unit、lifecycle 和 interaction 矩阵

冻结 inventory 分母为 162。

- Character：1，Covered 1。
- Card：103，Covered 103。
- Power：37，Covered 37。
- Relic：9，Covered 9。
- Other：12，Covered 12。
- Content matrix 分母：`Covered=162`、`Partial=0`、`Unsupported=0`、`Unrun=0`、`RuntimeFailure=0`。

两个原先 Unsupported 卡牌已完成真实生命周期。

- `CARD.LEX_NINJA2_CARD_I_SEE_YOU` 在真实 hand 中等待 turn end，触发 `OnTurnEndInHand`，记录 effect 和 cleanup。
- `CARD.LEX_NINJA2_CARD_MOSQUITO_HAND` 通过真实 `CardReward.OnSelect` 入手，进入 deck，触发 `AfterRoomEntered`，记录 cleanup。

12 个 Other 的成功证据如下。

- CardPool：真实注册、`AllCards` 发现和清理，见 `other-cardpool`。
- RelicPool：真实注册、`AllRelics` 发现和清理，见 `other-relicpool`。
- PotionPool：真实注册、`AllPotions` 发现，观察到空池，完成清理，见 `other-potionpool14`。
- 四个 Event：通过原生 `EventRoom`、真实 option callback、完成状态和 `ExitCurrentRooms` 清理，见 `other-drink2`、`other-event-manyudian`、`other-event-greatseal`、`other-event-spectre`。
- ManYuDianEncounter：使用 mutable encounter 进入原生 `CombatRoom`，用受控 HP 和 energy precondition 完成真实 Victory 和 cleanup，见 `other-encounter17`。
- 四个 singleton：通过角色 full-run、combat hook listener 生命周期和 cleanup，见 `other-singleton-dirty`、`other-singleton-holy`、`other-singleton-lexkela`、`other-singleton-science`。

Interaction risk 表为 8 Covered、1 Partial、1 Unrun。Partial 是 9 个 Relic 的独立 hook-specific trigger 组合仍未逐个隔离。Unrun 是 Lex Kela secondary-resource 增减回调。两项均已从 content matrix 分母中区分出来。

## 验收标准

- LexNinja2 registration identity 和 exact RitsuLib compat/0.111.0：通过。
- 依赖顺序和加载身份审计：通过。
- 角色、Card、Power、Relic、Other 真实发现：通过。
- 至少一个 RitsuLib 特有 combat hook 语义真实执行：通过，singleton combat hook lifecycle 和 reward/content hook dispatch 均有 native evidence。
- Primary、Repeat、Replay、Failure：通过。
- Native Victory/Death full-run coordinator：通过。标准 cohort 以真实 Death 完成，Encounter probe 以真实 Victory 完成。
- 完整 content gate：通过，162 个 unit 全部 Covered。
- 核心模拟器不引用 Mod、Godot 或外部 DTO：通过。本阶段只使用 isolated worker 和证据目录。
- 平台能力：Audio Dummy 和 muted buses 已观察。visible policy、windowed UI、正式存档和设置、map presentation、network、Steam、cloud 未纳入 gameplay gate。

## 执行命令和退出码

- `dotnet build .build/runs/m2-4-lexninja2-completion12-20261111/worker/NativeLifecycleProbe.csproj -c Release --no-restore`：0。
- `pwsh run-path.ps1 -Kind other-primary ...`：所有 12 个 Other 成功退出 0，终态为 `ContentLifecycleCompleted / Completed`。
- `pwsh run-path.ps1 -Kind primary -OutName primary-final-20261112`：0。
- `pwsh run-path.ps1 -Kind repeat -OutName repeat-final-20261112`：0。
- `pwsh run-path.ps1 -Kind replay -OutName replay-final2-20261112`：0，`matched=true`。
- `pwsh run-path.ps1 -Kind failure -OutName failure-final-20261112`：0，终态 `Unknown / BudgetExceeded`。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`：0，全部 137 项 contract checks 通过。
- `git diff --check`：0。

## 已知缺口、状态分母和平台能力缺口

- Content matrix 的 Partial、Unsupported、Unrun、RuntimeFailure 分母均为 0。
- Interaction risk 仍有 1 Partial 和 1 Unrun，分别是 Relic hook-specific trigger 组合和 Lex Kela secondary-resource 增减回调。
- 可见策略、windowed rendering、正式 save/load、map presentation、network、Steam 和 cloud 只作为 platform capability 记录，没有计入 content gate。
- 早期 `other-encounter` RuntimeFailure、旧 PotionPool Death 和旧 replay path 失败目录均保留原始证据，没有被重写成通过。

## 下一阶段唯一建议入口

若继续扩大语义覆盖，唯一入口是 Lex Kela secondary-resource 增减回调的受控 native probe。不要重新打开已通过的 162-unit content gate，也不要扩大到第二个角色。

是否提交或推送：**否**。
