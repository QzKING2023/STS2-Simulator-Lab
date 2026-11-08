> 2026-11-04 最终状态：M1 在固定原版 Ironclad/A0 声明范围验收通过。最终构建自然 RunVictory464、两次同构建重放和六固定种子各自真实 Death/重放已核验；旧失败、独立 legacy 构建和未覆盖项保留。详见 [M1_FINAL_ACCEPTANCE.md](../milestones/M1_FINAL_ACCEPTANCE.md)。以下按时间保留历史记录；其中未完成声明不是最新状态。

> 2026-11-04 M1 batch06：保持 Guarded 默认，统一显式终态策略；修复三处真实差分。六固定样本已有实际 Death，但仅 EVAL03–06 完成最终构建 live/replay（1,479 差分步、24,692 状态/RNG观察一致）；EVAL01/02 仍须最终构建补跑，M1 未通过，未提交推送。见[验证](../milestones/M1_ACCEPTANCE_TERMINAL_VALIDATION.md)、[交接](../milestones/M1_ACCEPTANCE_TERMINAL_NEXT_HANDOFF.md)。以下保留旧构建证据。

> 2026-11-04 M1 batch05: Reflections / CrystalSphere verified within declared scope; six fixed samples retain five policy stops and one budget stop. Two full-run regressions reach RunVictory. M1 acceptance remains pending; no commit/push. See [validation](../milestones/M1_ACCEPTANCE_EVENT_COMPLETION_VALIDATION.md) and [next handoff](../milestones/M1_ACCEPTANCE_EVENT_COMPLETION_NEXT_HANDOFF.md).

> 2026-11-04 M1 第四批：真实 Tainted 差分、药水选择、原生事件资源和继承 OnPlay 准入已修复；最终构建两次 499 动作 RunVictory、393 步差分保持。六固定样本仍有策略/预算停止及 Reflections 未支持，CrystalSphere 未验证，M1 未通过，未提交推送。见[验证](../milestones/M1_ACCEPTANCE_TAINTED_RESOURCES_VALIDATION.md)和[交接](../milestones/M1_ACCEPTANCE_TAINTED_RESOURCES_NEXT_HANDOFF.md)。

> 2026-11-04 M1第三批：Stratagem、先古真实交易、宝箱修正及三个事件已推进；最终构建两次499动作RunVictory重放/393步差分保持。六固定样本仍有真实差分、Unsupported与预算停止，M1未通过，未提交推送。见[本批验证](../milestones/M1_ACCEPTANCE_LIFECYCLE_VALIDATION.md)与[后续交接](../milestones/M1_ACCEPTANCE_LIFECYCLE_NEXT_HANDOFF.md)。

> 2026-11-04 M1 第二批验收：固定样本 01 已自然通过前两幕并推进至第三幕 630 确认动作；当前构建两份完整冻结重放均 RunVictory。六个固定样本仍有先古、阶段选择、宝箱和事件支持缺口，M1 未通过、不提交推送。见[验证与预算](../milestones/M1_ACCEPTANCE_EVENT_INTENT_VALIDATION.md)及[后续交接](../milestones/M1_ACCEPTANCE_EVENT_INTENT_NEXT_HANDOFF.md)。

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

## 2026-11-03 玩家死亡观测锚点

[自伤死亡修复报告](../native-run/NATIVE_RUN_SELF_DEATH_REPAIRS.md) 的最终构建为 `A5126450FBE068F1239FAE3639CF193759ADF3460AFCE9E02CFD3503E7F4ABAB`。9个定向原生进程、2个非法重放控制、43个生产读取控制和2个实际Harmony审计API夹具分别记录；原448步失败原件未改。锚点观察原生同步方法，不替换效果；具体伤害触发来源未被原生history记录时明确未知。证据入口 `.build/runs/self-death-work01/closeout.json`，完整M1与最终同构建全量验收未完成。

> 2026-11-03 最新第三幕进度：自然三战胜利/奖励、FakeMerchant 不交易原生离开，随后第6层 Death474。封版 `59EB82B365107C244603DBE2F530EB3538F6441BBF656AF9EB01E32CCC4DE9FE`，待独审。Boss/RunVictory/M1未完成；另保留一项回合开始自伤死亡认证缺口。见 [本轮报告](../native-run/NATIVE_RUN_ACT3_PROGRESS_VALIDATION.md) 与 [交接](../native-run/NATIVE_RUN_ACT3_PROGRESS_NEXT_HANDOFF.md)。

# 最新来源：native-act3-final04（2026-11-03）

统一冻结 `2EE02774AE02C77D25AA310382F08C5930580987D78F4DB7D70649A01A664FD3`。自然来源均为正常Neow开局，持续同run、真实Tanx候选/取得/战斗/奖励/死亡；首战387与连续路径Death418分列，未声称通关。早期进程在第三幕提示图标加载处0xC0000005中断，367确认/368派发及原始退出完整保留。StunIntent展示方法适配及实际Harmony审计API证据另列，API业务调用0。

来源清单及94份实际dirty源码快照、原始进程、重放、模式对照、571+2份证据读取及21生产控制见 `.build/runs/native-act3-work01/`、`.build/runs/native-act3-final04/` 与[本轮报告](../native-run/NATIVE_RUN_ACT3_VALIDATION.md)。最终15原生是6Death+9SliceBoundary；594是验收集合读取，不是局数。15输入52兼容固定。当前必要数据仍有机械内部状态缺口，完整M1及最终全量门未完成。以下旧记录及失败不改写。

# 最新来源：第二幕胜利（2026-11-03）

源码/生产/检查器统一冻结`D59D778F3360FBBFF7B65A419725D321F20A7B541FBC2E91CDAF45FDCFC45910`，实际目录`.build/native-build/act2-strategy-dev04/`。15次最终原生执行，另3次开发执行分列；389份完整原始证据重读与122历史控制，另21新控制，合计532次生产检查器调用。两方法实际Harmony注入负控是独立API夹具，目标业务调用0，不能算战斗。15输入/52兼容源码保持。所有本轮来源见`.build/runs/act2-strategy-work01/closeout.json`和[验证报告](../native-run/NATIVE_RUN_ACT2_VICTORY_VALIDATION.md)。

以下为历史来源，保留原producer及失败，不改称本轮构建生成。

## 当前来源：自然Boss360动作Death与双宝箱（2026-11-03）

原生生产冻结`DEE290B603413137770A9C52ED4B905E2ED40701843D42FDD1655A18CF1BE957`有17次本轮执行；最终读侧`908DE0CD7A852365FC40B55CE691C7AF36D0BF8A95742D024C056CABBC59328F`仅补有限旧策略描述校验，未将旧producer重标。371份原始证据中282份完整/合法终局通过、89份原错误/未完成拒绝；另有70旧控制和24本轮生产控制。自然360动作同run有三宝箱、第二幕Boss原生敌方致死；自然306动作精英死亡另列；9动作双宝箱是API夹具。第一幕准备精英/Boss胜利不等于第二幕胜利。声音窄桥保留Rocket原回调，原生谓词和当前动作历史/完整快照绑定继续。15输入和52兼容源码不变。

详见[本轮报告](../native-run/NATIVE_RUN_ACT2_LIFECYCLE_VALIDATION.md)及`.build/runs/act2-lifecycle-work01/closeout.json`。下列原始证据保留。

## 历史来源：自然第二幕277动作Death（2026-11-03，已独审通过）

本轮原生生产冻结`6FF6F3528D673DEC235644E08AC7FA35F91E7BD20302CA937263CBC791182994`有11次实际执行；检查器冻结`EE0A62AF261D0F9A9786338F130BBCC98ED293F0E434DAFFE16D16DE41C3490F`仅修改NativeRewardContract，并对完整346份冻结原始证据、18新控制及24旧独审控制完成检查。不能把原始producerBuildId改成检查器版本，也不能将84份预期拒绝记成成功游戏路径。

DEV03的277动作来自正常Neow同run：第一幕Boss/奖励/跨幕，第二幕4胜/奖励，随后第6层真实Death。a216的SpecialCardReward经原生领取返还CARD.INFLAME原实例i448；无手动追加牌组。15输入/52兼容源码不变，dirty源码和原始失败保留。断电前346/18/11结果已完整落盘，恢复只补原先不存在的24控制目录。详见[验证与完整边界](../native-run/NATIVE_RUN_ACT2_VALIDATION.md)，本轮待独审，M1仍在进行。

以下保留此前各轮来源记录；其时点状态以本节和最新报告为准。

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

# Implementation provenance and isolation

## 4D1C 实现与同构建回归完成，待独立验收（2026-11-02）

冻结 `37375A30D91CA842BB756301A94F344335F75498B4DE759A3BFBBAC2D4A297BD`：245 原生进程/2889 检查/Core98、333 新生产控制、830 历史重读符合预期。实际遗物取得、MawBank Hook、Courier 两次药水补货与自然 WingCharm 购入后下一战出牌已验证；预登记另两自然样本 Unsupported 保留。15 输入/52 兼容源码及历史证据固定。详见 [4D1C 验证](../native-run/NATIVE_RUN_N3D1C_VALIDATION.md) 和 [后续交接](../native-run/NATIVE_RUN_N3D1C_NEXT_HANDOFF.md)。完整 M1 继续推进。

## 4D1B 最终独立验收通过（2026-11-02）

冻结 `3B0C3069A9199A13E6DC202961E9A7D233FB39B2AEF1EA53357624F2A502F3D0` 已通过房间归属/阶段顺序复验：230 次生产 reader（165 控制、23 原生证据包、原 9+3 反例、30 独立副本）全部符合预期。复核 227 原生进程/2453 套件检查/Core98、600 历史重读记录及 2622+993 文件保持；本轮未重跑这些原生场景。自然 72/41/83 动作和完整状态/选择/RNG 重放保持。15 输入、52 兼容源码不变。详见 [最终审查](../native-run/NATIVE_RUN_N3D1B_FINAL_REVIEW.md)；[4D1C](../native-run/NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md) 可继续，完整 M1 尚未完成。

The current mainline is the vanilla native offline run. Steps 1–3 and 4A–4C have independent acceptance; historical Mod evidence below is retained separately. The bounded 4D1 repairs also passed [final independent acceptance](../native-run/NATIVE_RUN_N3D1_FINAL_REVIEW.md): 558 production reader executions matched expectations, including freshly resealed semantic counterexamples, without rerunning native game scenarios or changing their evidence. The [repair report](../native-run/NATIVE_RUN_N3D1_REVIEW_REPAIRS.md) records the frozen identity and 204-process / 2092-check same-build regression. The reader binds removal choices to actual permanent instances and binds completed-effects/SetUsed/Hook/return snapshots in full. Native inventory generation, purchase wrappers and removal synchronization execute within explicit API fixtures. The fixed UI's removal business delegation (`SetUsed`) runs at the native OneOff task boundary before `AfterItemPurchased`; two mandatory observations verify that order. Only merchant asset preloading was newly suppressed by 4D1. TestMode potion-price jitter and merchant Exit history remain limitations; no natural merchant route or full run is claimed. See the [lifecycle contract](../native-run/NATIVE_RUN_N3D1_LIFECYCLE_CONTRACT.md), [next merchant semantics/travel handoff](../native-run/NATIVE_RUN_N3D1B_IMPLEMENTATION_HANDOFF.md), and [remaining 4D work](../native-run/NATIVE_RUN_N3D1_NEXT_HANDOFF.md).

The 4D1B profile restores only three pinned native TestMode business guards: potion CalcCost, MerchantRoom.Exit, and UpdatePlayerStatsInMapPointHistory. Original native algorithms/history execute; no Lab price or history effect is substituted. Whole-method token/MVID/IL identity, unique guard anchors and the host patch inventory remain recorded. Three natural cohorts complete normal-start merchant purchase/travel/next-battle Play; API fixtures remain separately labelled. Same-build regression is 227 processes / 2405 checks plus Core98, pending independent review. The interrupted final05 run retained its first19 complete native processes and header-only partial evidence; four unfinished cases were resumed without repeating those19. Recovery orchestration hashes are supplemental to the frozen native/test manifest. See [4D1B validation](../native-run/NATIVE_RUN_N3D1B_VALIDATION.md) for exact build identity, evidence and untested method-specific unknown-patch controls. The profile does not establish all-content retail/TestMode equivalence.

Independent 4D1B review preserved the native and recovery evidence but rejected final data acceptance: nine resealed malformed packets passed the production reader. Three required repairs and exact repros are recorded in [NATIVE_RUN_N3D1B_REVIEW.md](../native-run/NATIVE_RUN_N3D1B_REVIEW.md). The previously unrun method-specific unknown-patch gate now has three actual Harmony weaving API probes against the frozen DLL, covering pre-existing and late patches. They execute no target business methods and deliberately stop at the pre-start isolation invariant when the inventory is clean; they do not claim fully initialized game-host tests.

The subsequent [4D1B R1–R3 repairs](../native-run/NATIVE_RUN_N3D1B_REVIEW_REPAIRS.md) bind the full existing price sequence to native generation confirmation without adding new effects or runtime seams. Required Travel stats/completion/arrival phases and actual result coordinates are now checked. A new frozen 227-process / 2436-check regression plus Core98 passes; the 148 production controls, all nine original counterexamples and earlier historical packets match expectations. Method-specific actual weaving API probes were rerun on the final DLL. Prior evidence is preserved and independent re-review remains pending.

The [following phase/ownership repair](../native-run/NATIVE_RUN_N3D1B_OWNER_PHASE_REPAIRS.md) addresses two additional independent-review findings: old-node stats must precede inventory generation, and Attach/inventory must retain the actual generated and arrived merchant room. The final frozen 227-process /2453-check regression plus Core98, 165 controls and 600 historical packet rereads pass; independent acceptance remains pending. Three new counterexamples and the original nine are rejected. Real method-specific Harmony audit API probes were rerun against the final DLL; their no-business-execution scope is unchanged. API Setup's absent natural room-controller projection is explicit and does not establish natural arrival.

Stage 1 evidence and the immutable source/artifact manifest are documented in [STAGE_1_VALIDATION.md](STAGE_1_VALIDATION.md). Terminal capture now distinguishes the original Victory teardown prefix from a read-only `ProcessPendingLoss(CombatTurnState)` prefix. Native TestMode skips CreatureCmd's run-ended/history/metrics/game-over UI path; no loss method or role reset is suppressed. Combat Death is compared before that boundary; remaining state/subscriptions are observed separately after the actual CombatEnded event. The full retail loss lifecycle is outside this evidence.

The historical candidate is `31F713F7CF538FB9C0A076E24560EE1BDC0C5FF852EFBA872C91BC722BEE7D0A`; independent review rejected its result-gate/progress handling. The replacement frozen identity and repaired evidence are in [STAGE_1_REVIEW_REPAIRS.md](STAGE_1_REVIEW_REPAIRS.md). Wrong-manifest/artifact/input/source controls operate on metadata copies; exception/watchdog/mismatch and mid-query-fault controls remain explicitly labelled. The separate older-role compatibility build is not presented as the main binary. No raw local source snapshots, game inputs or execution artifacts belong in a public release.

## Source boundaries

The core protocol, comparison adapters and validation drivers are Lab code. Integration targets the MIT-licensed CombatSolver 0.50.1 reference, retained as a local dependency with its original license. Overlay scripts contain exact source anchors and Lab extensions; they fail on incompatible upstream layouts. See [THIRD_PARTY_NOTICES.md](../../THIRD_PARTY_NOTICES.md).

No game DLL, game implementation source, decompiled source, Mod binary, save file, research bundle or raw run artifact is included. Native API names and observed behavioral contracts do not grant rights to the external implementations.

## Combat and progression evidence

The subsequent [reward-card compatibility validation](REWARD_CARD_COMPATIBILITY_VALIDATION.md) adds three exact unpatched OnPlay adapters and a selection-only bridge. No new unaudited CardChoiceMirrors callback registry is used. Ordered tokens validate StateKey and physical occurrences; native choices are actual offered instances and the provider remains scoped through action completion. Pending opaque role effects use whole-action replay from the immutable parent. Principle uses a fixed simulator transformation while the actual locked native card calls CardScope/Transform normally; no reward/card addition is fabricated.

The new reward registration controls install real Harmony prefixes on each target and its async MoveNext, verify root-signature invalidation/dispatch rejection, then remove them before combat. These are actual weaving controls, separate from earlier synthetic relic metadata controls. The final artifact and same-artifact directed/natural/regression evidence are identified in that validation report; broader automatic-play listeners and arbitrary Mod scopes remain outside its claim.

The subsequent [reward review repairs](REWARD_CARD_REVIEW_REPAIRS.md) reject unsupported choice fields before native dispatch and add direct physical-instance A/B/A and new-versus-existing Essence fixtures. Per-action `generationHistory` compares actual native generation entries with resolved simulator generation snapshots and stable identities. Empty root History arrays are not evidence of generation-history equivalence. Permanent-deck checks include ordered references, upgrades/state keys and native serializable content. The fixed helper's skipped AfterCardChangedPiles behavior remains an explicit unverified listener boundary; this work does not claim every unknown listener is rejected.

VanillaGameActionProbe, VanillaBattleReplayProbe and VanillaRandomTargetProbe compare actual game actions with independent simulator branches. EleanorV040Probe binds version-specific card/power/relic adapters to exact real methods and source identities. Registration rejects mismatched types, manifests, assemblies and patch compositions. Branch state remains separate from actual mutable runtime state.

NativeModProgression executes a single live run through the first actual Victory, teardown, RewardsCmd.OfferForRoomEnd, selection through RewardsSetSynchronizer, native CardPileCmd.Add, legal map travel and first draw of the next battle. It retains the same Player/RunState, not a reconstructed postcombat fixture.

RunProgressRules validates protocol legality only. It is not an independent implementation of reward amounts, reward RNG or run effects. Native progression, protocol checks and combat differentials are reported separately.

## Authentic minimal Mod discovery

NativeModRunLoop uses ModManager's supported test reset/initializer path, authentic locked manifests and actual assembly associations before AssemblyInfo/model IDs. Actual custom model constructors and PoolAttribute register into native ModHelper.

Only BaseLib's original AddCustomCharacters and ModelDbSharedCardPoolsPatch discovery postfixes are installed. Canonical Pool queries now pass in this profile, including the separate StarterCardPool for GuidingPower. No fabricated Mod record or manually populated reward pool is used. Full framework initialization is not claimed.

A frozen discovery signature rejects a real late Harmony postfix and is restored after its removal. The existing unknown-patch audit remains enabled. Earlier relic/temporary-Recording controls use synthetic PatchInfo metadata and are explicitly labelled metadata tests rather than actual runtime weaving.

## Headless environment seams

- TestMode, Steam disabled and verified MemorySaveStore provide the offline host. Progress-save and replay calls are intercepted; ModManager file IO is read-only and rejects path escapes, writes and save copies.
- A single ModManager command-line query is bridged to actual process arguments to avoid Godot Dictionary static initialization. Platform/executable queries refer to the isolated Lab input view.
- MegaInput's unused hotkey StringName initialization is suppressed. The native TestMode selection screen returns null and the standard selector provides decisions. Reward population, alternatives/delegates, synchronization and effects still execute.
- Existing clock, resource, scene and hover-tip cleanup isolation applies to presentation. Damage, enemy actions, win checks and EndCombatInternal execute normally.

The final loop recorded three suppressed progress-save calls and one suppressed replay write across startup, Victory and next-node entry. Persistent save/replay writing was disabled.

## Repeatability and limitations

The cases runner freezes the first actually passed native battle plan and checks identical actions and complete raw continuations across independent processes. Adapter delegates bind directly to their existing static handlers, keeping implementation audit identities stable across processes without stripping audit fields from comparisons.

The five-process milestone has 24 checks: per-action differential/parent isolation, next-root equality and save/discovery isolation for each process; per-policy lifecycle/reward, terminal/root and full fixed-plan repeat checks; shared reward options, shared prefix plan and nonzero Record/Echo teardown checks.

The separate power API fixture does not represent an implemented Echo card. The published minimum-loop snapshot stops at next-combat first action; the subsequent local milestone adapts and executes ManaShield. Neither establishes retail rendering, full framework compatibility, complete character coverage or balance.

The paragraph above describes the published minimum-loop snapshot. The local-only extension adds a strict ManaShield mirror and actual two-battle replays; see [TWO_BATTLE_VALIDATION.md](TWO_BATTLE_VALIDATION.md). It uses real acquired-card DeckVersion checks, native hand resolution, explicit nonterminal prefix results and separate per-policy frozen second-battle plans. It preserves the original audit and partial/full terminal distinction.

The further bounded stages in [EXTENDED_VALIDATION.md](EXTENDED_VALIDATION.md) label their native API setup separately from natural reward acquisition. The six ManaShield boundary fixtures use genuine upgrade/resource commands, then independent simulator branches. Batch seeds are immutable bindings from the actual native scenario argument; they are declared before execution and never replaced after failure.

Opening card identity remains tied to captured native references. Independently generated cards receive stable per-world ordered first-observation identities. Counts, order, model/state and complete continuations must still match. A generated play resolves by an already-observed unique hand identity plus owner/model, after complete root equality, rather than by type/name alone. The retained failing plan verifies real Slimed generation and play with this resolver.

Native reward synchronization is queried before issuing a skip: taking all rewards can already close the reward set. The driver does not alter reward completion or effects. Two naturally offered Mod cards remain explicit unsupported coverage boundaries.

`NativeCombatDecision` wraps the already-used CombatSolver query. A ticket holds the actual source combat and complete live/continuation stamps, verifies search left native state unchanged, and is consumed exactly once. Each online iteration executes one actual action through the existing differential replay before querying the resulting boundary. The separate frozen mode executes recorded one-action chunks without new solver queries. The three-process bounded fixture passed 15 checks; no global optimality, arbitrary-seed support or full-run strategy claim follows.
