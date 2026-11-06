# 阶段 1：Mod 输入审计和适配边界

日期为 2026-11-06。目标游戏版本固定为 `0.111.0`。本报告只覆盖固定 Workshop 快照的输入审计、现有 Simulator Lab seam 盘点和阶段 2 至阶段 6 的设计交接。没有加载 DLL，没有执行玩法，没有反编译快照内容。

## 阶段状态

**PASS**

静态审计和边界设计已完成。Watcher 的首战可行性仍需阶段 5 的隔离 worker 和阶段 6 的运行时探针确认，这属于本阶段明确保留的运行时未知项，不阻塞本次只读审计。

## 工作区和来源保护

- 当前分支为 `main`。
- 开始前工作区已有 270 条未提交变更，包含 219 条删除、25 条修改和 26 条未跟踪路径。主要涉及既有 `README.md`、`THIRD_PARTY_NOTICES.md`、`tools/native-run` 变更，以及 `docs/`、`src/`、`tools/maintenance` 和 `tools/performance` 下的新路径。
- 本 Agent 未执行 `reset`、`clean`、`checkout`、`commit` 或 `push`，没有覆盖任何既有变更。
- 固定快照目录 `local-mods/workshop-snapshot-20261106-163352` 存在。
- 固定清单 `local-mods/workshop-snapshot-20261106-163352.manifest.json` 存在并已读取。
- 本 Agent 新增本报告文件。快照目录、清单、源码、NativeRun 入口和 Eleanor fixture 均未改写。

## 静态 manifest 事实

清单 schema 为 `sts2-simulator-lab/workshop-snapshot/v1`，Workshop App 为 `2868840`，捕获时间为 `2026-11-06T08:41:51.260846Z`。清单记录 21 个 Workshop 条目、142 个文件和 `6,974,558,103` bytes，磁盘复扫得到相同的条目数、文件数和字节数。固定内容摘要为 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。清单标记 `SourceAndSnapshotInventoryMatch=true`，比较字段为相对路径、长度和 SHA-256。

一个 Workshop 条目可以包含多个逻辑 Mod manifest。3749627968 是一个条目，但包含 4 个逻辑 Mod。因而本次输入有 21 个来源条目和 24 个逻辑 Mod ID，24 个逻辑 ID 均唯一。

| Workshop ID | 逻辑 Mod ID | 版本 | 最低游戏版本 | 依赖 | 玩法标志 | 静态内容类别 |
| --- | --- | --- | --- | --- | --- | --- |
| 3737322022 | QuickRestart | v2.0.0 | 0.107.0 | BaseLib >= 3.4.5 | false | QoL，DLL、PCK、JSON |
| 3737335127 | BaseLib | v3.4.7 | 0.107.1 | 无 | false | 基础库，DLL、PCK、JSON |
| 3737336234 | MintySpire2 | v1.2.0 | 0.107.0 | BaseLib >= 3.4.5 | false | QoL 或资源包，DLL、PCK、JSON、PDB |
| 3747492505 | Watcher | 1.5.14 | 0.107.1 | BaseLib >= 3.4.7 | true | 单角色包，DLL、PCK、JSON |
| 3747508091 | Downfall | 0.1.24 | 0.107.1 | BaseLib >= 3.4.7 | true | 多角色和遭遇包，9 组 DLL/PCK 加 JSON |
| 3747508952 | figure_Saya | 2.1.0 | 0.107.1 | 无 | true | 角色和表现资源，DLL、PCK、JSON、设置文本 |
| 3747515571 | Remilia | v1.0.3 | 0.108.0 | STS2-RitsuLib >= 0.5.2 | true | RitsuLib 角色包，DLL、PCK、JSON |
| 3747528152 | intentgraph2 | 1.6.0 | 0.107.1 | manifest 未声明 | false | 工具或表现包，含 BaseLib、RitsuLib 和 `0.110.0` 核心 DLL |
| 3747528329 | Ryoshu | 0.3.6 | 未声明 | manifest 未声明 | true | 单角色包，DLL、PCK、JSON、README |
| 3747530622 | LexNinja2 | v3.0.11-beta-0-g358712f | 0.111.0 | BaseLib >= 3.4.5，STS2-RitsuLib >= 0.5.12 | true | RitsuLib 角色包，DLL、PCK、JSON |
| 3747548629 | Kaguya | v3.2.4 | 0.111.0 | BaseLib >= 3.4.7 | true | 角色、敌人、Boss、事件和第四幕包 |
| 3747588205 | Acheron | v0.4.11 | v0.107.1 | 无 | true | 单角色包，DLL、PCK、JSON |
| 3747595039 | MinionLib | 0.6.3 | 0.110.1 | 无 | true | MinionLib 框架，DLL、PCK、JSON |
| 3747602295 | STS2-RitsuLib | 0.6.10 | 0.107.1 | 无 | false | 框架库，4 个兼容目录、shared 程序集、assets.zip 和 viewer |
| 3747644378 | shadowversebydmod | 1.6.0 | 0.111.0 | STS2-RitsuLib >= 0.6.2，MinionLib >= 0.6.2 | true | 大型复合角色和 Minion 包，DLL、PCK、JSON |
| 3747725225 | Aemeath | 1.0.2 | 未声明 | BaseLib >= 3.2.1 | true | 单角色包，DLL、PCK、JSON |
| 3747952763 | ATA_IronClad | 0.1.12 | 0.103.2 | 无 | false | 非玩法角色替换包，DLL、PCK、manifest |
| 3749061519 | St2bs | v2.3.0 | 0.111.0 | BaseLib，最低版本为 null | true | 角色或复合玩法包，DLL、PCK、JSON |
| 3749627968 | NecrobinderCardPortraits、NecrobinderOstyAnimMod、OrchisNecrobinderSkinMod、RestSiteFondleCursorMod | 1.4.0、1.4.0、1.0.0、1.4.0 | 均未声明 | 均未声明 | 均为 false | 资源和表现层集合，卡图、动画、皮肤、光标，12 个文件 |
| 3787753911 | LieRenTVmod | v0.1.3 | 未声明 | 无 | false | PCK-only 资源包 |
| 3829924471 | YukiMakoto | v0.0.7-hot | 0.107.1 | STS2-RitsuLib >= 0.6.10 | true | RitsuLib 角色包，PCK 加 0.107.1 和 0.111.0 DLL 变体 |

### 兼容目录和变体

- STS2-RitsuLib 的 `compat/` 目录有 `0.107.1`、`0.109.0`、`0.110.0` 和 `0.111.0` 四组。每组有 `STS2-RitsuLib.dll`、`STS2-RitsuLib.Runtime.dll`、PDB、XML 和 `compat-target.txt`。`ritsulib-variants.manifest` 为 schema 2，并为每组记录程序集 SHA-256。目标版本必须选择 `compat/0.111.0`。
- YukiMakoto 的 `lib/` 目录有 `0.107.1` 和 `0.111.0` 两组。`yukimakoto-variants.manifest` 记录两组目录、目标和程序集 SHA-256。目标版本必须选择 `lib/0.111.0/YukiMakoto.dll`。
- intentgraph2 只有 `intentgraph2core-lt-0.110.0.dll` 这一静态文件名，没有 `0.111.0` 变体段。它的 `affects_gameplay=false`，阶段 1 将其视为资源或工具包，不把这个文件名当作目标游戏兼容证明。
- 其他角色包没有显式兼容目录。只有存在兼容目录清单时才进行目录选择。最低游戏版本比较仍需使用目标 `0.111.0`。

### 输入格式异常

- Aemeath、Ryoshu、4 个 Necrobinder 逻辑 Mod 和 LieRenTVmod 没有 `min_game_version`。
- St2bs 的 BaseLib 依赖存在，但 `min_version` 为 null。
- Acheron 的最低版本写成 `v0.107.1`，带 `v` 前缀。
- 依赖字段在不同 manifest 中同时出现为缺失、null、对象和数组。解析器必须把这些形状规范化，不能把缺失依赖和空依赖混为非法版本。

## 现有 Simulator Lab seam

### NativeRun 和宿主隔离

- `tools/validation/NativeModRunLoop.cs` 已有最接近目标的受限加载 seam。它调用原生 `ModManager.Initialize`，目标版本写死为 `0.111.0`，使用 `PinnedFileIo` 只读目录视图，拦截 `CopyUnmoddedSaveFilesIfNeeded`，替换环境查询，设置 MemorySaveStore，并在新进程中绑定 BaseLib、STS2-RitsuLib 和测试 Mod。它还记录原始 BaseLib discovery patch、库程序集所有权、late patch 负控和 pool preflight。当前实现仍绑定 Eleanor 环境变量和 `MyMod`，不能直接当通用 Mod worker。
- `tools/native-run/NativeHost.cs` 提供 TestMode、Steam 禁用、MemorySaveStore、UI 和网络约束、回放和进度写入拦截、Harmony patch inventory 及 `Ensure` 审计。可复用的是隔离契约和 patch inventory，不能复用 Eleanor 专属状态。
- `tools/native-run/Program.cs` 是原生进程入口。它读取构建清单，限制输出目录在 `.build/runs`，写入 `profile.json`、`events.json`、`trace.jsonl`、`trace-seal.json`、`result.json`、`process.json` 和 timing 信息。header 已包含 buildId、seed、scenario、provenance、游戏版本和 RNG inventory。
- `tools/native-run/NativeScenario.cs` 为场景登记 `natural-run`、`api-fixture` 和 `fault-control`，并把 live、frozen replay 和 observation-control 分开。Mod worker 应复用这种来源分类，不能把静态发现或 fixture 写成自然玩法。

### build manifest、run manifest 和日志

- `tools/native-run/build-native-run.ps1` 产出 `build-manifest.json`，schema 为 `native-build/v2`。其 `manifest.source`、`inputs`、`compiled` 和 `artifacts` 行绑定路径和 SHA-256，并生成 content key、buildId、版本和工具链信息。
- 同一脚本产出 `run-manifest.json`，schema 为 `native-build-run/v2`，并把 `run.log` 作为完整构建日志。普通构建不复制 evidence package。
- `tools/native-run/run-native-slice.ps1` 要求新的 `.build/runs/<dir>`，复制 build manifest，运行前后重验 source、input、compiled 和 artifact 的 hash，保存 `console.log`、`stderr.log` 和 `process.json`，拒绝覆盖旧运行目录。
- `NativeEvidence` 和 `NativeTraceContract` 提供 JSONL 事件链、seal、身份引用、计数、生命周期、RNG 和 replay 一致性检查。`src/SimulatorLab.Core/NativeRunExecution.cs` 提供 typed stop、terminal、confirmed/dispatched、data completeness 和 exit code 契约。

### fixture 和 Eleanor 冻结证据

- `tests/fixtures/stage-one-cases.json` 固定 v0.111.0 的重复 fixture、错误控制、预算控制和已知 Unsupported 项。它证明的是可复现和错误传播，不是 Mod 玩法覆盖。
- `tests/fixtures/eleanor-v041-contracts.json` 保存 Eleanor v0.4.1 独立契约，包含 assembly SHA-256、ManaShield、Reverse upkeep 和死亡生命周期期望。该文件可作为回归输入，但不是新的 Mod 标准模型。
- `tools/validation/EleanorModBootstrap.cs`、`EleanorV040Probe.cs`、`NativeModRunLoop.cs` 和 `run-v041-native-loop-cases.ps1` 展示了实际程序集身份、manifest hash、MemorySaveStore、写入拦截、奖励选择、两战重放和 teardown 的 seam。
- `docs/validation/ELEANOR_NATIVE_RUN_LOOP_REVIEW.md` 记录了 v0.4.1 冻结路线、游戏 `0.111.0 / 41cef1ea`、16 个真实动作和 122 项检查的证据入口。当前工作区没有该文档所引用的 `.build/pinned-inputs/eleanor-v0.4.1-forms-cardpool.fix`、`.build/pinned-inputs/eleanor-v041-native-bootstrap` 或相应 `.build/runs` 目录，因此本阶段只确认入口和契约，不把历史路径当作当前可执行证据。

## 外部输入到内部模型的边界

```text
固定快照目录 + snapshot manifest
        |
        v
ModCatalog / ModSource / ModManifest
        |
        v
DependencyResolver(target = 0.111.0)
        |  只输出获批的文件引用、精确兼容变体和拓扑顺序
        v
新进程 Mod worker
  只读 ModManagerFileView
  只允许固定快照的 manifest、DLL、PCK 和显式资源
  Godot 对象、Mod 类型、Harmony、注册表只存在于 worker
        |
        v
受审计的 RuntimeObservation / Adapter DTO
  来源身份、事件、模型字段、Hook、错误和证据引用
        |
        v
内部标准模型
  ModPackage、CharacterDefinition、CardDefinition、RelicDefinition
  PowerDefinition、EnemyDefinition、EncounterDefinition、EventDefinition
  RewardDefinition、HookCapability、CompatibilityReport
        |
        v
核心模拟器和 NativeRun replay
```

核心模拟器只接受标准模型和错误记录。任何 Godot 对象、任意外部 Mod 类型、程序集实例和未审计 DTO 都在 worker 边界被截断。缺少语义时输出 `Unsupported` 和证据引用，不用空对象代替玩法。

## 最小边界字段设计

这些是阶段 2 至阶段 5 的接口下限，字段均为可序列化的稳定值。

### `ModSource`

- `snapshotId`
- `workshopId`
- `snapshotRelativeRoot`
- `capturedUtc`
- `snapshotContentSha256`
- `metadataPath`
- `files[]`，每项为 `relativePath`、`length`、`sha256`
- `sourceDigest`

### `ModManifest`

- `modId`
- `displayName`
- `version`
- `minimumGameVersion`，规范化后的版本值和原始文本
- `affectsGameplay`
- `dependencies[]`，每项为 `modId`、可选 `minimumVersion`、原始文本
- `source`
- `manifestRelativePath`
- `hasDll`、`hasPck`
- `packageFiles[]`
- `compatibilityVariants[]`，每项为 `target`、相对目录、程序集文件和 SHA-256
- `normalizationWarnings[]`

### 依赖图

- `nodes[]`，以 `workshopId + modId` 为稳定来源键
- `edges[]`，包含 source、target、minimumVersion、declaredPath
- `selectedVariants[]`，包含 target `0.111.0`、目录、文件摘要和选择原因
- `topologicalOrder[]`
- `missingDependencies[]`
- `cycles[]`
- `diagnostics[]`
- `targetGameVersion`

### `CapabilityReport`

- `schema`
- `snapshotId`、`snapshotContentSha256`
- `targetGameVersion`
- `workerId`、`workerRunId`
- `modIdentities[]`，包含 Workshop ID、Mod ID、版本、程序集和 manifest 摘要
- `discoveredCapabilities[]`
- `mappedCapabilities[]`
- `unsupportedCapabilities[]`，包含 capability、reason、evidenceRef
- `conversionCounts`
- `errorCounts`
- `determinismDigest`
- `isolation`，包含读取范围、写入拦截和残留检查
- `evidenceRefs[]`

### `ErrorRecord`

- `code`
- `stage`
- `severity`
- `blocking`
- `retryable`
- `objectId`
- `path`
- `message`
- `sourceDigest`
- `evidenceRef`
- `exceptionType`，只作为 worker 日志摘要，不能把外部异常类型泄漏进核心模型

阶段使用的稳定错误码为 `MissingFile`、`InvalidFormat`、`UnsupportedVersion`、`MissingField`、`InvalidValue`、`DuplicateId`、`DependencyMissing`、`ConversionError`、`DataError`、`Unsupported`、`DeterminismMismatch`、`BudgetExceeded` 和 `RuntimeFailure`。

### worker 边界

输入最小字段为 `workerRunId`、`snapshotId`、`targetGameVersion`、`selectedModIds`、`selectedFiles`、`adapterVersion`、`configDigest`、`seed`、`replayPath` 和新建 `outputDirectory`。输出只允许 `CapabilityReport`、标准模型、`ErrorRecord[]`、最小日志摘要和文件哈希引用。worker 每次新进程启动，结束后检查 Mod 注册、Harmony patch、临时文件和写入拦截计数。

## Watcher 首个角色切片

### 静态可行性

Watcher 是当前最适合的首个角色探针。它只有一个 DLL、一个 PCK 和一个 JSON manifest，依赖闭包为 `Watcher -> BaseLib >= 3.4.7`，最低游戏版本为 0.107.1，目标 0.111.0 没有显式兼容目录冲突。它不引入 RitsuLib、MinionLib 或多角色包的额外依赖面。

### 运行时探针必须确认的假设

1. `ModManager.Initialize` 在受限只读文件视图下能发现 Watcher 和 BaseLib，并且加载顺序、依赖状态和程序集所有权完整。
2. BaseLib 3.4.7 的原始 discovery hook 可以在当前 TestMode 和 headless Godot 环境中安全执行。
3. Watcher 的真实角色、起始卡组、起始遗物、CardPool、Power 和任何自定义 Hook 能被发现并映射。manifest 的 `affects_gameplay=true` 不能替代逐项发现。
4. Watcher PCK 的资源、atlas、localization 和模型构造不会触发当前 worker 未覆盖的 Godot 原生初始化。
5. 首场战斗可以走原生角色选择、初始状态、卡牌或遗物奖励、离开战斗和 teardown，且不需要未登记的 UI、保存或设置语义。
6. 两个新进程使用相同快照、0.111.0、seed 和动作脚本时，标准模型摘要、能力报告和 trace digest 一致。
7. worker 能在发现失败、写入尝试、Harmony 残留或未支持 Hook 时给出精确 `RuntimeFailure` 或 `Unsupported`，不会仅返回 Loaded。

结论为 **条件可行**。阶段 6 只有在阶段 3 的 BaseLib 3.4.7 闭包、阶段 4 的标准模型和阶段 5 的 worker 隔离均通过后才可开始。第一条运行时探针应先停在角色发现和首场战斗准备边界，确认后再进入奖励和重放。

## 阶段 2 至阶段 6 的拟修改范围和测试矩阵

### 阶段 2，ModCatalog 和 manifest 解析

拟修改范围为新增 `src/SimulatorLab.Core/Mods/` 下的来源、manifest、版本规范化、catalog 和解析错误类型，新增 `tools/mods/` 下的只读报告入口，新增 `tests/fixtures/mods/` 合成输入和 `tests/SimulatorLab.Core.Checks/` 的解析检查。现有 `tools/native-run` 契约只读，不重写。

测试覆盖 21 个 Workshop 条目、24 个逻辑 Mod、`mod_manifest.json` 和自有 JSON 两种形状、RitsuLib 多兼容目录、缺字段、无效 JSON、重复 ID、排序扰动、路径逃逸、同输入双次解析和快照未写入。

### 阶段 3，依赖图和 0.111.0 选择

拟修改范围为 `src/SimulatorLab.Core/Mods/DependencyResolver*`、版本约束和诊断模型、`tools/mods/` 的解析报告入口以及依赖 fixture。不得加载 DLL。

测试覆盖 BaseLib、RitsuLib、MinionLib 正常闭包，Watcher、Remilia、LexNinja2、YukiMakoto 和 Shadowverse 依赖图，缺依赖、循环依赖、最低版本不满足、缺兼容目录、RitsuLib `compat/0.111.0`、YukiMakoto `lib/0.111.0` 和输入顺序扰动稳定性。没有精确兼容变体时返回 `UnsupportedVersion`，不回退到其他目录。

### 阶段 4，内部标准模型和合成 fixture

拟修改范围为 `src/SimulatorLab.Core/Mods/Models*`、`CapabilityReport`、`ErrorRecord`、转换器外壳和注册表边界，新增 `tests/fixtures/mods/` 的正常与失败 fixture，以及 `tests/SimulatorLab.Core.Checks/` 的稳定摘要检查。

测试至少覆盖 Character、Card、Relic、Power、Enemy、Encounter、Event、Reward、资源结果、HookCapability 和 CompatibilityReport。失败项覆盖 MissingFile、InvalidFormat、UnsupportedVersion、MissingField、InvalidValue、DuplicateId、DependencyMissing、ConversionError、DataError、Unsupported、DeterminismMismatch、BudgetExceeded 和 RuntimeFailure。重复转换必须摘要一致，失败不能污染注册表。

### 阶段 5，只读 worker 和运行时隔离

拟修改范围为新增 `tools/mod-worker/` 或等价独立 worker 项目、`tools/native-run/` 的最小 manifest 衔接和 `tools/validation/` 的只读 worker 检查。可复用 `NativeModRunLoop.PinnedFileIo`、`NativeHost` 的写入和 patch 审计、`build-native-run.ps1` 的输入绑定和 `run-native-slice.ps1` 的新运行目录规则。不得把 worker 中的 Godot 类型加入 `SimulatorLab.Core`。

测试覆盖正常静态加载、缺 manifest、错误 DLL、依赖缺失、路径逃逸、PCK 访问范围、设置或存档写入、真实 Workshop 枚举、两次同输入发现摘要、进程退出后的 Mod 注册和 Harmony 清理。每次运行都必须有新进程、新目录、最小 run manifest、日志和输入摘要。

### 阶段 6，Watcher 首战纵向切片

拟修改范围为 Watcher 专用 adapter、角色选择和初始状态转换、首战 fixture、奖励和 teardown 证据，主要落在 `tools/mod-worker/`、`src/SimulatorLab.Core/Mods/` 的标准模型适配层、`tools/native-run/` 的新增场景入口和 `tests/fixtures/mods/watcher/`。不接入 Aemeath 或其他角色。

测试矩阵为角色发现、初始卡组、初始遗物、首场真实战斗、至少一个卡牌或遗物奖励边界、离开战斗、结束清理、新进程同构建重放、两次 digest 比较、写入拦截、原版回归和 Eleanor 冻结检查入口。任何未实现生命周期、UI、资源或保存语义均进入 `Unsupported` 或 `RuntimeFailure`，不能以加载成功收口。

## 阶段 1 验收结果

| 验收项 | 结果 | 证据或说明 |
| --- | --- | --- |
| 读取全部 21 条目、兼容目录、变体清单和包布局 | 通过 | 清单 21 条目、142 文件。RitsuLib、YukiMakoto、intentgraph2 和 Necrobinder 特殊布局已单独核对 |
| 建立来源身份、版本、最低版本、依赖、玩法标志和静态类别 | 通过 | 上表覆盖 21 个来源条目和 24 个逻辑 Mod |
| 读取 NativeRun、build manifest、run manifest、日志、fixture 和 Eleanor 入口 | 通过 | 已定位 NativeModRunLoop、NativeHost、Program、NativeEvidence、build/run scripts、fixtures 和冻结报告入口 |
| 画出外部输入到内部模型边界 | 通过 | worker 和标准模型边界见上图 |
| 明确最小字段 | 通过 | ModSource、ModManifest、依赖图、CapabilityReport、ErrorRecord 和 worker 字段已列出 |
| Watcher 可行性判断和运行时假设 | 通过 | 条件可行，运行时假设已列出 |
| 不执行玩法、不反编译、不访问实时 Workshop | 通过 | 本阶段命令只读取仓库和固定快照，未访问实时目录 |

## 已知缺口和未支持语义

- 本阶段没有验证任何 Mod DLL 的加载、初始化、模型发现、PCK 资源或玩法语义。
- Aemeath、Ryoshu、Necrobinder 和 LieRenTVmod 的最低游戏版本缺失。Acheron 的版本带 `v` 前缀。St2bs 的 BaseLib 最低版本为 null。这些输入必须在阶段 2 产生规范化警告或稳定错误，不能静默猜测。
- intentgraph2 的 `0.110.0` 核心 DLL 不能证明 `0.111.0` 玩法兼容。资源类和非玩法类条目不得进入角色玩法成功分母。
- 当前工作区保留了 Eleanor 的报告和 fixture 入口，但引用的 pinned input 和历史 `.build/runs` 证据目录不在本地。只能在未来阶段重新验证或明确引用外部冻结物，不能把文档路径当作现时运行证据。
- 现有 `NativeModRunLoop` 依赖 Eleanor 环境变量、`MyMod` 类型和专用 profile。它是 worker 设计的 seam，不是完成的通用 Mod loader。
- 快照顶层聚合摘要的具体计算公式未在本阶段重建。Phase 0 报告已确认逐文件路径、长度和 SHA-256 完全一致，阶段 2 应继续复用清单中的固定摘要并避免自行定义新的快照身份公式。

## 下一阶段唯一建议入口

阶段 2，使用本报告的字段和异常清单实现只读 `ModCatalog` 与 manifest 解析。入口提示词为 `docs/prompts/MOD_ADAPTATION_STAGE_PROMPTS.md` 的“阶段 2：ModCatalog 和 manifest 解析”。首先锁定 21 个来源条目、24 个逻辑 ID、版本前缀规范化和重复 ID 规则，再进入阶段 3 的依赖选择。

## 提交和推送

本 Agent 未提交、未推送、未创建远程 PR，也未修改 Git 历史。
