# M2 角色 Mod 全流程阶段启动提示词

本文件对应 [M2 阶段计划](../mods/MOD_FULL_RUN_EXECUTION_PLAN.md)。每个阶段从新上下文开始，读取上一阶段报告，完成当前范围后停止。M2-3、M2-4、M2-5 的提示词每次只执行一个 Mod，完成一个 Mod 后再用同一模板启动下一个。

## 所有阶段共用的启动约束

把下面内容放在每个阶段提示词之前：

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

先读取并遵守：
- AGENTS.md
- GLOSSARY.md（如果存在）
- docs/agents/domain.md
- docs/mods/MOD_FULL_RUN_EXECUTION_PLAN.md
- 上一阶段报告（如果存在）
- 本提示词指定的阶段范围

开始工作前：
1. 读取当前分支、HEAD、git status 和当前快照摘要。
2. 保护已有未提交修改。不要 reset、clean、checkout、覆盖式复制或删除用户已有文件。
3. 确认固定快照 D:\STS2-Simulator-Lab\local-mods\workshop-snapshot-20261106-163352 和 manifest 存在。
4. 目标游戏固定为 0.111.0；兼容目录必须精确匹配，禁止静默回退。
5. 所有运行使用新的 .build/runs 子目录，并记录 seed、输入摘要、worker 身份和配置摘要。

共同边界：
- Workshop 快照和新建快照都是只读输入；不要修改、重打包、替换、反编译或覆盖 DLL、PCK、资源和 manifest。
- 不把实时 Workshop 目录、正式存档、正式设置、正式游戏数据、Steam Cloud 或 UI 当作运行时输入。
- Mod DLL 只在独立 worker/NativeRun 进程加载；核心模拟器不得引用 Mod、Godot 或外部 DTO。
- 不用空实现、静默跳过、原版替代行为或预测死亡冒充玩法兼容。
- 遇到 RuntimeFailure、Unsupported、预算、权限、输入哈希或环境阻塞时，保留证据并在安全点停止。
- 不在本阶段提交、推送、重写历史或创建远程 PR。

完成后写阶段交接报告并停止。报告必须包含：
- 阶段状态：PASS、PARTIAL 或 BLOCKED。
- 当前分支和 git status 摘要。
- 实际修改文件、关键未修改文件和固定输入摘要。
- 执行命令、退出码和证据目录。
- 验收标准逐项结果。
- primary/repeat/replay/failure 的终态、digest 和错误分类。
- 已知缺口、Unsupported/Unrun 分母和没有被覆盖的语义。
- 下一阶段唯一建议入口。
- 是否提交或推送：默认“否”。
```

## M2-0：目标、术语和主分母冻结

**前置**：当前基线 `d55a41e`。

**交付**：`docs/mods/stage-reports/M2_PHASE_00_SCOPE_FREEZE.md`，必要的 `GLOSSARY.md` 和 ADR 更新。

**启动提示词**：

```text
执行 M2-0，只做只读审计、术语冻结和范围决策，不改运行时代码。

读取固定快照的 21 个 manifest、最新的全量矩阵修复报告、Stage 6 至 Stage 10 报告和当前标准模型。

完成：
1. 按 manifest、affects_gameplay 和实际能力发现，把每个条目标为 role-bearing、dependency、functional、resource-only 或 excluded。
2. 冻结角色全流程主分母；明确 BaseLib、RitsuLib、MinionLib、MintySpire、intentgraph2、资源包和 Eleanor 的处理方式。
3. 定义 flow-compatible、实际 Victory/Death、稳定、repeat、replay、Unsupported 和随机 Workshop 样本。
4. 为每个主分母 Mod 记录候选角色、精确兼容变体、依赖闭包和第一个最小纵切片。
5. 规定每个 Mod 的 primary/repeat/replay/failure 最低证据和终态字段。
6. 将不可逆边界写入 GLOSSARY.md；需要长期遵守的范围决策写入 docs/adr/。

禁止：
- 加载第三方 Mod 入口点。
- 下载或读取实时 Workshop。
- 选择多个角色同时开工。
- 修改固定快照或源码。

通过标准：主分母可由下一 Agent 复核，固定输入和随机样本规则无歧义。完成报告后停止。
```

## M2-1：通用终态运行协议

**前置**：M2-0 PASS。

**交付**：可复用的 full-run coordinator、终态分类、repeat/replay 契约、原版控制证据和回归测试。

**启动提示词**：

```text
执行 M2-1，只建立通用终态运行协议，不接入新的角色 Mod。

在现有 NativeRun、NativeModLoop、run-manifest 和最高稳定 seam 上实现或补齐：
- 从合法角色/内容选择开始的房间、战斗、奖励和清理推进。
- 实际 RunVictory 和实际 Death 的终态识别。
- BudgetExceeded、Unsupported、RuntimeFailure、EnvironmentBlocked、EngineError、策略停止和进程崩溃的明确分类。
- primary/repeat/replay/failure 四路输入契约、完整状态/RNG/trace 对比和终态摘要。
- 不把预测死亡、战斗 Victory 或预算结束提升为 Run 终态。

先使用原版或已有稳定 fixture 做控制运行。不要在本阶段新增任何 Mod 类型、角色、卡牌或遗物适配。

验收：
1. 新进程控制运行可真实到达 Victory 或 Death。
2. 两次独立运行的边界、终态分类和 digest 一致。
3. replay 通过完整状态/RNG/trace 校验。
4. 预算、Unsupported 和失败控制各自保留原始原因。
5. Core 137 项、原版 slice、现有 Mod worker 和 Stage 6 至 Stage 10 回归通过。

完成报告后停止。
```

## M2-2：第一条角色全流程纵切片

**前置**：M2-1 PASS。

**交付**：一个冻结主分母角色的完整 primary/repeat/replay/failure 证据。

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
- 至少一个自定义角色或其他玩法身份进入真实生命周期。

禁止为了通过而跳过未知语义、强行替换原版角色、把 Unsupported 改成成功，或顺手接入第二个角色。

验收：终态不是 BudgetExceeded、Unsupported 或 RuntimeFailure；primary/repeat/replay 的边界和 digest 稳定；固定快照、正式数据和核心外部类型边界未被破坏。完成报告后停止。
```

## M2-3：直接角色阵容扩展

**前置**：M2-2 PASS。

**交付**：每次一个直接角色 Mod 的全流程证据。候选包括 Watcher、Aemeath、figure_Saya、Ryoshu，以及 M2-0 标为同类的条目。

**启动提示词**：

```text
执行 M2-3，只处理一个直接角色型 Mod：<填入本次 Mod 和 Workshop ID>。

沿用 M2-2 的 primary/repeat/replay/failure 契约，完成角色选择、初始状态、战斗、奖励、推进、清理和真实 Victory/Death。

先读取该 Mod 的能力矩阵，尤其检查：
- 自定义角色初始化和身份。
- 卡牌、遗物、Power 或 token 的实际选择路径。
- Watcher 的负 canonical-cost token Card（若适用）。
- 任何 Godot/UI/地图/保存依赖。

若发现缺口：优先在最高稳定 seam 补充标准模型和回归；无法安全建模时输出精确 Unsupported/RuntimeFailure，并保留失败输入和新进程证据。不能静默绕开触发点。

完成一个 Mod 后停止，不要并行接入其他角色。报告必须给出该 Mod 是否满足 M2 flow-compatible 门槛。
```

## M2-4：RitsuLib 角色阵容扩展

**前置**：M2-3 PASS；RitsuLib 精确变体已验证。

**交付**：每次一个 Remilia、LexNinja2 或 YukiMakoto 的全流程证据和能力矩阵。

**启动提示词**：

```text
执行 M2-4，只处理一个 RitsuLib 角色型 Mod：<填入本次 Mod 和 Workshop ID>。

强制使用精确的 RitsuLib compat/0.111.0 或角色 lib/0.111.0 变体。完成：
- 依赖顺序和加载身份审计。
- 自定义角色、卡牌、遗物、Power、Hook 和奖励的实际发现。
- 至少一个 RitsuLib 特有战斗语义的真实执行。
- full-run coordinator 推进到真实 Victory/Death。
- primary/repeat/replay/failure 四路证据和完整能力矩阵。

31 partial、114 unsupported 或其他历史分母只能作为输入事实，不能直接改为通过。每个未触发内容保留 Unrun；每个触发但未建模内容保留 Unsupported。

核心不得引用 RitsuLib 外部类型。完成一个角色后停止。
```

## M2-5：复杂角色和复合玩法扩展

**前置**：M2-4 PASS。

**交付**：每次一个 Kaguya、Downfall、St2bs、Acheron 或 Shadowverse/Minion 复合 Mod 的终态证据。

**启动提示词**：

```text
执行 M2-5，只处理一个复杂角色或复合玩法 Mod：<填入本次 Mod 和 Workshop ID>。

先从一个明确内容表面进入：
- 一个自定义敌人/遭遇；或
- 一个事件/奖励/资源账本；或
- 一个 Minion/组件卡/战斗清理路径。

确认该表面能在真实 run 中被合法触发，再接入 full-run coordinator。复杂第四幕、多 Boss、多角色、完整 UI/地图/保存/网络不是默认前置；但它们不能被静默跳过，必须进入能力矩阵。

Shadowverse 必须把现有 Stage 10 的真实 Minion/组件卡证据提升到 RunVictory/Death；只证明一场战斗不算本阶段通过。

验收：自定义内容身份实际见证，run 到达真实 Victory/Death，primary/repeat/replay/failure 稳定，所有未支持语义有证据。完成一个 Mod 后停止。
```

## M2-6：固定阵容全量终态矩阵

**前置**：M2-3、M2-4、M2-5 中主分母候选均已有单 Mod 结果。

**交付**：固定主分母全流程矩阵、回归报告和失败分母。

**启动提示词**：

```text
执行 M2-6，只做固定阵容的批量终态验证和报告，不新增适配语义。

读取 M2-0 的冻结主分母，逐个 Mod 串行执行：
- 两个新进程 primary/repeat。
- 一个 replay。
- 一个预算/失败隔离控制。

每个条目记录：快照摘要、Workshop ID、Mod/依赖版本、精确变体、seed、策略配置、custom identity、终态类型、digest、状态/RNG/trace 对比、run manifest、错误码和能力分母。

通过条件：主分母中的每个条目都有真实 RunVictory 或 Death，没有隐藏的 BudgetExceeded/Unsupported/RuntimeFailure；核心、原版和已通过阶段回归保持通过。

任何未通过条目都保留 PARTIAL 和下一最小工作单元，不要用批量摘要覆盖单 Mod 失败。完成矩阵报告后停止。
```

## M2-7：新 Workshop 角色准入试点

**前置**：M2-6 PASS，或已明确记录固定阵容的剩余 Partial 与是否允许进入试点。

**交付**：首批 3 个新角色型 Workshop Mod 的新快照、静态审计、隔离和 full-run 结果。

**启动提示词**：

```text
执行 M2-7，只处理首批新 Workshop 角色准入试点。

选择规则：
- 先固定游戏 0.111.0 和角色型/玩法型条件。
- 选择 3 个此前不在固定快照主分母中的角色 Mod，并在运行前记录选择理由。
- 为它们创建新的只读快照和 manifest；记录来源、版本、依赖、文件数、大小和 SHA-256。
- 不下载、修改或覆盖当前固定快照；如果合法输入不可用，立即 BLOCKED。

每个样本都走：manifest/PE 审计、精确依赖、worker 隔离、角色选择、full-run、primary/repeat/replay/failure 和能力矩阵。

“随机”不能省略版本和来源锁定；“立刻接入”不能省略语义证据。失败必须分类为 Unsupported/RuntimeFailure/其他稳定错误并保留原因。

首批 3 个样本全部达到真实 Victory/Death 才算试点 PASS；否则输出 PARTIAL 和逐样本下一步。完成后停止，不自动扩大到 5 个。
```

## M2-8：M2 验收和下一路线

**前置**：M2-7 完成。

**交付**：`docs/mods/stage-reports/M2_FINAL_ACCEPTANCE.md`、更新后的 docs map 和 README 入口。

**启动提示词**：

```text
执行 M2-8，只做独立复核、汇总和停点报告，不新增代码或接入新的 Mod。

汇总固定阵容和新 Workshop 样本的：
- role-bearing 主分母、dependency/resource/functional 排除分母。
- PASS、Partial、Unsupported、RuntimeFailure、Unrun 和环境阻塞分母。
- 每个通过条目的 primary/repeat/replay/failure 证据引用。
- Victory/Death 终态、digest、seed、依赖闭包、快照摘要和隔离字段。
- 仍未支持的卡牌、遗物、Power、敌人、事件、地图、UI、保存和网络语义。

重新运行 Core 137 项、ModWorker、Watcher、Ritsu、Kaguya、Minion Stage 9/10 和原版 slice 回归。报告必须明确 M2 是 PASS、PARTIAL 还是 BLOCKED，并给出下一唯一入口。

不要在本阶段进入性能优化、策略优化、完整内容覆盖或公共 Mod SDK。默认不提交、不推送。
```

