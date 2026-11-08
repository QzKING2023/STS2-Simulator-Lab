# CombatSolver 第 3 步：整场在线战斗与真实终态

2026-11-04。**本轮实现及定向验证完成，待独立验收。** 正常 Ironclad 首战由求解器在线逐步执行至原生 Victory；单独 API 夹具由求解器实际选择 EndTurn 后原生死亡。两条路径各自同构建完整重放通过。没有 Boss、RunVictory、胜率或 M1 完成声明。

## 实际结果

| 原始目录（`.build/runs/`） | 来源/方式 | 原生实际结果 |
| --- | --- | --- |
| `solver-battle-exec-01` | 正常 Neow 开局、原生旅行至首战，live | 13次求解/13个求解器动作，含敌方回合、抽牌和再次求解；16/16总根动作确认，第4玩家回合真实胜利 |
| `solver-battle-exec-02` | 同输入、同producer，frozen replay | 同一实际胜利；完整route/choices/final-state、13个step及查询状态/计划/RNG一致 |
| `solver-battle-exec-03` | 明确API夹具，live | 正常首战对象；原生把手牌移入Exhaust并设玩家HP1；求解器真实选择EndTurn，敌方行动造成实际死亡；1次求解、5/5总根动作确认 |
| `solver-battle-exec-04` | 同一死亡夹具、同producer，frozen replay | 相同真实Death、完整状态/RNG和失败生命周期一致 |

四次原生退出均0；其中Death是已正确完成的实验结果。自然胜利的运行结果为 `Ongoing / SliceBoundary / NotCompared`，死亡为 `Death / Completed / NotCompared`。每步报告提供精确战斗差分；顶层 `NotCompared` 保留原生整条运行协议含义，没有伪称清场也由模拟器预测。

胜利清理前HP73，敌人HP0；实际原生燃烧之血在清理后回复至79，Phase变None、战斗历史清空。死亡HP0，原生ProcessPendingLoss确认完成；该路径没有调用AfterCombatEnd或零售RunLoss收尾，仍保留PlayerCombatState及8条历史。死亡用例每条独立进程，**没有认证死亡后的同进程worker复用**。死亡夹具不构成正常路线策略表现或自然死亡样本。

## 复用与实现

复用固定CombatSolver 0.50.1 Lab衍生版本的 `CheckWinCondition` / `CombatTerminalStamp`、原有捕获与fork、既有 `AdvanceRound` 和逐步执行桥。没有新搜索算法、强制求解动作或Boss评分改动。

`NativeSolverBattle.cs` 增加有限整场控制器：最多64次查询/动作；直接调用既有求解器，再执行其未修改的第一动作，每次重新捕获求解。沿用beam8、1000节点、5秒soft查询、10秒合作取消、DOP1和120秒进程硬门。NodeLimit/TimeLimit不采用候选，Unsupported/非法动作/异常/数据差分有原出口；没有候选时不会改用贪心动作装作求解成功。

共享执行器增加默认关闭的 `allowTerminal`。普通旧片段仍保留非终态门；新控制器允许取得真实terminal stamp。EndTurn仍要求原内部AdvanceRound返回None，只有非终态才继续普通稳定边界settle；不会把终态强行推进下一玩家回合。

原生端复用已审过的 `EndCombatInternal` prefix和 `ProcessPendingLoss` prefix，在原生清理前读取完整 `ContinuationStamp.CaptureLive` 和状态；没有新增被patch的游戏方法，没有替换业务逻辑。捕获点绑定当前query、实际root/context和真实玩家/敌人终态事实。预测端的完整continuation与此点比较，父fork和原生来源继续核验。

动作确认后单独记录原生清理状态、计数和历史；不把清理后HP79与预测战斗HP73直接比较，也不删除字段来使两者相等。终态后不再捕获普通行动root/重新求解；关闭动作门并实际调用拒绝控制，证明零额外派发。该Closed是实验控制器边界，不等于原生整局RunVictory。

新流程仍支持上一阶段的有限动作集。NestedChoices、TurnStartChoices、未知来源/效果等继续明确拒绝；没有扩大为所有牌、药水、复活、召唤物或多阶段Boss认证。

## 版本、生成链与分层验收

| 角色 | 固定构建 |
| --- | --- |
| 四次原生执行的producer | `AAACB551E949A74ED8DBA202B8C91DB71AA8CB3535C6F0A48A811570BA0CA17A`，`.build/native-build/solver-battle-final01/` |
| 最终生产检查器 | `06CDFFD5E34C8A9176ED9B12B2DEB4D64A8C0DED8A553675994162DC1D8D00FD`，`.build/native-build/solver-battle-reader02/` |

四次游戏执行全部属于同一producer。收尾时补强检查器的opening完整确认状态/来源/预算绑定，编译副本相较producer**只有NativeSolverBattleContract.cs变化**；游戏执行/采集器源码完全相同。按分层原则重读全部四份冻结原始证据，不重跑四个原生进程、不覆盖旧buildId。最终M1同构建全量门仍保留；这里不称四次执行来自reader02。

`NativeSolverBattleContract` 在实际生产 `--review` 入口检查：完整查询/阴影/派发/确认集合，未修改首动作及实际牌/目标/药水绑定；每步parent/票据/完整continuation；最后一步唯一捕获/验证/清理/结束，当前动作与上下文、完整快照及终态事实；清理后仅允许声明的Closed false→true；具体结果分类、文件集合和来源。通用trace检查器仍检查历史、选择、任务及封签。

独立产物目录：`.build/runs/solver-battle-work01/`。

- `plan.json`、`ledger.json`：预先登记4次、20分钟、2GiB，进程120秒；本轮4次已封口，实际进程累计约13.57秒。没有失败后换种子或追加原生额度。
- `verification.json`：100项事实核验全部通过，包含四条原始执行、两对完整重放、独立6点燃烧之血恢复、实际死亡、清理和RNG、来源/输入/构建及原始证据锁。
- `controls.json`：最终生产reader共55次调用，13正常/等价接受、42受损或未完成记录拒绝，全部符合预期；含原第2步34控制和本轮终态缺失/重复/错归属/continuation、清理完整RNG/计数、opening漂移、虚假汇总、缺step、伪存活/伪胜利及旧query锚点等。
- `build-ids.json`：两个manifest按实际算法重算一致；`runtime-to-reader-compiled-diff.json` 明确唯一reader变化，原producer编译副本及产物均按原锁核验，当前源按最终reader锁核验。
- `execution-facts.json`、`execution-lock.json`、`work-lock.json`、`closeout.json`：执行来源、预算、最终结论和可核验文件锁。

两构建均4 warning/0 error（既有Publicizer类型冲突及三个有前提的owner nullable）。15锁定输入、52兼容源码、固定solver826源/6DLL、游戏v0.111.0/41cef1ea及MIT归属保持。旧在线/药水原始证据未改，旧预算和InvalidAction仍按原producer/退出保留；没有把检查器拒绝这些未完成记录说成新失败场景实跑。

自然胜利的SaveProgress/replay拦截为4/1，死亡为3/0；它们是被拦截的原生调用，不是持久化写入。四条均MemorySaveStore、ShouldSave=false、Steam未初始化、无UI、单机服务、零遥测/异步故障、未加载MyMod/BaseLib。宿主补丁清单和root审计保持，默认原生配置的拒绝没有删除。本轮未重新执行默认配置全量矩阵，限于影响范围重验旧证据。

## 复现和下一步

四次原始命令在ledger、wrapper日志和process中保留。重放输入分别是exec01和exec03的原 `route.json`/`choices.json`，不是手工生成路线。现有预算已用完，直接重复原wrapper会被次数门拒绝；新实验应登记独立额度、使用新输出目录并保持来源。

本轮正常开局完整首战胜利成立，尚未执行胜利奖励或下一节点，也未接回第三幕Boss。通用header中的旧scope文字仍为兼容描述；本轮实际范围以独立scenario来源、solver-battle-opening/completion及本报告为准，不将泛用new-run开局策略标签误当后续战斗求解器策略。

下一阶段先独立审查本轮终态业务和有限契约，再将整场控制器接回现有原生run控制器的战斗策略入口：奖励/地图/事件保持原生控制，支持范围外显式停止；先验证多战连续调用时关闭/重启战斗门、票据失效和资源生命周期，再做有预算的自然路径。新的牌/选择能力只在实际需要时补最小可验证范围。完整M1最终全量验收仍待完成，不恢复单场Boss评分微调。

本轮变更仅Lab：新增2个战斗控制/检查器文件及8个既有入口/共享执行/观察契约文件，另有本文和README。没有角色/安装/真实存档修改，无游戏UI/真实Steam网络，无提交推送。无权限或环境阻塞。
