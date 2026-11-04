# M1 步骤3交接：N2 选择与数据可信门

日期：2026-11-02。依据[正式 M1 方案](MILESTONE_1_NATIVE_RUN_PLAN.md)及[N0／N1 独立验收](NATIVE_RUN_N0_N1_REVIEW.md)。**六步总体已获授权，下一轮 Sol 可直接实施本工作包，无需再次申请步骤3批准。** 本文是内部实施矩阵，不是公共 Mod SDK。

当前基线为 `n0-n1-final-03`／Build ID `913DEAA5D674366A36B2256E9658F46380B4F9C66C43EE9BD8707FDE9FEEAFD6`。固定游戏 v0.111.0／41cef1ea、Ironclad／A0／独立实验档案；保留已完成兼容实现、15个Mod输入及历史证据。

## 1. 本轮完成定义与边界

让原生执行器真正消费策略／重放给出的选择，产出能回答“做了什么、来源是什么、资源如何变、为何停止、能否重现”的必要数据。成功门是选择完整消费、原生效果、可追溯记录和不扰动；不要求策略强、所有牌有预测或完整爬塔。

本轮只实现：既有 Neow／地图／战斗／奖励请求的内部统一协议；普通卡片选择与 Bundle；必要永久牌组选择 API；类型化效果／生命周期记录；实际生产检查器及不扰动／丢数据负控。房间全集、商店经济、休息室完整生命周期、嵌套事件战斗、精英／Boss／跨幕留 N3–N5。

使用 API fixture 验证永久牌组选择与原生 Upgrade 命令即可，不必先铺完整休息室。若实现者需要一个真实 Smith 房间实例来完成最小证据，只接通这个指定入口并标 `lifecycle-fixture`，不能顺带宣称休息节点类别全支持。N1-T 尾部风险探针仍需在广泛 N3 扩房间前执行；本轮不将尾部 fixture 当选择门的前置条件。

## 2. 已核验事实及直接实现取舍

静态证据位于 `.build/runs/native-n0-n1-independent-review01/*reference.cs`，仅为本地审查资料，不进入公开源码。

| 固定版本事实 | 实施决定 |
| --- | --- |
| `CardSelectCmd.UseSelector` 可接入 `ICardSelector`；普通方法仍执行原生过滤、自动集合处理及 LogChoice | 首选此生产支持的自动选择入口；复用原生方法，不能移植整个游戏效果实现 |
| `FromSimpleGrid`／`FromCombatPile`／`FromHand` 的空集合、无需确认的单个或少量候选可直接返回，绕过 selector | 必须观察原生方法的最终返回，标 `implicit`／`no-candidate`；不能每次硬要一条策略答复，也不能遗漏这些请求 |
| 部分 wrapper 调用另一 `From*` 方法，如 FromHandForDiscard→FromHand、FromCombatPile重载 | 为同一逻辑请求合并包装来源，不产生重复待答请求／重复消费；记录调用链可单独存在 |
| `FromChooseACardScreen` 给 selector 的 min 是0，即使 canSkip=false；偏好参数不总能从 selector 回调恢复 | broker scope 必须携带原始方法参数、允许取消／跳过条件，独立校验实际响应；不能只相信 min/max 回调 |
| 全局 Selector 会跳过部分原生 ReserveChoiceId／SignalPlayerChoiceBegun/Ended；LocalSelector 行为又与其不同 | 使用原生自动选择语义，显式记录 `transport=native-selector` 和 Lab requestId；nativeChoiceId 不存在时为 null。不能补造同步消息、声称完整零售同步链已执行 |
| Bundle 不读 selector，TestMode 固定 bundles[0]；但原方法前有结束／空集合守卫和 ReserveChoiceId，后有 LogChoice | 给固定 async 状态机安装窄介入，仅替换默认索引／取组调用，保留其余控制流；验证原方法／MoveNext来源、IL指纹、唯一匹配及现有补丁。不得替换整个方法、全局关闭 TestMode，或在失败时回落首项 |
| CardReward.OnSelect 已有无 UI 的 `GetSelectedCardReward` 路径，仍 Reserve／SyncLocalChoice 并执行原生获卡和 alternatives | 复用原生 RewardsSetSynchronizer→CardReward.OnSelect；选择真实候选引用，不直接 append。alternatives 若不能支持先明确拒绝，不能返回不属于原始列表的对象 |
| CardSelectCmd.Reset 在 TestMode 下不会清理 selector 栈 | 自己持有并 finally 释放作用域；检测泄漏，失败进程不复用。不能依赖 Reset 清理 |
| CombatHistory.Add 后立即触发 Changed；Clear 也触发 Changed；历史对象包含可变引用，终局会清空 | 捕获时复制为类型化不可变 DTO；用明确 Add／Clear 生命周期标记，不能只用持续增长的 reference HashSet 掩盖清理 |
| EventOption.Chosen 经 RunSafely；AwaitPendingOptionTasks 会吞异常并清表 | 保留 TaskHelper fault ledger，并关联原始选项任务、开始／结束／fault；外层 await 返回不等于成功 |

Bundle 的具体窄实现可选：对已核验 TestMode 分支的 `bundles[0]` 索引或 get_Item 调用作单点 transpile，转交当前请求 broker；原 ReserveChoiceId 与末尾日志仍由游戏执行。要求在本机固定程序集上验证唯一匹配，保留未知补丁拒绝。若无法做到，不允许用整方法替代掩盖；先报告准确停点，可继续普通 selector／数据独立工作。

## 3. 工作包及串行依赖

### N2-A：先冻结最小内部契约

建议新增独立文件承担 `NativeRunPolicy`、`NativeChoiceBroker`、`NativeIdentityRegistry`、`NativeRunObserver`、`NativeEvidenceWriter/Reviewer`，从 `Program.cs` 迁出职责；名称可以调整，不设计插件发现或外部兼容承诺。纯 DTO／验证放 Core，游戏类型留 native-run。

请求包含：runId、act/mapGeneration/room/combat、boundarySequence、rootActionId、requestId、parentRequestId、来源方法／模型、原生候选顺序及实例、基数、允许取消／跳过、原生隐式分支原因。响应是明确的 kind／候选引用／顺序；拒绝未知字段、重复实例、越界数量、旧来源、旧边界和重复消费。root计数、choice计数、原生task计数分别累计，不能互相当作动作数。

身份注册是独立的执行基础设施，不依赖详细日志是否开启。对永久牌／战斗副本／生成对象记录 identity、model、upgrade、DeckVersion关系与生成源；同名对象不能合并。重放可用确定的出生序号与类型化来源映射，不能仅靠手牌index或首次详细观察位置。策略收到不带隐藏顺序含义的公开句柄；私有状态stamp与RNG只给执行守卫／审计。

保留现有严格 native-only 结果和旧差分结果。N2 数据声明至少有 `scope`、`requiredEventKinds`、`requiredFields`、`complete`、`missingReasons`；不能继续用“没有抛异常”直接推导完整数据。

### N2-B：普通选择、Bundle、奖励及根动作桥

先给既有 EnterAct／Neow EventOption／Travel／PlayCard／EndTurn／TakeGold／SkipRewards 建立同一个 request→validate→dispatch→confirm 通路。简单策略和冻结重放消费相同请求；执行器不再先替重放选第一项再仅核对。重放的未知额外字段继续在派发前拒绝。

逐个允许已审计的 CardSelectCmd 方法，仍拒绝未实现方法。先普通手牌／pile／grid，再升级／永久牌组通用选择，最后 Bundle 和 reward card selection。candidate枚举只做一次，不反复调用生成器或可能耗RNG的 getter；同一原生集合供策略、校验和执行使用。

子选择需要随真实执行动态产生，不能以 solver 预测完整未来选择为前置。请求跨 await 的归属以显式上下文／任务绑定保留，主线程完成；异常和 finally 不泄漏 selector。存在未完成／未消费请求时根动作不能确认，不能在未完成动作之后继续下一步。

**拒绝计数必须如实：** 根动作的过期／未知字段应0新增根派发。运行中才出现的子选择错误可能发生在付能量、伤害或格挡之后，例如 Armaments 先获得格挡才选升级；此时当前根动作已派发。应记录1个未确认的in-flight根动作、0个非法choice提交、保留先前确认前缀并终止进程。不能回滚资源伪造“零派发”，不能把此前已发生的效果删除，也不能重试该动作。

### N2-C：类型化记录与关联

只覆盖本轮首批问题需要的事实；不反射遍历所有对象，也不承诺所有 Hook 完整因果图。

| 记录 | 最低必需信息与观察点 |
| --- | --- |
| Header／配置 | build/input/profile/policy/schema hash、seed、scenarioKind、预算、信息模式、host seams、观察模式、加载程序集清单 |
| 请求／动作／任务 | requested/resolved/implicit/rejected、dispatch/start/completed/fault、parent关系；原生保留choiceId时记录，否则明确null／原因；每个待决请求一个终结 |
| 生命周期 | room enter/exit、battle start、victory前、History.Clear前后、原生cleanup后、reward生成/取得/跳过、下一Play；后续run终态预留明确类型，不在本轮假填 |
| Damage | 原生 DamageReceivedEntry：dealer/receiver/cardSource、Result的blocked/unblocked/overkill/props/kill标记；分别记录HP前后及revive可能性，不能把UnblockedDamage无条件当实际HP下降 |
| Block／能量／卡牌 | 实际BlockGained金额、props／CardPlay关联、EnergySpent；PlayStarted/Finished、Draw/Discard/Exhaust/Generated的实例与来源；永久升级／获卡在原生命令完成后观察 |
| 敌方行动／Power | MonsterPerformedMove的monster/move/targets/round/side；PowerReceived及边界power快照。未能归因到具体power/relic触发器时写null+reason，不把父根动作当精确效果来源 |
| 边界快照 | HP/maxHP/block/energy/gold、全部piles及卡牌升级/必要变化、永久deck、relic/potion实例、敌人状态、powers、回合phase；原生新对象关系可解析 |
| RNG | 已有run/player；继续盘点本轮用到的event模型／event_synchronizer等流，只读取可确认无副作用的状态／计数。记录完整性清单，禁止为观测Next调用 |

静态确认 Armaments 普通版先原生获得5点Move格挡，然后 FromHandForUpgrade；Headbutt先9点基础攻击（升级+3），再从弃牌选择并原生放抽牌堆顶。这两张原版卡可作为真实选择＋顺序／对象身份夹具，不需要任何效果镜像。固定无干扰状态的独立契约由这些静态事实建立；实际受Power等影响时另列，不能从本次运行DynamicVars计算期望再声称独立。

数据标签逐字段分为 player-visible、oracle、presentation、diagnostic。公开策略只接收白名单DTO，不传game对象、draw顺序、seed派生未来状态或private hash。查询抽牌堆时保持原生展示排序；同名同稀有度的句柄顺序也不能偷偷暴露隐藏draw位置。日志可保存oracle顺序，但不能从日志反向喂策略。`native-source`、`lab-observation`、`derived-summary`标记来源，TestMode省略的历史通过后两者补足，不冒称原生统计恢复。

### N2-D：持久记录、错误与完整性门

采用主线程单调seq追加JSONL；每行只写必要事件与引用，关键边界另写快照。Header、chunk范围/hash、最终条数/hash、结果footer相互绑定；未知schema、缺字段、seq缺口／重复／逆序、悬空ID、缺请求终结、缺确认／清理边界、截断行、损坏chunk、内层fault均不能验收通过。最终summary必须能由原始记录重算。

观察器只捕获事实，不在回调中执行效果。序列化／写入失败不能通过事件回调悄悄改变游戏返回值或静默继续：设置独立fatal ledger，在下一安全检查点停止；保留最后flush的有效前缀，当前动作明确未确认。磁盘写入失败时允许仅由wrapper记录Interrupted/DataError及已有前缀，不能要求已经失效的sink保证完整footer。

日志初始配额沿方案512MiB，可配置小配额做标注控制；耗尽必须显式停止并标数据不完整。预算、InvalidAction、Unsupported、EngineError、DataError分开；innerfault不能被await队列空或外层exit0覆盖。网络服务类型／无NGame实例纳入主动守卫，继续MemorySaveStore、SaveProgress／replay／telemetry审计；不新增OS级权限或取消安全检查。

### N2-E：不扰动和实际消费者负控

对相同最终构建、同一冻结动作／选择，在新进程运行详细观察关／最小／详细三模式。关闭的是可选详细观察，身份注册、必要执行守卫和最少边界指纹始终存在，必须在报告中说明。独立比较实际合法候选、选择、资源、所有本轮声明状态、RNG、实例关系及终点，不能仅比较终态HP。

再对普通选择API夹具比较provider与原生自动选择基线的结果对象／状态，Bundle的“无patch原生TestMode首组”仅可作已知语义基准；生产provider必须能选非0组。选择不同组的效果当然可以不同，这不能混入observer不扰动对照。

记录初始化／原生执行／等待／复制／序列化的分层时间、输出字节和峰值内存；N2不设未经测量的吞吐目标。固定动作预算用于语义对照，墙钟开销单列，避免观察开销触发不同路径后错误宣称语义变化或等价。

## 4. 必需验收矩阵

以下为实施队列，不是已通过数量。每个正例在新进程重复并保存完整选择；只有指定不扰动组需要三模式。所有控制作用于生产broker／writer／checker，不能另造一个总会通过的替身。

| ID | 场景与验收 |
| --- | --- |
| C01 | 普通single：至少2候选，明确选非0；同名不同实例，实际选中正确引用；无隐式first fallback |
| C02 | multi／顺序：显式选择两个不同实例；重复选择、少于min、多于max、非法候选拒绝；顺序是否有语义按原方法保留 |
| C03 | empty／implicit：合法空pile、单候选自动返回、RequireManualConfirmation=true仍显式请求；原生ReportSoftlock类空输入单列为错误控制，不能当合法空选择 |
| C04 | sequential/nested：同根连续两个请求、子请求有父ID；真实顺序消费；缺失、多余、错request、过期／跨room／重复response分别拒绝。重复wrapper不能双消费 |
| C05 | Bundle：2组以上，明确选index1；原生ReserveChoiceId只发生一次、日志／返回对象正确；结束和空集合原守卫仍在；非法组、无答复、额外答复拒绝。其native自动分支缺Sync不能说成网络同步通过 |
| C06 | 原生Armaments：对非0重复实例执行升级，格挡在选择前；另一同名实例不变，永久deck完整内容／升级／引用按实际战斗副本规则检查；非法子选择保留已有格挡与未确认根前缀 |
| C07 | 原生Headbutt：实际攻击→指定弃牌对象到draw top；空弃牌／单候选单列；不改RNG／强插目标来冒充自然流程，夹具设置写明 |
| C08 | 非战斗永久牌组：FromDeckForUpgrade／FromDeckGeneric候选过滤和指定非0实例；由原生Upgrade／相应命令执行效果，永久对象关系正确；未接休息室时仅称API夹具 |
| C09 | 自然纵向路径原skip配置重跑＋冻结重放；另立take配置，在同一DEV01自然链原生领取明确非0奖励卡，下一战永久deck/DeckVersion可追溯。无需打出该牌或推进第二战终局；不能用修改seed/index回避失败 |
| C10 | 实际事件异常控制：经EventOption.Chosen／同步器路径的有界注入，TaskHelper吞异常后仍报告失败，已确认prefix不丢；错误不计为自然内容缺陷 |
| D01 | 原始事件可重算动作、选择、伤害/格挡/能量、奖励资源、房间顺序汇总；实例引用均解析；before-clear记录存在且早于原生清空 |
| D02 | 原始记录各负控：删中间行、重复seq、截断尾行、改hash、悬空实例、去掉choice终结／cleanup、修改exit，实际checker全部拒绝；原文件不修改 |
| D03 | observer关／最小／详细在C09 skip和至少一个含选择夹具逐边界等价；详细日志改变实例ID／合法候选顺序时必须修复，不能删字段让它通过 |
| D04 | sink异常／小日志配额控制：有效前缀保留、当前未确认准确、DataError或明确BudgetExceeded＋incomplete；wrapper不会把缺结果当成功 |
| D05 | public DTO不包含draw顺序/未来RNG/private stamp；oracle文件字段明确；候选采集／public投影前后无原生状态和RNG变化 |
| G01 | 同冻结构建Core、N0/N1适用原负控、C/D矩阵；未知结果／raw exit矛盾／空证据拒绝；0/1/2/3含义保持。真实API不支持与预算未完成不能算普通正例通过 |

牌组API夹具与真实卡夹具通过不等于自然获得了这些卡。C09是自然流程；C01–C08按实际入口标api-fixture／lifecycle-fixture；C10／D02／D04标fault-control。重复执行是复现证据，不是独立统计样本。本轮仍不声称全角色／全部选择来源／全部监听器覆盖。

## 5. 集成顺序、回归和停点

按 A→普通B→C基础→Bundle及非战斗B→D→E→最终矩阵串行集成。先冻结字段和身份，再实现writer／检查器；共用Program、NativeHost、build脚本由一个集成者修改，共享缓存串行构建。

保留旧23/3/9/7文件基线为历史，新的源／编译生成文件／输入／输出重新锁定。源码修正后更新最终构建并重跑受影响及整轮必需矩阵，不能混用旧22check成功替代新产物。同一次最终交付需包含实际脚本、场景清单、输入配置、退出状态、原始到汇总证据与未覆盖表；不要只给外层绿色总表。

原版旧战斗／RNG／有限奖励回归优先；冻结Eleanor只用于共享变化防回归，仍保持独立legacy构建标识，不扩Mod适配。若Core共享变动，重建受影响兼容profile并明确新身份，不能只跑不引用新Core的旧二进制便称验证了共享变动。原有Mod pin与报告均保留。

允许在既有Lab范围修复普通可恢复问题，不再问用户步骤授权。需要新宿主权限、真实UI、改游戏／角色、关闭原审计，或只能改写游戏规则才能处理关键入口时，停止该依赖并报告准确目标／错误／已执行状态；其余工作继续。完整room适配与整幕策略不在本轮拓展。

完成后交Astra只读验收N2，再进入N1-T／N3。M1全六步继续由根线程协调，不以本次交接或N2完成视为整体任务结束。
