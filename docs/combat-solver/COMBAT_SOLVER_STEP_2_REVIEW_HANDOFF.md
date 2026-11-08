# CombatSolver 第 2 步综合独审交接

2026-11-04。成功药水路径的同构建重放现已补齐。**当前有界动作集可提交一次综合独审；此文不宣称第 2 步或 M1 已验收完成。**

## 本轮仅一次重放

保持代码、输入和构建不变，使用：

- manifest：`.build/native-build/solver-smart-final02/build-manifest.json`
- build ID：`12F1E1BFF786FBBD5DD29BF882BD1C743949AD82E31A464F7A9AF221F6A6312F`
- scenario：`solver-smart-energy`
- replay：`.build/runs/solver-smart-exec-04/route.json`，同目录原始 `choices.json`
- 新原始结果：`.build/runs/solver-smart-replay-exec-01/`

执行前登记1次额度、5分钟窗口、2GiB、单进程120秒，不重置旧ledger。实际只运行1个原生进程，约2.48秒，exit0、5/5根动作确认、`Ongoing / SliceBoundary / NotCompared / DataComplete=true`。

求解器重新求解后仍主动选择 EnergyPotion；原生使能量0→2、HP保持80、药水实例i29移除。实际再查询仍返回 Strike；该卡未执行，敌人HP仍1，没有原生战斗胜利。

live/replay 的 `route.json`、`choices.json`、`final-state.json`、完整 `solver-online-step-1.json` 相等。两次 query 的完整状态、continuation、binding、实际计划、有效Profile/药水策略、候选和结果一致；fork/来源不扰动、消费和过期票据检查通过。全部已记录 run/player/event RNG 及完整continuation重放一致。

`.build/runs/solver-smart-replay-work01/verification.json` 的42项事实核对通过，生产reader接受新重放副本。构建952条source、77条compiled、3条game inputs、15条artifacts均匹配；前批102个原始执行文件和1459个工作证据文件锁匹配，15锁定输入和52兼容源码保持一致。没有重跑旧59控制或完整原生矩阵；其生产reader和实现没有改变，原证据可复用。

本轮没有代码、依赖或策略改动。旧报告“成功药水重放未运行”保留为当时事实，本文及新closeout补充当前状态，不改写旧原始结果。

## 当前已验证覆盖

| 能力 | 实际执行与证据 | 声明范围 |
| --- | --- | --- |
| 普通出牌、敌方目标 | `solver-adapter-exec-03/04`：求解器实际采用 Defend、目标 Strike、再次求解及重放 | 原版首战片段；不等于全部原版卡效果已认证 |
| EndTurn | 同上实际敌方行动及下一玩家Play；另 `solver-smart-exec-01/02` 的8动作前缀逐步差分和重放 | 非终态稳定回合边界；不含结束回合子选择/复活/终态 |
| 在线行动内手牌耗尽选择 | `solver-online-exec-01/02`：主动采用 TrueGrit+ 和 Wound token，原生耗尽指定i29，i30留手，9格挡/1能量，live/replay及实际再查询 | Action / Exhaust / Hand；在线实测候选index0、两个occurrence均0 |
| 非首项耗尽 | `solver-choice-potion-exec-04`：固定API计划选择候选index1的Defend i31，原生返回和耗尽一致 | 固定动作夹具，不能称求解器在线采用非首项；OptionOccurrence仍0 |
| 在线自目标药水 | `solver-smart-exec-04` live + 本轮 `solver-smart-replay-exec-01`：主动EnergyPotion、原生使用、精确差分、再求解及重放 | 上游派生API输入、Smart无强制指令、诊断搜索设置；不是自然获得或默认策略表现评价 |
| 敌方目标药水 | `solver-choice-potion-exec-04`：固定FirePotion对i26造成实际20伤害并移槽 | 固定动作差分；未认证主动在线目标药水或该固定路径完整重放 |
| BlockPotion在线机会 | `solver-online-exec-03/04`：NodeLimit；`solver-smart-exec-01/02`：未采用药水、8动作门停止 | 失败/未完成结果保留；不能记为已在线采用，也不能由此断言原生BlockPotion不兼容 |
| 动作身份和字段 | `NativeSolverActionContract`、实际card/target/potion映射、原9个误接受反例关闭 | 类型、当前turn、卡模型/升级/两个occurrence/StateKey、目标index/CombatId、药水实际槽/型号/实例及已支持执行字段；不承诺任意新字段自动有语义 |
| 子选择来源/消费 | `NativeSolverChoices`复用上游token游标，绑定当前root/context、真实候选、来源、时机和完整消费 | 缺失、多余、过期、错来源等已有托管API/证据负控；尚不等于每种错误均有原生途中失败路径 |
| 票据、捕获与数据 | 原独审及最终59生产控制：未消费旧票据跨动作拒绝、零额外派发、Oracle标签、捕获角色/顺序/摘要、静止完整状态与真实动作绑定 | 有限内部协议，不是从任意整份伪造日志复算全部游戏语义 |
| 隔离与依赖 | 版本锁、严格root审计、MemorySaveStore、ShouldSave=false、Steam未初始化、单机服务、无UI/遥测、未加载MyMod/BaseLib | 无真实用户存档/Steam/网络操作；固定本机游戏程序集仍是合法依赖 |

这些证据来自不同、明确保留的producer builds。最新reader重验旧证据属于分层验证，不能说它们都由final02重新执行。最终M1同构建全量门未关闭。

## 真实修复与失败分类

原五项动作派发绑定及后续四项选择/药水身份反例均已修复，最终59控制包含这些反例和合法等价副本。

`solver-smart-exec-03` 的合法EnergyPotion候选被Lab对null目标的旧验证拒绝，raw exit1、InvalidAction、4/4 setup前缀且零UsePotion派发。复用上游“以玩家验证自目标、入队仍传null”的语义后，04执行成功，本轮同版本重放也成功。03的InvalidAction是执行器拒绝分类，不能再拿来证明该求解候选在原生游戏中非法；故障和归因保留。

原0/0/3/3在线批和3/3/1/0 Smart批均保持原退出码、失败前缀和producer ID。预算停止候选不采用，不能因有预测胜利路线或外层检查成功而提升为完成。

## 明确未覆盖

- 在线非首项、非零重复实例选择，以及不同状态但同模型实例的实际部署组合。
- 连续/嵌套选择、PlayerTurnStart/结束回合选择、生成卡来源、选择中再生成选择；现有在线能力门明确拒绝未支持协议。
- 缺失/多余/非法晚到子选择在原生根动作部分生效后的完整未确认前缀；已有纯API/证据负控不能代替该实证。
- 在线药水槽替换/多药水、药水自己的选择、自动药水/复活，以及全部上游CanSearch药水表的原生认证。
- 多敌人目标组合、自动出牌、全部卡/遗物/幂等性组合、预测终态与真实终态/清理的在线认证。预测终态仍在派发前明确Unsupported。
- 真实单场完整终局、回到M1自然Boss路径及完整RunVictory、完整同构建回归。没有新胜率、全局最优或平衡结论。

这些边界既不自动算通过，也不自动扩为本次必须实现的全部游戏内容。综合审查应判断已授权第2步的必要门；若有必修，给最小可复现缺口，不重启单场评分微调。

## 综合独审建议范围

1. 核查冻结版本、MIT归属、隔离和原生来源，区分direct beam旧片段与新Smart协调器场景。上游复用及诊断设置依据见 `COMBAT_SOLVER_SMART_POTION_VALIDATION.md`，不采用奖励RNG重选，不强制用药。
2. 实际核验原动作/身份反例关闭、合法副本、真实派发与卡/药水/目标关联、子选择请求/返回/消费、未消费旧票据、捕获/摘要和完整状态/RNG。复用现有59控制及有限独立控制；仅检查器审查不重跑原生矩阵。
3. 核验本轮 Energy live/replay 同一final02：真正首动作采用、原生UsePotion、2能量和移槽、实际再查询、状态/RNG和完整重放；确认自目标修复不将非法敌方目标转成玩家目标。
4. 核查3类原始结果未被外层绿掩盖：成功、BudgetExceeded、Lab目标检查InvalidAction；不能把固定夹具或预测路线称为自然胜利。
5. 给出本有界动作集通过/必修及下一步；单场终态和自然流程接回仍需独立实证。此文只准备交接，不代替独立审查结论。

关键代码：`NativeSolverGate.cs`（捕获/查询/协调器）、`NativeSolverLoop.cs`（共享执行及目标验证）、`NativeSolverChoices.cs`（子选择token）、`NativeSolverActionContract.cs`、`NativeSolverFixtureContract.cs`、`NativeSolverOnlineContract.cs`（生产协议检查）。

关键证据包：

- `.build/runs/solver-action-loop-independent-review01/`：前轮独审和原动作反例。
- `.build/runs/solver-choice-potion-independent-review01/`：四个新增身份反例。
- `.build/runs/solver-online-work01/`：身份修复、在线TrueGrit live/replay和药水NodeLimit。
- `.build/runs/solver-smart-work01/`：协调器复用、自目标修复、115核对及59集中控制。
- `.build/runs/solver-smart-replay-work01/`：本轮42核对、生产reader、单次ledger及closeout；原生结果在对应 `solver-smart-replay-exec-01/`。

本轮仅新增交接文档和README入口，原生/运行代码未改，无提交推送。没有权限或环境阻塞；下一动作是综合独审，不需要重新批准既有范围。
