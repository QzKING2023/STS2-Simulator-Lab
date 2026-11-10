# M2-0 目标、术语、基础和主分母冻结报告

## 阶段状态

**BLOCKED（本地审计和文档交付完成；票据操作未完成）**。

M2-0 的只读审计、术语冻结、foundation 语义清单、主分母、内容单元/注册身份、生命周期矩阵、交互风险表、canonical input 和状态聚合规则已写入本报告、`GLOSSARY.md` 和 ADR-0005。没有修改运行时代码、固定快照、manifest、DLL、PCK 或资源。

阶段仍标为 `BLOCKED` 有两个可复核原因：

1. `gh issue view 2 --json number,title,body,labels,state` 在本机仍因 API 代理拒绝而退出码 `1`；GitHub 连接器成功复核票据 #2 为 `open`、`ready-for-agent`、`Blocked by: None`。
2. 按仓库协议认领票据时，GitHub 连接器 `add_issue_assignees(#2, QzKING2023)` 返回 HTTP `403 Resource not accessible by integration`。因此没有伪造认领、评论或关闭动作。

## 分支、HEAD 和工作区

- 分支：`main`
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`
- 阶段开始时已有修改保留：`README.md`、`docs/README.md`、`docs/agents/issue-tracker.md`、`docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md`、`docs/prompts/MOD_FULL_RUN_PHASE_PROMPTS.md`；已有未跟踪 `GLOSSARY.md`、`docs/adr/`、`tools/maintenance/Set-GitHubSession.ps1`。
- 本阶段新增/修改：`GLOSSARY.md`（追加冻结边界术语）、`docs/adr/0005-m2-0-frozen-denominator-and-evidence-contract.md`、本报告。
- 未执行 `reset`、`clean`、`checkout`、删除、提交、推送或远程 PR。

## 固定输入和审计来源

| 输入 | 冻结值/结果 |
| --- | --- |
| 目标游戏 | `0.111.0`，pinned game commit `41cef1ea` |
| 固定快照 | `local-mods/workshop-snapshot-20261106-163352`，存在 |
| manifest | `local-mods/workshop-snapshot-20261106-163352.manifest.json`，存在 |
| 快照条目/文件/大小 | `21 / 142 / 6,974,558,103 bytes` |
| SnapshotContentSha256 | `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73` |
| manifest 摘要（历史 run manifest） | `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3` |
| 静态 adapter | `mod-adapter/v1` |
| worker collector | `SimulatorLab.ModWorker`, `mod-worker-discovery/v1`, `mod-worker-run/v1` |
| native collector | 隔离 NativeRun/GameProbe；每个 probe 必须在自己的 `.build/runs/<new-run>` 目录写 manifest、canonical input 和 collector 摘要 |
| 当前矩阵证据 | `.build/runs/mod-matrix-repair-20261108-20261108-021117-429-01112138a1224ec5aa5dacf7eb533c4a/summary.json`；21/21 succeeded、0 failed、0 blocked（静态发现/隔离，不是玩法通过） |
| 精确变体 | RitsuLib `compat/0.111.0`；YukiMakoto `lib/0.111.0`；缺少精确目录时 fail closed，禁止回退 |

固定快照、manifest、DLL、PCK 和资源均作为只读输入。没有读取实时 Workshop，也没有加载第三方 Mod 入口点。

## 21 条目分类和处理

分类同时依据 manifest 的 `affects_gameplay`、依赖关系和已有静态/真实能力发现。表中的 `Supported` 只描述静态加载/隔离或已声明窄切片；它不等于 flow gate 或 content gate 通过。

| Workshop ID | 条目 | manifest/实际发现 | M2-0 分类 | 处理决定 |
| --- | --- | --- | --- | --- |
| 3737322022 | QuickRestart | `affects_gameplay=false`；BaseLib 依赖；QoL DLL/PCK | functional | 只记录 worker 加载/隔离；不进角色主分母 |
| 3737335127 | BaseLib | 共享库；被多个条目依赖 | dependency | foundation gate；不作为独立角色成功 |
| 3737336234 | MintySpire2 | `affects_gameplay=false`；QoL/UX；BaseLib 依赖 | functional | 只记录加载/隔离和声明的资源表面 |
| 3747492505 | Watcher | `affects_gameplay=true`；角色、卡牌、遗物、Power、首战 probe | role-bearing | 主分母；已有窄 probe，完整双门仍未闭合 |
| 3747508091 | Downfall | `affects_gameplay=true`；多角色 DLL/PCK 包 | role-bearing | 主分母；先选一个角色最小纵切片 |
| 3747508952 | figure_Saya | `affects_gameplay=true`；角色 DLL/PCK | role-bearing | 主分母；角色选择/初始状态为首片 |
| 3747515571 | Remilia | `affects_gameplay=true`；RitsuLib 依赖 | role-bearing | 主分母；先复用精确 RitsuLib 变体做发现 |
| 3747528152 | intentgraph2 | `affects_gameplay=false`；意图显示功能；包内含两个核心候选 | functional | 记录重复程序集身份/版本边界；不进角色主分母 |
| 3747528329 | Ryoshu | `affects_gameplay=true`；角色 DLL/PCK | role-bearing | 主分母；角色选择/初始卡组为首片 |
| 3747530622 | LexNinja2 | `affects_gameplay=true`；BaseLib/RitsuLib 依赖；Stage 7 窄片 | role-bearing | 主分母；保留 31 mapped、31 partial、114 unsupported |
| 3747548629 | Kaguya | `affects_gameplay=true`；Stage 8 事件/奖励窄片 | role-bearing | 主分母；事件已观测，其他内容未闭合 |
| 3747588205 | Acheron | `affects_gameplay=true`；角色 DLL/PCK | role-bearing | 主分母；先确认初始化依赖 |
| 3747595039 | MinionLib | 共享玩法库；被 Shadowverse 依赖 | dependency | foundation gate；不作为独立角色主分母 |
| 3747602295 | STS2-RitsuLib | 共享库；有 `compat/0.111.0` | dependency | foundation gate；变体选择必须精确 |
| 3747644378 | shadowversebydmod | `affects_gameplay=true`；RitsuLib/MinionLib 依赖；真实 Minion 战斗窄片 | role-bearing | 主分母；完整 Run 和全内容仍 Partial |
| 3747725225 | Aemeath | `affects_gameplay=true`；BaseLib 依赖 | role-bearing | 主分母；角色首战为最小纵切片 |
| 3747952763 | ATA_IronClad | `affects_gameplay=false`；Ironclad 外观替换 | resource-only | 只做资源/隔离表面，不进入角色分母 |
| 3749061519 | St2bs | `affects_gameplay=true`；BaseLib 依赖，manifest 明确 beta `0.111.0` | role-bearing | 主分母；先做角色或单敌人 probe |
| 3749627968 | Necrobinder 相关四包 | 四个 manifest，卡图/动画/皮肤/篝火光标，均 `affects_gameplay=false` | resource-only | 按资源类型独立记录，不做角色玩法兼容 |
| 3787753911 | LieRenTVmod | 只有 PCK，manifest 标为非玩法 | resource-only | 只记录 PCK/隔离；不进入角色分母 |
| 3829924471 | YukiMakoto | `affects_gameplay=true`；RitsuLib 依赖；有 `lib/0.111.0` | role-bearing | 主分母；默认第一条完整角色纵切片 |

**冻结主分母：12 个 role-bearing 条目。** 依赖为 3 个，functional 为 3 个，resource-only 为 3 个，当前 21 个快照条目没有额外 excluded；Eleanor 是独立 excluded baseline，不属于快照 21 条目。

## Foundation gate：导出玩法语义

以下清单是 foundation gate 的冻结语义范围。`Observed` 只表示已有阶段证据，`Required` 表示下一阶段必须补齐的共享语义；非玩法 API 另列，不能用来提升 foundation 状态。

| 共享库 | 注册 | 初始化 | 内容创建 | 事件/监听 | 生命周期 | 跨房间状态 | 清理 | 当前证据/缺口 |
| --- | --- | --- | --- | --- | --- | --- | --- | --- |
| BaseLib `3.4.7` | ModManager 装载、CustomContentDictionary/ModelDb 注册 | BaseLib 初始化、资源/脚本注册边界 | Character/Card/Relic/Power/Reward 等标准对象进入注册表 | 战斗开始/结束、奖励、事件和资源账本回调 | 构造→激活→效果→退出→teardown | 角色初始状态、资源账本、房间转移后的注册身份 | 注册表卸载、ALC teardown、PCK/资源引用清理 | Watcher/Kaguya/静态 worker 有窄证据；完整异常和跨房间语义未闭合 |
| RitsuLib `0.6.10` exact `compat/0.111.0` | Ritsu hook/自动注册、角色/卡牌/遗物/Power 注册 | Runtime/Shared 程序集初始化和版本选择 | Ritsu Card/Relic/Power/Reward 定义和目标选择 | hook、turn、combat、reward、before-death 监听 | trigger→effect→turn-end/next-turn→cleanup | secondary resource、Power/Relic 跨战斗持续性 | hook 移除、共享状态清理、ALC teardown | LexNinja2 Stage 7 有真实窄片；完整 hook、死亡和跨房间语义未闭合 |
| MinionLib `0.6.3` | Minion 类型注册、唯一身份和召唤登记 | MinionLib 初始化、依赖加载顺序 | Minion/组件卡/资源结果创建 | summon、damage、death、removal、combat-end 监听 | register→summon→effect→damage→death/removal→combat-end cleanup | pets/combat 集合、组件状态和目标状态跨边界 | combat cleanup、pets 移除、注册表 teardown | Stage 9/10 已覆盖一个真实 Minion；全 Minion、复杂组件、跨房间和网络语义未闭合 |

**Foundation gate 当前状态：`PARTIAL`，对依赖 Mod 的完整接受为 `BLOCKED`。** 任何未列入上述共享语义的设置、UI、地图、保存、Steam、云端、网络或非玩法 helper 均单列为平台/非玩法能力。

## Role-bearing content unit 和 registration identity 清单

以下是 M2-0 冻结的第一版 append-only inventory。静态或窄 probe 未证明的单位标为 `Unrun`，不得因 assembly 加载而变成 `Covered`。`REG.*` 是 registration identity 命名空间，不是对未执行内容的伪造运行时 ID。

| Mod | 精确兼容变体 | 依赖闭包 | 候选角色/内容单元 | registration identity | 第一最小纵切片 | 当前单元状态 |
| --- | --- | --- | --- | --- | --- | --- |
| Watcher | 无专用目录；目标 `0.111.0` | BaseLib → Watcher | `CHAR.WATCHER`；已观察 ModelDb 129 identity（Card/Power/Relic/Character），另有 6 个负 canonical-cost token Card 风险单元 | `REG.WATCHER.ModelDb#129@0.111.0`；每个动态 identity 追加为独立单元 | 角色选择→起始卡/遗物→一场战斗→奖励→清理 | Partial；6 token Card Unsupported，Stage 6 runner 保留 MOD-010 |
| Downfall | 无专用目录 | BaseLib → Downfall | `CHAR.AUTOMATON`, `CHAR.AWAKENED`, `CHAR.CHAMP`, `CHAR.GUARDIAN`, `CHAR.HERMIT`, `CHAR.HEXAGHOST`, `CHAR.SLIMEBOSS`, `CHAR.SNECKO`；包级 `REG.DOWNFALL.assembly-set` | `REG.DOWNFALL.assembly-set#9@0.111.0`（含候选角色程序集） | 先选一个角色，做角色选择和首战 | Unrun |
| figure_Saya | 无专用目录 | figure_Saya | `CHAR.FIGURE_SAYA`，以及其动态 Card/Relic/Power/Reward 注册集 | `REG.FIGURE_SAYA.assembly#1@0.111.0` | 角色选择→初始状态→一场战斗 | Unrun |
| Remilia | 无专用目录 | RitsuLib → Remilia | `CHAR.REMILIA`；Ritsu Card/Relic/Power/Reward 注册集 | `REG.REMILIA.assembly#1+RITSU@0.111.0` | 精确 RitsuLib 变体下做角色/卡牌/遗物发现 | Unrun |
| Ryoshu | 无专用目录 | Ryoshu | `CHAR.RYOSHU`，动态 Card/Relic/Power/Reward 集 | `REG.RYOSHU.assembly#1@0.111.0` | 角色选择→起始卡组→首战 | Unrun |
| LexNinja2 | 无专用目录；RitsuLib exact `compat/0.111.0` | BaseLib → RitsuLib → LexNinja2 | `CHAR.LEXNINJA2`；runtime scope 1 Character/103 Card/9 Relic/37 Power；首片已观察 `StrikeNinja`, `YiCut`, `DefendNinja`, `ShakeShakeHands`, `MosquitoHand` | `REG.LEXNINJA2.RitsuAutoRegister#179@0.111.0` | 一个已 mapped Card/Power + 一个 partial/边界 Power | Partial（31 mapped、31 partial、114 Unsupported 能力矩阵） |
| Kaguya | 无专用目录 | BaseLib → Kaguya | `EVENT.TAKETORI_MONOGATARI_EVENT`、两个 option、`REWARD.KAGUYA_TAKETORI_OPTION`、resource ledger；其余角色/卡/敌人/第四幕单元待追加 | `REG.KAGUYA.TAKETORI#1@0.111.0` | 事件进入→两个选项→READ→奖励/ledger→teardown | Partial（UI/Map/Save Unsupported） |
| Acheron | 无专用目录 | Acheron | `CHAR.ACHERON`，动态 Card/Relic/Power/Enemy/Reward 集 | `REG.ACHERON.assembly#1@0.111.0` | 初始化依赖确认→角色选择→单战斗 | Unrun |
| shadowversebydmod | 无专用目录；依赖 exact RitsuLib | MinionLib → RitsuLib `compat/0.111.0` → shadowversebydmod | `MINION.KNIGHT_SOLDIER_MINION`、`CARD.ANCIENT_ARTIFACT`、`COMPONENT.StrikeComponent`；其余 Minion/Card/Component/Enemy/Reward 动态集待追加 | `REG.SHADOWVERSE.content#1@0.111.0`；`REG.SHADOWVERSE.KNIGHT_SOLDIER#1` | 已闭合 Minion summon→damage→death/removal→cleanup + 组件卡 | Partial；UI/Map/Save/ComplexComponent/ReplayNetwork Unsupported |
| Aemeath | 无专用目录 | BaseLib → Aemeath | `CHAR.AEMEATH`，动态 Card/Relic/Power/Reward 集 | `REG.AEMEATH.assembly#1@0.111.0` | 角色选择→初始状态→首战 | Unrun |
| St2bs | manifest 明确 `0.111.0`；无静默回退 | BaseLib → St2bs | `CHAR.ST2BS`，复杂 Card/Enemy/Event/Reward 集 | `REG.ST2BS.assembly#1@0.111.0` | 角色或一个自定义敌人最小 probe | Unrun |
| YukiMakoto | **`lib/0.111.0`** | RitsuLib `compat/0.111.0` → YukiMakoto | `CHAR.YUKIMAKOTO`；候选 runtime scope 1 Character/270 Card/4 Relic/281 Power | `REG.YUKI.lib/0.111.0#1@0.111.0` | 默认第一条完整角色纵切片：角色选择→初始卡/遗物→首战 | Unrun |

若真实运行发现新的动态注册身份，必须追加到本表并把受影响 Mod 的旧结论标为 `Affected evidence`，不能折叠进包级 identity。

## Lifecycle matrix

该矩阵适用于所有主分母；每个 Mod 表中的单位必须引用适用行。`Covered` 需要所有适用检查点和交互契约都有证据。

| 内容类型 | 发现/注册 | 构造/加载 | 激活/效果 | 持久化/跨房间 | 退出/清理 | 正常场景 | 边界/失败场景 |
| --- | --- | --- | --- | --- | --- | --- | --- |
| Character | identity、选择入口、起始注册 | 角色对象、起始卡组/遗物/状态 | 战斗开始、回合、死亡/胜利 | 房间转移后的状态与资源 | 离开战斗、run teardown、注册表卸载 | 合法角色选择和首战 | 初始化崩溃、缺资源、错误角色 ID、死亡中断 |
| Card | Card identity、池/奖励注册 | 构造、cost、目标模式 | play、target、damage/block/power、生成 | exhaust/discard/生成卡跨回合 | combat end、run end、临时状态清除 | 单目标、无目标、self-target | 负 cost token、多目标、空目标、非法 target、效果异常 |
| Relic | identity、起始/奖励注册 | 构造和初始 relic | combat-start/end、turn、before-death、reward trigger | 跨房间触发和计数 | remove、run end、hook detach | 一个触发时机 | before-death、重复触发、跨房间计数丢失 |
| Power/Status | identity、初始/卡牌获得 | 构造、层数、持续期 | apply、stack、turn-start/end、expire | 回合/房间持续、序列化 | expire、remove、combat/run cleanup | 获得、叠层、回合结束 | 零层、负层、死亡、重复 apply、清理遗漏 |
| Enemy | identity、encounter/intent 注册 | encounter 构造、intent | intent、damage、death、reward | encounter→reward 边界 | enemy removal、combat cleanup | 一场合法战斗 | 不可达 intent、死亡中断、奖励缺失 |
| Encounter/Event | identity、地图/池注册 | 进入房间、选项构造 | 选项回调、资源/卡/Relic/Reward 结果 | room transition、ledger | exit、teardown、回调解绑 | 一个合法选项 | 无效选项、重复选择、资源不足、结果异常 |
| Reward | reward identity、池注册 | reward 构造 | 选择/领取、资源或卡牌结果 | reward→下一房间 | 未领取清理、run end | 一个真实奖励边界 | 空奖励、重复领取、非法卡/资源 |
| Minion/Component | Minion/组件 identity、唯一注册 | summon、组件实例 | component resolve、damage、death/removal | pets/combat 集合和组件状态 | combat-end、pets 移除、ALC teardown | Knight Soldier + StrikeComponent | 复杂组件、多个 Minion、死亡顺序、重复 summon |
| Resource/Platform carrier | 资源 identity、账本/资源注册 | 资源结果构造 | 账本变更、保存承载 | 跨房间/保存/网络 | ledger cleanup、保存释放 | Kaguya resource ledger | UI、正式 save、map、network、Steam/cloud |

## Interaction risk table

| 交互契约 | 正常场景 | 边界/失败场景 | 当前证据/状态 |
| --- | --- | --- | --- |
| 依赖加载顺序 | BaseLib/RitsuLib/MinionLib 在 Mod 前按闭包加载 | 缺依赖、版本不符、同名程序集、错误变体 | 静态矩阵 21/21；intentgraph2 历史重复程序集、Shadowverse 大 DLL 已修复为可审计发现；foundation 仍 Partial |
| 角色选择与初始状态 | 选择自定义 Character，生成起始卡组/遗物/Power | 角色 ID 不存在、初始化崩溃、资源缺失 | Watcher/Lex/Yuki 有候选证据；主分母多数 Unrun |
| Card target/effect | 单目标伤害、无目标卡、self target | 负 cost token、多目标、空目标、非法 target | LexNinja2 4 张效果、Watcher 6 token 风险；Partial |
| Card stacking/generation | 生成卡、叠加 Power、discard/exhaust | 生成身份未注册、重复生成、序列化丢失 | 未形成共享 foundation 证据；Unrun/Unsupported |
| Relic triggers | combat-start/end、reward trigger | before-death、重复触发、跨房间计数 | LexNinja2 触发窄片；before-death Partial |
| Power lifecycle | apply、stack、turn-end、expire | 零/负层、死亡中断、cleanup 遗漏 | LexNinja2 有部分真实记录；其余 Partial/Unsupported |
| Enemy intent/death | intent→damage→death→reward | 不可达 intent、死亡中断、奖励不一致 | 主分母未形成全流程证据；Unrun |
| Event option/result | Kaguya 两选项、READ 结果、ledger | 无效/重复选择、资源不足、异常结果 | Kaguya Stage 8 PASS（窄片）；其它事件 Unrun |
| Reward selection | 真实奖励边界、领取一次 | 空/重复奖励、非法卡或资源 | Watcher/Kaguya/Lex 窄片；全包未闭合 |
| Minion summon/damage/death | Knight Soldier summon、HP 3→2→0、移除 | 多 Minion、死亡顺序、复杂 Component | Stage 10 primary/replay PASS；复杂组件 Unsupported |
| Cross-room state | 账本、Power/Relic/Minion 集合进入下一房间 | 状态泄漏、丢失、重复注册 | 未闭合，作为 foundation gate blocker |
| Cleanup/teardown | combat-end、pets 移除、ALC unload | callback/hook 残留、资源锁、进程污染 | Stage 9/10 窄片；共享语义未闭合 |
| Visible-policy boundary | 玩家可见信息驱动策略 | Oracle/hidden state probe 越界、UI 缺失 | ADR-0004；执行 probe 不计 visible-policy |

## Flow/content/stability 契约和状态聚合

- `flow gate`：合法完整流程必须产生真实 native `RunVictory` 或真实 native `Death`。
- `content gate`：每个冻结 content unit 的适用 lifecycle checkpoint 和 interaction contract 都为 `Covered`。
- `foundation gate`：BaseLib/RitsuLib/MinionLib 的共享玩法语义先闭合；当前 `PARTIAL`，因此依赖 Mod 的完整接受为 `BLOCKED`。
- `primary`：固定 canonical input 的第一条新进程；`repeat`：同 seed、同输入、第二条独立新进程；`replay`：按 primary route 重放并验证状态/RNG/trace；`failure`：独立控制运行，必须产生明确 flow stop。
- `Covered`：所有适用检查点和交互证据齐全；`Unrun`：只发现未执行；`Unsupported`：安全且有证据的语义缺口；`RuntimeFailure`：原生/Mod 进程异常；`Partial`：至少一个 gate 或单元未闭合；`BLOCKED`：共享基础/环境/合法输入未闭合。
- 状态聚合：`RuntimeFailure`/`BLOCKED` 阻止对应 gate；否则 `Unrun`/`Unsupported` 的必需单元让 content gate 不通过；任何 gate 不完整则 Mod 为 `Partial`；只有 foundation、flow、content、稳定性和输入审计全部通过才是 `PASS`。

## Canonical input、probe、visible-policy 和平台边界

每个后续运行必须把下列字段写入新的 `.build/runs` manifest：target game `0.111.0`、pinned game commit、snapshot ID/SHA256、Mod ID/version、依赖闭包及其文件摘要、精确兼容变体、seed、worker/process identity、配置、adapter schema、collector schema、route/replay reference、状态聚合和错误分类。

- `static discovery` 只能建立候选 registration identity、依赖和文件/程序集摘要。
- `execution probe` 可以在独立 worker/NativeRun 使用 native/Oracle 内部状态验证执行兼容；必须保留真实注册、回调、效果和清理事件。
- `visible-policy compatibility` 必须另行证明；隐藏状态、Oracle、内部资源或直接写入结果的 probe 不得计入。
- UI、保存、地图、网络、Steam、云端和纯表现层资源是 `platform capability`，除非某一内容生命周期直接依赖它们；平台缺口不得被 gate 结果覆盖。
- 不能静默回退版本、跳过回调、注入结果、使用空实现、用原版角色替代自定义角色、修改 Mod、修改快照或把预测死亡当真实 Death。

## Random Workshop sample 规则

M2-7 另建新快照和 manifest，使用一个写入 run manifest 的选择 seed，从候选中确定性抽取首批 3 个 `affects_gameplay=true`、目标版本可审计、依赖可解析且不重复固定 21 条目的 Mod。每个样本拥有独立 canonical input、四路证据和分母；失败必须保留 `Partial`/`Unsupported`/`RuntimeFailure`，不能改写为“未选择”。本阶段不选择样本、不读取实时 Workshop。

## 既有 primary/repeat/replay/failure 终态摘要

M2-0 本身是只读审计，未启动新的 Mod 运行；因此本阶段四路均为 `Unrun`，没有新的 digest。下表只汇总已有窄片，不能替代 M2-1 之后的完整四路契约：

| 范围 | primary | repeat | replay | failure | digest/错误分类 |
| --- | --- | --- | --- | --- | --- |
| Watcher | Godot probe 已有独立新进程事实 | 有同构建复核；Stage 6 runner 仍 Blocked | 完整 M2 replay 未闭合 | initializer/宿主边界 `RuntimeFailure`，runner `MOD-010` | 停止摘要 `45034b14022be5d704baa84ffc9a9376fc8c16d17f212e363a5480dde49a7b58` |
| LexNinja2 | 窄首战 PASS | 独立进程 PASS | PASS | 未形成完整 failure control | `62d868cc3c9fe0144f5c1e3da9005f3e31e7157206d400826d996b282c40be1b`；content Partial |
| Kaguya | 事件窄片 PASS | 独立进程 PASS | PASS | 未形成完整 failure control | probe `689abf7a485b941f4b522b35585d6e69b06fcb01d6a4254817b227be6774b767`；UI/Map/Save Unsupported |
| Shadowverse/Minion | 真实 Minion 战斗 PASS | 独立进程 PASS | PASS | budget control `RuntimeFailure`，无真实战斗 | `d10748bef819ea570c820e0749a15bcb080fc8cf23cc4c32e5861b70d50a8279` |
| 其它 8 个 role-bearing Mod | Unrun | Unrun | Unrun | Unrun | 无 digest；不能从静态 worker 推断玩法 |

## 分母和已知缺口

- 主分母：12 role-bearing；foundation：3 dependency；functional：3；resource-only：3；Eleanor：excluded baseline。
- 静态发现分母：21/21 worker succeeded、0 failed、0 blocked；这是 discovery/isolation 分母。
- 内容状态分母：已知窄片仍有 `Partial`（Watcher、LexNinja2、Kaguya、Shadowverse），8 个 role-bearing Mod 为 `Unrun`；完整 content `Covered` 分母为 0/12。
- `Unsupported`：Watcher 负 canonical-cost token、LexNinja2 未覆盖生命周期、Kaguya UI/Map/Save、Shadowverse UI/Map/Save/ComplexComponent/ReplayNetwork，以及所有尚未建模的内容语义。
- `RuntimeFailure`：Watcher Godot/initializer 原生访问冲突历史证据；过去 worker 的 intentgraph2 重复程序集和 Shadowverse 大 DLL 读取阻塞已在修复矩阵中得到明确选择/流式摘要证据，但不因此获得玩法通过。
- `Unrun`：Downfall、figure_Saya、Remilia、Ryoshu、Acheron、Aemeath、St2bs、YukiMakoto 的完整玩法；以及所有未观察的动态注册身份、死亡路径、跨房间持久化和失败路线。
- 平台缺口：UI、正式保存、地图注入、网络/多人 replay、Steam、Cloud、表现层资源；这些不计入 gameplay gate，却必须在报告中单列。
- Eleanor 当前冻结输入和报告目录缺失，保持 `BLOCKED/Unrun`，不计入 12 或 21 的成功分母。

## 验收标准逐项结果

| 验收标准 | 结果 |
| --- | --- |
| 21 条目都有五类分类 | 通过；12/3/3/3/0（role/dependency/functional/resource/excluded in snapshot）已冻结 |
| Foundation 语义覆盖注册、初始化、创建、事件、生命周期、跨房间、清理 | 通过；三库逐项清单建立；gate 为 Partial/Blocked，不掩盖缺口 |
| 每个主分母 Mod 有 content unit、registration identity、variant、依赖闭包、最小纵切片 | 通过；12 行 inventory 建立，动态发现采用 append-only |
| 生命周期矩阵和交互风险表可复核 | 通过；按内容类型和 12 类交互列出正常/边界失败场景 |
| flow/content/foundation、四路状态和聚合规则冻结 | 通过；本报告和 GLOSSARY 已定义 |
| canonical input、追加发现、probe、visible-policy、平台边界冻结 | 通过；字段、collector 和禁止回退规则已写明 |
| 固定快照、游戏 `0.111.0`、manifest 存在且只读 | 通过；摘要和规模已核对 |
| 不改运行时代码/Mod/二进制/快照 | 通过；仅修改 GLOSSARY、ADR 和报告 |
| 票据 open/ready/no blockers 且已认领 | 部分；状态复核通过，认领因 GitHub API 403 未完成，未关闭票据 |

## 下一阶段唯一建议入口

在票据认领权限恢复、foundation gate 的共享语义缺口仍被显式接受后，只选择 **YukiMakoto `lib/0.111.0`** 作为 M2-1/M2-2 的第一条角色入口；先实现/验证通用终态 coordinator 和 primary/repeat/replay/failure 契约，再加载该角色。不要并行选择第二个角色，不要把静态矩阵或现有窄片升级为完整兼容。

## 是否提交或推送

否。没有提交、推送或创建远程 PR。

