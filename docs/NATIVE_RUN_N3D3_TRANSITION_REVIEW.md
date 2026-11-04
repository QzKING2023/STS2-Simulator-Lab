# 4D3 跨房历史绑定修复：独立复审

结论：**本次跨房历史上下文修复通过，未发现当前验收契约内的必修缺口。** 原8个误接受副本现被拒绝，前29个反例和阶段负控保持有效。可接续精英/Boss胜利、实际奖励及跨幕工作；本结论不代表4D3或M1完成。

审查冻结：`53186436B11E78EED9FFFAF8214EF68A960D28B513EE04151BFD70557E392188`。
Native DLL SHA256：`CAD4C5DBCB071E1D0BE3237A83ED3F693AF027D66F3F3A26750E1737C4A18261`。
独立证据：`.build/runs/n3d3-transition-independent-review01/`；入口为`closeout.json`、`reader-summary.json`和`verification.json`。

## 复核到的根因修复

`tools/native-run/NativeMajorContract.cs`保留有限动作类型和阶段门。Travel / MajorFixture派发后登记本根动作的历史集合；每条历史绑定派发幕号和实际目的节点，并要求历史所指为新CombatRoom，`rooms.reference`与`majorCombat.room`相同。确认时将历史中的位置、完整不可变战斗身份与到达状态比较，随后由必需且唯一的`room-entered`绑定该完整确认快照。完成检查不允许残留未确认历史集合。

不可变身份比较只排除固定版本中的`inProgress`和`intentAttackers`；Encounter、类型、奖励资格、房间引用及新增嵌套身份字段均参加比较。历史位置仍明确为act/node/具体room引用。本次未将资源、手牌、牌堆或RNG强行固定为派发或最终状态。

从实际执行代码另行核对：Travel调用原生`EnterMapCoord`；MajorFixture明确调用`EnterRoomDebug`，仍为API夹具；观察器在原生history记录入口获取状态。本轮变化只在数据检查器及控制脚本，没有改变上述业务调用或资源规则。

## 实际生产检查器复验

在全新的独立目录对副本调用冻结DLL的`--review`，共 **292次**，全部符合预期：269次拒绝，23次接受。没有修改生产实现或原始证据，也未重新运行原生战斗矩阵。

- 重放实现轮240个控制：包含原8个跨房反例、前29个反例、82个阶段负控、21个前轮独立组合、76个本次绑定控制，以及正常/不完整/等价副本。
- 独立新增38个负控：Travel与MajorFixture中全部历史和确认记录联动更改act/node/room/Encounter/type/奖励资格/嵌套身份；进一步同时更改到达记录；交换另一战斗身份；历史移至确认后；缺失或重复到达；重新归属根动作。
- 这38项中37项由实际生命周期或状态语义门拒绝，1项使用尚未登记的未来房间引用，由悬空身份门拒绝；不将后者单独称为到达绑定证明。
- 独立新增2个等价副本、7个当前正常/重放/最小观察副本均通过。5个Unsupported、执行错误、预算停止及数据错误包均不能被认证为完整成功。
- 所有新增修改副本都成功重新封装；拒绝没有依赖损坏hash链或未知schema。所有23个接受结果实际生成`status=passed`的reader输出。

`review.py`保存精确变换，每个`case-*`含副本和`production-review.log`。复现时对该审查副本执行：

```powershell
dotnet .build/native-build/n3d3-transition-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-transition-independent-review01/case-000
```

该例是原8反例之一，预期exit1。需要重跑全部独审时应使用新输出目录，保留当前证据。

## 原生变化与冻结证据

独立重新计算manifest哈希，并核对79份源输入、48份编译输入、3份游戏输入、7份产物和全部dirty源码快照；15个锁定Mod输入、52份冻结兼容源码未变。共 **896项**来源、执行、上下文和重复性核对通过；审查期间3738个原始文件的保全哈希未变。

同构建十组结果确为 **309次原生执行、3908项套件检查及实际Core98**。原始退出码保留为226×0、70×1、8×2、5×3；本轮只复核这些既有执行证据，没有把历史控制重读计作新增原生执行。

逐一检查了56次已确认跨房动作，其中30次含155条真实跨房历史，包含150条CardDrawnEntry。这些历史均对应本次实际到达的act/node/房间和战斗身份。30次含历史的跨房动作均存在真实RNG变化；对三个主要详细样本的中间帧，phase有42处差异，hand/draw各34处，energy有2处。它们在新检查器下合法通过，证明没有通过要求历史完整state等于确认state来消除原生抽牌和资源演化。

live/replay/minimal/off的route、choices、reward-choices和final-state按完整JSON结构比较，包括完整RNG状态。失败样本的冻结重放前缀保持：Unsupported35/35及99/100、EngineError3/4、BudgetExceeded4/4、DataError51/51。13个真实Death边界仍满足HP0、原生loss回调及确认状态转换；夹具目标Death51/51、自然精英Death93/93均没有改称胜利。

## 后续与限制

本次通过的是已触达有限profile的跨房历史关联契约，不是独立重演所有游戏规则，也不是对任意整体重写日志的真实性证明。当前原生数据里的跨房history均发生在新战斗内；将来若出现旧房退出history，需显式区分原生阶段，不能恢复宽泛豁免。

**精英/Boss Victory、目标奖励、实际act-ready和正常开局完整run仍未验证。** 首场Monster胜利、真实Death和尾部/API夹具均不替代这些门。下一实现回合以本次已审冻结为基线，按[N3D3后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md)组合现有原生节点能力，保留失败样本，依靠合法准备闭合精英/Boss胜利→奖励→下一幕，再推进M1步骤5–6。旧交接列出的首批冻结仅为历史来源。

本轮只新增独立报告与隔离审查副本/脚本；无生产代码修改，无游戏UI、真实Steam/网络或用户存档调用，无Mod/安装修改，无提交推送。没有环境或权限阻塞。
