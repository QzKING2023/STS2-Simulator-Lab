# 第3步终态回调绑定修复

2026-11-04。定向修复及复验通过，可继续第4步；独立复审待安排。原始游戏执行 producer 保留为 `AAACB551E949A74ED8DBA202B8C91DB71AA8CB3535C6F0A48A811570BA0CA17A`，本轮没有重跑游戏。

`NativeSolverBattleContract.BindNativeTerminal` 将 solver 终态与对应原生 Victory/LossBefore 回调绑定完整结构状态、root/context 和先后顺序，回调必需且唯一；Death 还绑定 LossAfter 的动作及完成顺序。同步采集与回调之间没有业务变化，直接完整比较，新增字段同样受检。胜利后恢复6HP、清场及 LossAfter 的 Losses 增量属于后续业务阶段，保持合法。

证据 `.build/runs/solver-battle-repair-work01/`：四个原误接受副本全部拒绝；42次生产reader调用（8个正常/等价接受、34个坏副本拒绝）全部符合预期，包含前轮30项和新增上下文/新字段/缺失/重复/完成错序等12项。153项原始事实与重放核对保持通过。只编译源 `NativeSolverBattleContract.cs` 变化，执行采集源没有变化；检查器冻结ID见该目录 `closeout.json`，产物 `.build/native-build/solver-battle-repair01/`。

第3步声明范围保持正常首战真实Victory与API死亡夹具Death、清理前局部差分及各自完整重放；死亡同进程worker复用、零售run-loss收尾、奖励与多战未由此修复新增认证。原始证据、15锁定输入、52兼容源码和solver依赖保持。无提交推送。
