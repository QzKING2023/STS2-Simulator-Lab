# 搜索成本、其他职业与可见信息边界：有界试点

2026-11-05。结论为 **部分完成**：搜索对照及窄范围可见 DTO 检查通过；四职业完整样本中一条真实 Death、两条 Unsupported、一条 DataError。没有全职业整局支持、玩家可见整局策略、性能优化收益或可靠胜率结论。M1 封存不变，本轮未提交推送。

## 版本、登记与范围

- 游戏仍为 v0.111.0 / 41cef1ea；CombatSolver 0.50.1 的独立本地派生 `osty-root-identity.1`，DLL SHA256 `376D1E61BA2CF4640D9A36A73C56856B58244961D003ADECB21F0258F3B7D205`。没有再改求解器评分或依赖。
- 原生最终构建：`6352F742DA265F83356EBB118B8E75686D8B593FADB8665A02EF045AC1B95E13`，`.build/native-build/performance-study-02/build-manifest.json`。中间 build01 未执行原生样本。
- 登记、原始结果、哈希封存与分析：`.build/runs/performance-study-work01/`。`registration.json` 在首个进程前落盘；`closeout.json` 明确 PARTIAL。
- 预登记上限 16 次原生进程、首末执行窗口 60 分钟、4 GiB；短研究进程 240 秒、整局 465 秒。实际 **11 次、673.43 秒窗口、285.75 秒进程墙钟总和、收口时约 564 MiB**。全部登记进程已退出。没有为凑齐成功样本更换种子。
- 五职业 × 首战两个固定查询（q1、q6）× 两配置 × DOP1/2 × 两次重复，主分析纳入 **80 次搜索、10 个根状态**。另有原 Silent 的 16 次受潜在构建重叠影响，保留但不纳入计时主分析，因此实际研究求解调用总数为 **96**。种子 `LAB_V0111_PERF_PILOT_01`。这些是相关状态，重复不是独立统计样本。
- 四职业整局样本沿用 M1 Oracle、RunToTerminal、DOP2 与原预算；每个仅一个固定种子。替代配置的候选没有部署到这些样本中。

## 1. 复用上游预算和多线程，先测而未调优

已核实上游 `SolverSettings.ApplyPerformancePreset/ResolvePerformanceValues`；并行度为独立设置，并不等同于整局预算。

| 配置 | Beam | 每成员节点 | 卡/牌堆/手牌分支 | Soft 时间 | 本批执行 |
| --- | ---: | ---: | --- | ---: | --- |
| LabBounded | 8 | 256 | 32/18/24 | 10 秒 | 是 |
| 上游 Low | 45 | 60,000 | 24/12/16 | 60 秒 | 是，另设 10 秒协作取消 |
| Medium | 60 | 120,000 | 32/18/24 | 120 秒 | 仅配置核查 |
| High | 90 | 250,000 | 48/28/36 | 180 秒 | 仅配置核查 |
| VeryHigh | 135 | 500,000 | 72/42/54 | 300 秒 | 仅配置核查 |

两组一致关闭 HP 提前接受与可选 `UseBeamWidthPortfolio` / `UseNoveltyPortfolio` 开关；沿用现有评分、Smart 药水机制及 Coordinator。这里不表示关闭所有上游成员，OpeningPower、Refinement 等仍可能按既有门控运行。Low 的请求预算保持原值，外部 10 秒取消另记；本次 80 次均返回 `None`，没有碰到取消。不能据此推断复杂状态的 Low 耗时或完成率。

`NativeSearchStudy.cs` 对同一 immutable root 分别求解，记录墙钟、进程 CPU、全局分配增量、GC 次数、上游成员归因、候选路线、预测终态及工作量。每次核对原生完整状态/RNG 和父 fork；重复第二遍逆序执行以暴露顺序影响。

| 配置 | 次数 | 墙钟中位数 | 墙钟总和 | CPU 总和 | 实测最大并发 |
| --- | ---: | ---: | ---: | ---: | ---: |
| LabBounded DOP1 | 20 | 124 ms | 4.58 s | 4.55 s | 串行标记 0 |
| LabBounded DOP2 | 20 | 105 ms | 2.30 s | 3.61 s | 2 |
| Low DOP1 | 20 | 359 ms | 7.80 s | 9.61 s | 串行标记 0 |
| Low DOP2 | 20 | 292 ms | 6.21 s | 11.22 s | 2 |

按每个根状态的两次中位数再配对，单线程/双线程耗时比的中位数分别为 1.68、1.26。**这不是稳定加速比**：首战很小，只有两次重复，JIT、暖机、顺序和主机负载仍影响时间；Low DOP2 的总 CPU 也更高。当前证据支持继续使用已有并行能力，尚不支持全局升档或关闭正确性校验。

- 40/40 重复对的完整动作、预测结果和展开量相同；20/20 单/双线程对预测结果相同。所有父状态/RNG 检查通过。
- Low 在 10 个状态的两处预测保血更高：Silent q6 为 69→70，Regent q1 为 73→75，DOP1/2 一致；其余八处这些预测质量字段相同。九处候选路线不同。**没有原生执行替代路线，不能把预测保血作为实际收益。**
- `search-analysis.json` 保存逐根、逐重复、分配、GC 和成员表。诊断中的 root/context 是查询时的上一个原生派发标签；分析以 `oldSequence`、完整 `nativeBefore` 与 continuation 绑定真实当前查询，不把旧标签当作即将执行动作。
- 五职业新首战与旧修复构建的基线动作、完整状态、RNG、选择一致。这是显式标注的跨构建观测不扰动对照，不冒称同构建重放。

## 2. 搜索墙钟的细分与其他职业整局样本

| 职业 | 实际结果 | 确认/派发 | 差分一致步/记录步 | 墙钟 | 搜索墙钟 |
| --- | --- | ---: | ---: | ---: | ---: |
| Silent | Death，Completed | 323/323 | 289/289 | 74.50 s | 39.91 s（53.6%） |
| Regent | Unsupported | 167/168 | 139/139 | 30.28 s | 9.78 s |
| Necrobinder | Unsupported | 54/55 | 45/45 | 17.40 s | 7.76 s |
| Defect | DataError | 41/41 | 33/34 | 10.53 s | 2.25 s |

均从正常开局原生执行，同一 seed、无换样本。后三条没有完整终局，其运行时间不能当整局成本。旧 `battleTerminal` 摘要在失败路径可能仍指上一个已完成战斗，**全局分类以 `result.Stop/Terminal` 和本次实际回调为准**。

Silent 经 12 个战斗条目到真实死亡，原始数据约 178 MiB、峰值工作集约 879 MiB。同构建重放 323 动作、289 差分步和 **4,167 个完整状态/RNG 观察、2,029 项对照**全部一致；重放 85.43 秒且仍求解，不能与 live 相减推算搜索时间。第二条整局重放未执行，因为其余三个预登记样本没有完成终局。

已有三条完整在线成本样本的搜索归因如下。旧两条保持各自构建身份；此表用于定位成本，不是同构建策略比较。

| 样本 | 搜索墙钟 | PrimaryBeam 成员时间 | SmartPotionGradient | OpeningPowerRouteMember |
| --- | ---: | ---: | ---: | ---: |
| 旧 Ironclad DEV01 RunVictory | 95.87 s | 24.80 s | 45.83 s | 12.46 s |
| 旧 Ironclad EVAL02 Death | 40.07 s | 17.21 s | 11.39 s | 5.97 s |
| 新 Silent Death | 39.91 s | 13.74 s | 13.06 s | 4.00 s |

新 Silent 的上游总归因搜索时间 34.80 秒、总展开 93,806，其中 Primary 37,278、SmartPotionGradient 31,313、OpeningPower 13,574、Refinement 8,398。候选搜索边界为 230 次 None / 59 次 NodeLimit；存在不完整搜索不代表错误或数学最优。其他阶段分别记录原生根执行约 11.10 秒、投影 3.54 秒、根捕获及纯度检查 2.06 秒等。

这些墙钟、await、根执行、采集范围有嵌套；成员 GC/分配也有共享或重叠，不能相加为排他的 CPU 剖析。没有侵入式 profiler。现有数据使“仅两个样本均为 61%–63%”扩大到第三个职业样本，且比例变为 54%；仍不足以估计职业总体成本。

容量规划只可作条件算术：若十条运行都与这条 Silent 短死亡局相同，约 12.4 分钟和 1.74 GiB 原始证据，另加重放与审查；实际晚期胜利局可能更长。三个未完成样本不能填入完整运行成本均值，不启动 100 局。

### 真实缺口与下一步依据

1. Regent q140 / a168：HeirloomHammer 原生 FromHand 的 `Duplicate` 选择被 `NativeSolverChoices.Resolve` 的效果准入拒绝，包含真实 SovereignBlade token。Necrobinder q46 / a55：Snap 的 `ApplyRetain` 同样被拒绝。上游已有动作语义，Lab 通用选择桥还未接通；均在根动作派发后发生，保留未确认前缀，不能称零派发。
2. Defect q34 / a41：EndTurn 的 LightningOrb 结束战斗，原生和预测均出现 Victory 回调，但 continuation 中 `Y=0/0/0/0` 对 `0/0/1/1`、`AttackStarts=0` 对 `1`，其余字段（含 RNG）一致，父 fork 未污染。`Y` 后两项是本回合卡牌开始次数，不是 orb 伤害计数。生产检查器以 `Terminal policy: step exact` 拒绝该样本。应优先检查上游回合历史计数读取/侧切换与终态时序；**当前仅定位差异，尚未证明完整根因或修复**，不可删除这两个字段。
3. 下一批应先用上述原始失败做最小通用修复与独立回归，再续接原固定样本；无需为这次计时研究重跑 M1 全量。当前三个缺口是技术阻塞，不是权限拒绝。

## 3. 玩家可见信息：已验证的窄边界

新增 `VisibleCombatPolicy` 和单独 `VisiblePolicyWorker`：消费者仅接收 stdin JSON，依赖只有 Core，不引用游戏、CombatSolver，不传回调或真实对象。公共手牌/敌方槽位 token 映射当前合法 PlayCard/EndTurn；不发送 RNG、seed、抽牌顺序、root/hash、预测结果或日志路径。严格拒绝未知字段。

- 10 个真实根状态：隐藏 RNG、抽牌顺序、私有身份、seed/future 字段置换后，DTO 字节及确定性选择不变；改变公开 HP 会改变 DTO。原生状态/RNG 不变，零额外派发。
- 每个根五种隐藏字段注入被生产消费者拒绝；独立 worker **22 项正负控制通过**，包含十个真实包、嵌套私有字段、错误标签、槽位/token、重复和不支持动作以及合法公开变化。worker 与冻结原生构建使用完全相同的 Core DLL（SHA256 `384270D6D9B3C2D5E5CF6E52AA6FDF6B5C17848D37A914ADB576A2103A7D1FAB`）。
- 这里是 **受信任纯消费者的数据边界**，不是恶意插件的 OS 沙箱。隐藏置换保持同一个公开合法动作向量，没有在另一原生世界重新计算合法性；该策略也尚未原生派发。
- DTO 标记 `CommonCombatSubset`，缺少职业资源、完整牌费/动态值、意图等必要玩家信息；所有整局样本仍为 Oracle。**玩家可见整局隔离和公平策略成绩均未验收。**

已有 Core `CombatObservationV1/CombatObservationProjector/IPlayerVisiblePolicy` 对旧 `CombatRootV1` 提供可见投影；其 98 项契约回归仍通过。当前原生宿主使用另一采集格式，本轮新增的是该格式到受限 wire 的小型试验适配，不建立第二套搜索器或公开 SDK。后续应把公共字段映射回既有 observation 模型，保留严格 wire 与当前槽位动作封装，避免两个观察模型长期分叉；不通过构造虚假完整 CombatRoot 来填补缺失数据。

## 4. 验收、事故与保留

- 原生 build02 编译通过（0 errors，5 条既有警告）；独立 Core 构建/检查 98/98；当前构建的通用弃牌生产控制 10/10。
- 首战六进程（含一次 clean Silent）生产检查通过；Silent live/replay 完整检查通过；两条 Unsupported 的生产前缀及明确退出检查通过；Defect 准确性检查真实失败，未豁免。
- 22,117 项哈希核对、零变更：包括 M1 原始 12,391 文件、旧修复证据及构建、15 个锁定输入与 52 个兼容源码。新原始 4,975 文件单独封存。哈希证明一致，实际执行另依赖进程/调用链/差分证据。
- 初次 Silent study 可能与独立 worker 构建收尾重叠：原结果和准确性证据保留，计时汇总明确排除，使用一份预留预算做相同配置 clean repeat。
- 分析脚本一度混淆 continuation 字符串和原生动作 root，已按实际 `oldSequence`、查询状态和 continuation 修正；没有重写原始数据。
- 初版外层 Unsupported 审核误要求 task faults 为空。保留两份失败审核，后续把存档/Steam/UI 隔离与原异常传播分开核对；只接受与 `result.Error` 同一异常的已记录传播，不把它算执行成功。原生生产检查器和运行代码未为此修改。
- worker 构建出现 NuGet 漏洞元数据服务不可达警告，已有缓存依赖构建完成；未改安全/代理/身份设置，后续 Core 构建使用已有本地包源且不查询该服务。

## 5. 复现入口与后续门

已有证据可直接只读分析，输出必须选择新的文件名：

```powershell
python -X utf8 tools/performance/analyze_study.py --work .build/runs/performance-study-work01 --output .build/runs/performance-study-work01/search-analysis-review.json
python -X utf8 tools/performance/review_study.py visible --work .build/runs/performance-study-work01 --worker .build/visible-policy-worker01/VisiblePolicyWorker.dll --output .build/runs/performance-study-work01/visible-review-repeat.json
python -X utf8 tools/performance/review_study.py full --build .build/native-build/performance-study-02 --directory .build/runs/performance-study-work01/b02-full-silent-live --output .build/runs/performance-study-work01/full-review-repeat.json
```

原生入口为 `tools/performance/study_runner.py`；运行前应另登记新批次，不继续已收口目录。原始 launch 命令、输入哈希、manifest 与 process 中均有完整配置。

后续优先级：

1. 先修复 Defect 的真实终态差分及 Duplicate/ApplyRetain 的通用桥，保留原样本与错误分类，扩大样本前完成定向验证。
2. 将受限观察策略接到有界原生闭环，补公开字段、合法票据与不读取日志/Oracle 状态的调用审计，做隐藏状态置换和明确的端到端隔离验收；此前不比较公平策略成绩。
3. 从已支持的中后期战斗获取相同根状态，预登记少量多 seed 配置对照，再决定是否采用 Low 或调整并行。先复用现有成员统计，不立刻关闭 Smart 药水搜索或改变评分。
4. 完成上述门后才扩大性能/策略样本。此次没有证明完整性能与策略里程碑完成。
