# CombatSolver Smart 药水主动采用验证

2026-11-04。**已证实求解器主动选中 EnergyPotion、原生使用、完整差分和实际重新求解；这条成功路径的同版本完整重放尚未运行。** 四次预登记额度已收口，第 2 步及 M1 未完成。

## 复用依据与改动

固定 CombatSolver 源码中，`Testing/Contracts/Search/UnattendedTestRunner.FixedPrefixOutcomes.cs:77–118` 的 Smart 用药测试通过 `CombatSearchCoordinator.Solve` 调用无药水基线及药水策略层，使用 beam=8、每成员 256 节点、10 秒固定搜索预算、DOP=1，无强制用药指令。原 Lab 小闭环直接调用 `CombatBeamSolver`。本轮为独立注册的 `solver-smart-*` 场景接入已有协调器，原场景入口保持原模式。

复用上游的有限搜索设置和 `FixedPrefixOutcomes.cs:188–206` 的高自伤机会成本构造；输入中采用 `HpTargetStop.cs:121–127` 已使用的 EnergyPotion。使用现有 fork 药水对象和执行器，没有新增药水效果、搜索算法或评分函数。

实际 policy 写入每份 query：Smart、空 Directives、FixedBudget=true、10000ms、beam8/per-member256、取消15秒、DOP1、禁用宽度/novelty portfolio、禁用 HP 目标提前结束、忽略长期奖励。结果累计节点超过4096拒绝采用；这项是返回结果门，不应描述成在第4096节点精确中止的共享硬门。查询和原生进程另有取消/超时门。

上游用例包含为取得指定 NoDrop/Drop 而消耗奖励 RNG 的夹具步骤，本轮不采用该步骤，明确 `PredictPotionReward=false`，不伪造奖励预测或替换 RNG。上述设置是上游派生诊断设置，不能称为原应用默认全部设置不变。原 `solver-profile.json` 是基础设置，query 中的有效 policy/Profile 才是本场景实际使用值。

## 预登记场景和结果

`plan.json` 在原生执行前保存，上限 4 次尝试、20 分钟、2 GiB、单进程120秒，每路径最多8实际动作/9查询；输入、种子和评分在取得结果后未更换。

| 尝试 | 场景及实际结果 |
| --- | --- |
| 01 | `solver-smart-block` live，复用上一批空手牌＋BlockPotion 输入。协调器搜索9次，实际执行8个动作，仍未使用药水。到8动作门返回 BudgetExceeded，raw exit=3，12/12根动作确认 |
| 02 | 相同构建和输入的 Block frozen-replay。8个动作、所有完整状态/RNG/continuation、route/choices/final-state一致，仍 BudgetExceeded、exit3 |
| 03 | `solver-smart-energy` live。求解器真实首动作是 EnergyPotion，结果记录 ExplicitPotionCount=1、PotionHpSaved=12、PotionHpRequired=9。Lab 在原生派发前错误拒绝自目标；InvalidAction、exit1，4/4 setup 前缀，没有 UsePotion 派发 |
| 04 | 修复目标检查后的相同 Energy 输入 live，raw exit0，5/5根动作确认，其中1个实际 UsePotion。能量0→2，HP80不变，原药水实例i29移除；完整预测/原生 continuation精确一致、父fork/原生来源及已记录全部RNG检查通过。实际再次求解返回 Strike；该卡未执行 |

两个输入均为 **API 夹具**，不是自然取得物品或自然获胜路径。

Block 输入保持正常首战，把初始手牌通过原生 CardPileCmd 移入 Exhaust，再原生取得 BlockPotion。没有更改 HP、能量或敌人，用于参考原输入下的协调器行为。引擎和搜索设置同时变化，因此不能将与旧批差异单独归因于某个参数或入口；本批也未证明它为何未选择 BlockPotion。

Energy 输入明确清除战斗牌堆、遗物和 powers，通过原生 API 设置敌人 HP1/Strength20、建立 Bloodletting（该战斗副本 HpLoss12）及 Strike、能量0，并原生取得 EnergyPotion。它由上游高自伤 fixture 派生，将原 FirePotion/StrengthPotion 换为 EnergyPotion，以便在药水使用后仍有非终态实际再查询。永久牌组不变，不修改角色项目或正式卡牌数据。不能把夹具中的 HP、遗物或卡变量状态当作自然路线结果，也没有实际战斗胜利。

## 本轮定位并修复的实际接口问题

上游 `Testing/Support/UnattendedTestRunner.Potions.cs` 在目标为 null 时，以 `potion.IsValidTarget(player.Creature)` 确认自目标合法，实际 `EnqueueManualUse` 仍传 null。原 Lab 直接对 null 调用 IsValidTarget，导致 EnergyPotion 的合法求解动作在派发前被拒绝。

共享执行器现在复用上游同一验证关系：只有实际目标为空时，才尝试以玩家作为验证对象；显式目标仍验证原对象，实际入队目标不被替换。没有跳过合法性或自定义可用性检查，也没有弱化来源/补丁审计。

03/04 的查询前完整状态、RNG、continuation、原始求解首动作及前4个确认动作一致；两版 compiled/source 只有 `NativeSolverLoop.cs` 的目标检查改动。新版本原生执行后精确差分通过。因此有具体接口修复实证；这组跨构建对照仍不等于04的完整同构建重放。

03的失败保留。执行前另存 `target-guard-repair-decision.json`，把最后一个原定重放槽位用于同输入的修复 live 验证，没有增发第五次。**成功 Energy 路径的完整 replay 留待下一次单独有界执行，不能由03的相同前缀或02的 Block replay代替。**

## 冻结与集中验证

| 用途 | 构建 ID |
| --- | --- |
| 尝试01–03 | `521721B266936D58DD25FD570E18EFFF0157B0FD70B0E86442F0A05850A1CCAA` |
| 目标检查修复、尝试04及最终生产检查器 | `12F1E1BFF786FBBD5DD29BF882BD1C743949AD82E31A464F7A9AF221F6A6312F` |

集中证据目录：`.build/runs/solver-smart-work01/`。原始执行：`.build/runs/solver-smart-exec-01/` 至 `04/`。

- `combined-controls.json`：最终生产 reader 集中调用59次，全部符合预期。包括原24身份/标签/捕获控制、前批17在线选择控制、本批3非完成记录拒绝、3正常/等价接受及12药水/Smart协议负控。控制集合只复验一次，没有为每个小改动重跑完整原生矩阵。
- `verification.json`：115项来源、实际动作、每步完整状态/RNG、父fork、Block重放、Energy规则、失败前缀、策略设置、输入和旧证据锁核对通过。检查数量不表示115场对局或完整角色覆盖。
- `build-id-checks.json`：两版manifest按原算法重算；源码、compiled、artifacts和固定输入验证。旧构建保留原身份，未用最终ID覆盖失败producer。
- `plan.json`、`ledger.json`、`target-guard-repair-decision.json`：原预算及最后一次执行用途变更。四个raw exits为 **3、3、1、0**；只有最后一次成功。进程累计约18.19秒，所有尝试在20分钟窗口和120秒进程门内。
- `change-scope.json`、`closeout.json`、`execution-lock.json`、`work-lock.json`：4个生产文件改动、预算测量、结论与保留证据。收尾前保守计量约0.58GiB，本批新增约0.12GiB，未删除或有损替换原始产物。

15锁定输入、52兼容源码、826固定solver源文件及6程序集、旧原生执行/工作/独审证据锁保持一致。游戏固定v0.111.0/41cef1ea，solver固定Lab衍生0.50.1，MIT及上游归属保留。本批没有改固定依赖DLL或增加补丁。

四条记录的隔离均为 MemorySaveStore、ShouldSave=false、Steam未初始化、无UI实例、单机网络服务、零遥测，MyMod/BaseLib未加载。两次求解器构建均4 warning/0 error（Publicizer及有非空前提的owner nullable）；本批未重新编译或执行默认配置，不将以前默认构建结果当作本批新执行。最终M1同构建全量门保留。

## 收口及下一步

已完成真实主动用药和实际再求解。最小剩余是：**保持 final02不改代码，使用尝试04的 `route.json`/`choices.json` 做一次同版本完整 replay**，核对首动作、实际药水移除/能量、完整状态/RNG/continuation和再查询。新执行须另登记仅此目的的有限额度，不能重置本批ledger或删除失败。

重放补齐后，把本批与既有动作派发、旧票据、Oracle/捕获、目标攻击/EndTurn、TrueGrit在线子选择、药水身份和失败传播证据一起交一次综合独立审查。此处的实现验证不是独立审查通过声明，不建议为本次目标检查小改动另开一轮全量原生验收。

非零重复实例、连续/回合开始选择、药水槽替换、晚到非法选择的实际未确认动作前缀、终态和整场仍未完整覆盖。第2步和M1尚不能关闭；没有新增自然BossVictory或RunVictory，也没有平衡或胜率结论。

本轮没有权限/环境阻塞。修改仅位于4个Lab生产文件、本文及README；未改角色项目、游戏安装、真实用户存档或其他项目，未启动游戏UI、真实Steam/网络、提交或推送。
