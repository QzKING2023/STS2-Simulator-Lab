# M1 步骤 4A 独立验收：执行证据成立，终态数据门仍需修复

日期：2026-11-02。审查依据：[4A 本地验证](NATIVE_RUN_N3A_VALIDATION.md)、[步骤4交接](NATIVE_RUN_N3_IMPLEMENTATION_HANDOFF.md)、[M1方案](MILESTONE_1_NATIVE_RUN_PLAN.md)。

**结论：4A 尚未通过最终验收。** 正常起点真实死亡、跨幕局部夹具、Architect 原生选项到真实 WinRun 的已有执行证据成立；生产证据读取器仍会接受关键终态快照矛盾和失联／乱序记录。以下 R1、R2 是本轮既定数据可信门的必修，不涉及扩大节点覆盖或改变原生玩法。修复前不把4A标为独立通过，也不开始4B实现。

## 1. 冻结与独立检查范围

- Build ID：`30C9DF55C940F10D12ACD40B1793BCC667DBB22A3CEE85700B1069E4F2D27E5F`。
- NativeRun DLL SHA256：`2064581C092AC6B61640BB2138E858FF87C58CCE0E1EB04A0BF03E71139D7A60`。
- Manifest：`.build/native-build/n3a-final05/build-manifest.json`。37个source、21个compiled、3个原版inputs、7个artifacts逐文件hash匹配；用原规范JSON重新计算Build ID一致。
- 独立审查产物：`.build/runs/n3a-independent-review01/summary.json`、`review.py`、`reseal.ps1`、`build-id-check.json`、各副本中的`reader.log`。

本轮只读运行实现和原始证据，在新隔离目录修改证据副本、运行冻结DLL的生产`--review`。**没有启动新的原生游戏场景、没有重新编译项目。** 133项文件／已有结果核对全部成立，但不能用这些绿项覆盖下述8个错误接受。

生产读取器实际运行130次：112个已有腐坏副本全部exit1；5个正常副本及1个重新封装但未改语义的副本全部exit0；12个新增语义负控中4个正确拒绝、8个错误exit0。副本重建seq、hash链、footer计数、seal及result中的seal；错误接受不是旧seal未更新造成，也不是孤立测试替身。468个读取源文件的hash和全部既有项目文件在审查脚本结束时保持不变。

## 2. 已成立的执行结论及限制

### 自然死亡与失败前缀

`Program.Battle`实际调用`OnEndedTurnLocally`、入队`EndPlayerTurnAction`并等待原生队列；没有改HP、伤害或直接调用LoseCombat。`ProcessPendingLoss`原生主体确认停止战斗，观察postfix后才允许Death stamp。

`.build/runs/n3a-final05-suite/natural-death-1/`记录13 dispatched／13 confirmed、最后边界`EndTurn:13`、10次真实EndTurn、80条战斗history。敌方实际UnblockedDamage合计80，HP80→0。最后一击UnblockedDamage14、Overkill4；Unblocked已经是实际HP损失，不能再扣一次Overkill。无胜利清理、奖励或后续旅行。Death是合法终局，但早死不证明后期爬塔覆盖。

原生TestMode损失路径不调用`OnEnded(false)`，不执行胜利AfterCombatEnd／History.Clear；保留死亡后的实际资源与战斗残留是正确的当前范围。一个run一个进程，不能据此承诺死亡worker可复用或零售loss统计收尾。

故障／预算原始结果没有误报成功：

| 场景 | confirmed / dispatched | 实际结果 | 最后确认边界 |
| --- | --- | --- | --- |
| natural-death-budget | 4 / 4 | Ongoing / BudgetExceeded，exit3；真实敌方回合后HP68 | EndTurn:4 |
| natural-death-fault | 3 / 4 | Unknown / EngineError，exit1；敌方效果已发生，当前root未确认 | Travel:3 |
| tail-architect-prefix-fault | 6 / 7 | Unknown / EngineError，exit1；OnEnded主体未执行，HP80 | ArchitectOption:6 |
| tail-architect-win-fault | 6 / 7 | Unknown / EngineError，exit1；原生OnEnded与清场已执行，wrapper在认证前故障，HP0 | ArchitectOption:6 |

这些是预先声明的故障控制；后者不能被描述为“原生WinRun从未完成”，也不能据HP0改判Death。

### 跨幕局部夹具

`TailProbe`声明局部`EnterAct`前置，再通过`SetLocalPlayerReady`、`VoteToMoveToNextActAction`、原生`OnPlayerReady/MoveToNextAct`执行`EnterNextAct`。实现单独等待原始transition Task，未把队列空闲当完成。实际Act0→1、map generation2→3、MapRoom，HP／金币／永久deck引用保持。旧幕vote实际入队，由原生旧幕条件忽略，没有再次开始transition。结果5/5、Ongoing/SliceBoundary。

该夹具未完成前幕Boss，未验证自然Boss奖励结束后允许ready的业务前提；下一幕首地图候选已记录但未派发。来源api-fixture正确，不构成自然跨幕证据。

### Architect与展示适配

已检查固定程序集的`TheArchitect`、`RunManager`、`EventModel`和相应展示入口。原生调用链为：EnterNextAct进入实际Architect EventRoom → OnRoomEnter一次 → PlayCurrentLine原Task → 原生AdvanceDialogue／真实PROCEED选项 → TheArchitect.WinRun → RunManager.WinRun → OnEnded(true)成功返回 → GuaranteeKillAllPlayers → WinRun完成 → 根动作确认 → terminal-stamp → terminal-contract。

`.build/runs/n3a-final05-suite/tail-architect-1/`实际7/7，OnEnded返回快照HP80、WinRun完成快照HP0、最终RunVictory。事件IsFinished在原生选项返回后变true。实现没有手工添加PROCEED、直接调用RunManager.WinRun或合成已获胜状态。清场前状态通过run-end-return保存；最终stamp在清场后认证成功任务，先前原生胜利证据没有被HP0覆盖。

`NativeArchitectPresentation`只在固定async MoveNext的唯一GetSpeaker空值早退锚点加入无UI继续判断，并拦截ShowSpeechBubble绘制；其余原生对话、SetEventState和WinRun主体保持。目标已有patch拒绝、唯一锚点、MVID／token／原IL hash和运行期patch inventory核对存在。原PlayCurrentLine Task被跟踪并实际等待，异常可传播。

这一适配会改变无节点时的原始早退行为，作用是恢复藏在展示后面的原生选项流程，不能称为完全未改变控制流。没有构造假speaker；ArchitectCreature仍为null，所以原生攻击演出分支不执行，也不消耗它们的event RNG。最终事件RNG完整记录、counter为1；live／replay／off／minimal同一无UI语义范围内一致。**没有验证零售动画、评分演出、其他人物／访问次数对话或零售RNG等价**。当前有限尾部探针可接受这个明确边界，无需本轮扩演出仿真。

### 安全与旧基准

`OnEnded`持久保存／历史／metrics／achievement段受ShouldSave守卫，Daily上传另受DailyTime守卫；当前二者关闭。MemorySaveStore、SaveProgress／WriteReplay拦截、Singleplayer、无NGame／Steam／Mod／CombatSolver隔离保持。Architect实际命中一次SteamStatsManager.RefreshGlobalStats前置断言；该原生方法在Steam未初始化时立即返回，未改成伪造平台成功。

自然Death拦截Progress3／Replay0，Architect Progress2／Replay0，Telemetry0。没有OS级全系统文件写审计，因此结论限定为已检查入口及隔离证据；不把Lab报告落盘或内存档案变化说成真实用户存档写入。

90个既有原生进程及677项suite断言已逐报告核对：4A16／97，N2 74／580。实际exit为56×0、31×1、1×2、2×3，与每条预期、report、process一致。151原补充与Core98是独立计数，不是额外对局。本轮确认Core98日志与当前Core输出身份一致。15个Mod锁定输入和52个兼容源码hash不变；未把旧Mod二进制结果当作本次NativeRun运行结果。

N2正常skip24／take25与其冻结重放保持；原B9的route和final-state逐字节一致。112个原腐坏控包含N2的37旧控、65快照控、2 metadata控，以及4A的8控，均由本轮独立副本调用生产读取器拒绝。natural／api-fixture与live／replay／observation-control来源字段独立并一致。

## 3. 必修 R1：关键终态事实未与快照相互核验

位置：`tools/native-run/NativeTerminalContract.cs`的Accept（约13–44行）和Finish（约46–58行）。当前只检查计数非负、bool可读取和末个HP；EventRoom只要求eventModel.rng。若保留事件名称及`valid=true`，互相矛盾的关键事实仍可通过。

以下新副本均由冻结生产`--review`返回0并生成`trace-review.status=passed`：

| 副本目录后缀（均位于独立审查目录） | 仅修改的语义 | 应拒绝的原因 |
| --- | --- | --- |
| control-review-death-stamp-alive | Death terminal-stamp.state.hp改80，后续原快照仍0 | 终态认证时玩家仍存活，与Death矛盾 |
| control-review-architect-zero-authority | 全部lifecycle.native计数置0、Won/Closed=false；stamp facts.native同步置零 | 事件声称成功，但实际所记录权威计数始终未发生，且从未关闭 |
| control-review-terminal-contract-wrong-terminal | terminal-contract.facts.terminal改Death，其他仍RunVictory | 合约、stamp和结果相互矛盾 |
| control-review-architect-missing-event-identity | 所有事件快照删除reference、model、IsFinished，保留RNG | 无法确认同一真实Architect及实际完成状态 |
| control-review-next-act-snapshot-contradiction | tail-contract.state改act0／CombatRoom，facts仍to1／MapRoom／valid=true | 实际快照不支持跨幕完成事实 |

**最小修复：** 在现有三个有限场景的生产读取器内，明确必需字段及类型；把生命周期计数、Won／Closed、事件身份与完成状态、HP、幕／地图与当前房间绑定到对应事件和结果。stamp里的native事实应与同条snapshot一致；terminal-contract的terminal应与stamp及result一致；next-act facts应与原transition和tail snapshot一致。不要仅信任写入方`valid`，也不要只检查字段存在。

按原生实际顺序校验：OnEnded成功返回可先观察Won=true，WinReturns在Task完成后才变1；Architect的IsFinished是在WinRun完成之后、原生选项收尾时才true，不能错误要求run-win-completed时已经完成事件。Death必须在loss之后、该stamp快照HP0；本轮不要求原生死亡运行胜利清理。无需对所有未知状态字段做无界schema审计。

## 4. 必修 R2：新增生命周期记录缺少动作关联和顺序约束

位置：`NativeTraceContract.Accept`约50行把记录交给terminalContract，但新增生命周期事件未加入root／context所有权校验；`NativeTerminalContract`只用少量位置变量和sticky bool记录成功。以下副本也全部生产exit0：

| 副本目录后缀 | 修改 | 应拒绝的原因 |
| --- | --- | --- |
| control-review-architect-detached-lifecycle | 将run-win、run-end、stamp、terminal-contract、tail-contract的root/context改为不存在的值 | 原生成功记录不再属于实际Architect选项根动作 |
| control-review-terminal-contract-before-native-win | 把terminal-contract移动到run-win-start之前 | 尚无原生完成就声称终态关闭；读取器的terminalValid仍会在后续沿用 |
| control-review-duplicate-terminal-stamp | 复制Death terminal-stamp并重新封装 | 一次终局被重复认证，当前读取器仍接受 |

**最小修复：** 复用已有root／context状态机，对原生start／return／completed绑定当前已派发动作；对动作确认后产生的stamp／contract绑定最后已确认动作，不强迫其仍在active root内。同一原生调用的开始／完成必须配对、恰好一次且顺序正确。终态序列要求真实completion → 对应root确认 → 唯一stamp → terminal-contract；禁止在终态之后新增派发。tail-contract、transition及实际ActReady动作同样绑定，保留旧vote在其独立root内合法被原生忽略的证据。

跨幕时动作context记录的是开始房间，完成state已经是下一房间；应绑定该动作既有context及真实迁移事实，不把“context与完成后房间字符串不同”误判非法。不要新增可伪造默认关联或去掉旧N2检查。

## 5. 修复验收与下一轮

可直接复现一个错误接受（此命令只写该审查副本的reader结果）：

~~~powershell
dotnet .build/native-build/n3a-final05/bin/Release/net9.0/SimulatorLab.NativeRun.dll `
  --review .build/runs/n3a-independent-review01/control-review-architect-detached-lifecycle
~~~

预期修后exit1；当前实际exit0。其他7个错误接受同理。`review.py`记录每次进程exit与stderr。未改语义的resealed-positive已通过，证明重封装工具不会自行制造通用seal错误。

此外，缺terminal-stamp.state、run-win-completed.state=null、act-transition-completed.state非object、缺WinReturns四个控制已正确拒绝；现有快照必需机制有效，应保留。

下一实施轮只修R1/R2并补生产负控，保留原证据目录。修后冻结实际源码和输出，用同一最终产物执行4A＋N2必需矩阵及Core适用回归；原8个错误接受须在具体语义／关联门拒绝，正常场景、前缀故障、observer／replay及15输入／52源码继续保持。不能用外层exit0或文档限制替代修复。

本轮未进入4B交接的通过前提。现有N3交接中的4B范围仍有效：内部room adapter、Rest heal/smith/cancel、简单事件和首条自然多节点片段；待4A复审通过后细化实施。4C–4D、步骤5自然跨幕／完整run、步骤6预登记批测仍待执行；全六步授权继续有效，无需重新询问用户。

本轮仅新增本审查文档和忽略目录内审查产物；未改运行实现／既有正式计划／原始结果，未提交或推送，未改角色／安装／用户存档，未启动UI或实际Steam／网络入口。无权限或工具阻塞；当前阻断是上述已复现的数据完整性缺陷。
