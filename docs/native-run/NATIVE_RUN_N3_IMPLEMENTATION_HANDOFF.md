# M1 步骤 4：N1-T 终态前置与 N3 原版节点实施交接

日期：2026-11-02。依据[正式 M1 方案](../milestones/MILESTONE_1_NATIVE_RUN_PLAN.md)与[N2 最终独立验收](NATIVE_RUN_N2_SNAPSHOT_REVIEW.md)。用户已批准全部六步；本交接供 Sol xhigh / Standard 按子回合实施，无需再次索取步骤批准。本轮仅检查代码、复核证据和编写交接，以下新场景尚未执行。

入口基线：Build ID `B9C7B7EB283CA1FA895EB554901B3B79FEBAEEAF37B1FCCF724A85775B45BDED`，本地 HEAD `d73039d688eb54c9513f5b9508a03947e689aa18` 加已有未提交源码。固定 v0.111.0 / 41cef1ea、Ironclad、A0、Standard、现有独立实验档案、一 run 一进程。继续原生无 UI、无 Mod／CombatSolver 主路径，结果默认 native-only / NotCompared。

## 1. 完成定义及子回合顺序

步骤 4 交付可重复的原版房间驱动和同 run 多节点片段，并先证明尾部宿主路径可行。它不等于完成第一幕、自然跨幕或完整爬塔；这些属于步骤 5（N4/N5）。多 seed 可靠性批测与整体交付属于步骤 6（N6）。

| 子回合 | 顺序与交付 | 完成门 |
| --- | --- | --- |
| **4A：终态与 N1-T** | 先审保存／网络调用链；完成统一等待边界、真实死亡；以明确生命周期夹具触达 act-ready、EnterNextAct、Architect 实际选项到 RunVictory | 真实死亡和战斗 Victory 区分；尾部夹具成功或取得精确不可继续阻塞；未成功的尾部不得标 N1-T 通过 |
| **4B：房间控制器、休息、简单事件** | 抽出内部 room adapter；Rest heal/smith/cancel；原生普通事件的非首项与继续／离开；首条自然多节点片段 | 原生候选、合法性、选择完整消费、实际效果、离开条件；不把选项任务完成误当房间完成 |
| **4C：奖励、宝箱／遗物与药水** | 将固定 Gold/Card 奖励脚本改成真实集合驱动；宝箱领取／跳过／额外奖励；药水取得、容量、使用、丢弃及必要自动触发 | 取得命令与实际对象关联；至少一个遗物影响下战的 Hook；药水合法时点／目标与消费证据 |
| **4D：商店、嵌套事件、精英／Boss 与集成** | 依赖 4B/4C 的选择和取得桥，完成购买／移除／取消；事件战斗后恢复；代表性精英与 Boss；同构建多节点重放 | 各类代表场景与负控通过，至少一条自然路径经过两类非战斗房间且完成两场战斗，持久状态连续 |

每个子回合用独立冻结和输出目录交付，明确完成／未完成／阻塞。4A 若发现关键安全或宿主方向问题，暂停依赖它的尾部动作；可继续独立且安全的节点工作，但整个步骤 4 保留该阻塞。不得用跳过 Architect 或直接 WinRun 消除阻塞。

## 2. 当前实现确切需要迁移的部分

下列是现有代码事实，不能据 N2 通过推导已具备新功能：

| 当前位置 | 当前限制 | 下一实现责任 |
| --- | --- | --- |
| `Program.Ready/Battle/Main` | 仅等待 Play；HP≤0 可被当异常；首战计数要求全局等于 1；主流程硬编码 Opening→Battle→Rewards→下战 Play | 按 room/combat 实例记录边界；等待函数返回 Play、battle victory、loss、room decision 或明确 stop；计数使用每战 delta，累计 root 前缀不重置 |
| `NativeHost.ObserveRunEnded` | 前缀仅赋值 RunWon，尚无成功完成／异常关联；Main 未用它认证最终胜利 | 记录原生调用开始、成功返回及 WinRun 任务终结；不能仅凭 prefix flag、HP 或队列空闲认证 RunVictory |
| `Program.Travel` | Hook + Children 后直接 EnterMapCoord；没有独立 room completion／map generation 契约 | 先确认当前房间可离开、奖励与子任务已闭合；绑定地图 generation／源节点／目标，复用原生同步器或经核验的动作入口 |
| `Program.Rewards` | 固定领取所有 Gold、take 时只取一个 Card、最后 skip；断言单 offer、金币必须正增长 | 抽出集合会话；区分 terminal/nonterminal rewards；零奖励也可合法；未知必选奖励 Unsupported，不能以 skip 隐藏失败 |
| `NativeChoices` | 已覆盖普通卡选择、Bundle、CardReward；不是通用事件／商店／遗物／药水 selector | 复用 request/handle/stamp/消费检查；增加内部 root/choice kind 及各自原生 transport，保留来源审计。不要把非卡对象假装 CardModel |
| `NativeTraceContract/NativeEvidence` | natural-run 完成门固定要求一次首战生命周期及 next-combat-first-play | 保留 N2 场景门；为 terminal fixture、多节点、各房间建立有限场景契约，按战斗／房间实例检查重复生命周期，不把全部事件改成可选 |
| `NativeScenario` | 来源与 execution 已独立登记 | 新自然／夹具／故障场景逐项登记；建议生命周期夹具仍用 api-fixture，并新增明确 FixtureKind/Setup 描述。冻结重放不改变来源 |

建议在 `tools/native-run/` 添加内部 `NativeLifecycle`、`NativeRooms` 和按房间分文件的 adapter，逐步迁出 Program。每个 adapter 只负责当前原生对象的候选、调用、等待和完成证据；不复制卡牌效果，也不建立公开 SDK。纯协议需要时才进入 Core。保持旧 N2 入口可重跑。

根动作中出现嵌套奖励／卡选择时，沿用父 root 并创建子请求／任务，不能在未确认的 root 内再启动一个平级 root。需执行新根动作的状态转换则先在合法边界确认前一动作。队列 empty、原生 task 返回和 room complete 分别记录。

## 3. 4A：终态真实性与尾部风险先决门

### 原生事实与安全顺序

固定程序集的 `CombatManager.ProcessPendingLoss` 清 PendingLoss、停止 combat 并发出 CombatEnded；它不运行胜利路径的 AfterCombatEnd、History.Clear 或奖励。`EndCombatInternal` 才有胜利清理、CombatWon、SaveProgress/replay 等。`RunManager.WinRun` 先 OnEnded(true)，再 GuaranteeKillAllPlayers；因此最终 HP=0 不能覆盖先前真实 RunVictory。

`ActChangeSynchronizer.SetLocalPlayerReady` 入队 `VoteToMoveToNextActAction`；MoveToNextAct 经 `TaskHelper.RunSafely(EnterNextAct())` 启动后续异步任务。只等待 ActionExecutor 队列空不足以证明下一幕已进入。`EnterNextAct` 在末幕进入 TheArchitect；否则进入下一 Act。

`RunManager.OnEnded` 的存档、历史写入、统计上传主段由 ShouldSave 守卫，Daily 上传另由 DailyTime 守卫。`TheArchitect.OnRoomEnter` 还调用 StatsManager.RefreshGlobalStats，后者直接将 SteamStatsManager.RefreshGlobalStats 交给 RunSafely；现有 NativeHost 未给此入口建立单独 seam。实施前继续核查 Steam 方法的实际守卫及所有新增回调，确认无实际平台／网络调用，再执行尾部夹具。必要 IO seam 必须是已识别的纯外部服务边界，登记精确方法、来源、调用次数；不能整个 OnEnded 或事件选择函数替换成成功。Architect 事件专属 RNG 也需纳入观测。

### 验收矩阵

| ID | 场景与来源 | 必须取得的证据／拒绝条件 |
| --- | --- | --- |
| T01 | 既有自然首战 Victory | 原生 victory 回调、清理前 history、清理后状态、合法奖励；不是 run victory |
| T02 | 正常开局，声明 end-turn-only 合法策略，实际敌方行动致死 | 需要把 EndTurn 作为合法候选，即使仍有可打牌；不改 HP/伤害、不直接 LoseCombat。确认真实损失边界、当前 action 完成、无奖励／下一节点，保留死亡前缀与原生残留，一进程结束 |
| T03 | 非最后幕的 act-ready 生命周期夹具 | 明确设置的是局部前置，不宣称打完前幕。执行真实 ready/vote→EnterNextAct，等新 act/map generation／room 首决策，追踪异步任务及持久状态；拒绝旧幕重复投票 |
| T04 | 最后幕尾部生命周期夹具 | 明确前置构造；经真实 EnterNextAct 进入 Architect，实际选项推进到原生 WinRun 成功结束；不直接调用 WinRun，不合成胜利标志 |
| T05 | 终态解释控制 | 先胜利后原生清场杀玩家仍 RunVictory；无成功胜利证据的真实敌方致死为 Death；prefix 已触发但任务失败须 EngineError／未完成；停止派发后旧动作拒绝 |
| T06 | 一次合法边界预算停止／中途任务故障 | 保留已确认前缀与未确认当前动作；Unknown／已观察 terminal 与 stopReason 分开；不从队列空、异常文本或最终 HP猜测结果 |

T02 的 Death 是正常起点加弱策略的真实终局，不能算“完整爬塔流程覆盖”。若自然可达性暂时阻塞，可增加低 HP 等明确初态 API 夹具补终态机械验证，但必须保留 T02 未完成，不用夹具替代其自然证据。

T03/T04 可使用原生 fixture/debug 构造入口或显式局部前置，逐项写 Setup 与 snapshot，不能偷偷改 action 后的结果、奖励、RNG 或生命。自然 driver 禁用这些入口。旧 Mod 死亡残留无需重写；本回合只按当前原版真实行为记录，死亡进程不复用，不承诺完整零售 game-over UI 清理。

### Architect 特别处理

`NEventRoom.Create` 在 TestMode 返回 null；生产 `NEventRoom.SetupLayout` 调用 `Layout.SetEvent`，`NEventLayout.SetEvent` 在 InitializeVisuals 后调用原生 EventModel.OnRoomEnter，因此该语义回调在无 UI 下需要明确桥接。应核查同一原生 EventModel 的顺序，按 room 实例 exactly once 运行语义回调，跟踪其异步任务，再公开可决策选项。不能用“初始选项已出现”跳过 OnRoomEnter，也不能把空的基类 OnSetupComplete 当作该入口。

Architect 对话的首行、ClearCurrentOptions、PlayCurrentLine 与 UI CreatureNode 有依赖；攻击演出还消耗 event.Rng。实施者先验证实际分支：可以保留原生无 UI 分支及明确记录其视觉差异；若桥接会漏掉必要选项／RNG／终态语义，应给窄方案或 Unsupported。不得手工增加 PROCEED 或整方法跳过对话／获胜逻辑。胜利夹具必须记录原生选项→OnEnded成功→WinRun完成→清场实际顺序。

## 4. 4B–4D：原生节点、选择与数据依赖

下表为固定版本静态入口，不是已运行支持清单。每项先做独立声明夹具，再由自然路径证明至少必要的组合；同一类未遇内容保持未验证。

| 内容／建议顺序 | 已核验原生入口与风险 | 窄验收用例 |
| --- | --- | --- |
| Rest heal／smith | `RestSiteRoom.EnterInternal`→BeginRestSite 生成选项一次；`RestSiteSynchronizer.ChooseLocalOption`→OnSelect。该 synchronizer 本身未替 adapter 检查所有 IsEnabled 条件；Smith 走 FromDeckForUpgrade，可取消返回 false；成功后 Hook 决定是否仍有选项 | heal 的实际 HP 增量与上限；无额外修正 fixture 下独立 30% 基础值；指定重复卡非零实例升级；取消零升级且房间仍可决策；disabled/重复/旧请求拒绝；Exit 经 BeforeLocalRestSiteExited 和 AfterAllRestSitesCompleted |
| 普通事件／Ancient | `EventRoom.EnterInternal`、BeginEvent、AfterEventStarted；`ChooseLocalOption`→原生 option task。保留 N2 swallowed-fault ledger；增加新事件的 EventModel.Rng 盘点。OnRoomEnter 不等同 AfterEventStarted | 至少一个资源／永久牌组效果与非首项；locked/非法拒绝；多阶段选项和继续的真实完成条件；新子选择完整消费；任务失败不允许 travel |
| 通用奖励 | `RewardsCmd` 与原生 RewardsSet/RewardSynchronizer；集合生成和领取各一次；已有 selector 桥用于 CardReward | Gold、Card、Potion、Relic 代表集合；合法 skip／不可 skip；空集合；额外奖励嵌套；领奖后对象与节点来源，不以一律正金币作为门 |
| Treasure／Relic | `TreasureRoom.EnterInternal` 已调用 BeginRelicPicking，不能再生成。`DoNormalRewards`→OneOffSynchronizer；`DoExtraRewardsIfNeeded` 会生成额外集合。`PickRelicLocally` 入队，`RelicsAwarded` 只发布分配结果，本身未把 relic 加到玩家 | 抽取候选、真实 vote/result、原生获得命令及 Hook；恰好一次正常／额外奖励；skip 在单人下可延后到 OnRoomExited 才结束；重复领取拒绝；遗物保留到下一战，至少一条真实 Hook效果 |
| Potion | `PotionModel.EnqueueManualUse`→UsePotionAction→OnUseWrapper，wrapper 原生移除药水并执行 Hook/History；目标规则与卡牌不同；enqueue 不是完整时点验证 | 从奖励／商店原生取得；有空槽与满槽；显式合法丢弃后领取；指定敌人使用与槽清空；自用药水目标规范；旧槽被另一实例替换、错误时点／死目标拒绝；自动药水不能当主动动作 |
| Shop | `MerchantRoom.EnterInternal` 一次生成 MerchantInventory；`MerchantEntry.OnTryPurchaseWrapper(inventory,false)` 保留价格／扣费／库存／restock／AfterItemPurchased；移除有独立 overload 与 OneOffSynchronizer | 卡／遗物／药水各一个真正购买；不足金、售罄、旧报价拒绝；指定重复永久卡的非零实例移除、取消不扣费、移除次数／价格／Used正确；离店不丢未售记录 |
| 嵌套事件战斗 | EventRoom/CombatRoom 的 room stack；战后 `ProceedFromTerminalRewardsScreen` 在 ShouldResumeParentEventAfterCombat 时 ResumePreviousRoom，继而原生 ResumeEvents | 原事件→战斗→奖励→同一父事件继续→合法离开；不越过父事件直去地图；生命周期及 action/choice/context关联正确 |
| 精英／Boss | `RunManager.CreateRoom/PullNextEncounter` 与原生 CombatRoom；复用 battle adapter，不手写敌人规则 | 各至少一个固定实际 encounter 到真实胜利及其奖励；阶段变化／敌方动作有日志。fixture 牌组／资源全部声明；自然弱策略 Death如实记录，不把普通 Monster 结果外推 |

为本子回合实际遇到的未接通卡选择，只补最小通用原生 transport；不为每张牌写 simulator mirror。预测未支持仍可原生执行并标 NotCompared；宿主／选择不明则 Unsupported。卡／遗物／药水效果清单不要求全量枚举，报告必须列出实际模型与未覆盖范围。

### UI 中承载语义的三个具体检查点

1. **商店移除：** 已核验 OneOffSynchronizer.DoMerchantCardRemoval 执行 FromDeckForRemoval、原生扣金／移牌及 CardShopRemovalsUsed++。`MerchantCardRemovalEntry.OnTryPurchase` 成功后的 UI 链是 NMerchantInventory→NMerchantCardRemoval.OnCardRemovalUsed→原生 entry.SetUsed；无 UI 时这段不执行。另有 FillSlot 中 `Hook.ShouldAllowMerchantCardRemoval` 禁用语义。最小桥应复用该 Hook 与 SetUsed 的原生入口，保留原扣金／移除链及 AfterItemPurchased，验取消、第二次移除、价格变化和禁用条件。FromDeckForRemoval 尚不在 N2 明示选择来源集合中，须核验其 wrapper／隐式返回及审计桥，不能直接移除指定对象替代选牌。
2. **宝箱领取：** 已核验 `NTreasureRoom.OpenChest` 依次运行 DoNormalRewards、DoExtraRewardsIfNeeded，再开启遗物选择。`RelicsAwarded` 的 UI 订阅者 `NTreasureRoomRelicCollection.AnimateRelicAwards` 将获配 canonical relic 转为 mutable，非 Skipped 时启动原生 `RelicCmd.Obtain`，并为其他玩家把原候选移回 grab bag fallback；最后结束选择UI任务。无 UI bridge应保留这些真实规则入口和顺序，取得任务也要被跟踪到完成／失败。不能手工 Add relic，不能把分配通知或UI选择任务结束当作取得效果已完成，也不能把普通额外奖励移到领取遗物之后。
3. **房间与地图继续：** UI 的按钮启用／旅行条件可能承载语义。普通事件完成、Rest剩余选项、商店可离开、宝箱skip、Boss ready分别核对。`Hook.ShouldProceedToNextMapPoint` 与地图 Children 只是部分条件；未知节点交原生 RollRoomTypeFor，不预先替换成 Monster。

普通事件还需区分真正的 event option 与完成后 UI 生成的离开按钮。`NEventRoom` 对 IsProceed 选项直接调用 `option.Chosen`，普通选项走 EventSynchronizer；完成后的通用 Proceed 只开启地图，不是另一个奖励效果。adapter不得把所有按钮无条件送进同一种同步器，也不能以同名 PROCEED 跳过 Architect 的真实 WinRun 选项。

以上桥接属于已授权的普通 Lab 无 UI 适配；只移交生产已存在的规则入口，不改游戏结果。如果缺少安全非视觉入口，记录具体方法及阻塞，不扩宿主权限或跳过规则。

## 5. 数据与协议增量——仅覆盖新增路径所必需

保留 N2 的 20 必需 snapshot /16 metadata 分类和所有负控。新增 event kind 要明确分类及场景必需性，不能用可选遍历接受缺失数据。N2 历史文件继续用原契约；新生命周期模式注册新的场景要求，并由 header 明示版本／模式。

最小新增内容：

- room-enter/exit 的 run、act、map generation、node、room实例、room stack父关系；combat实际 ID与battle terminal；候选请求与同一room的完成令牌。
- Rest 的 optionId/enabled、原生选牌身份、取消／执行结果；event model/阶段/选项状态、专属 RNG；shop entry实例/报价/stock/交易前后金币和所获对象；treasure开箱正常奖励→额外奖励→遗物分配→取得及其可能新增子奖励；potion slot/实例/目标/原生消耗。
- act-ready/transition任务开始、完成／失败与新地图；battle terminal、run终态及之后的cleanup顺序。死亡可保留实际资源／订阅残留，不能构造假清空快照。
- 全永久 deck内容、升级、身份及后续 DeckVersion；HP/maxHP/gold/relic/potion跨节点链。临时 combat资源按实际模型清理，不能一概断言都归零。
- 新 RNG 流和事件局部 RNG 完整状态；若展示演出独享流与规则流不同，分别记录与限制。不得沿用“Neow无持久事件RNG”覆盖所有事件。
- TestMode省略的地图 stats/商店未售历史，以 `source=lab-observer-of-native-operation` 单列真实事实；不声称原生历史原样存在，不为日志恢复持久存储。

每个节点至少有一个独立契约（如普通无折扣交易价格、取消不付费、指定永久实例变化），期望来自审阅的规则与固定fixture常量，不能从实际 after值倒推。同一个后续原生读取不能充当独立 oracle。

reader 必须能拒绝：缺房间效果／终态事件、相同实例重复取得、在子请求未完成时离开、跨room旧选项、只有分配无取得、confirmed缺state、缺必要RNG／局部history，以及吞掉任务异常。只给本回合新增场景增加有针对性控制，不做无界schema全值域审计。

玩家可见 policy DTO只含当前合法可见候选／价格／已知资源。hidden state stamp、draw顺序、未来地图随机结果和完整RNG只进入oracle证据；所有trace继续禁止直接交公平策略。新内容不得借观察getter重新生成候选或消耗RNG。

## 6. 自然集成、fixture与预算

**自然路径门：** 正常 Neow 起点，同一 run 的真实地图，至少完成两场战斗、两类非战斗节点，取得／升级／移除中的至少一种永久变化在后续战斗中可定位。无需强迫奖励牌首手出现；不人为摆牌，不把几段 fixture拼成自然路径。完整幕转换胜利留步骤5。

开发种子在第一次新执行前登记：保留 `LAB_V0111_NATIVE_DEV_01`，可增加明确的 DEV_02/DEV_03 用于可达性调查；登记每次路线策略与结果，不以成功种子替换失败历史。它们不是 N6 留出评估集。初始建议自然片段节点上限12，触及第一幕Boss边界可提前停止并报告；若所需节点组合未自然达到则集成门未完成，fixture只证明各节点机制。

保持现有45秒单动作／等待、1000战斗决策与512MiB日志上限作为初始有界默认；新增房间选项上限（建议32）和整个run动作／墙钟上限需写配置并同header冻结。当前外层5分钟watchdog可能短于多节点实验，须显式调整和记录，不能让运行时预算与外层超时互相矛盾。候选生成、子选择、异步任务和日志均受界限约束；预算耗尽是BudgetExceeded，不是死亡或完整完成。

每个正场景：独立进程真实执行一次、用完整动作＋选择在新进程冻结重放一次。各新机制至少一条详细／最小观察对照，比较完整状态与本场景全部已盘点RNG；不要求所有重复都当独立样本。动作／策略改变需新profile和证据，不在已冻结路径上偷偷修行为。

负控在可预判非法时要求零新增派发／无资源变化；原生效果发生后才出现的子选择非法，保留实际派发和已发生效果，当前root不确认。异步失败、取消、写盘错误也要落下有效前缀和具体stop。外层watchdog仅结束该子进程，状态不确定时禁止继续或自动重放该动作。

## 7. 同构建收口与下轮执行清单

首个实施回合只收口 **4A**：

1. 读取本交接和当前指引，检查dirty源码／输入；静态核对新死亡／Architect／act-change调用链的保存与平台入口。
2. 实现terminal/boundary等待、每战计数、异步完成关联；登记夹具来源，不先铺全部房间。
3. 跑T01–T06，保留可恢复失败并修复Lab范围内问题；若尾部只能取得具体阻塞，给出最小安全方案并继续独立已授权部分。
4. 冻结产物，跑既有N2全矩阵及Core，补终态必需控制与观察对照；报告新哈希、实际原始exit、数据完整性、自然／fixture范围，交独立审查后进入4B。

4B、4C、4D按同样模式分别收口，不在一个回合同时修改全部入口。可写后续更小实施清单，避免无限扩大当前子回合。最终4D在一个冻结NativeRun产物上执行各子包必需代表矩阵和旧N2回归；旧证据保留，只做同构建可解释汇总。

回归优先级：Core协议／结果与失败传播；N2自然skip24/take25和全部完整性／选择负控；各新增节点与terminal矩阵；原版战斗/RNG旧基线；仅当共享Core/host源码受影响时运行适用冻结Eleanor回归，另列legacy产物身份。15个锁定Mod输入和52个兼容源码持续hash核对，不扩Mod专项。

验收报告每类写：入口和模型、合法候选／完成条件、实际执行及重放、独立断言、负控、RNG／数据、保存拦截、未覆盖／阻塞。外层exit0不能覆盖内部DataError、EngineError、InvalidAction或未知结果。Death是合法执行终局，但不能补足尚未走过的节点／幕／胜利路径覆盖。

步骤4通过才进入步骤5的自然第一幕、跨幕和完整RunVictory；步骤6再做预登记批测、性能／数据查询与整体交付。公共Mod API和新增Eleanor内容继续后置。没有自动commit/push权限，不触角色、安装目录、用户存档／Steam Cloud或真实UI。

## 8. 本轮证据定位

- 当前实现：`tools/native-run/Program.cs`、`NativeHost.cs`、`NativeChoices.cs`、`NativeObservation.cs`、`NativeTraceContract.cs`、`NativeScenario.cs`、`run-native-slice.ps1`；结果／票据：`src/SimulatorLab.Core/NativeRunExecution.cs`。
- 终态与room stack原生只读证据：`.build/native-reference/RunManager.cs`、`EventRoom.cs`、`EventSynchronizer.cs`；`.build/review/CombatManager.stageone.cs`。
- 本轮固定程序集静态查看资料：`.build/review/n3-handoff/` 下 RestSite、Merchant、Treasure、ActChange、Architect、Potion、地图动作及相关UI类型。它们只用于本地审查，禁止发布游戏反编译代码；公开文档只记录入口／判断和相对证据位置。
- 游戏程序集SHA256：`0861BFA1DF347538D932F22D580E75420F08082792EB914E53B4882764ACDBE9`。方法行为以此版本为准；实现时按目标方法IL／token／MVID建立精确seam审计。

当前节点接口与风险来自静态代码核验；4A–4D的可执行通过情况均待后续实现，不把本规划写成新功能完成报告。
