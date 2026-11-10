# M2-2 YukiMakoto foundation/content 缺口审计交接（票据 #4）

## 阶段结论

**BLOCKED**。

本次只复核共享 foundation，未启动 Yuki 内容 probe、未启动新的 native game process、未接入第二个角色，也没有使用 Yuki 专用 adapter。注册/初始化的窄片仍可复核，但没有跨 Mod 或内容族可复用的真实证据闭合以下任一共享语义：重复注册/reload/注册失败与 teardown、callback/hook 顺序和异常中断、before-death 与解绑、奖励后跨房间状态、共享 hook/state 清理、注册表卸载、运行时 ALC teardown 和 failure cleanup。按 ADR-0001/0002/0003/0004 及 M2-0 foundation gate 规则，依赖 Mod 的完整接受必须停在 `BLOCKED`。

票据 #4 保持 `OPEN`、`ready-for-agent`；阻塞票 #3 已 `CLOSED`。不要把既有 Yuki flow PASS 提升为 content 或 foundation PASS。

## 票据、分支和工作区

- `gh issue view 4 --json number,title,body,labels,state,assignees`：退出码 `0`，#4 为 `OPEN`，标签 `ready-for-agent`。
- `gh issue view 3 --json number,title,labels,state`：退出码 `0`，#3 为 `CLOSED`。
- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- 工作区开始时已有 dirty 修改；本次没有 reset、clean、checkout、覆盖式复制、删除、提交或推送。既有修改仍保留：`README.md`、`docs/README.md`、`docs/agents/issue-tracker.md`、M2 计划和 prompt、`src/SimulatorLab.Core/NativeRunExecution.cs`、`tools/native-run/NativeScenario.cs`、`tools/native-run/Program.cs`、`tools/native-run/run-native-slice.ps1`，以及已有未跟踪的 GLOSSARY、ADR、M2 报告、`RunEvidenceContract` 和维护脚本。
- 本次实际新增仅为：`.build/runs/m2-2-yuki-foundation-audit-20261108-r2/` 下的三个审计 JSON，以及本报告。`.build/` 目录受忽略规则保护；没有修改源码、核心外部类型边界、Mod、快照、manifest 或既有证据。
- `git diff --check`：退出码 `0`；只报告工作区既有 LF/CRLF 提示。

## 固定输入核验

- 游戏：`v0.111.0`，commit `41cef1ea`；`.build/pinned-inputs/sts2-0.111.0/release_info.json` 与 `input-lock.json` 存在。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`，manifest `local-mods/workshop-snapshot-20261106-163352.manifest.json` 存在；manifest 记录 `21` 个条目、`142` 个文件，内容 SHA-256 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`，manifest SHA-256 `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- YukiMakoto：`v0.0.7-hot`，Workshop ID `3829924471`，精确 `lib/0.111.0/YukiMakoto.dll`，实际/manifest SHA-256 `442113614bbaf43cc988cf5f917f82be3a41dd57ee0a7d19b1e6b5ccb360caa2`。
- RitsuLib：精确 `compat/0.111.0`；`STS2-RitsuLib.Runtime.dll` 实际/manifest SHA-256 `3df9987d226b6d88b09727d19e99e51d3cf76c90d819d1cd04cf191d8b87fcf8`，`STS2-RitsuLib.dll` 实际/manifest SHA-256 `6cc6b9df7b892ed18480ad528168a804af46e24e376f0a77cfa26b57900c1b11`。
- 依赖闭包：`STS2-RitsuLib compat/0.111.0 -> YukiMakoto lib/0.111.0`。
- canonical seed：`LAB_M2_2_YUKI_01`。

## 执行命令、退出码和证据

| 检查 | 退出码 | 证据 |
|---|---:|---|
| `. .\\tools\\maintenance\\Set-GitHubSession.ps1; gh issue view 4 --json number,title,body,labels,state,assignees` | 0 | #4 `OPEN` / `ready-for-agent` |
| `. .\\tools\\maintenance\\Set-GitHubSession.ps1; gh issue view 3 --json number,title,labels,state` | 0 | #3 `CLOSED` |
| 固定快照、manifest、pinned game、RitsuLib/Yuki exact variant 存在性与 SHA-256 核对 | 0 | 既有冻结摘要和三份 exact DLL manifest hash 全部匹配 |
| 读取并 `ConvertFrom-Json` 验证既有 contract/runs/content/lifecycle/interaction 与 foundation audit JSON | 0 | 既有 `m2-2-yuki-20261108/` 和 `m2-2-yuki-foundation-audit-20261108/` 可解析 |
| 既有 Yuki primary trace event-kind 检查 | 0 | 仅确认角色、战斗、奖励、死亡和 combat cleanup 边界；没有共享 foundation 闭环 |
| `git diff --check` | 0 | 无新增 whitespace error |

新的审计证据目录为 [m2-2-yuki-foundation-audit-20261108-r2](../../../.build/runs/m2-2-yuki-foundation-audit-20261108-r2/)，包含：

- `canonical-input.json`：固定输入、依赖闭包、worker、配置、adapter/collector 和来源证据。
- `foundation-audit.json`：四项共享 foundation 检查及阻塞决定。
- `evidence-index.json`：既有证据保留、矩阵未变更、命令结果和 `newNativeRuns=0` / `contentProbes=0`。

## 共享 foundation 审计

| 共享语义 | 状态 | 已有真实观察 | 缺失的跨 Mod 可复用证据 | 对依赖内容 |
|---|---|---|---|---|
| 注册身份、重复注册、reload、注册失败、teardown | `PARTIAL` | exact RitsuLib/Yuki 加载成功；身份为 `REG.YUKI.lib/0.111.0#1@0.111.0`；静态 worker 只验证文件和 collectible ALC 发现 | 没有独立 fixture 证明重复注册的拒绝/幂等、reload 身份保持、失败回滚或运行时注册表 teardown | 阻塞 |
| callback/hook 顺序、异常中断、before-death、解绑 | `BLOCKED` | Yuki trace 有角色/战斗/奖励/死亡/cleanup；LexNinja2 只有窄 Card/Relic/Power 片 | turn/combat/reward/before-death 顺序、异常中断、ownership、callback detach 未闭合；Stage 7 明确 `BeforeDeath` 和真实死亡中断未运行 | 阻塞 |
| 奖励后跨房间 secondary resource、Power/Relic、registration、callback ownership | `BLOCKED` | Yuki 到达 reward-complete 和第二战/死亡边界 | cross-room persistence 为 `Unrun`，post-reward map 为 `Partial`；没有跨 Mod 证据证明 secondary resource、Power/Relic 状态和 callback ownership 连续 | 阻塞 |
| 共享 hook/state 清理、callback detach、注册表卸载、ALC teardown、failure cleanup | `BLOCKED` | Yuki 只有 native Death 后 combat cleanup；只读 worker 的 ALC unload 属于静态发现；Minion 只有一个 combat cleanup | 没有运行时共享 hook 移除、状态清理、回调解绑、注册表卸载、运行时 ALC teardown 或 failure cleanup 的跨族证据 | 阻塞 |

既有 narrow evidence 不能满足 foundation gate：Stage 7 明确 `RitsuLib secondary resource`、真实死亡路径和 `BeforeDeath` 未接入；静态 worker 的 `assemblyLoadContextUnloaded=true` 不等于 Mod runtime hook/state teardown；Minion cleanup 只覆盖 Minion 族的一个实例。

## Flow、content、stability 和内容矩阵

- Foundation：`BLOCKED`；依赖 Mod 的完整接受为 `BLOCKED`。
- Flow：`PASS`，沿用既有接受证据，没有重跑或重写。
- Content：`PARTIAL`；foundation 阻塞期间没有继续内容族 probe。
- Stability：`PASS`，沿用既有 primary/repeat/replay 一致性；failure 保留独立预算停止。
- 阶段聚合：`BLOCKED`，阻塞优先于既有 flow PASS。

既有 runtime inventory 保持 `586` 个身份，registration identity 为 `REG.YUKI.lib/0.111.0#1@0.111.0`：

| 类型 | 数量 | 生命周期矩阵状态 |
|---|---:|---|
| Card | 270 | `Partial`：发现/注册、起始卡组构造、有限合法 play、reward selection、cleanup boundary；全量 target/effect、生成、跨回合/跨房间和异常清理未闭合 |
| Character | 1 | `Partial`：发现/注册、选择、初始状态、combat entry、death、combat cleanup；run teardown、注册表卸载、跨房间状态和 failure interruption 未闭合 |
| Other | 30 | `Unrun`：只有 inventory discovery；类型特有 callback 和 cleanup 未运行 |
| Power | 281 | `Unrun`：construction、activation/stack、expiry/cleanup 未运行 |
| Relic | 4 | `Partial`：发现/注册、起始 relic、combat/reward boundary、cleanup boundary；全量 trigger、BeforeDeath、跨房间计数、hook detach 未闭合 |

内容单元状态分母没有变更：`Covered=0`、`Partial=8`、`Unrun=578`、`Unsupported=0`、`RuntimeFailure=0`。8 个 `Partial` 仍为 1 个 Character、3 个起始 Card、3 个起始 Relic 和 1 个奖励 Card；没有删除、折叠或升级任何既有身份。

交互风险保持如下：

| 交互契约 | 状态 | 缺口 |
|---|---|---|
| 依赖加载顺序 | `Covered`（加载边界） | 不等于 shared foundation closure |
| 角色选择与初始状态 | `Covered` | 仅该 Yuki 身份 |
| Card target/effect | `Partial` | 未观察身份、生成卡和复杂 target |
| Reward selection | `Covered`（有限边界） | 空/重复奖励未形成共享 fixture |
| Room/map progression | `Partial` | post-reward transition 未闭合 |
| Death and cleanup | `Covered`（战斗边界） | shared teardown 仍缺 |
| Cross-room persistence | `Unrun` | secondary resource/Power/Relic/registration/callback ownership 未证明 |
| Visible-policy boundary | `Unsupported` | TestMode/internal state 只证明 execution compatibility |

平台能力继续单独计列：正式 UI/headless presentation、正式 save/load、map presentation/injection、network/replay network、Steam、cloud、screen-shake/FTUE 等未被 gameplay gate 覆盖；本次也没有把它们升级为 foundation 证据。

## 四路终态、digest 和错误分类

以下全部沿用既有 `.build/runs/m2-2-yuki-20261108/`，本次未重跑、覆盖或替换：

| 路径 | 终态/停止 | state digest | RNG digest | trace digest | route digest | 错误分类 |
|---|---|---|---|---|---|---|
| primary | `Death / Completed` | `79ca17c18aadeac4b979bca04eceb87f4711a5f6a7500941ee8f63ba89e3d8e2` | `620a655bd2e4574686aa860b3b01af30ebc16020f4e9b556db479b1d3e1a4f09` | `dd8513bec6fa299b3f474a04c9b8a63bcebeb7762755b5a957443e0c3fe26f5b` | `b6a490a8c33fbd471f9457bc284c63dd0b7a4d4c83c20ad37e1fa4b1184070e7` | 真实 native Death |
| repeat | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 真实 native Death |
| replay | `Death / Completed` | 同 primary | 同 primary | 同 primary | 同 primary | 真实 native Death；状态/RNG/trace/route 一致 |
| failure | `Unknown / BudgetExceeded` | `a54b5f510189b75e2e579087059defd4d52c19288fd421616e227542e193d5ba` | `dbcbe2b7e6f06c4ed23e2b24cf9212c373cf8059f2cbfe6b55ed782706893b30` | `b079bbeaf580370e99d734f950aabd476277cdb3e7430f116d921560e0f7c9b8` | 无 | `BudgetExceeded`，未提升为 Death |

未覆盖语义分母：

- `Partial`：content unit `8`；foundation 四项共享缺口、Card/Relic effect、post-reward room/map、跨房间 persistence。
- `Unsupported`：content unit `0`；visible-policy `1` 项以及 UI、正式 save/load、map presentation/injection、network/replay network、Steam/cloud 等平台能力。
- `Unrun`：content unit `578`；其中 Power `281`、Other `30` 全部未运行，另有未触发 Card/Relic、复杂生成、完整 failure callback 和共享跨房间语义。
- `RuntimeFailure`：最终 accepted cohort content unit `0`、四路运行 `0`；早期探索失败目录仍保留，没有被改写为成功。
- `Covered`：content unit `0`；已观察的依赖加载、角色选择、奖励和战斗 cleanup 只是边界证据。

## 下一唯一入口

建立一个**不含 Yuki 名称、Workshop ID 或单包分支的共享 RitsuLib foundation fixture**，在独立 native worker/game process 中真实覆盖：注册/重复注册与失败回滚、callback/hook 顺序和异常中断、before-death 与解绑、奖励后跨房间 secondary resource/Power/Relic/registration/callback ownership、hook/state/注册表/ALC cleanup 及 failure cleanup。fixture 闭合并有跨 Mod/内容族回归后，才回到 Yuki 选择最小内容族；在此之前保持 #4 OPEN，不关闭票据，不开始 M2-3。

是否提交或推送：**否**。
