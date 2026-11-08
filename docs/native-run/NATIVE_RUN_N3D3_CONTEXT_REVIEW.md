# 4D3 上下文修复独立复审

结论：**未通过，剩余一项动作分类/阶段门缺口。** 原 19+4 个反例已全部闭合；当前 act/node/完整房间引用绑定对实际登记的战斗动作有效。但生产检查器会接受未登记动作，也允许把普通出牌伪称为开局 `EnterAct`，从而绕过绑定。本轮保存了 6 个可复现误接受副本。

冻结：`98EE6B390F01A94AFB407F2D1C6576F59AE09A9D853EE1A2161CE5F5EA526FFC`。
Native DLL SHA256：`4450A8959E83B636F269889B08A87A5CE958FFDF2495EA77ECEA61F391A219A4`。
独立证据：`.build/runs/n3d3-context-independent-review01/`，入口为 `closeout.json`、`summary.json`。生产实现和原始结果未修改。

## 已成立的验证

- 独立重算 canonical build ID，核对 77 源输入及快照、48 编译输入、3 游戏输入、7 产物；15 Mod 输入和 52 兼容源码一致。675 项独立来源/结果/重放核对通过，结束时再次核查原文件保持。
- 同构建的 309 次原生执行、3908 项套件检查、Core98 保存日志及 Core DLL 身份一致。原始退出为 226×0、70×1、8×2、5×3。未将预期 Unsupported、预算、执行错误和数据错误当作全部获胜；本次没有重跑原生矩阵或 Core 测试。
- 本轮直接调用最终 DLL 的生产 reader **277 次**。239 个既有控制全部符合预期，包含原 23 反例拒绝；独立新增 38 次中，8 个正常/等价包接受、24 个坏证据或不完整包拒绝、6 个坏证据误接受。共 51 次合法接受、220 次预期拒绝、6 次误接受。
- 修改副本重建有效 seal，所有 reseal 成功；语义拒绝不依赖损坏哈希链。历史副本保留其原构建 ID，属于新 reader 的兼容审查，未算作新原生执行。
- 独立重读实际轨迹，3513 条 PlayCard/EndTurn/UsePotion 所属详细历史的位置符合派发上下文；155 条 Travel/MajorFixture 的开战历史确实在新房间产生，仍可合法通过。记录见 `location-facts.json`。
- 13 条真实 Death 路径维持输入→完成→根确认的有限原生转换；详细模式的最后历史与死亡输入相等。live/replay/minimal/off 的四份完整 sidecar JSON（含 RNG）一致。Unsupported35/35、99/100、故障3/4、预算4/4及导出DataError51/51前缀保持。

新增联动 act/node/context、跨动作旧房间引用、死亡阶段重标记等控制被拒绝。合法的完整属性重排、稳定嵌套扩展，以及含真实跨房变化的原始包仍通过。这些结果说明位置绑定本身可复用，剩余问题在决定哪些记录适用该绑定的入口。

## 必修：自报动作 kind 可以绕过战斗上下文门

位置：`tools/native-run/NativeMajorContract.cs` 的动作期间条件检查和 dispatch 分派；`NativeTraceContract.cs` 只保证 root-request、dispatch、confirmed 三份动作相等，没有验证本 profile 的动作种类和阶段。

`NativeMajorContract` 从 dispatch 的 `facts.kind` 读取 kind，仅在其值为 PlayCard/EndTurn/UsePotion 时启用不可变位置约束。分派逻辑没有穷尽未知种类并拒绝；`EnterAct` 也没有开局唯一阶段约束。因此只要三份记录共同换名，读者就不再把这段真实战斗历史视为战斗动作。

所有新反例来自当前冻结 `major-elite-1` 的第一张实际出牌（root a4，原 CARD.DEFEND_IRONCLAD），保留原伤害/格挡/能量、根序号、完整后续死亡和退出状态。

| 副本 | 改动 | 实际结果 |
| --- | --- | --- |
| `case-000` | root-request、dispatch、confirmed 的 action.kind 一起改为 `UnknownOperation` | exit0，trace-review.status=passed |
| `case-001` | 上述换名，另把该动作一条 combat-history 的 state.node 改成 `MapCoord (0, 0)` | exit0，passed |
| `case-002` | 三份 kind 一起改为 `EnterAct`，保留原出牌 decision | exit0，passed |
| `case-003` | 上述 EnterAct 换名，再改写单条历史 state.node | exit0，passed |
| `case-274` | 三份动作均改为 `EnterAct`，decision 全改为合法形状 `{"act":0}`，再改写历史节点 | exit0，passed |
| `case-275` | 三份动作均改为 `EnterAct`，decision 全改为 `{"act":1}`，再把一条历史 state.act 改成1 | exit0，passed |

后两包说明仅验证 decision 字段形状不足以修复。该动作发生在已完成开局、已进入战斗后的普通出牌位置，期间没有实际幕进入、跨幕或新房间流程。`Program.cs` 的真实 `EnterAct` 位于正常初始化后、Opening 与 MajorProbe 之前；它不在当前有限驱动的战斗决策中出现。

作为相邻拒绝对照，将同一动作改称 EventOption、Travel、MajorFixture、ActReady 已被各自生产契约拒绝。将死亡所属动作重标记为 EnterAct 也被既有死亡动作前提拒绝。本问题是普通战斗动作的分类入口绕过，未发现原死亡回调完整转换本身退化，也未发现原始原生执行错误。

### 最小修复范围

1. 在有限 Major profile 的生产分派入口显式接受已登记动作种类，未知 kind 失败关闭，不能作为未处理的根动作继续确认。
2. 给开局 EnterAct 等位置检查豁免动作绑定真实阶段和前置状态；EnterAct 只能属于正常初始化位置，不能在战斗内复用。原有 Travel/夹具/ActReady 的合法跨房/跨幕契约继续保持。
3. 在验证种类与阶段后选择位置规则；不要让未验证的自报 kind 决定是否启用上下文保护。按当前有限动作集合解决即可，无需引入公共 SDK 或新增宿主权限。
4. 将本轮 6 反例纳入实际生产 reader 回归；保留原23、合法 EnterAct/Travel/MajorFixture、正常战斗及死亡、故障前缀。覆盖未知种类、已知种类错阶段、完整动作副本共同改写及合法变化，不能只硬编码拒绝一个字符串。

## 复现和后续

只对审查副本运行，例如：

```powershell
dotnet .build/native-build/n3d3-context-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-context-independent-review01/case-001
dotnet .build/native-build/n3d3-context-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-context-independent-review01/case-275
```

当前均为 exit0 / passed；修复后应由种类或阶段语义门拒绝。完整变换在 `review.py`，每包有 `production-review.log`、`reseal.log` 和 reader 认证文件。脚本收集完成的 exit0 不代表验收通过；本轮独立 `closeout.json` 和最终 gate 明确未通过，gate exit1。

精英/Boss Victory、目标奖励和实际 act-ready 仍未验证；API夹具和真实死亡不能代替正常完整Run。补齐上述入口门、同构建验证并复审后，继续 [既有4D3后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md) 的合法资源准备与胜利/奖励/跨幕工作，随后步骤5–6。无需新增用户授权。

本次仅新增审查文档和隔离证据/辅助脚本，未修改生产实现、原报告、Mod、安装或用户存档；未启动UI、原生游戏执行或真实Steam/网络，未提交推送。无环境阻塞；当前验收阻断项为上面的可复现动作分类缺口。
