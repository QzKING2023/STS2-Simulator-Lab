# 4D3 战斗上下文绑定修复

本轮修复 [独立复审](NATIVE_RUN_N3D3_REPAIR_REVIEW.md) 的剩余 act/node 缺口。精英/Boss Victory、其奖励及实际 act-ready 继续未验证，整个 4D3/M1 尚未完成。

冻结：`98EE6B390F01A94AFB407F2D1C6576F59AE09A9D853EE1A2161CE5F5EA526FFC`，目录 `.build/native-build/n3d3-context-final01/`。Native DLL SHA256：`4450A8959E83B636F269889B08A87A5CE958FFDF2495EA77ECEA61F391A219A4`。构建 0 warning / 0 error，77 源输入、48 编译输入、3 游戏输入、7 产物。

本轮同构建本地验证完成，**待独立复审**。最终验收入口为本轮 `closeout.json`；该状态不代表整个 4D3/M1 完成。

## 有限上下文契约

每个真实 PlayCard、EndTurn、UsePotion 派发时，保存由 act、node、完整房间引用组成的不可变 JSON 投影，并保存所属 root。派发时仍检查该投影对应实际 `act:node:room-reference` context。动作期间所有带 state 的观察、死亡输入/完成以及根 confirmed 都必须与该投影相等；Existing Encounter/RoomType/shouldGiveRewards 等身份检查继续有效。

比较的是身份上下文，真实 HP、格挡、能量、牌区、伤害、资源和 RNG 变化继续由原生执行。房间引用按完整结构比较，新增嵌套字段不能被忽略。原死亡输入→完成→确认→终态的完整状态链及 Closed 契约保持。

Travel、MajorFixture、ActReady 不进入战斗动作的静止位置约束。尤其原生 Travel/MajorFixture 在旧房派发、进入新房并产生抽牌历史是合法变化，仍由原有房间/夹具/到达契约验证。不会将新房历史强行解释成旧房状态。

生产改动仅 `NativeMajorContract.cs`，新增可复用控制脚本 `major_context_controls.py`。共享 `NativeTraceContract.cs`、原生命令、宿主补丁、策略、seed、预算、Encounter 与输入锁均未调整。与上轮冻结的精确源码差异见 `source-delta.json`。

## 修复前后与回归入口

工作目录：`.build/runs/n3d3-context-work01/`。

- `before.json`：复制原四个反例后，旧 DLL 实际重现四次 exit0 / passed，未改原审查副本。
- `gate.json`：新 DLL 将四个反例全部拒绝，五个原始正常包仍通过。拒绝由 `Major immutable combat action location` 产生。
- `source-final01.json`、`frozen.json`：实际 dirty 源和输出版本锁；不只用 HEAD 声称源码身份。
- `final01-progress.json`：十组原生矩阵、Core 与上下文控制的真实退出。所有任务结束后才作最终收尾。
- `facts-final01.json`、`native-context-facts.json`、`outcomes-final01.json`：最终同构建、完整状态/RNG重放、真实死亡及失败前缀核对。

新增控制直接调用实际生产 reader，并在标记副本上重建有效 seal。它们包括历史、死亡输入/完成、确认、胜利回调中的 act/node/完整房间引用变更；详细/minimal/off 的联动死亡尾部变更；缺失/null/错误类型；旧反例及合法属性顺序、完整一致扩展、真实跨房历史。旧证据保留自身旧构建 ID，作为新 reader 兼容控制，不能算作新原生执行。

```powershell
python -P tools/native-run/major_context_controls.py --manifest .build/native-build/n3d3-context-final01/build-manifest.json --source .build/runs/n3d3-context-final01-major --output .build/runs/n3d3-context-reproduce-controls01
```

复现必须用全新输出目录。完整原生 Major 矩阵可用 `run-native-major.py`，指定同一 manifest 和全新目录；整个统一矩阵命令和日志保存于工作目录 `run-final01.py`、`final01-*.log`。

## 最终结果

最终十组原生套件全部符合预期：**309 次实际执行 / 3908 项套件检查，另有实际 Core98**。原始退出保持 226×0、70×1、8×2、5×3，未把不支持、预算或故障转为成功。12 个最终任务（十组原生、Core、上下文控制）全部完成后才生成本收尾。

追加 **239 次实际生产 reader 调用 + 5 项原生位置见证检查**，全部符合预期。包括原19误接受拒绝、上轮4新误接受拒绝、55原系统控制、79历史事件控制及其它独立复审副本；新增43次调用针对位置单点/联动/类型、五个正常最终包。所有重封装成功，拒绝不以无效哈希链为依据。另有最终Major套件95个原生产控制保留。各套件已有控制、追加reader与新原生执行分列，不重复计算原生次数。

722 项来源、构建、退出、重放与历史保持核对通过。77份最终源快照匹配dirty源码；15锁定输入及52兼容源码保持。3513 条真实战斗动作所属详细历史维持派发位置；155 条真实跨房开战历史保留合法新房变化；13条实际死亡输入→完成→确认转换通过。

与上轮相比，19条Major路径的四份完整JSON（route/choices/reward-choices/final-state，含RNG）逐份相等；本轮live/replay/minimal/off及失败前缀对照也相等。精英/Boss Death51/51、自然精英Death93/93、Unsupported35/35及99/100、包装故障3/4、预算4/4、导出DataError51/51保持。生产变更只位于reader，没有改原生命令或为胜利修改资源。

实际结果详见 `.build/runs/n3d3-context-work01/closeout.json`、`facts-final01.json`、`native-context-facts.json`，以及 `.build/runs/n3d3-context-final01-controls/summary.json`。历史修复失败、原独审误接受、旧输入和原始执行证据均保留。

## 限制与交接

当前策略仍为 `oracle-current-intent/defend10/attackfirst/v2`，不宣称公平策略或可靠胜率。Death 是合法终态证据；精英/Boss API 夹具和自然精英片段不等于正常开局完整通关。有限已登记宿主的状态契约不代表任意未知监听器支持。

修复验证完成后交独立复审，再按 [既有后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md) 继续内部能力组合、合法资源准备、实际精英/Boss 胜利及奖励/跨幕，再步骤5–6。原始成功、失败、不支持、预算和审查反例均保留。

仅 Lab 实现和隔离实验；无游戏 UI、真实 Steam/网络/用户保存、Mod 专项或提交推送。
