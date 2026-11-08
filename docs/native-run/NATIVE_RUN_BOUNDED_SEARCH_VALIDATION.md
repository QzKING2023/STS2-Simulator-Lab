# TestSubject 有预算原生组合搜索验证

2026-11-03，本轮已停止新增原生执行，等待独立验收。**没有 TestSubject 胜利、RunVictory，M1 尚未完成。** 最好完整战斗结果为真实 **Death624，玩家第 11 回合，Boss 剩 59HP**；另一个入围前缀为 Death610、Boss 剩 94HP。不能把有限 rollout 的存活状态或退出 0 当胜利。

最终搜索/尾部构建：`414A75D8BB0334B941D95A1A985A8711AA5821A4797FAB03C2757EBA40E5CFA0`，清单 `.build/native-build/bounded-search-final01/build-manifest.json`。总证据 `.build/runs/bounded-search-work01/closeout.json`，原生输出位于 `.build/runs/bounded-search-*`。源码、构建、输入核验及关键事实见 `facts.json`，配置与所有尝试见 `process-ledger.json`。

## 新能力与范围

新增窄内部原生分支 provider、计划停点及生产检查器契约。执行始终从正常 Ironclad/A0/Standard 开局开始；固定 `v0.111.0/41cef1ea`、种子 `LAB_V0111_NATIVE_DEV_03`。精确重放 burst612 自己的正常开局前缀至 a595：玩家 67HP、TestSubject 第三形态当前 299HP、turn5 的真实 Play。没有载入战斗状态快照、修改资源、拼接其他策略的状态或克隆原生全局状态。

前缀仍核对全部动作及 before/after 状态哈希。选择和奖励前缀也须完整消费；到安全确认边界后仅切换一次。分支动作先检查当前原生合法候选，再执行原生任务；每步重新枚举。子节点必须再次从正常开局重放父亲的实际动作前缀，并在纳入评分前比较父前缀的完整状态/RNG。新结果没有 simulator 差分，`Comparison=NotCompared`。

True Grit 的新分支选择在真实 FromHand 请求到达时按候选实例选择，继续经过原来的请求/context/stamp/cardinality/消费校验。接口样本 a596 实际选择非首项 Stampede i1220，并由原生 CardExhaustedEntry 记录同一个对象；重复进程得到相同完整边界和 choices。该证据覆盖**新分支 provider 的 live 非首项执行**；旧 v39 在打牌前计划索引的通用路径没有因此自动获得新增覆盖。

计划停点只在原生动作和子任务完成、玩家存活且处于原生 Play 时触发，结果为 Ongoing/SliceBoundary；不会制造 Death/Victory。生产契约绑定 switch、frontier、horizon 的请求/动作归属、顺序、必需唯一和完整确认快照，要求指定动作及选择消费完成，核对两次已确认 EndTurn 周期。真实死亡仍按原来的原生死亡锚点、清理和终态契约认证。

## 预算和执行结果

依据[交接](NATIVE_RUN_TESTSUBJECT_BOUNDED_SEARCH_HANDOFF.md)冻结宽度2、深度3、每节点最多4候选，未根据结果调常数或增加层数。

| 范围 | 本轮实际 |
| --- | --- |
| 接口额度 | 8 次尝试，其中 1 次构建未完成导致包装器未启动原生进程，保留失败；其余 7 个原生进程 |
| 接口正向 | a595 精确前缀；实际非首项耗尽与重复；两敌方回合安全停点；旧 610 路线完整冻结重放 |
| 接口负向 | 篡改 a595 after 哈希：594确认/595派发、EngineError、Unknown；非法卡：595/595、InvalidAction，均退出1并拒绝认证 |
| 搜索 | 第1层4次、第2层7次、第3层2次，共13个原生 rollout；全部达到真实 Ongoing 停点 |
| 停止原因 | 固定三层完成；部分节点合法候选较少，未用剩余额度追加深度/种子 |
| 完整尾部 | 两个入围前缀各1次，均真实 Death |
| 尾部复现 | 两条完整正常开局路线各1次冻结重放，完整状态/RNG及 route/choices/reward-choices/final-state 一致 |
| 新增原生总数 | 24；另1次未启动原生的包装尝试，共25次尝试，全部低于各组上限 |

预登记配置、时间与数据量见 `budget.json`、`search-registration.json`、`tail-registration.json`。各进程未触发120秒 watchdog；最后原生执行发生在60分钟截止前。执行证据与三个构建目录总量低于8GiB，准确字节数见 closeout；没有清理或替换失败来腾出预算。

搜索候选按固定规则生成：EndTurn、v39首选、两个实际 True Grit 实例选择，再用防御/剩余合法首选补足至4项。相同动作/不同耗尽实例分开计入额度；默认选择与显式选择可能得到同一状态，实际执行后按完整 frontier 状态合并保留席位。第2/3层候选数变少是合法候选减少，不是漏跑已登记样本。

节点指定至多3个根动作，其后按固定 v39 规则实际执行两个 EndTurn 周期。保留一席实际 HP 优先、一席 Boss HP 优先。该评分没有完整估计后续手牌价值；不同指定前缀可能已包含 EndTurn，因此 horizon 的总时间并非严格相同。所有选择均基于原生未来 rollout 结果，标为 **oracle-native-rollout**，不能称公平玩家信息策略、全局最优或可靠胜率。

第2层最好有限结果为玩家44HP/Boss190HP；最终入围的有限结果为44/185及25/176。完整尾部才给出624步/59HP和610步/94HP的真实死亡。保留 Stampede、耗尽指定 Defend 的组合改善了本批实际终局，但不能把该关联推广为所有局面应当如此选择。

## 验证层次与数据口径

接口生产构建为 `bounded-search-dev02`，编译0警告/0错误。之后仅修改检查器对真实第三Boss完成的收尾接受条件：必须继续通过原 Major 胜利/奖励权威校验，不能凭 mode=tail 直接通过。最终构建同为0警告/0错误；差异见 `build-transition.json`。5份正向接口证据用最终检查器重验，原始输入未变。不能把接口旧 producer 和最终 producer 混称一次最终 M1 同构建验收。

**13次搜索、2次完整尾部和2次完整尾部重放共17个进程均使用最终同一构建。** 搜索使用既有 minimal 模式，完整动作边界、实例、状态/RNG和选择保留；详细 native-history 条目按模式未采集。搜索02与 detailed 接口同一分支的全部边界一致，验证该受影响分支的观测不扰动。接口耗尽和最终尾部保留 detailed 原生历史；未宣称每个搜索样本都有完整效果因果图。

生产检查器新增13个有限副本控制：1个合法重序列化副本通过，12个损坏副本拒绝。覆盖 switch/horizon 缺失、重复frontier、错误owner、完整RNG/未知state字段共同篡改、错误停点mode、旧手牌实例、选择索引、指定动作错配、停点后继续动作和非Play停点，均由语义门拒绝而非仅封签错误。

最终检查器重验既有144份冻结输入，50接受、94预期拒绝，零预期外结果；旧缺锚点证据和旧实际失败继续未认证。加上5份接口重验、13次搜索、4次尾部/重放、13个控制和2个接口失败，最终检查器共181次调用；另5次接口初始检查使用接口 producer 的检查器，单列。383项来源、实例、父前缀、状态/RNG、预算及隔离事实核对通过。这些数量表示验证规模，不能替代实际流程进度。

负向接口实验的来源确为正常开局，输入由本批人为损坏；在实验账本/closeout中列为 input-control，不能计入自然成功样本。保留原 producer 的 provenance 和错误，不事后改写原始标签。原先包装失败日志及构建警告的开发产物也保留，未当作成功原生执行。

15锁定输入、52兼容源码及Core产物不变。所有实际进程继续采用MemorySaveStore、SaveProgress/replay拦截、无UI、无真实Steam/网络/Telemetry、严格未知补丁审计；没有修改角色或游戏安装、真实存档、其他项目，没有提交或推送。

## 限制与交接

本批只闭合固定 TestSubject 入口的内部原生分支/选择和有限搜索，不是通用快照fork、公开Mod接口或完整战斗求解器。单候选/所有特殊选择、复活、广泛牌组与其他Boss未由本批覆盖。现有预持折扣遗物 rawCost 缺口继续保留；该固定Boss入口没有新的商店，不扩大无关原生矩阵。

真实终态仍为死亡。后续可根据最佳轨迹在下一危险回合另立有限实验，或推进其他原预登记种子的正常完整流程，保留 DEV_03 未胜利事实。本轮不再新增搜索。下一交接见 [NATIVE_RUN_BOUNDED_SEARCH_NEXT_HANDOFF.md](NATIVE_RUN_BOUNDED_SEARCH_NEXT_HANDOFF.md)，M1最终同构建全套验收仍待完成。

## 本地复现入口

保留本轮旧目录，使用新的输出目录和另立的预算再复现。单个分支入口为 `tools/native-run/run-native-slice.ps1 -BuildManifest <冻结清单> -OutputDirectory <新Lab输出目录> -Scenario major-natural-act3-route -Seed LAB_V0111_NATIVE_DEV_03 -ReplayPath <burst612/route.json> -BranchPlan <本轮某项plan.json>`。原样配置及实际执行脚本在 `.build/runs/bounded-search-work01/`，包含输入哈希、Moves、Mode和HorizonTurns。旧批次脚本不应直接覆盖重跑；本轮已停止新增执行。检查器入口为冻结DLL的 `--review <证据副本目录>`，损坏控制只作用于显式标记副本。
