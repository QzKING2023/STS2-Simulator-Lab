# M2 角色 Mod 全流程阶段启动提示词

本文件对应 [M2 阶段计划](../mods/MOD_FULL_RUN_EXECUTION_PLAN.md)。`/to-tickets` 已完成，当前 GitHub 票据图是执行入口：父票 #1，子票 #2–#21。每个阶段从新上下文开始，读取上一阶段报告，完成当前票据范围后停止。M2-3、M2-4、M2-5 和 M2-7 样本执行的提示词每次只执行一个子票，完成一个子票后再启动下一个。

## 所有阶段共用的启动约束

把下面内容放在每个阶段提示词之前：

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

先读取并遵守：
- AGENTS.md
- GLOSSARY.md（如果存在）
- docs/agents/domain.md
- docs/agents/issue-tracker.md
- docs/agents/triage-labels.md
- docs/adr/README.md（如果存在）及其涉及 M2 的 ADR
- docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md
- 上一阶段报告（如果存在）
- 本提示词指定的阶段范围

开始工作前：
1. 根据下方 ticket map 选定一个具体子票；用 `gh issue view <number> --json number,title,body,labels,state` 读取它，确认票据仍为 open、标签为 `ready-for-agent`，并确认所有 `Blocked by` 票据已经关闭。父票 #1 只作为协议背景，不替代子票范围。
2. 读取当前分支、HEAD、git status 和当前快照摘要。
3. 保护已有未提交修改。不要 reset、clean、checkout、覆盖式复制或删除用户已有文件。
4. 确认固定快照 D:\STS2-Simulator-Lab\local-mods\workshop-snapshot-20261106-163352 和 manifest 存在。
5. 目标游戏固定为 0.111.0；兼容目录必须精确匹配，禁止静默回退。
6. 所有运行使用新的 .build/runs 子目录，并记录 canonical input：seed、快照摘要、游戏/Mod/依赖版本、worker 身份、配置、适配器和采集器摘要。

票据纪律：这些票据已经经过 `/to-tickets` 拆分，不要再次运行 `/triage`、创建重复票据或把多个未关联子票合并执行。若票据仍被阻塞、缺少合法输入或无法满足其边界，保留原始证据并报告 `BLOCKED`，不要绕过阻塞边或扩大范围。确认范围后再按仓库约定认领票据；完成后在对应票据评论中留下证据入口并关闭该子票，父票和后继票据只在其阻塞关系允许时更新。

在首次使用 `gh` 或 GitHub-backed `git` 命令前，执行 `. .\tools\maintenance\Set-GitHubSession.ps1`；遇到验证错误时先执行 `. .\tools\maintenance\Set-GitHubSession.ps1 -Verify`，不要直接重复登录或创建新 token。

共同边界：
- Workshop 快照和新建快照都是只读输入；不要修改、重打包、替换、反编译或覆盖 DLL、PCK、资源和 manifest。
- 不把实时 Workshop 目录、正式存档、正式设置、正式游戏数据、Steam Cloud 或 UI 当作运行时输入。
- Mod DLL 只在独立 worker/NativeRun 进程加载；核心模拟器不得引用 Mod、Godot 或外部 DTO。
- 不用空实现、静默跳过、原版替代行为或预测死亡冒充玩法兼容；未知语义必须停在 Unsupported 或 RuntimeFailure。
- 不修改 Mod 原有代码，不重写二进制，不运行时打补丁，不跳过回调，不注入结果，不用假 DTO 冒充真实生命周期。
- BaseLib、RitsuLib、MinionLib 的共享玩法语义必须先通过 foundation gate；共享基础不足时不能用单 Mod 特判绕过。
- 只有跨 Mod/内容族可复用且有独立 fixture/回归的泛用接口才可作为外部适配；禁止包含 Mod 名称、Workshop ID 或单包分支的 adapter。
- 逐个建立 content unit、registration identity、lifecycle matrix 和 interaction risk table。可达内容必须在真实完整流程中观察；不可达内容才可用真实注册、真实回调和真实清理的 probe。
- 受控 probe 可使用 Oracle/原生内部状态证明 execution compatibility，但不能证明 visible-policy compatibility；UI、保存、地图、网络、Steam/云端单独记录为 platform capability。
- 遇到 RuntimeFailure、Unsupported、预算、权限、输入哈希或环境阻塞时，保留证据并在安全点停止。
- 不在本阶段提交、推送、重写历史或创建远程 PR。

完成后写阶段交接报告并停止。报告必须包含：
- 阶段状态：PASS、PARTIAL 或 BLOCKED。
- 当前分支和 git status 摘要。
- 实际修改文件、关键未修改文件和固定输入摘要。
- 执行命令、退出码和证据目录。
- 验收标准逐项结果。
- primary/repeat/replay/failure 的终态、digest 和错误分类。
- foundation gate 状态、content unit/lifecycle/interaction 矩阵、内容单元状态和平台能力缺口。
- 已知缺口、Partial/Unsupported/Unrun/RuntimeFailure 分母和没有被覆盖的语义。
- 下一阶段唯一建议入口。
- 是否提交或推送：默认“否”。
```

## `/to-tickets` 后的 GitHub ticket map

| 阶段 | 执行票据 | 票据范围 |
| --- | --- | --- |
| M2-0 | #2 | 冻结范围、foundation 语义和证据契约 |
| M2-1 | #3 | 通用 full-run coordinator 和终态协议 |
| M2-2 | #4 | 第一条 role-bearing Mod 纵切片 |
| M2-3 | #5 / #6 / #7 / #8 | Watcher / Aemeath / figure_Saya / Ryoshu，各一票一 Mod |
| M2-4 | #9 / #10 | Remilia / LexNinja2，各一票一 Mod |
| M2-5 | #11 / #12 / #13 / #14 / #15 | Kaguya / Downfall / St2bs / Acheron / Shadowverse-Minion，各一票一 Mod |
| M2-6 | #16 | 固定阵容双门矩阵 |
| M2-7A | #17 | 选择并冻结三个新 Workshop 样本 |
| M2-7B | #18 / #19 / #20 | 样本 A / B / C，各一票一实例 |
| M2-8 | #21 | M2 验收报告和下一路线 |

票据正文中的验收条件和 `Blocked by` 是当前执行契约；本文件只提供共用边界和可复用提示词。

## M2-0：目标、术语和主分母冻结

**票据**：#2。
**前置**：当前基线 `d55a41e`。

**交付**：`docs/mods/stage-reports/M2_PHASE_00_SCOPE_FREEZE.md`，必要的 `GLOSSARY.md` 和 ADR 更新。

**启动提示词**：

```text
执行 M2-0，只做只读审计、术语冻结、基础门和范围决策，不改运行时代码。

读取固定快照的 21 个 manifest、最新的全量矩阵修复报告、Stage 6 至 Stage 10 报告和当前标准模型。

完成：
1. 按 manifest、affects_gameplay 和实际能力发现，把每个条目标为 role-bearing、dependency、functional、resource-only 或 excluded。
2. 冻结角色全流程主分母；明确 BaseLib、RitsuLib、MinionLib、MintySpire、intentgraph2、资源包和 Eleanor 的处理方式。
3. 为 BaseLib、RitsuLib、MinionLib 冻结导出玩法语义清单，覆盖注册、初始化、内容创建、事件/监听、生命周期、跨房间状态和清理；非玩法 API 单独列出。
4. 为每个主分母 Mod 建立 content unit 和 registration identity 清单；记录候选角色、精确兼容变体、依赖闭包和第一个最小纵切片。
5. 为每个内容类型建立 lifecycle matrix；为每个声明或观察到的交互建立 interaction risk table，至少包含正常和边界/失败场景。
6. 定义 flow gate、content gate、foundation gate、primary/repeat/replay/failure、Covered/Unrun/Unsupported/RuntimeFailure、Partial/BLOCKED 以及随机 Workshop 样本。
7. 冻结 canonical input、追加发现、probe、visible-policy 和平台能力边界。
8. 将不可逆边界写入 GLOSSARY.md；需要长期遵守的范围决策写入 docs/adr/。

禁止：
- 加载第三方 Mod 入口点。
- 下载或读取实时 Workshop。
- 选择多个角色同时开工。
- 修改固定快照或源码。

通过标准：主分母、foundation 语义、每个内容单元、生命周期/交互矩阵、固定输入、状态聚合和随机样本规则可由下一 Agent 复核；不改源码。完成报告后停止。
```

## M2-1：通用终态运行协议

**票据**：#3。
**前置**：M2-0 PASS，且 #2 已关闭。

**交付**：可复用的 full-run coordinator、终态分类、repeat/replay 契约、可关联的生命周期/输入证据、原版控制证据和回归测试。

**启动提示词**：

```text
执行 M2-1，只建立通用终态运行协议，不接入新的角色 Mod。

在现有 NativeRun、NativeModLoop、run-manifest 和最高稳定 seam 上实现或补齐：
- 从合法角色/内容选择开始的房间、战斗、奖励和清理推进。
- 实际 RunVictory 和实际 Death 的终态识别。
- BudgetExceeded、Unsupported、RuntimeFailure、EnvironmentBlocked、EngineError、策略停止和进程崩溃的明确分类。
- primary/repeat/replay/failure 四路输入契约、完整状态/RNG/trace 对比和终态摘要。
- canonical input、run-manifest、生命周期边界和内容证据关联字段。
- 不把预测死亡、战斗 Victory 或预算结束提升为 Run 终态。

先使用原版或已有稳定 fixture 做控制运行。不要在本阶段新增任何 Mod 类型、角色、卡牌或遗物适配。

验收：
1. 新进程控制运行可真实到达 Victory 或 Death。
2. 两次独立运行的边界、终态分类和 digest 一致。
3. replay 通过完整状态/RNG/trace 校验。
4. 预算、Unsupported 和失败控制各自保留原始原因。
5. 生命周期、输入摘要和 run-manifest 可关联到控制运行。
6. Core 137 项、原版 slice、现有 Mod worker 和 Stage 6 至 Stage 10 回归通过。

完成报告后停止。
```

## M2-2：第一条角色全流程纵切片

**票据**：#4。
**前置**：M2-1 PASS，且 #3 已关闭。

**交付**：一个冻结主分母角色的 flow/content 双门、完整 primary/repeat/replay/failure 证据。

**启动提示词**：

```text
执行 M2-2，只选择一个角色型 Mod 做完整全流程纵切片。

默认候选是 YukiMakoto 的精确 lib/0.111.0 变体；如果静态审计证明其初始化依赖尚未闭合，记录理由并改选 Aemeath。不得无报告地换角色。

必须完成：
- 精确依赖闭包和 Mod 选择。
- 自定义角色选择和初始卡组/遗物观察。
- 合法战斗动作、奖励、房间/地图推进和清理。
- 真实 RunVictory 或真实 Death。
- 两个新进程的 primary/repeat，一个 replay，以及预算/失败隔离控制。
- 所有冻结 content unit 的适用 lifecycle checkpoint 和关键 interaction contract；可达内容必须在真实 run 中观察，不可达内容才可使用真实回调 probe。
- 至少一个自定义角色或其他玩法身份进入真实生命周期。

禁止为了通过而跳过未知语义、强行替换原版角色、把 Unsupported 改成成功，或顺手接入第二个角色。

验收：flow gate 和 content gate 均通过；终态不是 BudgetExceeded、Unsupported 或 RuntimeFailure；primary/repeat/replay 的边界和 digest 稳定；固定快照、正式数据和核心外部类型边界未被破坏。完成报告后停止。
```

## M2-3：直接角色阵容扩展

**票据**：#5、#6、#7、#8（每次只执行一个）。
**前置**：M2-2 PASS，且 #4 已关闭。

**交付**：每次一个直接角色 Mod 的 flow/content 双门证据。候选包括 Watcher、Aemeath、figure_Saya、Ryoshu，以及 M2-0 标为同类的条目。

**启动提示词**：

```text
执行 M2-3，只处理一个直接角色型 Mod：<填入本次 Mod 和 Workshop ID>。

沿用 M2-2 的 primary/repeat/replay/failure 契约，完成角色选择、初始状态、战斗、奖励、推进、清理和真实 Victory/Death；逐项完成该 Mod 的 content unit lifecycle matrix 和 interaction risk table。

先读取该 Mod 的能力矩阵，尤其检查：
- 自定义角色初始化和身份。
- 卡牌、遗物、Power 或 token 的实际选择路径。
- Watcher 的负 canonical-cost token Card（若适用）。
- 任何 Godot/UI/地图/保存依赖。

若发现缺口：优先在最高稳定 seam 补充标准模型和回归；无法安全建模时输出精确 Unsupported/RuntimeFailure，并保留失败输入和新进程证据。不能静默绕开触发点。

完成一个 Mod 后停止，不要并行接入其他角色。报告必须分别给出该 Mod 的 foundation、flow gate 和 content gate 状态；不能把静态发现或 DLL 加载成功提升为内容通过。
```

## M2-4：RitsuLib 角色阵容扩展

**票据**：#9、#10（每次只执行一个）。
**前置**：M2-3 的 #5–#8 已完成且 foundation gate 仍通过；RitsuLib 精确变体已验证。单个 Mod 的 Partial/Unsupported/RuntimeFailure 不自动阻塞下一个 Mod，但必须在交接报告中保留。

**交付**：每次一个 Remilia 或 LexNinja2 的 flow/content 双门证据和能力矩阵。YukiMakoto 已由 M2-2 的 #4 覆盖，不在本阶段重复开工。

**启动提示词**：

```text
执行 M2-4，只处理一个 RitsuLib 角色型 Mod：<填入本次 Mod 和 Workshop ID>。

强制使用精确的 RitsuLib compat/0.111.0 或角色 lib/0.111.0 变体。完成：
- 依赖顺序和加载身份审计。
- 自定义角色、卡牌、遗物、Power、Hook 和奖励的实际发现。
- 至少一个 RitsuLib 特有战斗语义的真实执行。
- full-run coordinator 推进到真实 Victory/Death。
- primary/repeat/replay/failure 四路证据和完整能力矩阵。
- 所有冻结 content unit 的生命周期检查点和关键交互证据；未触发内容只能通过真实 probe 或保留 Unrun/Unsupported。

31 partial、114 unsupported 或其他历史分母只能作为输入事实，不能直接改为通过。每个未触发内容必须先判断是否可由真实注册、真实回调和真实清理的 probe 补齐；probe 未完成时保留 Unrun，每个触发但未建模内容保留 Unsupported。

核心不得引用 RitsuLib 外部类型。完成一个角色后停止。
```

## M2-5：复杂角色和复合玩法扩展

**票据**：#11、#12、#13、#14、#15（每次只执行一个）。
**前置**：M2-4 的 #9、#10 已完成且 foundation gate 仍通过。单个 Mod 的 Partial/Unsupported/RuntimeFailure 不自动阻塞下一个 Mod，但必须在交接报告中保留。

**交付**：每次一个 Kaguya、Downfall、St2bs、Acheron 或 Shadowverse/Minion 复合 Mod 的 flow/content 双门证据。

**启动提示词**：

```text
执行 M2-5，只处理一个复杂角色或复合玩法 Mod：<填入本次 Mod 和 Workshop ID>。

先从一个明确内容表面进入并建立对应 lifecycle/interaction 证据：
- 一个自定义敌人/遭遇；或
- 一个事件/奖励/资源账本；或
- 一个 Minion/组件卡/战斗清理路径。

确认该表面能在真实 run 中被合法触发，再接入 full-run coordinator。复杂第四幕、多 Boss、多角色、完整 UI/地图/保存/网络不是默认前置；但它们不能被静默跳过，必须进入能力矩阵。

Shadowverse 必须把现有 Stage 10 的真实 Minion/组件卡证据提升到 RunVictory/Death；只证明一场战斗不算本阶段通过。

验收：自定义内容身份实际见证，所有冻结 content unit 达到 `Covered` 或有明确状态，run 到达真实 Victory/Death，primary/repeat/replay/failure 稳定，所有未支持语义有证据。完成一个 Mod 后停止。
```

## M2-6：固定阵容全量终态矩阵

**票据**：#16。
**前置**：M2-3 的 #5–#8、M2-4 的 #9–#10、M2-5 的 #11–#15 均已有单 Mod 结果。

**交付**：固定主分母 flow/content 双门矩阵、回归报告和失败分母。

**启动提示词**：

```text
执行 M2-6，只做固定阵容的批量终态验证和报告，不新增适配语义。

读取 M2-0 的冻结主分母，逐个 Mod 串行执行：
- 两个新进程 primary/repeat。
- 一个 replay。
- 一个预算/失败隔离控制。

每个条目记录：快照摘要、Workshop ID、Mod/依赖版本、精确变体、canonical input、seed、策略配置、custom identity、flow/content/foundation 状态、终态类型、digest、状态/RNG/trace 对比、run manifest、content unit/lifecycle/interaction 证据、错误码和能力分母。

通过条件：主分母中的每个条目同时通过 flow gate 和 content gate，没有隐藏的 BudgetExceeded/Unsupported/RuntimeFailure/Unrun；foundation、核心、原版和已通过阶段回归保持通过。任何未通过条目都使 M2-6 至少为 PARTIAL。

任何未通过条目都保留 PARTIAL 和下一最小工作单元，不要用批量摘要覆盖单 Mod 失败。完成矩阵报告后停止。
```

## M2-7：新 Workshop 角色准入试点

**票据**：#17（选择/冻结）以及 #18、#19、#20（样本执行，每次只执行一个）。
**前置**：#16 总体 PASS；固定阵容存在任何 Partial、Unsupported、RuntimeFailure 或 Unrun 时不得进入试点。

**交付**：首批 3 个新角色型 Workshop Mod 的新快照、foundation 依赖审计、静态审计、隔离和 flow/content 双门结果。

**启动提示词**：

```text
执行 M2-7A（票据 #17），只选择并冻结首批三个新 Workshop 角色样本，不执行样本玩法接入。

选择规则：
- 先固定游戏 0.111.0 和角色型/玩法型条件。
- 选择 3 个此前不在固定快照主分母中的角色 Mod，并在运行前记录选择理由。
- 为它们创建新的只读快照和 manifest；记录来源、版本、依赖、文件数、大小和 SHA-256。
- 不下载、修改或覆盖当前固定快照；如果合法输入不可用，立即 BLOCKED。

只完成来源、版本、依赖、快照、manifest、文件摘要、精确游戏版本判断和 canonical input 登记。“随机”不能省略版本和来源锁定；合法输入不可用时分类为 BLOCKED。完成 #17 后停止，分别启动 #18、#19、#20；不得在本票据中运行样本或自动扩大到 5 个。
```

### M2-7B：单个 Workshop 样本执行（#18 / #19 / #20）

使用以下提示词分别执行样本 A、B、C；把 `<sample-ticket>` 替换为对应的 #18、#19 或 #20，把 `<sample-id>` 替换为 #17 冻结的样本身份：

```text
执行 M2-7B（票据 <sample-ticket>），只处理冻结的 Workshop 样本 <sample-id>。

读取 #17 的快照、manifest、canonical input 和选择理由，并确认本票据的所有前置已关闭。完成 manifest/PE 审计、精确依赖和 foundation gate、worker 隔离、角色选择、full-run、content unit/lifecycle/interaction 矩阵、primary/repeat/replay/failure 和能力矩阵。

真实 RunVictory/Death、稳定 Unsupported、RuntimeFailure 或 BLOCKED 都必须保留原始原因和新进程证据；不得修改或覆盖固定快照，不得把加载成功或单场战斗冒充 flow/content 通过。完成一个样本后停止，不自动执行另外两个样本。
```

## M2-8：M2 验收和下一路线

**票据**：#21。
**前置**：#17、#18、#19、#20 均完成。

**交付**：`docs/mods/stage-reports/M2_FINAL_ACCEPTANCE.md`、更新后的 docs map 和 README 入口。

**启动提示词**：

```text
执行 M2-8，只做独立复核、汇总和停点报告，不新增代码或接入新的 Mod。

汇总固定阵容和新 Workshop 样本的：
- role-bearing 主分母、dependency/resource/functional 排除分母。
- foundation、flow、content、PASS、Partial、Unsupported、RuntimeFailure、Unrun 和环境阻塞分母。
- 每个通过条目的 primary/repeat/replay/failure 证据引用。
- Victory/Death 终态、digest、canonical input、seed、依赖闭包、快照摘要和隔离字段。
- 每个 content unit 的生命周期和交互矩阵、probe/重复证据及状态。
- 仍未支持的卡牌、遗物、Power、敌人、事件，以及单独列出的地图、UI、保存和网络平台能力。

重新运行 Core 137 项、ModWorker、Watcher、Ritsu、Kaguya、Minion Stage 9/10 和原版 slice 回归。报告必须明确 M2 是 PASS、PARTIAL 还是 BLOCKED，并给出下一唯一入口。

只有在固定阵容和首批样本的双门验收、foundation 回归和独立复核全部完成后，才给出 M2 总体 PASS；否则必须给出 PARTIAL 或 BLOCKED。不要在本阶段进入性能优化、策略优化或公共 Mod SDK。默认不提交、不推送。
```

