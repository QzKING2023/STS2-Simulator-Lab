# 4D3 准备路径、目标胜利与跨幕独立审查

结论：**未通过，存在 R1–R3 三类必修。** 原始原生执行和版本一致性已核实；生产证据检查器仍可把矛盾证据认证为通过。因此不能关闭本轮可信数据门，也不能用本报告作为正常开局完整爬塔通过结论。

审查冻结 `FAEC900A695EEDA64586342FB70DF2F118F5B4A941296BA34BF714B064CB423C`，Native DLL SHA256 `3752953ADF7580AC301646D3478B5EE4DA23D0A992773C7E14D9330E34583E0C`。证据目录 `.build/runs/n3d3-preparation-independent-review01/`，汇总 `closeout.json`、`reader-summary.json`、`verification.json`、`facts.json`。本次只读生产代码，新增审查文档和隔离副本；没有启动原生游戏运行、UI、真实 Steam/网络/用户存档，没有修改生产实现、Mod 或安装，没有提交推送。

## 已成立的执行事实

| 路径 | 实际结果 | 原生证据 |
| --- | --- | --- |
| Elite DEV03 | 113/113，5 场准备战斗胜利后，固定 BYGONE_EFFIGY_ELITE 胜利 | 目标房间 i199，胜利 a112、奖励 a113；35 Gold、PommelStrike i215、MealTicket i212；满槽药水失败后实际丢弃、领取 CureAll；HP12→18 为真实战后治疗 |
| Boss DEV03 | 184/184，9 场准备战斗胜利后，固定 CEREMONIAL_BEAST_BOSS 胜利并实际跨幕 | 目标房间 i368，胜利 a182、奖励 a183、ActReady a184；100 Gold、DarkEmbrace i403；HP11→17；真实下一幕 act1/floor0/node null/MapRoom，mapGeneration2 |
| Boss DEV01 / DEV02 | 93/93、156/156，准备阶段真实 Death | 失败样本保留，没有被成功种子替换 |
| Natural DEV01 | 77/77，真实 Elite Death | 无 Debug 跳转；不能据此声称自然精英胜利 |

两个成功目标都是 `api-fixture`，由原生正常开局及合法准备流程后显式 `EnterRoomDebug` 进入固定目标；没有手改 HP、金币、牌组来制造胜利。它们证明有限目标和真实跨幕调用链，不证明自然 Boss 路径、下一幕首节点、完整 RunVictory。

代码链：`NativePreparation.cs` 的 PreparedMajor → MajorBattleAndRewards → 原生 TryManualPlay / EndPlayerTurnAction → EndCombatInternal 观察 → RewardsCmd.OfferForRoomEnd / 原生选择同步器；Boss 之后调用 SetLocalPlayerReady，等待 ActionExecutor 及实际 NativeLifecycle.ActTask。`NativeLifecycle.cs:43`–`50` 包装原 Task 并等待，异常重新抛出。没有以观察记录代替业务执行。

固定 SDK 的 ActChangeSynchronizer 在调用 EnterNextAct 前执行 ActFloor 加一；EnterNextAct → EnterAct → ExitCurrentRooms / SetActInternal / GenerateMap / MapRoom / AfterActEntered。当前成功样本 start 的完整变化只有 floor+1 与 ActStarts+1；完成时合法清除旧房间/战斗上下文，重置节点和层数，生成新 MapRoom。持久资源、完整库存、三组 RNG 在该样本保持。该实际路径不是零售演出等价声明。

## R1：胜利回调快照没有绑定事实或完整观察状态

位置：`tools/native-run/NativeMajorContract.cs:177`–`186`。`battle-victory-before-cleanup` 只核对当前战斗动作、房间及布尔流程状态，随后把 `won` 设为 true；没有验证存活玩家、真实胜利时的战斗终止条件，也没有绑定最后有效原生观察的完整状态。

9 个误接受副本：`case-011`–`case-017`、`case-364`–`case-365`，生产检查器均返回 **0**。

- 单改目标胜利观察：HP=0、Gold=9999、energy=99、Shuffle RNG s0=1、嵌套新增字段、inProgress=false 均通过。
- `case-017` 把动作派发时仍活着的 Boss 敌人列表放回胜利观察，后续记录保持原样，仍通过。
- `case-364/365` 同时改最后一条原生历史和胜利观察为 HP0 / 存活 Boss，仍通过。因此仅把胜利快照与最后历史做相等比较还不够。

最小修复：明确固定版本的胜利前置事实与观察时序，将完整胜利观察绑定到实际当前动作及有效原生观察锚点；同时验证原生胜利条件，拒绝自洽但相互作假的历史/胜利组合。detailed 与 minimal/off 的可用锚点需明确，不可因没有详细历史而真空通过。保留真实原生战后治疗和清场变化，不能把胜利前后要求整体相等；当前成功样本 HP+6 是正例。

## R2：ActReady / EnterNextAct 的身份、入口与完成框架缺口

位置：`NativeMajorContract.cs:84`–`85`、`:124`、`:196`–`202`。开始仅验证布尔流程和动作种类；完成仅验证 act+1、mapGeneration 变大与六个持久字段。它没有约束实际入口完整状态，也没有验证新 MapRoom、节点/层数重置及完整状态的合法变化范围。ActReady 三份动作内容彼此一致却仍可不对应当前 act/map，ready-start 的 room 事实也可指向旧房间。

21 个误接受副本：`case-018`–`022`、`024`–`028`、`031`–`039`、`043`–`044`，均返回 **0**。

- 入口可单改 HP、Gold、node、act、RNG、嵌套字段，然后在完成时恢复，检查器不拒绝。
- 完成与后续静止记录共同改为错误 room/node/floor/block/energy/map代次、房间栈、三组 RNG 或嵌套字段，仍通过。
- `case-039` 把完成至末尾的房间类型、具体房间引用和 majorCombat 共同改回旧 Boss 房间；act仍为1，却被认证为已完成跨幕。
- `case-043` 的 ready-start.room 指向更早普通战斗；`case-044` 把 request/dispatch/confirmed 中 ActReady decision 同时改为 act8/map9，均通过。

最小修复：把 ActReady 参数和事实绑定当前已确认的目标 Boss 房间、act/map；入口完整状态从不可变派发/ready-start 推导，只允许固定 SDK 实际发生的投票记账变化。完成记录须核实新 MapRoom 具体身份、合法节点/层数、地图代次与旧战斗清理，并从入口完整状态建立明确的变更契约，后续静止记录绑定该已验证结果。不能继续用少数字段白名单，或信任完成记录自己提供的状态作唯一锚点。实际合法跨幕与真实 Hook 变化保留；当前有限 profile 之外尚未支持的变化应明确界定，不要求本轮实现全部遗物。

## R3：本次精英 RelicReward 取得后的类型与业务状态缺口

位置：`tools/native-run/NativeRewardContract.cs:253`–`259`、`:310`–`319`。RelicReward 的成功分支只核对所报 acquired 引用等于奖励 content、进入 relics 且此前不存在；没有将新条目的 model/完整内容绑定登记身份与实际提供内容，也没有约束领取造成的完整状态变化。Gold/Card/Potion 有分支状态契约，当前 RelicReward 缺少对应保证。

4 个误接受副本：`case-358`–`case-361`，均返回 **0**。从真实 MealTicket 领取返回开始共同修改后续快照：

- 新遗物 i212 的 model 改为 BURNING_BLOOD，但奖励和 identity 登记仍是 MealTicket。
- Gold 或 HP 凭空增加100，随后所有父子/确认记录沿用被改状态。
- 新遗物内容加入未绑定的嵌套字段。

独立对照 `case-362/363` 的“acquired 指已持有遗物”和“删除实际新增实例”已正确拒绝，说明现有存在性检查有效，类型及状态框架仍缺失。固定 SDK MealTicket 没有拾取治疗/金币效果，仅进入 MerchantRoom 时有治疗 Hook；当前为 CombatRoom 领取。原始新实例、类型、金币和 HP 均正确，问题是检查器不能区分坏副本。

最小修复：绑定实际新引用、登记 type/model、offered content 与新持有条目；为当前原生遗物取得建立完整状态变化与 Hook 契约。对没有拾取效果的当前 MealTicket，新增正确遗物之外不得凭空改资源或嵌套状态。需要拾取业务变化的已有受支持分支应通过真实子操作/观察记录闭合，不能一概要求所有遗物只追加条目，也不能让未知 Hook 的任意变化直接通过。

## 独立验证范围与计数

- 生产读取器实际调用 **369 次**：31 个正常/等价副本通过，304 个坏/不完整副本拒绝，**34 个坏副本误接受**。整体审查失败，不能用脚本正常完成或原 suite 退出0代替此结论。
- 其中本次独立构造/原始正例90次；实现方39控制及历史240控制均重读并保持预期。原8跨房反例、前29反例和82阶段控制继续全部拒绝。
- 原始准备样本14次的详细/重放/观察模式均通过读取器；冻结路线、子选择、奖励选择、完整最终状态（含所有 RNG）逐结构相等。两组合法属性重排副本通过，真实治疗/清场/奖励/抽牌/RNG变化没有被删除。
- 独立1028项文件、冻结、来源、实际执行、状态/实例及重放核对通过：81源文件、49编译副本、3游戏输入、7产物与哈希；实际323执行/4074 suite检查，另Core98；原964项验证文件的计数和通过记录可复核。
- 原始退出码为240×0、70×1、8×2、5×3。新准备成功路径为 SliceBoundary/Ongoing；Death和旧Unsupported/EngineError/BudgetExceeded/DataError前缀各自保留。开局前失败控制 networkService=null 合法，未被误称实际运行。
- 15锁定输入、52兼容源码、原3011份保存文件及本轮复制的原证据哈希保持。只在标记副本上重算 seal，再调用同一冻结 DLL 的 `--review`，拒绝不是靠坏hash/坏JSON获得。
- 一次独立分析辅助脚本把无选项的外层 operation 当数组，另一次初版检查误要求开局前失败也有已启动网络服务；均只修正本轮分析工具，并保留 `verify-first.py`、`verify-first-error.txt`、`verification-first.json`。不属于生产引擎失败，不计作通过或原生执行。

## 复现与下一步

单反例复现（输出应为1，当前为0；仅修改该审查副本中的 trace-review.json，不碰原证据）：

```powershell
dotnet .build/native-build/n3d3-preparation-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-preparation-independent-review01/case-017
dotnet .build/native-build/n3d3-preparation-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-preparation-independent-review01/case-039
dotnet .build/native-build/n3d3-preparation-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-preparation-independent-review01/case-358
```

`review.py` 和 `review-linked.py` 保存全部构造方式；每个 case 含 trace、seal、result、process、reseal.log 和实际 production-review.log，误接受还生成 trace-review.json。`reader-summary.json` 包含具体预期/实际退出、分组与来源。不要覆盖旧反例，修复后用新冻结读取副本。

先集中修 R1–R3，保留合法原生流程和失败分母，同一最终产物回归后再独立验收。通过后接既有 PREPARATION_NEXT_HANDOFF 的自然 Boss、下一幕首节点和通用 run 组合；现阶段没有执行环境或授权阻塞，但可信数据门尚未通过。后续全部 M1 授权继续有效，本轮不开始下一实现阶段。
