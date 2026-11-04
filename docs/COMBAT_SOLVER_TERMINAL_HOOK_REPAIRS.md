# 抽牌致胜后的常规 Hook 门控修复

2026-11-04，第 5 步有界推进。**q519 差分已闭合；第二幕继续推进，但 M1 未完成。** 未提交或推送。

## 根因与复用选择

上轮 [附魔映射验证](COMBAT_SOLVER_ENCHANTMENT_VALIDATION.md) 中 Swift 真实执行成功；q519 的原生抽牌触发攻击并击杀最后敌人，之后模拟器仍衰减 Plating。原生清理前为3，模拟为2，两次均 DataError/exit1，旧证据完整保留。

核查固定 CombatSolver 源码后，发现它已经提供：

- `CombatPredictionSimulator.IsOverOrEnding`，含结束中及已结束状态。
- `HookMirrors.IterateCombatHookListeners` 的常规 Hook 停止门。
- Draw/自动打牌和安全终态 stamp 等既有机制。

缺口是两条手动快速路径没有应用同一门控：`SimulatedCombatState.TriggerSideTurnStart` 在常规 Hook 外先执行基础状态变化；`HookMirrors.AfterPlayerTurnStart` 的原版批处理快速路径也可绕过停止门。**新派生依赖只在这两个入口复用 `IsOverOrEnding`**，不改卡牌、数值、评分、搜索配置、Draw 事务或终态锁定时机。修复不包含卡名/敌人/种子的特判。

原生依据是固定版本 `Hook.IterateCombatHookListeners` 的结束语义。修复作用于已建立战斗根及回合推进；没有新增建模初始化中间点的 `IsStarting` 特例，也不声称覆盖复活、多玩家或全部死亡回调。

## 独立依赖与构建

原 `combatsolver-0.50.1-gate01` 的826源文件及6 DLL保持不变。新增本地独立快照：

`.build/pinned-inputs/combatsolver-0.50.1-terminal-hooks01/`

- 上游版本仍为 **0.50.1**；Lab 派生标识为 **terminal-hooks.1**，不是新的官方发行版。
- Solver DLL SHA256：`F79144BD26BE618383719135449176A75092C6131B2B03D4105829B6CD0010B2`。
- `derivative-lock.json` 记录原锁、两个源差异、独立构建工程、资源、工具和产物来源；`solver-lock.json` 绑定实际载入 DLL。
- 独立工程只执行编译，没有部署、辅助程序构建或游戏安装目录写入目标。沿用已缓存 Godot SDK、Publicizer及既有本地依赖，保留 MIT 与上游归属。
- 构建工具为 `tools/native-run/prepare-solver-terminal-inputs.py`。首次编译成功（0 warning/0 error）后，工具因误用 SDK 输出路径而复制失败；已修正路径，核验并复用已完成产物，没有重复编译或丢弃失败记录，详见 work 目录 `dependency-build-recovery.json`。

最终原生 producer：

`B6B0952C3BED91D14CB200B8748870D4D3CC728F8D22278703B5BAEAF5FC932A`

三个构建的作用分开记录：

| 构建 | 用途 |
| --- | --- |
| `22B46C9D…05FCE40` | 同一新控制器配原依赖；模型负控按预期失败 |
| `24D37243…816DC57` | 仅生命周期修复，q519通过，进入未审计 StoneOfAllTime 时 Unsupported |
| 最终 `B6B0952C…5FC932A` | 同一生命周期依赖，加既有事件桥的 Lift 分支；live及失败前缀重放 |

前两个构建是诊断证据，不能拼为最终同构建结论。最终两次执行使用完全相同的 producer；宿主构建均4个既有 warning、0 error。

## 最小控制及原生时序

`NativeSolverHookControls.cs` 在真实根的隔离 fork 中设置明确标记的 API 夹具，验证8种情况：正常首回合/后续侧回合、结束中侧回合、已提交终态侧回合、正常/结束中的 AfterPlayerTurnStart、多个敌人中死一个、最后敌人死亡。夹具没有修改原生 HP/资源，没有产生额外原生命令。

- 正常侧回合仍执行应有变化：Plating4→3、待抽牌层清除；首回合保留Plating4。
- 正常 AfterPlayerTurnStart 的 Hibernate3→2；结束后不执行。
- 结束中/已结束分支要求**完整 continuation（含 RNG）和历史完全不变**。
- 父 fork 与原生状态/RNG均保持。原依赖在相同 `ending-side` 控制中实际失败，而非仅由新断言自证通过；该负控停在3个原生命令后，没有派发求解器牌动作。

真实自然路径独立验证了关键顺序：下一回合抽牌/攻击历史 → 原生 Victory 捕获 → 清理前回调 → 历史清理 → 动作确认 → 终态校验 → 原生清理完成。q515正常回合仍Plating4→3；q519致胜抽牌历史和清理前始终为3，模拟也为3。检查的是这些已采集生命周期锚点，未宣称逐个监听器调用都已完整记录。

**与旧失败路径对照：126步的实际动作、原生前后状态及 continuation全部不变；只有q519原来错误的预测状态得到修正。** 这比用不同自然路径推测修复效果更直接。最终126步全部差分一致，同构建重放一致。

## 第二幕实际进度

同一正常新 run 的110动作冻结合法前缀后，继续在线求解及原生节点流程：

1. q519 第二幕首战真实Victory并完成清理。
2. a166 原生领奖：金币241→252；被盗的原牌 `i166 / Inflame+1` 经原生特殊奖励返还，另取得 `i375 / Cruelty+1`，牌组18→20。不能把返还原对象说成生成了两张新牌。
3. 原生药水奖励取得SkillPotion，并按既有会话处理满槽；指定实例为`i371`。
4. 进入 StoneOfAllTime，复用现有原生 EventSynchronizer、选择与任务等待桥，选择真实 **Lift** 选项：消耗`i371`，HP/MaxHP80→90，事件完成并恢复药水使用/移除权限。没有自行实现事件效果。
5. Stone事件模型RNG counter1→2及完整四字状态记录并重放一致；事件同步器、run/player RNG在此选项期间保持不变。它们是不同的流，未混作同一个计数器。
6. a169 Travel已经原生派发，进入下一事件 **ThisOrThat** 时审计门拒绝；最后确认是a168。实际部分执行后的最终状态仍保留，不用最后确认快照掩盖它。

Stone当前仅审计了继承无操作`OnRoomEnter`及Lift分支；Push/选牌/Vigorous未支持，不能默认选第一项冒充全部事件支持。RandomForeseer所选源子集没有覆盖该事件或Darv拾取效果预测，报告明确为**原生执行、预测未知**。

## 重放、退出和证据

最终live和重放均：**168确认 / 169派发，126求解步全部差分一致，Stop=Unsupported、exit2、DataComplete=false**。不是完整run通过，也没有将Unsupported改成Victory。

重放输入保留168个实际确认记录，并从原始trace提取实际已派发的a169请求。该请求的`after`和`confirmed`明确为null，来源文件记录`expectedConfirmation=false`；没有编造确认边界。原生在同一审计门失败，尚未走到成功后状态比较；如果该请求意外完成，null after会使冻结边界检查失败。重放复现了相同的成功前缀、部分执行和Unsupported。

证据位于：

- `.build/runs/solver-terminal-hooks-work01/plan.json`、`ledger.json`、`facts.json`、`closeout.json`。
- `.build/runs/solver-terminal-hooks-exec-01/`：原依赖负控；`exec-02/`：修复后Stone入口停止。
- `.build/runs/solver-terminal-hooks-exec-03/`、`exec-04/`：最终同构建live/重放；重点为`solver-online-step-519.json`、`solver-hook-controls.json`、`stone-lift.json`、`trace.jsonl`、`result.json`。
- work目录`failed-prefix-input/provenance.json`：未确认动作重放来源；`collect-facts.py`可重读，**64项断言通过表示证据核对成立，不代表Unsupported run被验收为完整成功**。

本轮4/4次原生执行已关闭，实验窗口611.48秒，单次均小于120秒；保守存储计量含新依赖、构建及共享旧缓存约918 MiB，小于2 GiB。未因检查器或文档变化重跑无关全量原生矩阵。

15个Mod输入、52兼容源码、原826源/6 DLL及此前2040份原始执行文件哈希保持。MemorySaveStore、保存/回放拦截、无UI/Steam/遥测隔离保持。最终M1同构建全套验收仍待完成。

下一步见 [交接](COMBAT_SOLVER_TERMINAL_HOOK_NEXT_HANDOFF.md)。
