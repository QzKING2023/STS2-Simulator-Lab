# Mod 适配阶段启动提示词

这份文件把 `docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md` 拆成可以交给独立 Agent 的执行包。每个执行包都适合从新上下文开始。Agent 完成一个执行包后停止，由下一次新会话继续，不在同一会话中顺手进入后续阶段。

## 所有阶段共用的启动约束

把下面这段放在每个阶段提示词之前。

```text
你在 D:\STS2-Simulator-Lab 以独立 Agent 工作。

先读取并遵守：
- AGENTS.md
- GLOSSARY.md（如果存在）
- docs/agents/domain.md
- docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md
- 本提示词中指定的阶段范围

开始工作前必须：
1. 读取当前 git 分支和 git status。
2. 识别并保护已有未提交修改。不要 reset、clean、checkout 覆盖它们。
3. 确认固定快照存在于 D:\STS2-Simulator-Lab\local-mods\workshop-snapshot-20261106-163352。
4. 读取对应的 D:\STS2-Simulator-Lab\local-mods\workshop-snapshot-20261106-163352.manifest.json。
5. 将游戏目标固定为 0.111.0，兼容变体必须精确匹配。

工作边界：
- Workshop 快照是只读输入。不要修改、重打包、替换、反编译或覆盖其中任何 DLL、PCK、资源和 manifest。
- 不枚举或读取实时 Workshop 目录作为运行时输入。
- 不访问真实玩家存档、正式设置或正式游戏数据。
- 不把任意 Mod 类型、Godot 对象或外部 DTO 直接放进核心模拟器。
- 不修改 SnowLuma 或无关项目。
- 不删除文件，不重写历史，不提交，不推送，不创建远程 PR。
- 不以空实现、静默跳过或“加载成功”声称玩法兼容。
- 遇到路径、哈希、权限、数据格式、运行时语义或预算阻塞时，在安全点停止并报告精确阻塞。

每个阶段只完成本提示词的范围。完成后写交接报告并停止，不自行进入下一阶段。

交接报告必须包含：
- 阶段状态：PASS、BLOCKED 或 PARTIAL
- 当前分支和 git status 摘要
- 实际修改文件和未修改的关键文件
- 执行过的命令及结果
- 验收标准逐项结果
- 已知缺口和未支持语义
- 下一阶段唯一建议入口
- 是否提交或推送
```

## 阶段 0：快照完整性复核

**适合的 Agent**：只读校验 Agent。当前快照已由主会话建立，本阶段用于交接前复核。

**前置**：无。

**交付**：确认固定快照与清单一致，并输出 `docs/mods/stage-reports/00-snapshot-verification.md`。

**启动提示词**：

```text
执行阶段 0，只做固定快照的只读复核。

验证：
- 快照目录存在且包含 21 个 Workshop 条目。
- 清单记录 142 个文件和 6,974,558,103 bytes。
- 按相对路径、文件长度和 SHA-256 重算内容摘要。
- 快照目录中没有额外的 Mod 内容文件，也没有缺失文件。
- 复核过程不访问实时 Workshop 目录作为运行时输入，不改变快照。

完成标准是清单中的摘要和重算摘要相同。任何差异都标记 BLOCKED，并给出第一个差异路径。
只写复核报告，不修改代码。
```

## 阶段 1：Mod 输入审计和适配边界设计

**适合的 Agent**：只读审计和设计 Agent。

**前置**：阶段 0 PASS。

**交付**：`docs/mods/stage-reports/01-input-audit-and-boundary.md`。本阶段不改源码。

**启动提示词**：

```text
执行阶段 1，只读审计固定快照和现有 Simulator Lab。

完成以下工作：
1. 读取全部 21 个条目的 JSON manifest、兼容目录、变体清单和包布局。
2. 建立每个条目的来源身份、版本、最低游戏版本、依赖、玩法标志和静态内容类别。
3. 读取现有 NativeRun、build manifest、run manifest、日志、fixture 和 Eleanor 冻结证据的入口，标出可复用 seam。
4. 画出外部 Mod 输入到内部标准模型的边界。
5. 明确 ModSource、ModManifest、依赖图、CapabilityReport、错误记录和 worker 边界的最小字段。
6. 给出 Watcher 作为首个角色纵向切片的可行性判断，并列出需要运行时探针确认的假设。
7. 列出阶段 2 至阶段 6 的拟修改文件范围和测试矩阵。

输出必须区分静态 manifest 事实、运行时未知项和设计决定。不要反编译 DLL，不要执行玩法，不要修改代码。
完成后停止。
```

## 阶段 2：ModCatalog 和 manifest 解析

**适合的 Agent**：编码 Agent。

**前置**：阶段 1 PASS。

**交付**：最小可用的 ModCatalog、manifest 解析、稳定排序、输入哈希和解析测试。保留现有核心接口。

**启动提示词**：

```text
执行阶段 2，实现固定快照的 ModCatalog 和 manifest 解析。

范围：
- 只读取固定快照目录和快照清单。
- 支持 mod_manifest.json 和各 Mod 自有 JSON manifest 的实际形状。
- 规范化 ModSource、ModManifest、ModDependency 和来源文件摘要。
- 以相对路径、Workshop ID、Mod ID 的明确规则进行稳定排序。
- 生成稳定输入哈希和可审计解析报告。
- 对缺文件、无效 JSON、缺必填字段、非法版本、重复 Mod ID 和路径逃逸返回稳定错误码。
- 使用仓库现有的测试入口和 fixture 目录，不改写 NativeRun 的既有契约。

测试至少覆盖：
- 正常 BaseLib、RitsuLib、MinionLib 和 Watcher manifest。
- RitsuLib 的多兼容目录。
- 缺字段、无效格式、重复 ID、排序扰动和同输入重复解析。

完成标准：全部测试通过，两次解析的模型摘要和错误分类相同，快照文件保持未修改。写阶段报告后停止。
```

## 阶段 3：依赖图和游戏版本选择

**适合的 Agent**：编码 Agent。

**前置**：阶段 2 PASS。

**交付**：依赖解析器和 `0.111.0` 兼容变体选择，含错误报告和测试。

**启动提示词**：

```text
执行阶段 3，在 ModCatalog 之上实现依赖图和兼容变体选择。

范围：
- 解析最小版本约束和无版本约束。
- 固定目标游戏版本 0.111.0。
- 对 RitsuLib 选择 compat/0.111.0。
- 对 YukiMakoto 选择 lib/0.111.0。
- 任何没有精确兼容变体的条目返回 UnsupportedVersion，不静默回退。
- 输出稳定的拓扑加载顺序、依赖闭包和依赖诊断。
- 区分 DependencyMissing、UnsupportedVersion、InvalidValue、DuplicateId 和 RuntimeFailure。

测试至少覆盖：
- BaseLib、RitsuLib、MinionLib 的正常闭包。
- Watcher、Remilia、LexNinja2、YukiMakoto、Shadowverse 的依赖图。
- 缺依赖、循环依赖、最低版本不满足和兼容目录缺失。
- 依赖顺序扰动后输出保持一致。

禁止加载 DLL。完成测试和交接报告后停止。
```

## 阶段 4：内部标准模型、错误码和合成 fixture

**适合的 Agent**：编码 Agent。

**前置**：阶段 2 PASS。阶段 3 可并行，但依赖模型字段时以阶段 3 的稳定输出为准。

**交付**：内部标准模型、错误码、校验器、转换器外壳和最小 fixture 集。

**启动提示词**：

```text
执行阶段 4，建立不依赖真实 Mod DLL 的最小适配框架。

内部模型至少覆盖：
- CharacterDefinition
- CardDefinition
- RelicDefinition
- PowerDefinition
- EnemyDefinition
- EncounterDefinition
- EventDefinition
- RewardDefinition
- ModDependency
- HookCapability
- CompatibilityReport

错误分类至少覆盖：
MissingFile、InvalidFormat、UnsupportedVersion、MissingField、InvalidValue、DuplicateId、DependencyMissing、ConversionError、DataError、Unsupported、DeterminismMismatch、BudgetExceeded、RuntimeFailure。

每类错误都要有稳定错误码、关联对象或路径、可重试性、是否阻止继续和修复方向。

建立合成 fixture：
- 正常角色、卡牌、遗物、Power、敌人、事件、奖励和资源结果。
- 缺文件、无效格式、不支持版本、缺字段、非法值、重复 ID、缺依赖、稳定排序和重复转换。

完成标准：正常 fixture 能进入内部模型，失败 fixture 分类稳定，同输入两次转换摘要相同，失败不污染注册表。不要加载真实 Mod。完成后停止。
```

## 阶段 5：只读 worker 和运行时隔离

**适合的 Agent**：编码 Agent，熟悉 NativeRun 和程序集加载边界。

**前置**：阶段 3、阶段 4 PASS。

**交付**：独立 worker、只读 ModManager 文件视图、写入拦截、进程级清理和最小运行 manifest 衔接。

**启动提示词**：

```text
执行阶段 5，建立黑盒 Mod 运行时边界。

范围：
- Mod DLL 只在独立 worker 进程加载，核心模拟器保持只依赖内部标准模型。
- worker 只能看到固定快照中获批的 manifest、DLL 和 PCK 路径。
- 真实 Workshop 枚举、正式存档、正式设置和游戏数据访问必须被阻止并记录。
- 所有写入尝试都进入隔离错误记录，不能返回成功假象。
- 每次运行使用新进程和新运行目录，输出最小 run manifest、日志和输入摘要。
- 与现有 NativeRun、build manifest、run manifest、seed 和 replay 检查衔接，不破坏原有入口。

验证：
- 正常静态加载路径。
- 缺 manifest、错误 DLL、依赖缺失、路径逃逸和写入尝试。
- 两次相同 worker 输入产生相同发现摘要。
- worker 结束后不残留 Mod 注册、Harmony 补丁或临时文件。

不要在本阶段接入角色玩法。出现需要扩大宿主权限的情况立即 BLOCKED。完成后停止。
```

## 阶段 6：Watcher 首战纵向切片

**适合的 Agent**：编码和原生运行验证 Agent。

**前置**：阶段 4、阶段 5 PASS。Watcher 的依赖闭包必须由阶段 3 解析为 BaseLib 3.4.7。

**交付**：一个可重复的 Watcher 首战 fixture 和适配报告。

**启动提示词**：

```text
执行阶段 6，只接入 Watcher，不扩展其他角色。

固定输入：
- 快照 workshop-snapshot-20261106-163352。
- 游戏版本 0.111.0。
- BaseLib 3.4.7。
- 固定 seed 和固定动作脚本。

必须走通：
- 角色发现和选择。
- 初始卡组和遗物。
- 一场真实原生战斗。
- 至少一个卡牌或遗物奖励边界。
- 离开战斗和结束清理。
- 新进程中的同构建重放。

每个发现的卡牌、遗物、Power、Hook 和奖励身份都要有内部标准模型映射或明确 Unsupported 记录。加载成功不能作为玩法兼容结论。

验收：
- 真实战斗完成或以精确的 RuntimeFailure/Unsupported 停止。
- 相同快照、版本、seed 和脚本产生相同摘要。
- 不写入快照、玩家存档或正式设置。
- 既有原版和 Eleanor 冻结检查保持通过。

完成阶段报告后停止。不要顺手接入 Aemeath 或其他角色。
```

## 阶段 7：RitsuLib 角色和战斗内容表面

**适合的 Agent**：编码和原生运行验证 Agent。

**前置**：阶段 6 PASS。阶段 3 的精确变体选择必须已通过。

**交付**：一个 RitsuLib 角色的最小闭环，以及卡牌、遗物和 Power 能力矩阵。候选顺序为 YukiMakoto、Remilia、LexNinja2，由阶段 1 的审计结果决定。

**启动提示词**：

```text
执行阶段 7，只选择一个 RitsuLib 角色进行扩展。

先确认候选角色的静态和运行时范围，再选择 YukiMakoto、Remilia 或 LexNinja2 之一。固定 RitsuLib 0.111.0 兼容变体，禁止调用其他游戏版本目录。

本阶段新增：
- 卡牌效果和目标选择。
- 遗物触发时机。
- Power/Buff 的获得、持续、回合边界、死亡和战斗结束清理。
- 与阶段 6 相同的 seed、trace、replay 和错误报告约束。

逐项输出已支持、部分支持和 Unsupported 的内容表面。每个部分支持项必须有边界测试和停止理由。

验收：
- 依赖闭包和加载顺序稳定。
- 至少一个新战斗语义在真实原生生命周期中可重放。
- 同输入两次转换和运行的摘要一致。
- 没有把 RitsuLib 特有 API 直接暴露给核心模拟器。

完成报告后停止，不进入事件、敌人或 MinionLib。
```

## 阶段 8：自定义敌人、遭遇、事件和奖励

**适合的 Agent**：编码和原生运行验证 Agent。

**前置**：阶段 7 PASS。

**交付**：从 Kaguya、St2bs 或 Downfall 中选一个小范围内容，完成一个自定义敌人或事件的最小生命周期闭环。

**启动提示词**：

```text
执行阶段 8，只选择一个 Mod 的一个小范围内容表面。

候选范围：
- 一个自定义敌人和一个最小遭遇。
- 或一个事件、两个选项和一个奖励/资源结果。

优先选择 Kaguya、St2bs 或 Downfall 中最容易建立证据的内容。不要把第四幕、多 Boss、多角色或完整地图当作本阶段目标。

建立并验证：
- 敌人意图和行为，或事件选项和房间转移。
- 奖励生成和资源账本。
- 生命周期开始、执行、结束和 teardown。
- 未覆盖 UI、地图和保存语义的明确 Unsupported 报告。

验收：
- 一个真实原生生命周期闭环可重放。
- 事件或敌人身份、依赖、输入摘要和错误分母进入 run manifest。
- 失败不会污染下一次新进程运行。
- 原版和已通过的角色回归保持通过。

完成报告后停止。
```

## 阶段 9：MinionLib 和 Shadowverse 复合依赖

**适合的 Agent**：编码和原生运行验证 Agent。

**前置**：阶段 7、阶段 8 PASS。

**交付**：MinionLib 最小生命周期和 Shadowverse Beyond Mod 的最小复合闭环。

**启动提示词**：

```text
执行阶段 9，先验证 MinionLib，再接入 Shadowverse Beyond Mod 的最小范围。

固定依赖：
- RitsuLib 至少 0.6.2，目标变体为 0.111.0。
- MinionLib 至少 0.6.2，目标游戏版本为 0.111.0 的可验证范围。

先建立单独 fixture 验证：
- Minion 注册和唯一身份。
- Minion 生命周期和战斗结束清理。
- 一个组件卡牌或资源结果。
- 跨 Mod 依赖解析和加载顺序。

再把最小结果接入 Shadowverse，保留每个未支持语义的证据。不要追求整包通关或性能优化。

验收：
- 依赖、加载顺序、Minion 生命周期和失败隔离可重放。
- 同输入摘要稳定。
- 核心模型没有硬编码 Shadowverse 或 MinionLib 的外部类型。
- 既有角色和事件回归保持通过。

完成后停止。
```

## 阶段 10：全量矩阵、回归和停点报告

**适合的 Agent**：验证和报告 Agent。

**前置**：阶段 6 至阶段 9 的实际结果已经存在。

**交付**：静态发现矩阵、运行时能力矩阵、回归结果和下一阶段建议。

**启动提示词**：

```text
执行阶段 10，只做批量验证、矩阵汇总和停点报告。

覆盖固定快照中的全部 21 个条目，按以下维度记录：
- manifest 是否可解析。
- 依赖是否闭合。
- 0.111.0 兼容变体是否存在。
- worker 是否能在只读边界内启动。
- 已发现的内容表面。
- 已支持、部分支持、Unsupported 和未运行项。
- 稳定错误码、成功分母和失败分母。
- 快照摘要、seed、适配器版本、worker 身份和 run manifest 引用。

资源类、QoL 类和非玩法 Mod 可以只完成加载与隔离验证。角色和复杂内容必须按实际证据填写，不用“加载成功”替代玩法兼容。

重新运行既有原版 M1、Eleanor 冻结证据和核心检查，记录新旧结果。

最终报告必须给出：
- 已完成阶段。
- 未完成阶段。
- 每个 Mod 的下一最小工作单元。
- 失败分母和错误分类。
- 仍需用户决定的边界。
- 是否有任何环境、权限或数据阻塞。

完成报告后停止。不要进入性能、策略或大规模内容接入。
```

## 阶段交接规则

- 每个阶段使用新上下文，先读计划和上一阶段报告。
- 阶段 Agent 只修改本阶段需要的源码、测试和报告文件。
- 任何跨阶段接口变更都要在交接报告中列出，交给下一阶段重新确认。
- 阶段通过以验收证据为准，不以代码量或加载数量为准。
- 阶段失败时保留失败日志和输入摘要，下一 Agent 先复现失败再决定修复或标记 Unsupported。
- 性能、策略和大规模 Mod 接入在阶段 10 报告之后另开路线。
