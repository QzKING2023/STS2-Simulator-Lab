# CombatSolver 第4步：原生奖励、地图与下一战

2026-11-04。**本轮第3步R1定向修复通过，第4步两战纵向流程实现及定向验证通过，待综合独立验收。M1尚未完成。** 沿用固定游戏v0.111.0/41cef1ea、原版Ironclad/A0、独立离线档案和固定CombatSolver 0.50.1 Lab衍生版；没有单Boss评分改动。

## 真实自然流程

四份原始目录均在 `.build/runs/`：

| 目录 | 来源与策略 | 实际执行结果 |
| --- | --- | --- |
| `solver-run-exec-01` | 正常Neow开局，live；原生金币，跳过卡及剩余奖励 | 同Run首战Victory→原生奖励→合法地图移动→第二战Victory；23次solver动作，29/29总根动作确认 |
| `solver-run-exec-02` | 相同输入、相同producer，完整冻结重放01 | 两场Victory、动作/选择/状态/RNG一致 |
| `solver-run-exec-03` | 正常Neow开局，live；原生金币，领取首张原生卡候选，跳过其他剩余奖励 | 同Run两场Victory；25次solver动作，31/31总根动作确认；实际取得Rage，下一战在线求解器实际打出对应副本 |
| `solver-run-exec-04` | 相同输入、相同producer，完整冻结重放03 | 两场Victory、动作/选择/状态/RNG一致 |

种子均为 `LAB_V0111_NATIVE_DEV_01`。两条路径的首战均为13次solver动作；第二战分别10和12次。没有FixtureSetup、调HP/能量、人工加牌、摆首手、强制solver动作或更换失败样本。金币由原生GoldReward领取17（249→266）；take路径新增未升级 `CARD.RAGE` 永久实例i37，下一战战斗副本的DeckVersion指回该实例，query71真实PlayCard。最终两路径HP80、金币266，永久牌组分别10/11张。

原生首战清理后HP79，奖励及进下一战保持HP79、金币、完整已定义永久牌组/升级/实例引用、遗物及药水。下一战由原生初始化到turn1、energy3、block0、Play、玩家powers为空。第二战胜利的BurningBlood原生恢复与历史清理另行观测，未算作独立模拟的奖励/战后效果。

两组完整重放使用原route、choices和reward-choices，无手工改写。全部step文件、query的原始PlanAction、binding、continuation、完整已定义nativeState（含run/player/event RNG）、有效Profile相同。每一步实际执行后核对并重新求解；不是仅执行搜索器预报的整条路线。所有战斗局部continuation相等，包含九条战斗RNG；顶层仍为Ongoing/SliceBoundary/NotCompared，未声称奖励有独立仿真或整局RunVictory。

## 复用与生命周期

`NativeSolverRun.cs` 是有界的两战控制器，复用 `SolverWholeBattle`、原生 `RewardsCmd.OfferForRoomEnd`、`NativeRewards.Collect`/RewardsSetSynchronizer、`ProceedFromTerminalRewardsScreen` 和合法 `TravelC`。奖励策略只选择原生候选；skip指领取金币后跳过卡及其余奖励，take指领取金币和首张卡后跳过其他奖励，不是“全部奖励都跳过”。地图采用既有原生首个合法排序子节点。

每场最多64次求解/动作，整条路径最多160个根动作。第一场结束该场求解，保留Run可继续；实验Closed只在最终战斗后启用，原终态关闭门继续拒绝新根动作。旧单战场景保持默认关闭行为。第二场重新捕获实际新CombatState，query65起使用独立文件编号，避免覆盖首战1–13。

前一场最后查询的真正未消费票据，在第二战实际CombatState上被拒绝，Used仍为false、零额外派发且完整状态未变；序号15→19。查询和shadow fork继续核验父状态与原生RNG不变。死亡仍终止独立实验，不新增同进程死亡worker复用能力。

Travel记录的root/context属于派发动作的来源；其confirmed完整state包含真实到达位置。下一战静止观察承接这个确认所有者，随后战斗动作使用当前实际房间的上下文。检查器以真实Travel.to、到达state、新CombatState检查和实际战斗动作关联识别新战，不能把来源context误当目的位置。

`NativeSolverBattleContract.ReviewBattle` 复用于每场明确片段，保留完整状态、实际动作/目标/实例、选择、连续查询及终态回调绑定；第一场允许继续实验，最后一场只允许声明的Closed false→true。`NativeSolverRunContract` 验证两场次序、原生奖励动作、实际金币、卡实例与下一战副本、地图坐标、资源继承、跨战票据和全部查询文件。共享 `NativeRewardContract` 仍检查选择/父子任务必需唯一、顺序、真实取得对象与静止完整状态；仅新scenario的最终n3c-contract允许已由战斗契约认证的关闭位变化，其他字段继续完整比较。

## 分层验收与冻结

第3步R1修复产物：`AD0506AE6E0E76F47DC5A7915B4D0C6C1229720A6E18B6F19601561B1E33B0E0`，`.build/native-build/solver-battle-repair01/`。153事实及42次生产reader均通过，原四反例拒绝；新增原生执行0。详见 [修复报告](COMBAT_SOLVER_WHOLE_BATTLE_REVIEW_REPAIRS.md)。

本批四次游戏执行使用同一producer：

`6D57D5954F46BC3E728D43B66C7C660D16C4A54D2F0D1F72CE3557C2DECDA984`，`.build/native-build/solver-run-final01/`。

最终生产reader：

`FE6232717EFE8C7F5E0B2B41DBE5C5FEEF94CE2623C5BA7FA534CAF25A97365F`，`.build/native-build/solver-run-reader03/`。

四次游戏退出均0。收尾的初次reader错误拒绝有效记录，原因是遗漏声明的关闭位变化；reader02又将Travel来源context误当到达context。两次错误保存在 `first-reader/result.json` 和 `second-reader/result.json`，没有覆盖或计成成功。只修正 `NativeRewardContract.cs` 与 `NativeSolverRunContract.cs` 后重读冻结原始证据，运行/采集编译源完全相同，没有第五次游戏执行。不能声称四次游戏由reader03产生，最终M1同构建全量门仍待完成。

完整证据目录 `.build/runs/solver-run-work01/`：

- `facts.json`：168项事实核对全部通过，包括真实流程、Rage取得与实际使用、资源初始化、父fork/原生来源、状态/RNG、两组完整重放、15输入/52兼容源码、solver826源/6DLL以及原始证据锁。
- `controls.json`：最终生产reader的19项本批正负控全部符合预期（5正常/等价接受、14拒绝）。覆盖关键记录缺失、跨战票据标志、取得卡错指旧牌、完整RNG/资源矛盾、错误关闭位、缺卡子会话、地图目标、下一战目标及缺step。
- `r1-regression/controls.json`：42项单战及R1控制在本批最终reader上仍通过（8接受、34拒绝），四原反例没有回归。
- `legacy-reward/controls.json`：两份旧原版奖励take/skip正常证据接受，两个旧场景静止快照损坏副本拒绝。只重验副本，未重跑旧原生矩阵。
- 最终reader合计65次调用：15正常/等价接受，50损坏拒绝，零意外。检查数量不等于65场游戏或65种牌效。
- `plan.json`、`ledger.json`、`execution-facts.json`、`execution-lock.json`、`producer-reader-delta.json`、`build-ids.json` 与 `closeout.json` 保存来源、预算、版本及收尾；哈希仅证明一致性，执行另有原始process/trace/route支持。

预登记上限4次原生执行、20分钟、2GiB、每进程120秒。实际四次进程累计40.84秒，03:53:54 UTC全部结束，早于04:12:05截止；次数已封顶，无失败后追加。空间的最终保守测量见closeout，未达到2GiB。构建保持既有4warning/0error；输入、solver许可/上游归属与52兼容源码不变。

本批每次SaveProgress/replay拦截为6/2；MemorySaveStore、ShouldSave=false、Steam未初始化、单机服务、无UI、零遥测/异步故障、无MyMod/BaseLib均保留。未改角色/游戏安装/真实存档或其他项目，未提交推送。

## 覆盖限制与下一步

这是单种子、两种预登记奖励决策、两场连续战斗。没有测第二场奖励、第三节点以后、Boss/整局RunVictory、多个种子的可靠性/胜率；Oracle策略也不等于玩家可见信息模式。未增加全部卡牌/子选择/药水/复活认证，Unsupported与BudgetExceeded仍须保留，不能用合法动作fallback掩盖未支持语义。

死亡仍沿用第3步原生夹具及重放，没有新增自然死亡样本。第3步原始producer、四份原证据以及失败控制保持；没有以本批两场Victory取代这些边界。

第4步已实现并定向验证的入口是“同Run战斗求解→原生奖励→原生地图→下一场求解至终态”，待独立验收。后续优先把这一战斗策略单元接回已有原版多节点/跨幕控制器，复用其事件、商店、休息、Boss与终局生命周期；明确每次新遇内容的支持/失败分类及预算，逐步达到真实RunVictory和最终同构建全量验收。保持通用策略，避免恢复针对单场Boss的评分微调。公开Mod接口继续后置。

用户补充的RandomForeseer复用方向已做本机源码及许可初查，见 [复用入口说明](RANDOM_FORESEER_REUSE_NOTE.md)。当前未新增战斗外预测或原生实验；该源码支持范围、UI耦合和已知Hook缺口须分别验证，不能把manifest版本匹配当兼容通过。
