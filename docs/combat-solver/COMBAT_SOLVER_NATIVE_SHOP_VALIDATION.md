# CombatSolver 原生商店组合与 Boss 首决策

2026-11-04。第 5 步本轮已将既有原生商店接入新的 `solver-native-shop` 场景：正常开局、同一 Run 的七场胜利及奖励之后，完成四次原生购买、合法离店、休息升级并实际进入第一幕 Vantom Boss。110 个根动作确认，76 个战斗求解动作。Boss 首次查询被既有 `candidate-predicts-death` 采用条件拒绝，尚未派发 Boss 动作。原始退出仍为 **2 / Unsupported / Ongoing / DataComplete=false**，未证明 Boss 胜利或实际死亡，第 5 步与 M1 仍未完成。

## 复用与范围

复用 `NativeThirdShop` 的库存、原生 `OnTryPurchaseWrapper` 及完成 Task、实际付款/库存/取得实例、原生 MerchantRoom.Exit；复用 `NativeMerchantEconomy.InstallPriceOnly` 恢复固定版本原生药水价格 RNG 路径。没有另建商店效果执行器或修改 CombatSolver 评分、节点预算、牌效、资源。

新场景仅解除既有代码对“第三幕 profile”的耦合，使用显式注册的第一幕及真实当前房间/地图绑定。历史 `ThirdShop` 事件和 `natural-third-shop/v1` 是内部复用协议名称；旧第三幕仍只接受 act2，新场景只接受 act0。Header 必须声明该集成 profile，未知场景不能提交这些商店事件。

旧 `solver-native-budget` 保留到达商店后的 Unsupported；旧严格 `solver-native-boss` 保留预算即停。Courier、特殊拾取/拒收/替换、移除、已有折扣遗物原价采集和其他未认证交易仍没有被本场景宣称完整支持。此次购买仅使用既有默认拾取分支。原生退出中的 TestMode 历史差异仍按旧模块范围说明：旅行与房间账本由 Lab 完整记录，未声称零售内部统计完全等价。

## 原生事实与重放

生成器 `96716731D32672CC23F4F5DEA899C450852B0DD7C027E0A276BCFB59CFA253B0`，`.build/native-build/solver-shop-final01/`；原始 live/replay 为 `.build/runs/solver-shop-exec-01/02`。固定 v0.111.0/41cef1ea、Ironclad/A0、种子 `LAB_V0111_NATIVE_DEV_01`。无 FixtureSetup、状态拼接、强制结果或资源编辑。

第一幕 `MapCoord (5, 14)` 的同一原生库存产生四次真实交易：

| 实际对象 | 金币 | 原生结果 |
| --- | ---: | --- |
| OddlySmoothStone | 161 | 新遗物 i270，库存售罄 |
| WingCharm | 177 | 新遗物 i274，库存售罄 |
| ExplosiveAmpoule | 51 | 新药水 i276，进入实际空槽 |
| TrueGrit | 51 | 新永久牌 i260，升级0，库存售罄 |

总支出 440，金币 581→141，HP80 保留，永久牌组17→18。每次付款、新实例、商品原引用、模型、库存状态和 PurchaseCompleted 回调均一致，等待原生交易完成。三个药水价格各消耗一次真实 Shops RNG；购买前后其余状态变化按既有完整契约核对，未人为重置 RNG。

a108 的真实 Travel 拥有商店 Exit 的开始/返回，随后到达休息点。a109 的 SMITH 原生选择 q4/o10 指向既有 PommelStrike i99，升级0→1；同一个永久实例和全部其他牌保留。a110 实际进入 `ENCOUNTER.VANTOM_BOSS`，`MapCoord (3, 16)`，HP80、金币141、牌组18，遗物和药水继承。Boss 起手与 RNG 由原生初始化。

七次战斗胜利仍由真实回调及清理记录验证，76 个动作仍有原生/模拟 continuation 与 RNG 差分、源/父 fork 不变及过期票据拒绝。它们是战斗局部差分；商店与地图是原生业务执行加独立契约验证，没有声称整段业务存在独立仿真镜像。

两份原始执行的路线、普通/奖励选择、final-state、全部 step/shadow 文件及77个 query 的完整 nativeState、continuation、binding、动作、Profile、候选诊断一致。重放仍执行真实原生命令并核对冻结动作输入。

## 停止含义与失败前缀

Boss query449 正常返回 `Boundary=None`，展开932节点，实际输入未改变，候选无已知未补偿 gap 或不支持意图；但候选末端 Snapshot.PlayerDead=true。当前 `node-first-action/v1` 对此拒绝采用。候选首项是 FlameBarrier，既没有被原生派发，也没有被当作模拟胜利或真实死亡。

这次停止来自当前通用采用条件，不能解释成 Boss 业务已不兼容、所有策略必败或预算耗尽。它也不能证明后续真实战斗安全、可胜或全程可精确预测。没有针对 Vantom 调分、换种子或增加节点来消除该结果。

新增仅供证据读取的 `--review-solver-rejected-prefix`：要求实际退出2、Unsupported、Ongoing、DataComplete=false，已确认动作/任务/选择闭合；逐个复验七个完整战斗与商店契约，并将下一战首个未采用查询、源状态/RNG、root/context、实际拒绝条件与报告原因绑定。未采用查询不能含额外 dispatch/step。输出明确为 `prefix-validated-rejected-candidate-not-completed`。没有最终 history-checkpoint，不补造完整 Run 数据；普通 `--review` 仍拒绝该失败路径作为完成成功。

reader02 首次发现顶层事件路由仍只登记旧第三幕，正常证据因此被拒绝。该失败保留在 `preflight/`。reader03 将路由统一到同一 `NativeThirdShop.IsScenario` 注册边界，仍由原有价格/交易/退出检查器验证全部事件；没有跳过事件。只有检查器和提前 CLI 入口变化，未重新执行原生流程。

最终 reader 为 `CF55DD217BE07920D4A3EB2CACC917F4E45BACC42FDB34AB7D15D86B11DFD3AD`，`.build/native-build/solver-shop-reader03/`。正常及等价副本、错误金额/实例、缺失/重复交易返回、退出前后共同状态篡改、错误幕/库存/拒绝理由、预算计数，以及旧商店 Unsupported、严格模式和旧第三幕自然商店回归，共17次生产 reader 调用全部符合预期。

68项事实/指纹核对通过。事实脚本最初错误要求“商店后牌组到Boss完全不变”，没有计入途中真实 SMITH 升级，原66项中的两项失败保留在 `facts.json`；已根据独立升级规则、实际选择实例和原生休息记录核对其唯一合法变化，结果见 `facts-final.json`。未修改原始数据或重跑游戏来修正断言。

## 预算、隔离与下一步

预登记最多4次原生执行、20分钟、2GiB，单进程120秒；实际只用 live/replay 两次，共40.0138552秒，不为用完预算追加执行。字节、截止时间和最终锁见 `.build/runs/solver-shop-work01/closeout.json`。编译0error，既有4warning保持。15个锁定输入、52个兼容源码、826个solver源码和6个程序集不变，旧原始证据保留。

MemorySaveStore、ShouldSave=false、Steam未初始化、离线单人服务、无UI/遥测/异步故障继续成立；每份执行 SaveProgress/replay 拦截25/7次。不改游戏、角色工程或用户存档，不提交推送。

下一步优先分析上游通用部署规则和 CombatSearchCoordinator，处理“未来候选预测死亡”与“当前动作合法且可差分”之间的区别。当前 v1 规则应保留为可复现基线；若另设允许合法逐步执行并诚实记录预测死亡的 profile，须新策略版本、明确风险标签、原生 Death/Victory 终态以及逐步比较，不得修改旧拒绝证据或称死亡预测就是真实终态。也可先复用 Coordinator 的通用候选组合/可信度，在预设总工作量内对照。两者都应使用通用策略，继续禁止单 Boss 评分微调和无界试错。

当前商店模块已经闭合；自然 Boss 胜利、奖励/跨幕、第5步后段和完整 M1 仍待执行。战斗外预测继续先复用 [RandomForeseer 既有入口](../validation/RANDOM_FORESEER_REUSE_NOTE.md)，从普通单节点奖励对照开始，核查 live Player/RunState 不扰动及多步 hook 限制。此次商店接入没有新建预测器，也没有新增 RF 运行实证。最终 M1 同构建全量验收仍保留。
