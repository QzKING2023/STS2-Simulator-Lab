# 职业兼容修复与玩家可见首战试点

日期：2026-11-05。状态：**本次有界批次完成；性能与策略里程碑仍为部分完成。**

## 版本与范围

- 游戏仍为 v0.111.0 / `41cef1ea`；种子固定 `LAB_V0111_PERF_PILOT_01`，没有换掉失败样本。
- 本批有效执行构建：`1C4E973F45E05D9B4774EF61F9605A47DBB8FA6ACA00ED6475E68E5BE98DFA46`，目录 `.build/native-build/performance-compat-02`。
- Solver 仍复用 CombatSolver 0.50.1，新增 MIT 派生 `orb-terminal-boundary.1`，DLL SHA256 `6D0C4421D03451A1DFEED2809545B89B95048594D3985FCA775A4531499C299F`。父派生为 `osty-root-identity.1`，父 DLL `376D1E61BA2CF4640D9A36A73C56856B58244961D003ADECB21F0258F3B7D205`。来源/源码/产物在独立 lock 中固定，旧 pin 不改。
- 搜索评分、种子、上游 Coordinator 策略和预算均未调整。完整样本沿用 `RunToTerminal`、DOP 2、beam 8 / nodes 256 / soft 10000 ms、aggregate return admission 16384；返回准入门不是精确熔断器。
- 原始证据、注册、测量、检查与封存：`.build/runs/performance-compat-work01/`。M1 旧证据没有用于冒充本批构建结果。

## 三项原问题

### Regent：Duplicate

上游已经有 Duplicate 选择和复制语义。失败来自 Lab 传输层未准入对应 Hand / FromHand 选择。现在复用原 PlanCardChoice 的模型、升级、StateKey、来源 occurrence、候选 occurrence 和实际原生 selector；复制仍由原生 OnPlay 完成。

固定样本 q140、q182 实际完成选择及复制，逐步差分、父分支隔离通过。记录选择前后各牌实例，检查被选原牌保留、新对象确为 clone、实际模型/升级一致及永久牌组完整内容不变。生产检查器 11 项正负控制通过。

### Necrobinder：ApplyRetain

同样是已有上游效果被 Lab 选择准入遗漏。q46、q57 实际原生施加 Retain，与分支预测一致；核对被选对象、关键词、StateKey 和原生最终牌堆。生产检查器 10 项正负控制通过。

这些结果覆盖实际遇到的单次手牌选择，不能扩大成任意嵌套、多重选择或无候选/满手等全部组合。

### Defect：回合末球击杀后的历史计数

原失败中，球效果已使战斗结束，但预测在提交终态前继续走回合侧转换，卡牌历史缓存随之被清零。新派生只在已有 `SimulateEndPlayerTurnAfterOrbPassives` 的 `IsOverOrEnding` 分支调用现有 `CheckWinCondition`；不改卡牌、球数值、评分或原生业务。

新原种子样本通过对应 EndTurn 球击杀边界，并运行到真实 Death。历史字段、完整 continuation 与 RNG 仍全部比较，没有删字段。不过修正预测会改变搜索选择：相对旧失败轨迹，首个动作差异在 a40；q34 同一房间/EndTurn 的手牌与球状态已不同。因此这是同种子新实际轨迹对通用终态边界的回归，**不是旧状态逐位相同的反事实重演，也不是策略质量提升证据**。详见 `orb-terminal-final-comparison.json`。

## 完整样本与新停点

| 职业 | 本批原生结果 | 确认/派发动作 | 精确差分步 | 在线墙钟 | 搜索墙钟 |
|---|---|---:|---:|---:|---:|
| Defect | Completed / Death | 236 / 236 | 202 / 202 | 159.50 s | 100.51 s |
| Silent | Completed / Death | 323 / 323 | 289 / 289 | 152.60 s | 95.11 s |
| Regent | InvalidAction，第二幕 | 239 / 240 | 200 / 200 已执行步 | 144.40 s | 83.85 s |
| Necrobinder | DataError，第一幕 | 88 / 88 | 76 / 77 | 49.91 s | 22.08 s |

Death 是完整合法终局，不是通关。完整性有效也不表示策略强。

新缺口保留原始异常、已发生的前缀和退出码，没有改写为成功：

- Regent q201 / a240：PhotonCut 原生先产生效果，再请求从 Hand `MoveToDrawTop`；桥目前只接受该效果的 FromCombatPile。真实新需求包括抽牌之后的选择时点身份，不能仅删除 leaf 检查。下一批应复用现有来源牌堆/token 与原生选择桥，加入选择时点快照、置顶结果和顺序校验。
- Necrobinder q77 / a88：EndTurn 的 Doom 致胜边界，玩家 `VulnerablePower` 预测为 1、原生为 2，其余 continuation 部件及 RNG 相同，父分支未污染。待核实原生 BeforeSideTurnEnd/终态中断顺序并修通用生命周期，不能对 Mawler、Doom 或易伤数值硬编码。虽有原生胜利回调，该样本整体仍是 DataError，不能作为通过的战斗或完整样本。

`--review-full-run` 允许核查失败前缀，返回 0 不表示 Completed：Regent 前缀完整性通过且仍保留 `partialRoot=a240` / InvalidAction / 原退出 1；Necrobinder 的真实差分被生产检查器拒绝。外层汇总独立核对结果分类、原退出、完整性和终局。

## 同构建重放

- Defect：236 动作、202 差分步、1,420 项比较和 3,092 个完整状态/RNG 观察均一致。
- Silent：323 动作、289 差分步、2,029 项比较和 4,167 个完整状态/RNG 观察均一致。
- 重放仍调用求解器，并强制核对冻结动作、选择及边界；不能用在线耗时减重放耗时估算搜索开销。

## 玩家可见策略：首次实际原生闭环

复用已有纯 `VisibleCombatPolicy`，增加 `visible-battle` / `visible-battle-replay` 内部场景。调用链为：当前原生合法动作 → 白名单 DTO 序列化 → 只接收字符串的纯策略 → 公开手牌/敌人槽位 token → 新鲜原生合法性和实例绑定 → 实际执行。

- 原版 Ironclad 自然首战，16 个策略战斗动作、19 个宿主根动作，真实 BattleVictory；**0 Solver query**。
- 同构建重放 143 个完整状态/RNG 观察及 33 份策略/执行文件一致。
- 每个决策点执行隐藏 RNG、抽牌堆顺序、私有身份、未来/seed 字段替换对照和公开 HP 变化对照；拒绝额外信息字段，核验观察不改变原状态/RNG。
- 10 项生产派发正负控制通过，覆盖动作归属、实际卡实例/目标、前后完整状态、非法 token、隐藏字段和纯度声明。
- 诊断日志保留 Oracle 标签；策略输入标 PlayerVisible。没有把整份宿主日志交给策略。

**范围限制：**这是受信任代码的纯数据调用边界，不是任意插件的 OS 安全沙箱；隐藏替换测试保持同一公开合法动作向量，没有重建不同隐藏原生世界。DTO 只有公共战斗子集，尚无完整职业资源、意图、费用视图，也没有完整 Run 的玩家可见策略。不能据此宣称全职业/全局公平策略已经验收，更不能作为真人水平基线。

## 性能记录及解释

保留上一批 80 次分析搜索及其实际计时、分配、预测质量和确定性数据，没有以修复后样本替换它们。本批不再调搜索评分或档位。

本批实际观察到 DOP 上限 2，出现并发大于 1 的查询分别为 Regent 201/201、Necrobinder 76/77、Defect 201/202、Silent 287/289。完整的 waves、work items、成员展开、耗时、WorkerAllocatedBytes 与 GC 数据保存在 `measurements.json` 及原 query 文件。

例如 Defect 的 SmartPotionGradient 成员归因约 56.26 s、8.44 GiB 累积分配，PrimaryBeam 约 25.72 s、3.68 GiB。累积分配不是峰值占用，成员归因也不是独占进程 CPU/GC。各测量 scope 会嵌套，不能求和。

需要重视测量波动：Silent 本批与上批都是 93,806 展开、相同实际轨迹，但在线墙钟从约 74.50 s 到 152.60 s；本批重放又为 188.82 s。没有足够证据把变化归因于代码修复、并行设置或某种搜索效果。后续性能比较应使用同构建、交错顺序、重复不可变根及环境记录；暂不据此扩成 100 局或宣称可靠提速/胜率。

## 检查、预算与保留

- 预登记：最多 16 次原生执行，45 分钟首末运行窗口，4 GiB 证据；短进程 120 s，完整样本 465 s。
- 实际 11 次注册启动尝试（其中 1 次预检未启动 native），10 次原生进程；另保留 1 次运行前输入准备失败。首末运行窗口 1,421.22 s（23.69 分钟），证据约 1.82 GiB。21,105 项保留性哈希核查零差异，封存 7,896 份原生文件；15 个 Mod 输入及 52 个兼容源码保持不变。全部注册进程已退出。
- 核心纯逻辑检查重建后通过 98 项；其 Core DLL 哈希与当前原生构建一致。构建显示 NU1900 漏洞数据服务警告，但缓存依赖构建成功；没有关闭审计或改连接设置。
- 原始 native 输出、失败、未启动的输入目录及全部控制均保留。详见 `closeout.json`、`preservation-audit.json`、`native-evidence-seal.json`。
- 首个 Defect 诊断运行期间编辑了新观察代码，触发运行后源码一致性门；该证据不能当当前源码认证。随后使用独立冻结构建重做，未覆盖原证据。一次 Regent 预检未启动 native。
- 首次 visible 启动误传 solver plan，零派发拒绝；一次 visible replay 输入准备发现本场景本就没有奖励 transcript，保留半成品，新目录仅给不使用的奖励输入空数组，真实 route/choices 仍必需。
- 第一组 11 个控制误复制整份 trace，约占 0.97 GB，保留未删；后续控制只提取对应动作记录，同一生产检查函数仍被实际调用。
- 初版汇总错误期待所有失败前缀检查都必须 exit 1，现已分开“前缀完整性”和“运行通过”；旧断言失败记录保留，生产实现未因此放宽。

## 复现入口与下一步

- 选择/玩家可见的生产控制：`tools/performance/compat_controls.py`，总计 31 项。
- 原始证据审查：`tools/performance/review_compat.py`；运行分类不由检查器退出码替代。
- 汇总/封存：`tools/performance/summarize_compat.py`、`closeout_compat.py`。
- 原生运行：`compat_runner.py` 和 `run-native-slice.ps1`。重跑须登记新批次、使用新输出目录，并满足源码/输入/产物哈希门；禁止覆盖现有输出。

下一有界工作优先处理上述两处新兼容缺口，再补职业终局样本；玩家可见策略扩展应复用现有 Core observation/policy 端口，先完整界定可见字段，再做真实隐藏状态隔离和完整 Run。性能调优等待可解释、可复现的同构建对照；不再围绕单场 Boss 微调。

未提交或推送。本批不改变 M1 已验收/已推送结论，也不宣称新里程碑完成。
