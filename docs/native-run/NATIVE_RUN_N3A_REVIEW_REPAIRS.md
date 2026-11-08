# M1 步骤 4A：独立审查 R1 / R2 修复及统一回归

日期：2026-11-02。依据[独立审查](NATIVE_RUN_N3A_REVIEW.md)。**R1/R2 已完成生产修复和同构建本地回归，待独立复审。** 本轮止于4A，不进入4B，不把尾部夹具或自然早死称为完整爬塔。

## 冻结与证据

- Build ID：`E261E0D1974021A463F4C382D05670BC588C731F606FD3922453850E235094FD`。
- NativeRun DLL SHA256：`BC465E9F22162F95B6AC99135E6529691B2E8DFC7A5CA0EE098CDEE26D640A8E`。
- Manifest：`.build/native-build/n3a-repair-final01/build-manifest.json`，38个source、22个compiled、3个原版inputs、7个artifacts逐项hash匹配；Build ID按原规范JSON重算一致。
- 本地HEAD仍为`d73039d688eb54c9513f5b9508a03947e689aa18`，实际dirty源码及编译快照已纳入上述manifest，未拿HEAD替代源码身份。
- 汇总：`.build/runs/n3a-repair-work01/summary.json`；补充核对：`inspection.json`，216项通过。
- 4A矩阵：`.build/runs/n3a-repair-final01-suite/summary.json`；新增控制：同目录`terminal-review-controls.json`。
- 步骤1–3回归：`.build/runs/n3a-repair-final01-n2/summary.json`；Core实际执行：`.build/runs/n3a-repair-work01/core.log`。

最终编译0 warnings／0 errors，所有原生场景均用final01同一产物。15个Eleanor锁定输入、52个兼容源码hash保持；未在原生host加载Mod或CombatSolver。原始审查及历史失败证据未改写。

## 生产修复

`NativeTerminalContract.cs`现在维护有限场景的生命周期状态机，与写入方的`valid=true`断言分别核验：

1. 每个native计数由对应已观察开始／成功完成事件推进，再与每条快照的全部8项计数、Won及Closed精确比较。缺字段、清零、虚增及状态倒退都不能靠保留事件名通过。
2. EventRoom快照必须有完整RNG、已注册对象reference、匹配model及布尔IsFinished；Architect实例必须保持同一真实TheArchitect。OnEnded返回记录清场前HP，WinRun／loss完成记录实际HP0；stamp及后续终态快照保持HP0。
3. stamp里的native事实与同条快照比较；terminal-contract、stamp、result终态必须一致。Architect的IsFinished按原生顺序在选项收尾后、stamp时才要求true，允许WinRun完成时事件尚未SetEventState收尾。HP0的原生胜利继续认证为RunVictory。
4. transition与实际ActReady的act／map输入绑定；next-act完成的幕、map generation、房间及HP／金币／完整永久deck与开始和尾部事实交叉校验。旧幕vote有独立已确认动作，原生忽略后的实际状态必须不变。
5. 原生loss、WinRun、OnEnded和act-transition回调绑定当前已派发root及其context。动作确认后的stamp／contract绑定最后确认root；stamp必须晚于原生成功完成及该动作确认。唯一性检查拒绝重复stamp、重复完成和重复合约；终态stamp之后拒绝新增root／dispatch。

跨幕完成状态属于新房间，但callback沿用动作开始时的context，这是合法关联。没有把context强行改成完成后房间，也没有新增默认关联值。

`NativeEvidence.cs`只增加已登记的`--mutate terminal-review:*`控制路由；新`NativeTerminalControls.cs`仅修改新建Lab证据副本，沿用生产变异工具重建seq／hash链／footer／seal／result seal。实际回归调用冻结DLL的生产`--review`，没有用测试脚本代替生产校验。原生执行器、业务调用与Architect展示seam未修改。

## 原审查12负控：全部在生产门拒绝

下列目录均在`.build/runs/n3a-repair-final01-suite/control-terminal-review-<名称>/`。每项mutation实际exit0，随后生产review实际exit1；完整diagnostic保存在`review.log`及`terminal-review-controls.json`。

| 原审查名称 | 拒绝门 |
| --- | --- |
| death-stamp-alive | Terminal stamp requires actual dead player snapshot |
| architect-zero-authority | Lifecycle counter mismatch: ActStarts |
| terminal-contract-wrong-terminal | Terminal contract/stamp mismatch |
| architect-missing-event-identity | Missing event model identity/completion |
| next-act-snapshot-contradiction | Tail current snapshot mismatch |
| architect-detached-lifecycle | Lifecycle active action owner mismatch |
| terminal-contract-before-native-win | Lifecycle confirmed boundary owner mismatch |
| duplicate-terminal-stamp | Duplicate lifecycle event: terminal-stamp |
| missing-terminal-state | Missing required state snapshot: terminal-stamp |
| null-win-state | Null state snapshot: run-win-completed |
| nonobject-transition-state | Nonobject state snapshot: act-transition-completed |
| missing-terminal-counter | Lifecycle counter mismatch: WinReturns |

另补12个同类负控：单独错误win context、stamp事实／快照不符、Closed=false、事件未完成、错误event model、引用已注册但非该事件的对象、重复win completion、stamp早于root确认、终态后dispatch、WinRun完成快照仍存活、终态后快照存活、丢失OnEnded清场前HP。均在对应语义／所有权门exit1。

24个负控之外，两项真正等价副本通过：重新封装不改语义，以及native对象属性顺序反转。未要求JSON属性序相同才能通过。正常场景也继续接受合法History.Clear和先原生胜利再杀玩家的次序。

补充独立核对脚本检查了每个副本的实际扰动内容，既核对exit，也核对具体诊断，未用通用seal错误代替语义拒绝。

## 同最终产物的执行结果

| 范围 | 实际结果 |
| --- | --- |
| 4A真实场景矩阵 | 16个原生子进程、149项检查、0失败 |
| 步骤1–3／N2 | 74个原生子进程、580项检查、0失败；含自然skip24／take25及冻结重放、选择／身份／来源／完整RNG／观察对照、37旧腐坏控＋65快照控＋2 metadata控 |
| 新增终态控制 | 24坏副本全部拒绝、2等价副本通过；包含原12负控，计入4A检查数，不计作原生对局 |
| Core | 实际执行98项通过；所用Core DLL与final01中的Core DLL hash一致 |
| 补充绑定及内容核对 | 216项通过，不计作额外对局或独立样本 |

合计90个原生子进程、729项suite断言。实际原始exit为56×0、31×1、1×2、2×3；逐进程与expected／process／result一致，未被外层成功退出覆盖。

真实自然Death仍为13/13、10次EndTurn、实际敌方HP损失合计80；没有胜利清理或奖励。next-act夹具5/5、Act0→1、map2→3、旧vote无效。Architect夹具7/7，原生OnEnded返回HP80，WinRun完成HP0，原生事件完成后认证RunVictory。

预算／失败前缀分别保持：

| 场景 | confirmed / dispatched | 状态与HP |
| --- | --- | --- |
| natural-death-budget | 4 / 4 | Ongoing / BudgetExceeded，HP68，exit3 |
| natural-death-fault | 3 / 4 | Unknown / EngineError，HP68，exit1 |
| tail-architect-prefix-fault | 6 / 7 | Unknown / EngineError，HP80，无成功OnEnded return，exit1 |
| tail-architect-win-fault | 6 / 7 | Unknown / EngineError，HP0，有OnEnded return但认证wrapper故障，exit1 |

这些失败／预算结果的DataComplete和seal.complete均false，失败不会变成可认证成功。预算仍是一次实际战斗决策后的声明停止，未冒称墙钟耗尽。

三个正场景各自live、冻结replay、off、minimal的route／choices／final-state逐字节一致。三个4A场景和自然skip／take的这三个文件也与旧final05基线逐字节一致，详见`business-state-comparison.json`。修复没有改变实际动作、资源或已盘点RNG。

另外，用新reader复核上轮审查保存的18个历史副本：原12坏副本全部拒绝、6个正常／等价副本通过。证据在`n3a-repair-work01/historical-reader/summary.json`，明确分别记录新reader Build ID和旧packet Build ID。这是读取器兼容回归，不拼入本次同构建原生执行结论；历史原文件hash保持。

## 复现与范围

~~~powershell
pwsh -NoProfile -File tools/native-run/run-native-n3a.ps1 `
  -BuildManifest .build/native-build/n3a-repair-final01/build-manifest.json `
  -OutputDirectory .build/runs/<fresh-n3a-repair-directory>
pwsh -NoProfile -File tools/native-run/run-native-n2.ps1 `
  -BuildManifest .build/native-build/n3a-repair-final01/build-manifest.json `
  -OutputDirectory .build/runs/<fresh-n2-directory>
~~~

仍固定v0.111.0／41cef1ea、Ironclad/A0、现有独立内存实验档案、seed `LAB_V0111_NATIVE_DEV_01`；同种子重复是复现证据，不是独立胜率样本。自然／fixture／fault-control与live／frozen-replay／observation-control字段保持独立。

本轮没有修改角色、游戏安装、真实用户存档或部署，没有启动游戏UI、真实Steam或游戏网络服务，没有提交／推送。保存继续使用MemorySaveStore及现有SaveProgress／WriteReplay拦截；终态流程安全入口保持。未做OS级全系统文件写入审计。

边界继续保留：TestMode原生死亡没有OnEnded(false)／胜利AfterCombatEnd清理，死亡进程不复用；跨幕夹具未完成前幕Boss；Architect夹具未从正常起点打完整run。展示桥不代表零售演出或其RNG等价。当前生产契约仅认证已登记的有限场景，不宣称全部事件、全run或公开Mod接口支持。

本轮已修改三个既有文件（NativeTerminalContract、NativeEvidence、run-native-n3a）并新增NativeTerminalControls和本文；其他既有dirty工作及原报告保留。开发候选dev01先验证旧证据，final01编译和统一矩阵一次通过；所有最终结论只使用final01。当前无权限或执行阻塞，交独立复审后再推进4B；4C–4D及步骤5–6仍待执行，全六步授权继续有效。
