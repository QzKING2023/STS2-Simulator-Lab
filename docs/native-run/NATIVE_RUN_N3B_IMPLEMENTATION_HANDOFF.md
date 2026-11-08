# M1 步骤 4B：房间驱动、休息与简单事件实施交接

日期：2026-11-02。前置：[4A最终独立通过](NATIVE_RUN_N3A_FINAL_REVIEW.md)。依据[主M1方案](../milestones/MILESTONE_1_NATIVE_RUN_PLAN.md)及[N3总交接](NATIVE_RUN_N3_IMPLEMENTATION_HANDOFF.md)。全六步已授权，下一轮可立即按本交接实施，无需重新申请步骤批准。

基线为Build ID `223DB2B31B0E199B694CFF24DBF08600F0A104AFCC095493EB907FD71538AA1F`，HEAD `d73039d688eb54c9513f5b9508a03947e689aa18`加现有dirty源码。固定游戏v0.111.0／41cef1ea、Ironclad/A0、独立内存档案、一run一进程、native-only／NotCompared。建议Sol xhigh / Standard实施，Astra xhigh / Standard独立验收；不据自称记录实际模型元数据。

## 目标与本回合边界

4B交付：内部房间控制器，真实Rest heal／smith／cancel，至少一个普通事件的非首项效果和多阶段完成，以及首条自然多节点片段。4B自然片段至少完成两场真实战斗、一个新非战斗节点，并在之后战斗的实际状态中定位该节点造成的持久变化。Rest与普通事件各有夹具覆盖；同一自然路径包含两类非战斗房间的最终集成门仍在4D，不把该要求悄悄取消。

宝箱／广义奖励／药水留4C，商店／嵌套事件战斗／精英Boss留4D，完整幕和run留5，批测和性能交付留6。遇新节点不跳过、不替换为战斗，不将未知合法选项伪称非法。新增原生transport属于普通Lab适配；不建立公开SDK或新增Mod专项。

## 当前代码事实与最小迁移

1. `Program.Battle`已按`BattleVictories-victoriesBefore`检查每战增量，4A已提供`WaitBoundary`、真实终态及失败前缀。保留这些成果，替换Main的单次Battle→Rewards→下战Play脚本为有限房间循环。
2. `NativeLifecycle.IsScenario`目前只登记tail／natural-death，且同时影响观察状态和旧终态reader。**不能只把新场景加入该谓词**：旧`NativeTerminalContract.Finish`会要求三种已登记尾部场景的事实，Architect身份连续性也不适用于一般事件。最小拆分为“启用哪些原生生命周期观察”和“采用哪个场景数据契约”，为4B添加有限room契约，保留旧4A／N2门。
3. `Program.Travel`已有Hook、当前Children和原生`EnterMapCoord`；还缺房间可离开凭据。新增room session绑定act/map generation/node/room实例，选项请求绑定当前room、候选顺序及边界票据。执行前重新核验合法性，不能只校验index范围。
4. 建议内部`NativeRooms`加Rest/Event两个实现，职责为读取当前原生候选、派发、等待、记录结果和可离开条件。不得重新生成选项或复制牌效。CardSelect子请求沿用`NativeChoices`，仍属于当前根动作，不在根动作尚未确认时另开平级root。
5. `Rewards()`仍为N2固定Gold/Card脚本。4B可复用其现有正常战后路径，并处理Rest的合法空自定义奖励；新必选／额外奖励类型必须明确Unsupported，通用集合驱动留4C。不能依赖TestMode默认首项或一律skip掩盖新增效果。

## 原生入口与本轮静态核验

### Rest

`RestSiteRoom.EnterInternal`调用`BeginRestSite`一次，生成原生options并执行入房Hook；`ChooseLocalOption`返回原生`Task<bool>`。同步器只检查完成状态及上界，不能替代Lab对负index、IsEnabled、旧对象／旧房间和重复答复的检查。

Heal执行`CreatureCmd.Heal`，再执行AfterRestSiteHeal、ModifyRestSiteHealRewards及OfferCustom。基本值为maxHP×0.3，不能因是休息而省略后续Hook／奖励任务。Smith走已有FromDeckForUpgrade，Cancelable=true、RequireManualConfirmation=true；空选择返回false，不升级且不消耗选项。成功后原生Hook决定清空剩余选项还是仅移除本项。

离开走原生`RestSiteRoom.Exit`：BeforeLocalRestSiteExited、AfterAllRestSitesCompleted、checksum。成功选项并不总等于房间无剩余选项；取消后仍应允许重新选择或显式离开。不要提前手工清空options或假完成TaskCompletionSource。

### 普通事件

`EventRoom.EnterInternal`负责BeginEvent、实际模型实例、AfterRoomEntered和AfterEventStarted。`OnRoomEnter`原本由布局触发，需按实际对象审查并恰好一次桥接，不能与AfterEventStarted混同。普通选项走EventSynchronizer；原生IsProceed选项按现有UI规则直接等待option.Chosen；事件完成后UI生成的通用离开按钮只是地图继续，不构造额外奖励或重新调用旧选项。

AwaitPendingOptionTasks会吞异常，继续用既有原始task与TaskHelper故障账本，Ensure后才确认root／允许travel。新event model的完整RNG、身份、完成状态和阶段必须进入4B快照，不沿用“N2只有Neow”的RNG说明。

**建议首个普通事件夹具用AbyssalBaths。** 已对固定DLL静态核实：非首项ABSTAIN调用原生Heal10后结束；IMMERSE依次GainMaxHp2、实际Damage3并增加下一次damage，再提供LINGER／EXIT_BATHS；EXIT_BATHS真实结束。两个有界脚本分别验证非首项效果和多阶段“任务已返回但房间尚未完成”，不跑无限LINGER。Immerse的HP净值须实施前再核对CreatureCmd.GainMaxHp语义并写独立常量断言，不能从运行后结果倒推。

本轮也核实DenseVegetation的REST包含音频／屏幕依赖并转入事件战斗；Wellspring涉及药水奖励或移除；ThisOrThat的ORNATE涉及随机遗物取得。它们不作为第一条简单事件的默认路径，后续按真实依赖处理，不凭名称认定可支持。

## 串行工作包与验收矩阵

可分为4B.1（控制器／Rest）、4B.2（事件／数据）、4B.3（自然集成／冻结）；按依赖推进，全部属于已授权4B。

| ID | 来源／执行 | 必须证明 |
| --- | --- | --- |
| B01 | API夹具，原生Rest入房 | 同一room只生成一次选项；候选来自当前对象；读取不消耗RNG或改变options |
| B02 | Heal夹具，无治疗修正，maxHP80/HP40 | 原生治疗到64；HP75时上限80；不从被测GetHealAmount作为唯一oracle；后续Hook与空奖励链完成 |
| B03 | Smith夹具 | 选择重复卡中的非0实例；仅指定永久对象升级一次，完整deck身份／其它牌／资源不变；后续战斗DeckVersion关联 |
| B04 | Smith取消→再选 | 显式合法空选择返回false；零升级、选项仍可用；再选择成功；取消与非法缺失答复有不同结果 |
| B05 | Rest离开／禁用控制 | disabled、负index、越界、重复、旧room／旧boundary预派发拒绝；显式放弃剩余选项经原生Exit，完成任务后才确认离开 |
| B06 | AbyssalBaths ABSTAIN夹具 | 真实非首项；无治疗修正时HP40→50；真实IsFinished；金钱／deck无意外变化；event RNG完整记录 |
| B07 | IMMERSE→EXIT_BATHS夹具 | 每步原生任务及资源来源；第一步完成但IsFinished=false，不能travel；选原生退出后才可离开；阶段变化使旧请求失效 |
| B08 | root/choice负控 | 锁定、错误实例、过期、缺失、多余、重复答复拒绝；如果真实前缀已发生，保留已派发／未确认root及实际资源，不伪称零派发 |
| B09 | 原生任务故障／预算 | 事件吞错仍EngineError；正在等待选牌／任务时不能travel；边界预算为BudgetExceeded并保留前缀 |
| B10 | 数据腐坏副本 | 缺room入／出／结果、错误room关联、效果前确认、重复完成、必需state/RNG缺失、静止边界资源漂移均由生产reader拒绝；正常等价副本通过 |
| B11 | 观察／重放 | 每个正脚本新进程冻结重放；Rest和事件至少各一条minimal/detailed对照，比较完整状态、候选、RNG与终止原因 |
| B12 | 自然多节点 | 正常Neow同run，至少两场Victory＋一个Rest/普通事件＋后续战斗实际Play；升级／资源持久变化可追踪，合法地图边连贯 |

夹具可通过原生debug/API建立局部room和明确的受伤前置，所有setup记录为api-fixture；不将其拼接成自然路径，不改角色／游戏数值。B02/B06采用整数治疗值避开未验证的舍入假设；实际fixture必须核实没有额外治疗修正。B03不要求升级牌首手出现，也不强行摆牌。

## 房间、数据和合法离开的门

- root动作绑定当前run/act/map generation/room实例、请求序号与合法候选；普通选项和离开动作明确不同。两种同名选项或相同index跨阶段不可共用票据。
- Enter／选择开始／原生成功返回／效果／可离开／Exit成功按实际顺序关联；原生调用成功与room finished分别记录。room complete凭据消费一次，跨房间不得复用。
- 新snapshot明确必需字段与类型、缺失拒绝；静止确认后的观察绑定完整state，仅列出真正允许的实验室标记变化。业务调用中间快照可合法变化，不把4A的Closed例外泛化到其它字段。
- 当前player-visible候选／价格／已知资源进入策略DTO；完整RNG、隐藏抽牌序、未来随机结果留oracle证据。采集不得重新生成候选或调用有副作用getter。
- 使用真实资源／牌组命令前后及任务完成记录建立来源。TestMode缺失原生history时记录为Lab observer事实，不能声称原生history已产生。
- 已知原生合法性与当前adapter支持能力分开记录；Unsupported不能从候选中静默消失，也不能视为合法房间完成。未知房间停止并保留可重放前缀。

## 自然路径、预算与收口

保留`LAB_V0111_NATIVE_DEV_01`，需要可达性调查时在运行前登记DEV_02／DEV_03、策略和预算，保留全部死亡／Unsupported结果。可用公开地图选择通向Rest的合法子边；未知节点仍由原生RollRoomTypeFor决定，不用oracle预知结果。默认自然片段最多12节点，第一幕Boss前停止；若无法达到B12，明确4B自然集成尚未通过，再在已授权范围诊断策略或节点依赖，不用夹具填补。

保留45秒单动作、每战1000动作、512MiB日志门；建议4B新profile采用每房间32次决策、整个片段5000根动作／10分钟墙钟，全部写入header。等待上限取45秒与剩余片段时间的较小值。新多节点profile的外层watchdog建议12分钟，留出收尾余量；旧N2／4A profile不因本建议改变。若实测需要调整，保留旧结果并新建配置，不能靠无限加预算掩盖挂起。超时仅结束该实验子进程，不能把Interrupted当Death或重派不确定动作。

完成后用同一最终产物跑B01–B12适用矩阵、当前N2完整矩阵、4A终态／负控与Core；逐项保留真实exit、失败前缀、来源／执行模式、输入与dirty源码身份。15个Mod输入及52个兼容源码保持hash核对；只有共享Core/host确实受影响时追加适用旧Mod回归，并分列旧产物身份。不得以删旧断言、减RNG字段或复用过期成功结果收口。

回归通过交独立验收后进入4C；4D完成两类非战斗节点的同run集成，5–6继续自然跨幕／完整run及预登记批测。无需用户再次批准。权限拒绝不绕过；若必须扩大宿主权限或关闭审计，停止该依赖并给精确接口方案，继续独立安全部分。

## 只读证据位置

- 当前项目：`Program.cs`、`NativeLifecycle.cs`、`NativeTerminalContract.cs`、`NativeChoices.cs`、`NativeObservation.cs`、`NativeTraceContract.cs`、`NativeScenario.cs`、`run-native-slice.ps1`（均在`tools/native-run/`）。
- 固定版本原生入口：`.build/review/n3-handoff/`中的RestSiteRoom、RestSiteSynchronizer、Heal/SmithRestSiteOption、NEventRoom；`.build/native-reference/`中的EventRoom、EventSynchronizer、RunManager。
- 本轮事件静态查看：`.build/runs/n3a-state-independent-review01/4b-static/`中的AbyssalBaths及候选排除证据。来自已核对SHA256的v0.111.0程序集；仅静态反编译，没有初始化游戏或执行新场景。

这些是本地只读研究材料，不发布反编译源码。4B内容仍为实施交接，尚无运行通过声明。本轮仅新增此文档和最终审查报告，保留所有dirty工作；不提交推送、不启动UI，不改角色／安装／真实存档，不调用真实Steam或游戏网络。
