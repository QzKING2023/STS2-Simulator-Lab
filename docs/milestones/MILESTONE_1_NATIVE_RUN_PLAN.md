> 2026-11-04：本方案 N0–N6 已按声明范围验收，结论与限制见 [M1_FINAL_ACCEPTANCE.md](M1_FINAL_ACCEPTANCE.md)。下文设计时预算/建议保留；实际最终配置以报告和冻结 plan 为准，不能用历史“待实施”说明否定已取得证据。

> 当前实施更新：自然 Boss/奖励/跨幕/Ancient及第二幕首战已完成分层验证，见[本轮证据](../native-run/NATIVE_RUN_MULTIACT_LAYERED_VALIDATION.md)。后续采用[分层验收与信任边界](../architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)：局部检查器修改先定向，生命周期稳定后最终同构建全量门保留。完整M1未完成，以下成文时的状态和规划说明作为历史基线保留。

# M1-NativeRun 实施方案：原版完整离线流程与可信数据

日期：2026-11-02。对应[正式路线图](LONG_TERM_ROADMAP.md)的新里程碑 1，**不等于已完成的旧 STAGE_1**。

**状态：用户已批准自主实施。N0／N1 的首个切片已完成本地验收，见 [NATIVE_RUN_N0_N1_VALIDATION.md](../native-run/NATIVE_RUN_N0_N1_VALIDATION.md)；其余门槛仍待实现／验证。** 本文保留成文时的静态分析，区分 F（当时确认）、R（建议决定）与 H（待实验）。验收标准本身不代表已通过，本文末尾的“本轮操作范围”是规划回合的历史记录。

## 1. 范围、默认配置与完成含义

### 1.1 建议默认配置

| 配置 | 建议值与理由 | 明确限制 |
| --- | --- | --- |
| 平台／游戏 | 当前 Windows 离线 host；v0.111.0 / 41cef1ea；本机合法程序集，锁哈希 | 暂不升级游戏、不宣称跨平台 |
| 角色／模式 | Ironclad、单人 Standard、Ascension 0、无 Daily | 不要求全角色或全部难度 |
| 内容／Acts | 使用该固定版本原生默认 Acts、原生生成与发现的原版内容 | “支持”按实际内容／机制矩阵声明，不把有限 run 推广到所有组合 |
| 解锁档案 | 独立、明确命名的已解锁 Lab 档案，包含正常 Neow 开局；原生 UnlockState 序列化锁定 | 与用户进度无关；不是零解锁新账户或零售教程验收 |
| 起始状态 | 原生新 run、原生起始牌组／遗物／HP／金币；普通节点进入 | 不用 Debug encounter 充当自然开局，不手动补资源以确保胜利 |
| 默认策略 | 有限预算、确定性、玩家可见信息的简单合法策略 | 先保证执行；不承诺获胜或全局最优 |
| 预测 | 可选 CombatSolver 插件式内部策略适配 | 无预测必须记录 NotCompared，不以此阻断可合法原生执行的动作 |
| 实验隔离 | 一个 run 一个进程；全部存储在 Lab，MemorySaveStore，禁真实保存／Steam／网络副作用 | 暂不恢复真实存档、不复用死亡 worker |

**F：档案不是无关配置。** `SaveManager.GenerateUnlockStateFromProgress` 读取内存 Progress；`RunManager.SetStartedWithNeowFlag` 检查 Neow 解锁，地图起点会受影响。原生 `UnlockState` 有显式集合构造、`all`、序列化接口；`FromSerializable(null)` 会退到 all。现有默认空 Progress 不能被当作“通常玩家的完整开局”。

**R：N0 将档案内容与完整初始 Progress 投影一起冻结。** 可从原生 all 档案提取本版本解锁集合，再以明确的实验档案序列化保存；all 中的实验计数必须标注，不能声称是真实游玩统计。逐项确认影响 Ancient、事件、内容抽取的可见 Progress 与 RunState.UnlockState 一致。禁止用 null 隐式选择档案或加载用户存档。若全解锁 Lab 档案与自然流程定义冲突，调整并版本化档案，不能中途改值。

### 1.2 “完整”的验收定义

- **完整胜利路径：** 同一 run 从上述正常开局，经实际房间、奖励、合法地图行进和默认 Acts，到最终原生 RunVictory。必须保存整个动作／选择前缀与幕转换，不能拼接不同场景。
- **死亡路径：** 真实原生致死结果、最后有效边界、未发放胜利奖励、独立进程结束。死亡是有效策略结局，但不能代替完整胜利路径的功能证明。
- **流程覆盖：** 战斗、奖励、地图、休息／升级、事件、商店、宝箱／遗物、药水、精英／Boss、幕转换、最终 Architect 事件与 run 终局均有指定证据。允许分离夹具补充类别覆盖，但至少一条完整胜利必须自然连续。
- **范围有限：** 不要求全部卡牌组合、全部事件分支、全种子、全角色或可靠总体胜率。未测内容和已发现 unsupported 都列在支持矩阵中；验收队列中的必需功能不能以“未测”关闭。

不做公开 Mod SDK、新 Eleanor 玩法、整局最优策略、全 run fork、独立重写原版规则、真实 UI 或云存档。本方案没有新增这些权限。

## 2. 本轮静态核验改变了哪些实施判断

| 事实 F：当前入口／行为 | 必须采取的实施决定 R | 不能接受的捷径 |
| --- | --- | --- |
| `ValidationOutcomeV1.Validate` 的 Completed 要求 native/sim 相同终态、Exact 和 Complete；`FromStatus` 会同时填两者 | 新增原生 run 结果契约，保留旧差分结果规则 | 调用 FromStatus("passed") 虚构模拟器结果，或放宽旧 Completed |
| `NativeCombatDecision.Ticket.Consume` 依赖 CombatRootSnapshot／continuation；Query 直接调用 ModRuntime 与发现审计 | 新原生决策票据与 native 状态边界；solver 使用可选包装器 | 原生动作先要求完整 solver capture 成功 |
| `NativeModProgression`、`NativeSecondBattle` 绑定 Eleanor、两战、奖励覆盖策略及在线／冻结路线 | 复用可证明的原生命令与隔离组件，新增通用 run 控制路径 | 把现有两战函数包进 while 就宣称完整 run |
| `RunProgressProtocol` 只验证有限奖励／节点阶段，末端无子节点会拒绝 | 扩展生命周期模型，但保持原生执行负责效果 | 把协议合法性当作奖励、事件或幕转换执行 |
| `FromChooseABundleScreen` 在 TestMode 取 bundles[0]，不经过普通 selector；仍有 choice ID 预留和日志 | 对该固定版本设置窄选择介入点，保留原始守卫、同步／记录与效果 | 认为 UseSelector 已接管所有选择；默认首项覆盖策略请求 |
| `EventSynchronizer.AwaitPendingOptionTasks` 捕获异常后清理；选项还经过 TaskHelper.RunSafely | 在真实任务／原始选择调用处记录异常与完成关系，验证事件状态 | 队列空、外层 await 完成或 exit 0 就算事件成功 |
| `CombatManager.EndCombatInternal` 在生命周期中清空 CombatHistory；地图 stats 更新在 TestMode 跳过 | 清理前追加式采集，另建事实投影；原生历史缺口标明 | 以终局空 History 相等证明事件完整；为填日志重执行效果 |
| `WinRun` 在 OnEnded(true) 后还会清场杀死玩家 | 从原生 run 终局调用／事件判定 Victory 并保留顺序 | 仅凭最终 HP=0 判 Death |
| TestMode 下地图动作直达 EnterMapCoord，而 UI 的可行进状态还有 `Hook.ShouldProceedToNextMapPoint` 等守卫 | 复用生产非视觉条件、同步器和当前地图身份 | 仅检查 Children 就绕过房间尚未结束或 Hook 禁行 |
| 原生 Death 不走 AfterCombatEnd；原版／Mod 既有证据均有限 | 保留真实死亡状态，报告清理边界，进程隔离 | 擅调用胜利清理、改角色状态或假设同进程可再次开局 |

代码位置与固定程序集证据索引在第 12 节。以上均是静态事实或既有测试事实；新的无 UI 适配是否可行仍需 N1 判别实验。

## 3. 架构与迁移边界

### 3.1 内部组件与依赖

~~~text
固定输入/配置 → OfflineNativeHost → NativeRunController → 原生命令/同步器/模型
                                         ↕ 合法请求、一次性决策
                                     RunPolicy
                                     ├─ 简单合法策略
                                     ├─ 冻结动作重放
                                     └─ 可选 CombatSolver 战斗适配
原生边界/效果/任务 → RunObserver → 原始事件 + 状态投影 → 独立检查器/可重算汇总
可选预测分支 → 原有严格差分结果（不代替原生 run 结果）
~~~

这些名称是内部职责建议，不是公开接口承诺。先在现有 harness 构建内落地；原生路径不得加载 MyMod 或为运行原版而要求 Mod 注册成功。是否拆独立项目在完成最小依赖证明后决定，避免同时重写构建系统。

| 现有组件 | 迁移方式 | 保持不变的责任 |
| --- | --- | --- |
| GameBootstrap、MainLoopContext、MemorySaveStore | 抽出无 Mod 依赖的原生 host 配置与守卫；bootstrap 补丁逐项登记 | 主线程、合法程序集、内存存储、源码／版本／补丁来源审计 |
| EleanorV040Probe 中保存与 replay 拦截 | 提取公共安装／检查逻辑，保留旧 profile 调用入口 | 精确签名、调用次数与拒绝未知持久化路径 |
| NativeModProgression 中原生领奖、进入节点 | 按小组件复用；旧两战驱动继续作为冻结回归 | 真正原生生成／领取，不手动 append 卡或金币 |
| AdapterCardSelection／RewardCardSelectionBridge | 复用候选身份、来源、完整消费及拒绝模式；增加原版 room scope | 不能直接复用 Eleanor 专有候选过滤或许可假设 |
| NativeCombatDecision／ReplayVerification | 保留差分用途；新增 native-only 票据和确认前缀 | 不削弱父 fork、continuation、跨战旧动作拒绝 |
| 结果与批次检查器 | 复用穷尽分类、退出码核查、非空证据与负控方式；新 schema 独立验证 | 不能把新 Completed 塞进旧差分聚合器 |
| CombatObservationV1／VanillaCombatRootAdapter | 保留现有用途，另建最小原生 run 投影 | 不把有限 Hand/Discard DTO 当完整 run 状态；不要求 solver 才能观察 |

### 3.2 生命周期与完成条件

建议状态：`Boot → EnteringRoom → AwaitDecision → Executing → Resolving → AwaitDecision/Rewards/Travel → TransitionAct → Finalizing → Terminal`；所有非终局状态都可以进入明确的 `Stopped`。

控制器一次只派发一个根动作，在主线程等待其原生任务、关联子选择和房间状态达到规定边界。**任务泵空闲只是必要观测，不是房间完成。** 每类房间提供“等待什么、允许哪些动作、什么证明完成”的小型内部适配器，不另写游戏规则。

- 进入节点前确认 room completion、生产守卫、地图 generation 与合法目的地。
- 战斗等待玩家 Play、实际终态或明确请求；不能沿用只等 Play 的 120 秒循环处理所有房间。
- 嵌套事件战斗结束后走原生恢复事件路径，不能直接跳地图。
- 幕末进入原生 ActChangeSynchronizer，再追踪原生 EnterNextAct；不能把无子节点当 run 已结束。
- 捕获 terminal stamp 后，分别记录 native battle terminal、后续原生 cleanup、run terminal。它们不是一个状态，也不是全部经过仿真差分。

### 3.3 策略、动作与子选择协议

默认先实现透明、确定的 baseline：按可见且合法的稳定顺序选可打牌及目标，无可打牌时 EndTurn；奖励、地图和事件使用事先写明的排序／跳过规则；只在原生允许时跳过。初版可保守地不用主动药水，但仍须处理拾取、容量、自动药水效果，且有独立用药覆盖。排序规则、可见信息与策略版本随配置冻结。获得胜利需要改策略时建立新策略版本，不能改测试样本或偷偷补资源。

每个请求包含：run/act/room/combat ID、boundary sequence、request ID、父 action/choice ID、选项实例 ID、原生候选顺序、数量／是否可取消／是否可跳过、所需目标与来源。每个动作包含对应请求及显式选择；只允许本 kind 的字段，拒绝缺失、多余、旧、跨房间、重复消费或不支持的非空字段。隐式单候选／自动选择也需观察记录，不能伪称策略主动选择。

原生票据使用 native run/room/combat 对象关联、单调边界和必要原生状态投影，重新检查合法性后消费。隐藏状态 hash 可供内部防旧与重放，不交给玩家可见策略。票据创建、候选枚举本身若能触发 RNG 或产生对象，则必须变为一次明确的原生生成动作并缓存其结果，不能每次查询重生成。

CombatSolver 可用于可支持的战斗边界。记录查询预算、原生状态不变、候选与退回原因；选择不支持或超预算时按预先配置的显式 fallback 策略重新提出原生动作，或者停止。不得执行未知是否合法的 solver 尾部，不得把 fallback 说成预测通过。全流程不以 solver 覆盖率为入口门。

## 4. 内容依赖与实际入口矩阵

下表的入口已静态核验或已在有限旧流程使用；**没有一行意味着该类内容已完成新 M1 验收**。

| 类别 | 可复用原生入口／事实 | 未验证依赖与必要处理 | 必须取得的窄证据 |
| --- | --- | --- | --- |
| 开局／档案 | RunState.CreateForNewRun、Player.CreateForNewRun、默认 Acts、SetUpNewSingleplayer、FinalizeStartingRelics、Launch、EnterAct | 空 Progress 与 Neow 差异；既有 Debug encounter；原生初始牌组和 first draw | 普通开局 profile hash、原生牌组／遗物、Neow 决策及首战首个合法边界 |
| 战斗 | CardPlay、EndTurn、ActionExecutor、CombatManager 已有实际回放基础 | 原版 turn-start／card 子选择、生成对象、敌方阶段终局；选项不能只支持旧 Mod 范围 | 无 solver 真实战斗到 Victory/Death，全部派发与边界有证据 |
| 奖励 | RewardsCmd.OfferForRoomEnd、RewardSynchronizer 的原生选择／跳过 | 奖励是否已发放、卡／金币／药水／其他奖励、容量与嵌套选择 | 原生列表与领取 ID、金币实际变化、卡牌来源和后续牌组、skip 无伪领取 |
| 地图 | 原生地图生成、MapSelectionSynchronizer、MoveToMapCoordAction、EnterMapCoord | 当前 generation、合法边与生产 Hook、未知节点原生 roll；UI travelable 也有语义守卫 | 非法旧节点零派发；合法选择、实际房间与访问历史关联 |
| 休息／升级 | RestSiteRoom 原生生成选项、RestSiteSynchronizer.ChooseLocalOption、Smith 原生选牌／CardCmd.Upgrade | 生成选项可改状态／用 RNG；启用条件、取消、附加选项与离开条件 | 同一永久卡升级身份、资源变化、取消与合法继续，不手工 Upgrade |
| 事件／Ancient | EventRoom、EventSynchronizer.ChooseOptionForEvent、EventModel、原生选项任务 | EventModel.OnRoomEnter 与 UI 回调次序；资源 preload；事件继续、嵌套战斗、被吞异常 | 非首项、不可用项拒绝、阶段切换、内层异常传播；至少一个嵌套流程 |
| 商店 | MerchantRoom、原生 Inventory、MerchantEntry.OnTryPurchaseWrapper | 付款／存货／补货／移除选牌、遗物／药水副作用、UI-only 未售历史 | 真正扣金并获物；不足金币／售罄拒绝；移除指定永久实例；合法离店 |
| 宝箱／遗物 | TreasureRoom.BeginRelicPicking、原生 relic picking 同步器、额外奖励 | 独占领取、额外奖励、遗物获得时 Hook 与特殊选择 | 原生生成与领取、遗物跨战保留、至少一个影响后续动作的实际 Hook |
| 药水 | PotionModel.EnqueueManualUse、UsePotionAction、目标检查和原生效果 | Usage 时点、目标、容量／替换／丢弃、战外或自动触发；enqueue 不等于完整合法校验 | 普通有目标使用与消耗；非法时点／目标拒绝；相关自动效果单列 |
| 精英／Boss | 原生 encounter 抽取与 CombatRoom；现有战斗驱动可复用 | 新敌人机制、阶段、奖励、终局动作与音乐／场景依赖 | 实际完成精英和 Boss；不能从普通 Monster 推导支持 |
| 幕转换 | ActChangeSynchronizer、VoteToMoveNextActAction、RunManager.EnterNextAct／EnterAct | ready/act index、异步 EnterNextAct、地图／房间 teardown、下幕初始事件 | 同一 run 上一幕 Boss → 下一幕真实地图／首决策；持久状态链完整 |
| 最终事件／胜利 | EnterNextAct 最终进入 TheArchitect；该事件后走原生 WinRun | 对话、OnRoomEnter、动画与统计；TestMode 下没有 NEventRoom；WinRun 清场顺序 | 原生选项推进到实际 RunVictory，而非直接调用 WinRun；清场后仍判 Victory |
| 死亡／run 结束 | CombatManager.ProcessPendingLoss；RunManager.OnEnded 等相关链 | Death 不调用 AfterCombatEnd；完整零售 loss 回调可能依赖 UI；最终保存入口 | 真实死亡、无错误领奖、清理边界／残留披露、单进程结束；不伪称零售清理已执行 |
| 跨节点持久状态 | 原生 player/deck/relic/potion/runRNG 与 map history | TestMode 的历史省略、额外临时牌／power 的规则、角色／遗物特例 | HP/maxHP/gold/deck/upgrades/relics/potions 连续；临时状态按具体原生规则而非全清零 |

遗物与药水不是单独房间开通后就全部支持。使用原生 Hook 可避免逐效果手工镜像，但其选择、宿主依赖和事件来源仍须在实际遇到时验证。未知原生内容若能通过已验证的通用入口完整执行可标明“原生实际执行、未独立差分”；未知宿主调用或无法解释的决策停点必须 Unsupported，不能无声略过。

## 5. TestMode、无 UI 边界与保存隔离

### 5.1 允许的介入与禁止的替代

维护版本锁定的 host seam 清单：目标方法／程序集／原始方法 hash、介入原因、行为类别、处理者、调用次数和验证案例。区分：

1. **展示／IO 依赖替代：** 资源 preload、动画、hover-tip、UI 节点、实际保存／replay。仅在已确认不执行规则的范围内 stub。
2. **决策 provider：** 原生候选生成之后提供合法选择，继续原生付款、变牌、奖励、事件效果、同步与日志。
3. **观察器：** 只读事实投影及追加记录，不调用效果。
4. **原生游戏语义：** 继续运行；不得用手工资源变更或固定期望状态替代。

既有 bootstrap 的广泛 Godot 类型初始化处理必须逐项复核，不能推断所有新增类型的静态初始化都纯属展示。若新的完整运行必须依赖无法安全替代的引擎能力，提交具体 host 方案与权限需求，停在该边界；本方案不授权安装引擎、关闭审计或启动 UI。

### 5.2 三个必须显式解决的差异

- **Bundle：** 普通 card selector 无效。优先窄拦截 TestMode 自动首项决策，保留 CombatEnding／空集合守卫、ReserveChoiceId、候选顺序、原生记录及后续效果。若需要替换代码分支，精确版本／方法／补丁审计，不能整个方法直接返回某组。非零选项、非法 index、旧请求、缺失／额外选择必须有控制证据。
- **历史：** CombatHistory 在清理前就复制为不可变记录；不能在清理后补采。TestMode 跳过地图 stats、商店未售等原生记录时，独立采集实际事实并标记来源，不能称为“原生历史原样导出”。只补必要观测，不恢复整个 UI／保存链。
- **UI 携带的生命周期：** NEventRoom.Create 在 TestMode 返回 null；EventModel.OnRoomEnter／最终事件有独立行为。先追踪真正生产回调的调用者、次序和 exactly-once 条件，再给无 UI host 接入同一语义入口。尚未证明的回调不能一律手动调用，更不能直接强制结束事件。

### 5.3 持久化、网络与异常退出

继续 shouldSave:false、MemorySaveStore、SaveProgress／WriteReplay 离线拦截与检查。静态检查 RunSaveManager 的两类重载、SaveManager.SaveRun 附带 progress、OnEnded、历史／metrics／Daily／Steam 入口；不能只验证一个 SaveRun 方法。固定 Standard、无 DailyTime、不初始化 Steam；禁止云端、遥测／上传和真实用户目录 IO。优先不启用相关客户端，再对可能路径建立精确 fail-closed 守卫；拦截网络不能隐藏应执行的游戏效果。

安全证据包含实际触达的保存／网络入口、阻断次数、内存写入、允许的 Lab 报告写入、未出现持久文件差异的限定检查范围。不能仅凭“未见存档”宣称证明了所有 OS 写入。新未分类 IO 入口要停止并定位。

取消只在确认边界停止派发；原生 ActionExecutor 的取消会转为异常，不能取消一半动作后接着重放。硬 watchdog 仅结束本次子进程，保留最后确认前缀，当前动作标 Interrupted／Unknown。不确定是否执行时不能重试同一动作。死亡进程不复用，零售 loss 清理未覆盖部分单列；不调用 AfterCombatEnd 消除真实残留。

## 6. 最小可信数据契约

### 6.1 由三个首批问题倒推

1. **这一局为何推进、死亡或停止？** 需要动作／选择、HP／格挡／资源变化、敌方行动、来源和失败边界。
2. **奖励、事件、商店或升级如何影响下一战？** 需要真实取得／移除对象、永久身份、金币／HP、跨节点状态及后续使用记录。
3. **相同输入与决策能否重现？成本在哪里？** 需要版本／档案／seed、实际决策、RNG 边界、完整性、预算与分层耗时。

先支持这些查询；不为尚未提出的全量分析无限记录对象图，也不把单局数据变成平衡结论。

### 6.2 数据层与最少字段

| 数据层 | 必要字段／记录 | 完整性规则 |
| --- | --- | --- |
| RunHeader | schema、runId、源码／构建／依赖 hash、游戏版本、档案与策略配置、seed、信息权限、预算、host seam 清单 | 配置变更必须换 run 身份；禁止以 HEAD 代替 dirty 源 |
| 身份 | act/mapGeneration/node/room/combat ID；player/creature/card/relic/potion 实例；永久 DeckVersion／战斗副本／生成源关系 | 不能仅用卡名、手牌序号或观察时首次位置当永久身份；所有引用可解析 |
| DecisionRequested／Resolved | boundary、actionId、choiceId、父请求、原生合法候选／目标、数量、取消／跳过规则、实际选择、implicit 标记 | 每个必需请求恰好有一个终结；未知、多余、旧请求拒绝并留证 |
| 执行与状态 | root dispatch/start/finish、原生任务关联、前后 HP/maxHP/block/energy/gold 及原版特有资源、piles、完整永久 deck/upgrades、relic/potion | 分清派发与完成；前缀累计不被分段覆盖；临时状态不得凭规则猜写 |
| 效果事实 | DamageResult、实际 HP loss／blocked/unblocked、BlockGained、攻防来源、Power、CardGenerated/Drawn/Discarded/Exhausted、MonsterMove、Potion 等 | 实际数值与 Hook 阶段；来源未知明确 null/reason，必要来源缺失则数据门失败 |
| 流程 | room enter/exit、原生奖励列表／领取、地图选择、事件阶段与选项结果、商店交易、休息结果、act change、terminal／cleanup | 可还原同一 run 顺序；夹具入口、自然流程与冻结路线明确分开 |
| RNG／重现 | seed、已盘点的 run/player/combat/event/同步器流及可读取状态／计数、策略独立 RNG、边界签名 | 不为观测调用随机接口；未覆盖流明确标记，不能声称任意快照可恢复 |
| Policy／性能 | 信息模式、策略版本、查询次数／候选／节点／墙钟预算、fallback、耗时分层、日志字节／内存 | 固定路线重放与在线求解分开；采集开销不偷偷改变策略预算 |
| 结果 | RunVictory／Death／Ongoing／Unknown，stopReason，原生执行覆盖、数据完整性、可选 comparison，原始 exit，最后确认边界 | 完成的原生 run 可 NotCompared；有差分 Mismatch 或缺必需数据不得报告验收通过 |

`CombatHistory` 的已有实际条目可提供攻击、伤害、格挡、卡牌、能量、power、药水等事实。它们可能在 AfterX Hook 之前记录，而且持有可变对象引用；应在通知时复制必要字段，记录阶段，再由稳定 ID 关联。不能把一个 action 的总伤害等同于每个触发器的完整因果链。N2 明确“必要来源”列表；复杂来源无法归属时如实暴露缺口。

事件采用主线程单调 sequence + run/room/action/choice/parent 关联；墙钟只作性能数据，不用于逻辑排序。异步子任务需显式关联、结束／异常记录。事件选项异常不能被 TaskHelper／同步器吞掉后标完成：对原始调用建立只记录不改返回值的异常 ledger；如缺乏安全观察点，当前内容 Unsupported。

### 6.3 数据暴露、存储与观察不扰动

- **玩家可见投影：** 手牌、已知牌组组成、公开敌意／资源、当前选择等经逐字段核验的内容；隐藏 draw order、未来 RNG、完整内部状态和 hash 不进入策略。
- **Oracle 记录：** 复现／审计所需内部事实单独存储并标注，不能通过派生特征、候选排序或实例 ID 编码泄露未来信息。普通合法性检查可用私有状态，但结果不能包含隐藏预测。
- **原始记录：** 分块 JSONL，header／chunk hash／结束标记及总条数；关键边界保存结构化快照，避免每一 Hook 复制全对象图。汇总由原始记录重算，原生历史与 Lab 观测来源分开。
- **不完整报告：** sequence 缺口、悬空实例、未完成必需选择、异步异常、文件截断、缺 terminal/cleanup、关键事件丢失均不能通过；记录 DataIncomplete 或 DataError，保存有效前缀。到日志配额时显式停止，不静默丢样本。
- **非扰动门：** 在相同冻结动作上比较采集开／关、最小／详细两模式的原生状态、候选、RNG、终局；不得反复调用有副作用的 getter、生成选项或序列化钩子。先证明语义不变，再量化耗时／内存。墙钟求解受采集开销影响时单列，不以其路线一致作为唯一不扰动证明。

## 7. 结果、预算、回放与独立验收

### 7.1 新原生结果，不改旧差分定义

建议新增 `NativeRunResultV1`，至少分开：

- nativeRunOutcome：RunVictory / Death / Ongoing / Unknown；
- stopReason：Completed / BudgetExceeded / Unsupported / InvalidAction / EngineError / EnvironmentBlocked / Cancelled / Interrupted / NoCandidate / DataError（以及实际比较的 DifferentialMismatch）；
- dataCompleteness、nativeCoverage；optionalPrediction 的 requested/available/comparison；
- counters：nativeRootDispatches、completedRootActions、resolvedChoices、differentialActions，各自作用域；
- lastConfirmedBoundary、inFlightAction、原始异常和子进程 exit。

Completed 必须有**实际 run 终态**及完整必要数据；BattleVictory 只是流程事件。Death 可以是正确执行结果，预算耗尽不是 Death 也不是完成。未知枚举、exit 矛盾、空步骤、缺内层结果都拒绝。预期负控单列；wrapper 成功不改变其子进程失败事实。新的批次汇总不能套用旧 `FromStatus` 来补模拟器终态。

### 7.2 初始预算与样本注册

预算是保护执行的初始建议，不是性能目标：单根动作／房间进入等待 45 秒，单战 100 回合／1,000 根动作，单 run 20,000 根动作／256 房间／30 分钟，嵌套选择深度 16、每根动作 128 个选择，原始日志配额初始 512 MiB。均在 N0 作为显式配置确认；等待动画仍耗时等原因要诊断，不得直接扩大预算掩盖挂起。若确需调整，保留原结果并新建配置／实验编号。现有 solver 的 1,000 nodes／5,000 ms 等只适用于原有 fixture，不冒充整局预算。

建议开发 seed 前缀为 `LAB_V0111_NATIVE_DEV_`；在 N6 前冻结评估六个字面种子：
`LAB_V0111_NATIVE_EVAL_01` 至 `LAB_V0111_NATIVE_EVAL_06`。
每个配置第一次真实执行，第二次在新进程冻结动作重放，共 12 条路径；重复是复现检查，只有六个不同 seed。配置、合法策略和内容范围必须在查看评估结果前锁定；失败不换 seed，不改变奖励 index 以回避内容，不从成功样本补齐数量。额外策略／预算实验单列。

六个 seed 都早死时可说明这一批策略弱，不能关闭 M1。开发集可合法改进 baseline、使用声明为 Oracle 的 solver 或准备完整冻结决策路径；所有步骤仍由原生验证执行。为调试尾部可用正式 API 建立局部 fixture，但不得将该结果混入自然完成率或冒充完整开局胜利。

### 7.3 独立检查与回放边界

- 纯协议检查：旧／跨战／跨节点票据、重复派发、不支持字段、缺失／多余选择、错误退出与空证据；作用于实际 production 汇总逻辑。
- 独立数值／结构契约：固定本版少量原版卡升级、交易扣款、金币奖、实例变更、房间合法性、终局顺序；期待值经静态审查写入版本契约，不从运行时被测 DynamicVars 同源求出。
- 实际流程：原生执行的每一步可回放，后续资源与永久身份连续；可预测的步骤保留原有完整差分，其他明确 NotCompared。
- 故障控制：内层事件任务异常、损坏原始事件、过期选择、取消／预算、observer 配额、无合法候选。注入故障标 control，不当作自然缺陷或真实策略死亡。
- 回放：从同一锁定开局和决策序列重执行，检查中间原生状态与已盘点 RNG 边界；不承诺任意中途恢复。不同策略同 seed 可能消耗不同 RNG，只能称共享初始 seed，不能据此宣称严格因果配对。

## 8. 工作包、依赖、交付与停止门

以下均为**待实施的工作**。总体依赖为 N0 → N1 → N2 → N3 → N4 → N5 → N6；N1 中尾部风险探针和 N2 数据细化可在接口冻结后独立推进，但正式集成仍按依赖顺序。

| 工作包 | 范围与可用交付 | 通过门／不通过时处理 |
| --- | --- | --- |
| **N0 基线与契约** | 冻结真实源码／输出／工具链／原版输入；原生无 Mod host profile、档案与 seam 清单、结果／数据 schema、预算；审计新的保存与终局调用链 | hash 完整、安全入口有范围、无隐式 solver/Mod 前置；发现额外宿主权限需求先停该依赖，完成独立设计 |
| **N1 判别性纵向切片** | 简单策略不用 solver；自然 Neow／起点 → 首战 → 原生奖励 → 合法下一节点／首决策；同一配置两次真实执行与最小数据 | 至少有实际首战 Victory 可验证奖励链；真实 Death 另记；若仅碰到死亡或 UI 阻塞则切片未完成，诊断合法策略／宿主，不改资源 |
| **N1-T 尾部风险探针** | N0 安全边界下单独标注 fixture，验证原生 act-ready → EnterNextAct 和 Architect 实际选项／回调路径；仅做最小依赖探测 | 证明无 UI 调用路径或取得精确阻塞；不直接强制 WinRun，不把 fixture 算自然胜利。必要 host 架构变更应此时暴露 |
| **N2 选择与数据可信门** | Bundle 非 0、单／多／隐式／连续选择、稳定实例、原生票据、事件任务异常 ledger、清理前采集；实际 production 检查器 | 拒绝控制零新增派发；非空数据、内层错误传播、observer 不扰动、完整原始→汇总重算；任一缺口不得以仅最终 HP 相同通过 |
| **N3 房间适配与多节点** | 休息／升级、事件（含嵌套）、商店交易／移除、宝箱／遗物、药水、精英／Boss 的窄案例；继而连续多节点 | 每类入口的候选／效果／结束条件有证据；原生付款与取得，不跳过未知房间；未支持项显式停点 |
| **N4 第一幕与跨幕** | 自然同一 run 实际完成第一幕 Boss／奖励／act ready／下一幕开局；冻结重放；跨幕状态契约 | 真正新 act/mapGeneration/首决策；持久属性保留、临时状态按原生规则；若上一幕 Victory 后只设置 act index 则失败 |
| **N5 完整 run 与终局** | 至少一条从正常开局到最终 Architect／RunVictory 的实际轨迹与两次新进程重放；另有真实死亡；所有默认 Acts | 胜利由原生流程产生，日志包含清理前后和最终杀玩家顺序；死亡过程不污染后续进程；只有尾部 fixture 不通过 |
| **N6 同构建与交付** | 六 seed × 执行／冻结重放；数据问题 Q1–Q3 查询示例；瓶颈测量；原版、Core、适用冻结 Mod 回归；独立审查 | 验收队列零未解释 Engine/Data/Diff/Invalid 错误；正常队列必须到实际 Victory/Death，预算／unsupported 保留但相应路径不能算完成；全部门无必修缺口 |

N1 的基础请求／最少数据来自 N0，不等待 N2 全量功能；N2 是扩内容前加固门。N3 每个节点适配必须先确定真实生产条件，不能把 UI 剥离当无条件放行。

### 自然路径、fixture 与后期可达性

每份证据必须有 `scenarioKind`：natural-run、frozen-natural-replay、api-fixture、lifecycle-fixture、fault-control。fixture 用来隔离难题，允许从明确构造的局部状态起步，不计自然完成率。冻结自然路线必须能追溯到同配置的实际合法动作，重放不改 HP／RNG／奖励。无法获得任何自然完整胜利时，应报告“执行片段与后期 fixture 已验证，完整 run 尚未验收”，不降低定义。

## 9. 构建、回归、性能与可复现交付

- 冻结包括实际 dirty Lab 源码、上游 source tree／overlay、项目与构建命令、工具版本、所有加载程序集、host profile 和输入 hash。版本字符串相同不够；最终归档必须可定位所编译文件。
- 一次 M1 验收使用同一最终产物；修代码后重新冻结并重跑受影响及最终必需矩阵。旧结果按旧 ID 保留，单列不能同构建的兼容 profile。
- 优先级：Core 协议与失败传播 → 原版现有战斗／RNG／有限奖励回归 → 新 native-run → 涉及共享组件的冻结 Eleanor 回归。最终对适用旧 Mod 范围跑冻结矩阵，不增加新 Mod 内容；独立 legacy 构建身份明确列出。
- 性能分初始化／模型发现、原生动作、等待／同步、观察复制、序列化、solver；先单进程定位瓶颈，再考虑并行独立 run。共享静态状态／主线程未隔离前不并行同一原生 run。
- 发布文档使用可配置本机路径，源码／许可与依赖分开；游戏 DLL、反编译游戏代码、用户目录、Agent 文件与原始运行报告不入仓库。保留上游 MIT/NOTICE，不把依赖重新许可为 Lab。
- 未来复现入口应提供 inspect-inputs、execute-native-run、replay-native-run、review-evidence 四种职责及示例配置。命令名待实现确认，本轮不虚构可执行的新脚本。
- 最终交付：支持矩阵、版本／档案／策略 lock、schema、执行与重放入口、原始事件和衍生查询示例、同构建验收矩阵、失败与未覆盖清单、性能基线、独立审查结论。原始大型产物在 Lab 本地，公开文档仅给安全相对位置与摘要。

## 10. 分工、模型与检查点

本轮没有启动并行代理。后续建议沿用 **Standard**，关键设计与独立验收用 Astra xhigh，编码／定向修复用 Sol xhigh。实际模型／推理配置只按会话元数据记录，不凭自称推断，不自动切 Fast。

| 责任 | 可独立工作 | 必须串行的交接 |
| --- | --- | --- |
| 集成负责人：host／生命周期 | N0、N1 主路径、保存／网络边界、最终集成 | 独占共享 bootstrap、构建 overlay、最终 run 入口；其他人不同时改这些文件 |
| 数据与检查负责人 | 已冻结 schema 后实现 observer、完整性／结果检查、内存负控 | 事件与 ID 契约先审查；写入生产 hook 由集成者合并；不能另改游戏规则 |
| 房间适配负责人 | N2 后按独立文件处理休息／商店／事件等一个类别 | 基于同一 host/choice 版本；涉及公用 seam 的变更先交接；不并行共享缓存构建 |
| 策略／性能工作 | 合法 baseline、已有证据分析、独立策略配置 | 不成为原生主线前置；不通过改样本帮主线验收 |
| 独立审查 | 只读实现与冻结证据、必要有界负控 | 在提交给验收的冻结构建上审查；提出必修后由实现者修复再冻结 |

每个工作包结束报告：实际完成／未完成、当前版本与配置、自然或 fixture、真实原始 exit、原生终局与 optional diff、数据完整性、下一依赖与具体阻塞。长命令给进度，不重启已运行的测试。一次只由一个集成进程使用共享构建缓存，独立证据输出目录不可重用覆盖。

## 11. 需要决定的事项与完工清单

以下有推荐默认值，实施前写入配置即可，不要求反复向用户确认已批准主线：

- Ironclad / A0 / Standard / 已解锁独立 Lab 档案。影响起点和内容范围；改成零解锁档案会新增教程／解锁流程验收。
- 玩家可见 baseline 为默认，Oracle solver 另列。影响策略比较和胜利证据标签，不改变实际原生执行要求。
- 一个 run 一个进程。初始化成本暂高，但延续已验证隔离，避免死亡／静态订阅污染。
- 六个预登记评估 seed，开发 seed 分开；不得据有限样本声明总体胜率。

真正需要停止并另提方案的情况：现有合法程序集无法在当前无 UI host 运行关键语义；必须增加宿主访问／安装依赖／放宽安全审计；必须用替代规则才能推进最终事件；或者公开契约需求超出当前内部最小接口。只暂停依赖这些变化的部分，继续可独立完成的只读诊断与文档，不绕过拒绝。

**M1 完工核对：**

- [x] 固定输入、档案、初始状态、预算与无 Mod 原生 host；保存／网络隔离。
- [x] 无 solver 前置的自然纵向切片，合法请求与状态身份。
- [x] 规定节点／选择类别及异步故障门；临时／永久状态与跨幕连续性。
- [x] 全默认 Acts 的自然 RunVictory、冻结重放，以及真实 Death。
- [x] 最小数据契约、清理前历史、原始→汇总、必要来源、观察不扰动与缺数据拒绝。
- [x] 预登记批次及同构建回归，死亡／预算／unsupported／执行／数据错误分开。
- [x] 独立审查无必修项，公开说明准确限定覆盖、预测与策略能力。

满足后才进入 M2 公共接口设计。M1 内部接口可以整理命名、依赖与错误契约，但不承担外部兼容承诺。M2 将以真实原版流程稳定的数据／生命周期契约为基础，使 Eleanor 通过公开扩展能力接入并接受声明范围的实际验收。

## 12. 证据索引与本轮操作范围

### 项目文件

- 结果／计数：`src/SimulatorLab.Core/ValidationOutcome.cs`、`src/SimulatorLab.Core/ReplayVerification.cs`、`tools/validation/NativeCombatDecision.cs`。
- 有限流程：`src/SimulatorLab.Core/RunProgressProtocol.cs`、`tools/validation/NativeModProgression.cs`、`NativeSecondBattle.cs`。
- 观察／选择／隔离：`VanillaCombatRootAdapter.cs`、`AdapterCardSelection.cs`、`RewardCardSelectionBridge.cs`、`EleanorV040Probe.cs`（以上为 `tools/validation/` 下文件）。
- 现有本地复用 host：`.build/local-reference/CombatSolver-v040-0bb36bc7e397/tools/search/OfflineSearchHarness/` 中的 `OfflineCombat.cs`、`GameBootstrap.cs`、`MainLoopContext.cs` 与 MemorySaveStore 实现。属于本地证据，不复制第三方或游戏源码到本文。
- 历史基线：[STAGE_1_REVIEW_REPAIRS.md](../validation/STAGE_1_REVIEW_REPAIRS.md)、[TWO_BATTLE_VALIDATION.md](../validation/TWO_BATTLE_VALIDATION.md)、[REWARD_CARD_REVIEW_REPAIRS.md](../validation/REWARD_CARD_REVIEW_REPAIRS.md)、[候选分析及复审附录](../native-run/NATIVE_RUN_FIRST_FEASIBILITY_CANDIDATE.md)。
- 当前兼容冻结：`.build/runs/reward-repair-final-build-01/build-manifest.json`、`.build/runs/reward-repair-final-01/summary.json`、`evidence-review.json`。

### 固定游戏的静态证据

本轮以 ILSpy 只读查看固定游戏程序集中的 RunManager、CombatManager、CardSelectCmd、EventSynchronizer、ActionExecutor、UnlockState、SaveManager／ProgressSaveManager、EventRoom、RestSiteRoom、MerchantRoom／MerchantEntry、TreasureRoom、PotionModel、MapSelectionSynchronizer、MoveToMapCoordAction、ActChangeSynchronizer、TheArchitect、NEventRoom、NMapScreen／NMapPoint、CombatHistory 等相关入口。部分既有静态摘录在 `.build/review/`；其余为本轮只读标准输出，无游戏初始化、没有复制游戏源码到仓库。

游戏 hash 为 `0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`。对应方法名与行为只适用于本固定版本；后续实现须重新对目标方法签名／来源和实际调用顺序设门。

本轮只更新正式路线图、README 入口、新增本方案和旧路线图归档。**没有运行新的实验／测试，没有修改运行代码、既有原始证据或锁定输入，没有提交／推送。** 当前方案明确保留尚待实际证明的宿主、选择、最终事件与观测边界。

静态收尾核对：`src/`、`tests/`、`tools/` 下排除 bin/obj 后的 68 个既有文件与本轮开始时逐文件 SHA-256 相同；15 个 Eleanor 锁定输入无变化且与输入锁一致；HEAD 仍为 `d73039d688eb54c9513f5b9508a03947e689aa18`。归档保留原路线图完整正文（仅统一文本换行并增加历史说明），本轮四份文档的本地链接均存在，已跟踪文档的 `git diff --check` 通过。该检查是文档／文件一致性核对，不是功能回归。
