# 默认拾取之后：继续原版完整流程

2026-11-04；M1持续授权，无需逐步请批。本批结束后不要重置额度。

## 证据与构建

先读 [本轮验证](COMBAT_SOLVER_RELIC_PICKUP_VALIDATION.md) 和 `.build/runs/solver-relic-pickup-work01/closeout.json`。最终冻结 `6C02AFDC464AA71611330AAA56ACD74AA4795CFE94FB58BBDD3432D6220FB721`，原生输入与52兼容源码不变。上一轮旧冻结同构建重放已补齐；本轮新在线和最终回放跨构建，差异仅默认拾取来源方法归一化，必须保持分别标记。

## 实际停点：SlipperyBridge

410确认/411派发，第三幕第11层。a411已经原生Travel到EventRoom，然后入口审计明确Unsupported；不能把该动作补写完成或直接拼接后续。保留全部前缀和原始exit。

优先核实固定版本 RandomForeseer 的 `EventOptionPrediction.cs:81` 注册 `SlipperyBridgePrediction.GetHoverTips`。当前Lab选取的源码快照包含注册表但没有该predictor文件，注册表出现不等于Lab已接通预测。下一轮从既有合法本机上游参考中核实实际方法、MIT归属、依赖和可复用范围，新增独立输入锁，不热改旧快照。预测与原生事件执行分别记录；不要复刻已有事件逻辑。

核实原版SlipperyBridge的入口、实际选项、完成任务/异常、随机调用和资源变化，复用已有原生EventSynchronizer/选择桥。先明确安全展示依赖与状态边界，再有界原生执行至后续稳定边界；若预测只覆盖部分选项如实标unknown。禁止仅去掉白名单或跳过业务来制造完整流程。

## 同时保留的检查

- 默认遗物来源审计使用真实声明MethodInfo，已有真实Harmony注入漏检与修复证据；不要退回继承句柄查询。
- MummifiedHand已原生入库，后续战斗镜像存在于固定CombatSolver但本路径尚未验证。若出现差分，先审上游机制与调用链，不逐遗物任意特判。
- 冻结回放使用 `solver-native-darv-act2-replay`；live只接受规定110动作前缀。未确认的已派发动作必须来自真实root-request并保留空after/confirmed。
- 以原版整体流程推进衡量结果，保留死亡/预算/Unsupported，不针对单场调整评分。按影响分层回归，最终M1同构建全量门不取消。
- 无UI/真实Steam网络存档/其他项目/提交推送；保持15输入与52兼容源码，不重置dirty工作。
