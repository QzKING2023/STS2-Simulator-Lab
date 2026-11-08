# 通用有限预算候选接入与自然七胜前缀

2026-11-04。已修复预算检查 R1，并复用固定 CombatSolver 0.50.1 的候选、可信度和风险补偿机制。新 opt-in 场景 `solver-native-budget` 在原有 beam8、1000 节点、soft5 秒、单 worker 设置下，实际越过旧 query321 停点，完成七次原生战斗胜利与奖励，106 个确认根动作后停在商店。当前为 **Unsupported / Ongoing / DataComplete=false**；第 5 步和完整 M1 仍未完成。

没有修改上游输入、牌效、Boss 评分、游戏或角色工程。默认 `solver-native-boss` 保持严格预算停止。MIT 和既有上游归属不变。

## 预算 R1 与检查器验证

新增 `NativeSolverBudgetContract`：当前只认证单 CombatBeamSolver 的计数协议，明确区别 Coordinator。核对 ExpandedNodes 非负、TotalExpandedNodes 与其相等且不越过有效上限；Profile、query budget、配置快照的节点/beam/soft 时间和单 worker 一致。NodeLimit 必须实际达到上限。TimeLimit 要求独立采集的搜索耗时且达到 soft 时间；新候选采用模式仍拒绝 TimeLimit。旧文件没有耗时字段时不补默认值证明超时。

修复 reader 为 `1E7EF938ADE84AD2BBC8CC89BD488AA6544C18FD52A05F9DD57809AC6C8DBA56`。`.build/runs/solver-budget-repair-work01/` 的 10 次真实生产 reader 调用全部符合预期：旧 live/replay 正控通过，原“0/0 展开却报 NodeLimit”、缺计数、负数、聚合矛盾、配置矛盾、错误引擎和零上限被拒绝。此部分原生执行为零。

最终 reader 又重读旧 NodeLimit live/replay、旧双战 skip/take 和原反例，避免把修复构建的结论直接套到后续 reader。

## 候选采用的有限契约

`NativeSolverCandidateContract` 使用显式 `node-first-action/v1` 策略。只允许正常返回的 None 或 NodeLimit 结果进入单步验证；要求 SearchCompletion、内部 Snapshot 边界为 None、无未补偿风险/缺口、不支持或近似预测、非预测死亡、有当前回合候选。读取并保存真实 SolverResult 的范围、Snapshot 风险/边界、UnmirroredDetails 和 Forecast 标记，不从旧证据伪造这些字段。

通过该门只代表可进行 shadow 验证。随后仍使用既有牌实例、升级、目标、药水槽及子选择绑定，检查原生当前合法性；在独立 fork 执行第一动作、确认父状态与原生源/RNG 不变，才派发真实动作。每一步要求完整 continuation/RNG 差分，再重新捕获和求解。实际 step 保留 `searchBoundary` 和 `incompleteSearch`；不会把 NodeLimit 改成 None 或把有限搜索称为全局最优。

TimeLimit、MemoryNoProgress、未完成选择、未知效果、无合法候选、过期票据、未知补丁及差分失败均保持显式拒绝/停止，没有静默换成贪心策略。上游 EndTurn 的 IsExecutable=false 表示沿用原有 EndTurn 契约，不被当作普通卡牌采用。

15 项 JSON/API 控制使用生产候选门，覆盖正常 None/NodeLimit、空候选、风险/gap/未知意图、近似预测、预测死亡、未完成边界、过期回合、未知策略、TimeLimit、MemoryNoProgress 和错误计数，全部符合预期。这里的正控元数据是明确标记的协议夹具，不是实际 SolverResult 或原生执行证据。

## 首次停止与上游风险补偿的复用

原生执行 `solver-budget-exec-01` 使用 `FFF9AC436963AE11007AC67A706E9E90111434EBC09C607FE2B4ED8253C08489`，在 query9 的 shadow 检查返回 Unsupported，保留 11 个已确认根动作，未派发第 9 个 solver 动作。原因是初版把 `CombatPredictionSimulator.HasRisk` 的原始历史标记当成未建模风险。

检查固定上游后确认 `CombatBeamSolver.StateEvaluation.GetCoverageSummary` 调用 `PredictionCoverage.Collect`，区分原始历史风险与已由上游补偿目录实现的效果。Lab 改为复用同一个分类器，逐查询保存 shadow 原始标记及全部补偿项，拒绝任何未补偿项。生产 reader 要求实际 step 的 gap 列表与独立 shadow 文件一致，且全部已补偿。这没有新增效果白名单，也没有关闭审计；01 的失败证据保留，不计作认证七胜路径。

## 原生 live 与精确重放

最终生成器为 `414602E55020326091A6B3DA3A90C0F859128EA7930D24D6483C556505C9FE22`，`.build/native-build/solver-budget-final04/`。正常 Ironclad/A0、种子 `LAB_V0111_NATIVE_DEV_01`，原始执行 `.build/runs/solver-budget-exec-02/03` 分别为 live 和冻结输入重放。无 FixtureSetup、资源编辑或状态拼接。

两次均有 106 个派发/确认根动作、76 个求解动作、七次真实原生 Victory 回调/清理及奖励。实际遭遇依次为 Nibbits、FuzzyWurmCrawler、ShrinkerBeetle、Fogmog、RubyRaiders、Slimes、CubexConstruct。保留原生事件、三次 SMITH、宝箱领取和合法地图推进。

query321 的 NodeLimit 候选首项是 FlexPotion，a81 实际使用该药水、原生结算与模拟 continuation 完全一致，随后重新求解并完成 Slimes 和 CubexConstruct。该 pair 只有这一次采用的 NodeLimit；不能据此宣称所有有限预算候选、药水或战斗均支持。ShipInABottle、BlockPotion 也由在线求解器实际使用。

route、普通与奖励选择、final-state、全部 76 个 step/shadow 文件相等；76 个 query 的完整状态、binding、RNG continuation、动作、Profile 和真实候选诊断相同。查询及 fork 不扰动源状态/RNG，未消费旧票据跨动作与战斗被拒绝。当前信息模式明确为 Oracle。

最后在第一幕 `MapCoord (5, 14)` 到达真实 MerchantRoom：HP80、金币581、永久牌17张。错误来自 `PreparedNonCombatRoom` 的既有有限分支，尚未接商店，不是求解无解或模拟差分失败。既有第三幕商店模块可复用，但其房间/幕归属、原生定价与退出契约尚未接到本 profile；不能直接标房间完成、忽略商店或改用夹具拼出 Boss 路径。

## 失败前缀检查与构建区别

后续变化仅为检查器及其提前 CLI 分支，reader 为 `D1BF51CB29F132B41810192CC064B2E2978518FAC7BED326FD38815BB5F69E01`，`.build/native-build/solver-budget-reader05/`。生成器 final04 的原始证据未改写构建身份。

新增 `--review-solver-room-prefix` 只接受本 profile 的明确商店未接入停点：原始退出 2、Unsupported、Ongoing、DataComplete=false、没有待确认动作/任务/选择；此前战斗/奖励闭合，原生 Travel 到达 MerchantRoom，最终完整状态绑定最后确认及 room-entered。七个完整战斗均复用生产逐步差分检查。输出为 `prefix-validated-unsupported-not-completed`，普通 `--review` 仍拒绝它作为完成成功。

该失败路径没有最终 history-checkpoint 或整局数据闭合标记；不补造记录、不认证完整 Run。专用前缀检查不代表任意 Unsupported 或任意中途故障都已认证。

第四次执行使用最终 reader05 构建运行原严格场景，实际退出 3，在 query321 拒绝候选；普通路线、最终完整状态、全部旧 step 字段和 query 状态/RNG 与此前冻结严格证据一致。该执行的候选前 80 动作及 query321 的源状态、continuation、binding、动作和 Profile 与新采用模式也完全相同。因此本批对照支持“明确采用同一预算候选后继续推进”的归因，没有把不同前缀或评分变化当作效果改善。严格烟测的失败前缀另经生产 reader 接受，仍未认证完成。

`.build/runs/solver-budget-work01/` 的 52 项事实/指纹核对全部通过，`preflight/` 两份正常前缀通过；`final-controls/` 的 14 次生产调用全部符合预期，包含原 R1、旧预算/双战回归、风险/近似候选、隐藏不完整搜索、错误药水目标、最终状态矛盾、错误停止类型和合法等价。编译保留既有 4 个 warning、0 error。

## 预算与下一步

预登记为最多 4 次原生执行、20 分钟、2 GiB、单进程 120 秒；没有增加搜索节点或针对场景调分。01 为明确失败，02/03 为最终同构建有效 pair，04 为保留严格模式的受影响烟测。精确退出、时间、字节和第四次结果以本批 `ledger.json`、`closeout.json` 为准。不得重置 ledger 追加执行。

15 锁定输入、52 兼容源码、826 个 solver 源文件和 6 个程序集保持；MemorySaveStore、ShouldSave=false、Steam 未初始化、离线单人服务、无 UI/遥测/异步故障、保存与 replay 拦截继续核对。没有提交推送，RF 未加载或执行。

下一最小工作包是复用 `NativeThirdShop`、`NativeMerchantEconomy` 和已存在商店契约，解除其仅第三幕 profile 的耦合，明确实际当前幕/房间/库存归属。原生进入前须启用已审计的价格/RNG路径，交易及退出必须闭合，不能靠 TestMode 价格捷径或跳过节点。保持当前 battle 配置和奖励策略，先让自然商店→离开→后续战斗成为一个有界切片，再接已有 Boss 奖励/跨幕控制器。特殊补货、拾取选择、既有折扣遗物等按真实依赖显式受控，未知不假通过。

战斗外预测继续按 [RandomForeseer 复用说明](../validation/RANDOM_FORESEER_REUSE_NOTE.md) 的普通单节点奖励门推进：固定来源和依赖、复用计算入口、调用前后完整 live Player/RunState/RNG 不扰动及原生候选比对；当前没有新增预测实证。部分上下文复制和 AfterModifyingCardRewardOptions 多步限制仍禁止将其用作隔离的多步原生 fork。原生业务与预测分别报告。完整 M1 和最终同构建全套验收仍待后续完成。
