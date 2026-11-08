# CombatSolver 第 3 步整场战斗独立验收

2026-11-04。**真实整场业务与重放成立；第 3 步验收暂未通过，剩一项生产检查器必修 R1。** 四份原始证据没有发现终态矛盾。独立有限反例显示，新的 solver 终态快照尚未绑定紧邻的原生胜利/死亡回调，损坏副本仍可被认证。修复属于检查器层，不需要因此重复全部原生执行。

本轮只读实现和原始证据，新增独立审查脚本、派生副本和本报告。新增原生游戏执行 **0**，未编译、未修改生产实现或原始证据、未提交推送，无权限或环境阻塞。

## 独立确认的业务范围

| 原始目录（`.build/runs/`） | 实际结果 | 声明边界 |
| --- | --- | --- |
| `solver-battle-exec-01/02` | 正常 Neow 开局，同 Run 首战；13 次实际求解/动作，16 个总根动作，3 次 EndTurn，回合4原生 Victory；live 与完整 replay 一致 | 单敌人 Nibbit、Oracle、原版 Ironclad；不是 Boss 或完整 RunVictory |
| `solver-battle-exec-03/04` | 明确的原生 API 死亡夹具；手牌移至 Exhaust、HP1；求解器主动选择 EndTurn，敌方实际行动，原生 ProcessPendingLoss 完成；1次求解、5个总根动作；live/replay 一致 | 死亡结果真实，但 HP1/空手牌不是自然策略形成的状态 |

每次查询的完整已定义状态、原始首个 PlanAction、实际牌/目标映射、部署后 continuation、父 fork 与原生来源不变、未消费旧票据拒绝均可追溯。两组 replay 的 route、choices、final-state、全部 step 文件以及 query 的 nativeState/binding/actions/Profile/continuation 相等。查询与派发逐步交替，未把首个预测路线当成已执行整场。

`NativeSolverLoop.cs` 复用原 shadow 执行和 EndTurn 的 AdvanceRound，新增允许终态的窄开关；非终态才 settle，随后 CheckWinCondition/TerminalStamp 与真实回调比较。`NativeSolverBattle.cs` 在既有 EndCombatInternal / ProcessPendingLoss prefix 中，原生业务清理前捕获状态及 continuation，没有新造 Victory 或替换业务效果。每次实际动作后的结果均与 shadow 比较，终态不再查询普通行动 root。

这里的“完整”限项目定义的快照和 continuation：后者包括九条战斗 RNG；原生快照另有 run/player/event RNG。没有据此声称全部游戏私有字段、所有效果或所有 RNG 流均被仿真。

胜利前玩家 HP73、敌人 HP0；原生战后 BurningBlood 恢复6至79，战斗 Phase=None，历史清空。模拟差分使用 HP73 的清理前终态；HP79 属于另外观测的原生清理，未冒称战后独立仿真。死亡 HP0、Losses=1；该原生路径保留 PlayerCombatState 和8条历史，没有 AfterCombatEnd 或零售 RunLoss 收尾承诺。死亡实验各在独立进程，同进程 worker 复用仍未认证。

四个原始退出均为0；胜利顶层为 Ongoing/SliceBoundary，死亡为 Death/Completed，顶层 Comparison 保持 NotCompared。战斗局部的 exact continuation 与这些顶层语义分开，未把战斗 Victory 写成整局胜利。终态后确实尝试通过原动作门派发 EndTurn，Closed 拒绝且没有新增 dispatch。未执行奖励或下一场。

## R1：终态采集与原生回调缺少完整边界绑定

位置：`tools/native-run/NativeSolverBattleContract.cs:42` 至终态事实判断。当前绑定 solver-terminal-captured 的嵌套 State、solver-terminal-verified.preCleanup 和 continuation，原生 battle-victory-before-cleanup / battle-loss-start / battle-loss-confirmed 则主要检查数量，没有将对应回调的完整状态、当前 root/context 与采集点连成同一边界。通用 NativeTraceContract 对此新 scenario 未启用旧 NativeTerminalContract，不能依赖旧分支补上该约束。

独立证据目录：`.build/runs/solver-battle-independent-review01/`。以下四份明确标记的派生副本，由最终生产 `--review` 实际读取，均错误 exit0；其原生回调、原始 continuation、实际确认记录保持原内容：

| 副本名（位于 `copies/`） | 有限变更 | 被遗漏的矛盾 |
| --- | --- | --- |
| `ind-terminal-hp-versus-native-callback` | 仅将 captured 三处重复投影的 HP73 改为74 | 原生胜利回调和 continuation 仍为73 |
| `ind-terminal-RNG-versus-native-callback` | 同三处 runRng 新增 `unexpected-stream` | 原生回调和实际 continuation 没有该状态 |
| `ind-native-callback-wrong-root` | 将胜利回调 root 从 a16 改为 a15 | 回调脱离造成终态的实际最后动作 |
| `ind-death-precleanup-gold-drift` | 死亡 captured/verified 的 gold 改为999 | 原生 LossBefore 及实际动作仍为249 |

三处重复投影指 captured.detail.state、captured.detail.facts.State、verified.detail.facts.preCleanup。没有协同重写整个执行、源码或所有观察来构造不可判定的伪造；这里是同一同步回调内直接可比较的完整快照漏绑定。四份真实原始记录中，该完整状态和 root/context 恰好一致，且采集点紧邻原生回调，所以不需要复制游戏业务语义修复。

最小修复要求：

1. 将最终 solver terminal 与本次相应原生回调绑定：相同实际 root/context、必需且唯一、正确顺序，处于最后 dispatch 与 confirmed 之间。Victory 对应 battle-victory-before-cleanup；Defeat 对应 battle-loss-start，并要求对应 loss-confirmed 在后、确认动作在后。
2. 对同步、静止的 captured→回调边界使用完整结构快照比较，覆盖新增字段；复用已有结构比较能力。不要对胜利清理前 HP73 与清理后79强行相等，也不要禁止 LossAfter 原生增加 Losses。后者是另一业务阶段。
3. 重验本轮四反例、四份原始正常副本、既有17项终态控制和有限合法等价/时序控制。若仅修改 reader，重新冻结检查器并重读这四份原始证据即可；保留原 producer ID。只有修改真实采集/调用链时才按影响范围追加原生烟测。

此次审查在一个可复用根因定位后停止扩展反例。无需把所有私有字段、复活、复杂 Boss 或全部卡牌纳入当前修复。

## 冻结、预算与检查规模

四次游戏执行 producer：`AAACB551E949A74ED8DBA202B8C91DB71AA8CB3535C6F0A48A811570BA0CA17A`，目录 `.build/native-build/solver-battle-final01/`。

最终 reader：`06CDFFD5E34C8A9176ED9B12B2DEB4D64A8C0DED8A553675994162DC1D8D00FD`，目录 `.build/native-build/solver-battle-reader02/`。

两个 build ID 按实际 manifest 算法独立重算一致。producer 的 compiled/input/artifacts 保持；当前 source、compiled/input/artifacts 对应 reader。两个构建编译源差异只有 NativeSolverBattleContract.cs 的两行 opening 绑定检查，原生执行/采集源完全相同。因此本轮可以复用同一 producer 的原始证据；不能说四次游戏是 reader02 产生，也不能据此代替最终 M1 同构建全量门。

独立 `facts.json` **153项事实检查通过**，包括原始执行、两组重放、完整状态/RNG、隔离、源/产物、15个输入、52个兼容源码、solver 826源文件/6程序集及原始/work锁。依赖版本、MIT和上游归属未变。原始文件在审查前后均保持哈希；哈希只是同一性依据，执行事实另依赖进程、实际调用链、历史与重放。

`controls.json` **30次生产 reader 调用**：四份正常原始副本、既有17项终态控制、9项有限独立控制。6个正常/等价副本接受，20个损坏副本拒绝，**4个上述坏副本误接受**。既有17项结果全符合预期，shadow 差分、旧票据标志、下一查询 RNG 和终态错序新控制也被拒绝；没有用整体检查脚本完成代替逐项结果。

原生 ledger 在首次执行前注册，上限4次/20分钟/2GiB、单进程120秒、整场64动作/查询。四次均完成，累计实际进程13.57秒，最后执行03:28:10 UTC，截止03:47:21 UTC；记录的保守空间约659MB，在2GiB内。没有新增第五次原生运行。本轮只运行 reader，未验证新的64步耗尽或超时终止；旧预算/失败证据仍保留，不能因本轮成功运行很快而新增取消保证。

Unsupported、无候选、非法动作、差分错误和 BudgetExceeded 的运行出口仍不同，无候选不回退贪心动作，NodeLimit/TimeLimit候选拒绝采用。本批没有真实新 Unsupported/BudgetExceeded，当前认证不增加这些场景覆盖。子选择、药水的第2步支持边界保持，不能把本批13次普通牌/EndTurn写成全部动作整场覆盖。

MemorySaveStore、ShouldSave=false、Steam未初始化、单机服务、无UI、零遥测/异步故障均在原始记录中；SaveProgress/replay拦截为胜利4/1、死亡3/0，是被拦截调用数。没有加载 MyMod/BaseLib，未调用 Entry.Initialize、设置加载保存或额外搜索补丁。默认宿主隔离未修改，且本次没有重跑默认无solver宿主流程。

旧 header scope 与 ledger scope 仍保留此前通用说明，具体来源以结构化 provenance、plan、scenario 和 solver opening/completion 为准；这是已披露的描述兼容性欠账，适合下一次生成链变更同步整理，不为改文字重跑本批。

## 第 4 步最小起点（R1闭合后）

先把现有整个 battle 方法作为正常 Run 控制器里的战斗策略单元：每场以实际新 CombatState 建立新 root/context/query计数及 capture，胜利结束该场票据，允许原生奖励/Travel，下一场从新 Play 开始捕获求解。当前 Closed 是场景终止门，不能直接关掉保护后复用旧战斗；需明确“结束战斗”和“结束整次实验”的不同生命周期。死亡继续终止独立实验进程，不新增未验证 worker 复用。

奖励复用 `NativeRewards.Collect`、RewardsSetSynchronizer 与 `RewardsCmd.OfferForRoomEnd`；`NativeRewardProbe.cs:73` 的统一会话及正常 room-end 调用链已经可用。将新场景纳入相应观察/检查能力门，保持完整选择任务、父子归属与实际 acquired 实例。不要重写奖励业务、人工添加牌或只启用执行而漏启用观察契约。

建议首个有界批次登记正常首战胜利→原生金币与卡奖励 skip/take→合法 Travel→下一战在线动作/终态及 replay。最多4次原生执行（两条分支各live/replay）、20分钟、2GiB、进程120秒；每场64动作和查询，整条路径另设总动作门。固定原种子及奖励策略；新牌不在 solver 支持范围时保留 Unsupported，不换种子或跳过效果求全绿。

验收重点是同一 Run 的 HP、金币、永久牌组/奖励实例/遗物/药水延续，战斗资源与临时状态按原生初始化，上一场未消费票据跨场拒绝，新战斗 parent/RNG不受查询扰动，逐步动作与回放准确。奖励/地图以原生执行与数据契约验收；战斗是局部差分，不能让奖励也冒称独立仿真。取得的卡必须留在后续牌组，是否首手抽到由原生流程决定。

当前第3步需要先关闭R1；第4步未执行。M1仍缺接回奖励/多战及后续自然完整通关和最终同构建验收。本报告不要求用户重新批准已授权步骤。
