# 阶段 3 交接报告：依赖图与兼容变体选择

## 阶段状态

**PASS**

已在 `ModCatalog` 之上实现只读 `DependencyResolver`。目标游戏版本固定为 `0.111.0`；解析只消费 catalog 中的 manifest、文件摘要和兼容目录元数据，不加载 DLL、PCK 或 Godot 对象。

## 当前分支和 Git 状态摘要

- 分支：`main`。
- 工作区开始时已有大量未提交修改（README、NativeRun、docs 删除/新增等）；本阶段未 reset、clean、checkout、commit 或 push，既有修改保持原状。
- 本阶段新增/修改状态：新增 `src/SimulatorLab.Core/Mods/DependencyResolver.cs`，修改 `ModCatalog.cs` 和检查入口，新增本报告。

## 实际修改文件

- `src/SimulatorLab.Core/Mods/DependencyResolver.cs`
  - 解析依赖闭包、最低版本约束和无版本约束。
  - 输出稳定节点、边、依赖闭包、拓扑顺序、循环和精确变体选择。
  - 对 `RitsuLib` 选择 `compat/0.111.0`，对 `YukiMakoto` 选择 `lib/0.111.0`；存在兼容目录但无精确目标时返回 `UnsupportedVersion`，不回退。
  - 分类 `DependencyMissing`、`UnsupportedVersion`、`InvalidValue`、`DuplicateId` 和循环导致的 `RuntimeFailure`。
- `src/SimulatorLab.Core/Mods/ModCatalog.cs`
  - 扩展阶段错误码枚举，加入 `DependencyMissing` 与 `RuntimeFailure`。
- `tests/SimulatorLab.Core.Checks/Program.cs`
  - 增加正常闭包、指定 Mod 依赖图、缺依赖、循环、最低版本不满足、缺兼容目录和输入顺序扰动测试。
- `docs/mods/stage-reports/03-dependency-resolution.md`

未修改的关键输入：`local-mods/workshop-snapshot-20261106-163352` 及其 `.manifest.json`；未读取实时 Workshop 目录，未写入快照内容。

## 执行过的命令及结果

- `git branch --show-current; git status --short`：`main`，确认已有未提交修改。
- 读取 `AGENTS.md`、`GLOSSARY.md`（不存在时按域文档规则继续）、`docs/agents/domain.md`、`docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`：成功。
- 确认快照目录存在并读取 `local-mods/workshop-snapshot-20261106-163352.manifest.json`：成功；固定目标为 `0.111.0`。
- `dotnet build tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj --no-restore`：成功，0 错误；NuGet 漏洞源不可达产生 `NU1900` 警告。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj --no-restore`：成功，`PASS all 114 contract checks`。

## 验收标准逐项结果

- BaseLib、RitsuLib、MinionLib 正常闭包：通过。
- Watcher、Remilia、LexNinja2、YukiMakoto、Shadowverse 依赖图可解析：通过（完整快照解析并验证关键闭包/边及变体）。
- 缺依赖：通过，返回 `DependencyMissing`。
- 循环依赖：通过，返回循环集合和 `RuntimeFailure` 诊断。
- 最低版本不满足：通过，返回 `UnsupportedVersion`。
- 兼容目录缺失：通过，返回 `UnsupportedVersion`，无静默回退。
- 目标版本精确为 `0.111.0`：通过。
- 依赖顺序扰动后输出稳定：通过，序列化结果一致。
- 禁止加载 DLL：通过；实现仅处理清单和文件摘要。

## 已知缺口和未支持语义

- 尚未进入运行时 worker、程序集隔离或玩法语义探针；阶段 3 只输出获批的静态依赖计划。
- 循环图没有合法完整拓扑序，返回可排序的非循环前缀并保留循环诊断；调用方必须检查 `IsSuccess`。
- 版本约束当前为最小版本（含预发布标识）的比较，不支持复合范围表达式。

## 下一阶段唯一建议入口

以 `DependencyResolution` 的精确文件引用和拓扑顺序为输入，进入新进程 Mod worker 的只读加载边界与运行时探针设计；继续禁止把 DLL 类型或 Godot 对象放入核心模拟器。

## 是否提交或推送

否。未提交、未推送、未创建远程 PR。
