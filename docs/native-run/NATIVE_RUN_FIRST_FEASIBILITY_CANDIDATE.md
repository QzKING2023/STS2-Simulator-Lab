# 候选可行性分析：先完成原生离线爬塔与数据采集

日期：2026-11-02。**状态：候选分析，尚未批准为实施方案。** 本文不替换 `LONG_TERM_ROADMAP.md`，不授权后续实现、公开 SDK、提交或推送。

本次检查读取了当前 Lab 源码、冻结证据，以及固定游戏程序集的相关类型；游戏类型通过 ILSpy 静态查看，没有执行新的爬塔、房间初始化或游戏界面。两卡复审在方向调整前已经进行，收束结果见附录 A。本文区分 **事实 F、建议 R、待验证推断 H**。

## 1. 判断与适用条件

**R：支持把主线调整为“原版原生离线完整 run 执行与可信数据采集”，暂停新增 Mod 专项适配，推迟公开 Mod API。** 这更直接服务于离线测试场的目标。已有 Mod 工作应作为保留的验证资产，而不是继续扩张的理由。

支持有三个条件：

1. “完整爬塔”首先指从真实新 run 开始，经合法节点和原生选择，到真实 Death 或最终 RunVictory；不要求最优策略、必胜、所有种子、全角色或全内容一次完成。
2. 原生执行覆盖、仿真预测覆盖、策略水平、数据完整性分别报告。原生执行可以通过而该牌的仿真仍 unsupported；不能将其写成 native/sim 差分通过。
3. 将基础数据契约和观察正确性与 run 驱动一起建立，不等爬塔完成才补日志。TestMode 本身存在有意义的行为分支，不能把它当作纯粹关闭渲染的开关。

**H：原版单角色、固定版本、单人 Standard run，采用简单合法策略，技术上具备可行路径。** 当前没有完整 run 实证，不能承诺无需新环境适配即可完成，也不能据已有两战估算工期。实际难点会从“每个 Mod 效果的镜像”转移到“房间生命周期、选择、宿主边界和观测契约”。

## 2. 四件不同的事

| 层次 | 应交付什么 | 不应成为其前置条件 |
| --- | --- | --- |
| 原生无 UI 执行与采集 | 游戏自己的模型、命令、RNG、奖励与房间流实际运行；记录输入、选择、变化、终局 | 每张牌都已有预测镜像；强策略；公开 Mod SDK |
| 战斗仿真与 fork | 不污染父状态的预测分支、回放、严格差分与明确不支持范围 | 整个 run 必须可 fork；先重写所有非战斗效果 |
| 策略 | 根据声明的信息与预算选择合法动作；可替换、可复现、可统计 | 保证胜利、穷举整局、全局最优 |
| 公开 Mod API | 稳定的加载、状态、选择、观察、预测适配契约及版本与兼容承诺 | 在原版流程尚未稳定时先设计完整通用 SDK |

“模拟完整爬塔”若特指脱离游戏程序集的独立全规则引擎，则本建议不能直接满足；那是成本和验证负担显著更高的另一项目。用户当前离线、无游戏界面的目标允许使用合法本机固定程序集，本分析据此判断。

## 3. 当前代码实际说明了什么

### F：已有可复用基础

- 原版战斗实际执行与预测差分、敌方回合、随机目标重放已有证据。原版 M3 是显式原生奖励命令与下一节点的有限验证；不能把 Eleanor 两战证据等同于完整原版 run。
- `NativeModProgression.RunLoop` 已在同一 run 执行真实 Victory、原生金币领取、卡牌 skip/take、合法下一 Monster 节点、第二战；`NativeSecondBattle` 保留真实奖励 DeckVersion 和打出证据。这证实部分生命周期能脱离零售 UI 驱动。
- 该驱动仍显著绑定 Eleanor：固定角色状态断言、两场战斗、奖励覆盖列表、求解/重放、有限预算。`NativeCombatDecision.Query` 直接调用 CombatSolver；它尚不是通用、可替换的整个 run 策略接口。
- `RunProgressProtocol.cs` 只验证奖励阶段与地图边合法性。其 `ProceedToRoute` 明确拒绝没有子节点的末端，说明幕转换没有被实现。它不模拟奖励效果、事件、商店或 run 状态复制。
- `OfflineCombat` 创建真实 RunState、原生玩家与默认 Acts、设置 `shouldSave:false`、初始化遗物、Launch 和 EnterAct。已有原版入口还包含 Debug encounter 选择；不能不加区别地称作自然完整开局。Mod 原生入口才使用普通 `EnterMapCoord`。
- `MainLoopContext`、MemorySaveStore、版本冻结、失败传播、动作前缀计数、过期票据拒绝、父 fork 隔离、进程隔离和独立契约都可保留。

### F：当前规模没有证明的能力

没有原版完整一幕或完整 run 的验收记录；没有通用事件/商店/休息/宝箱驱动；没有原生多幕胜利证据；没有完整且经过观察扰动检查的必要数据流。90 次两卡 API 夹具和两个各 12 条两战批次验证的是窄语义与重放，不能转换为整局支持率。

**R：无需推倒 Core 和已有证据，但需要一次有边界的驱动重构。** 将宿主初始化、安全隔离、原生动作派发、房间调度、策略和采集从 Eleanor probe/solver 调用中拆开，是实质性工作，不能仅在两战函数外加 `while`。可先在当前 harness 复用这些组件，后续再评估是否物理拆成独立原生 host 项目；不宜第一步就重写构建系统。

## 4. 逐类可行路径与缺口

以下“存在入口”是静态代码事实，不等于本 Lab 已实际验证该入口。

| 类别 | F：可复用的原生行为 | 尚缺的离线驱动或验证 |
| --- | --- | --- |
| 开局、解锁与角色 | `RunState.CreateForNewRun`、`Player.CreateForNewRun`、`FinalizeStartingRelics`、`EnterAct` | 固定并记录内存解锁档、Ascension、默认 Act 列表、起始 Ancient/Neow 语义；不得依赖真实存档，也不得把 Debug 战斗当自然开局 |
| 战斗 | `CardModel.CanPlay/IsValidTarget/TryManualPlay` 进入 `PlayCardAction`；原生 `OnPlay`、Hook 和敌方调度执行效果 | 通用合法动作与目标枚举、子选择、自动触发和非标准终止；目前 replay 还要求 sim 可执行。原生路径须能独立工作 |
| 卡牌与状态 | 实际 OnPlay/生成/抽弃/升级由游戏执行 | 原生执行不需要逐牌重写镜像，但卡牌的 UI 依赖、特殊选择与观察范围仍可能 unsupported；不能空执行未知效果 |
| 奖励 | `RewardsCmd`、`RewardsSet`、`RewardsSetSynchronizer` 生成并领取；`CardSelectCmd.UseSelector` 可提供选择 | 从有限 Gold/Card 扩到实际出现的其他奖励、嵌套奖励、不能跳过、reroll/alternative；每个 set 只生成和结算一次 |
| 地图 | `GenerateMap`、`EnterMapCoord`、`MapSelectionSynchronizer` 与 `MoveToMapCoordAction`；Unknown 由原生 odds/RNG 决定房间 | 新地图 generation ID、合法旅行/遗物例外、房间完成门、动作同步。普通入口不是所有阶段合法性的替代检查；不能强行将未知节点改 Monster |
| 事件与 Ancient | `EventRoom`、`EventSynchronizer.ChooseLocalOption`、`CurrentOptions`、`AwaitPendingOptionTasks`、原生选项回调 | 多页、锁定选项、事件战斗/恢复父房间、奖励/移除等嵌套选择、特殊 UI；完成判断不能仅看动作队列空。事件资源预载尚未纳入现有三个 preload shim |
| 商店 | `MerchantRoom` 原生生成各玩家 inventory；`MerchantEntry.OnTryPurchaseWrapper` 检查库存与金币，再执行购买/补货/hooks | `ignoreCost:false`、购买与删牌选择、折扣/补货、药水满槽、交易后记录；离开不购买是合法策略，但不证明购买功能已验收 |
| 休息 | `RestSiteOption.Generate`、`RestSiteSynchronizer.ChooseLocalOption`、`AfterAllRestSitesCompleted` | 原生有效选项、升级选牌、多次选择/特殊遗物选项及退出；不能直接改 HP/Upgrade 代替选择 |
| 宝箱与遗物 | `TreasureRoom`、`TreasureRoomRelicSynchronizer`、`DoNormalRewards/DoExtraRewardsIfNeeded`；遗物回调留在原生流程 | 开箱/选择/额外奖励有独立生命周期；新增遗物可能改变地图、商店、选项、战斗与自动选择；不能只记录遗物 ID |
| 药水 | `PotionModel.EnqueueManualUse/OnUseWrapper/IsValidTarget` 执行真实使用和移除 | 药水与卡牌的目标规则不同；须验证取得/满槽/丢弃、战内战外使用、自动药水。策略可以明确不主动喝，但自动效果不能省略 |
| 精英与 Boss | 原生 encounter、怪物状态机、召唤/阶段转换、战后 Hook | 现有两战不能推出这些都无 UI 可运行；首幕 Boss 及后续 Boss 需真实执行证据 |
| 幕转换 | `ActChangeSynchronizer.SetLocalPlayerReady` 排队投票动作，再由 `EnterNextAct/EnterAct` 驱动生成地图和 hooks | 等待实际转换完成、持久资源与 odds 重置、Boss 奖励与下一 Ancient；不能用 SetActInternal/debug 跳幕冒充验收 |
| 最终胜利 | 最后 Act 后 `EnterNextAct` 进入 `TheArchitect`；其选项调用 `RunManager.WinRun` → `OnEnded(true)` | 最终事件对话/UI/平台依赖未验证；末场 CombatWon 不是 RunVictory。WinRun 后还有杀死玩家的清理，不能仅凭 HP=0 将胜利误判死亡 |
| 死亡与清理 | `ProcessPendingLoss` 发出 CombatEnded；已有真实 Death 夹具与终态 stamp | 该路径不等于 AfterCombatEnd，也不等于完整零售 run-loss 流。完整驱动须捕捉原生死亡、正确停止与封存数据；先保持每 run 独立进程，不承诺同进程复用 |
| 持久状态与保存 | RunState、玩家 deck/遗物/药水/HP/gold、RNG/odds；MemorySaveStore 与已有写入拦截 | 新房间、最终 OnEnded、统计/metrics/平台调用逐条核验；保留无保存/非 Daily/Steam off，不以两战零写入证明全部终局安全 |

原生商店购买、事件效果和卡牌效果原则上都可由游戏计算，Lab 提供决策与运行环境。它们不会因为“没有预测镜像”而天然不能执行。相反，若宿主遗漏所需 UI 回调、资源或原生依赖，仍必须修环境边界或报告 unsupported；不能靠更宽的 try/catch 或 no-op 跳过语义。

## 5. TestMode 与无 UI 的具体风险

本次静态检查发现以下有直接实现依据的差异：

- `RunManager.ShouldApplyTutorialModifications()` 在 TestMode 为 false；`GenerateMap()` 是否将起点改 Monster 取决于 `StartedWithNeow`，该值来自解锁状态。因此起始档案是实验输入。
- `CardSelectCmd.FromChooseABundleScreen` 在 TestMode 直接取 `bundles[0]`，不经普通 selector。允许明确“总选第一组”的策略，不允许称任意组选择已受控或悄悄把它算作 agent 决策。
- 普通 `CardSelectCmd` 自动 selector 会走特定测试分支，有些同步/choice ID 过程会被绕开。合法决策注入可以保留，必须记录所用路径；不能据此声称完整网络/UI协议等价。
- `RunManager.UpdatePlayerStatsInMapPointHistory` 在 TestMode 直接返回；`MerchantRoom.Exit` 也跳过未购买物品历史汇总。不能仅导出游戏已有 history 就宣称数据齐全。
- 原生 `CombatHistory` 有动作、抽弃消耗、生成、伤害、格挡、怪物动作、药水和 power 等真实 entries；Victory 清理会 `History.Clear()`。应及时读取不可变投影，不在终局后才导出持有可变引用的整表。
- 现有 GameBootstrap 只拦截 Run/Act/Combat 三类资源预载，并跳过 Godot Node 类型的静态构造。事件、商店、休息、宝箱与终局进入新的资源和回调路径。原有限战斗解释不能自动覆盖所有这些节点的静态字段与逻辑。
- `EventRoom.EnterInternal` 会调用事件节点创建，部分节点调用虽有 null 条件，但资源预载本身并不因此消失。某些特殊事件还把推进放在动画回调中，必须区分展示与效果/完成信号。
- 终局平台调用也有可利用的原生隔离条件：`SteamStatsManager.RefreshGlobalStats` 在 Steam 未初始化时返回；`OnEnded` 的主要保存/上传块受 `ShouldSave` 控制，Daily 上传另受 DailyTime 控制。这支持非 Daily、`shouldSave:false`、Steam off 的方案，但不是尚未执行的整个终局安全证明。

**R：优先继续当前纯 .NET/TestMode host，逐个登记所需宿主替代点；设明确停止线。** 若关键进度语义必须依赖 Godot 运行时，应比较独立、隔离的 Godot headless host，单独进行安全与权限评估。这仍可不打开游戏界面，但不能在本次仅分析授权下启动，也不能通过大面积跳过游戏方法假装现有 host 足够。

## 6. 原生主线与镜像主线的取舍

| 路线 | 价值 | 成本与正确性风险 | 对当前目标的判断 |
| --- | --- | --- | --- |
| 原生单轨执行 + 简单策略 + 数据 | 直接触达真实完整生命周期；复用游戏卡牌、事件、商店和遗物效果 | 宿主边界与异步调度；每次只推进一条实际轨迹；无天然 cheap fork；数据观察和 TestMode 偏差仍要验证 | **推荐作为候选主线** |
| 每个效果先有镜像，再允许主流程运行 | 可进行大量预测分支、细粒度差分和搜索 | 双实现同步、状态/选择/监听器覆盖膨胀；容易把预测缺口变成执行阻塞 | 保留已验证子集；不适合继续作为所有爬塔功能前置门 |
| 原生执行 + 可选 CombatSolver | 复用已有搜索能力；遇预测缺口仍可使用预先声明的原生合法策略 | 必须分离观测/执行和求解，记录策略切换与信息模式；不再默认每步都有差分 | 最合适的组合方向 |
| 完全独立重写全规则引擎 | 更强控制、潜在可移植与高吞吐 | 牌/遗物/事件/版本重建与验证负担最大 | 当前不建议 |
| 隔离 Godot headless host | 有机会保留更多原生资源/回调，减少特例 shim | 初始化、资源依赖、平台/保存隔离成本；性能尚未测量 | 当前 host 遇具体结构性阻塞时的候选，不是已授权实施 |

原生运行通常能避免逐效果预测镜像维护；这不是“几乎零成本支持全内容”。性能也不能仅凭架构推定。需要分别测初始化耗时、每动作/每房间耗时、观察开销、内存、完整轨迹吞吐，以及失败前可诊断率。

### R：CombatSolver 可替换，而不是删除

建议的内部关系是：原生 host 持有唯一实际 run → 产生合法决策请求与声明信息 → 策略返回动作 → 先验证来源与合法性 → 原生执行到稳定边界 → 采集。CombatSolver 只作为其中一种战斗策略/预测服务。

初始策略可以是固定排序、简单启发式或独立 policy RNG 的合法随机策略：例如从原生当前可打牌与合法目标中按声明规则选取，无法或决定不再出牌时 EndTurn；事件选未锁定选项，商店按预算购买或合法离开。选择策略可以简单，**效果必须仍真实执行**。

对于未被 Solver 支持的牌，两个“未知”需分开：

- 原生效果与选择路径可执行、只是没有预测支持：原生策略可合法选用，报告 `comparison=not-compared` 和原因；不得调用镜像空实现。
- 无法供应真实选择、遇未隔离宿主依赖，或效果执行状态不确定：停止为 unsupported/environment-blocked/engine-error，保留前缀。不得跳过该效果后继续。

可选 Solver 失败后是否回退、什么预算触发回退，应在实验前声明。不能默默将搜索失败替换为 EndTurn，再把结果写作 Solver 策略表现。未知规则导致的执行错误与弱策略导致的死亡要分开。

## 7. 公开 Mod API 的推迟：合理，但保留窄内部边界

**R：现在不设计公开 SDK、稳定 ABI、万能效果注册器或任意 Mod 自动兼容承诺。** 先让原版多节点、完整 run 和数据契约暴露真实需求，之后再决定哪些边界需要公开。

现在只保留能被现有需求检验的内部边界：

1. 固定版本/真实来源的 host profile 与安全隔离；内部可查询 capability，遇不支持有明确结果。
2. 决策请求及选择的来源状态、对象身份、页/阶段、合法候选、消费次数；这也是原版重复牌、生成牌、事件和商店必需的能力。
3. 原生执行结果与稳定边界；可选预测服务，不要求原生所有动作经过它。
4. 只读 observation/event sink；run 持久对象与战斗克隆/生成对象的身份关系。

不把现有内部接口立即升级为公共契约。当前 `IPlayerVisiblePolicy` 的观测 DTO 没有完整 run 信息，也缺少稳定卡牌实例 ID、药水/遗物等必要输入；应由实际原版需求推动演进。

冻结但保留 Eleanor pins、适配器、真实来源/patch 审计、负控和既有回归。通用改动若影响旧适配，应报告回归，不因此自动展开新角色内容。可以在原版里程碑后重新运行冻结 Eleanor 作为架构压力测试，不要求现在继续扩卡。

延迟风险包括原版专用假设、后续 Mod 状态无法序列化、加载与预测契约混淆。缓解方法是保留上述内部 seams 和旧复杂样本，而不是提前设计覆盖所有 Mod 的抽象。未来公开时应分别标明 **原生加载/决策/数据支持** 与 **预测/fork 支持**；能加载 Mod 绝不意味着能正确复制它的私有状态。API 发布时间由原版 run 与契约的稳定证据决定，不必等待全游戏所有内容被穷举。

## 8. “必要数据”的最小可核验集合

建议两层产物：结构化、按顺序追加的事实流与稳定边界快照；由这些事实离线计算汇总。原始日志可分块压缩，失败轨迹保留；不能只留均值与胜负。这里的“完整日志”指已声明观测契约内没有漏项，不声称记录任意内部方法调用。

| 数据 | 最小字段/关系 | 验收方式 |
| --- | --- | --- |
| 实验身份 | 游戏/build hash、Lab dirty source/output hash、依赖/profile、seed、解锁档、角色/难度、策略与预算、宿主替代点、schema | 每产物可归属同一次冻结；不能只写 HEAD |
| 生命周期 | run/act/map-generation/node/room/combat ID、进入/退出、战斗与 run 终态、最后确认边界 | 不混末战胜利与 RunVictory；Death/预算/异常/unsupported 独立 |
| 决策与执行 | 请求 ID、合法候选、选择来源、对象 ID、target、嵌套选择、派发/完成/拒绝、理由、策略回退 | 拒绝不增加执行计数；过期/重复/多余字段失败；记录真实完成而非计划尾部 |
| 资源与状态 | HP/maxHP、Block、energy/stars、power 数量与来源范围、金币、相关计数器，边界前后值 | 原生实际值，独立规则检查和动作前后对账；不以净 HP 差替代伤害日志 |
| 牌组与对象 | 永久 deck 完整内容/升级/附魔/可保存状态；战斗 pile 顺序；生成/转换/消耗/移除与 DeckVersion 来源 | 同名不同实例可辨；跨战 clone 不混永久对象；历史保存不可变投影 |
| 遗物与药水 | 获取/丢弃/使用、槽位、状态/计数、费用与触发来源 | 不能只拍 ID 清单；自动触发同样计入 |
| 伤害/格挡/触发 | 原生 DamageResult、实际 HP loss、blocked/unblocked、overkill、来源/目标；Block gain/consume；已观察的 power/relic/card 触发顺序 | 先复用 CombatHistory；缺少因果来源时标 unknown，不从最终差值编造归因。并行/复合来源需明示 |
| 节点与选择结果 | map edges 与实际 Unknown 解析；奖励原始 offer、取得/跳过；事件页/选项结果；商店报价/交易；休息/升级；宝箱 | 生成、选择、效果三者关联；金币、牌、遗物、药水变化与原生命令对应 |
| RNG 与重放 | native run/player/combat/odds 及事件/同步器等额外流的算法/状态或可复现证据；策略 RNG 独立 | 同输入动作重放边界一致；不宣称当前九类战斗流已覆盖所有 run 流 |
| 成本与失败 | 预算、查询次数/扩展数、墙钟/CPU/内存/日志量、超时、首个差异、未完成步骤 | 原始 child exit + typed outcome；预算不是死亡，未知不是成功 |

### 信息与观察约束

- PlayerVisible 与 Oracle 数据分开输出/传给策略。隐藏抽牌顺序、未来 RNG/遭遇和内部敌方状态不应泄漏到可见策略；合法牌堆查看与游戏明确公开的选择候选按实际规则处理。
- 当前 `CombatObservationProjector` 只是一部分战斗投影，不能直接宣布它已满足全局可见信息隔离。Oracle 可以用于诊断与回放，但对应策略成绩必须标记。
- 尽量从已生成的 entries/字段读取，禁止观察时重新生成 offer、调用随机采样或带副作用 getter。已有身份记录只是报告编号表，应沿用这一原则。
- 具体例子：`RestSiteOption.Generate` 的原生说明明确允许消耗 RNG 或改变 run 状态；采集器应读取本次已生成的 Options，不能为日志再次调用 Generate。
- 先做观察开/关、不同日志等级的同动作重放比较，确认 RNG 和最终状态不变；日志序列化的时间也会影响墙钟搜索预算，必须从策略成本中区分、报告。
- 完整因果触发树比“伤害、格挡、生成等真实事件序列”更难。第一版可核验必需事件与范围明确的触发来源，不能将未知归因冒充完备事件图，也不应为追求全内部 trace 阻塞第一条原生 run。

## 9. 完整 run 的定义与候选验收层次

建议最初声明：固定 v0.111.0 / 41cef1ea，原版 Ironclad、单人 Standard、固定 Ascension 与内存解锁 profile；不装 Eleanor，不换牌组、不改数值，不使用 Debug 跳房/跳幕。默认 Act 列表作为输入记录，不假设一个永恒不变的“全内容”集合。

| 层次 | 需要真实证据 | 不能据此宣称 |
| --- | --- | --- |
| 原生单战独立执行 | 关闭预测前置门后，从自然初始状态到 Victory/Death；有合法选择和数据 | 原版全卡/全敌人都已覆盖 |
| 多种节点连续切片 | 同一 run 中实际奖励、地图、至少必要的非战斗节点和下一战；持久状态连续 | 通过替换 Unknown/跳过事件完成了完整流程 |
| 完整一幕 | 自然开局到首幕 Boss 实际胜利、原生幕转换与下一幕可行动边界 | 只有 Death 轨迹就证明 Boss/转幕支持 |
| 完整 run 范围 | 多幕实际旅行与战斗，至少一条真实最终 RunVictory 证据；另有真实 Death；所有路径严格分类 | 全种子必胜、可靠胜率、全角色或所有事件分支已支持 |
| 可用实验室 | 声明 cohort 的批量运行、失败可复现、数据契约与观察不扰动通过 | 有结果 JSON 就足以支持平衡或强策略结论 |

死亡是合法终局，应完整记录并计入分母；但早死批次不能替代未触达的幕转换与胜利路径验收。简单策略若不足以产生胜利，可另用预先标注的合法固定脚本或另一声明策略验证终局；不能补血、强杀怪物或修改随机结果。机制定向夹具可以提前定位转幕/特殊节点问题，必须与自然连续 run 分开，不计入自然完整 run 成功数。

策略可以合法跳过卡奖励、离开商店或选择事件的离开选项；这不是跳过房间。但要真实生成/进入/选择/退出，并明确该路径没有验证其他购买或事件分支。预设样本不可通过替换失败种子变绿；探索性寻找胜利轨迹与统计样本分开。有限多局表现也不能估算未经设计的“可靠整局胜率”。

## 10. 最小判别实验建议——仅建议，未执行

目的不是先承诺整幕开发，而是尽快检验“原生主线是否实质减少适配阻塞”。可在下一次获得实施授权后考虑：

1. **原生执行与 Solver 解耦试片**：原版 Ironclad、明确开局 profile，固定合法简单策略，第一战→真实奖励→下一自然战斗；完全不以预测 Capture/ManualPlay 成功为派发门。不支持的子选择停报。对比相同声明输入的原生事实流与冻结动作重放。
2. **非战斗接口探针**：真实模型的 event、shop、rest、treasure 各选一个明确实例验证选择及效果；原生生成/命令执行，不手工加资源。先做标注 API/room fixture，再选择预声明合法路线做同 run 连续片段；两种证据分开。
3. **提前检查尾部风险**：在明确标注夹具里查看 Boss 后同步、幕转换与最终事件/OnEnded 的安全路径。它不能代替自然 run，但可避免完成整幕后才发现宿主结构性障碍。
4. **数据非干扰控制**：同一冻结、同输入/动作，以必要日志和详细日志执行，比较资源、牌组、边界 RNG、选择、终局；验证不丢终局前历史、TestMode 缺失项由独立观察补足。

成功门：关键效果没有被 shim 跳过；原生策略遇新卡不再因缺镜像自动停；选择明确受控；输入与退出可追溯；不读写真实用户数据。观察结果应允许“不支持某房间/特殊选择”的真实失败。

停止/重评门：需要替换战斗或节点效果才能继续；无法判断异步任务是否完成；必须突破隔离或初始化未审查宿主；观察者会改变 RNG/状态；关键进度只存在于 Godot 回调且无法窄隔离。此时给出具体宿主方案与成本取舍，不重新回到逐卡补镜像，也不无限叠加无语义依据的 no-op。

## 11. 少量待决定事项与建议默认值

目前无需为只读结论追问。真正进入实施前，建议确定：

1. **支持范围**：默认先原版 Ironclad / Standard / Ascension 0 / 固定且明确的内存解锁档；确认是否要从含起始 Ancient 的正常开局开始。不得继承当前空进度偶然造成的开局差异。
2. **第一版数据目标**：默认满足第 8 节事实流与汇总，触发归因按可观察范围标记；暂不要求每个内部 Hook 的完整因果调用树。
3. **环境后备**：默认继续纯 .NET host；若静态审计/最小探针证明其不适合某个关键生命周期，再单独评估隔离 Godot headless host，不自动启用。

这些是候选默认值，不是对用户已有选择的改写。

## 附录 A：方向调整时收束的两卡最终复审

**结论：通过本轮限定范围的独立验收，无剩余必修项。** 不是全角色或任意监听器支持。没有因本结论继续修复或扩展 Mod。

- 接受冻结 `D5FF494B75BF670D2EFD0A335A6E87290F6C5A2C7B3C8511839CD68ACB0DFF1D`；manifest 与当前源码/编译源码/产物/输入重新核对通过。15 项 Eleanor 输入重新逐文件 hash，零差异。Harness SHA 为 `9EBF76E26E467D763545F374E0AACE73D3F70104BD59BE8EC8AAE568D7951452`。
- 36 个任务的原始退出与预期相符，237 条证据检查无失败。独立读取 90 个过程与边界报告，45 个场景双执行/45 次重复、112 个实际卡动作、516 个选择负控；逐步 exact、continuation、parent 与生成记录一致。
- 本次另在 `.build/runs/reward-repair-acceptance-fields-01/` 重跑既有四项选择字段负控：子进程均 exit 1；保留合法前缀与 native dispatch 总数分别 2/2/0/1；拒绝动作零额外派发，native continuation 不变。新负控 driver exit 0 代表预期拒绝通过，不是非法计划成功。
- RF/MC 非零重复实例选择与同父 A/B/A 已直接核对。MC 普通/升级夹具生成一张新 Essence，native 与 sim 独立 generation entries 对应；指定新对象真实打出并 Exhaust，旧 Essence 留手。完整永久 deck 序列化、状态/升级 key 与引用顺序均检查。对象编号按捕获世界分配，不能将不同 replay adapter 的局部编号当全 run ID。
- 两组各 12 条路径仍为原三个 seed、reward index 0、beam 8 / nodes 1000 / 5000ms / DoP1；逐条检查均第二战真实 Victory，各 320 个确认动作。ReturnFlow 与 Principle 的奖励引用/DeckVersion 和生成 Essence 实际执行证据存在；未替换失败种子或预算。
- 旧阶段 1 传播保障保留：online 累计 18/第二战 9，故障保留 11/第二战 2 且两端 Unknown；预算案例真实一步后仍存活，25 个分类/传播控制保留。未知变换/移动监听器没有因此被宣布全部支持或全部可拒绝；Death 仍限定进程隔离。
- 本次复审没有改实现、原报告或正式路线图，没有提交/推送；只新增独立负控运行产物。该结论收束两卡工作，不改变本候选分析尚未获批的状态。

## 附录 B：证据索引与复核范围

### Lab 源码及既有文档

- `src/SimulatorLab.Core/RunProgressProtocol.cs`：有限进度状态机及 terminal-act 明示未建模。
- `src/SimulatorLab.Core/CombatProtocol.cs`：Oracle/Visible 区分、现有观测投影和接口的实际范围。
- `tools/validation/NativeModProgression.cs`、`NativeSecondBattle.cs`、`NativeCombatDecision.cs`：两战原生流程、Eleanor 耦合和 Solver 前置调用。
- `tools/validation/NativeRunLoopProbe.cs`：旧原版 M3 的实际有限路径。
- `tools/validation/VanillaGameActionProbe.cs`、`VanillaCombatRootAdapter.cs`：原生 CardModel 调用与差分捕获。
- `tools/validation/EleanorV040Probe.cs`：写入拦截、终态截取、动作执行与原生/预测比较。
- `tools/validation/RewardCardSelectionBridge.cs`、`RewardCardBoundaryProbe.cs`、`RewardCardIdentityProbe.cs`：本轮拒绝、身份、deck 和 generation 修复。
- `tools/validation/StageOneEvidenceRules.ps1`、`ClassifyBatchResults.ps1`、`TestStageOneFailurePropagation.ps1`：失败分类、计数与负控。
- `docs/ELEANOR_NATIVE_RUN_LOOP_REVIEW.md`、`REWARD_CARD_REVIEW_REPAIRS.md`、`STAGE_1_REVIEW_REPAIRS.md`：原设计、安全边界和历史证据。`LONG_TERM_ROADMAP.md` 是先前正式路线图，本文未修改它。
- 本地固定 harness 的 `OfflineCombat.cs`、`GameBootstrap.cs`、`MainLoopContext.cs`、`MemorySaveStore.cs`：位于 `.build/local-reference/CombatSolver-v040-0bb36bc7e397/tools/search/OfflineSearchHarness/`；只读检查，许可与归属保持原约定。

### 冻结结果

- `.build/runs/reward-repair-final-build-01/build-manifest.json` 与 source snapshot。
- `.build/runs/reward-repair-final-01/summary.json`、`evidence-review.json`、`failure-propagation-controls.json`。
- 同目录 `reward-cards/`、`reward-choice-fields/`、`batch/`、`slimed-replay/`、`online/`、`decision-budget-1/`。
- 本次独立重跑：`.build/runs/reward-repair-acceptance-fields-01/summary.json`。

### 固定游戏的静态检查

游戏 DLL SHA-256：`0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`。使用已有 ILSpy CLI 10.1.1 查看类型；只读取本机程序集，没有初始化类型或执行目标游戏。除已有本地 `.build/review/RunManager.stageone.cs`、`CombatManager.stageone.cs`、`CardModel.cs`，本轮其余类型只输出到检查工具，不复制游戏源码到项目文档。

可重复静态查看形式：`ilspycmd --disable-updatecheck -t <完整类型名> <本机已锁定的 sts2.dll>`。重点类型：

- `MegaCrit.Sts2.Core.Runs.RunManager`：GenerateMap / EnterMapCoord / EnterMapPointInternal / CreateRoom / EnterNextAct / WinRun / OnEnded / UpdatePlayerStatsInMapPointHistory。
- `MegaCrit.Sts2.Core.Rooms.{EventRoom,MerchantRoom,RestSiteRoom,TreasureRoom,CombatRoom}`。
- `MegaCrit.Sts2.Core.Multiplayer.Game.{EventSynchronizer,RestSiteSynchronizer,ActChangeSynchronizer,MapSelectionSynchronizer}`。
- `MegaCrit.Sts2.Core.Entities.Merchant.{MerchantEntry,MerchantCardEntry}`、`Entities.RestSite.RestSiteOption`、`Events.EventOption`。
- `MegaCrit.Sts2.Core.Commands.CardSelectCmd`、`Models.{CardModel,PotionModel}`、`Combat.History.CombatHistory`。
- `MegaCrit.Sts2.Core.Models.Events.TheArchitect`、`Platform.StatsManager`、`Platform.Steam.SteamStatsManager`，用于标识终局/平台边界，非执行证明。

上述静态依据支持候选路线的可行性判断；完整 run 能力和新宿主边界的运行正确性仍待获批后的实验证据。
