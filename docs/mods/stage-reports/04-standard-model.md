# 阶段 4 交接报告：内部标准模型与合成适配夹具

## 阶段状态

**PASS**

已建立不依赖真实 Mod DLL 的最小适配框架。转换器只接受 JSON 友好的 adapter 输入，输出内部标准模型、错误记录和兼容报告；注册表采用候选构建后一次性提交，任何阻塞错误都不会写入注册表。

## 当前分支和 Git 状态摘要

- 分支：`main`。
- 开始工作时工作区已有大量未提交修改，包括 `README.md`、`THIRD_PARTY_NOTICES.md`、大量 `docs/` 删除/新增、`src/SimulatorLab.Core/Mods/ModCatalog.cs`、`src/SimulatorLab.Core/Mods/DependencyResolver.cs`、`tests/SimulatorLab.Core.Checks/Program.cs` 和 `tools/native-run/` 改动。
- 本阶段未执行 `reset`、`clean`、`checkout`、`commit` 或 `push`；既有修改保持原状。
- 当前状态仍包含上述既有修改，以及本报告列出的阶段 4 新增文件。

## 实际修改文件

本阶段新增：

- `src/SimulatorLab.Core/Mods/ModAdapterModels.cs`
  - `ModPackage`、`CharacterDefinition`、`CardDefinition`、`RelicDefinition`、`PowerDefinition`、`EnemyDefinition`、`EncounterDefinition`、`EventDefinition`、`RewardDefinition`、`ResourceResult`、`HookCapability`、`ModDefinitionSet`、`CompatibilityReport` 和转换计数。
- `src/SimulatorLab.Core/Mods/ModAdapterErrors.cs`
  - 13 类稳定错误码：`MissingFile`、`InvalidFormat`、`UnsupportedVersion`、`MissingField`、`InvalidValue`、`DuplicateId`、`DependencyMissing`、`ConversionError`、`DataError`、`Unsupported`、`DeterminismMismatch`、`BudgetExceeded`、`RuntimeFailure`。
  - 每条错误包含稳定码、对象 ID、路径、可重试性、阻止继续标志、修复方向、消息和可选证据引用。
- `src/SimulatorLab.Core/Mods/ModAdapterInput.cs`
  - adapter 输入记录、JSON 解析器和 `InvalidFormat` 入口。
- `src/SimulatorLab.Core/Mods/ModAdapterConverter.cs`
  - 受限对象种类和 Hook 白名单转换、版本/字段/值/依赖/引用校验、确定性摘要和事务注册表。
- `tests/fixtures/mods/normal.json`
  - 正常角色、卡牌、遗物、Power、敌人、遭遇、事件、奖励和资源结果。
- `tests/fixtures/mods/normal-shuffled.json`
  - 输入顺序扰动夹具。
- `tests/fixtures/mods/missing-file.json`
- `tests/fixtures/mods/invalid-format.json`
- `tests/fixtures/mods/unsupported-version.json`
- `tests/fixtures/mods/missing-field.json`
- `tests/fixtures/mods/invalid-value.json`
- `tests/fixtures/mods/duplicate-id.json`
- `tests/fixtures/mods/dependency-missing.json`
- `docs/mods/stage-reports/04-standard-model.md`

本阶段修改：

- `tests/SimulatorLab.Core.Checks/Program.cs`
  - 增加正常转换、模型计数、稳定排序、重复转换摘要、失败分类、失败注册表隔离和错误码完整性检查。

未修改的关键输入和边界：

- `local-mods/workshop-snapshot-20261106-163352/` 及 `local-mods/workshop-snapshot-20261106-163352.manifest.json` 未修改；没有读取实时 Workshop 目录。
- 未加载、反编译、重打包或替换任何 DLL、PCK、Godot 资源或 manifest。
- `ModCatalog`、`DependencyResolver` 的既有阶段 2/3实现只作为输入契约使用；`tools/native-run/` 和无关项目未修改。

## 执行过的命令及结果

- `git branch --show-current; git status --short`：分支为 `main`；确认已有未提交修改并保留。
- `Test-Path local-mods/workshop-snapshot-20261106-163352` 和读取对应 `.manifest.json`：成功；快照目录存在，清单声明 21 个条目、142 个文件，内容摘要为 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- `dotnet build tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj --no-restore`：成功，0 错误；NuGet 漏洞源不可达产生既有 `NU1900` 警告。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj --no-restore`：成功，`PASS all 133 contract checks`。

## 验收标准逐项结果

| 验收项 | 结果 | 证据 |
| --- | --- | --- |
| 内部模型覆盖角色、卡牌、遗物、Power、敌人、遭遇、事件、奖励、资源结果、Mod 依赖、Hook 和兼容报告 | 通过 | `ModAdapterModels.cs`；正常 fixture 转换并提交 8 个定义、3 个资源结果 |
| 每类错误有稳定错误码、对象/路径、可重试性、阻止继续和修复方向 | 通过 | `AdapterErrorRecord` 与 `AdapterErrorCatalog`；13 个错误码均有唯一 `MOD-001` 至 `MOD-013` 映射 |
| 正常合成 fixture 能进入内部模型 | 通过 | `normal.json`；检查角色至奖励各 1 个，资源结果 3 个，注册表提交成功 |
| 缺文件、无效格式、不支持版本、缺字段、非法值、重复 ID、缺依赖分类稳定 | 通过 | 7 个失败 fixture；每个错误码在两次转换中稳定，失败结果不提交 |
| 稳定排序 | 通过 | 定义 ID、依赖、Hook、资源和错误均按稳定序排序；检查入口验证模型 ID 有序 |
| 同输入两次转换摘要相同 | 通过 | 两个新注册表转换 `normal.json`，`Summary` 与 `DeterminismDigest` 相同 |
| 失败不污染注册表 | 通过 | 7 个失败 fixture 转换后注册表计数均保持 0 |
| 不加载真实 Mod | 通过 | 本阶段只读取仓库文件和合成 JSON；未引入 DLL/PCK 加载路径 |

## 已知缺口和未支持语义

- 当前阶段只建立标准模型和 adapter 边界，没有执行 Mod worker、程序集隔离、Godot 生命周期或玩法运行时探针。
- `ConversionError`、`DataError`、`Unsupported`、`DeterminismMismatch`、`BudgetExceeded` 和 `RuntimeFailure` 已有稳定策略和修复方向；本阶段合成验收重点覆盖提示词指定的 7 类输入失败，尚未把每个运行时类都接入真实 worker 触发路径。
- Hook 只允许当前白名单中的战斗/奖励/事件时机；未支持 Hook 会明确返回 `Unsupported` 并阻止候选提交，不会静默跳过。
- 标准模型不包含 Godot 对象、程序集实例、Harmony 状态、玩家存档、正式设置或真实 Workshop 路径。
- 资源结果目前是有向量的稳定数值记录，尚未定义游戏特定的资源上限、结算顺序或 UI 语义。

## 下一阶段唯一建议入口

阶段 5：只读 worker。以 `DependencyResolution` 的精确 `0.111.0` 文件引用和本阶段 `ModAdapterDocument`/`ModAdapterConverter` 契约为输入，建立新进程、只读 ModManager 文件视图、写入拦截和最小 run manifest；继续禁止把 DLL 类型或 Godot 对象放入核心模拟器。

## 是否提交或推送

否。未提交、未推送、未创建远程 PR。
