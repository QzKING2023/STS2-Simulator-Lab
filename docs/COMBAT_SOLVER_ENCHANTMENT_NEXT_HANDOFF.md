# 第 5 步交接：先闭合抽牌致胜后的通用 Hook 边界

2026-11-04。用户对 M1 的持续授权不变。本文件是本批失败后的最小技术交接，不表示 M1 或第二幕已完成。

## 当前可依赖的事实

见 [本批验证](COMBAT_SOLVER_ENCHANTMENT_VALIDATION.md)。Swift 的通用身份映射及真实执行通过，不能再归因为“Hellraiser 缺镜像”。同 producer 两次在 q519 复现 Plating 2/3 的清理前差分；原生已实际 Victory，但整 run 仍 exit1/DataError，第二幕首战奖励未到达。

优先复用证据，不重跑原生全量：

- producer `1764AC07BA87E63C1B04599AB167D50D908F47A941FDBB3BAE69DC30A7F88665`。
- `.build/runs/solver-enchantment-exec-01/solver-online-step-519.json` 及同构建 exec02 对照。
- 固定上游源码：`Search/CombatBeamSolver.TurnExecutionContinuation.cs:109–157`、`Search/SimulatedCombatState.cs:1240–1265`、`:1310–1325`。
- 本地只读诊断：work 目录 `native-CombatManager.txt`、`native-Hook.txt`、`native-PlatingPower.txt`；不得提交这些游戏反编译内容。

## 下一最小修复方向

1. 先确认选中上游版本是否已有通用结束门可复用。当前分支中的该方法未应用原生 `IsOverOrEnding && !IsStarting` 的常规 Hook 停止语义。需要精确区分常规 Hook、杀死目标那次仍应继续的直接伤害/牌结算，以及正常回合状态推进；不能简单在首次击杀时截断整个原生 Draw。
2. 如必须调整 Solver，只在 **新的、明确版本化 Lab 依赖快照/构建** 中修复通用生命周期，再保持原 826 源/6 DLL 旧 pin 和失败证据不变。不能运行时 Harmony 绕过审计、替换一个字段或更改牌效；无需修改角色或游戏安装。
3. 先做局部模型/回放测试：正常存活的回合开始仍应衰减 Plating；抽牌击杀最后敌人后常规侧回合 Hook 不再执行；非最后一敌人的击杀仍继续；初始开战语义不可被 ending 门误挡。比较完整状态/RNG、实际动作与来源，而非仅比较 Plating。
4. 生成链变更后，仅重跑受影响的这一条自然前缀/在线尾部及同新构建重放，并补适量相邻回合/终态控制；此前证据按各自构建保留。预先登记下一批预算，不能借用已关闭 ledger 的剩余名额。检测到差分、未建模或其他安全停止仍必须停止。
5. 成功后从实际原生奖励与下一节点继续第二幕。Coordinator、评分、信息模式和 RF 未覆盖边界保持；不重新进入单 Boss 手调。

## 本轮明确未做

没有修改上述固定依赖，没有应用未经验证的终态“归一化”，没有领取 q519 后奖励、没有开展额外原生实验。检查器/CLI 控制与本地诊断仅支持问题定位，不代替后续修复的真实差分验收。最终 M1 同构建全量验收继续保留。
