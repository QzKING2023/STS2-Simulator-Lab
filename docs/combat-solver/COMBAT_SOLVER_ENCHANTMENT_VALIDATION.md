# CombatSolver 附魔动作映射：定向验证与明确停点

2026-11-04。第 5 步继续推进中的有界批次；**M1 未完成**。本轮没有提交或推送。

## 结果

| 项目 | 实际结果 |
| --- | --- |
| 原失败门 | Lab 的通用动作字段门拒绝所有非空 `CardEnchantmentId`；上游已有 Swift 镜像，拒绝发生在原生派发之前 |
| 通用映射修复 | 显式版本化的完整卡牌身份协议；复用固定上游注册表、完整 StateKey 和实例 occurrence，不按卡名特判 |
| Swift 真实动作 | q516，原生打出带 Swift 的 Hellraiser；费用、额外抽牌、状态、完整 continuation/RNG 差分一致 |
| 身份控制 | 实际原生解析器 6 个错误动作均在额外派发前拒绝，原状态/RNG 不变；生产证据绑定器 1 正控 + 17 负控通过 |
| 后续在线动作 | q517、q518 两次 Strike 差分一致 |
| 实际原生终态 | q519 EndTurn 后，抽牌触发的攻击击杀第二幕首战敌人；原生记录 Victory 并实际清理 |
| 终态差分 | **失败**：模拟器清理前 Plating=2，原生清理前 Plating=3。其余 continuation 字段（含 RNG）一致 |
| 同构建重放 | 复现全部 165 个原生命令、125 个相同步骤和同一个 q519 差分；不是成功通关重放 |
| 第二幕后续 | 因差分停止，未领取此战奖励或推进下一节点；没有 BossVictory/RunVictory/M1 完成结论 |

两次原始结果均保留 `Stop=DataError`、`Comparison=NotCompared`、`DataComplete=false`、exit **1**，未改写成通过。原生动作确认前缀为 **165**；差分一致前缀最后为 **a164**，不能把 a165 的实际命令完成等同差分通过。

## 修复边界与上游复用

`NativeSolverEnchantments` 从固定 CombatSolver 的真实 `EnchantmentOnPlayMirrors` 描述器读取明确的 `Handled` 接收类型，并要求实际游戏程序集。它只描述/校验传输能力，不注册处理器、不实现附魔效果、不改上游注册表。

新协议 `upstream-card-enchantment/v1` 同时绑定：

- 本次原生手牌的完整、有序、唯一实例集合。
- 牌模型、升级、完整上游 CardStateKey、模型 occurrence 和状态 occurrence 所指的同一对象。
- 附魔 ID、真实接收类型/程序集、amount、status 和上游 private-state stamp。
- 当前状态与 continuation 的既有绑定、实际派发的牌/目标及旧票据检查。

原生执行仍走真实 `TryManualPlay`；分支执行仍使用上游镜像。旧证据未声明新协议时沿用旧拒绝门，原有 Swift Unsupported 证据不被追认成成功。注册表目前存在六种明确处理器；**本轮真实执行只验证 Swift**，其余五种不能据此称为通过。

主要实现为 `tools/native-run/NativeSolverEnchantments.cs`、`NativeSolverActionContract.cs`、`NativeSolverGate.cs`、`NativeSolverLoop.cs` 及 `NativeSolverEnchantmentControls.cs`。新场景复用既有第二幕控制器、地图和奖励规则，Coordinator 搜索设置及评分未调整。

## 新停点：抽牌期间结束战斗后的 Hook 时序

观察事实：q515 的正常 EndTurn 将 Plating 4→3，差分通过；q516–518 保持 3。q519 进入下一回合，在抽牌过程中 Hellraiser 引发多次攻击并击杀最后敌人；所有原生战斗历史及 Victory 清理前记录中的 Plating 均为 3。原生清理后 `powers=[]` 是真实业务清理，比较所用的是清理前回调，并非把空列表拿来比较。

对固定程序集和固定上游源码的只读核验发现：

1. 原生 `Hook.IterateCombatHookListeners` 在战斗结束中/已结束且非开局时停止分发常规战斗 Hook；`PlatingPower` 的衰减位于 `AfterSideTurnStart`。
2. 固定上游 `CombatBeamSolver.TurnExecutionContinuation.cs` 的玩家回合路径在 Draw 后继续到 Side；`SimulatedCombatState.TriggerSideTurnStart` 无结束门地调用基础侧回合逻辑，其中衰减 Plating。
3. 这与本次唯一差分及原生击杀时序吻合。**未通过修改字段、丢弃 powers、关闭差分或人为恢复数值来继续。** 影响范围可能不止 Plating，需按通用终态 Hook 契约处理，不能做卡名或数值补丁。

本轮没有修改固定 Solver 输入。下一轮最小技术工作见 [交接](COMBAT_SOLVER_ENCHANTMENT_NEXT_HANDOFF.md)。本地反编译仅作只读诊断，未加入仓库文件。

## 来源、冻结与复现

固定 producer：

`1764AC07BA87E63C1B04599AB167D50D908F47A941FDBB3BAE69DC30A7F88665`

- 构建：`.build/native-build/solver-enchantment-final01/build-manifest.json`，4 个既有 warning / 0 error。
- 预登记：`.build/runs/solver-enchantment-work01/plan.json`、`ledger.json`。
- 原始 live / replay：`.build/runs/solver-enchantment-exec-01/`、`solver-enchantment-exec-02/`。
- 具体动作：各目录 `solver-query-516.json`、`solver-online-step-516.json`、`solver-online-step-519.json`、`solver-enchantment-controls.json` 和 `trace.jsonl`。
- 绑定器控制：`.build/runs/solver-enchantment-work01/binding-controls01/summary.json`。
- 证据核对：同 work 目录 `facts.json`，39 项断言通过。该数字表示事实核对通过，**不表示失败运行通过验收**。

两个子进程来自同一个正常新 run 及 110 动作冻结合法前缀，再进入在线尾部；不是拼接两份状态。来源标签保留 `natural-run` 与 `prefix-replay/live-tail` / 重放模式的区别，策略信息模式仍为 **Oracle**。同 seed 重放是可复现证据，不是独立样本或胜率。

只执行 API 绑定控制、不启动原生 run 的复现命令：

```powershell
python -X utf8 tools/native-run/check-solver-enchantment.py `
  --build .build/native-build/solver-enchantment-final01 `
  --run .build/runs/solver-enchantment-exec-01 `
  --output .build/runs/solver-enchantment-binding-review-new
```

输出目录必须不存在。实际运行所用参数、输入 route/choice/reward 哈希和原始退出码保留在 ledger 及计划中；不要绕过本轮已关闭的执行预算重新运行。

## 预算与未覆盖

本批上限为 3 次原生执行、20 分钟、2 GiB、单进程 120 秒；实际 **2 次**，原生实验窗口 **379.32 秒**，保守存储计量约 **681 MiB**，剩余 1 次不再使用。后续仅证据核对与 API 控制，不继续原生实验。两次进程分别约 34 秒和 33 秒，均正常退出 1，未触及强制终止。

冻结核验覆盖 975 个来源项、3 个游戏输入、92 个编译快照项和 16 个产物；15 个锁定 Mod 输入、52 个兼容源码、826 个固定 Solver 源文件及 6 个 DLL 不变，上一轮 1008 份原始执行文件不变。MemorySaveStore、保存/回放写入拦截、无 UI / Steam /遥测隔离保持；记录到的保存调用是被拦截调用数，不是用户文件写入。

RandomForeseer 仍只复用已锁定的源代码子集；Darv 实际拾取效果的未覆盖预测保持明确未知。没有新验证 CallingBell、多节点隔离预测、所有附魔、未来死亡候选采用（本批未出现）或完整原版 run。
