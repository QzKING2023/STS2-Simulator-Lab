# 回合开始自伤死亡的生命周期认证修复

2026-11-03。实现与定向验证完成，待独立复审。冻结：`A5126450FBE068F1239FAE3639CF193759ADF3460AFCE9E02CFD3503E7F4ABAB`，manifest 位于 `.build/native-build/self-death-final01/build-manifest.json`。真实源码98份、编译输入62份、游戏输入3份、产物7份，编译0警告0错误。15个锁定输入与52个兼容源码继续固定。

## 根因和修复

旧 `.build/runs/act3-strategy-dev10-main/` 在同一a448内，先记录玩家HP0的致死伤害历史，随后原生死亡逻辑移除能力，原生回合循环再将死者置为AutoPostPlay和End。ProcessPendingLoss发生在这些变化之后。旧检查器把历史回调和死亡处理入口强制全等，导致真实Death448被拒绝。

新增 `NativeDeathObservation.cs` 只观察两个原生同步方法：`Creature.RemoveAllPowersAfterDeath` 和 `PlayerCombatState.set_Phase`。原方法照常执行，没有替换业务效果、额外调用移除规则或推定Task完成。观察仅在Major原生流程中、当前战斗根动作内、本地玩家已死亡时启用。历史Changed另发出各观测模式均必需的致死记录。新header明确声明 `deathProfile=native-player-death/v1`。

`NativeDeathContract.cs`将状态接成有限的完整结构链：

| 真实边界 | 生产校验 |
| --- | --- |
| 致死历史 | 玩家HP0、真实WasTargetKilled/Receiver、当前root/context；详细模式绑定完整历史快照、epoch/index及DamageResult |
| 移除能力入口 | 与前一锚点完整相等，同一死亡玩家，唯一且先于返回 |
| 移除能力返回 | 使用原生返回的实际移除对象列表；必须是入口能力的唯一子集；只允许该列表对应的powers变化，其余投影完整相等 |
| 死者阶段赋值入口/返回 | 同一玩家、当前根动作、完整状态连续；此次固定链只允许Start→AutoPostPlay→End，每次只允许combat.phase赋值变化 |
| ProcessPendingLoss入口 | 所需致死与移除已完成、无悬空setter，与最后原生锚点完整相等 |
| 死亡处理、根确认、终态 | 沿用原有完整输入输出、实际Losses/inProgress、确认前缀及过期动作拒绝契约 |

详细模式中后续真实CombatHistory仍可更新当前锚点，例如敌方致死后的攻击完成记录；原生记录本身仍由已有动作/战斗上下文契约验证。原生AfterRemoved等若产生当前未观察且无法绑定的投影变化，读取会失败，不允许任意powers、资源、RNG或阶段差异。能力原生保留规则未改：检查器按实际移除列表保留其他对象，并未硬编码所有死亡都清空能力。

`Program.cs`的固定“after actual enemy action”死亡原因改为准确的“ProcessPendingLoss在安全边界完成”说明。原生伤害历史没有给出具体触发Power实例时，标记`triggerSource=not-recorded-by-native-damage-history`；没有凭机制名称伪造来源。

为重放旧v11策略的实际GamblersBrew选择，冻结模式现在从下一条真实UsePotion决策解析当前持有对象、槽位和目标，并走既有`UsePotionC`全部合法性、ticket、原生EnqueueManualUse和实际消费检查。冻结药水决策不再取决于当前live启发式是否会选同一药水。旧choices/reward-choices仍完整消费。没有重新启用live应急Gamblers策略，也没有更换旧种子或路线。

改动为六个既有native-run文件与两个新文件：NativeHost、NativeObservation、NativePreparation、Program、NativeMajorContract、NativeTraceContract，以及NativeDeathObservation、NativeDeathContract。Core、Mod及角色实现未改。旧header缺少该profile的证据继续使用原严格规则，旧Death448原件仍读取失败；修复成功证据是新的实际原生执行，不是改写旧日志。

## 定向原生结果

最终矩阵 `.build/runs/self-death-final01/summary.json` 共9个同构建原生进程，原始退出及检查器退出逐项核对：

- 原v11动作、卡选择和奖励选择：详细、minimal、off三次均真实Death448，native/reader均exit0。896个完整派发/确认投影与旧原始执行相等，448条route包括前后状态哈希完全相同。新增记录没有改变游戏状态或RNG。
- 三次都观察到7个真实锚点：致死记录、能力入口/返回、两次阶段入口/返回。实际移除i918/i929/i930三个能力；Start→AutoPostPlay→End；epoch38历史215条保持完整。
- 当前live原自然路径真实Death474，以及其独立进程冻结重放，均通过；948个完整边界与原已通过Death474路径保持一致。
- 原版skip24、take25通过。
- 真实major-fault保留EngineError、3确认前缀、exit1；major-budget保留BudgetExceeded、4确认前缀、exit3；读取不将它们认证为完成。

另运行两个同构建非法药水重放控制：在旧第414条药水决策修改槽位或目标，均在新药水根派发前InvalidAction/exit1，413派发=413确认，DataComplete=false。见 `.build/runs/self-death-work01/replay-potion-controls.json`。输入副本明确标为invalid-route控制，原件未改。

## 生产检查器和补丁审计

证据目录 `.build/runs/self-death-work01/`，入口 `closeout.json`：

- 完整历史认证集合及本轮原始记录重验718份，预期接受和拒绝分开记录；旧腐坏副本和旧Death448失败保留。没有重跑历史全部原生矩阵。
- 43个新生产检查器控制：41拒绝、2正常/等价接受。包括每类锚点缺失、重复、null、跨context、新增嵌套字段；致死cursor/result/主体错配；实际移除实例、重复和漏项；入口/返回共同改写金币或RNG后恢复；阶段乱序/非法赋值；锚点全部缺失及死亡入口矛盾。控制使用重封副本实际调用最终生产逻辑，不依赖原封条拒绝。
- 35项实际原生事实断言通过，涵盖完整状态/RNG重放、真实移除/阶段、隔离及错误分类前缀。
- 两个新目标分别安装真实外来Harmony prefix：生产Register拒绝已存在补丁，生产Ensure拒绝后加补丁；4项未知补丁拒绝通过，target业务调用均0。正常生产注册成功，卸载外来补丁后恢复预期的未启动宿主隔离错误。它们是审计API夹具，不是对局业务实证。

最初审计夹具未初始化其独立进程的GameBootstrap Harmony字段，导致正常注册断言失败；原始两个`patch-*-initial.json`保留。补足夹具自己的Harmony实例后重新执行通过，未改变生产实现或审计规则。

复现原生正例时，使用 `tools/native-run/run-native-slice.ps1`，指定上述BuildManifest、新的Lab OutputDirectory、Scenario=`major-natural-act3-route`、Seed=`LAB_V0111_NATIVE_DEV_03`、ReplayPath=`.build/runs/act3-strategy-dev10-main/route.json`，Observation可取detailed/minimal/off。然后用同一冻结DLL `--review <输出目录>`。已有final-suite/reread/controls脚本拒绝或假定新目录，不能原地覆盖本轮证据。

## 范围和下一步

本轮按采集链影响范围重跑Major玩家真实死亡、冻结药水执行、原版纵向烟测、异常/预算及观测对照，并完整重验历史原始证据。没有扩大到所有节点或重跑整套原生矩阵。最终M1同构建全量门、必要机械数据采集和完整RunVictory仍保留。

当前认证是已观测的无复活玩家致死链。直接Kill而无伤害历史、死亡预防/复活及额外AfterRemoved业务变化尚未建立独立契约，会明确读取失败，不能宣称全部死亡机制已支持。minimal/off的致死事实来自固定原生观察点，详细模式另与原生历史逐字段绑定；这不构成脱离受控生成链的执行真实性证明。

请先独立复审本轮实际原生状态、有限契约和43控制，特别检查保留能力/非法移除、root/context/房间归属和静止完整状态。通过后接续第三幕合法策略、Boss胜利及Architect/RunVictory。当前仍无第三幕Boss或完整M1通过结论。无环境/权限阻塞；未运行真实UI、真实Steam/网络、用户存档，也未提交推送。
