# 分支选择、超时门修复及 a617 原生对照

2026-11-03。已完成本轮登记范围，等待独立复审。**仍没有 BossVictory 或 RunVictory，完整 M1 未完成。** 对照证实了 v39 忽略无防御需求时格挡输出的盲点：最佳原生终局仍为 Death624，Boss剩33HP；基线为59HP。

冻结：`87CE0E2BA6856B18EC882DF1BDAD22CAEA861439D8C4662A19506BCC83BB98BB`，清单 `.build/native-build/juggernaut-final01/build-manifest.json`。编译0警告/0错误。证据和实际脚本在 `.build/runs/juggernaut-work01/`，其中 `closeout.json` 为收尾索引。

## 修复及分层回归

依据 [独立复审](NATIVE_RUN_BOUNDED_SEARCH_INDEPENDENT_REVIEW.md) 修复两项问题：

- **R1**：`NativeBranchContract` 在派发时保留当前强制动作的Exhaust约束，跟踪当前FromHand请求→分支决策→解析→返回→确认。绑定请求id、root/context、实际对象，要求显式决策唯一且不能省略；指定Exhaust必须等于实际解析对象并完成返回。默认与隐式路径按原生记录处理，没有给隐式单候选添加虚构决策。
- **R2**：`run-native-slice.ps1` 对带分支和完整冻结重放统一使用默认120秒、最大120秒的显式参数，调用 `NativeProcessBudget.ps1` 的单调时钟门。超时只终止该次所属进程树，保留已经写出的文件，记录watchdog与生效限额。外层异常停止实验并保留不确定结果，不自动补跑。

最终构建与前冻结相比仅三个生产文件变化：上述两个文件和新增的 `NativeProcessBudget.ps1`。编译C#仅检查器变化，Core产物不变，原生执行牌效/策略/宿主补丁没有修改。此次选择不同牌由既有窄branch provider指定，**常规v39评分尚未修改**。

先用最终生产检查器重读旧24个原生证据与13个旧控制：37次均符合原预期，23接受/14拒绝。复验前审8控制，加5个同类控制，共13次：2合法接受、11错误拒绝，原三误接受全部闭合。新增覆盖缺返回、错解析上下文、错请求id、重复决策，以及合法默认null Exhaust。旧原始材料未改写，副本/日志和变异配方保留。

超时门用**非游戏API夹具**验证：2秒限额在约2.055秒触发，所属父进程及子进程均退出，写出的prefix文件仍在；正常快速退出未误判。共2项API控制、3个辅助OS进程，不冒称原生游戏120秒超时实证。新6次原生执行均记录生效120秒，实际约24.6–27.7秒，没有触发超时。

修复属于检查器和包装器限额变化，因此没有无条件重跑原生全量矩阵。随后原生对照本身覆盖了共同入口、实际强制动作、默认路径及无BranchPlan的完整重放。最终M1同构建全套验收继续待完成。

## 登记、预算和来源

`experiment-registration.json` 在开跑前固定种子 `LAB_V0111_NATIVE_DEV_03`、来源 `bounded-search-tail01`、a617确认边界、三种方案、排序与停止条件。全部从正常开局重放到同一真实状态；没有克隆全局状态、改HP/能量或拼接后续。入口为第三形态TestSubject76HP、玩家3HP/2E/5Block、turn10 Play，Juggernaut12，两张升级Defend及Dark Embrace等实际手牌。

预算是8次原生尝试、30分钟、2GiB新增数据，任一先到停止，且每进程120秒；后续尾部仍保留96根动作/16玩家回合/128子选择限额。实际只登记三种方案，执行顺序为：入口probe、三方案、最佳方案详细尾部、完整冻结重放，共**6次**。剩余2槽位未用于追加搜索。前置修复回归没有启动原生游戏。

从 `2026-11-03T23:20:41.881959Z` 登记，到 `23:23:48.762130Z` 完成原生及重放检查，约**3分7秒**；6进程原生用时合计约154.5秒，最后进程于 `23:23:41.150273Z` 退出。最终数据约**1.30GiB**，精确字节数在closeout，包含新构建、原始执行/便利导出、修复和reader副本、日志与控制。没有删除原生原始证据来满足限额；控制副本使用无损gzip保存，原始旧328份执行/配置文件哈希仍匹配。

## 实际结果

| 方案 | a617后指定动作 | 真实结果 | Boss剩余HP |
| --- | --- | --- | --- |
| baseline | 无；继续原v39 | Death624 | 59 |
| two-defends | Defend i1195，再Defend i1221 | Death624 | 33 |
| embrace-defend | Dark Embrace i1205，再Defend i1195 | Death625 | 47 |

入口probe为真实Ongoing/SliceBoundary617。原生任务结束后的两个候选都在短窗内真实死亡，未用SliceBoundary伪装终局。所有动作先通过实际 `CanPlayTargeting` 检查，再执行原生牌效，未因为v39给负分而将合法牌排除。

最佳方案详细历史在 `juggernaut-tail/trace.jsonl`：a618的升级Defend实际获得8Block、能量2→1、Boss76→64；a619再获得8Block、能量1→0、Boss64→52。每个根动作内都有真实BlockGainedEntry及12点Unpowered DamageReceivedEntry，符合当时Juggernaut12的机制。玩家HP仍3。后续实际死亡时Boss33HP；相对基线最终多减少26HP，不能把最终差异全部归结为当下两次24点触发，后续牌序/状态及RNG消耗也发生变化。

玩家仍在第11回合EndTurn被原生敌方动作杀死，未获得Boss奖励、未进入Architect或RunVictory。该入口的合法输出增益已经证实，能够获胜以及可靠胜率仍未证实。

## 状态、重放与证据口径

- 六条路径的a617之前dispatch/confirmed完整payload均与来源一致，包括状态、实例、所有捕获RNG状态字和计数。基线完整路径与原Death624一致。
- 最佳候选与详细尾部相同；详细尾部及完整冻结重放逐边界一致，route、choices、reward-choices和final-state也完全一致。生产检查器对6份新执行均接受。全部结果Comparison为NotCompared，没有声称simulator差分。
- 修复50次reader调用加新6次，共56次生产检查；另外302项冻结/输入/原生事实/预算核对通过。新旧材料生产者身份分别保存，旧证据重读不能称为新构建重跑。15锁定输入、52兼容源码、Core不变。
- 每次均为无Mod/CombatSolver的原生路径，MemorySaveStore、shouldSave/UI false、Steam未初始化、Telemetry0、host faults空。保存/replay拦截计数表示拦截，不是实际持久化写入。
- 对照候选使用minimal，详细尾部和重放使用detailed；共享完整边界相同。未宣称每个minimal分支具有完整伤害触发因果图。不同策略消耗不同RNG，不能把同seed称为同一随机事件的严格配对。

主要文件：`choice-repairs.json`、`repair-readers/reread-summary.json`、`timeout-controls.json`、`experiment-ledger.json`、`experiment-outcomes.json`、`experiment-closeout.json`、`state-comparisons.json`、`causal-records.json`、`facts.json`和`execution-lock.json`。脚本拒绝覆盖既有执行目录，不能直接重复运行这批登记。

## 下一步边界

本轮已完成固定对照并停止新增原生执行，待独立审查两项修复及六条原生证据。建议停止扩大此TestSubject入口的逐次试错，转回原预登记DEV_01/02自然路径与M1后续生命周期；保留DEV_03未胜利和这些反例。必要的共享策略调整可利用本轮发现，但应另登记范围和预算，不能把余下两个槽位临时挪作新实验。

仍未覆盖：通用任意卡/多层选择分支、显式Exhaust遇隐式单候选、旧v39提前非首项排序、复活、分支BossVictory/正常WinRun、预持折扣遗物原价、完整M1统一回归。没有开展Mod专项、其他项目、UI、真实Steam/网络、用户存档或安装操作，未提交推送。没有环境或审批阻塞。
