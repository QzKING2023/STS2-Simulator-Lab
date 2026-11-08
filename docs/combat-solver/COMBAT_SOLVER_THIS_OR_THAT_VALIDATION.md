# ThisOrThat 原生接通与当前全流程进度

2026-11-04，第5步有界批次。**当前 CombatSolver 自然路径推进到第二幕第11层、`MapCoord (3, 10)`；M1未完成。** 原生第二幕累计三场胜利，本轮新增两场。此前其他策略的历史证据不受此范围说明影响。

## 本轮结果

| 内容 | 实证 |
| --- | --- |
| ThisOrThat | 原生Plain选项完成：HP90→84，金币252→313，MaxHP90不变；牌组、遗物和药水不变 |
| 原生任务 | `EventOption.Chosen`真实任务开始、完成后，才记录选项返回和动作确认，再进入下一节点 |
| RandomForeseer | 复用0.13.19既有遗物预测底层方法；未选Ornate的预览两次均为StrikeDummy，源状态、完整RNG及遗物袋不变 |
| 后续战斗 | 原生ExoskeletonsWeak、MytesNormal胜利和对应领奖；连同本幕此前ThievingHopperWeak，共三胜 |
| 后续节点 | 原生商店流程、两次休息升级、宝箱开取及合法地图推进 |
| 最远状态 | 217确认／217派发，HP/MaxHP91，第二幕SlumberingBeetleNormal战斗首回合，尚未采用该战首个求解动作 |
| 停止原因 | q705候选的静态预览含条件分支近似，现有保守准入门拒绝；保留Unsupported/exit2 |
| 同构建重放 | 217动作边界、159个已执行求解步、160个查询的源状态/首项候选及最终状态一致，复现相同停止 |

本轮没有调战斗评分、为单场选择特别动作或修改怪物逻辑。新停点不是实际死亡、预算耗尽或原生业务异常，未将预测路线中的Victory当作实际胜利。

## 原生入口与已审计范围

固定游戏v0.111.0/41cef1ea的ThisOrThat继承无操作`OnRoomEnter`。初始化中的原生`CalculateVars`已经消耗事件模型RNG，生成当前金币变量；实际提供两个选项：

- **Plain**：承受6点原生伤害，领取当前金币变量。
- **Ornate**：原生生成/取得遗物，加入Clumsy诅咒。

本批仅接通存活、无额外数值修正的Plain路径。调用仍通过既有`EventSynchronizer.ChooseLocalOption`、`AwaitPendingOptionTasks`和通用房间选择协议，业务效果由原生执行。当前61金币是已生成的原生变量，Lab没有重新roll它，也没有新写事件效果逻辑。

选择数据保留真实key、实例、当前请求/状态票据和前后完整状态；-1非法索引及未审计的Ornate在真实生产分支检查函数中拒绝，额外派发数为0。Ornate实际取得及诅咒路径未运行，不能说整个事件已支持。

Plain期间run、player、事件同步器、事件模型的完整RNG均不变；模型counter保持1。这与初始化已经roll过金币相符。任务异常与既有失败传播机制继续沿用，未增加吞错路径。

## RandomForeseer复用与边界

只读核对了同版本的`EventOptionPrediction`注册及`Events/ThisOrThatPrediction.cs`。上游针对**Ornate**调用`PredictRelicRewards(Player, 1)`并追加拾取提示；Plain返回空提示，并没有提供一个Plain结果预测器。

本轮复用其既有底层三个方法体：序列化完整Xoshiro状态的`Clone(Rng)`、两个`PredictRelicRewards(Player,int[,Rng])`重载。它们复制Rewards RNG和遗物袋，使用只读的live Player/RunState筛选当前节点候选；不构成整个Player/RunState的深拷贝。

- 当前未选Ornate的遗物身份预览为`RELIC.STRIKE_DUMMY`，重复一致；完整原状态/RNG与遗物袋序列化内容均保持。
- 预览明确标为`CounterfactualNotExecuted`，**没有原生Ornate比对**，也没有执行`RelicTipsWithPickup`及后续拾取预测。
- Plain仅记录已显示的原生变量和实际结果；不把它归功为RF预测通过。
- RF Mod DLL、入口、UI及遥测没有加载；策略并未根据这个未验证预览改选Ornate。
- 预览的所有者是已确认Travel169后的稳定边界；它的完整状态同时绑定169确认快照和170派发前快照。预览不是170执行结果。事件效果报告属于实际RoomOption170。

新快照`.build/pinned-inputs/randomforeseer-0.13.19-native03-this-or-that/`保留旧7输入，另锁定上述事件注册和预测源文件，共9项。旧7文件及旧锁不变，MIT归属保留。版本仍为0.13.19，无整包新初始化或多节点精确预测结论。

## 当前停止门及方向风险

q705真实返回：`boundary=None`、`hasRisk=false`、无unmirrored、`forecastUnsupported=false`，但静态预览`forecastExact=false`，列出：

- `BOWLBUG_ROCK.POST_HEADBUTT:条件分支`
- `SLUMBERING_BEETLE.SNORE_NEXT:条件分支`

当前准入规则拒绝此候选，未产生q705影子执行或原生命令。完整源状态等于最后确认的Travel217状态。生产准入API对真实q660返回0、对q705返回1，验证保守门保持。

源码核验发现，上游`BranchMonsterAi.ResolveConditional`**已经有这两个条件的分支处理**；静态`IntentForecaster`仍统一将条件分支标为近似。两种机制的关系需要核验。它不是“缺少卡牌OnPlay”，也不能仅凭存在分支处理就把候选标为精确。本轮没有删除`forecastExact`限制、添加怪物特判或另写AI。

下一高价值工作应是复用上游接口确认静态预览标记与实际分支执行覆盖的关系，保留真正未建模/差分失败停止。逐怪重写或继续单场评分微调会偏离原版全流程目标。具体交接见[下一步](COMBAT_SOLVER_THIS_OR_THAT_NEXT_HANDOFF.md)。

## 数据采集范围

本批验证当前选项、原生任务完成、资源、实例列表、完整RNG、动作边界与下一房到达。既有`MajorTravel`直接等待原生`EnterMapCoord`完成，其路径负责原生退出/进入；**本控制器没有单独发出另一条4B驱动的`room-exit-start/return`记录**。本次没有把这些未采集阶段记为通过。M1最终数据门需要统一复用已有退出阶段采集，避免再按每个节点增加一套旁证。

## 冻结、复现与资源

producer：`2DDA71BA23328126335E620304CF091E1FD07417F094DF8089C5F20822C38422`

- `.build/native-build/solver-this-or-that-final01/build-manifest.json`：4个既有warning、0error；游戏、采集源码、编译快照、产物与工具链指纹锁定。
- `.build/runs/solver-this-or-that-work01/plan.json`、`ledger.json`、`facts.json`、`closeout.json`。
- `.build/runs/solver-this-or-that-exec-01/`、`exec-02/`：同一producer的live与冻结重放。
- 关键文件：`foreseer-this-or-that.json`、`this-or-that-plain.json`、`solver-query-705.json`、`solver-admission-705.json`、`trace.jsonl`、`result.json`。
- work目录`collect-facts.py`重读冻结记录；59项证据断言成立，不代表将Unsupported run认作完整通关。

本批使用2/4次原生执行，实验窗口441.68秒；单次约49.13/50.50秒，均小于120秒。保守存储计量约753 MiB，小于2 GiB；两次均真实exit2，未机械全量重跑或替换失败样本。剩余名额已关闭。

当前Solver仍为原锁定`terminal-hooks.1`，原826源/6 DLL、派生826源/6 DLL均未更改；15Mod输入、52兼容源码与此前3613份原始执行文件保持。原168确认/169派发的This入口Unsupported及更旧DataError完整保留。MemorySaveStore、保存/replay拦截、无UI/真实Steam/遥测边界保持，无提交推送。
