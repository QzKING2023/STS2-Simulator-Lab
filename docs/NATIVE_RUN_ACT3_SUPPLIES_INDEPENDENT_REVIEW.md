# 第三幕补给与死亡最大生命值观察：独立审查

2026-11-03。结论：**当前范围未通过独立验收，有两项生产检查器必修。** 原生自然流程、五次购买、两事件及 Death545 的执行证据成立；新死亡最大生命值观察没有发现阻断缺口。发现的两项问题影响商店证据认证，不是已经证明原生购买或死亡执行错误。

审查入口：`.build/runs/act3-continuation-independent-review01/closeout.json`。原实现报告为 [NATIVE_RUN_ACT3_SUPPLIES_VALIDATION.md](NATIVE_RUN_ACT3_SUPPLIES_VALIDATION.md)。本轮只写独立审查副本、脚本及本报告，未修改生产实现或原证据，未启动原生游戏矩阵、UI、真实存档或 Steam 服务，未提交推送。

## 冻结与核验范围

| 角色 | 冻结 |
| --- | --- |
| 实际原生执行/采集 | `2EB24CD78644588B03618F313FF3842166AB850858CBEB09BE9C937A6E7F9F53` |
| 本轮实际调用的生产检查器 | `F28EA015BBC3D32D3919E732209C11F7ABE7FD5CE8CE026BC50E4CEFFE127777` |

独立重新计算两份 manifest ID，验证各自 64 份编译输入、7 个输出、3 个游戏输入，以及当前 100 份源码和源快照。两个冻结之间只有 `NativeMajorContract.cs`、`NativeThirdShopContract.cs` 检查器变化；采集链保持相同。15 个固定输入、52 份兼容源码不变。507 项来源、执行、状态、RNG、实例及重放断言均通过，见 `facts.json`；哈希证明一致性，原生真实性同时依据受控进程、调用链、原始记录和重放。

实际重新调用生产检查器 **223 次**：

- 202 份既有原始/历史/控制输入重新读取：38 接受、164 拒绝，全部符合各自预期。包括本轮 9 个原生进程结果、旧故障和预算前缀、原有 54 个控制及旧商店案例，见 `reread-summary.json`。
- 独立新增 21 个有限契约控制：2 个合法/等价副本通过；19 个错误副本中 13 个正确拒绝，**6 个错误接受**，分属下述 R1/R2。见 `controls/summary.json`。每个副本先合法重封，实际调用 `SimulatorLab.NativeRun.dll --review`；没有用损坏 seal 代替语义负控。

## R1：退出开始没有绑定派发前的完整状态

位置：`tools/native-run/NativeThirdShopContract.cs:59` 至退出分支。`third-shop-exit-start` 直接克隆自身快照作为 `exitState`，返回只检查该快照的 `merchant.closed=false→true`。两条记录能互相证明，却没有绑定到当前 Travel 派发及原生出栈变化。

独立反例均保留原动作、其他记录和最终结果，只修改这两份退出快照；生产检查器返回 **0**：

| 副本 | 改动 |
| --- | --- |
| `controls/02` | 两份快照金币各增加 100，随后恢复原记录 |
| `controls/03` | 两份快照的玩家 Shops RNG counter 改成 999，随后恢复 |
| `controls/05` | 两份快照节点改回旧节点 `(2,5)`，与 Travel 实际目标 `(2,6)` 矛盾 |

这属于当前完整状态/动作归属契约内的局部错误；不需要协同伪造整份日志。仅反转属性顺序的等价副本通过，交换退出开始/返回则拒绝。

最小修复：从不可变当前 Travel 派发快照和真实目标构造退出开始的预期完整状态，绑定当前房间、库存及已完成商店会话；返回再应用原生退出完成的允许变化。复用已有出栈契约，不能只逐字段补金币/RNG。

**不得直接要求退出开始等于 dispatch。** 原生此时已经更新 node、floor 并弹出当前房间，`room/rooms` 为 null；本次会员卡购买后，旧库存的 Cost 投影也在房间弹出时恢复非商店价格。锁定程序集的 `MerchantEntry.Cost` getter 仅在当前房间为 MerchantRoom 时调用价格 Hook，出栈后返回原始价格。实际完整差异保存于 `exit-native-transition.json`。修复要依据真实原生 getter/Hook 及已记录库存来源保留这种合法变化，不允许任意放开全部 `merchant.entries`。无需为此复制完整游戏规则或扩大到未实现的商店补货/移除。

## R2：新购对象登记类型未与模型和商品类别绑定

位置：`tools/native-run/NativeThirdShopContract.cs:70` 的 purchase-start 及取得结果校验。已检查 offered/acquired 引用、登记 model、实际新增对象、支付及库存，但 `identities[id].type` 未用于校验被购买对象的真实模型类型。

下面各副本只改一条本次实际 offered 对象的 `identity.type`，其 model、引用和所有状态均保留。生产检查器均返回 **0**：

| 副本 | 矛盾登记 |
| --- | --- |
| `controls/07` | `RELIC.MEMBERSHIP_CARD` 对象 `i990` 被登记为卡牌 `Inflame` |
| `controls/08` | `POTION.EXPLOSIVE_AMPOULE` 对象 `i992` 被登记为遗物 `MembershipCard` |
| `controls/09` | `CARD.INFLAME` 对象 `i980` 被登记为 `Creature` |

最小修复：对当前集成商店支持的实际模型，统一核对登记具体类型、model 和原生商品类别，并把 offered→acquired→实际牌组/遗物/槽位串起来。使用真实固定版模型元数据或窄的已声明模型映射，不凭卡名猜测、不设计公开 SDK。补三类合法对象和错误类型控制，保持现有实例、金额、升级及会员卡价格契约。

## 已独立成立的原生事实

- 正常 Neow 开始、同一 seed/Run，第三幕五场原生战斗胜利，随后真实 Death545，停于第三幕第十二层。过程没有 API 房间/资源夹具。不是第三幕 Boss 胜利或完整 RunVictory。
- 同一真实 MerchantRoom 内五笔原生命令交易：MembershipCard 185、Pantograph 112、StoneCracker 114、ExplosiveAmpoule 25、Inflame 36，共 472 金币。原生 callback、实际新对象、库存清除和后续离店出牌存在。原生任务由 `OnTryPurchaseWrapper(ignoreCost:false)` 执行并等待；没有手工 append 或资源注入。
- HungryForMushrooms 实际 BigMushroom 分支：HP21→41、maxHP76→96，并实际承担首回合少抽两张。PotionCourier 实际 Ransack：一次 Rewards RNG 推进、原生奖励任务完成及 HeartOfIron 取得。锁定程序集只读反编译核实了两效果；副本只保存在本地忽略目录。
- 545 路线的 detailed replay、minimal、off 与 live 的 **1090 个派发/确认完整状态、RNG、root/context** 相等；20 条商店/价格子记录、完整动作路线、选择、奖励选择及最终状态亦相等。不是只对旧 JSON 的部分字段比较。旧 537 路线在新采集器中的实际重放与原执行 1074 个边界一致。
- 9 个原生进程来源/输出记录一致。EngineError 为 3 确认/4 派发、原始退出 1；BudgetExceeded 为 4/4、原始退出 3，二者检查器拒绝认证完成。新观察与旧 v17 实际 545 路线完整边界相等。
- 实际宿主报告 MemorySaveStore、禁保存、Steam 未初始化、UI 空实例、无 Mod/CombatSolver。70 次进度保存与 22 次 replay 写入为宿主拦截计数，不能当作持久化写入。

## 死亡最大生命值观察的专项结论

`NativeDeathObservation.MaxBefore/MaxAfter` 是原生 `Creature.MaxHp` setter 的同步 prefix/postfix，没有替换 setter 或修改资源。`Own` 要求对象是 `Program.player.Creature`、已经死亡、处于当前原生战斗动作。固定版 PaperCutsPower 的 `AfterDamageGiven` 会在玩家受到实际未格挡伤害后调用 LoseMaxHp，并不排除已致死结果，因此 fatal damage 与清理之间再改 maxHP 是实际合法边界。

本例原始顺序是 fatal damage → maxHP-start86 → maxHP-return84 → powers-start/return → loss。前后同步相邻、同实际玩家 `i23`、同 a545/root/context；最大生命值返回只改变 maxHp，完整状态及 RNG 保持，随后能力清理使用该返回作为新锚点。源码和生产负控均验证这一有限同步框架。

独立加入的敌人/卡牌主体、错 root/context、RNG s0 变化、额外状态字段、无变化目标、返回与能力清理错序、额外事实字段均被拒绝。此前的缺失、重复、错误目标等控制也全部重验拒绝。本轮没有发现需要修改该死亡观察实现的必修项。

旧 11 份 v1 缺玩家锚点证据仍拒绝；开发期 v2 Death537/545 因缺本次 maxHP 锚点仍拒绝，**没有回填或误认证**。新进程重新执行成功只能认证新证据。setter 的实际 Harmony 注入审计已有两次拒绝证据，本次核对来源和作用域，未重跑该 API 夹具；它的业务调用次数为零，不当作死亡执行证明。复活、死亡预防和完整触发因果图仍未覆盖。

## 修复交接与停止门

先修 R1/R2，通过本次 6 个反例、相关合法副本及受影响原始证据重验。若仅修改检查器，遵循 [分层验收](NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)，不重跑无关原生矩阵；若为保存必要价格来源而改变采集器，重跑受影响自然商店/离店路线及重放即可，明确新旧生成链。

通过后继续 [既有下一步交接](NATIVE_RUN_ACT3_SUPPLIES_NEXT_HANDOFF.md)：当前策略在低 HP、第十二层 ScrollsOfBiting 存活仍有困难；第三幕 Boss、正常开局 Architect/RunVictory 及最终 M1 同构建全量验收仍未完成。现有授权持续有效，无权限或环境阻塞；本轮止于独立审查，未开始修复。
