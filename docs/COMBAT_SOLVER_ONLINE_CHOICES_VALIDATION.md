# CombatSolver 在线子选择、身份修复与药水预算路径

2026-11-04。本批收口：**四个身份反例已关闭；求解器主动采用子选择的在线 live/replay 通过；药水在线采用尚未覆盖。第 2 步及 M1 未完成。**

## 已完成的修复

依据 `COMBAT_SOLVER_CHOICES_POTIONS_INDEPENDENT_REVIEW.md`：

- 子选择候选现在按实际手牌 id 查找，核对 model、upgrade、捕获 key、计划 token、source/option occurrence、原生返回及实际耗尽对象。候选描述不能通过同时改写计划模型/升级来冒用未升级 Defend。
- 药水计划 slot 必须等于实际派发 slot；映射 model 必须等于同 id 原生持有药水 model；槽位和实例映射要求唯一。计划和附属映射同时改写仍会被真实派发/状态拒绝。
- `solver-online-repair01` 构建先复验 24 个生产 reader 控制，最终在线构建再次复验同一集合，均符合预期。四个新反例、旧五反例全部拒绝，7 个合法/等价副本接受。重复执行是同一覆盖的回归，不是 48 个独立场景。

这些检查器修复没有触发原生游戏重跑。随后接口改动才启动下述预登记的小批执行。

## 在线闭环与复用

从原 `SolverLoop` 抽出共享 `ExecuteSolverAction`，原基础循环继续使用窄模式。新 `SolverOnlineFixture` 在真实 `SolverQuery` 返回之后采用未修改的第一个 PlanAction，执行、比较并重新求解；没有按目标能力过滤/改排求解结果。

扩展模式复用固定 CombatSolver 现有的：

- `GetActionChoicesInExecutionOrder`、`BeginActionChoices/EndActionChoices` 和 `NativeSolverChoices`，原生请求发生在当前动作范围内，必须完整消费计划。
- 实际卡牌 StateKey/实例映射和实际目标绑定，以及消费一次和未消费旧票据跨动作拒绝。
- fork 自身的药水槽、`PotionOnUseSupport.CanSearch`、`PotionExecutionSupport.Prepare/Complete`，原生使用 `EnqueueManualUse` 并等待 ActionExecutor。
- 原 EndTurn 的 `AdvanceRound`、边界稳定和实际敌方/下一玩家回合执行。

未新增搜索算法、评分权重或 Boss 特判，Smart 药水策略保持原值。动作内能力当前限已声明的 Action/Exhaust/Hand；嵌套、回合开始等不支持选择明确拒绝。终态预测仍在原生派发前报 Unsupported，未假称认证完整战斗。

`NativeSolverOnlineContract` 要求查询/捕获/影子/派发/确认/再查询的完整集合、顺序和归属，完整静止状态、实际首动作、选择及药水身份、票据和 continuation。选择 reader 复用已经修复的同一子选择契约。默认原生配置可编译且仍无求解器入口初始化。

## 预登记输入与真实执行

`plan.json` 在原生执行前保存，上限 4 次尝试、20 分钟、2 GiB；每进程硬限 120 秒，每条路径最多 8 个实际动作/9 次查询；每次求解 1000 节点、5 秒软门、DOP=1。种子固定 `LAB_V0111_NATIVE_DEV_01`。

两个输入都是明确的 **API 夹具**，从正常 Ironclad 首战开始：以原生 CardPileCmd 将初始手牌移入 Exhaust，永久牌组保持原内容。选择场景通过原生 CreateCard/Add/Upgrade 建立升级 TrueGrit 和两张 Wound；药水场景保留空手牌，通过 PotionCmd.TryToProcure 获得 BlockPotion。没有改 HP、能量、敌人、评分或策略来迫使结果。

| 尝试 | 实际结果 |
| --- | --- |
| 01 `solver-online-choice` live | exit 0；5/5 根动作确认，其中 1 个在线战斗动作。求解器主动选择 TrueGrit+，并提供耗尽 Wound i29 的 token；原生执行获得 9 格挡、消耗 1 能量，i29 耗尽、i30 留在手牌；再查询首动作 EndTurn |
| 02 同构建选择 frozen-replay | exit 0；重新求解仍返回同一实际动作与子选择；完整 route、choices、状态/RNG 和 continuation 重放一致 |
| 03 `solver-online-potion` live | exit 3 `BudgetExceeded`，5/5 确认前缀。第一次求解选择 EndTurn，原生 HP 80→68、进入下一玩家回合，精确差分。第二次查询在 1000 节点停止，候选未采用、药水未使用 |
| 04 同构建药水失败前缀 frozen-replay | exit 3，同一 EndTurn、同一完整状态/RNG/route/choices，第二次仍 NodeLimit1000；没有伪造完成摘要 |

03 发生后，最后一个额度用于原输入、原构建的失败重放，决定事先记录在 `failure-replay-decision.json`。它替代原成功重放槽位，没有替换失败样本、场景、种子或策略。失败 raw exit=3、DataComplete=false 和缺失成功摘要均保留；成功 reader 对两份预算失败记录均拒绝将其认证为成功，另由事实核对器验证实际执行前缀及重放。

四个进程共约 **23.41 秒**，从首次注册执行至最后完成约 1 分 46 秒。四次额度已用完，没有第五次原生执行。收尾时保守预算目录测量约 0.60 GiB（含历史相关构建），本批新增约 0.15 GiB，均小于 2 GiB。原始数据未删除或压缩替换。

选择路径的候选 index=0、重复 Wound occurrence=0；本批不认证非首项或非零重复实例的在线选择。上一批固定 TrueGrit/Defend 非首项及 FirePotion 实际使用的证据保留，但不能用来填补本批在线药水采用缺口。

## 冻结与验证范围

| 用途 | 构建 ID |
| --- | --- |
| 纯检查器身份修复 | `8E31D25B0F9194FE15FC7F821A99170D46D540722BF025F50F59D0C938DD9F4B` |
| 四次实际原生执行及最终生产检查器 | `BFCE3DB34859BA2A4944F742488C6BC2D11B0AA1012A15B2C5E3E079B8671101` |
| 默认原生配置编译 | `0CB156300B5CBCFA0D345D2988B739DFFF374ED0D2E56B4EC3022D6B6B36250A` |

最终生成器和 reader 是同一构建；三个 ID 均按 manifest 原算法重算，源码、输入、compiled 和 artifacts 核对。纯修复构建之后确有在线运行代码变化，不能把它与最终构建说成同一个产物。默认配置只有编译，无本批新默认原生 gameplay。

证据：`.build/runs/solver-online-work01/`。

- `repair-controls.json`、`final-repair-controls.json`：两阶段的同一 24 控制，均按预期。
- `online-controls.json`：17 次实际生产 reader 调用，3 个成功/显示等价副本接受，12 个错误副本和两份非完成预算记录拒绝；包括真实手牌模型/升级、source/option occurrence/key、缺子选择、多余嵌套、未采用首动作、完整 RNG、缺捕获、完成计数和旧票据语义。
- `verification.json`：78 项核对全通过，包括同构建、每步完整 continuation、父 fork、原生来源及记录的全部 RNG、原生实际效果、两种重放、失败传播、隔离、原始数据锁及 15 输入/52 兼容源码。其控制集合核对不与 reader 次数相加作覆盖宣传。
- `build-id-checks.json`、`change-scope.json`：构建身份与变更范围。
- `ledger.json`、`plan.json`、`failure-replay-decision.json`：预登记、实际消耗和失败重放决策。
- `closeout.json`、`execution-lock.json`、`work-lock.json`：最终口径与保留证据。

原始执行目录为 `.build/runs/solver-online-exec-01/` 至 `04/`。`solver-online-step-1.json` 记录实际采用的 query1 首动作；query2 是执行后真实状态重新求解。成功选择的两份文件包含实际原生 request/response/returned 对象，药水失败路径无 UsePotion 派发。

游戏仍为 v0.111.0/41cef1ea；固定 CombatSolver Lab 衍生 0.50.1 DLL SHA256 `4503D5234915B4B0C163556C6FDB4FA0C38F45796F656AB4B2C7E76A1BF7D98B`，826 源文件和 6 DLL 锁未变，MIT 及上游归属保留。15 个角色输入和 52 个兼容源码未改，先前原始执行、工作和独审锁未改。

实际执行隔离检查均为 MemorySaveStore、ShouldSave=false、Steam 未初始化、无 UI、单机网络服务、零遥测，MyMod/BaseLib 未加载；没有放宽根审计或安装新补丁。求解器构建 3 warning/0 error（Publicizer 和有非空前提的 owner nullable），默认构建 1 warning/0 error（默认未赋值 provider）。

本批按影响范围验证：检查器修复重读旧原始证据；共享执行接口变化用真实在线选择及真实 EndTurn 的 live/replay，并编译默认配置。没有机械重跑旧完整原生矩阵；也没有据此声称已在新构建重新执行目标攻击、默认自然主线或全部药水。最终 M1 同构建全量门保留。

## 下一步

本批没有权限或环境阻塞。技术上已建立真实在线子选择闭环；药水桥已经进入同一个执行器，但本批求解器没有采用它，NodeLimit 是未完成结果。

下一有界批次先复用上游已有 Smart 药水场景与候选诊断，核实为什么当前路径不采用药水及候选是否被纳入；依据现有明确规则选择输入，避免反复随机改夹具或针对单场调评分。再预登记一次在线药水采用和完整重放，保留本批失败分母。不能把旧固定 FirePotion 使用或强制药水指令称为默认策略主动采用。

随后仍需非零重复实例、晚到/缺失/多余选择实际失败前缀、必要连续/回合开始选择、药水槽替换、终态与整场。当前两类路径都是 API 输入夹具，尚未增加自然 BossVictory 或 RunVictory；M1 自然通关及数据收口继续后置验收。

生产改动为 12 个 `tools/native-run` 文件，具体见 `change-scope.json`；另新增本文并更新 README 入口。原有 dirty 工作保留，没有更改角色项目、安装或真实用户存档，没有游戏 UI/真实 Steam/网络操作，没有提交推送。
