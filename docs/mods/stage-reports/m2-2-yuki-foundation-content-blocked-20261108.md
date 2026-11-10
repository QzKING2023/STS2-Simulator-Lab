# M2-2 YukiMakoto foundation/content 续作交接报告

## 阶段状态

**BLOCKED**。

本续作先审计共享 RitsuLib foundation，再决定是否进入 Yuki 内容族。精确 `compat/0.111.0` 注册和初始化可复现，但共享 callback/hook 生命周期、奖励后跨房间状态和共享 cleanup/teardown 仍没有闭合证据。按照 M2-0、ADR-0001/0002/0003/0004，依赖 Mod 的内容 probe 不能在这个前置下继续；没有使用 Yuki 专用 adapter，没有改写或升级既有 flow/content 结论。

#4 保持 `OPEN`、`ready-for-agent`；#3 已关闭。没有开始 M2-3，没有接入第二个角色。

## 分支、HEAD、工作区与输入保护

- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 开始前 `git status --short --branch` 已记录既有 dirty 工作区；`README.md`、`docs/README.md`、`docs/agents/issue-tracker.md`、`docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md`、`docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md`、`src/SimulatorLab.Core/NativeRunExecution.cs`、`tools/native-run/NativeScenario.cs`、`tools/native-run/Program.cs`、`tools/native-run/run-native-slice.ps1` 以及既有未跟踪的 GLOSSARY/ADR/阶段报告/运行协议文件均保留。
- 本续作实际新增：`.build/runs/m2-2-yuki-foundation-audit-20261108/canonical-input.json`、`foundation-audit.json`、`evidence-index.json`，以及本报告。
- 未执行 `reset`、`clean`、`checkout`、覆盖式复制、删除、提交或推送。
- 未修改固定快照、快照 manifest、游戏输入、Mod DLL/PCK/资源、Mod 原有代码、核心外部类型边界或已接受四路证据。

## 固定输入和存在性核验

- 游戏：`v0.111.0`，commit `41cef1ea`；`.build/pinned-inputs/sts2-0.111.0/release_info.json` 和 `input-lock.json` 存在。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`，存在；manifest `local-mods/workshop-snapshot-20261106-163352.manifest.json` 存在。
- 快照内容 SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- YukiMakoto：`v0.0.7-hot`，Workshop ID `3829924471`，精确 `3829924471/lib/0.111.0/YukiMakoto.dll`，SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- RitsuLib：精确 `3747602295/compat/0.111.0`；Runtime SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，loader SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- 依赖闭包：`STS2-RitsuLib compat/0.111.0 -> YukiMakoto lib/0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_01`。审计采集器：`SimulatorLab.FoundationEvidenceAudit` / `m2-2-foundation-audit/v1`；本续作没有启动新的 native game process。

## 执行命令、退出码和证据目录

| 命令/检查 | 退出码 | 结果/证据 |
|---|---:|---|
| `. .\\tools\\maintenance\\Set-GitHubSession.ps1; gh issue view 4 --json number,title,body,labels,state` | 0 | #4 `OPEN`，标签 `ready-for-agent` |
| `. .\\tools\\maintenance\\Set-GitHubSession.ps1; gh issue view 3 --json number,title,state,labels` | 0 | #3 `CLOSED`，确认阻塞已关闭 |
| `git branch --show-current; git rev-parse HEAD; git status --short --branch` | 0 | `main`、HEAD 与 dirty 状态已记录 |
| 固定输入/精确变体 `Test-Path`、`Get-FileHash`、`release_info.json`/`input-lock.json` 读取 | 0 | 快照、manifest、`0.111.0`、Yuki/Ritsu exact variant 均存在且摘要匹配 |
| 读取 M2 ADR、M2-0/M2-1/M2-2 报告、既有五个证据索引 | 0 | foundation 与 append-only 现状可复核 |
| 写入只读审计索引 | 0 | 新目录 `.build/runs/m2-2-yuki-foundation-audit-20261108/` |
| `ConvertFrom-Json` 验证新审计 JSON | 0 | 三个新 JSON 可解析 |
| `git diff --check` | 0 | 无新增 whitespace error |

新证据目录只保存审计输入、foundation 检查和既有证据索引；它没有伪造新的 lifecycle 或 terminal run。

## Foundation 审计决定

| 共享语义 | 现有事实 | 本续作结论 | 阻塞原因 |
|---|---|---|---|
| 注册 | exact RitsuLib/Yuki 已加载；`REG.YUKI.lib/0.111.0#1@0.111.0` 已见证 | `PARTIAL` | 共享重复注册、reload、注册失败和 teardown 语义未证明 |
| callback/hook | LexNinja2 只覆盖窄卡牌/遗物/Power 边界；Yuki trace 覆盖角色、奖励和死亡事件 | `BLOCKED` | turn/combat/reward/before-death 的共享顺序、异常中断和解绑未闭合；Stage 7 明确保留真实死亡/BeforeDeath 为未运行 |
| 跨房间状态 | Yuki 已到达首战奖励和第二战死亡边界 | `BLOCKED` | 奖励到下一房间的 secondary resource、Power/Relic 状态、注册身份和 callback ownership 仍 `Unrun`；post-reward map transition 仅 `Partial` |
| cleanup/teardown | Yuki 观察到 combat cleanup；Minion 窄片有独立 cleanup | `BLOCKED` | Ritsu 共享 hook 移除、共享状态清理、callback detach、注册表卸载和 ALC teardown 未证明 |

这四项中 callback、跨房间和 cleanup 已足以阻止依赖 Mod 的完整接受。静态加载成功或 Yuki 的真实 Death 不能把 foundation gate 提升为 `PASS`。不得用 Yuki 专用 shim 或 adapter 绕过。

详细审计证据见 [foundation-audit.json](../../../.build/runs/m2-2-yuki-foundation-audit-20261108/foundation-audit.json) 和 [canonical-input.json](../../../.build/runs/m2-2-yuki-foundation-audit-20261108/canonical-input.json)。

## Flow、content、stability 状态

- Foundation gate：`BLOCKED`（继承 M2-0 的 `PARTIAL/BLOCKED for dependent full-run acceptance`；本续作没有新增共享语义）。
- Flow gate：`PASS`，沿用已接受证据；没有重跑或改写。
- Content gate：`PARTIAL`，不能在 foundation 阻塞时推进到新的内容族。
- Stability：`PASS`，沿用已接受 `primary/repeat/replay` 一致性；failure 为独立预算隔离。
- 阶段聚合：`BLOCKED`。依赖前置阻塞优先于已有 flow PASS；#4 保持 OPEN。

### Content unit 与 registration identity

既有 append-only content matrix 未修改，registration identity 仍为 `REG.YUKI.lib/0.111.0#1@0.111.0`，总数 586：Card 270、Character 1、Other 30、Power 281、Relic 4。

8 个已有 `Partial` 单元：

- `CHARACTER.YUKI_MAKOTO_CHARACTER_YUKI_MAKOTO`
- `CARD.YUKI_MAKOTO_CARD_STRIKE`
- `CARD.YUKI_MAKOTO_CARD_DEFEND`
- `CARD.YUKI_MAKOTO_CARD_DARK_HOUR`
- `CARD.YUKI_MAKOTO_CARD_DEFENSE_MEMORY`
- `RELIC.YUKI_MAKOTO_RELIC_WILD_CARD`
- `RELIC.YUKI_MAKOTO_RELIC_VELVET_ROOM`
- `RELIC.YUKI_MAKOTO_RELIC_MEANING_OF_SEEKING`

冻结分母：`Covered=0`、`Partial=8`、`Unrun=578`、content-unit `Unsupported=0`、content-unit `RuntimeFailure=0`。没有删除、折叠或重新分类任何身份；新审计索引明确 `appendOnlyUpdate=false`，因为 foundation 被阻塞。

### Lifecycle matrix

| 内容类型 | 已有适用检查点 | 当前状态 | 未闭合语义 |
|---|---|---|---|
| Character | discovery/registration、selection、initial state、combat entry、death、cleanup | `Partial` | run teardown、注册表卸载、跨房间状态和 failure interruption |
| Card | discovery/registration、starting deck construction、部分合法 play、reward selection、cleanup boundary | `Partial` | 全量 target/effect、生成/弃牌/耗尽、跨回合/跨房间和异常清理 |
| Relic | discovery/registration、starting relic construction、combat/reward boundary、cleanup boundary | `Partial` | trigger 全集、BeforeDeath、跨房间计数、hook detach |
| Power | discovery/registration 尚在 inventory；construction、activation/stack、expiry/cleanup 未运行 | `Unrun` | 全部生命周期和共享 callback 语义 |
| Other | discovery/registration 在 inventory；applicable callback、cleanup 未运行 | `Unrun` | 类型特有 callback 与 teardown |

原始矩阵仍在 [lifecycle-matrix.json](../../../.build/runs/m2-2-yuki-20261108/lifecycle-matrix.json)。

### Interaction risk table

| 交互契约 | 状态 | 证据/缺口 |
|---|---|---|
| 依赖加载顺序 | `Covered`（仅加载边界） | Yuki primary manifest；不等于 shared foundation closure |
| 角色选择与初始状态 | `Covered` | Yuki primary trace |
| Card target/effect | `Partial` | 只观察有限合法卡牌动作，未知生成/复杂 target 未运行 |
| Reward selection | `Covered`（有限边界） | GoldReward/CardReward/reward-complete |
| Room/map progression | `Partial` | Neow→首个 Monster 已见；post-reward transition 未闭合 |
| Death and cleanup | `Covered`（战斗边界） | native Death 后 combat cleanup；共享 teardown 仍缺 |
| Cross-room persistence | `Unrun` | reward 后 secondary resource/Power/Relic/registration 未证明 |
| Visible-policy boundary | `Unsupported` | TestMode/internal state collector；不证明 visible-policy compatibility |

原始风险表仍在 [interaction-risk.json](../../../.build/runs/m2-2-yuki-20261108/interaction-risk.json)。

### 平台能力缺口

UI/headless presentation、正式 save/load、map presentation/injection、network/replay network、Steam、cloud，以及 screen-shake/FTUE 等表现层 callback 仍是独立 platform capability。它们不计入 content gate；当前 collector 只能证明 execution compatibility，不能证明 visible-policy compatibility。

## 四路终态、digest 和错误分类

以下全部沿用原始 `.build/runs/m2-2-yuki-20261108/`，本续作没有重新运行、覆盖或替换它们：

| 路径 | 终态/停止 | state digest | RNG digest | trace digest | route digest | 错误分类 |
|---|---|---|---|---|---|---|
| primary | `Death / Completed` | `79ca17c18aadeac4b979bca04eceb87f4711a5f6a7500941ee8f63ba89e3d8e2` | `620a655bd2e4574686aa860b3b01af30ebc16020f4e9b556db479b1d3e1a4f09` | `dd8513bec6fa299b3f474a04c9b8a63bcebeb7762755b5a957443e0c3fe26f5b` | `b6a490a8c33fbd471f9457bc284c63dd0b7a4d4c83c20ad37e1fa4b1184070e7` | 真实 native Death |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 真实 native Death |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 真实 native Death；状态/RNG/trace/route 一致 |
| failure | `Unknown / BudgetExceeded` | `a54b5f510189b75e2e579087059defd4d52c19288fd421616e227542e193d5ba` | `dbcbe2b7e6f06c4ed23e2b24cf9212c373cf8059f2cbfe6b55ed782706893b30` | `b079bbeaf580370e99d734f950aabd476277cdb3e7430f116d921560e0f7c9b8` | 无 | `BudgetExceeded`，未提升为 Death |

canonical input digest：成功三路 `73ecc49093727f9036a390326fcae06742e2c29a9ed638d1fdaf147da49c1f8a`；failure 为 `c57eb08cf82e9361c714e0b6904c2cdcb0d6813be734411edaa015086ff586db`。

## 未覆盖分母与保留语义

- `Partial`：content unit 8；foundation 的注册/共享语义、Card/Relic effect、post-reward map/room 和跨房间 persistence 仍不闭合。
- `Unsupported`：content unit 0；语义/platform 侧为 visible-policy collector 1 项，以及 UI、正式 save/load、map presentation/injection、network/replay network、Steam/cloud 等平台能力；这些不被宣称为 gameplay 兼容。
- `Unrun`：content unit 578；其中 Power 281、Other 30 全部未运行，另外还有未触发 Card/Relic、复杂生成、完整 failure callback 和共享跨房间语义。
- `RuntimeFailure`：最终 accepted cohort 的 content unit 0、四路运行 0；早期探索 RuntimeFailure 目录保留在原 `.build/runs/`，没有被改写为成功。
- `Covered`：content unit 0；依赖加载、角色选择、奖励领取和战斗 cleanup 仅是 interaction/lifecycle 边界，不足以把对应 unit 提升为 Covered。
- `RunVictory`：本片没有运行；已有三路真实 `Death` 仍然保留。

## 下一唯一入口

先建立一个**跨 Mod/内容族可复用的共享 RitsuLib foundation fixture**，用独立 native worker 真实闭合：注册/重复注册与失败、callback/hook 顺序和异常中断、奖励后跨房间状态、hook/state/ALC cleanup 及 failure cleanup。该 fixture 必须不含 Yuki 名称、Workshop ID 或单包分支；foundation 未闭合前不要新增 Yuki 内容 probe、不要关闭 #4、不要开始 M2-3。

是否提交或推送：**否**。
