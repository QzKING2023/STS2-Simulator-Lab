> 2026-11-04 最终状态：M1 在固定原版 Ironclad/A0 声明范围验收通过。最终构建自然 RunVictory464、两次同构建重放和六固定种子各自真实 Death/重放已核验；旧失败、独立 legacy 构建和未覆盖项保留。详见 [M1_FINAL_ACCEPTANCE.md](M1_FINAL_ACCEPTANCE.md)。以下按时间保留历史记录；其中未完成声明不是最新状态。

> 2026-11-04 M1 batch06：保持 Guarded 默认，统一显式终态策略；修复三处真实差分。六固定样本已有实际 Death，但仅 EVAL03–06 完成最终构建 live/replay（1,479 差分步、24,692 状态/RNG观察一致）；EVAL01/02 仍须最终构建补跑，M1 未通过，未提交推送。见[验证](M1_ACCEPTANCE_TERMINAL_VALIDATION.md)、[交接](M1_ACCEPTANCE_TERMINAL_NEXT_HANDOFF.md)。以下保留旧构建证据。

> 2026-11-04 M1 batch05: Reflections / CrystalSphere verified within declared scope; six fixed samples retain five policy stops and one budget stop. Two full-run regressions reach RunVictory. M1 acceptance remains pending; no commit/push. See [validation](M1_ACCEPTANCE_EVENT_COMPLETION_VALIDATION.md) and [next handoff](M1_ACCEPTANCE_EVENT_COMPLETION_NEXT_HANDOFF.md).

> 2026-11-04 M1 第四批：真实 Tainted 差分、药水选择、原生事件资源和继承 OnPlay 准入已修复；最终构建两次 499 动作 RunVictory、393 步差分保持。六固定样本仍有策略/预算停止及 Reflections 未支持，CrystalSphere 未验证，M1 未通过，未提交推送。见[验证](M1_ACCEPTANCE_TAINTED_RESOURCES_VALIDATION.md)和[交接](M1_ACCEPTANCE_TAINTED_RESOURCES_NEXT_HANDOFF.md)。

> 2026-11-04 M1第三批：Stratagem、先古真实交易、宝箱修正及三个事件已推进；最终构建两次499动作RunVictory重放/393步差分保持。六固定样本仍有真实差分、Unsupported与预算停止，M1未通过，未提交推送。见[本批验证](M1_ACCEPTANCE_LIFECYCLE_VALIDATION.md)与[后续交接](M1_ACCEPTANCE_LIFECYCLE_NEXT_HANDOFF.md)。

> 2026-11-04 M1 第二批验收：固定样本 01 已自然通过前两幕并推进至第三幕 630 确认动作；当前构建两份完整冻结重放均 RunVictory。六个固定样本仍有先古、阶段选择、宝箱和事件支持缺口，M1 未通过、不提交推送。见[验证与预算](M1_ACCEPTANCE_EVENT_INTENT_VALIDATION.md)及[后续交接](M1_ACCEPTANCE_EVENT_INTENT_NEXT_HANDOFF.md)。

> 2026-11-04：Juggernaut评分已收窄到实际第三形态；从a595合法冻结前缀进入在线尾部的3次执行均为Death610/Boss94HP，完整状态/RNG重复与重放一致。首个分歧为a597 True Grit耗尽Stampede而旧冻结选择为Defend，不能单独归因评分。3次额度已用完，BossVictory/RunVictory/M1未完成，待独审。见[本轮结果](../native-run/NATIVE_RUN_JUGGERNAUT_PHASE_VALIDATION.md)。上一轮v40[独立复审已通过](../native-run/NATIVE_RUN_JUGGERNAUT_POLICY_INDEPENDENT_REVIEW.md)，保留以下历史记录。

> 2026-11-03：Juggernaut窄排序v40候选已完成5次预登记原生执行，三个种子均真实Death；DEV_03为第三幕Death618/Boss119HP，DEV_01/02在第一幕死亡。详细重复与完整状态/RNG重放一致，未证实策略改善或RunVictory，M1未完成，待独立验收。见[本轮结果](../native-run/NATIVE_RUN_JUGGERNAUT_POLICY_VALIDATION.md)。

> 上轮R1/R2及a617对照已[独立验收通过](../native-run/NATIVE_RUN_JUGGERNAUT_INDEPENDENT_REVIEW.md)；其较好33HP结果包含固定前缀与局部干预，不能与本轮全程live候选作严格同策略对照。以下保留历史记录。

> 2026-11-03：分支选择/120秒停止门修复及a617对照已收口，待独审。6次原生执行、约3分7秒、1.30GiB；两张Defend真实触发格挡输出，最好Death624/Boss33HP。没有BossVictory或RunVictory，完整M1未完成。见[修复与对照报告](../native-run/NATIVE_RUN_JUGGERNAUT_REVIEW_REPAIRS.md)。

> 2026-11-03：有预算原生组合搜索已收口，13个候选＋2个完整尾部及2次冻结重放；最好真实Death624/Boss59HP，没有BossVictory或RunVictory，M1未完成。分支live非首项耗尽及精确前缀已验证，待独立验收。见[本轮报告](../native-run/NATIVE_RUN_BOUNDED_SEARCH_VALIDATION.md)。

> 2026-11-03：TestSubject 固定前缀策略实验已收口，16 个开发案例均真实 Death，当前 Death618；Boss 胜利和 M1 仍未完成。最终冻结 `187D8116…21D49` 的重放/失败传播定向验收见 [报告](../native-run/NATIVE_RUN_TESTSUBJECT_STRATEGY_VALIDATION.md)。待独立审查；完整 M1 同构建全量门保留。

> 最新本地进展（待独审）：正常开局同一 Run 已完成第三幕 MechaKnight 精英胜利、Orichalcum 原生领奖并到达 TestSubject Boss，真实 Death610。见[本轮验证](../native-run/NATIVE_RUN_ACT3_BOSS_PROGRESS_VALIDATION.md)。完整 M1、第三幕 Boss 胜利和 RunVictory 仍未完成。

> 最新检查器修复（2026-11-03）：第三幕商店独审 R1/R2 已修复并通过冻结原证据定向验收，待独立复审。仅一个生产检查器文件变化，未启动新原生流程；最远仍为 Death545，Boss/RunVictory/M1 未完成。见[修复与证据](../native-run/NATIVE_RUN_ACT3_SHOP_REVIEW_REPAIRS.md)。

> 最新实施（2026-11-03）：自然商店实际5次购买及离店、HungryForMushrooms/PotionCourier 原生任务、死亡后 maxHp setter 采集完成定向验证，待独审。原生生成冻结 `2EB24CD78644588B03618F313FF3842166AB850858CBEB09BE9C937A6E7F9F53`；仅检查器后续冻结 `F28EA015BBC3D32D3919E732209C11F7ABE7FD5CE8CE026BC50E4CEFFE127777`。自然最远第三幕第十二层 Death545，非完整通关；旧缺锚点数据仍未认证。见[本轮报告](../native-run/NATIVE_RUN_ACT3_SUPPLIES_VALIDATION.md)。

## 2026-11-03 死亡主体身份修复，待独审

冻结 `5E73CF13D99435E7559ECB49B50CB20B4D8B554D21FD9796020626A62730A7B2`：每场战斗原生记录实际玩家Creature，v2死亡链绑定该实例和当前房间；旧v1缺玩家锚点的死亡证据明确未认证。9个定向原生执行和分层证据重验见[身份修复报告](../native-run/NATIVE_RUN_DEATH_OWNER_REPAIRS.md)。完整M1/RunVictory及最终同构建全量门仍待完成。以下保留历史记录。

## 2026-11-03 自伤死亡生命周期修复，待独审

冻结 `A5126450FBE068F1239FAE3639CF193759ADF3460AFCE9E02CFD3503E7F4ABAB`：旧448步原生自伤路线在三种观测模式重放并通过；记录真实能力移除和 Start→AutoPostPlay→End，保留完整状态/RNG。敌方Death474及重放、skip/take、异常和预算前缀回归通过。43个新生产控制与历史证据重验见 [修复报告](../native-run/NATIVE_RUN_SELF_DEATH_REPAIRS.md)。本条不关闭第三幕Boss、RunVictory或M1最终全量门。

> 2026-11-03 最新第三幕进度：自然三战胜利/奖励、FakeMerchant 不交易原生离开，随后第6层 Death474。封版 `59EB82B365107C244603DBE2F530EB3538F6441BBF656AF9EB01E32CCC4DE9FE`，待独审。Boss/RunVictory/M1未完成；另保留一项回合开始自伤死亡认证缺口。见 [本轮报告](../native-run/NATIVE_RUN_ACT3_PROGRESS_VALIDATION.md) 与 [交接](../native-run/NATIVE_RUN_ACT3_PROGRESS_NEXT_HANDOFF.md)。

# 最新：第三幕自然入口与连续节点（2026-11-03）

同一正常开局进入实际第三幕Tanx，首战切片387动作真实胜利/奖励。连续路线原生取得ThrowingAxe、完成两战胜利/奖励，在第4层Fabricator真实Death418。分层验证待独立审查，不能记作RunVictory。第四步仍有自然房间组合缺口，第五步已进第三幕但未完成，必要数据与最终同构建全量门未完成。

冻结 `2EE02774AE02C77D25AA310382F08C5930580987D78F4DB7D70649A01A664FD3`；15个最终原生进程、594份验收集合读取均符合预期。15输入52兼容保持。详见[本轮验证](../native-run/NATIVE_RUN_ACT3_VALIDATION.md)与[后续交接](../native-run/NATIVE_RUN_ACT3_NEXT_HANDOFF.md)。以下历史按各自冻结与范围保留。

# 最新：第二幕胜利及再次跨幕（2026-11-03）

DEV03同run原生第二幕Boss胜利/奖励/再次跨幕，367动作到第三幕MapRoom；独立精英路线318动作胜利与MealTicket奖励。15次同最终构建原生执行、532次生产读取符合预期；待独审。完整M1与最终同构建全量门仍未完成。详见[验证](../native-run/NATIVE_RUN_ACT2_VICTORY_VALIDATION.md)及[下一交接](../native-run/NATIVE_RUN_ACT3_IMPLEMENTATION_HANDOFF.md)。

以下保留历史阶段记录，旧第二幕死亡及未验证声明以各自冻结范围解释。

## 当前状态：第二幕Boss首达与重复宝箱，本轮待独审（2026-11-03）

DEV03正常开局同run完成16场胜利后在第二幕KAISER_CRAB_BOSS死亡360动作；独立自然精英路线在INFESTED_PRISMS_ELITE死亡306动作。DEV01/02继续保留93/156动作Death。三个自然宝箱（第二幕两个）均由原生转换退出，双宝箱API夹具take/skip及原版准备精英/Boss胜利回归通过。17次原生执行生产冻结`DEE290B603413137770A9C52ED4B905E2ED40701843D42FDD1655A18CF1BE957`；最终检查器`908DE0CD7A852365FC40B55CE691C7AF36D0BF8A95742D024C056CABBC59328F`仅补有限历史描述读取。371份原始记录、70历史控制、24新生产控制符合预期。完整M1未完成，第二幕胜利与再次跨幕仍待实证。

见[当前验证](../native-run/NATIVE_RUN_ACT2_LIFECYCLE_VALIDATION.md)、[后续交接](../native-run/NATIVE_RUN_ACT2_LIFECYCLE_NEXT_HANDOFF.md)。以下历史保留。

## 历史：第二幕第6层真实死亡（2026-11-03，后续已独审通过）

DEV03正常开局同run继续完成第二幕4场战斗及奖励，277动作后在THE_OBSCURA_NORMAL真实死亡，HP0/Gold403；DEV01/02保留93/156动作Death。11次原生执行及完整重放/观察对照、346份原始证据重验、18新控制和24旧独审控制符合预期。原生生产冻结`6FF6F3528D673DEC235644E08AC7FA35F91E7BD20302CA937263CBC791182994`；最新检查器冻结`EE0A62AF261D0F9A9786338F130BBCC98ED293F0E434DAFFE16D16DE41C3490F`仅在读侧修复后变化。断电恢复仅补未启动的24控制。

详见[本轮分层验证](../native-run/NATIVE_RUN_ACT2_VALIDATION.md)及[下一交接](../native-run/NATIVE_RUN_ACT2_NEXT_HANDOFF.md)。第二幕Boss、第二次跨幕、重复自然宝箱和完整RunVictory尚未覆盖，第四步及M1均未宣布完成。15输入/52兼容源码保持，无提交推送。

以下保留此前各轮记录；其“当前/待审”措辞是当时状态，以本节和最新报告为准。

## 当前状态：自然流程至第二幕首战（2026-11-03）

正常开局、第一幕 THE_KIN_BOSS Victory/原生奖励、真实跨幕、Tezcatara 营养汤及第二幕 BOWLBUGS_WEAK Victory/奖励，共206确认动作，act1/floor2、HP75/Gold361。冻结 `02479D358FE8B4311A0B164D02A62BE8546C3DD9BDEF86A07B399A427311FD42` 的分层验证通过，待独审；完整M1继续进行，最终全量门保留。

[本轮验证](../native-run/NATIVE_RUN_MULTIACT_LAYERED_VALIDATION.md) · [可信数据边界](../architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md) · [下一交接](../native-run/NATIVE_RUN_MULTIACT_NEXT_HANDOFF.md)。旧API夹具不冒充本轮自然路径，历史失败和15输入/52兼容源码保留。

以下为历史轮次原文，其中“最新”“待独审”“未验证”等均指各轮当时状态，不替代上述当前状态。

> 最新实施状态：4D3 胜利条件动作归属修复已完成同构建回归，待独立复审；M1 仍在进行。见[修复报告](../native-run/NATIVE_RUN_N3D3_VICTORY_OWNER_REPAIRS.md)。

最新本地进展：4D3准备路径R1–R3修复及最终统一回归完成，等待独立复审；见[修复证据](../native-run/NATIVE_RUN_N3D3_PREPARATION_REVIEW_REPAIRS.md)。冻结`B1FB0F818977EF57C6CF77014F33FC2695D4C56261C3685BF28F50D453917DDD`；当前为有限API夹具，M1仍进行中。

## 4D3 合法准备与跨幕（本地验证，待独审）

固定精英113/113、固定Boss至下一幕184/184的原生动作、奖励及幕转换已完成；来源为合法地图准备后明确目标API夹具。当前策略另外两个开发种子及自然精英样本真实Death保留，不声明自然完整通关。详见 [准备验证](../native-run/NATIVE_RUN_N3D3_PREPARATION_VALIDATION.md) 与 [下一交接](../native-run/NATIVE_RUN_N3D3_PREPARATION_NEXT_HANDOFF.md)。最终冻结 FAEC900A695EEDA64586342FB70DF2F118F5B4A941296BA34BF714B064CB423C，证据 `.build/runs/n3d3-preparation-work01/closeout.json`。旧证据和15输入/52兼容源码保留，下一幕首节点与完整M1仍待完成。

## 4D3 跨房历史上下文绑定完成，待独立复审（2026-11-03）


冻结`53186436B11E78EED9FFFAF8214EF68A960D28B513EE04151BFD70557E392188`。Travel/MajorFixture历史幕和节点绑定实际决定，完整新房及战斗身份在确认时与本次到达闭合，保留原生资源/RNG变化。8个误接受副本先复现，再全部语义拒绝；原29反例及82阶段负控保持。最终240次reader、47项原生业务见证、309原生执行/3908套件检查+Core98及732来源核对符合预期。15输入52兼容源码固定。

精英/Boss Victory、目标奖励、act-ready及完整M1仍未验证。见[本轮修复](../native-run/NATIVE_RUN_N3D3_TRANSITION_REPAIRS.md)、[前轮独审](../native-run/NATIVE_RUN_N3D3_ACTION_PHASE_REVIEW.md)和[后续交接](../native-run/NATIVE_RUN_N3D3_NEXT_HANDOFF.md)。无UI、真实Steam网络、用户存档、Mod专项或提交推送；无环境阻塞。下一步为独立复审后接续既定4D3与M1步骤5–6。

## 4D3 动作类型与生命周期阶段修复完成，待独立复审（2026-11-03）

冻结 `17C6C990BAD45E733D0210C373BAE162BE319B709E4FE391700808C3CF922247`。先验证有限动作集合和真实阶段，再选择位置规则；初始EnterAct不得在战斗中复用。六个联动篡改反例先复现误接受，新构建全部语义拒绝；原23反例保持拒绝。369次生产reader调用+5位置见证、309原生执行/3908套件检查+Core98及730来源核对符合预期。15输入52兼容源码保持，原始错误/Unsupported/预算/导出失败和确认前缀不变。

精英/Boss Victory、目标奖励、实际act-ready与完整M1仍待推进。见[本轮修复](../native-run/NATIVE_RUN_N3D3_ACTION_PHASE_REPAIRS.md)、[原独审](../native-run/NATIVE_RUN_N3D3_CONTEXT_REVIEW.md)及[后续交接](../native-run/NATIVE_RUN_N3D3_NEXT_HANDOFF.md)。无UI、真实Steam网络、用户存档、Mod专项或提交推送，无环境阻塞。必须先独立复审，再继续既定4D3及步骤5–6。

## 4D3 战斗act/node上下文修复完成，待独立复审（2026-11-03）

冻结 `98EE6B390F01A94AFB407F2D1C6576F59AE09A9D853EE1A2161CE5F5EA526FFC`。将战斗动作内所有带状态观察、死亡输入/完成及确认绑定派发时不可变act/node/完整房间引用，保留Travel/MajorFixture真实跨房变化。4个独审反例先复现误接受，新构建全部语义拒绝；原19反例保持拒绝。最终239次追加reader调用与5位置见证符合预期，十组原生309执行/3908检查+实跑Core98、722来源核对通过。15输入52兼容源码不变。

实际Death、Unsupported、故障、预算及DataError前缀和完整RNG重放保持；13死亡阶段核验、3513条固定战斗位置及155条合法跨房历史均成立。精英/Boss Victory、目标奖励、act-ready与完整M1仍待推进。见[上下文修复](../native-run/NATIVE_RUN_N3D3_CONTEXT_REPAIRS.md)、[前轮独审](../native-run/NATIVE_RUN_N3D3_REPAIR_REVIEW.md)和[后续交接](../native-run/NATIVE_RUN_N3D3_NEXT_HANDOFF.md)。本轮无UI、真实Steam网络、用户存档、Mod专项或提交推送，无外部阻塞。

## 4D3 三项独审修复完成，待独立复审（2026-11-03）

冻结 `004B16F2E83E94C9CD546E0A4082B412259209CE53849C01548E01AC35AA8ECC`。修复死亡完整状态/终态事实、历史root/context归属、夹具声明到实际Encounter/房间的关联。19个原误接受先复现，修后全部拒绝；最终55系统性控制及79历史事件包均符合预期。统一309原生/3908套件检查+实跑Core98，604来源核对通过，15输入52兼容源码固定。只改两个生产检查器及新增一个reader控制脚本；候选错误和原证据均保留。

精英/Boss夹具Death51/51、自然精英Death93/93、Unsupported35/35与99/100、故障3/4、预算4/4及导出DataError51/51保持；13实际死亡阶段完整状态核验通过。Victory、目标奖励和实际act-ready仍未验证，整个4D3/M1未完成。见[修复报告](../native-run/NATIVE_RUN_N3D3_REVIEW_REPAIRS.md)、[原独审](../native-run/NATIVE_RUN_N3D3_REVIEW.md)及[后续交接](../native-run/NATIVE_RUN_N3D3_NEXT_HANDOFF.md)。待独审后按已有授权继续，无外部阻塞，无UI/真实Steam网络/用户存档/Mod专项/提交推送。

## 4D3 首批精英/Boss驱动与终态证据，待独立审查（2026-11-03）

冻结 `963B389CA6C6E6B353000ED0CD67CD6B3F60EC397FA94135985FCE0CF601F20C`；完整同构建309原生/3908套件检查及实跑Core98符合预期，603来源核对通过，15输入52兼容源码保持。新组19次执行/261检查/95生产控制（93拒绝、2等价），原4D2独审79包按预期重读；原始退出226×0/70×1/8×2/5×3，不是全胜。

实际精英夹具Death51/51、Boss夹具Death51/51、DEV01正常地图精英Death93/93，各有live/replay/minimal/off；DEV02商店Unsupported35/35与DEV03Hollow Unsupported99/100均保留并重放。另有实际首卡后包装故障3/4、一个真实战斗动作后的预算4/4，以及死亡后便捷导出IO控制DataError51/51。第二版当前敌意策略标为Oracle。

先前冻结自查5个误接受反例及全部历史失败保持，最终以生产修复门及新冻结完整复跑收口。新片段规范JSONL持续追加，便捷events导出结束时一次写入，完整状态/RNG及路线对照保持。精英/Boss Victory、相应奖励、实际act-ready/自然跨幕与完整M1步骤5–6仍未完成；不能用死亡或夹具代替。

报告：[4D3首批验证](../native-run/NATIVE_RUN_N3D3_VALIDATION.md)；[生命周期契约](../native-run/NATIVE_RUN_N3D3_LIFECYCLE_CONTRACT.md)；[后续交接](../native-run/NATIVE_RUN_N3D3_NEXT_HANDOFF.md)。4D2最终独审已通过，见[N3D2状态绑定复审](../native-run/NATIVE_RUN_N3D2_STATE_BINDING_REVIEW.md)。当前无权限/环境阻塞，不改角色/安装/用户存档，无UI/真实Steam网络/Mod专项/提交推送。

## 4D2 剩余完整状态绑定修复完成，待独立复审（2026-11-03）

冻结 `5B062F0D8E5AF9C6CB6498C5F472EA626C000E83F1A970B568CB88A8EE2B0758`。修复未完成Resume外层返回、空EventRewards与Resume奖励集合入口，保留原生RNG变化。修复前9反例复现；最终85历史gate、213事件控制（203拒绝/10合法等价）通过。统一290原生/3647套件检查+实跑Core98，1026历史reader、537来源核对通过；自然80/80、夹具62/62及故障/预算60/61保持，15输入52兼容不变。见[修复报告](../native-run/NATIVE_RUN_N3D2_STATE_BINDING_REPAIRS.md)和[上轮独审](../native-run/NATIVE_RUN_N3D2_REPAIR_REVIEW.md)。待独审后接4D3及步骤5–6，完整M1未完成，无提交推送。

## 4D2 四类审查修复完成、待独立复审（2026-11-03）

冻结 `C4B85E2FDDADA7418E49A1A73A6F91256195707415128D3038461B08CF1A563C`。修复真实 Resume 与奖励子任务时序、选择/根动作归属、清理/到达记录及完整状态、EventRewards 必需集合与返回状态；原16反例由生产语义门全部拒绝，10正常/等价副本保留。最终统一290原生进程/3586套件检查+实跑Core98；事件套件10/243，152生产控制（147拒绝、5接受），1026历史副本重读及537来源核对通过。15输入52兼容源码不变，三处源码修改。详见[修复报告](../native-run/NATIVE_RUN_N3D2_REVIEW_REPAIRS.md)；旧原始证据保留，待独审后接[精英/Boss与步骤5](../native-run/NATIVE_RUN_N3D3_IMPLEMENTATION_HANDOFF.md)。完整M1未关闭，无UI、真实用户存档或提交推送操作。

## 4D2 实施与同构建验证完成、待独立验收（2026-11-03）

冻结 `12F0036561A3AF6AA801BB2D894EE5B3FFBED04D4246E8DECC1DFB54CD545004`。原DEV_03前77动作完整保持，Hollow原生领奖后到下一战Play共80/80；明确的事件战斗API夹具实际击杀、奖励、真实Resume、合法行进至下战Play共62/62。统一290原生进程/3540套件检查+Core98、106新增生产控制及跨场景负控、1026历史实际重读、552来源+40补充核对均通过，15输入52兼容源码固定。详见[验证报告](../native-run/NATIVE_RUN_N3D2_VALIDATION.md)和[生命周期契约](../native-run/NATIVE_RUN_N3D2_LIFECYCLE_CONTRACT.md)。异常与预算60/61前缀保留，所有原始失败不覆盖。待独审后接[精英/Boss与步骤5](../native-run/NATIVE_RUN_N3D3_IMPLEMENTATION_HANDOFF.md)，完整M1及自然跨幕未完成，无提交推送。

## 奖励闭环三项审查修复完成、待独立复验（2026-11-03）

冻结 `AFB0E42BE7173FFDF371CE7E0DDBEC605430151608F171177F9436C12C289A52`。修复 Sozu 取得事实、父子静止完整状态、新旧实例/类型/升级绑定；原13误接受均由生产语义门拒绝。统一280原生进程/3343套件检查+Core98，新35/419/171控制，993历史副本实际重读、530来源核对通过；15输入52兼容源码固定。见[修复报告](../native-run/NATIVE_RUN_REWARD_CLOSURE_REVIEW_REPAIRS.md)与[分支契约](../native-run/NATIVE_RUN_REWARD_CLOSURE_REPAIR_CONTRACT.md)。旧失败及原证据保留，待独立复验后接事件战斗/Boss及步骤5–6；完整M1未关闭，无提交推送。

## 原生奖励语义闭环实现完成、待独立验收（2026-11-03）

冻结 `FE913EB8BEFAB02D2FAF45BA7FC658A19EBCABEB946E8202EDF7BBD2F769FAFF`。五项闭环为原生reroll、FresnelLens候选与真实Add替换、Sozu拒收、自然Neow两次取卡与下一战、SpoilsMap任务宝箱账本。统一280原生进程/3304套件检查+Core98，新35进程/380检查/132控制，830历史副本重读和530来源核对通过；15输入52兼容源码固定。参见[验证报告](../native-run/NATIVE_RUN_REWARD_CLOSURE_VALIDATION.md)及[后续交接](../native-run/NATIVE_RUN_REWARD_CLOSURE_NEXT_HANDOFF.md)。保留真实失败与Unsupported，宝箱跨幕是显式API夹具，未宣布完整M1或整局胜率；无提交推送。

4D1C R1/R2修复及同构建回归已通过，待独立验收；冻结 `1348A7FAD6C9BDA35E91EC469911FD57CC1800BB8B76CD3B19B30F9872D3163B`。245原生进程/2924检查+Core98、368交易控制、15补充语义副本、830历史重读。详见 [修复报告](../native-run/NATIVE_RUN_N3D1C_REVIEW_REPAIRS.md)。M1继续推进，旧记录保留。

# Milestone status

## 4D1C 实现与同构建回归完成，待独立验收（2026-11-02）

冻结 `37375A30D91CA842BB756301A94F344335F75498B4DE759A3BFBBAC2D4A297BD`：245 原生进程/2889 检查/Core98、333 新生产控制、830 历史重读符合预期。实际遗物取得、MawBank Hook、Courier 两次药水补货与自然 WingCharm 购入后下一战出牌已验证；预登记另两自然样本 Unsupported 保留。15 输入/52 兼容源码及历史证据固定。详见 [4D1C 验证](../native-run/NATIVE_RUN_N3D1C_VALIDATION.md) 和 [后续交接](../native-run/NATIVE_RUN_N3D1C_NEXT_HANDOFF.md)。完整 M1 继续推进。

## 4D1B 最终独立验收通过（2026-11-02）

冻结 `3B0C3069A9199A13E6DC202961E9A7D233FB39B2AEF1EA53357624F2A502F3D0` 已通过房间归属/阶段顺序复验：230 次生产 reader（165 控制、23 原生证据包、原 9+3 反例、30 独立副本）全部符合预期。复核 227 原生进程/2453 套件检查/Core98、600 历史重读记录及 2622+993 文件保持；本轮未重跑这些原生场景。自然 72/41/83 动作和完整状态/选择/RNG 重放保持。15 输入、52 兼容源码不变。详见 [最终审查](../native-run/NATIVE_RUN_N3D1B_FINAL_REVIEW.md)；[4D1C](../native-run/NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md) 可继续，完整 M1 尚未完成。

## Current mainline: native offline M1

The new mainline prioritizes the vanilla native offline run and trustworthy data. Steps 1–3 and 4A–4C have passed independent acceptance. The final 4C review verifies the 17-operation contract, static parent/child state boundaries and actual acquired-card identity: 502 production reader executions match expectations, including all 292 controls, 115 historical packets and fresh reproductions of all 21 prior counterexamples. The same-build native regression remains 179 processes / 1721 suite checks plus Core98. Real collection/nested choices, treasure/relic acquisition, potion use, the normal-start 24-action slice, replay/full RNG and failure prefixes remain. See [final 4C acceptance](../native-run/NATIVE_RUN_N3C_FINAL_REVIEW.md), [latest repairs](../native-run/NATIVE_RUN_N3C_BOUNDARY_REPAIRS.md), the [lifecycle contract](../native-run/NATIVE_RUN_N3C_LIFECYCLE_CONTRACT.md), and the ready [4D handoff](../native-run/NATIVE_RUN_N3D_IMPLEMENTATION_HANDOFF.md).

4D and steps 5–6 remain; this is not yet the complete vanilla run milestone. Public Mod interfaces remain the later M2. The earlier Mod/Stage1 evidence below is preserved as historical foundation, without expanding that workstream.

The bounded 4D1 merchant repairs have passed independent acceptance: required removal choices bind actual selected/removed instances, and completed-effects/SetUsed/Hook/return states bind in full. The independent review executed the production reader 558 times with zero expectation mismatches, including all 253 merchant controls, 231 prior packets, 25 current result packets and 49 independently generated copies. Same-build native evidence remains 204 processes / 2092 suite checks plus Core98; this review did not rerun those game processes. See [final 4D1 review](../native-run/NATIVE_RUN_N3D1_FINAL_REVIEW.md), [repairs](../native-run/NATIVE_RUN_N3D1_REVIEW_REPAIRS.md), and the [lifecycle contract](../native-run/NATIVE_RUN_N3D1_LIFECYCLE_CONTRACT.md). Continue directly with [merchant price/RNG/history semantics and natural travel](../native-run/NATIVE_RUN_N3D1B_IMPLEMENTATION_HANDOFF.md), followed by the [remaining 4D work](../native-run/NATIVE_RUN_N3D1_NEXT_HANDOFF.md). This remains API-fixture merchant coverage; the natural merchant route and complete vanilla run are still pending.

The remaining 4D1B phase/ownership repairs now pass the final frozen regression and await independent review: 227 complete native processes /2453 suite checks plus Core98, 165 production controls and 600 historical reader outcomes. Generation follows the same Travel's completed native stats; its MerchantRoom is bound through arrival, Attach, inventory projection and exit. All three new misacceptances and the original nine are rejected. Natural72/41/83, complete RNG/replay and failure prefixes remain; 2622 historical files plus 993 prior-repair/review files, 15 pinned inputs and 52 compatibility sources are preserved. The three current-DLL actual weaving probes cover six audit API rejections only. See [phase/ownership repairs](../native-run/NATIVE_RUN_N3D1B_OWNER_PHASE_REPAIRS.md), [preceding review](../native-run/NATIVE_RUN_N3D1B_REPAIR_REVIEW.md) and [4D1C handoff](../native-run/NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md). Full M1 remains pending.

## Reward-card compatibility: review repairs and replacement regression

ReturnFlow, ManaConversionPrinciple and generated ManaEssence have strict source-bound effects and ordered selection replay. Independent review confirmed the earlier natural-path evidence but rejected final acceptance because action fields could be ignored and direct identity/history evidence was incomplete. Those two repairs now pass the replacement same-artifact regression: 36 tasks, 237 artifact checks, 90 native fixture processes and 45 repeat comparisons; the unchanged cohort and frozen replay each retain 12 actual second Victories. Independent acceptance re-review is pending. See [REWARD_CARD_REVIEW_REPAIRS.md](../validation/REWARD_CARD_REVIEW_REPAIRS.md) for the new frozen identity and direct evidence. The prior 35-task/135-check candidate remains historical evidence in [REWARD_CARD_COMPATIBILITY_VALIDATION.md](../validation/REWARD_CARD_COMPATIBILITY_VALIDATION.md).

The older 8 Victory / 4 unsupported results below remain historical evidence. The repair is uncommitted and has not been pushed; Stage 1 itself was recorded in local commit `d73039d688eb54c9513f5b9508a03947e689aa18`.

## Stage 1: terminal outcomes and failure evidence

The first implementation candidate completed 33 jobs / 93 evidence assertions, then independent review found three result-gate/progress defects. It was not accepted. Those fixes now pass a fresh frozen regression: 34 jobs / 132 artifact assertions, Core 81 checks, 25 failure controls, online/frozen/fault 27 checks, original 16-action/122-check and two-battle/41-check baselines. Seed cohorts retain 8 Victory / 4 unsupported and exit 2. See [STAGE_1_REVIEW_REPAIRS.md](../validation/STAGE_1_REVIEW_REPAIRS.md) for exact identity and scope; independent re-review passed on 2026-11-02, closing Stage 1 within its documented scope.

Native loss does not invoke AfterCombatEnd: role resources/subscriptions remain in this pinned TestMode path. This is recorded behavior, not a forced cleanup or full retail run-loss validation. See [STAGE_1_VALIDATION.md](../validation/STAGE_1_VALIDATION.md) for hashes, the complete acceptance matrix, commands and limits. The accepted Stage 1 changes were recorded in local commit `d73039d688eb54c9513f5b9508a03947e689aa18`; this milestone has not been pushed.

Validated game: **v0.111.0 / 41cef1ea**, main assembly hash **222455745**.
Eleanor input: **v0.4.1-forms-cardpool.fix**, DLL SHA-256 **949320F651298D34399335EB28176578D270139DFF2CCE60FB7FDF35E7DBAD42**.

## Passed bounded coverage

| Area | Evidence scope |
| --- | --- |
| Vanilla M1 | Fixed native/simulator combat through Victory; two-target random selection and RNG replay |
| Earlier Eleanor baseline | Four named effects, Complete Orb Mentor draw/upkeep, mana-to-block and repeated two-turn Victory; locked to v0.3.0-m3.burst23 |
| Eleanor v0.4.1 combat | Guide's two native choices, conversion/OffensiveDefense, Record/Echo API, temporary Recording expiry, Reverse upkeep success/failure and owned attributes; separate 16-action five-turn native Victory |
| Eleanor minimum run loop | Actual generated-map start, nine native actions through Victory, native reward offer/selection, legal child entry, next combat's first actionable exact-ready root |
| Cross-combat API fixture | Real Recording powers and blocked incoming damage produce nonzero Echo; native combat teardown clears combat resources before next startup |

The preserved 16-action combat baseline passed **122 independent artifact checks**. The minimum-loop suite passed **five independent processes and 24 checks**, with final exit 0. Vanilla/earlier Eleanor regressions also exited 0. Evidence is generated locally and is not distributed with this source repository.

## Native reward policies

Both policies collect the native 20 Gold reward, increasing Gold 99 → 119 while retaining HP 70 and the starting relic.

- **Card-skip:** skip the remaining Card/Potion rewards; permanent deck remains 11 cards.
- **Card-take:** select offered card index 0, ManaShield; skip the remaining Potion reward; native deck addition increases the permanent deck to 12.

The original options are ManaShield, CarefulDeduction and WasteToTreasure. The acquired card is cloned through the game's DeckVersion link into the next combat draw pile; it is not placed into the opening hand by the fixture. The route starts at generated coordinate 3,0 / ActFloor 1 and enters legal Monster child 0,1 / ActFloor 2.

Every combat action retains native/simulator roots, full raw continuation text and parent-fork isolation checks. The first passed native plan is frozen and reused across the repeated policies. The nine-action plan SHA-256 for the recorded milestone is **4F01FC79E9559C6A7FA94A3778C11099A532E75C8674139B853ECA5C6064558C**.

The separate power API case starts with Recording 1 and TemporaryRecording 2. Actual 1 Block and 1 incoming damage produce Echo 3 without HP loss. Echo reaches 15 before Victory; the temporary layer expires at the next player start while permanent Recording persists within the fight. Native teardown clears powers, Block, Mana, form and Echo. The next Cracked Orb combat opens at Mana 3, Neutral and Echo 0.

## Result interpretation

Native reward effects and map travel execute through game APIs. RunProgressRules validates phase, offered choices and legal map edges; it does not independently predict reward effects or reward RNG. Separate native processes implement policy branches; the Lab does not clone the entire RunManager.

The published minimum-loop snapshot stops at the next battle's first actionable state. The subsequent local two-battle milestone adds ManaShield's strict OnPlay mirror and actual play after natural drawing; see the extension below.

The supported bootstrap is a limited offline test profile. It uses genuine manifest/assembly ownership and original BaseLib content-discovery patches, but does not establish full BaseLib/Ritsu startup, PCK loading or retail collection rendering. A short user-reported retail smoke is separate from automated coverage. Earlier v0.4.0 retail integration failure remains tied to that older binary.

## Remaining limits

Complete Orb Reverse upkeep, exact overkill cases, more card effects, full retail callbacks and multiplayer/full loader compatibility remain unverified. No complete role, full-climb, optimal route or balance conclusion is claimed. The six planned Echo cards are absent from the pinned DLL.

Reproduction and output layout are documented in [VALIDATION.md](../validation/VALIDATION.md).

## Unpublished second-battle extension

The local milestone adds strict ManaShield OnPlay adaptation and actual acquired-card play after natural drawing, then drives skip/take through the second Victory. The five-process cohort passed 41 artifact/repeat checks. This work is separate from the published minimum-loop commit and has not been pushed. Scope, fixture policy, evidence and regressions are recorded in [TWO_BATTLE_VALIDATION.md](../validation/TWO_BATTLE_VALIDATION.md).

## Local bounded validation stages

- ManaShield base/upgraded × Mana 0/1/2, each twice: 12 native/simulator executions and six full-repeat comparisons passed. Insufficient mana still produces base Block 5; successful payment gives Block 10 / upgraded 13. All spend 1 energy, and parent forks remain unchanged.
- Three declared seeds × card-skip/take × twice: all 12 paths recorded, 8 actual two-battle Victories, 4 explicit unsupported, no observed Death or semantic failures, 30 checks passed. ReturnFlow and ManaConversionPrinciple acquisition work natively, but their acquired-card coverage policies are unsupported; they are not replaced with ManaShield.
- A retained-plan regression actually plays generated Slimed twice after fixing independent generated-card identity and action resolution. Exact roots, ordered piles, raw continuations and audit checks remain required.
- Bounded online decisions: two independent nine-query/nine-action executions and a zero-query frozen replay reach actual second Victory; 15 checks passed. The online scope is the original seed's card-skip second combat, with fresh source validation and exact action verification before replanning.

The batch driver exits **2** for unsupported paths, rather than reporting an all-pass result. These fixed policy fixtures do not establish a full-run win rate. Implementation, declarations, output directories and reproduction are in [EXTENDED_VALIDATION.md](../validation/EXTENDED_VALIDATION.md). This turn creates no commit or push.
