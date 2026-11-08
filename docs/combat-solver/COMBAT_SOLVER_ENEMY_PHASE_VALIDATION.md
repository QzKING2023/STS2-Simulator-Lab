# 逐幕复验与敌方回合选择（2026-11-04）

## 状态：M1未完成

已关闭此前“逐幕入口未原生复验”项；无UI敌方选择桥已真实执行。最终实验停在整局墙钟预算，**q857原生修复回归、当前producer同构建重放、第二幕Boss及后续跨幕仍未认证**。事实检查通过不能把这些项记成通过。

### 证据分层

| 批次/producer | 真实结果 | 已证实 | 尚未证实 |
|---|---|---|---|
| `solver-act-control`，既有final06/`27D5AB…AC43FDD` | 两次均268确认/268派发、Unsupported/exit2 | 实际入口0→首幕Boss/奖励→入口1；31次Travel；200步完整状态/RNG差分；同producer重放 | 敌方选择当时仍未执行 |
| `solver-enemy-choice-exec-01`，final02 | 290确认；DataError/exit1 | q836选MindRot、q848选Disintegration，两次原生来源/计数器与分支一致 | q857抽牌击杀后的NoDrawPower差分；Boss回调出现但不能认证胜利/奖励 |
| `exec-02`，final03 | 3确认；DataError/exit1 | 能力应用初版的closed分支负控发现缺口，失败保留 | 此版不能作为终态修复通过证据 |
| `exec-03`，final04 | 269确认/269派发；BudgetExceeded/exit3 | 201个实际动作完整continuation/RNG差分；a269真实敌方选择；逐幕/Travel及正式选择事件完整性 | 最终q837查询未完成；没有本producer完整重放或q857原生回归 |

原先268步与a269未派发的证据未改；新批次a269已实际执行，是各自run内的动作编号。

## 实现与复用

- 复用固定CombatSolver0.50.1的 `SolverController.CaptureDeploymentEndTurnChoices`、`CombatBeamSolver.AdvanceRound`、`NativeChoiceSession.ResolvePlannedCards`、`CardChoiceSupport`、`KnowledgeDemonChoiceSupport.SourceId` 和 `MonsterValueReader`。上游UI driver依赖NGame，未启动。
- Lab只补无UI传输/审计：实际正在执行招式的敌人、CombatId/计数器、当前Enemy阶段、候选实例/升级/出现序号、请求归属/顺序、计划恰好消费。能力效果仍由原生OnChosen执行；没有新增怪物AI表或改搜索评分。
- 当前仅开放 `EndTurn/EnemyTurn/ApplyKnowledgeCurse/FromChooseACardScreen`；PlayerTurnStart及其他效果、未知来源、缺失/多余/过期选择仍拒绝。上游 `IsExecutable` 只为PlayCard/UsePotion返回true；EndTurn由单独部署路径处理，不能把其false值解释为禁止结束回合。
- `solver-phase-choice`是正式带完整状态的trace事件，位于请求与回答之间；child记录与此事件绑定。复用既有状态/RNG结构检查，未将不完整状态默认补齐。

## 通用终态缺口

q857 BattleTrance抽牌触发真实Boss胜利回调时，初版分支比原生多一个NoDrawPower。固定上游及本机参考版本的公共能力应用入口均缺此终态门。

独立依赖 `ending-powers.2` 复用上游 `IsOverOrEnding` 于公共Apply入口，不给BattleTrance加特例。原生PowerCmd检查IsEnding，清场后目标也已不能接收能力；分支为了比较保留终态对象，须同时覆盖closed状态。仅IsEnding的初版在closed后为false，已被API控制拒绝，旧pin/失败未覆盖。

新依赖相对strict-branch-runtime.1只改 `src/Search/SimulatedCombatState.cs` 的公共入口，保留原strict条件分支拒绝、turn-hook修复、评分、审计与MIT归属。当前4个分支API控制（正常应用、ending跳过、closed跳过、只死一敌仍应用）通过并验证父分支/原生状态不变；**不是q857原生场景已通过**。

## 构建、入口及验证

- 实际最终producer：`4F1F52B53BC15BC67BE269C124B932D824FE266419E2A512BE28EBD52D52A5A6`，`.build/native-build/solver-enemy-choice-final04/`。
- 收尾checker：`2E948B293194AA7752A1B5FC44CC6DBAFF154C5FEC41FA18F71576BD01022B2F`，`final05/`。仅3文件改动：复用状态结构检查、CLI控制输入绑定、检查器方法可见性；不改变原生生成链，见`checker-only.diff`。未因此重跑原生矩阵。
- Solver：`.build/pinned-inputs/combatsolver-0.50.1-ending-powers02/`，DLL SHA256 `A38C88EFD533BF1C8BE43E6C56E7B78BB2342720BE336A760FE255A645AE1E53`；826源码/6DLL独立锁。strict及初版ending pin保留。
- 入口：`.build/runs/solver-act-control-work01/facts.json`、`solver-enemy-choice-work01/facts.json`、`closeout.json`、`execution-lock.json`。
- 定向检查：实际原生选择回调11负控/2正控；13项生产形状控制；22项生产证据控制；4项分支能力终态控制。构造副本/API夹具与真实动作分列，不作为额外自然样本。
- 初次证据控制的CLI有效副本因JsonElement身份/索引处理失败，已修复控制入口绑定并保留`controls-reader04-initial/`；不是重写原始运行结果。

## 预算与下一步

本轮分为两个独立预登记批次：逐幕复验2次；选择桥最多4次/20分钟/2GiB/120秒进程门，实际运行3次，时间门到达后第四次未启动。最终原生进程在约113.4秒正常退出3，watchdog未触发；整局110秒协作门导致q837等待只余0.9秒。202次查询已开始、201次完成，未给最后一次虚构工作量。

搜索继续使用已验证custom：beam8/nodes256/soft10s/dop1。已核查的内置四档与并行设置见[前轮报告](COMBAT_SOLVER_FULL_RUN_CONTROL_VALIDATION.md)。本轮未启用并行；观察到搜索墙钟成本上升，但依赖/环境与样本范围不同，不能当作严格性能A/B归因。下一步应先复用上游并行/预算配置做有界同根验证，并据实际吞吐设计整局时间与复现门，不能用搜索档位代替整局预算，也不应反复微调某场评分。

当前首要未闭合：q857原生回归与最终producer重放。见[交接](COMBAT_SOLVER_ENEMY_PHASE_NEXT_HANDOFF.md)。没有新完成一幕；无真实UI/Steam网络/用户存档/角色或其他项目操作、提交推送。15个Mod输入、52个兼容源码及旧raw均保留；最终M1同构建全量门仍待完成。
