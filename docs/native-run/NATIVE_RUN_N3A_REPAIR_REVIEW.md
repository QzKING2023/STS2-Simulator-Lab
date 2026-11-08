# M1 步骤 4A 修复独立复审：R2通过，R1仍缺确认边界状态绑定

日期：2026-11-02。依据[原独立审查](NATIVE_RUN_N3A_REVIEW.md)、[R1/R2修复报告](NATIVE_RUN_N3A_REVIEW_REPAIRS.md)。

**结论：4A仍未最终通过。** 原12个负控和实施者补充的同类负控均已正确拒绝，动作关联／时序／唯一性修复成立；但终态／尾部快照仍未完整绑定最后已确认动作的状态。本轮独立构造4个矛盾副本，冻结生产读取器全部返回0并生成`status=passed`。这是R1剩余的一个有限缺口，不要求扩展4B或全局游戏规则验证。

## 冻结、独立执行及保留范围

核验Build ID：`E261E0D1974021A463F4C382D05670BC588C731F606FD3922453850E235094FD`。DLL SHA256：`BC465E9F22162F95B6AC99135E6529691B2E8DFC7A5CA0EE098CDEE26D640A8E`。

38个source、22个compiled、3个原版inputs、7个artifacts逐项hash匹配，Build ID重新计算一致。原90个原生子进程全部绑定该冻结；4A16／149、N2 74／580，共729项suite检查均与实际记录相符。raw exit分布56×0、31×1、1×2、2×3，逐项与expected／result／process核对一致。Core98日志和Core DLL身份一致，216项原补充检查记录完整。

本轮新证据在`.build/runs/n3a-repair-independent-review01/`：`summary.json`、`review.py`、`reseal.ps1`、`build-id-check.json`及各副本`reader.log`。实际调用最终DLL的生产`--review`共163次：

| 类别 | 独立结果 |
| --- | --- |
| 当前136个坏副本 | 全部exit1；包含本轮24个终态负控、4A原8个负控、N2的104个负控 |
| 原审查保存的12个坏副本 | 全部exit1；使用旧packet、新reader，独立列为读取器回归 |
| 当前5个正常副本、2个等价副本 | 全部exit0 |
| 本轮独立无修改重封装副本 | exit0 |
| 本轮额外3个归属／顺序／唯一性副本 | 全部exit1 |
| 本轮额外4个确认状态矛盾副本 | **全部错误exit0** |

160项独立身份／已有结果核对全部成立，但不覆盖4个错误接受。620个源证据文件hash保持，项目实现与既有文档未改。没有新原生游戏场景执行、没有重新编译；只在新目录复制并变异数据。重封装更新seq、hash链、footer计数、seal及result seal，保证检查的是生产语义门。未修改原始结果，也未使用实施者的`--mutate`来构造这4个新反例。

## 已闭合部分

R1原问题中的HP80死亡、计数清零、终态类型冲突、Architect身份缺失、跨幕act／room矛盾，以及必需state缺失／null／非object／缺counter，均已实际拒绝。native计数按开始／完成事件推进，再检查各快照；不会只凭`valid=true`通过。

R2原问题中的失联root／context、提前terminal-contract、重复stamp均被实际拒绝。本轮另外独立把death completion改挂到a1、交换OnEnded return与WinRun completion、复制act transition completion，分别在动作所有权、成功返回顺序和唯一性门拒绝。合法跨幕动作沿用起始context，完成state属于下一房间，正常副本继续通过。

真实执行证据保持成立：自然敌方损失80 HP、13/13动作后Death；跨幕局部夹具Act0→1、map2→3、5/5，旧vote实际无效；Architect夹具7/7经原生选项到OnEnded返回HP80、WinRun完成HP0、事件收尾后RunVictory。原生执行器、生命周期观察、展示桥源码未被本次修复修改，最终route／choices／final-state与旧final05逐字节一致；各自live／replay／off／minimal也一致。

自然skip24／take25与原基线相符，HP70、金币266及下一战Play保持；15个锁定Mod输入、52个兼容源码hash保持。失败前缀分别仍为预算4/4、回合后故障3/4、两个Architect故障6/7；故障结果Unknown／EngineError，预算Ongoing／BudgetExceeded，DataComplete和seal.complete为false，没有误记可认证成功。

## 唯一剩余必修：终态／尾部state与最后confirmed state脱节

位置：`tools/native-run/NativeTerminalContract.cs`：`CheckState`约27–51行、`confirmed`约88–92行，以及109行之后的stamp／contract分支。当前保留了lastConfirmed的root／context／kind／action，却没有保留它的完整state；`CheckState`又用当前记录覆盖lastState。后续只比较HP、少数计数和场景字段，未发现其它已采集状态在没有动作时发生变化。

四个副本都保留原root、context、原生完成事件、确认前缀、HP0和成功结果，只修改一个现有状态字段：

| 独立副本目录 | 同一已确认边界 | 实际扰动 | 生产结果 |
| --- | --- | --- | --- |
| control-independent-architect-stamp-wrong-act | a7 confirmed：act2 | terminal-stamp.state.act改成0 | exit0 / passed |
| control-independent-death-stamp-gold-created | a13 confirmed：gold249 | terminal-stamp.state.gold改成250 | exit0 / passed |
| control-independent-architect-contract-rng-changed | a7 confirmed：Shuffle.s0=`185516530060033458` | terminal-contract的同一值改为`185516530060033459` | exit0 / passed |
| control-independent-next-act-tail-rng-changed | a5 confirmed：Shuffle.s0=`185516530060033458` | tail-contract的同一值改为`185516530060033459` | exit0 / passed |

目录均相对`.build/runs/n3a-repair-independent-review01/`。其中前两个可让可信终态数据显示错误幕号或不存在的金币收益，后两个可让已声称完成且没有新动作的边界记录不同RNG。它们直接影响当前已承诺的状态／资源／复现数据，不是新节点功能或任意字段全值域审计。

可在上述独立副本上重现：

~~~powershell
dotnet .build/native-build/n3a-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll `
  --review .build/runs/n3a-repair-independent-review01/control-independent-architect-contract-rng-changed
~~~

当前实际exit0。该命令会写该隔离副本的trace-review.json；不应对历史原始证据目录运行带写回的review。

### 最小修复要求

1. 保存最后确认动作的不可变完整state，并将当前三个有限场景中动作完成后的`death`、`terminal-stamp`、`terminal-contract`、`tail-contract`绑定到该确认状态。
2. 使用结构化JSON比较，保留属性顺序等价。根据已核验的实际顺序，stamp与最后confirmed state相同；terminal-contract及Architect尾部只允许`lifecycle.native.Closed`从false变true。应构造这个明确的期望差异再比较，不忽略整个lifecycle、RNG或资源区域。
3. next-act尾部在OldActVoteControl确认后没有业务操作，完整尾部state应与该确认状态一致。保留开始context与结束房间不同的合法关系。
4. **不要把清场前后的原生回调快照强行相等。** OnEnded返回HP80与WinRun完成HP0属于真实业务转换；完整边界一致性只施加于原生任务结束且root已确认之后的静止观察区间。各回调既有专门契约、死亡原生残留及失败前缀都继续保留。
5. 将这4个反例及正常／属性顺序等价副本纳入实际生产门回归，冻结修复后源码／产物并完成4A、N2及Core适用回归。报告必须使用新冻结身份，历史结果继续保留。

这是一项R1收尾修复；R2无需重新设计，也不要求新增全场景schema、战斗仿真、策略或公开Mod接口。

## 后续与非阻断限制

本轮通过条件未满足，因此不生成已可开始的4B实施交接。既有[N3交接](NATIVE_RUN_N3_IMPLEMENTATION_HANDOFF.md)中的4B范围保留：room adapter、休息heal/smith/cancel、简单事件、首条自然多节点片段。待本项复审通过，再细化执行；4C–4D、5–6仍待办，六步授权继续有效。

原限制保持：一run一进程；TestMode死亡没有零售loss收尾承诺；尾部夹具没有打完前幕Boss或正常起点完整run；Architect展示桥只认证当前无UI语义，不代表零售演出／RNG等价；原生结果为NotCompared。没有触发真实Steam、游戏网络、用户存档或UI，没有改Mod、安装目录或实现，没有提交／推送。当前没有权限／环境阻塞，阻断项仅为上述可复现的状态认证缺口。
