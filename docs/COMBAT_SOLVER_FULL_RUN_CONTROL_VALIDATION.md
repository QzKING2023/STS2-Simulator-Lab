# 整局控制、退出采集与 Symbiote 续行（2026-11-04）

## 结论与范围

M1 **尚未完成**。本轮真实原生续行至第二幕第16层，268确认/268派发；同一producer完整重放复现。13次战斗胜利，其中第二幕5次；200个实际战斗动作的完整状态、continuation与RNG差分一致。第201次查询返回敌方回合选择，桥在原生根动作派发前明确停止，`Unsupported/exit2/DataComplete=false`。没有第二幕Boss胜利、第三幕或RunVictory的新证据。

还有一项本轮自查未关闭的验收：真实producer漏接逐幕入口观察调用。修正已落入后续构建，但本批3次原生执行额度用完，**修正后逐幕生成链未做原生复验**，不得称整局控制已验收。生产检查器现明确拒绝该缺失，原始证据不补写。

## 构建及原始证据

- 实际producer：`3FC4D916E770F455A364B2AF42AD4DC68F328E3E04D8EF2296E30076CB00B6B5`，`.build/native-build/solver-full-run-final04/`。
- 实际执行/同构建重放：`.build/runs/solver-full-run-exec-02/`、`exec-03/`。
- 当前修正构建：`27D5AB735A3E72D0BE6916D1BA889084C7ED3B5DFF5D979D1FC044E56AC43FDD`，见 `.build/native-build/solver-full-run-final06/build-manifest.json`；它与actual producer的差异仅 `NativeNaturalBoss.cs` 的遗漏调用补接及 `NativeFullRunContract.cs` 的必需阶段与完整状态绑定。不能将此构建冒称原生执行producer。
- 总证据入口：`.build/runs/solver-full-run-work01/closeout.json`、`facts.json`、`execution-lock.json`。
- 初次烟测 `exec-01`：29确认/30派发，旧前缀哈希不一致；新增退出facts抢先登记房间导致EventModel/room ID次序交换。修复为先按原State投影次序登记。失败保留，最终pair重新验证原110步前缀。

## 复用与实施

1. `NativeFullRun` 组合已有首幕、第二幕、第三幕与Architect/WinRun原生任务；单独记录每幕进入、Boss/奖励完成。终态仍须真实原生authority。当前只完成代码组合；后半程尚未到达，缺入口调用的修正仍待原生复验。
2. 全局上限在**每个根动作派发前、每次求解开始前**检查，不再只在查询时检查128步尾部。当前声明1200根动作、768查询、110秒协作墙钟、256MiB trace；保留每战64查询、原查询取消及120秒外部进程树watchdog。完整尝试/完成查询分别保留；这些是不同的停止门。
3. `MajorTravel` 在同一次 `EnterMapCoord` 前后采集退出开始、原生调用返回、入口准备完毕与确认/到达。本pair各31次，生产完整状态/动作/实例/顺序契约及23项控制验证。**这是组合迁移调用的边界，不宣称单独测得Exit内部每个阶段**；没有重复调用Exit或EnterMapCoord。
4. Symbiote使用真实 `EventOption`→`FromDeckForTransformation`→`CardCmd.TransformToRandom`，原生候选过滤、RNG、Hook和替换全部保留。本次实际移除Strike实例i1，生成TrueGrit实例i574；选项与牌实例、序列化内容、前后完整状态在`symbiote-transform.json`。Approach与空候选未覆盖。
5. 已检查RandomForeseer 0.13.19现有 `SymbiotePrediction` 与 `PredictDistinctDeckTransformResults`。所需转换预测辅助方法未纳入当前固定源码子集；本轮**未执行此预测**，没有另建预测器，也没有将原生成功等同预测通过。

## 上游搜索预算/并行核验

核对固定CombatSolver0.50.1 `Runtime/SolverSettings.cs`、`Search/SolverSearchProfile.cs`、`Search/SolverWeights.cs`：

| 内置档位 | 软时间 | beam | 主搜索节点上限 |
|---|---:|---:|---:|
| Low | 60s | 45 | 60000 |
| Medium | 120s | 60 | 120000 |
| High | 180s | 90 | 250000 |
| VeryHigh | 300s | 135 | 500000 |

支持自定义与`SearchMaxDegreeOfParallelism`，最大16；自动值在逻辑CPU>=16时8、>=4时4、>=2时2，否则1。已核实存在，**未在Lab新验证多线程或四档效果**。这些控制一次搜索的深度/成本，不能替代整局动作/查询预算、逐幕控制和退出采集。当前实际停点是动作协议拒绝，并非搜索时间耗尽，因此本轮保留已验证的custom（beam8/nodes256/soft10s/dop1）及上游Coordinator，不调评分，不以提高档位绕过准入门。以后确需改档/并行，应复用上游设置并独立登记成本、隔离/RNG和结果验证。

## 停止点与下一依赖

最后确认`PlayCard:268`；实际Act index1、`MapCoord (3, 15)`、floor16、HP95/95、gold78。q836返回 `EndTurn`，`TurnStartChoices`包含`ApplyKnowledgeCurse / EnemyTurn / KNOWLEDGE_DEMON:1:0 / MIND_ROT`，且`IsExecutable=false`。`NativeSolverActionContract.Fields`按原协议拒绝；没有派发a269，不是死亡、胜利或预算耗尽。

优先复用上游 `KnowledgeDemonChoiceSupport`、`CombatBeamSolver.ExecutionChoiceContinuation`、`NativeChoiceRuntime` 与 `SolverController` 的EndTurn计划。后两者UI driver要求`NGame`，不能直接启用；需复用其纯选择匹配/消费契约并接已有Lab无UI选择桥，而不是忽略字段或写怪物特判。具体下一步见[交接](COMBAT_SOLVER_FULL_RUN_CONTROL_NEXT_HANDOFF.md)。

## 验证边界

- 3次实际原生执行，首次失败与最终pair全部保留；最终pair原始exit均2。事实检查器通过只表示其声明的部分证据成立，不将exit2包装为全通过。
- 23项迁移生产逻辑控制、3项全局预算边界、逐幕API控制为定向检查；逐幕API使用明确构造夹具，不冒充实际发出的逐幕事件。当前checker拒绝原生pair缺失的act观察，这是预期未认证状态。
- 15个Mod输入、52个兼容源码、固定Solver/RF与旧原始证据保留；具体哈希核对见`facts.json`。未改策略评分、角色、安装、真实存档；无UI、真实Steam/网络、提交推送。
- 原生宿主MemorySaveStore/SaveProgress/replay/telemetry隔离继续通过。预测仍为Oracle；有近似标签的未来预测未升级为精确。
- 最终M1同构建完整验收仍待完整RunVictory及可信数据闭环，不由本轮部分路径或API夹具代替。
