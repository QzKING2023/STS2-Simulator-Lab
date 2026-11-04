# 4D3 修复独立复审

结论：**尚未通过；原 19 个反例已闭合，剩余一项战斗上下文绑定缺口。** 本轮发现 4 个新的可复现误接受副本。真实精英/Boss 夹具死亡和自然精英死亡证据继续成立，没有发现原始执行伪造；精英/Boss Victory、对应奖励、实际 act-ready 仍未验证。本结论不关闭 4D3 或 M1。

审查冻结：`004B16F2E83E94C9CD546E0A4082B412259209CE53849C01548E01AC35AA8ECC`。
Native DLL SHA256：`A50034525C00DC4AA75341F5A823F264B0A727ACFF552822B7EFC299BA367499`。
独立证据目录：`.build/runs/n3d3-repair-independent-review01/`，入口为 `closeout.json`、`summary.json`。

## 已实际复核

- 重新计算冻结 canonical SHA256；76 源输入及其快照、48 编译输入、3 游戏输入、7 产物一致。15 个 Mod 锁定输入、52 个兼容源码未变。670 项独立来源、结果及重放核对通过，最终再检查原文件保持。
- 核对同构建的 309 次原生执行、3908 项套件检查，以及 Core98 保存日志和对应 Core DLL。实际退出分布为 226×0、70×1、8×2、5×3；Unsupported、执行故障、预算和数据故障没有被外层成功掩盖。本轮未重新运行原生矩阵或 Core 测试。
- 直接调用最终 DLL 的生产 `--review` **196 次**：原 19 反例全部语义拒绝；修复报告的 55 个系统控制符合预期；79 个历史事件控制符合预期；独立新增 43 次中，8 个正常/等价包通过、31 个坏证据或不完整包拒绝、4 个坏证据误接受。
- 修改副本均重新生成有效 seal；未以破坏哈希链代替语义拒绝。reader 仅写入新的审查副本。历史副本保留自身旧构建身份，是新 reader 的兼容审查，未计作新原生执行。
- 13 条真实 Death 路径的 ProcessPendingLoss 输入→完成→根动作确认符合有限宿主契约。详细模式的最后原生历史状态等于死亡输入；minimal/off 不虚构详细历史。所有 live/replay/minimal/off 的 route、choices、reward-choices、final-state 完整 JSON 对照一致，含完整记录的 RNG。
- 原错误前缀保持：Unsupported 35/35、99/100，包装故障 3/4，预算 4/4，导出 DataError 51/51。reader 拒绝将这些不完整包认证为完整执行，未改变其实际原始结果。

原 R1 的回调输入/完成完整状态、终态 facts；原 R2 的 root/context 字符串归属；原 R3 的夹具声明、动作和实际 Encounter/房间引用关联，均在原反例上得到修复。以下缺口涉及这些关联的上下文来源，不能仅凭原 19 反例全拒绝关闭。

## 必修：战斗快照中的幕号/节点没有绑定所属动作上下文

位置：`tools/native-run/NativeMajorContract.cs:24`、`:44`、`:119`–`:135`；共享归属检查在 `NativeTraceContract.cs:113`。

当前逻辑在 dispatch 时核对 `act:node:room-reference` 等于动作 context；后续 `CurrentCombat` 只核对 CombatRoom、room 引用及 majorCombat 的 room/encounter/type/shouldGiveRewards。历史记录自己的 root/context 字符串也必须等于当前动作，但其 **state 中 act、node 可以与这个 context 矛盾**。

这会使详细历史锚点本身携带错误的位置；死亡回调与尾部虽然完整相等，仍可一起继承这个错误位置。

| 副本 | 修改 | 实际生产结果 |
| --- | --- | --- |
| `case-178` | 一条真正 PlayCard/EndTurn 所属 combat-history 的 state.node 改为 `MapCoord (0, 0)`，root/context、实际 dispatch 和房间引用不变 | exit0，trace-review.status=passed |
| `case-179` | 同类单条历史 state.act 加 1，其余不变 | exit0，passed |
| `case-194` | 最后一条历史、两个死亡回调、根 confirmed、death、terminal-stamp、terminal-contract 的 state.act 一起加 1；真实派发仍在 act0/context0，期间没有幕转换 | exit0，passed |
| `case-195` | 同一尾部链全部 state.node 一起改为 `MapCoord (0, 0)`；真实派发、根上下文及实际房间引用不变，没有 Travel | exit0，passed |

四个副本都来自本次最终构建的 `major-elite-1`，不是旧构建混用。联动副本保留原生命令、伤害、HP0、Losses1、Closed 变化和所有终态记录，仅制造同一战斗动作内部不可能发生的位置跳变。生产 reader 不仅返回 0，还写出了 passed 认证文件。每个目录保留 `production-review.log`、`reseal.log` 和完整证据副本；精确变换见 `review.py`。

### 最小根因修复

在当前有限 Major 战斗 profile 内，将战斗动作的**完整身份上下文投影**（act、node、具体 room 引用）保存为不可变锚点，并把每条适用战斗历史及死亡输入/完成快照都绑定到它。可以集中扩展 `CurrentCombat`，避免只在四个例子或测试收集器中特判。现有完整回调→确认→终态绑定保留；幕号/节点不能靠相互一致的错误快照自证。

此处不要求将整个战斗前后快照冻结：真实 HP、格挡、能量、牌区、RNG 等战斗变化必须继续允许。也不能将“根 context 必须等于当前 state 位置”无条件套给 Travel/MajorFixture；它们在旧房派发、在新房产生开战历史，属于合法跨房变化。

本轮独立读取当前 19 条 Major 路径中的 **3513 条真正 PlayCard/EndTurn/UsePotion 所属详细历史**，其 act/node/room 与当前动作 context 全部一致，act/node/floor 也都保持该动作派发值。证据为 `history-location-facts.json`。因此这个修复可有明确有限适用范围，不需要扩大公开 API 或新内容支持。

将上述 4 个反例及对应单点/联动正负控加入生产检查器回归；保留 Travel/MajorFixture 的真实新房历史、合法战斗数值与 RNG 变化、minimal/off 无详细历史边界、原 19 与旧事件控制。修复后重新冻结并核验同构建结果，再进入功能推进。

## 复现

以下命令只读取新审查副本并写其 review 结果，不启动游戏宿主：

```powershell
dotnet .build/native-build/n3d3-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-repair-independent-review01/case-194
dotnet .build/native-build/n3d3-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-repair-independent-review01/case-195
```

当前均 exit0；修复后应 exit1，并报告所属战斗上下文不一致，而非 seal 错误。审查收集脚本 exit0 只表示收集完成；本轮 `closeout.json` 和独立最终 gate 明确为未通过，gate exit1，不能以收集器退出状态关闭问题。

## 保留边界与后续

正常原生 Death 证据、原始失败样本和修复前后证据均保留。当前 reader 契约是固定版本、有限宿主/监听器范围；它不是对任意篡改的密码学真实性证明，也不是所有卡牌效果的独立规则验证。保留单进程一个 Run、MemorySaveStore、SaveProgress/replay 拦截、无真实 Steam/网络/用户保存和无 UI 边界。

修复这一有限缺口并复审后，按 [4D3 后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md) 继续内部能力组合、合法资源准备、实际精英/Boss Victory→奖励→act-ready，再推进步骤5–6。既有交接仍可执行，但本轮不以死亡或夹具代替上述实证，也不启动下一实现阶段。

本轮仅新增独立审查文档与隔离证据/审查辅助脚本；未改生产实现、原始报告、Mod、安装目录或用户存档，未启动 UI、提交或推送。无外部执行阻塞；当前阻断验收的是上述可复现生产数据门缺口。
