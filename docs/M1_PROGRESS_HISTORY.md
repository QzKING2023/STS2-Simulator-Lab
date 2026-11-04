# M1 实施期间项目入口历史

以下完整保留最终验收前的 README；其中“当前/待验收”均为当时状态。最新结论见 [M1_FINAL_ACCEPTANCE.md](M1_FINAL_ACCEPTANCE.md)。

> 2026-11-04 M1 batch06：保持 Guarded 默认，统一显式终态策略；修复三处真实差分。六固定样本已有实际 Death，但仅 EVAL03–06 完成最终构建 live/replay（1,479 差分步、24,692 状态/RNG观察一致）；EVAL01/02 仍须最终构建补跑，M1 未通过，未提交推送。见[验证](M1_ACCEPTANCE_TERMINAL_VALIDATION.md)、[交接](M1_ACCEPTANCE_TERMINAL_NEXT_HANDOFF.md)。以下保留旧构建证据。

> 2026-11-04 M1 batch05: Reflections / CrystalSphere verified within declared scope; six fixed samples retain five policy stops and one budget stop. Two full-run regressions reach RunVictory. M1 acceptance remains pending; no commit/push. See [validation](M1_ACCEPTANCE_EVENT_COMPLETION_VALIDATION.md) and [next handoff](M1_ACCEPTANCE_EVENT_COMPLETION_NEXT_HANDOFF.md).

> 2026-11-04 M1 第四批：真实 Tainted 差分、药水选择、原生事件资源和继承 OnPlay 准入已修复；最终构建两次 499 动作 RunVictory、393 步差分保持。六固定样本仍有策略/预算停止及 Reflections 未支持，CrystalSphere 未验证，M1 未通过，未提交推送。见[验证](M1_ACCEPTANCE_TAINTED_RESOURCES_VALIDATION.md)和[交接](M1_ACCEPTANCE_TAINTED_RESOURCES_NEXT_HANDOFF.md)。

> 2026-11-04 M1第三批：Stratagem、先古真实交易、宝箱修正及三个事件已推进；最终构建两次499动作RunVictory重放/393步差分保持。六固定样本仍有真实差分、Unsupported与预算停止，M1未通过，未提交推送。见[本批验证](M1_ACCEPTANCE_LIFECYCLE_VALIDATION.md)与[后续交接](M1_ACCEPTANCE_LIFECYCLE_NEXT_HANDOFF.md)。

> 2026-11-04 M1 第二批验收：固定样本 01 已自然通过前两幕并推进至第三幕 630 确认动作；当前构建两份完整冻结重放均 RunVictory。六个固定样本仍有先古、阶段选择、宝箱和事件支持缺口，M1 未通过、不提交推送。见[验证与预算](M1_ACCEPTANCE_EVENT_INTENT_VALIDATION.md)及[后续交接](M1_ACCEPTANCE_EVENT_INTENT_NEXT_HANDOFF.md)。

> 2026-11-04 M1 固定样本验收：新构建两次完整冻结执行均 RunVictory，393 步差分和 499 个确认状态一致；六个预登记样本仍有事件/动态预测缺口，M1 未通过，不提交推送。见[验收报告与后续](M1_ACCEPTANCE_SAMPLE_VALIDATION.md)。

> 2026-11-04：首次原版自然完整 RunVictory 已完成同构建整局重放：499动作、22战、393步战斗差分、7589个完整状态观察点一致。M1最终统一验收尚待收口。见[重放与证据](COMBAT_SOLVER_RUNVICTORY_REPLAY_VALIDATION.md)、[最终验收交接](COMBAT_SOLVER_RUNVICTORY_NEXT_HANDOFF.md)。

> 2026-11-04 最新实施（待独审）：旧冻结同构建重放已补齐；通用默认遗物拾取完成 MummifiedHand 原生入库与后续休息，推进至第三幕第11层，410确认/411派发，在 SlipperyBridge 入口明确Unsupported。最终审计修复构建重放325文件一致（跨构建标注），M1未完成。见[验证与预算](COMBAT_SOLVER_RELIC_PICKUP_VALIDATION.md)、[继续交接](COMBAT_SOLVER_RELIC_PICKUP_NEXT_HANDOFF.md)。

> 2026-11-04 最新实施（待独审）：原生商店已分别记录返金、实付和生命变化并合法离店；第三幕 SoulNexus 精英真实胜利，407 确认/408 派发、319 差分步。领奖因旧遗物准入表拒绝 MummifiedHand 而 Unsupported；本轮重放误用场景名被零派发拒绝，待补同构建重放，M1 未完成。见[验证与失败记录](COMBAT_SOLVER_SHOP_LEDGER_VALIDATION.md)、[最小后续](COMBAT_SOLVER_SHOP_LEDGER_NEXT_HANDOFF.md)。

> 2026-11-04 最新实施（待独审）：第二幕 Boss 胜利及跨幕已重放验证；Nonupeipe 原生拾取后第三幕四胜，380 确认/381 派发、297 差分步同构建重放一致。当前 a381 商店真实购牌触发返金，Lab 净支出断言误拒，保留 DataError；M1 未完成。见[验证与预算](COMBAT_SOLVER_THROUGHPUT_THIRD_ANCIENT_VALIDATION.md)、[商店收支交接](COMBAT_SOLVER_THIRD_ACT_SHOP_NEXT_HANDOFF.md)。

> 以下为历史进展记录，未完成项以最新报告为准。

> 2026-11-04：逐幕入口已原生双跑验收；敌方回合选择已接通。最终构建269确认/201步差分后达到整局时间门；q857原生回归与最终重放尚未完成，M1未完成。见[当前证据](COMBAT_SOLVER_ENEMY_PHASE_VALIDATION.md)、[继续交接](COMBAT_SOLVER_ENEMY_PHASE_NEXT_HANDOFF.md)。

> 2026-11-04 本轮：原生 Symbiote 转换完成，第二幕第五胜、268确认/200个执行差分同构建重放一致；q836敌方回合选择明确Unsupported。逐幕入口遗漏已修正但尚未原生复验，M1未完成。见[验证与未认证项](COMBAT_SOLVER_FULL_RUN_CONTROL_VALIDATION.md)、[下一交接](COMBAT_SOLVER_FULL_RUN_CONTROL_NEXT_HANDOFF.md)。

> 2026-11-04 第5步：复用上游分支AI接通q705，保留静态近似标签并通用拒绝未验证根回退；第二幕累计四胜，175差分步、236确认/237派发同构建重放一致，停在第13层Symbiote入口审计（Unsupported/exit2）。M1未完成。下一轮需按完整run统一逐幕控制、预算和退出采集，见[当前验证](COMBAT_SOLVER_BRANCH_RUNTIME_VALIDATION.md)与[整体交接](COMBAT_SOLVER_BRANCH_RUNTIME_NEXT_HANDOFF.md)。

> 上轮历史：ThisOrThat Plain原生完成，217确认/159差分步停于q705静态近似准入，原始Unsupported保留，见[此前证据](COMBAT_SOLVER_THIS_OR_THAT_VALIDATION.md)。

> 上轮历史：两条常规Hook快速路径修复后，第二幕首战与Stone Lift完成；168确认/169派发停于ThisOrThat审计入口，原始证据保留，见[门控修复](COMBAT_SOLVER_TERMINAL_HOOK_REPAIRS.md)。

> 上轮历史：Swift真实执行通过，但第二幕首战终态Plating差分导致两次DataError/exit1；原始165动作和失败证据完整保留，见[映射验证](COMBAT_SOLVER_ENCHANTMENT_VALIDATION.md)。

> 上轮历史：已复用原生选择桥完成Darv非首项VelvetChoker领取，161根动作、122求解步骤重放一致；当时停在Swift动作协议门。RandomForeseer的Darv三项拾取预测仍明确未覆盖，见[此前证据](COMBAT_SOLVER_DARV_VALIDATION.md)。

> 2026-11-04 第5步新进展：Coordinator 在认证前缀后完成43个在线动作，真实击败第一幕 Vantom、领取100金币/SpeedPotion/Hellraiser并跨入第二幕 MapRoom；155动作与119求解步骤同构建重放一致。当前明确停在尚未接通的 Darv 先古入口（Unsupported），第5步/M1未完成，待独审。见[原生胜利、奖励与跨幕证据](COMBAT_SOLVER_COORDINATOR_BATTLE_VALIDATION.md)。

> 2026-11-04 第5步：已复用上游CombatSearchCoordinator，在认证110动作前缀后完成8个在线动作、两次EndTurn和完整状态/RNG重放；118动作后按8步额度BudgetExceeded，Boss/M1未完成。构建间仅增加成员遥测和检查器，尚非同构建重放，见[本轮范围及限制](COMBAT_SOLVER_COORDINATOR_TAIL_VALIDATION.md)。

> 2026-11-04 当前第 5 步：已复用原生商店完成四次购买、支付440金币、合法离店与休息升级，进入第一幕 Vantom Boss；110个动作和重放通过。Boss首查询被既有预测死亡采用门拒绝，未执行Boss动作，第5步/M1仍未完成。见[商店验证与证据](COMBAT_SOLVER_NATIVE_SHOP_VALIDATION.md)，待独审。

> 2026-11-04 当前 CombatSolver 接入：预算检查 R1 已修复，显式采用上游安全 NodeLimit 候选后，自然同 Run 七胜/奖励、106 个确认动作及重放通过；当前到达未接入的商店，明确 Unsupported，第 5 步和 M1 未完成，待独审。见[候选采用验证](COMBAT_SOLVER_BOUNDED_CANDIDATE_VALIDATION.md)。严格模式仍在原预算点停止；后续优先复用既有商店及 RandomForeseer 入口。

> 2026-11-03：Juggernaut窄排序v40候选已完成5次预登记原生执行，三个种子均真实Death；DEV_03为第三幕Death618/Boss119HP，DEV_01/02在第一幕死亡。详细重复与完整状态/RNG重放一致，未证实策略改善或RunVictory，M1未完成，待独立验收。见[本轮结果](NATIVE_RUN_JUGGERNAUT_POLICY_VALIDATION.md)。

> 上轮R1/R2及a617对照已[独立验收通过](NATIVE_RUN_JUGGERNAUT_INDEPENDENT_REVIEW.md)；其较好33HP结果包含固定前缀与局部干预，不能与本轮全程live候选作严格同策略对照。以下保留历史记录。

> 2026-11-03：分支选择/120秒停止门修复及a617对照已收口，待独审。6次原生执行、约3分7秒、1.30GiB；两张Defend真实触发格挡输出，最好Death624/Boss33HP。没有BossVictory或RunVictory，完整M1未完成。见[修复与对照报告](NATIVE_RUN_JUGGERNAUT_REVIEW_REPAIRS.md)。

> 2026-11-03：有预算原生组合搜索已收口，13个候选＋2个完整尾部及2次冻结重放；最好真实Death624/Boss59HP，没有BossVictory或RunVictory，M1未完成。分支live非首项耗尽及精确前缀已验证，待独立验收。见[本轮报告](NATIVE_RUN_BOUNDED_SEARCH_VALIDATION.md)。

> 2026-11-03：TestSubject 固定前缀策略实验已收口，16 个开发案例均真实 Death，当前 Death618；Boss 胜利和 M1 仍未完成。最终冻结 `187D8116…21D49` 的重放/失败传播定向验收见 [报告](NATIVE_RUN_TESTSUBJECT_STRATEGY_VALIDATION.md)。待独立审查；完整 M1 同构建全量门保留。

> 最新本地进展（待独审）：正常开局同一 Run 已完成第三幕 MechaKnight 精英胜利、Orichalcum 原生领奖并到达 TestSubject Boss，真实 Death610。见[本轮验证](NATIVE_RUN_ACT3_BOSS_PROGRESS_VALIDATION.md)。完整 M1、第三幕 Boss 胜利和 RunVictory 仍未完成。

> 最新检查器修复（2026-11-03）：第三幕商店独审 R1/R2 已修复并通过冻结原证据定向验收，待独立复审。仅一个生产检查器文件变化，未启动新原生流程；最远仍为 Death545，Boss/RunVictory/M1 未完成。见[修复与证据](NATIVE_RUN_ACT3_SHOP_REVIEW_REPAIRS.md)。

> 最新实施（2026-11-03）：第三幕自然补给、两项新事件与死亡后最大生命值采集已完成本地定向验证，待独立审查；自然最远为第三幕第十二层真实 Death545。见[本轮证据](NATIVE_RUN_ACT3_SUPPLIES_VALIDATION.md)和[下一交接](NATIVE_RUN_ACT3_SUPPLIES_NEXT_HANDOFF.md)。第三幕 Boss、RunVictory 和最终 M1 全量门仍待完成。

最新身份修复：[玩家死亡主体绑定](NATIVE_RUN_DEATH_OWNER_REPAIRS.md) 已实现并完成定向验证，待独审。先前自伤死亡独审发现的4个误接受副本保留；第三幕Boss/RunVictory/M1尚未完成。

最新定向修复：[回合开始自伤死亡认证](NATIVE_RUN_SELF_DEATH_REPAIRS.md) 已实现并完成原生重放、负控与证据重验，待独立复审。第三幕 Boss、RunVictory 和完整 M1 仍在推进。

> 2026-11-03 最新第三幕进度：自然三战胜利/奖励、FakeMerchant 不交易原生离开，随后第6层 Death474。封版 `59EB82B365107C244603DBE2F530EB3538F6441BBF656AF9EB01E32CCC4DE9FE`，待独审。Boss/RunVictory/M1未完成；另保留一项回合开始自伤死亡认证缺口。见 [本轮报告](NATIVE_RUN_ACT3_PROGRESS_VALIDATION.md) 与 [交接](NATIVE_RUN_ACT3_PROGRESS_NEXT_HANDOFF.md)。

# STS2 Simulator Lab

当前自然流程已进入第三幕：有限首战路径387动作胜利并领奖；连续路径两战胜利/奖励后在第4层真实Death418。当前分层验证待独立审查；第三幕Boss和完整RunVictory仍待推进。

[本轮验证](NATIVE_RUN_ACT3_VALIDATION.md) · [证据信任边界](NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md) · [后续交接](NATIVE_RUN_ACT3_NEXT_HANDOFF.md)

An experimental offline validation framework for Slay the Spire 2 combat and run-state behavior.


## Current direction

The approved main line has two milestones:

1. **Vanilla native offline full runs and trustworthy data**, beginning with Ironclad on the fixed game version. Native execution does not require every effect to support simulator forks.
2. **A public Mod interface**, followed by actual Eleanor acceptance through that interface.

Prediction and strategy development support both milestones. New Mod-specific expansion is paused; existing bounded compatibility and safety regressions are retained. See the [current roadmap](LONG_TERM_ROADMAP.md) and [Milestone 1 implementation plan](MILESTONE_1_NATIVE_RUN_PLAN.md). The first native slice now executes a natural Ironclad opening, one Victory, native rewards and the next combat's Play boundary without loading Mods or CombatSolver; [N0/N1 validation](NATIVE_RUN_N0_N1_VALIDATION.md) records the frozen build, replay and limits. Full-run implementation remains in progress.

[N2 validation](NATIVE_RUN_N2_SNAPSHOT_REVIEW.md) has passed independent review for bounded native choices, rewards, typed records and integrity controls. [Step 4A validation](NATIVE_RUN_N3A_VALIDATION.md) records native enemy-action Death and a separate Architect lifecycle fixture through actual native RunVictory; local final regression passed and awaits independent review. The [data contract](NATIVE_RUN_DATA_CONTRACT.md) explains the evidence and RNG scope. Natural full-run victory remains unverified.

## Verified foundation

The implementation targets game **v0.111.0 / 41cef1ea** and a separately locked Eleanor **v0.4.1-forms-cardpool.fix** input.

- Vanilla combat replays compare native actions with simulator branches through Victory, including enemy turns and a two-target RNG fixture.
- Eleanor has bounded card, form, upkeep and Record/Echo API coverage, with strict version/assembly/manifest/patch checks.
- The minimum offline run loop executes **Victory → native rewards → legal next node → next battle's first actionable state**. Gold collection plus card-skip and card-take policies repeat in separate processes with the same fixed battle plan.
- A separate power API fixture verifies temporary Recording expiry and nonzero Record/Echo cleanup across combats.

This is a validation milestone, not complete character support, independent reward prediction, balance analysis or a full-run optimizer. The published minimum-loop snapshot stops at next-combat first action. The local two-battle milestone additionally adapts ManaShield and validates both reward policies through the second Victory; see [two-battle validation](TWO_BATTLE_VALIDATION.md). The six candidate Echo cards remain absent from the pinned input.

See [milestone scope](MILESTONE_STATUS.md), [validation setup](VALIDATION.md), and [implementation provenance](PROBE_PROVENANCE.md).

## Roadmap and active work

- [Development approach](DEVELOPMENT_APPROACH.md): investigate reuse, licensing, pinned dependencies and adaptation costs; validate the smallest useful slice before expanding custom implementation.
- [CombatSolver policy adapter](COMBAT_SOLVER_POLICY_ADAPTER_VALIDATION.md): the verified capture/search/one-native-action/requery gate, architecture, budget, evidence and remaining integration work.
- [CombatSolver ordinary action loop](COMBAT_SOLVER_ACTION_LOOP_VALIDATION.md): stale-ticket/Oracle/capture repairs and verified targeted attacks, native enemy turn, next-player boundary and online re-query.
- [CombatSolver choices and potions](COMBAT_SOLVER_CHOICES_POTIONS_VALIDATION.md): strict action-to-dispatch binding and the first native TrueGrit/FirePotion API fixture, with preserved failures and remaining online/replay coverage.
- [CombatSolver online choices](COMBAT_SOLVER_ONLINE_CHOICES_VALIDATION.md): repaired instance binding, solver-selected TrueGrit child choice with exact live/replay, and a preserved potion-path budget stop; step 2 remains incomplete.
- [CombatSolver Smart potion](COMBAT_SOLVER_SMART_POTION_VALIDATION.md): upstream coordinator reuse, actual unforced EnergyPotion use and requery, and self-target validation repair.
- [CombatSolver step 2 review handoff](COMBAT_SOLVER_STEP_2_REVIEW_HANDOFF.md): completed same-build EnergyPotion replay, supported actions/choices, explicit limits, and the combined independent-review evidence.
- [CombatSolver whole battle validation](COMBAT_SOLVER_WHOLE_BATTLE_VALIDATION.md): online natural first-battle Victory and explicit Death fixture, both replayed, pre-cleanup differential and native cleanup scope; pending independent acceptance.
- [CombatSolver whole battle review repairs](COMBAT_SOLVER_WHOLE_BATTLE_REVIEW_REPAIRS.md): native terminal callback state, action and context binding with the original counterexamples rejected.
- [CombatSolver run loop](COMBAT_SOLVER_RUN_LOOP_VALIDATION.md): same-run native rewards, map travel and two online battle Victories, including acquired Rage play and complete replay; directed validation pending independent acceptance.
- [Long-term roadmap](LONG_TERM_ROADMAP.md): the approved two-milestone direction, retained foundations and completion gates.
- [Milestone 1 native-run plan](MILESTONE_1_NATIVE_RUN_PLAN.md): code-grounded scope, host and choice boundaries, data contract, work packages and acceptance matrix. This is distinct from the completed historical Stage 1.
- [Historical roadmap archive](LONG_TERM_ROADMAP_2026_11_02_ARCHIVE.md): the prior roadmap, retained for provenance rather than current priorities.
- [Native-first feasibility analysis](NATIVE_RUN_FIRST_FEASIBILITY_CANDIDATE.md): the historical candidate analysis and the bounded reward-card review conclusion; superseded by the formal roadmap for implementation priorities.
- [Historical Stage 1 implementation plan](STAGE_1_IMPLEMENTATION_PLAN.md): terminal outcomes, budget stops, stale-action rejection, independent contracts and reproducible regression; see the accepted review below for completion evidence.
- [Stage 1 implementation and verification](STAGE_1_VALIDATION.md): actual terminal behavior, result classification, regression evidence and retained limits.
- [Stage 1 review repairs](STAGE_1_REVIEW_REPAIRS.md): accepted result-gate repairs, frozen build identity, negative controls and independent re-review.
- [Reward-card compatibility plan](REWARD_CARD_COMPATIBILITY_PLAN.md): ReturnFlow, ManaConversionPrinciple and ManaEssence contracts, shared choice/replay repair, and the unchanged seed cohort acceptance plan.
- [Reward-card compatibility validation](REWARD_CARD_COMPATIBILITY_VALIDATION.md): implementation, directed and natural coverage, final regression evidence and limitations.
- [Reward-card review repairs](REWARD_CARD_REVIEW_REPAIRS.md): strict action-field rejection, physical A/B/A identity and independent generation-history evidence.
- [Latest bounded validation evidence](EXTENDED_VALIDATION.md): ManaShield boundaries, the declared seed cohort and online decisions.

## Core checks

The core protocol library does not require game or Mod assemblies:

~~~powershell
dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj
~~~

Game-bound validation additionally requires locally supplied licensed game files and pinned dependencies. No dependency is downloaded, deployed or bundled. Configuration uses command parameters or environment variables documented in [VALIDATION.md](VALIDATION.md). Local input locks, binaries, reports and research stay under ignored directories.

## Licensing

Original Lab code is MIT licensed; see [LICENSE](../LICENSE). [THIRD_PARTY_NOTICES.md](../THIRD_PARTY_NOTICES.md) preserves upstream attribution and distinguishes external dependencies from Lab code. The Lab does not distribute or relicense game code, game assets, Mod binaries or private research.

Only project source, necessary validation tools and public technical documentation belong in this repository.

原版原生流程最新子阶段：[4D1C 原生遗物、购买 Hook 与补货验证](NATIVE_RUN_N3D1C_VALIDATION.md)，以及[后续交接](NATIVE_RUN_N3D1C_NEXT_HANDOFF.md)。完整 M1 尚在推进。


历史子阶段：[4D1C R1/R2集中修复及同构建回归](NATIVE_RUN_N3D1C_REVIEW_REPAIRS.md)。

当前策略接入：[CombatSolver两战报告绑定修复](COMBAT_SOLVER_RUN_LOOP_REVIEW_REPAIRS.md)；[第5步自然多节点前缀](COMBAT_SOLVER_NATIVE_JOURNEY_VALIDATION.md)（五胜后预算停止，完整M1仍在进行）。[RandomForeseer复用边界](RANDOM_FORESEER_REUSE_NOTE.md)说明后续单节点预测对照及多步限制。
