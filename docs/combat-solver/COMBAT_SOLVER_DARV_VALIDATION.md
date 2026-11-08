# 原生 Darv 与第二幕首战：有限接入验证

2026-11-04。本轮复用既有原生先古房间/选择桥，完成 Darv 非首项选项领取和第二幕首战三个在线动作、一次敌方回合。相同 producer 完整重放到 161 个根动作、122 个实际求解步骤，状态/RNG/continuation 一致。当前真实停止为 Unsupported：q516 的 Hellraiser 带 Swift 附魔，现有动作协议拒绝非空 CardEnchantmentId，未派发该牌。第二幕首战未胜，第5步及M1尚未完成。

## 版本与复用范围

游戏固定 v0.111.0/41cef1ea，Ironclad/A0，原种子 LAB_V0111_NATIVE_DEV_01。CombatSolver 0.50.1 的配置、搜索评分和死亡保护保持，所有新场景复用原生宿主、现有整战循环、动作/选择桥和失败分类。预登记普通选择规则为：从实际展示、未锁定、没有即时拾取效果且有明确持久状态契约的遗物中，按原生选项顺序取第一项；未支持选项不会默认执行。

RandomForeseer 参考版本0.13.19，min_game_version=0.111.0，完整Mod声明RitsuLib最低0.5.14，MIT/copyright2026 hotwords123。本轮未构建或初始化完整Mod，只复用选定源码：OutOfCombatPredictionUtils 的指定稀有度遗物袋预测方法，以及 PredictionExtensions.Clone(RelicGrabBag) 的原方法体；下一幕观察提取 NextActPrediction 读取已经生成的 act 内容的逻辑，改为标量输出。源快照7个文件含版本/完整许可，位于 `.build/pinned-inputs/randomforeseer-0.13.19-native02/`，有独立 input-lock；构建使用显式 ForeseerSnapshot 参数核验哈希并记录来源。native01保留初始快照历史。THIRD_PARTY_NOTICES保留归属及MIT文本。没有调用Entry、UI、遥测、配置读写或新增Mod补丁。

这不构成完整 RandomForeseer SDK：原 RunPredictionContext 仍持有 live Player/RunState，本轮没有假定它可隔离多步复制，也没有另写平行随机效果引擎。遗物袋预测只在实际提供CallingBell时可调用，必须检查完整观察状态/RNG及真实遗物袋序列化前后不变。本次未提供CallingBell，因此该方法仅编译/来源核验，**未取得原生预测差分证据**。

## 原生路径与逐节点观察

原正常开局110根动作输入保持；之后仍用相同Coordinator。前155根动作与上一批Boss胜利/奖励/跨幕证据完全相同。新profile标记prefix-replay/live-tail；重放标记frozen-prefix-and-tail-replay，没有初始化资源/血量/牌组/房间夹具。

| 原生边界 | 实际执行及预测范围 |
| --- | --- |
| a154之后、ActReady之前 | 复用RandomForeseer的已生成下一幕内容读取，观察Darv与KnowledgeDemonBoss；前后完整状态/RNG不变，明确Oracle和已生成内容，不称未来多节点模拟 |
| a155 | 原生跨幕完成，MapRoom真实内容与上述观察一致 |
| a156 | 复用NextActStart，正常进入Darv，A0原生回血30→80；实际选项SneckoEye、VelvetChoker、DustyTome。三项拾取预测均标UnsupportedBySelectedSourceSubset，不捏造结果 |
| a157 | 原生选择索引1、VelvetChoker；原生任务完成，具体新遗物进入持有列表。除该遗物和事件完成/选项列表外，完整已声明状态、RNG、牌组及资源保持 |
| a158 | 原生合法地图边进入第二幕首个Monster：ThievingHopperWeak |
| a159–a161 / q513–515 | Coordinator实际选择FlameBarrier、PerfectedStrike、EndTurn；分别影子执行、原生派发、完整状态/RNG差分并重新求解，包含实际敌方行动及玩家第2回合 |
| q516 | 原生状态HP80、金币241、永久牌组19张；求解建议Hellraiser，CardEnchantmentId=SWIFT。Fields在影子/原生派发前明确拒绝，最后确认仍a161 |

实际Darv初始化还会提前为**未选择的DustyTome**准备AncientCard，原生SetupForPlayer消耗一次玩家Rewards RNG；事件自身在固定标准第二幕配置消耗22次（11组取样、10次洗牌、1次是否提供DustyTome）。本轮记录了这两条真实流，生产检查器独立校验DustyTome那一次xoshiro推进及其余完整状态；没有把未选选项当成完全无状态影响。这个计数范围只声明固定标准A0配置，不推广到自定义modifier或所有Darv环境。

本轮新Coordinator查询没有预测玩家死亡；未来死亡实际派发、真实取消/TimeLimit、致死保护和复活均没有新增实证。没有对Boss或本场战斗调参。第二幕Boss、余下节点、奖励和完整RunVictory仍待推进。

## 保留的失败与同构建重放

第一次原生尝试以EngineError退出1：新流程一度误触发旧“第二幕专用序列化”开关，冻结前缀首个边界出现格式差异，1派发/0确认，0战斗查询。原始失败在solver-darv-exec-01完整保留，不记为正确性通过。修复将流程是否推进下一战与旧专用CardRecords采集开关分开；旧专用场景的额外序列化字段保持，新的Darv场景使用原认证前缀的观察格式，所有现有字段及完整continuation仍逐项比较。**本profile未新增原生save-card序列化覆盖**，不把它冒称完整内部玩家状态。

修正后的live/replay（exec-02/03）使用相同producer：

`BEC37F2AB45FC791934C727C4DD4127EFEF14ED2D926C6ECC2249172685B7E95`

目录 `.build/native-build/solver-darv-final04/`。两次均原始退出2/Unsupported/Ongoing/DataComplete=false，161/161确认，普通/奖励选择、所有122份step、查询状态与候选、最终完整状态及预测记录一致；拒绝Swift的q516也保留且一致。没有把检查器退出0写成原生执行成功。

后续只修证据读取，最终reader为：

`9D0AD4AFBDEDD9FEFB117DFD18975EE624D03C5DC6D6B2558F7C9083FB9EB2B2`

目录 `solver-darv-reader07/`。它补齐Darv原生选项/初始化RNG契约、Foreseer标量观察的归属/必需性/完整状态与比较，以及实际不支持动作字段的停止证明。reader05/06对新Darv证据的早期拒绝日志保留，随后重读相同原始证据，没有额外原生执行。源/构建/采集器身份分列；M1最终同构建全套门仍保留。

## 验证、预算与复现

540项事实核对通过，包含初始失败保留、155动作旧前缀、真实Darv非首项、预测纯度、原生第二幕三动作、完整重放及输入/旧证据哈希。17个候选规则控制逻辑未变；最终检查器10次定向调用均符合预期：两份新证据及旧8步/旧Boss证据通过，错误预测模型、缺必要观察、遗物袋不纯、错误所选遗物、缺依据的Unsupported理由、DustyTome RNG计数矛盾均拒绝。控制都是明确派生副本，未修改原始数据。

批次上限3次原生执行、20分钟、2GiB、每进程120秒，切换后128根动作、每战64次查询。实际3次已用完并关闭，时间/存储见closeout；全部进程低于120秒，无watchdog触发。保存/replay均被隔离，遥测和异步fault为0。15个旧输入、52兼容源码、826 Solver源码和6程序集保持；7个RF来源文件固定；948份上一批原始执行文件未变。构建4个既有warning/0error；首个编译缺Clone扩展的失败也保留，补用上游原Clone方法后构建成功。

原始目录 `.build/runs/solver-darv-exec-01/02/03/`；计划、输入锁、账本、facts.py、controls.py、读取日志和closeout在 `.build/runs/solver-darv-work01/`。在新副本上使用最终reader的 `--review-solver-coordinator-prefix`；不向已关闭批次追加执行。没有机械重跑无关原生全量，也没有提交推送。

下一项实际缺口是 `NativeSolverActionContract.Fields` 的附魔字段门。应先核对并复用CombatSolver既有Swift支持、稳定牌实例/附魔绑定和原生ManualPlay，再做有预算的逐步差分与同构建重放；保持未知附魔拒绝，不能清空CardEnchantmentId或更换已领取奖励来避开。之后继续第二幕及多幕控制器。CallingBell预测仍未实跑、此次三项Darv拾取预测仍未覆盖、整体策略和M1均未完成。
