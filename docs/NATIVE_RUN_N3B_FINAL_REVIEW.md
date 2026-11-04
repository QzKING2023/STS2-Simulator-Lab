# M1 步骤 4B 最终独立验收

日期：2026-11-02。结论：**4B在当前已声明范围内通过独立验收。原R1/R2及退出开始快照遗漏均已闭合，可接续4C；没有剩余必修项或环境阻塞。** 全六步已有授权，接续实施无需用户逐步确认。

审查对象为[退出开始修复报告](NATIVE_RUN_N3B_EXIT_STATE_REPAIRS.md)、实际实现、最终冻结产物及原始结果。本轮没有修改运行实现，也没有新执行原生游戏场景。当前自然多节点片段与4A尾部夹具的证据范围保持，完整原版run仍需后续步骤。

## 冻结与独立证据

- Build ID：`C632744968AA67F2BF4E472681E2B0F5B285C49F748B9F33A3420AF3D78AD438`。
- Native DLL SHA256：`9992F72852960676F2CCA92E89EA5A61FA6834493FD8514E74DDA49F05A15325`。
- 构建：`.build/native-build/n3b-exit-state-final02/`。
- 本轮独立目录：`.build/runs/n3b-exit-state-independent-review01/`；主要证据为 `summary.json`、`boundaries.json`、`build-id-check.json`、`closeout.json`，复现脚本为 `review.py`。

独立重算Build ID，核对42份源码及其实际快照、25个编译输入、3个原生输入、7个产物，与manifest一致；15个锁定Mod输入、52个兼容源码保持。Core98既有驱动的实际执行日志、驱动文件hash及其Core DLL与冻结产物的关系均核实。本轮没有重编译或重跑Core。

## 根因修复核验

`NativeRoomContract.cs:168`在现有归属与顺序检查后，将退出开始快照与当前不可变`dispatch`做完整结构比较，随后才设置`exiting`。其比较器使用JSON结构相等，支持对象属性顺序等价，并对数组顺序、缺失/新增字段和嵌套值差异保持敏感。

核对`Program.Action`及`NativeRooms`两个调用点：派发后立即调用执行委托；委托先记录退出开始，随后才调用原生`Exit`或`EnterMapCoord`。因此该开始边界与派发快照相等符合真实调用顺序。返回状态仍绑定实际confirmation，不与派发前状态强制相等。此次生产逻辑差异仅为这一完整比较，原R1选择闭合、归属、顺序和R2返回/战斗清理/奖励完成逻辑没有放宽。

生产检查器实际重现结果：

| 集合 | 本轮执行 | 结果 |
| --- | ---: | --- |
| 原九个反例＋退出开始四个反例 | 13 | 全部exit1，在对应语义门拒绝；四个开始状态反例均报`Room exit start dispatch state mismatch` |
| 最终构建的房间控制 | 79 | 76个损坏拒绝，3个正常/等价接受 |
| 最终构建正常包 | 9 | 7个夹具与DEV_01/02自然包全部接受 |
| 新独立控制 | 14 | 12个损坏拒绝，2个等价接受 |
| 合计 | **115** | **0项偏离预期** |

新控制独立构造并重新封装合法chain/seal，没有复用生产mutator生成变更。它们覆盖后续及最后一次退出的金币/RNG漂移、串用先前动作快照、嵌套未知字段、永久牌数组次序、缺失/null/非对象快照、删除/重复开始事件、过期context，以及统一扩展字段中的局部漂移。统一增加相同嵌套扩展、仅改变对象属性顺序的等价副本通过。没有发现该明确退出契约内的同根因遗漏。

直接复核原反例之一（只读副本）：

```powershell
dotnet .build/native-build/n3b-exit-state-final02/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3b-exit-state-independent-review01/original-control-new-exit-start-player-rng-drift
```

预期exit1及上述完整状态诊断。正常/等价副本可用同入口读取，例如独立目录下`normal-natural-01`和`independent-common-extension-equal`，预期exit0。

## 正常边界、真实变化与计数

本轮重新计算并核对：28个选择/退出开始边界与各自dispatch完整相等；52个相邻结果、leave-ready、exited、complete与各自confirmation完整相等；10次真实Travel返回均发生合法状态变化，并与相应confirmation一致。9个正常包实际通过生产reader，因此校验没有将这些真实变化误判。细目保存在`boundaries.json`。

另完成188项独立元数据、版本、原始退出、快照、来源、结果和重放核对，全部通过。实施者的以下统计与实际证据一致：

- 34+16+74=124个原生进程，307+273+580=1160项suite检查；逐个核对process/result/trace header/manifest及预期退出。
- 原始退出为80×0、39×1、2×2、3×3，故障、Unsupported与预算停止按原分类保留。
- Core98实际通过；694项补充检查与最终冻结相符。
- 394份旧审查包的最终reader记录、退出预期和源包内容一致；输入包保留各自原构建身份。本轮从中重新执行13个必查反例，其余按记录及完整文件hash核对，不将394份包称为本轮新增原生执行。

DEV_01保持同一自然开局Run的71/71动作、7房间、3次原生战斗胜利、永久指定实例升级及下战Play。DEV_02保持138/138实际Death，HP0、Losses1、WinReturns0。DEV_03保持77/78未确认Travel前缀及WhisperingHollow Unsupported，`DataComplete=false`。三种子的route、choices、final-state保持原AF641业务结果；DEV_01/02冻结重放和代表性minimal/off对照的完整状态/RNG一致。

既有4A真实Death、原生跨幕与Architect尾部结果及负控仍在最终同构建矩阵中。尾部仍标fixture，自然片段仍标natural-run，native-only/NotCompared范围保持。首轮控制目录重名和只读汇总历史路径错误均有失败日志，最终统一结果来自final02，未混用首轮局部结果。

## 4C接续与限制

[4C交接](NATIVE_RUN_N3C_IMPLEMENTATION_HANDOFF.md)已更新为本次通过的冻结基线，依赖顺序仍为：奖励集合与根/子事务协议 → Gold/Card及连续/嵌套集合 → Relic/Potion取得 → Treasure生命周期 → Potion实际使用及自然集成。保留旧4B及N2回归profile，扩展场景使用自己的明确契约，继续保持退出开始/确认状态绑定和真实任务前缀。

4C–4D、自然跨幕完整run和批测交付尚未执行；未知事件、商店、完整精英/Boss路线等不因本次通过而获得支持声明。预测、策略强度与公开Mod接口仍按主路线分别推进。Death的一Run一进程边界和Architect展示桥限制继续适用。

本轮只新增独立审查证据和本文，并更新4C交接的状态/基线说明。未改实现、原始证据、角色、安装或真实存档，未启动UI或调用真实Steam/网络，未提交推送。交根线程立即接续已授权4C。
