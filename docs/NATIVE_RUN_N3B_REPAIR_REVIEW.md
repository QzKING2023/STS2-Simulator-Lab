# M1 步骤 4B：R1/R2 修复的独立复审

日期：2026-11-02。结论：**原九个反例已闭合，R1 通过；R2 尚有一个完整状态绑定遗漏，4B 暂不通过最终验收。** 这是同一生命周期契约内的最小修复，不是新增节点或扩展 M1 范围。无需重新申请用户授权；由实施回合修复后继续独立验收和 4C。

## 审查对象与方法

- [原审查](NATIVE_RUN_N3B_REVIEW.md)、[实施修复报告](NATIVE_RUN_N3B_REVIEW_REPAIRS.md)。
- 冻结 Build ID：`830880E39BB8D6F59E503B87BF798EF0ABFFF912FB821F033400650E06F33B9E`。
- Native DLL SHA256：`3702EF7BEF0A7E5D59B6D949568849EA6A9F915FDA074C5CD712B4922ABC31E8`。
- 构建目录：`.build/native-build/n3b-repair-final01/`。
- 独立证据：`.build/runs/n3b-repair-independent-review01/summary.json`、`build-id-check.json`、`exit-start-proof.json`；脚本为同目录 `review.py`、`reseal.ps1`。

本轮只读取实现和原始结果，运行最终生产 DLL 的 `--review` 入口；没有执行新的原生游戏场景。损坏控制在独立副本上修改并重新计算合法 chain/seal，使判断到达实际语义校验。原始 1,524 个证据文件前后 hash 不变。未改运行源码、原始结果、角色或游戏安装；未调用真实 Steam、网络、用户存档或游戏 UI，未提交推送。

## 已通过部分

### 原九个反例与选择生命周期

最终生产检查器实际拒绝原审查的九份坏证据，返回 exit1，原因来自语义门而非破坏封装：

1. 删除全部选择结果边界。
2. 重复选择结果边界。
3. 将旧取消答复借给新升级动作。
4. Travel 返回快照金币与确认快照矛盾。
5. 战斗清理前事件脱离实际根动作。
6. 战斗清理完成事件脱离已确认根动作。
7. 战斗清理完成快照漂移。
8. 奖励完成事件脱离已确认根动作。
9. 奖励完成快照漂移。

`NativeRoomContract.cs` 已保存选择操作的 request/session/room/root/context 与独立 return/confirmed 快照。结果边界必须恰好一次，对应当前选择；下一请求、离房和成功收尾均检查闭合。补充的末个边界缺失、旧根动作、return 晚于 confirmed，以及取消后多个记录一致篡改金币等控制，均被生产门拒绝。正常取消、指定非零实例升级与真实状态变化继续通过。

Travel 返回→confirmed、战斗清理完成→对应 confirmed、奖励完成→对应 confirmed 的完整状态与归属修复成立；新增清理提前、奖励 facts 与真实金币矛盾控制也被拒绝。不能据此推断所有相邻静止观察点都已绑定，下面的遗漏仍需修复。

## 必修：`room-exit-start` 尚未绑定派发时的完整状态

位置：`tools/native-run/NativeRoomContract.cs:164`。该分支检查当前 root/context、动作类别、房间完成与退出顺序，然后设置 `exiting=true`；没有比较该事件的快照与不可变 `dispatch` 快照。通用 `State()` 只检查结构和部分房间语义，无法替代完整状态相等。

这不是原生业务过程中允许的资源变化：两个实际调用点都在执行原生退出/Travel **之前**记录 `room-exit-start`：

- `tools/native-run/NativeRooms.cs:97`：`RoomExit` 派发后记录开始，随后才调用原生 `Exit`。
- `tools/native-run/NativeRooms.cs:155`：`Travel` 派发后记录开始，随后才调用原生 `EnterMapCoord`。

这两个边界之间没有原生业务动作，应保留同一完整状态。独立核对 DEV_01/02 的 10 个真实 exit-start，以及 7 个正常夹具 exit-start，全部与所属 dispatch 结构相等。原 24 个正常返回/清理/奖励静止边界仍相等。

以下四份副本修改了唯一一个 exit-start 快照；所属 dispatch、后续执行结果和其余状态保持原样，最终生产检查器却全部返回 **exit0**：

| 独立证据目录（相对于独立审查目录） | 矛盾 | 实际结果 |
| --- | --- | --- |
| `control-new-natural-01-exit-start-gold-drift` | 自然 Travel a24：金币从 266 改成 999，dispatch 仍为 266 | 错误接受 |
| `control-new-room-heal-1-exit-start-gold-drift` | 夹具 RoomExit a5：金币从 249 改成 999，dispatch 仍为 249 | 错误接受 |
| `control-new-exit-start-extra-state-field` | 仅 exit-start 添加 `futureField` 对象，完整结构不一致 | 错误接受 |
| `control-new-exit-start-player-rng-drift` | 仅 exit-start 的玩家 Rewards RNG `s0` 翻转一位 | 错误接受 |

详细原值、变更值与所属 root/context 见 `exit-start-proof.json`。同一入口对 exit-start 脱离根动作的控制能正确拒绝，进一步定位为**状态未绑定**，而非整个分支未执行。

直接复现（只读取已生成副本，不初始化新的原生场景）：

```powershell
dotnet .build/native-build/n3b-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3b-repair-independent-review01/control-new-natural-01-exit-start-gold-drift
dotnet .build/native-build/n3b-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3b-repair-independent-review01/control-new-exit-start-player-rng-drift
```

### 最小修复要求

1. 在生产 `room-exit-start` 分支完成现有归属/顺序检查后、推进 `exiting` 之前，将必需快照与当前动作保存的不可变 `dispatch` 做完整结构比较。可与 `room-option-start` 已有的完整开始状态检查共用窄辅助方法，不逐字段补 gold/RNG 特判。
2. 保留现有 R1 请求闭合和 R2 owner/return/confirmed 校验。首次入房没有旧房间 exit-start 的 Travel 仍应合法。
3. 不要求 exit-return 等于 dispatch：原生 Exit/Travel 中的真实清理、换房、资源和 RNG 变化属于执行结果。也不将战斗清理前快照强行等同派发前快照。
4. 将上述四个控制及正常开始边界纳入生产检查器回归；正常副本、属性顺序变化的等价副本、合法取消/升级和真实业务清场继续通过。
5. 修复后重新冻结，在同一最终产物上复跑适用的 1–4B 矩阵及历史控制，保留本轮失败证据；15 个输入与 52 个兼容源码继续固定。无需扩展 4C、未知事件或 Mod 功能来关闭此项。

## 独立执行与冻结核对

本轮共 **394 次生产 reader 执行**：

| 集合 | 次数 | 结果 |
| --- | ---: | --- |
| 原独立审查包 | 306 | 全部符合预期；包括原九个反例现在被拒绝 |
| 本次实施者新增 room 控制 | 66 | 全部符合预期（63 损坏、3 等价） |
| 当前正常场景包 | 9 | 全部接受（7 夹具、2 自然片段） |
| 新独立控制 | 13 | 11 损坏中 7 拒绝、4 误接受；2 正常/等价接受 |

另有 **217 项**独立元数据、原始结果、完整状态和重放检查，全部通过；`exit-start-proof.json` 补充记录 7 个夹具开始边界。`summary.json.status=must-repair` 与 4 项 reader mismatch 是结论依据，不能用审查脚本自身的 exit0 覆盖它。

冻结及历史结果复核：

- 独立重算 Build ID 一致；42 source、25 compiled、3 native inputs、7 artifacts 全部 hash 一致。
- 最终原生矩阵 34+16+74=**124** 进程，281+273+580=**1,134** 检查；逐个核对实际 `process.json`、`result.json`、trace header、构建清单和预期原退出码。退出分布为 80×0、39×1、2×2、3×3；受控失败、Unsupported 和预算停止没有被归为普通成功。
- **Core98** 的既有实际执行日志及 Core DLL 身份一致，本轮未再次执行 Core；**582** 补充检查与最终构建一致。
- 306 份历史包包含旧冻结原生证据，由当前 reader 复核；不能称为当前构建重新执行的 306 次原生场景。
- 15 个锁定 Mod 输入和 52 个兼容源码 hash 不变。相对上一冻结，编译 C# 仅生产检查器及控制文件发生修复，未改实际房间执行代码。

## 自然片段与范围保持

- DEV_01：71/71 动作，同一自然开局 Run 的 7 房间、3 次真实战斗 Victory、事件与指定实例休息升级，进入后续战斗 Play；原永久实例及其战斗副本关系保持。结果仍是 `Ongoing/SliceBoundary`。
- DEV_02：138/138 动作，原生致死，HP0、Losses1、WinReturns0，`Death/Completed`。
- DEV_03：77 确认/78 派发，进入 WhisperingHollow 后 `Unknown/Unsupported`，保留未完成 Travel 和真实前缀，`DataComplete=false`，没有绕过该事件。
- 三种子的 route、choices、final-state 与修复前真实业务结果一致。DEV_01/02 冻结重放，以及 DEV_01/Heal/Phases 的 detailed/minimal/off 对照保持；比较对象包含完整 RNG 状态。
- 运行证据仍为原生无 UI、无 Mod、无 CombatSolver、MemorySaveStore；保留 native-only/NotCompared 的范围，不变成独立模拟器差分认证。

[4C 交接](NATIVE_RUN_N3C_IMPLEMENTATION_HANDOFF.md) 的奖励会话→Relic/Potion→Treasure→Potion 使用依赖仍可执行，但应先关闭以上一个最小必修。4C–4D、步骤 5–6 尚未执行；当前片段和 4A 尾部夹具均不等于正常开局完整通关。

没有权限、连接或挂起命令阻塞。本轮停在已获得具体实证的验收缺口，交根线程安排修复，随后继续既有六步授权。
