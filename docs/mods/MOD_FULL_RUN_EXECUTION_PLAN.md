# M2：角色 Mod 全流程终态兼容阶段计划

## 目标

M1 已经证明原版 Ironclad/A0 在固定游戏版本、固定输入和隔离 NativeRun 中可以完成真实 RunVictory 或 Death。前一轮 Mod 适配又建立了固定 Workshop 快照、依赖解析、只读 worker、标准模型边界，以及 Watcher、RitsuLib、Kaguya、MinionLib/Shadowverse 的窄切片证据。

M2 把下一步目标收敛为两个可验收的层次：

1. 固定快照中所有角色型或玩法内容型 Mod，都能在真实原生流程中从角色/内容选择走到真实 `RunVictory` 或真实 `Death`。
2. 从创意工坊新选一小批角色型 Mod，在固定输入、依赖和版本后，通过同一套通用准入门槛完成全流程推演。

这里的“兼容”是 **flow-compatible**：一次合法、可审计的有限策略推演能够到达游戏终态。它不等于所有卡牌、遗物、Power、敌人、事件、地图、UI、保存或网络语义都已经完整建模；那些内容仍必须在能力矩阵中逐项记录。

## 基线和固定输入

- 当前基线：`d55a41e`，已推送到 `origin/main`。
- 目标游戏：`0.111.0`。
- 固定 Workshop 快照：`local-mods/workshop-snapshot-20261106-163352`。
- 快照内容摘要：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 快照规模：21 个条目、142 个文件、`6,974,558,103` bytes。
- 最新静态 worker 修复结果：21/21 条目有独立 worker 和 run manifest；这只证明发现和隔离闭合，不证明玩法兼容。
- Stage 10 已有 Shadowverse/Minion 真实战斗纵切片，但尚未证明完整 Run 终态。

固定快照只读。任何新创意工坊样本都必须生成新的快照目录和 manifest，不能覆盖当前快照，也不能把实时 Workshop 目录当运行时输入。

## 范围分类

### 固定阵容主分母

当前快照中发现下列角色或玩法内容型候选，后续逐个做全流程闭环：

- Watcher、Aemeath、Downfall、figure_Saya、Remilia、Ryoshu、LexNinja2、Kaguya、Acheron、St2bs、YukiMakoto。
- Shadowverse Beyond Mod 作为 MinionLib/RitsuLib 复合玩法包纳入；MinionLib 本身仍是依赖和内容基础设施。

最终分母以阶段 0 从 manifest、`affects_gameplay` 和实际能力发现冻结的清单为准。静态表面不含角色或玩法内容的资源、QoL 和外观包不进入角色全流程分母。

### 依赖和排除项

- BaseLib、RitsuLib、MinionLib：作为依赖参与加载和闭包验证，不单独作为角色全流程条目。
- MintySpire2、QuickRestart、intentgraph2：按功能性 Mod 记录加载/隔离或明确 Unsupported，不纳入角色全流程主分母。
- Necrobinder 资源包、LieRenTVmod、ATA_IronClad 外观替换：按非玩法资源表面验收。
- Eleanor：保留为独立冻结回归基线，不混入当前 21 条目的主分母。

## 术语和通过条件

### 角色型 Mod

提供自定义角色、卡牌、遗物、Power、敌人、事件、奖励、Minion 或其他会改变 Run/Combat 生命周期的 Mod。判断先来自 manifest 和静态发现，再由真实 probe 校正；不能只按 Mod 名称猜测。

### 全流程终态

从固定 seed 和固定策略入口开始，经过合法的角色/内容选择、初始状态、战斗、奖励、房间/地图推进和清理，最终观察到：

- 原生 `RunVictory`；或
- 原生真实 `Death`。

`BudgetExceeded`、`Unsupported`、`RuntimeFailure`、`EnvironmentBlocked`、`EngineError`、策略停止或进程崩溃都不是终态。

### 稳定

每个已接入 Mod 至少产生：

1. 一个新进程 primary run；
2. 第二个新进程独立重复；
3. 一个基于第一条路线的 replay。

三者使用同一快照摘要、游戏版本、Mod 版本、依赖闭包、seed 和配置。primary/repeat 的边界和终态分类必须一致，replay 必须通过完整状态/RNG/trace 校验；允许结果是 Victory 或 Death，但不能在不同进程间无解释地改变为预算或 Unsupported 停止。

### 自定义内容见证

角色型 Mod 至少要在运行证据中观察到它声明的一个自定义身份。若包提供角色，必须实际选择该角色；若同时提供卡牌、遗物、Power、敌人、事件或 Minion，至少选择一个进入真实生命周期，或在报告中说明该内容没有被本次固定路线触发。不能用“DLL 加载成功”替代玩法见证。

## 共同工程边界

- Mod DLL 只在独立 worker/NativeRun 进程加载；`SimulatorLab.Core` 不引用 Mod、Godot 或外部 DTO。
- 使用固定快照、精确 `0.111.0` 兼容变体和新建 `.build/runs` 输出目录。
- 不访问实时 Workshop、正式存档、正式设置、正式游戏数据、Steam Cloud 或游戏 UI。
- 不修改、重打包、复制或覆盖快照内 DLL/PCK/资源。
- 不静默跳过未知语义，不把空实现或原版替代行为记为兼容。
- 每阶段只推进一个 Mod 或一个明确内容表面；阶段完成后停止，保留失败证据。
- 运行报告必须区分静态发现、真实玩法事实、预测/策略动作和 Unsupported。
- 阶段 Agent 默认不提交、不推送、不重写历史；提交和推送由主会话在审阅后完成。

## 阶段总览和阻塞关系

| 阶段 | 名称 | 阻塞于 | 主要交付 |
| --- | --- | --- | --- |
| M2-0 | 目标、术语和主分母冻结 | 当前基线 | 角色型 Mod 清单、终态契约、ADR/Glossary 更新 |
| M2-1 | 通用终态运行协议 | M2-0 | 可复用的 full-run coordinator、终态分类、重复/replay 契约 |
| M2-2 | 第一条角色全流程纵切片 | M2-1 | 一个固定阵容角色的 primary/repeat/replay/failure 闭环 |
| M2-3 | 直接角色阵容扩展 | M2-2 | Watcher/Aemeath/figure_Saya/Ryoshu 等逐个闭环 |
| M2-4 | RitsuLib 角色阵容扩展 | M2-3 | Remilia/LexNinja2/YukiMakoto 逐个闭环 |
| M2-5 | 复杂角色和复合玩法扩展 | M2-4 | Kaguya/Downfall/St2bs/Acheron/Shadowverse 的终态闭环 |
| M2-6 | 固定阵容全量终态矩阵 | M2-3、M2-4、M2-5 | 所有冻结主分母的全流程矩阵和回归 |
| M2-7 | 新 Workshop 角色准入试点 | M2-6 | 首批 3 个新角色 Mod 的快照、审计和全流程结果 |
| M2-8 | M2 验收和下一路线 | M2-7 | 最终报告、失败/未支持分母、下一阶段唯一入口 |

M2-3 至 M2-5 仍然按单 Mod 串行执行。表中的“阵容扩展”不是一次性并行启动所有角色，而是同一个阶段提示词被每个候选重新执行一次；任何一个角色阻塞时先完成修复或停在明确状态，再进入下一个角色。

## 阶段说明

### M2-0：目标、术语和主分母冻结

只做只读审计和文档决策。读取当前 21 条目 manifest、最新修复报告和已有能力矩阵，确定哪些条目进入角色全流程主分母；把 `flow-compatible`、终态、稳定、重复、replay、Unsupported 和随机样本的含义写入 `GLOSSARY.md`，必要的不可逆边界写入 `docs/adr/`。

通过条件：清单中每个条目都有 `role-bearing`、`dependency`、`functional`、`resource-only` 或 `excluded` 分类；主分母、排除理由、固定快照摘要和随机样本规则都可由下一阶段复核；不改源码。

### M2-1：通用终态运行协议

在现有 NativeRun/NativeModLoop 的最高稳定 seam 上实现可复用的全流程 coordinator。它负责路由推进、合法动作分发、战斗/奖励/房间边界、实际 Victory/Death 识别、repeat/replay 对比和预算/Unsupported/RuntimeFailure 的明确停点。

先用原版或已有稳定 fixture 做控制，不新增角色适配。任何必须依赖某个 Mod 类型的逻辑都留在 worker/adapter 边界。

通过条件：控制运行可以在新进程中到达 Victory 或 Death；同输入重复和 replay 可验证；终态分类不会把预算、预测死亡或 Unsupported 当作游戏结束；核心 137 项及既有 native 回归保持通过。

### M2-2：第一条角色全流程纵切片

从冻结主分母中选一个最容易建立完整证据的角色。默认优先 `YukiMakoto` 的 `lib/0.111.0` 变体；若其运行时初始化需要尚未闭合的 RitsuLib seam，必须在报告中记录决策并改选 `Aemeath`，不能静默切换。

通过条件：角色实际选择、自定义身份见证、完整 run 终态、两次独立进程、一次 replay 和失败隔离全部闭合。完成后停止，不自动接入第二个角色。

### M2-3：直接角色阵容扩展

依次推进 Watcher、Aemeath、figure_Saya、Ryoshu 或阶段 0 认定的同类角色。Watcher 的负 canonical-cost token Card、角色初始状态、奖励和清理若阻塞终态，先建立标准模型或稳定 Unsupported 证据，再继续。

每次只选一个角色，沿用 M2-2 的 primary/repeat/replay/failure 证据模板；不把静态发现矩阵提升为玩法通过。

### M2-4：RitsuLib 角色阵容扩展

依次推进 Remilia、LexNinja2、YukiMakoto 中尚未闭合的角色。强制使用精确 `compat/0.111.0` 或 `lib/0.111.0` 变体，记录 RitsuLib hook、卡牌、遗物、Power 和奖励的能力矩阵。

通过条件：每个角色都能合法走到 Victory/Death；未触发的内容保留未运行状态；不能用原版角色替代自定义角色。

### M2-5：复杂角色和复合玩法扩展

依次处理 Kaguya、Downfall、St2bs、Acheron 和 Shadowverse/Minion。复杂包先从一个能闭合的自定义敌人、事件、奖励或 Minion 内容表面进入，再合入 full-run；不以完整第四幕、全部 Boss、完整 UI/地图/保存/网络为前置。

Shadowverse 必须把现有真实 Minion/组件卡证据提升到完整 run 终态，否则只能记为 Partial；超大 DLL 元数据读取、依赖闭包和运行时加载都必须继续保持可审计。

### M2-6：固定阵容全量终态矩阵

对 M2-0 冻结的所有角色型 Mod 逐个运行 primary、repeat、replay 和 failure control，生成矩阵、分母和索引。每个条目必须引用快照摘要、版本、seed、依赖闭包、终态分类、digest、run manifest 和能力报告。

通过条件：主分母中每个条目都有真实 Victory 或 Death 终态；没有隐藏的预算/Unsupported/RuntimeFailure 停止；固定快照和核心/native 回归保持通过。若任何条目未通过，M2-6 只能是 PARTIAL，不能宣称固定阵容完成。

### M2-7：新 Workshop 角色准入试点

在不改变当前快照的前提下，按 M2-0 的规则选择首批 3 个新角色型 Workshop Mod。每个样本必须先生成新快照、manifest、来源摘要和精确游戏版本判断，再走同一套静态审计、worker 隔离和 full-run 终态流程。

通过条件：3 个样本都能在无需修改固定输入的前提下完成真实 Victory/Death、重复和 replay；如果失败，必须输出稳定错误分类和缺口，不得把随机样本失败改写为“未选择”。首批结果通过后再把样本规模扩大到 5 个。

### M2-8：M2 验收和下一路线

只做汇总、独立复核和文档更新。报告固定阵容和随机样本的成功、Partial、Unsupported、RuntimeFailure、未运行分母，保留所有失败证据，更新 README/docs map，并给出下一唯一入口。

M2 不在此阶段进入性能调优、策略胜率优化、完整内容覆盖或公共 Mod SDK 设计；这些是后续路线。

## 共同验证矩阵

每个角色至少执行以下层次：

1. 核心协议检查：`dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`。
2. 受影响 worker/阶段项目的 Release build，禁用部署和 NuGet 审计网络依赖。
3. 新进程 primary/repeat/replay/failure 四路运行，使用新 `.build/runs` 目录。
4. `run-manifest`、输入摘要、完整状态/RNG/trace、终态事件和隔离字段审计。
5. 既有 Mod worker、Watcher、Ritsu、Kaguya、Minion Stage 9/10 以及原版 slice 回归。
6. 阶段完成前运行 `git diff --check`；提交和推送由主会话在审阅后处理。

## ask-matt 路由

这是一个多会话、存在运行时未知项的工作。执行路线为：

`grill-with-docs`（术语、ADR、边界）→ `to-spec`（M2 规范）→ `to-tickets`（阻塞关系）→ 每个 ticket 单独实现/TDD → `code-review` → `pr` → `retro`。

当前文档先作为执行计划和阶段提示词，不自动创建 GitHub Issues；当 M2-0 的主分母和终态契约冻结后，再把它们转换成可抓取 ticket。

