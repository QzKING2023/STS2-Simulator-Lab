# 4D3 动作阶段修复独立复审

结论：**未通过；原 29 个反例已闭合，剩余跨房动作内部战斗历史的上下文绑定缺口。** 新动作集合/阶段门本身有效，普通战斗不能再伪报未知类型或开局 EnterAct 来绕过检查。但真正处于合法阶段的 Travel / MajorFixture，其内部历史可以记录错误的幕、节点或旧房间，并在确认时恢复；本轮 8 个坏副本仍被生产 reader 认证为 passed。

冻结：`17C6C990BAD45E733D0210C373BAE162BE319B709E4FE391700808C3CF922247`。
Native DLL SHA256：`D18E68C9143188FC00BFF205CF7C585DD76FE879898544B1A521B7A52B58E49C`。
独立证据：`.build/runs/n3d3-action-independent-review01/`，入口为 `closeout.json`、`summary.json`。

## 已成立的部分

- 独立重算冻结 ID，逐一核对 78 份源输入及快照、48 份编译输入、3 份游戏输入、7 份产物；15 个锁定输入、52 个兼容源码一致。677 项独立来源/结果/重放核对全部通过，原始退出及报告身份保持。
- 保存的同构建矩阵为 309 次原生执行、3908 项套件检查及实际 Core98 日志。原始退出是 226×0、70×1、8×2、5×3；本次没有重跑原生矩阵或 Core 测试。
- 本轮实际调用最终 DLL 的生产 reader **152 次**：原29反例全部拒绝，82个已有阶段负控全部拒绝，另有21个独立未知类型/阶段与位置联动/错误开局顺序控制拒绝，5个真实不完整结果不认证为成功；5个正常包及2个合法等价包接受。共 **137次预期拒绝、7次合法接受、8次误接受**。
- 独立新控制覆盖未知大小写/NUL/数值/空值/对象类型、合法参数形状的 EnterAct/Travel/RewardOffer/RoomOption 与整个战斗动作状态联合改写、插入 new-run 试图重置阶段、开局完成前移、死亡后重新进入。它们均失败关闭。合法属性重排和所有快照一致的扩展仍通过。
- 历史副本保留原构建身份，用于最终 reader 的兼容核验，未冒充新的本构建原生执行。修改副本均重新生成有效 seal；本轮拒绝不以损坏哈希链作为依据。所有 reader 调用都在独立副本进行。
- 3513条普通战斗动作历史维持固定位置，155条真实跨房历史仍可合法通过。完整route/choices/reward-choices/final-state（含RNG）在live/replay/minimal/off间一致。13条实际Death输入→完成→确认转换保持，Unsupported35/35及99/100、EngineError3/4、BudgetExceeded4/4、DataError51/51前缀保持。

## 必修：合法跨房类型的历史观察没有绑定实际到达上下文

`NativeMajorContract.cs:72`只对经过阶段验证的战斗动作启用CurrentCombat；Travel / MajorFixture的豁免本身是必要的，因为它们会在原生进入新房间时产生历史。但是第174行的combat-history分支对这两类动作仅核对根动作归属。第83行的合法地图决定、第109行的确认节点、第129行的到达记录，都没有反向约束该动作已记录的历史位置。共享`NativeTraceContract.cs:112`检查历史根/上下文信封及epoch/index，也没有把历史state位置与此次到达绑定。

因此，**“动作种类与阶段合法”没有推出“其中每条历史属于合法的新房间”**。根请求、派发、确认、room-entered、战斗开始、完整终态和seal均正确时，中间历史仍可被错归到别的幕、节点或房间。这会损害M1按节点/房间统计的可信数据。

以下全部来自最终构建的`major-elite-1`，不改动作种类、真实决定、请求/确认状态或终态：

| 副本 | 被改历史及错误值 | 生产结果 |
| --- | --- | --- |
| case-000 | Travel a3 首条CardDrawnEntry：act 0→1 | exit0 / passed |
| case-001 | 同一历史：node改为MapCoord (0, 0)，既非from也非to | exit0 / passed |
| case-002 | 同一历史：rooms.reference与majorCombat.room一起指向旧Neow房i14，实际新房为i33 | exit0 / passed |
| case-003 | MajorFixture a25 首条PowerReceivedEntry：act 0→1 | exit0 / passed |
| case-004 | 同一历史：node改为MapCoord (0, 0) | exit0 / passed |
| case-005 | 同一历史：两个房间引用一起指向旧Monster房i33，实际新精英房为i57 | exit0 / passed |
| case-150 | Travel a3全部5条历史共同改成act1、node(0,0)、旧房i14，确认恢复真实新房上下文 | exit0 / passed |
| case-151 | MajorFixture a25全部6条历史共同改成act1、node(0,0)、旧房i33，确认恢复真实新房上下文 | exit0 / passed |

这些不是合法Travel的“旧信封上下文、新房间state”差异。原始Travel的决定为(3,0)→(2,1)，历史发生在act0、房i33；夹具通过EnterRoomDebug进入精英，仍在act0/node(2,1)，历史发生在新房i57。错误副本写入了不可能的act1/第三个节点/旧房，并在确认时恢复。

独立逐条核对当前19条Major路径：**56次已确认跨房操作中的155条历史全部对应真实确认到达的act/node/具体房间及Encounter身份**，未发现必须容许上述错误值的合法路径。两个未确认Travel（Unsupported99/100原样本及重放）均没有战斗历史，仍保留失败前缀。详见`transition-facts.json`。此处只检查位置与战斗身份，不要求历史中变化的资源、RNG或整份state等于最终确认。

## 最小修复建议

1. 保留已经闭合的有限动作类型、阶段和普通战斗位置门。在Travel / MajorFixture的原生跨房区间，为战斗历史增加**本次实际到达上下文**的归属检查；可以暂存历史的位置/战斗身份，待根确认及到达记录出现时闭合，也可以使用明确的实际房间进入阶段记录。
2. Travel中本幕保持不变，历史节点必须对应本次合法目的地，具体房间绑定实际新房；MajorFixture按该API实际语义保持幕/节点，新房与本次声明、真实确认、Encounter对应。未来若实际退出阶段也会产出旧房历史，需要明确区分所属阶段，不能用整个动作无约束的豁免处理。
3. 不把全部历史state强行等同旧dispatch或最终确认；保留原生开场中的抽牌、力量、资源及RNG真实变化。修复的是位置/身份的证据关联。
4. 将8个新副本及合法跨房、真实状态变化、原29反例、失败/预算前缀纳入同构建验证，再独立复审。无需扩大公开SDK、Mod适配或宿主权限。

## 复现与后续

本轮`review.py`保存完整变换；每包有production-review.log、reseal.log和实际trace-review.json。例如仅对审查副本运行：

```powershell
dotnet .build/native-build/n3d3-action-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-action-independent-review01/case-150
```

当前返回exit0并认证passed。收集脚本正常结束只代表审查执行完毕，最终`final-gate.json`明确未通过。

精英/Boss Victory、目标奖励、实际act-ready与正常开局完整run仍未验证；首场Monster胜利/奖励及真实Death不替代这些门。补齐上述有限缺口后，继续[N3D3既有交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md)及M1步骤5–6。

本轮只新增独立审查文档和隔离证据/辅助脚本。生产代码、原始证据和报告保持不变；未运行游戏UI或原生矩阵、未触及真实Steam/网络、用户存档、Mod、安装、提交或推送。没有环境阻塞；阻断项是上述可复现的证据误接受。
