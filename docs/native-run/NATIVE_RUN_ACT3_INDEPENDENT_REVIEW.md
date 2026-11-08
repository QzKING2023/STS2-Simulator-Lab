# 第三幕自然流程独立审查

2026-11-03。结论：**本轮验收暂未通过，剩余一项生产检查器必修。原始自然执行与重放事实已核实。** 本次只审查实现和原始证据、运行冻结检查器及隔离审计 API；未修改生产实现，未重跑原生完整矩阵。

冻结为 `2EE02774AE02C77D25AA310382F08C5930580987D78F4DB7D70649A01A664FD3`。独立证据在 `.build/runs/act3-independent-review01/`；完整结果见 `closeout.json`。

## R1：Tanx 取得实例的登记 model 没有绑定实际模型

`NativeMajorContract.cs:126` 的身份字典仅保存 `id → type`。第三幕取得检查在第331行核对这个 type 与选择键；`NativeThirdAncientContract.cs:29` 另行核对取得状态中的 model。两处均未核对该实例原始 `identity.detail.model`。因此单条身份记录与实际取得状态矛盾时，检查器仍给出成功。

两个独立反例均只修改第5461条 identity 记录中的一个 model 值，保留动作、选择、业务状态、RNG、结果及所有其他身份字段。副本通过生产 reseal 工具重新封签，再调用冻结的生产 `--review`：

| 副本 | 原始取得事实 | 唯一业务记录修改 | 实际结果 |
| --- | --- | --- | --- |
| `controls/case-00` | a369、i787、MeatCleaver / `RELIC.MEAT_CLEAVER` | identity.model 改为 `RELIC.WAR_HAMMER` | reader exit 0，误接受 |
| `controls/case-01` | a369、i787、ThrowingAxe / `RELIC.THROWING_AXE` | identity.model 改为 null | reader exit 0，误接受 |

`case-00-diff.json`、`case-01-diff.json` 记录逐 payload 对比，均仅一个非 footer 记录改变。它们不是对整份日志的协同伪造，也没有修改原始执行证据。当前原始两份记录的 type/model/取得状态实际一致；此缺口影响可信数据检查，不是原生取得失败的证据。

最小修复：保留身份登记的模型字段，在本次 Tanx 新遗物取得处，将登记 id、具体 type、非空 model、当前选择键及实际新增持有实例绑定。可复用现有身份契约，避免增加一套业务效果模拟。普通非 AbstractModel 对象仍可合法登记 null，不应全局禁止。补齐错误、缺失/null model、正确两种实际取得及等价副本控制。

该修复若仅影响检查器，按分层约定重验本轮两个反例、针对性正负控与完整冻结原始证据集即可；原始生成链未变时不需再跑全部原生进程。最终 M1 同构建全量门继续保留。

复现入口（使用已生成的独立副本，不覆盖原证据）：

```powershell
dotnet .build/native-build/act3-dev04/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/act3-independent-review01/controls/case-00
dotnet .build/native-build/act3-dev04/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/act3-independent-review01/controls/case-01
```

## 已独立核实的范围

- 594份既有证据全部按预期通过生产检查器重验：343接受、251拒绝，包含407份原始执行记录与187份控制。完整计划、原始绑定及逐次退出在 `plan.json`、`input-lock.json`、`reread-summary.json`，没有覆盖原报告。
- 新增22个独立生产控制：19个坏副本中17个按语义拒绝、上述2个误接受；3个正常/等价副本通过。覆盖任务完成顺序、当前选择请求、嵌套完整状态、RNG字段、奖励房间归属及必需返回、minimal 胜利条件归属、死亡与跨房历史、非法阶段和展示隔离。结果在 `controls/summary.json`；控制脚本退出1准确保留了未通过状态。
- 122项独立事实核对全部成立。15个最终构建原生进程的实际结果为6 Death、9 SliceBoundary，均保持 natural-run 来源、原始退出和确认前缀、隔离证据及 native-only / NotCompared。完整范围与实际开始/结束时间在 `facts.json`。
- 有限第三幕场景从同一正常开局运行至387动作：a368原生进入Tanx并执行A0回复，a369实际选择MeatCleaver；a386首战胜利，a387原生奖励，最终HP32、金币525。
- 连续场景实际选择ThrowingAxe；第三幕两战分别在a386、a405胜利，a387、a406完成奖励，HP47/26、金币525/539。a418真实敌方回合导致Fabricator战斗死亡；死亡回调、确认、终态都属于当前房间，HP0，无致死战奖励或RunVictory。
- 两条第三幕路径的前367条动作及734个完整派发/确认状态，与原第二幕成功路径完全一致。连续路径 live/replay/minimal/off 的836个完整投影、全部动作/选择/奖励选择/最终状态相等；有限场景774个状态重放相等。比较包含run/player RNG各流的counter及s0–s3。原生ThrowingAxe在三个第三幕战斗共有12条PlayCount=2的出牌开始/完成历史，未合成牌效。
- 冻结清单的94源码、58编译输入、3游戏输入、7产物以及94份源码副本已核验；15锁定Mod输入、52兼容源码均未改变。原始文件锁复核通过。Core98仅复用已说明的同源码历史证据，本次未重新执行。

## 无界面展示桥与失败保留

检查锁定版本 `StunIntent.GetStaticHoverTip` 的实现与新补丁：原方法只建立本地化提示及图标，展示桥保留LocString并返回null纹理，避免AtlasManager/Godot文件接口。仅注册第三幕场景，并要求TestMode且无NGame实例；没有接管Tanx随机选择、遗物取得或战斗效果。

独立重新运行已有冻结审计探针：未知Harmony前缀实际织入后，生产PatchMethod拒绝；移除后允许安装；安装后的未知前缀由Ensure拒绝，移除后回到预期的未启动隔离门。目标业务调用0次，原始退出0，见 `tip-audit.json`。这是审计API证据，不是零售展示或战斗测试。

首次真实崩溃记录仍保留：rawExit `-1073741819`，367确认/368派发，StunIntent→AtlasManager→Godot.FileAccess堆栈，缺少result/seal按预期拒绝。不能把第368步未确认后缀声称为无副作用。

审查期间一次只读命令创建遭遇exec连接断开，命令未执行；现有控制进程随后正常落盘且退出1。Get-Location确认恢复后继续收尾，没有重复启动测试、修改连接安全设置或绕过审批。

## 后续边界

R1修复并复审后，继续 [第三幕交接](NATIVE_RUN_ACT3_NEXT_HANDOFF.md)。第三幕Boss胜利、最终Architect及正常开局RunVictory尚未运行；第四步自然房间组合仍有缺口，第五步未完成。状态比较覆盖声明投影，尚未覆盖Surrounded.Facing及部分遗物计数/使用状态；这些保留为M1必要数据门。没有全角色、全种子、可靠胜率、零售展示等价或完整M1通过结论。

本轮无UI、真实Steam/网络或用户存档操作，无Mod改动、提交或推送；生产实现及原始结果保持不变。
