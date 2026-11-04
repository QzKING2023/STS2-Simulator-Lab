# M1 步骤 4A：真实终态与 N1-T 尾部探针

日期：2026-11-02。依据[步骤4交接](NATIVE_RUN_N3_IMPLEMENTATION_HANDOFF.md)，仅实施4A。**4A实现与同构建统一回归本地通过，待独立审查。** 4B–4D、自然跨幕／完整爬塔和M1整体验收未完成。

## 冻结与复现

Build ID：`30C9DF55C940F10D12ACD40B1793BCC667DBB22A3CEE85700B1069E4F2D27E5F`。

NativeRun DLL SHA256：`2064581C092AC6B61640BB2138E858FF87C58CCE0E1EB04A0BF03E71139D7A60`。

Manifest：`.build/native-build/n3a-final05/build-manifest.json`，记录实际dirty源码／编译快照／输入／产物。HEAD仍为`d73039d688eb54c9513f5b9508a03947e689aa18`。4A矩阵在`.build/runs/n3a-final05-suite/summary.json`；N2回归在`.build/runs/n3a-final05-n2/summary.json`；补充核对在`.build/runs/n3a-work01/`。

最终编译0 warnings/0 errors。37个source、21个compiled、3个原版inputs、7个artifacts逐文件hash匹配；Build ID按规范JSON重算一致。15个Mod锁定输入及52个旧兼容源码hash保持，未加载到原生host。Core DLL与既有Core检查入口依赖一致。

~~~powershell
pwsh -NoProfile -File tools/native-run/run-native-n3a.ps1 `
  -BuildManifest .build/native-build/n3a-final05/build-manifest.json `
  -OutputDirectory .build/runs/<fresh-n3a-directory>
pwsh -NoProfile -File tools/native-run/run-native-n2.ps1 `
  -BuildManifest .build/native-build/n3a-final05/build-manifest.json `
  -OutputDirectory .build/runs/<fresh-n2-directory>
dotnet .build/native-build/n3a-final05/bin/Release/net9.0/SimulatorLab.NativeRun.dll `
  --review .build/runs/<fresh-n3a-directory>/tail-architect-1
~~~

固定v0.111.0/41cef1ea、Ironclad/A0、all-epochs/zero-runs内存档案、seed `LAB_V0111_NATIVE_DEV_01`。死亡策略为`native-end-turn-only/v1`；其余沿用first-legal。相同开发种子的重复不是独立样本或胜率评估。

## T01–T06实际覆盖

| 项目 | 真实执行与结果 | 界限 |
| --- | --- | --- |
| T01 战斗Victory | 由N2自然skip/take同构建回归检查清理前history、清理、原生奖励、下一战Play | 单战胜利不是run胜利 |
| T02 自然Death | 正常Neow后第一战只合法EndTurn；13 dispatched/13 confirmed，10次实际EndTurn，敌方实际未格挡HP损失合计80，HP80→0；原生ProcessPendingLoss后Death/Completed | 不改HP/伤害，不直接LoseCombat；早死不覆盖后期 |
| T03 跨幕夹具 | EnterAct(0)局部前置→原生ready/vote→EnterNextAct→Act1 MapRoom及递增map generation；5/5动作，记录首个starting-map-point候选；旧幕原生投票被忽略 | 未打前幕Boss，未验证Boss完成后允许ready的自然业务前提；下一幕首地图动作未派发 |
| T04 Architect夹具 | EnterAct(last)局部前置→原生ready/EnterNextAct→Architect.OnRoomEnter/真实选项→OnEnded(true)成功返回→WinRun原生任务完成；7/7，RunVictory/Completed | api-fixture，前幕和Boss未执行，不是normal-start full victory |
| T05 终态与错误解释 | OnEnded返回时HP80，WinRun完成时HP0，仍为RunVictory；实际敌方损失为Death。终态后实际Action入口拒绝派发，delegate未运行 | prefix／任务故障控制不能认证成功 |
| T06 预算／前缀 | 一个真实敌方回合后固定battle-decision budget停止：4/4，Ongoing/BudgetExceeded，exit3；回合后故障为3 confirmed/4 dispatched、Unknown/EngineError、exit1 | 次数预算不是墙钟耗尽实验；注入故障不是自然引擎缺陷 |

`tail-architect-prefix-fault`为6/7，prefix已观察但原生OnEnded主体未执行，无成功return；`tail-architect-win-fault`也为6/7，原生OnEnded及杀玩家已执行，但受控wrapper在成功标记前失败，HP0也不被改判Death或完整胜利。两者均fault-control/exit1，保留前缀和未确认当前动作。

TestMode原生死亡不调用OnEnded(false)，也不走胜利AfterCombatEnd/History.Clear。本报告保留死亡后的实际资源、战斗状态与残留，不补调胜利清理，不承诺零售game-over UI／loss统计收尾；一个run一个进程，死亡进程不复用。

## 实现与展示边界

`NativeLifecycle.cs`观察ProcessPendingLoss、OnEnded成功返回，并包装EnterNextAct/WinRun原始Task；不替换业务主体。`NativeTerminalProbe.cs`提供本轮局部夹具、有界等待、终态stamp与停止后动作门。旧战斗计数改为每战delta；原N2切片要求保持独立。

`NativeTerminalContract.cs`检查新增有限场景的开始／完成顺序、终态权威、夹具契约、结果、事件RNG。原20种必需state及16种metadata保留；新增12种必需state和3种metadata，不以放宽旧生命周期门接纳尾部场景。

Architect首次执行真实停在OnRoomEnter清空选项后：PlayCurrentLine找不到视觉speaker提前返回，首行选项未恢复。`NativeArchitectPresentation.cs`对固定游戏的async MoveNext中唯一`GetSpeaker→speaker存取→brtrue`锚点增加无UI展示判断，保留其余原生对话、等待、SetEventState和WinRun；仅跳过ShowSpeechBubble绘制。没有造假speaker/Creature、手工添加PROCEED或直接调用WinRun。

PlayCurrentLine原Task纳入task-start/completed/fault关联，OnRoomEnter根动作实际等待它完成；EventOption及WinRun任务分别记录。seam保留token/MVID/IL SHA256、唯一锚点、未知patch拒绝及完整patch inventory稳定检查。

视觉节点缺失时原生ArchitectCreature仍为null，依赖它的攻击演出分支原生不执行。当前EventModel.Rng实际字段已记录；不宣称零售UI演出RNG等价或完整演出已运行。该展示seam的语义范围仍需独立审查，不能外推其他事件。

## 保存与平台

执行前静态核对CreatureCmd.Kill、ProcessPendingLoss、RunManager.WinRun/OnEnded/GuaranteeKillAllPlayers、ActChange、EventModel和SteamStatsManager，原始只读资料保存在`.build/runs/n3a-work01/`及`.build/review/n3-handoff/`，不发布游戏反编译源码。

OnEnded的progress/history/metrics/achievement/删除存档主段受ShouldSave守卫；Daily上传受DailyTime守卫。继续shouldSave=false、无Daily、MemorySaveStore和SaveProgress/WriteReplay拦截。

Architect→StatsManager→SteamStatsManager.RefreshGlobalStats在Steam未初始化时原生立即返回。新增prefix只断言并记录该条件，原方法仍执行，不初始化Steam；Architect实际命中一次该守卫。自然死亡Progress3/Replay0，Architect夹具Progress2/Replay0；Telemetry均0。原N2 skip/take保留Progress5/Replay1。

实际报告核对无NGame、无Steam、内存backend、本地Singleplayer、无Mod／CombatSolver。未对全系统所有OS文件写入作追踪；Lab报告写入和内存档案变化不代表用户持久存档写入。

## 定向结果与保留失败

最终4A：**16进程、97检查、0失败**。三个正场景各live、frozen replay、off/minimal共12进程；四个失败／预算控制各1。原始exit为12×0、3×1、1×3，预期错误未当普通完成。

同一final05产物的N2回归为**74进程、580检查、0失败**，包含37类原腐坏控制、65个快照控制和2个metadata控制。自然skip24/take25及其冻结重放、Pommel Strike i31→下战i35.DeckVersion关联、完整RNG、observer对照保留；route与final-state与B9基线逐字节一致。Core实际执行98项通过。补充文件／绑定／独立结果核对151项通过（`inspection.json`）。

合计90个原生子进程、677项suite断言；151补充和Core98分别列出，不当作额外对局数。原始exit合计56×0、31×1、1×2、2×3，均与预登记expected及报告一致，没有外层exit0掩盖错误。次数预算header.actionCount指战斗决策循环上限，budget场景为1，不包含开局／旅行3个setup根动作。

8类腐坏控制调用生产`--mutate/--review`：删除terminal stamp、loss completion、WinRun completion、OnEnded return、act completion、tail contract；EventModel RNG=null；tail valid=false。重建hash链/footer/seal/result后，均在具体语义门exit1。三个正场景route/choices/final-state对重放、off/minimal逐字节一致，包含已盘点run/player/event synchronizer及当前EventModel实际RNG。

开发失败均保留：dev01编译引用错误；dev02真实死亡／跨幕通过但Architect Unsupported；dev03展示桥后原生胜利；final01的16/91矩阵。final02新增首候选查询后重放误消费未来根动作，该失败保留且不认证；修复为待决策候选记录。final03/04是收口候选，只有final05用于最终统一结论。

## 后续

下一轮交Astra审查终态权威、展示seam、实际调用、夹具范围、错误传播和冻结一致性。通过后进入已授权4B：room adapter、Rest heal/smith/cancel及简单事件的语义回调／完成条件。4C/4D、自然完整run与公开Mod API仍后置。当前没有权限或执行阻塞，不需用户追加批准既定步骤。

无commit/push、角色／安装／真实存档修改或真实UI操作；既有dirty工作与历史证据保留。
