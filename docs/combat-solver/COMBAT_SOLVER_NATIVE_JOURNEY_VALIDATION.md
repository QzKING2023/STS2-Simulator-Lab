# CombatSolver 第5步首批原生节点集成

2026-11-04。第4步报告绑定修复已定向通过，见 [修复报告](COMBAT_SOLVER_RUN_LOOP_REVIEW_REPAIRS.md)。第5步已将既有原生节点控制器的战斗策略替换为固定CombatSolver，完成一条可重放的自然第一幕前缀。**当前为五场真实胜利后预算停止，未到Boss，不是完整第一幕、RunVictory或M1验收完成。**

## 实际执行与停止

固定 `v0.111.0/41cef1ea`、Ironclad/A0、种子 `LAB_V0111_NATIVE_DEV_01`、独立实验档案。`solver-native-boss` 从正常Neow开局，复用 `NaturalBoss`、`MajorTravel`、`MajorBattleAndRewards`、原生事件/休息/宝箱入口；只替换Battle内部策略。没有FixtureSetup、状态拼接、改HP/资源、强制候选或单Boss评分调整。

最终原始目录 `.build/runs/solver-journey-exec-03/04`（live及精确冻结重放）均：

- 80个总根动作派发并确认；56个求解器动作，逐步执行、核对并重新求解，五场原生Victory及五次实际奖励。
- 11个实际进入的房间：6战斗、2事件、2休息、1宝箱。事件实际选择SunkenStatue入水、DenseVegetation继续；两次SMITH升级，宝箱由原生领取/退出。没有跳过未知业务来拼接路径。
- 求解器实际主动使用原生取得的ShipInABottle（槽1）与BlockPotion（槽0），实际派发/原生效果/指定槽移除、局部continuation和RNG一致。此处新增自然路径实例，不泛化为所有药水或未支持的子选择。
- 到第六战 Slimes、第一幕 `MapCoord (6, 11)`，HP75、金币546、牌组15张。query321返回NodeLimit，实际展开1000节点；本次候选未采用、没有对应step或新增派发。
- 真实退出均为 **3 / Ongoing / BudgetExceeded / NotCompared / DataComplete=false**，最后确认边界Travel:80。没有实际Death、BossVictory或RunVictory。

两次route、choices、reward-choices、final-state和全部56个step相等；全部57个query的定义内完整原生状态、binding、动作、continuation、Profile和边界相等。查询及shadow不扰动原状态/父fork/RNG，跨战旧未消费票据拒绝。这里的完整限已定义的状态及continuation，不是所有游戏私有字段。

## 保留的首次缺口与修复

原始01/02使用首个集成构建，同样有五胜、80动作及相同预算停止/重放；但新节点会话要求用药的UsePotion→potion-use父子观察，而旧solver传输只记录根动作。生产检查器实际拒绝：`Reward confirmation before return/state mismatch`。这两份保持为观察契约不完整的历史结果，不算已认证前缀；最初控制中的正常副本误拒及因共同前提失败的负控也保留，不计作最终有效门。

修复复用 `CAction`、`NativeRewards.Begin/End` 包裹原来同一个原生药水调用。solver决策的字符串实例ID只在此新profile中显式转换成既有奖励契约的已登记 `$ref`，精确比对主体、槽、目标和消费。其他profile保持既有表示和严格检查。没有放宽审计、删除快照比较或改变药水效果/搜索策略。因为采集链变化，在原四次额度内新增03及04，不拿旧缺记录数据代替新证据。

## 生产预算前缀检查

新增显式 `--review-solver-prefix`，只接受本自然scenario的预算3、Ongoing、DataComplete=false、无未确认根动作/选择/任务的证据。复用原链/引用/动作/房间/奖励/完整快照检查、既有安全未完成战斗契约及逐场solver差分；额外绑定尚未采用的query321到最后确认状态、动作上下文、源不扰动和真实NodeLimit/TimeLimit。

输出明确为 `prefix-validated-budget-not-completed`，同时保留rawExit=3、dataComplete=false。普通 `--review` 继续拒绝把本预算路径认证成完整成功。原异常路径没有最终history-checkpoint；前缀检查不宣称补出了该记录或整个run完整数据，只核验已有历史顺序/关联与已完成战斗清理，并在报告中标明checkpoint缺失。

证据位于 `.build/runs/solver-journey-work01/`：`plan.json`、不可重置的`ledger.json`、`facts.json`、`execution-facts.json`、`final-reader-controls.json`及closeout。63项事实/版本/重放核对通过。最终生产控制覆盖正常预算live/replay、合法等价、普通完整检查拒绝、错误退出/计数、假预算、查询源变更、错误实例、错battle偏移、返回快照新增字段、缺返回、旧动作归属、旧缺药水会话证据拒绝，以及第3/4步八份正常回归；最终具体计数和退出以closeout为准。控制仅操作标记副本，无新原生全量执行。

## 构建、预算与隔离

第4步修复reader：`C58A313ACDAC7DF51ACAFFDC988BA40E7BEF365B365FE1062E5238B9FF9A02EF`。

本批首个producer：`CBECA8FB8B815E3102A6CD2B26585410A81C116F4012CEB1F4FF9C2641E8AF92`，原生01/02；中间reader：`040D2BF02F5629B7BF9EFC3CEA38F48EC405851ED05D5ABF74F5F642CEB93CD5`。

最终同构建producer/reader：`A805EFC494A8FAC2424246BBC75FA52966DB5B1595DD3B6FB1B38112935F0C9B`，`.build/native-build/solver-journey-final02/`，实际原生03/04。没有混称四次均由最后构建产生。各构建4个已有warning、0error。

预登记上限4次原生、20分钟、2GiB、单进程120秒、每战64动作/查询、总5000根动作；搜索仍为beam8、每查询1000节点、soft5秒、单worker。四次用时合计120.49秒，全部在原截止前；额度已用尽，没有第五次或擅自增加搜索节点。最终字节盘点见closeout。

15个锁定输入、52个兼容源码、solver826源/6程序集及许可保持。MemorySaveStore、ShouldSave=false、Steam未初始化、单机服务、无UI/遥测/异步故障；每次SaveProgress/replay拦截18/5。未加载MyMod/BaseLib/RandomForeseer，没有写真实存档、安装、角色或其他项目，未提交推送。

## 下一步与RandomForeseer

第5步未完成。直接限制是通用求解预算策略：认证初期的“达到NodeLimit就拒绝采用候选”在合法新战斗首查询停止；这不证明该战斗不可胜，也不是牌效不兼容证据。下一批应先复用上游有界查询/候选语义，明确预算有限候选的采用规则与独立合法性/局部差分门，再预登记小规模自然推进及重放。保留本次1000节点严格停止基线，避免针对某个Boss调整评分或把预算结果改写为成功。仍需实际Boss、跨幕、后续节点和RunVictory，最终M1同构建全量门保留。

RandomForeseer设计按 [复用入口说明](../validation/RANDOM_FORESEER_REUSE_NOTE.md) 调整：优先其已有奖励/事件/先古/Boss相关计算或已生成状态读取，能力不足才补实现；预测与原生执行分别标识。Context只复制部分状态且持有live Player/RunState，AfterModifyingCardRewardOptions等已知多步限制禁止推断隔离多步精确。首次门仍是单节点预测→原生生成和真实状态/RNG不扰动对照，未知显式Unsupported。本轮只补项目设计说明，未加载或实证RandomForeseer，不以它阻断原生主线。Mod专项继续后置。
