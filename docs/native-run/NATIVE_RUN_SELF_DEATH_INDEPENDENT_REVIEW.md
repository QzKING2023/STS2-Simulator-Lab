# 自伤死亡认证修复独立审查

2026-11-03。结论：**真实自伤死亡的采集与回放成立，但本轮验收未通过：死亡主体缺少与实际玩家实例的绑定，4 个定向坏副本被生产检查器误接受。** 修复范围应限于这个身份契约，不需要重新实现死亡规则。完整 M1、第三幕 Boss 和 RunVictory 仍未完成。

审查冻结：`A5126450FBE068F1239FAE3639CF193759ADF3460AFCE9E02CFD3503E7F4ABAB`。独立证据目录：`.build/runs/self-death-independent-review01/`，入口 `closeout.json`。本轮只新增审查文件和副本，未修改生产实现或原证据，未重跑原生对局，没有 UI、真实 Steam/网络、用户存档或提交推送操作。

## 已核实的真实行为

- 同一旧 v11 路线，详细、minimal、off 三个独立原生进程均实际执行448个根动作后 Death；原始退出0，448派发/448确认。三种模式的896个完整派发/确认投影与旧原始执行相等，包含所有已投影 RNG 的 counter 和 s0–s3。route、choices、reward-choices、final-state亦一致。这里是已记录投影的完整比较，不是全部隐藏机械状态已采集的声明。
- a448、epoch38/index215的原生致死历史来自玩家回合第7回合 Start，HP0，实际未格挡伤害1。随后7个新增观察记录对应：致死事实、能力移除入口/返回、Start→AutoPostPlay入口/返回、AutoPostPlay→End入口/返回。实际返回的移除对象为 i918/i929/i930，对应 Strength、Inferno、Stampede。最后 setter 返回状态与 ProcessPendingLoss 入口全等。
- `NativeDeathObservation` 的两个 Harmony 包装只观察原方法：`Creature.RemoveAllPowersAfterDeath` 返回已物化的实际移除列表；调用者随后另行 await 各能力的 AfterRemoved。phase观察包装同步 setter。没有跳过原方法、重算保留规则、枚举惰性业务序列或把外层 Task 返回当任务完成。详细历史215项完整。
- 固定死亡文本已改成 ProcessPendingLoss 在安全边界完成，不再把所有死亡归因于敌方动作。具体触发 Power 实例在原生历史中不可得，保留 `not-recorded-by-native-damage-history`，没有把静态 Inferno 推断伪装成直接观测。
- 当前 live 路线真实 Death474及独立冻结重放成立，948个完整边界与先前已通过路线相等。skip24、take25、EngineError的3确认/4派发前缀、BudgetExceeded的4/4前缀保持。
- 两个非法药水控制实际沿同一冻结路线执行到413步。第414步槽位或目标错误均在新根派发前被拒绝，InvalidAction/exit1、413/413、DataComplete=false；826个完整前缀状态与正常路线相等。两份输入各只修改旧路线第414个动作，明确标 fault-control/invalid-route，没有替换种子或重写已确认前缀。

详细模式的 executionKind 是 frozen-replay，minimal/off 是 observation-control 且 Replay=true；三者 scenarioKind 均为 natural-run。观察对照仍实际消费同一冻结输入，不是实时策略自主选择了同一路线。

## 必修 R1：死亡主体不能只与自己重复的数据相等

位置：`tools/native-run/NativeDeathContract.cs:20–33`。首次 fatal 只检查 `subject == result.Receiver`，然后把该 subject 保存成后续操作的权威值；之后仅检查各记录与它相等。它既没有绑定实际当前玩家 Creature，也没有要求该身份确实是 Creature。详细模式虽比较整个 DamageResult，却没有把 Result.Receiver 与同条原生历史的 Receiver/Actor 及实际玩家身份交叉绑定。

生产检查器实际执行的四个反例如下。副本均重新封签；所有动作、上下文、顺序、完整状态及终态保持原样，并未协同重写整个游戏世界：

| 副本目录（相对独审目录） | 改动 | 实际错误结果 |
| --- | --- | --- |
| `controls/11` | minimal：7个死亡观察的subject及fatal Result.Receiver改为i911 | exit0、trace-review.status=passed |
| `controls/12` | off：同上 | exit0、passed |
| `controls/13` | minimal：上述身份改为i1 | exit0、passed |
| `controls/14` | detailed：上述身份改为i911，并将紧邻历史的Result.Receiver同样改为i911；历史Receiver、Actor仍为i23 | exit0、passed |

原始登记和未改快照明确显示：i911是当前存活的 Fabricator，HP48/alive=true；i1是 `StrikeIronclad / CARD.STRIKE_IRONCLAD`。玩家实际为i23。前3个反例各只改8处引用，详细反例改9处，所有完整state都未改；具体字段路径、登记和敌方状态见 `counterexamples.json`。这是已声明死亡主体/实例关联契约内的遗漏，不是要求检查器对任意整份日志伪造重新执行游戏规则。

最小修复要求：

1. 从受控原生观察获取当前本地玩家 Creature 的不可变引用，并绑定当前战斗/房间的既有生命周期。优先复用已经登记的对象，避免无必要地改变稳定实例编号。不能硬编码i23，不能从正在被验证的fatal.subject反推玩家身份，也不能只排除本次i911/i1。
2. fatal主体、Result.Receiver、能力移除和阶段setter的主体都必须等于该玩家引用；类型/登记必须匹配。详细模式同时交叉验证原生 DamageReceivedEntry 的相关 Receiver/Actor 字段，遵循固定原生定义。minimal/off同样保有必需的玩家身份锚点。
3. 保留本轮正常死亡链和4个反例，补覆盖未登记身份、已登记非Creature、当前/旧敌人和错玩家引用的有限控制；不要改为复制完整死亡规则或继续增加无关机制。
4. 若可仅用已采集权威字段修复检查器，按分层门重验冻结证据和定向控制即可；若需新增玩家身份观察，则重跑受影响自伤三模式、敌方死亡重放及最小烟测，记录新采集指纹。无需因此重跑全部历史原生矩阵；最终M1同构建全量门仍保留。

## 审查证据范围

- 独立核对98份实际dirty源码、62编译输入、3游戏输入、7产物及规范化manifest；当前SDK10.0.201。3849项来源/绑定断言通过，8个生产文件变更范围与报告一致。15锁定输入和52兼容源码保持，锁定文件结束时无变化；该断言数量包含重复绑定，不代表3849个场景。
- 完整重验既有763份检查器输入：718原始/历史记录、43实施控制、2非法药水输出。393接受、370拒绝，均符合原先声明的预期；旧缺锚点Death448继续拒绝，不能倒写成旧采集已通过。
- 本轮另做18个有限生产控制：3个正常/等价接受、11个错误拒绝、上述4个错误误接受。已验证旧root/context、HP矛盾、移除返回错序或缺失、第二次阶段缺失、共同额外状态、未完成清理前确认、未知profile等仍被拒绝。不是仅验证测试脚本本身。
- 84项独立事实断言通过。初版审查脚本把minimal/off的executionKind也误写成frozen-replay，产生2项审查断言失败；核实正式来源契约是observation-control且Replay=true后更正。初版脚本和结果保留为 `facts-initial-assumption.*`，生产和原证据未改。
- 两个新增目标的真实 Harmony 注入负控已有4次未知补丁拒绝；本轮读取其原始记录和生产Register/Ensure实现，不重复注入。它们是零业务调用的审计API夹具，不是额外对局证据。

这些数量是证据规模，不是独立种子数或完整爬塔覆盖。输入、产物和前后哈希用于证明一致性，不能替代原始进程、调用链和重放的执行证据。

## 非阻断范围及交接

原生能力保留规则未改变，检查器按实际返回的移除子集验证；当前自然样本只实际覆盖三个能力均移除，不能宣称已跑过所有死亡保留效果。无伤害历史的直接Kill、死亡预防/复活和额外未观察AfterRemoved状态变化仍未认证。没有零售败局结算或同进程重用死亡run的清理承诺。

下一轮先修R1并进行有边界复审，之后继续第三幕合法策略/Boss与正常开局RunVictory。没有环境或权限阻塞；当前阻断项是上面可复现的生产身份校验缺口，未作实现修改。
