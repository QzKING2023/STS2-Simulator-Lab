# 第4步报告实例绑定修复

2026-11-04。`NativeSolverActionContract.BindReport` 将 whole-battle step 的 `card/target` 与已通过身份验证的实际 dispatch.decision 绑定。PlayCard 精确匹配卡实例及 nullable 目标，UsePotion 要求 card=null，EndTurn 两字段均为null；缺失或错误JSON类型拒绝。没有修改原生执行、求解、RNG或原证据。

修复reader冻结：`C58A313ACDAC7DF51ACAFFDC988BA40E7BEF365B365FE1062E5238B9FF9A02EF`，`.build/native-build/solver-report-repair01/`。相对前reader仅 `NativeSolverActionContract.cs` 与 `NativeSolverBattleContract.cs` 编译源变化。4个既有编译warning、0error。

`.build/runs/solver-report-repair-work01/` 保存实际生产reader结果：八份正常原始证据、原30项综合控制及nullable/type/EndTurn控制，两个原误接受已拒绝，全部38项有效检查符合预期，新增原生执行0。共39次调用中，一项最初把query68的Bash误标为EndTurn，赋予原有目标i52没有改变内容，属于错误负控；原结果保留，另以真实query67补测非null card/target，均拒绝。这不是生产缺陷或原生失败。

第3步原终态四反例保持拒绝，第4步两条同Run真实两战、金币/卡奖励与Rage取得打出的原证据接受。真实业务范围和局部差分/原生奖励的声明保持。第4步窄范围定向修复已通过，继续第5步原生节点集成；M1和最终同构建全量门仍未完成。新集成将另设生产构建与有界执行ledger，不覆盖这些原producer或历史失败。
