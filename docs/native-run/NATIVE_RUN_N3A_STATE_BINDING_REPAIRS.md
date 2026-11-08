# M1 步骤 4A：最后确认状态绑定修复与统一回归

日期：2026-11-02。依据[修复独立复审](NATIVE_RUN_N3A_REPAIR_REVIEW.md)。**剩余 R1 已完成生产修复和同最终构建回归，待独立验收。** 本轮止于4A，4B尚未开始；4C–4D、步骤5–6及完整M1仍待完成。

## 修复与范围

原检查器保存了最后确认动作的root/context/action，却没有保存该动作的完整状态。后续仅交叉检查部分字段，因此能接受没有新业务操作却发生幕号、金币或RNG变化的终态／尾部快照。

`tools/native-run/NativeTerminalContract.cs`现在在每次`confirmed`时保存不可变的`JsonElement.Clone()`。静止观察边界统一与该完整状态做结构化JSON比较，覆盖所有已有及新增字段、嵌套对象、数组内容和顺序；对象属性顺序不影响比较。没有增加幕号／金币／RNG逐字段例外，也没有忽略整个lifecycle或资源区域。

| 有限场景 | 绑定最后确认完整状态的观察 | 唯一允许的差异 |
| --- | --- | --- |
| natural-death | death、terminal-stamp | 无 |
| natural-death | terminal-contract | lifecycle.native.Closed从false变true |
| tail-architect | terminal-stamp | 无 |
| tail-architect | terminal-contract、tail-contract | lifecycle.native.Closed从false变true |
| tail-next-act | OldActVoteControl确认后的tail-contract | 无 |

期望状态由确认快照另行解析构造，只改明确允许的Closed值；确认快照本身不会被修改。已有动作归属、时序、唯一性、原生计数和事件身份检查继续保留。

此比较只用于原生任务完成、root已确认之后的静止观察。OnEnded返回时HP80、WinRun完成时HP0属于真实清场变化，继续按原生回调契约验证，没有要求两个业务时刻状态相等。原生执行器、生命周期钩子、保存隔离和Architect展示桥源码均未修改。

## 最终冻结

- Build ID：`223DB2B31B0E199B694CFF24DBF08600F0A104AFCC095493EB907FD71538AA1F`。
- NativeRun DLL SHA256：`A02AEBF7A6791DC22E396F32D40202B778B222105C5054E9AC8BCCF8EDBFEFE6`。
- Manifest：`.build/native-build/n3a-state-final02/build-manifest.json`。38个source、22个compiled、3个原版inputs、7个artifacts逐项hash匹配；Build ID重新计算一致。
- 本地HEAD仍为`d73039d688eb54c9513f5b9508a03947e689aa18`，实际dirty源码及输出已冻结，未用HEAD代替源码版本。
- 仍固定游戏v0.111.0／41cef1ea、Ironclad/A0、独立内存实验档案、seed `LAB_V0111_NATIVE_DEV_01`。

最终编译0 warnings／0 errors。所有本轮原生执行使用final02产物；15个锁定Mod输入和52个兼容源码hash保持，原生host未加载Mod或CombatSolver。本轮没有新增Mod运行或适配。

## 生产门负控与等价副本

实际使用最终DLL的`--mutate`重建新副本的seq／hash链／footer／seal／result seal，再调用生产`--review`。语义诊断和原始退出均核对，未用坏seal替代语义拒绝。

`.build/runs/n3a-state-final02-suite/terminal-review-controls.json`记录**80个损坏副本全部exit1、8个等价副本全部exit0**：

1. 原24个终态控制保留，涵盖此前R1/R2、缺状态、错误身份、提前／重复／失联生命周期等。
2. 独立复审的4个新反例：Architect stamp错误act、Death stamp增加gold、Architect contract改Shuffle RNG、next-act tail改Shuffle RNG。均在`Confirmed lifecycle state mismatch: <事件名>`拒绝。
3. 七个静止边界×七类变更，共49个副本：block变化、deck升级变化、重复卡实例身份交换、player RNG变化、额外状态字段、额外lifecycle字段，以及已存在扩展字段在单一边界丢失。它们验证完整结构绑定，包括当前必需字段表以外的内容。
4. 3个R2回归副本：death completion改挂其它root、WinRun completion提前到OnEnded返回之前、重复act completion，分别在已有归属／顺序／唯一性门拒绝。
5. 8个等价副本包括原2项，以及三个场景各自的完整状态对象属性顺序反转、一致扩展字段。合法属性顺序和一致扩展不会误报。

跨幕尾部的deck升级／身份变更已由原有完整deck检查提前拒绝，诊断为`Tail current snapshot mismatch`；其它新完整状态控制在新增统一门拒绝。未要求负控绕过已有正确检查。

另外用最终读取器复核上轮独立审查的**163个原样副本**，全部得到预期结果，四个原始反例也在新状态绑定门拒绝。证据：`.build/runs/n3a-state-work01/historical-reader02/summary.json`。其中151包来自E261旧冻结、12包来自30C9旧冻结，每个case保留packetBuildId；这是读取器兼容回归，单列于本轮同构建原生执行。652个源文件hash保持，历史原包未改。

## 同最终构建结果

| 验收范围 | 结果及证据 |
| --- | --- |
| 4A矩阵 | 16个原生子进程、273项检查、0失败；`.build/runs/n3a-state-final02-suite/summary.json` |
| 步骤1–3／N2 | 74个原生子进程、580项检查、0失败；`.build/runs/n3a-state-final02-n2/summary.json` |
| Core | 实际98项通过，所用Core DLL与冻结产物hash相同；`.build/runs/n3a-state-work01/core.log` |
| 补充绑定与内容核对 | 347项通过；`.build/runs/n3a-state-work01/inspection.json` |
| 旧包生产读取器回归 | 163次、0失败；单列旧packet与新reader身份 |
| 汇总 | `.build/runs/n3a-state-work01/summary.json` |

合计**90个原生子进程、853项suite检查**。证据副本和重复执行不作为额外自然对局样本。实际raw exit为56×0、31×1、1×2、2×3，逐项与expected／process／result核对一致；没有用外层成功退出掩盖错误或预算停止。N2的37旧腐坏控、65快照控、2 metadata控及自然／fixture来源、执行模式、完整RNG门均回归通过。

真实自然Death仍为13/13动作、实际敌方伤害合计80；跨幕局部夹具5/5、Act0→1、map2→3、旧vote无效；Architect局部夹具7/7，经原生选项、OnEnded返回HP80、WinRun完成HP0，事件完成后认证RunVictory。自然skip24／take25仍到下一战Play，HP70、金币266、永久deck分别10／11。三个4A场景和自然skip／take的route、choices、完整final-state与原final05逐文件hash相同；各自重放及观察模式对照也保持一致。

四个失败／停止前缀保持：预算4/4且Ongoing／BudgetExceeded；回合故障3/4、Architect两个故障6/7且Unknown／EngineError。其DataComplete和seal.complete均false。没有因WinRun清场把未确认失败伪记Death或胜利。补充检查还独立构造七个边界的完整期望状态，验证正常轨迹只存在声明的Closed变化。

## 过程证据与复现

开发候选的20次定向读取检查通过。首个final01矩阵留下2项诊断文字断言失败：两个next-act deck变更已正确拒绝，但脚本原期待新增门的报错。只修正这两项预期诊断后重新冻结final02，并完整重跑；未修改生产检查器以迎合负控。旧结果和说明保留于`n3a-state-final01-suite/summary.json`及`n3a-state-work01/final01-diagnostic-correction.json`。

第一次旧包读取辅助脚本在完成读取后，汇总hash检查出现NameError；保留`historical-reader/helper-error.json`，修正辅助脚本后在新目录完成163次复跑。正式结论只采用`historical-reader02/summary.json`，该辅助故障不计作游戏故障或缺陷复现。

本轮复现入口（输出目录必须尚不存在）：

~~~powershell
pwsh -NoProfile -File tools/native-run/run-native-n3a.ps1 `
  -BuildManifest .build/native-build/n3a-state-final02/build-manifest.json `
  -OutputDirectory .build/runs/local-n3a-state-check
pwsh -NoProfile -File tools/native-run/run-native-n2.ps1 `
  -BuildManifest .build/native-build/n3a-state-final02/build-manifest.json `
  -OutputDirectory .build/runs/local-n2-state-check
~~~

旧包读取脚本为`.build/runs/n3a-state-work01/review-historical.py`，仅复制四个证据文件到新目录后执行读取器。不要对历史原始包直接执行会写回trace-review.json的命令。

## 改动、隔离与限制

本轮修改三个既有文件：`NativeTerminalContract.cs`、`NativeTerminalControls.cs`、`run-native-n3a.ps1`；新增本文。其他既有dirty源码、报告和历史证据保留。没有提交或推送。

真实场景仍使用MemorySaveStore，shouldSave=false、Steam未初始化、单人网络服务、无UI实例，SaveProgress继续被现有入口拦截；三个正常4A场景的拦截次数分别3／3／2，Replay和Telemetry实际调用计数均0、guards.faults为空。未改角色、游戏安装或用户存档，未启动游戏UI、真实Steam或游戏网络；没有声称进行OS级全系统写入审计。

边界保持：一run一进程，TestMode死亡不承诺零售loss收尾或进程复用安全；尾部夹具没有完成前幕Boss或从正常起点打完整run；Architect展示桥不代表零售演出或其RNG等价；原生执行为NotCompared。当前没有权限／环境阻塞。下一步为4A独立验收，通过后按既有授权接续4B。
