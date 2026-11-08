# 策略信息边界审计（试点）

2026-11-04 建立，2026-11-05 更新。当前试点和 M1 的 CombatSolver 决策均为 **Oracle**。已新增受限公共 DTO 和纯消费者控制；端到端玩家可见策略仍未验收，详见[搜索与可见接口试点](../performance/PERFORMANCE_SEARCH_AND_VISIBLE_PILOT.md)。

## 已核实的输入通道

| 通道 | 实际内容 / 证据 | 当前结论 |
| --- | --- | --- |
| `NativeSolverGate.SolverCapture/SolverQuery` | 真实 CombatRootSnapshot、ContinuationStamp、live CombatState；搜索前后检查原状态 / RNG | 精确分支校验需要这些信息；不能直接作为玩家可见策略接口 |
| 锁定 CombatSolver `src/Runtime/ContinuationStamp.cs` | 有序抽牌堆、Stars、Orbs、Osty、敌方 AI 状态、九条 RNG 完整状态 | 包含隐藏信息；不是公平策略的观察投影 |
| 动作与证据 JSON | `root`、状态哈希、完整 nativeState、候选路线、未来死亡与胜利预测、可关联的对象 ID | Oracle 诊断数据，不应传入受限策略，也不应允许其回读日志 |
| RandomForeseer `RunPredictionContext` | Clone 复制部分 RNG、奖励状态及牌组表示，但仍保留 live Player / RunState | 属于带内部随机状态的预测路径，不能假定是隔离的多步沙盒或玩家可见信息 |
| `NativePolicy` 与正常开局 | 现有原生宿主/实验档案仍包含 TestMode 和固定选择策略 | 可证明执行路径，不代表真人决策、零售演出等价或未知选择的公平性 |

首批锁定依赖：CombatSolver 0.50.1 / `death-amount-sync.1`（DLL `A5AEFA38…D2C35E`），RandomForeseer 0.13.19 的已审核源子集。后续[修复试点](../performance/PERFORMANCE_PILOT_REPAIRS_AND_COSTS.md)另建 `osty-root-identity.1`（DLL `376D1E61…B7D205`），只修正宠物根身份捕获，旧输入保留。各自 MIT 与上游归属保留，不初始化完整 Mod 或改写评分；信息权限仍为 Oracle。

## 玩家可见策略的最小门

1. 独立不可变白名单 DTO：公开生命、格挡、能量、Stars、Orbs、Osty、状态效果、手牌、公开弃牌/消耗牌、可见敌方意图、当前合法动作、公开地图和已发生事件。抽牌堆仅在游戏确实公开时给对应信息，通常只给剩余已知多重集，不给顺序。
2. 不传 Player/RunState、solver root、游戏 RNG/seed、状态哈希、未来预测或日志路径；也不通过回调让策略取得这些对象。动作令牌必须只由公开实体分配，不能以隐藏牌序或隐藏状态哈希编码。
3. 策略随机性由独立登记的策略 RNG 提供。需要未知未来抽样时复用适用的上游机制，但必须基于公开分布，不能偷偷使用真实游戏随机状态。
4. 对公开状态相同而隐藏抽牌顺序/RNG/未揭示节点不同的配对输入，检查 DTO 字节和确定性决策相同；策略 RNG 固定时同样成立。验证无日志回读、对象引用或错误消息泄漏。
5. 原生执行器仍用完整状态校验动作与记录真值；受限策略只能接收经筛选的执行结果。Oracle 与受限策略的数据、标签、指标分开。

这需要一个内部观察投影及能力受限接口；无需先设计公开 Mod SDK。本轮验证了真实原生状态的公共子集投影、固定合法动作向量下的隐藏字段置换，以及无游戏/求解器依赖的纯消费者；未实际部署该策略，未在另一原生隐藏世界重算合法动作，未覆盖职业资源/意图等公开信息，也不是恶意代码的 OS 沙箱。因此仍不能报告公平胜率、真人水平或数学最优。后续公共字段应整合到既有 `CombatObservationV1`，避免试验 wire 与旧观察模型长期分叉。

## 预算与比较

已核实上游有 Low/Medium/High/VeryHigh/Custom 和可配置搜索并行度。它们控制单次搜索，不等于整局或批次预算。最初职业首战批次沿用小 Custom 配置：beam8、每成员256节点、10秒soft、15秒取消、DOP1，4096为返回总工作量准入门。后续单独登记的同根研究比较 LabBounded/Low、DOP1/2，使用独立取消门；并未据此改变完整在线样本的默认配置，详见最新报告。

M1 完整样本用 DOP2、显式 RunToTerminal、16384返回门；本批首战用 DOP1、Guarded、4096返回门。两组耗时不构成优化前后对照。成员时间、GC计数可能重叠；重放仍求解，不能用在线减重放推算搜索成本。

公平比较需同信息权限、有效预算与预登记训练/留出样本，保留死亡、Unsupported、BudgetExceeded 和错误分母。同 seed 不保证不同策略走相同随机轨迹；同路径重放也不是新增独立样本。


## Bounded native deployment (compatibility pilot)

The next pilot adds `visible-battle` and `visible-battle-replay`: the existing pure `VisibleCombatPolicy` consumes only its serialized `CommonCombatSubset` DTO, returns a public hand/enemy-slot token, and the host binds it to a fresh legal native action. Host diagnostics retain Oracle state and RNG separately. This is a trusted code/data-flow boundary, not an OS sandbox for hostile policies. The first registered natural Ironclad battle ran 16 policy actions to a real native battle victory with zero solver queries; its same-build replay matched all 143 state/RNG observations and all 33 policy/execution files. See [the compatibility pilot](../performance/PERFORMANCE_COMPATIBILITY_AND_VISIBLE_EXECUTION.md) for source/build identities, production controls and failures.

This certifies only that bounded first-battle deployment. The DTO is intentionally incomplete (for example, no general class-resource/intents/cost view), no full-run non-Oracle policy was deployed, and alternate hidden native worlds were not reconstructed. Hidden-field substitutions test the projection with the same public legality vector. They must not be promoted to an adversarial sandbox or whole-run fair-information guarantee. Future production policies should reuse the existing Core observation/policy ports rather than create another permanent parallel SDK.
