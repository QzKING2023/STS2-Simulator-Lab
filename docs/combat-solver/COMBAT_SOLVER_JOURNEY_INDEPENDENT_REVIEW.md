# CombatSolver 第 4 步收口与第 5 步五胜前缀独立审查

2026-11-04。第 4 步在既定双战范围内通过：原两个 card/target 报告反例已关闭，真实领取奖励、地图推进、第二战执行和重放成立。第 5 步的自然五胜、80 个确认动作及预算停止事实成立，但尚未完成第 5 步或 M1。预算前缀生产检查器有一项有限的工作量一致性缺口，须在采用预算受限候选前修复。

本轮没有原生游戏执行、编译或生产代码修改。只读取固定代码和原始证据，并在明确标记的副本上运行生产检查器。独立证据目录：`.build/runs/solver-journey-independent-review01/`。`facts-final.json` 为 173 项核对全部通过；`controls.json` 为 20 次实际 reader 调用，其中 19 次符合预期，1 次误接受见 R1。不把控制数量当作游戏内容覆盖。

## 第 4 步：验收通过的具体范围

`NativeSolverActionContract.BindReport` 已被 `NativeSolverBattleContract.ReviewBattle` 调用。PlayCard 的报告卡牌绑定实际 dispatch 的实例，nullable target 绑定实际目标；非卡动作 card 必须为 null，EndTurn target 必须为 null，缺字段和错误 JSON 类型不会获得默认身份。

原独审 `rage-step-wrong-real-instance`、`attack-step-wrong-target` 两份坏副本均由当前生产 reader 拒绝，分别报 `report card/actual dispatch instance`、`report target/actual dispatch instance`。四份正常原生双战证据及合法 null 目标正控全部接受。

`solver-run-exec-01/02` 的 skip 路径是领取 17 金币后跳过其余奖励，并非跳过金币；29 个确认根动作、23 个求解动作、两次原生 Victory。`03/04` 的 take 路径有 31 个确认根动作、25 个求解动作、两次原生 Victory。Rage 永久牌 i37 的第二战副本 i44 通过 DeckVersion 关联，query71/a26 实际派发该副本。两路径均在同一正常 Neow 开局的 Run 中完成，没有资源夹具或手动追加奖励。

第一战清理、一次 RewardOffer、一次 ProceedRewards、一次合法 NativeTravel 有实际顺序和归属记录。金币 249→266、HP79、完整永久牌组/升级、遗物和药水跨战继承；第二战 turn1/Play、energy3、block0、powers 空由原生初始化产生。旧战斗未消费票据跨战被拒绝。清理与奖励是原生生命周期验证；逐战斗动作另有模拟 continuation 差分，不能把整段奖励地图流程称为独立仿真差分。

四份原始执行仍属于 producer `6D57D5954F46BC3E728D43B66C7C660D16C4A54D2F0D1F72CE3557C2DECDA984`。第 4 步修复 reader 为 `C58A313ACDAC7DF51ACAFFDC988BA40E7BEF365B365FE1062E5238B9FF9A02EF`；相对之前 reader 的编译源差异仅 ActionContract、BattleContract 两个检查器。当前综合 reader 重读旧证据不改变其 producer 身份。第二战奖励、更后节点和整局胜利不在此步声明范围。

## 第 5 步：实际推进与严格停止

最终有效 producer/reader 为 `A805EFC494A8FAC2424246BBC75FA52966DB5B1595DD3B6FB1B38112935F0C9B`，目录 `.build/native-build/solver-journey-final02/`；有效原始执行为 `.build/runs/solver-journey-exec-03/04`。

固定 Ironclad/A0、种子 `LAB_V0111_NATIVE_DEV_01`。已有原生房间控制器调用 CombatSolver 整场在线循环，完成五场战斗、五次原生奖励；11 个实际进入的房间包括 6 个战斗房、2 个事件、2 个休息、1 个宝箱。SunkenStatue/DenseVegetation 的真实选择、两次 SMITH、宝箱领取及离开均在同一 Run。未遇 Boss，也没有 RunVictory。

56 个 solver 动作均有查询、合法动作映射、原生派发、确认、父 fork 不变、未消费旧票据拒绝以及完整已采集状态/continuation/RNG 差分。a53 实际使用槽 1 的 ShipInABottle，a62 实际使用槽 0 的 BlockPotion；它们是在线求解器采用的自然持有药水。当前批次没有证明任意药水、任意嵌套选择或所有新牌兼容。

第六战 Slimes 初次查询 query321 到达 NodeLimit，原始 `ExpandedNodes=TotalExpandedNodes=Profile.MaxExpandedNodes=1000`。有候选，首项为 FlexPotion，但程序在创建采用票据前停止。没有 step321 或额外 dispatch。最后完整确认边界为 Travel:80，第一幕 `MapCoord (6, 11)`，HP75、金币546、牌组15。原始退出码保持 **3 / Ongoing / BudgetExceeded / NotCompared / DataComplete=false**。这既不是 Death，也不是战斗无解或全流程完成。

最终 live/replay 的 route、普通与奖励选择、final-state、全部 56 个 step，以及全部 57 个 query 的完整 nativeState、binding、continuation、动作和 Profile 相同。回放进程仍运行查询并核对冻结动作输入；本结论是这对固定配置的可复现证据，不能推论不同时间预算下求解路线总是相同。

早期 `01/02` 属于 producer `CBECA8FB8B815E3102A6CD2B26585410A81C116F4012CEB1F4FF9C2641E8AF92`，缺少新房间控制器要求的 UsePotion/potion-use 子会话观察，当前 reader 实际拒绝。它们完整保留为未认证执行，未混入最终同构建通过结论。之后复用现有 CAction/奖励观察包裹同一原生用药调用，并在原预算内运行 `03/04`，未改药效或评分。

四次原生执行各为退出 3，共 120.4899084 秒，均在 120 秒单进程、20 分钟批次和 4 次执行门内；实现者收口时保守统计 1,836,333,407 字节，小于 2 GiB。ledger 四项均已结束，不得重置或追加第五次。ledger 的通用 `scope` 仍残留旧夹具描述，以本批预登记 plan、每项 scenario 和实际 process/trace 为真实范围依据；下一批登记时应直接写正确 scope。

`--review-solver-prefix` 返回 0 只表示失败前缀通过指定检查，输出为 `prefix-validated-budget-not-completed`，仍保留 rawExit3、dataCompletefalse。普通 `--review` 对它返回 1。本轮还确认错误进程退出码、错误查询归属/源不变标记、假胜利、错误药水模型、最终 RNG 异常和缺少完成返回均被拒绝；正常及结构等价副本接受。该前缀没有最终 history-checkpoint，不能声称该条缺失记录已认证或完整 Run 数据已闭合。当前专用前缀检查只支持“若干完整战斗后，下一战首查询未采用”的边界，尚不是任意战斗中途停止协议。

## R1：NodeLimit 工作量矛盾仍会被接受

独立副本 `copies/pending-node-limit-zero-work/` 仅将 `solver-query-321.json` 的 `ExpandedNodes` 和 `TotalExpandedNodes` 从 1000 改成 0，保留 `boundary=NodeLimit`、`Profile.MaxExpandedNodes=1000`、真实状态、动作和所有其他文件。生产 `--review-solver-prefix` 返回 0。原始证据没有此矛盾，本发现不推翻五胜或实际 1000 节点停止。

根因在 `NativeSolverJourneyContract.cs:28` 的预算尾部验证：它绑定源状态、动作归属、continuation 和边界名称，却没有把停止原因绑定对应工作量/配置。当前用有限预算做停止和后续候选采用决策，计数矛盾属于该契约内的窄缺口。

最小修复是按已声明搜索引擎校验必需计数和预算类型、非负值、聚合/单引擎关系，NodeLimit 必须达到本次实际生效的节点上限；不要从字段名称推断 Coordinator 的聚合预算等同单 Beam 预算。保留合法 1000/1000 原样正控，拒绝 0/0、缺计数和矛盾配置的有限反例即可。TimeLimit 必须使用独立的实际耗时/预算证据，不能套用节点数断言；当前没有真实 TimeLimit 前缀覆盖。此项纯检查器修复重读现有原始证据即可，不需要重跑原生矩阵。

复现入口仅针对派生副本：

```powershell
dotnet .build/native-build/solver-journey-final02/bin/Release/net9.0/OfflineSearchHarness.dll --review-solver-prefix .build/runs/solver-journey-independent-review01/copies/pending-node-limit-zero-work
```

## 通用有限预算候选：可行，但尚未被本轮实证采用

固定上游源码提供直接依据，不需要自研搜索器。`CombatBeamSolver.Phases.cs:524` 将最终候选自己的边界与搜索停止原因分开：候选边界为 None 时，时间/节点耗尽可把结果的 BoundaryReason 标为 TimeLimit/NodeLimit；同文件后续重放并物化已选动作和 Snapshot。`SolverOverlaySnapshot.cs:627` 把这两种停止解释为“当前找到的最佳路线”；`SolverController.cs:2559` 先检查真实源状态未过期，再保留并部署当前回合计划，并不一概拒绝 NodeLimit。`UnattendedTestRunner.SurvivableBoundary.cs` 还有同节点预算下保留可存活前缀及重放的既有测试实现。这里只读检查了这些代码，没有运行上游游戏 UI 或该测试。

因此，“搜索未穷尽”与“当前第一动作可合法执行”应分开记录。当前 Lab 在 `NativeSolverGate.cs:61` 的严格拒绝是更保守的接入策略，不是上游要求。query321 已有 17 项候选动作，不能据此宣称候选安全：现有 query JSON 没有完整保留 SolverResult.Snapshot 的 HasRisk、PredictionGaps、内部 BoundaryReason、PlayerDead，或 Forecast 的不支持/近似标记，无法从旧 JSON 补出这些值。

建议下一批保留严格模式作可复现基线，增加显式的通用候选采用模式，先只开放 NodeLimit，并保持 beam8/1000节点/单 worker 不变，以验证规则本身。采用前要求：搜索任务正常返回、实际预算和结果来源可核对；结果、Snapshot 与 Forecast 的支持边界明确；无未补偿 gap/风险、未完成选择或过期状态；首动作属于当前回合和已支持字段，使用现有实例/目标/槽位/子选择解析与原生合法性检查。在独立 shadow 中执行这一步，确认不扰动父状态/RNG，然后真实执行一步、差分、重新求解。不能一次直接部署尚未独立验证的整条候选。

预算结果应继续记录 `searchBoundary=NodeLimit` 和实际采用/拒绝原因。若候选被合法采用，Run 可以继续；若没有候选、遇未知效果、硬取消、工作量越界或差分失败，按实际原因停止，保留前缀。不得把 NodeLimit 改写为 None；不得用 TimeLimit、MemoryNoProgress、PendingChoice、UnsupportedEffect 等未验证边界暗中放行。不要把 `IsExecutable=false` 一概视为非法，因为固定上游 EndTurn 本来就是该表示，仍应走已验证的 EndTurn 契约。

下一批建议最多 4 次原生执行、20 分钟、2 GiB、单进程 120 秒；先本地有限状态/负控及一对 live/replay，额外两次仅供受影响修复验证。记录第一处预算候选采用前后完整状态、上游结果风险信息、来源与独立 shadow 结果；首次语义不一致即停。对时间预算到达暂不采用候选，待有独立耗时证据后再增加。各门不自动扩大，节点目标为越过当前通用预算停点并记录最远合法节点，不针对 Slimes 或某个 Boss 调权重。完整 M1 同构建全量门仍保留。

## 保留的边界与交接

15 个锁定输入、52 个兼容源码、CombatSolver 826 个源码和 6 个程序集哈希及许可证一致；当前 producer 的实际源码、编译副本、程序集、工具链和输入指纹核对通过。原始执行和工作证据锁保持不变。MemorySaveStore、ShouldSave=false、Steam 未初始化、离线单人网络服务、无 UI/遥测/异步故障、保存与 replay 拦截均有本批记录。这些哈希证明一致性，执行真实性还依赖受控生产进程、实际原生命令链和重放证据。

当前 `solver-native-boss` 仅接第一幕 Boss、奖励及下一幕首决策的原生控制器，尚未真正到达这些后段；也还未把该 solver profile 扩到旧多幕主线。需要先关闭 R1、验证通用候选采用，再串接已有后续幕控制器与实际终态，不重新造奖励/事件/Boss 业务。

RandomForeseer 尚未执行预测。继续依照 [复用说明](../validation/RANDOM_FORESEER_REUSE_NOTE.md)，复用其单节点预测入口与原生生成比对，核对 live Player/RunState 和完整 RNG 不扰动；未知 hook 明确拒绝。部分 RNG/奖励状态复制和 AfterModifyingCardRewardOptions 多步限制仍禁止将其当作隔离多步精确 fork。它的预测接入应与原生执行区别记录，且不应阻塞已能执行的原版主线。

本轮证据脚本最初把原生奖励动作名称误写为 EventRewards，产生两项检查失败；实际记录为 RewardOffer，原输出保留在 `facts.json`，校正后的 `facts-final.json` 才是 173/173。没有因此重跑游戏或修改原始数据。除 R1 外，限定审查内未发现新的必修业务缺陷；没有权限或环境阻塞。
