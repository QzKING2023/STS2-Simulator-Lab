# M1 步骤 4C 独立审查：需修复后复验

日期：2026-11-02。审查对象为 [4C 验证报告](NATIVE_RUN_N3C_VALIDATION.md) 与实际冻结产物。**结论：原生执行证据成立，但可信数据验收未通过；有三组必修。** 本轮只读运行实现和原始证据，在独立目录运行生产 reader、制作损坏副本及读取固定程序集元数据，没有修改实现或重新启动原生 run。

冻结：`6392BBCE3ACEF0AD5C1B53DCD5628066F84031B7860368F306AA469D084CF3DB`。Native DLL SHA256：`E14EFF2BABF66D91EF52163D671DBB07F6B3E298908505B4A75C1CB733AB8458`。独立证据位于 `.build/runs/n3c-independent-review01/`，入口 `review.py`、`facts.py`；汇总 `summary.json`、`facts.json`、`reader-progress.json`。每个独立副本内保存 `trace.jsonl`、seal、result、process 和实际 `reader.log`。

## 已独立确认的结果

- 独立重算 Build ID 一致；48 源文件及其快照、30 编译输入、3 游戏输入和 7 产物逐项 hash 一致。15 个锁定 Mod 输入、52 个兼容源码未变化。
- 核对四套矩阵的实际 process/result/header/manifest，确为同一构建的 **179 个原生进程、1514 个 suite 检查**。原始退出分布为 122×0、52×1、2×2、3×3；Unsupported、预算停止和故障控制没有被外层成功掩盖。
- Core98 的执行日志、驱动源码和实际 Core DLL 身份一致；636 项补充检查的数量和明细一致。115 份旧审查包保留其原构建身份，460 个原始文件不变；它们是旧证据的 reader 回归，不能计为本轮原生执行。
- 本轮实际调用最终 DLL 的生产 `--review` **255 次**：现有 85 个控制、42 份正常包、115 份历史包及 13 份新增独立副本。前 242 次均符合预期；新增 11 份坏证据中 10 份误接受，2 份等价副本接受。195 项来源／计数／文件核对和另外 229 项实际流程／回放／隔离核对无失败。这些核对不抵销 reader 的误接受。
- 正常同一 run 有 24/24 确认根动作：Neow 原生 Golden Pearl 开局、首战原生 Victory、原生 Gold/Potion/Card 奖励、合法下一战 Play、永久卡与战斗副本 DeckVersion 关联、实际消耗所得 BlockPotion。最终 HP70、金币266、永久牌11张、药水库存空。全部为 native-only / NotCompared。
- `NativeRewards.Collect` 实际调用 `SelectLocalReward`、`SkipLocalRewardsSet`、`PotionCmd.Discard`；`CardAfter` 等待原生 OnSelect 返回，Cauldron 的嵌套 Offer 持有同一根动作。没有手动 append 领取卡牌、金币或遗物。
- 宝箱调用原生 `DoNormalRewards`、`DoExtraRewardsIfNeeded`、选择同步器与 `RelicCmd.Obtain`，等待实际取得任务。UI 原有取得业务被明确提取，通知本身未被当作取得成功。新 seam 是 `LoadRoomTreasureAssets` 展示资源加载；不能据此宣称零售展示或视觉 RNG 等价。
- 药水路径经过原生入队、`UsePotionAction` 与 `OnUseWrapper`；旧实例、错误／死亡目标和时点控制没有 UsePotion 根派发。奖励和药水效果后的故障保留真实已发生的 +7 金币／消耗与伤害，确认前缀分别为 2/3、4/5，均为 EngineError、不完整数据。
- 16 场景冻结回放和 5 组 minimal/off 对照的 route、普通 choices、reward-choices、完整 final-state（包含已盘点 RNG）逐文件一致。MemorySaveStore、shouldSave=false、无 Steam 初始化、无游戏 UI、单人网络服务及无 Mod/CombatSolver 加载均与原始报告一致；这不是所有 OS 写入的监控证明。

Neow 范围需准确表述：新的 4C 自然样本是 Golden Pearl 的直接原生效果，没有在 Neow 内触发 CardReward 集合。旧 4B 的连续 Neow 奖励回归仍使用旧 room profile；它不能代替新 4C session 对该分支的直接验证。新 session 安装在 Opening 前，代码具备接入点，仍需在后续迁移自然房间控制器时补对应样本。

## R1 必修：操作树缺少语义上的必需子节点和先后约束

位置：`tools/native-run/NativeRewardContract.cs` 69–105、120–143、188–190；对应运行记录在 `NativeRewards.cs` 117–132 和 `NativeRewardProbe.cs` 26–30。

生产 reader 实际误接受：

| 独立副本 | 修改 | 期望／实际退出 |
| --- | --- | --- |
| `independent-missing-card-skip-menu` | 从真实 CardReward skip 包删除整个 card-menu 的 open/answer/start/return，保留外层完成与后续集合 skip | 1 / **0** |
| `independent-card-child-before-parent-start` | 将 reward-item 的 start 移到 card-menu 完成之后、item return 之前 | 1 / **0** |
| `independent-set-return-false-complete` | reward-set return 的 `complete` 改 false | 1 / **0** |
| `independent-root-return-false-completed` | RewardOffer 根 return 的 `completed` 改 false | 1 / **0** |

当前栈验证已出现操作的 LIFO 与 ID，但没有要求 CardReward 的原生选择分支必须出现一次 menu，也没有要求父操作 started 后才能打开子操作；部分完成事实完全未读取。因此删掉整个选择事务不会留下未闭合对象，`operations.Values.All(Returned)` 仍能真空通过。

最小修复：对本次声明的有限操作种类定义父子与结果契约；子操作只能在父 started 且尚未结束时开始。CardReward 领取尝试须有对应 menu 完成（包括原生 alternative 不完成奖励的合法返回），item 返回与 menu 的真实 completion 语义一致。为 set/root 读取并验证所声明完成字段的存在、类型和值。禁止用任意 op kind 绕开相同门槛。保留 SkipRewards 和 empty set 的合法无 menu 情形，不能简单要求所有 reward-item 都有 card-menu。

## R2 必修：子会话的静止状态没有绑定前一确认状态

位置：`NativeRewardContract.cs` 69–84、115–118、120–162；原生无业务间隙可从 `NativeRewards.cs` 87–99、113–115 直接核实。

生产 reader 实际误接受：

| 独立副本 | 修改 | 期望／实际退出 |
| --- | --- | --- |
| `independent-set-return-unknown-state` | 仅在 reward-set return 的 state 添加嵌套未知字段 | 1 / **0** |
| `independent-set-return-gold-drift` | set-completed 后紧接的 set return 将金币写成999，随后父 return 又恢复真实值 | 1 / **0** |
| `independent-reward-item-open-answer-start-drift` | 将当前 item 的 open/answer/start 三份快照一起改为金币999，外层 set 与真实返回仍保持原值 | 1 / **0** |

根 return→confirmed→boundary 已完整绑定，但 set-completed→set return、父 set 最新静止边界→下一个 item open 只存储了快照，没有比较。后三份记录相互一致也不能证明它们来自当前 set 的真实状态。

最小修复：保存不可变的 set 声明／最近子操作返回／set-completed 完整快照，在已确认无业务效果的相邻边界做完整结构比较，包括未来新增嵌套字段。为每个边界明确其允许变化区间，不再增加金币／RNG逐字段特判。**不要**要求所有父子操作全程状态相等：奖励 Populate、真实取得、Cauldron 嵌套 Offer、Hook、宝箱业务和 Travel 都会合法改变状态。新锚点应遵循固定实现的真实调用顺序。

## R3 必修：取得身份和若干核心结果内容没有被交叉核验

位置：`NativeRewardContract.cs` 85–89、140–154、174–175；记录产生点 `NativeTreasure.cs` 13、19–22，`NativeRewards.cs` 121–132。

生产 reader 实际误接受：

| 独立副本 | 修改 | 期望／实际退出 |
| --- | --- | --- |
| `independent-treasure-acquired-old-relic` | treasure-obtain 的 acquired 指向领取前就持有的遗物，实际新 Gorget 仍在后态 | 1 / **0** |
| `independent-treasure-normal-gold-lie` | 原生正常宝箱金币返回从44改为−999，实际快照保持+44 | 1 / **0** |
| `independent-card-menu-upgrade-lie` | 固定未升级 Defend 候选的 Upgrade 从0改99，实际永久与战斗副本仍为0 | 1 / **0** |

当前宝箱仅检查 acquired 在最终库存中，没有检查是否为该 canonical 生成的新 mutable 实例；正常宝箱 gold 结果未读取；卡 menu 的 Upgrade 只检查非负，固定卡夹具只断言新卡的模型和数量。

最小修复：记录并验证 canonical→实际 mutable 取得关系、取得前后库存和对象模型，拒绝借用旧库存身份；把宝箱实际返回金额与该业务调用的前后资源事实按原生契约对应。对卡候选保留来源及选择时属性，选中实例的真实取得结果／升级状态必须可核对，并加入固定未升级夹具的独立期望。取得 Hook 可能合法升级或替换时，应保留前后及 modifier 来源，不能要求所有卡一律同对象同升级，也不能用“曾出现在库存”代替正确实例来源。

这些是本轮声明的奖励／宝箱／卡选择数据字段，不要求现在扩展所有原版效果。正常运行的 native 结果并未因这些损坏副本而被证明错误；缺陷在于当前检查器无法可靠区分坏证据。

## 修复与复验边界

将上述10份误接受副本纳入同一个生产检查器回归，补对应缺失／重复／错配／提前完成及合法替代结果控制。两份等价副本（属性顺序、所有状态共同增加同值字段）仍应通过。现有85个控制、42份正常包和115份历史包本轮均通过，不应削弱。

修正实现后重新冻结，保留当前失败副本和原始执行包，以同最终产物运行受影响4C和适用1–4C矩阵。不能只用 reader 的外层 exit0 或所有 operations 都已返回关闭验收。先完成本轮数据必修，再由独立复验决定4D进入条件；无需用户重复授权。

## 奖励限制对完整 M1 的影响与具体闭环

4C 当前有限案例无需支持全原版组合，但 [M1 方案](MILESTONE_1_NATIVE_RUN_PLAN.md) 的正常完整胜利与预登记评估队列不能以 Unsupported 结案。下列是原版内部流程扩展，不是新增 Mod 专项。

本轮从同一锁定 `sts2.dll` 只读反编译／读取元数据，资料在独立目录 `static/`；游戏代码仅保留本机，不写入项目文档正文：

| 边界与已核实原生行为 | 对M1的影响 | 后续安排 |
| --- | --- | --- |
| Driftwood 给实际 CardReward 设置 CanReroll；原生 REROLL alternative 调用 Reroll 后继续同一个 OnSelect 循环。当前 pendingCards 在外层 OnSelect 返回前不释放，第二次 CardChoice 必然 Unsupported | 策略可合法不选 reroll，但不能把已提供的 reroll 宣称可执行；选择它的自然路径会中止 | **4D末的奖励会话闭环**：把同一 OnSelect 的多次菜单作为有界子决策序列，各自记录原生结果，再继续下一次菜单。fixture真实获取Driftwood、明确选REROLL后选非0卡；保留同步choice ID、RNG、原候选历史、重放和中途失败前缀；不默认换第一项 |
| 固定程序集元数据中 `ShouldAllowSelectingMoreCardRewards` 只有AbstractModel与Hook定义，没有原版模型override | 目前不能把泛化的“多选奖励”当作已知必然发生的原版阻断，也不能据此声称已验证所有循环 | 用原生reroll验证实际循环机制；无原版触发者的额外多选保持未验证，不为此引入Mod或伪造正式机制 |
| CardPileCmd.Add 实际返回 cardAdded / success / modifyingModels；FresnelLens 有 CloneCard 并加附魔的替换 Hook，另外也会在奖励候选生成时修改卡 | 当前 CardAfter 只接受所选原对象在deck，实际发生替换或拒绝取得时会中止；不能仅凭Hook存在断言每个FresnelLens奖励都触发替换 | **4D商店／卡取得桥共享收口，最迟步骤5前**：读取真正CardPileAddResult，把selected→actual acquired/declined与modifier关联；用原生Hook合法可达夹具确认实际触发条件，允许候选已提前修改后不再替换。记录附魔／升级必要状态，不手工append或Clone模拟领取 |
| Sozu.ShouldProcurePotion 对持有者返回false；原生PotionReward把NotAllowed作为奖励已处理而返回true，但药水不进入槽。当前Collect把success且不在库存当Unsupported | 后幕取得Sozu后遇药水奖励会明确停机；这是真实原版分支，不能把它误记成已入槽 | **4D末／步骤5集成前**：区分reward completed与procurement success，记录原生failureReason=NotAllowed及未入槽，保留TooFull可重试；原生取得Sozu夹具、后续奖励及重放验证。不得伪造一个空槽或强制获得药水 |
| 新4C session未直接覆盖Neow内部连续CardReward，旧4B有独立成功证据 | 将新会话整合进完整房间循环时，旧profile通过不能替代迁移后的证明 | 4D迁移时保留旧样本，并在新profile重跑原来能触发连续Neow奖励的已知seed／决策；不以Golden Pearl开局替代这项 |

这些工作要进入4D交付清单及步骤5前置检查；若在4D内尚未闭合，明确列为步骤5必须完成的依赖，不能凭文档披露自动通过完整M1。其余商店、嵌套事件、精英／Boss的既有 [4D交接](NATIVE_RUN_N3D_IMPLEMENTATION_HANDOFF.md) 顺序仍合理，本轮没有实施4D。4D、5、6继续属于已授权主线，完成4C修复复验后由根线程接续。

本轮无权限拒绝、无挂起或结果不确定的操作；没有改角色／安装／真实存档，没有启动游戏UI或调用真实Steam／游戏网络，没有commit/push。原始实现和1056个已核对来源／证据文件保持不变。
