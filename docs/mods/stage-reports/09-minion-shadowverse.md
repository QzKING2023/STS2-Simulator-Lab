# 阶段 9 交接：MinionLib 与 Shadowverse 最小复合闭环

## 阶段状态

**PASS**。本阶段只覆盖 MinionLib 的最小注册/生命周期 fixture，以及 Shadowverse Beyond Mod 的依赖闭包、原生程序集加载和内容注册边界。真实 Shadowverse Minion 战斗、完整卡池和完整组件语义没有被误报为兼容，并在报告中保持 `Unsupported`。

## 当前分支和工作区

- 分支：`main`，相对 `origin/main` ahead 8。
- 工作区开始时已经存在大量未提交修改，包括 `README.md`、大量历史 `docs/` 删除和新增的 `docs/` 目录；本阶段没有 reset、clean、checkout、提交或推送，也没有覆盖这些修改。
- 固定快照目录存在：`local-mods/workshop-snapshot-20261106-163352`。
- 固定 manifest 存在：21 个条目、142 个文件、总大小 `6,974,558,103` bytes，内容摘要 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 目标游戏固定为 `0.111.0`。RitsuLib 使用精确 `compat/0.111.0`；MinionLib 没有兼容目录，只验证其最低版本 `0.110.1 <= 0.111.0`。

## 实际修改文件

- `src/SimulatorLab.Core/Mods/MinionStage9.cs`
  - 增加标准 Minion 定义、生命周期和组件结果的标量契约。
  - 增加独立 `minion-fixture/v1`，验证注册、唯一身份、召唤、组件结果、战斗结束和清理。
  - 增加阶段 9 依赖校验、原生 probe 转换、determinism、失败隔离和 Unsupported 能力报告。
  - 核心只使用内部记录和标准模型；没有引用 Godot、MinionLib、RitsuLib 或 Shadowverse 外部类型。
- `tools/mod-worker/MinionStage9.csproj`
- `tools/mod-worker/MinionStage9Program.cs`
  - 增加独立阶段 worker、run manifest、fixture 输出和输入引用边界。
- `tools/mod-worker/ModWorker.csproj`
  - 将阶段 8/9 顶级入口从通用 worker 编译目标排除，避免多入口冲突。
- `tools/native-run/MinionNativeModLoop.cs`
  - 在新进程和只读虚拟 ModManager 文件视图中加载 MinionLib、RitsuLib 精确变体和 Shadowverse 程序集。
  - 记录加载顺序、MinionLib 初始化、Shadowverse 内容注册和 fixture 生命周期事件。
  - 不复制或写入固定快照；正式设置、存档和用户数据导向本次 `.build/runs` 输出目录。
- `tools/native-run/AssemblyResolver.cs`
- `tools/native-run/NativeLifecycle.cs`
- `tools/native-run/NativeScenario.cs`
- `tools/native-run/NativeSolverProfile.cs`
- `tools/native-run/Program.cs`
- `tools/native-run/run-native-slice.ps1`
  - 注册阶段 9 场景、环境隔离、probe 输出和 Minion profile 的加载分支。
- `tools/validation/TestMinionStage9.ps1`
  - 阶段 9 逐项验收脚本。
- 本报告。

以下关键输入未修改：固定 Workshop 快照及其 manifest、MinionLib DLL/PCK、RitsuLib DLL 变体、Shadowverse DLL/PCK、原版游戏目录、正式设置和玩家存档。

## 运行证据

- Native build：`.build/native-build/minion-stage9-v6/build-manifest.json`。
  - 游戏版本 `v0.111.0`，游戏程序集摘要由 build manifest 固定。
  - build content key：`4c01494c47417e438b84db682ed2cdde`。
  - 最终 buildId：`AE3C555C063037347D8149263F72F528994D25AB32E56CA2294F850A7EBC7572`。
  - 构建 0 错误，4 个既有分析警告。
- Native primary：`.build/runs/minion-stage9-native-primary-20261108d`。
- Native replay：`.build/runs/minion-stage9-native-replay-20261108d`。
- 两个新进程均为 `nativeRuntimeAvailable=true`、`terminal=lifecycle-complete`。
- 两个 probe 的 digest 相同：`139d3278c21833795ecb43c116def15fac63fe2b6c50349bef4f37b55d24c9f1`。
- 两个进程都记录了：`native-loader`、`minion-register`、`minion-summon`、`component-result`、`combat-end`、`cleanup`。
- 原生加载顺序包含 `MinionLib`、`STS2-RitsuLib` 的 `0.111.0` 变体及其 shared/runtime 程序集，最后加载 `shadowversebydmod`。
- Shadowverse 内容注册在两个进程中均被观察到；`nativeGameplayObserved=false`，因此真实 Shadowverse 战斗保持 Unsupported。
- Worker 结果：`.build/runs/minion-stage9-evaluation-20261108c/stage9-report.json`。
  - 状态 `Pass`。
  - worker determinism digest：`cf5618439be7f5567ed461bf45fee33fbc1f2b099a6d6154b577526a2f6710ee`。
  - fixture digest：`f47af279ab772bd172bf2b04aa32135fec5b6f10e96dd4f8f9f9f4fc65a66b6d`。
  - run manifest：`.build/runs/minion-stage9-evaluation-20261108c/run-manifest.json`。

## 执行过的命令及结果

- `git branch --show-current; git status --short --branch`：分支 `main`，既有未提交修改保留。
- 固定快照目录和 manifest 存在性检查：通过；固定摘要和条目/文件计数匹配。
- `dotnet build src/SimulatorLab.Core/SimulatorLab.Core.csproj -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false`：通过，0 错误。
- `dotnet build tools/mod-worker/MinionStage9.csproj -c Release --nologo --ignore-failed-sources -p:NuGetAudit=false --no-restore`：通过，0 错误。
- `pwsh tools/native-run/build-native-run.ps1 -GamePath .build/pinned-inputs/sts2-0.111.0 -HostSource .build/pinned-inputs/watcher-native-host -OutputRoot .build/native-build/minion-stage9-v6`：通过，0 错误、4 个既有警告。
- 两次 `pwsh tools/native-run/run-native-slice.ps1`，场景分别为 `minion-stage9` 和 `minion-stage9-replay`：均退出码 0，两个 digest 相同。
- `dotnet tools/mod-worker/bin/Release/net9.0/MinionStage9.dll ...`：`Pass`，run manifest 写入成功。
- `pwsh tools/validation/TestMinionStage9.ps1`：通过。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore`：`PASS all 137 contract checks`。
- `pwsh tools/validation/TestModWorker.ps1`：通过。
- `pwsh tools/validation/TestWatcherStage6.ps1`：通过；既有安全 Unsupported 停点摘要稳定。
- `pwsh tools/validation/TestRitsuStage7.ps1`：通过。
- `pwsh tools/validation/TestKaguyaStage8.ps1`：通过。

## 验收标准逐项结果

| 验收项 | 结果 |
| --- | --- |
| 依赖解析和加载顺序可重放 | 通过；Shadowverse 闭包为 `MinionLib -> STS2-RitsuLib -> shadowversebydmod`，RitsuLib 精确选择 `compat/0.111.0`，两个进程顺序一致。 |
| Minion 注册、唯一身份和生命周期可重放 | 通过；`MINION.SHADOWVERSE_FIXTURE_SCOUT#1` 经过 register、summon、component、combat-end、cleanup 五个边界。 |
| 一个组件卡牌或资源结果 | 通过；`CARD.SHADOWVERSE_FIXTURE_COMPONENT` 产生明确 `damage=4` 结果。 |
| 失败隔离 | 通过；原生 probe 使用独立新进程和新 `.build/runs` 目录，快照写入和正式数据写入均为 blocked。 |
| 同输入摘要稳定 | 通过；primary/replay digest 相同，fixture digest 稳定，worker 报告 `deterministic=true`。 |
| 核心模型没有硬编码外部类型 | 通过；核心只消费 `MinionDefinition`、标准 Card/Hook/Resource 记录和 JSON 标量 probe。 |
| 既有角色和事件回归 | 通过；核心 137 项、ModWorker、Watcher 阶段 6、Ritsu 阶段 7、Kaguya 阶段 8 均通过。 |

## 已知缺口和未支持语义

- 没有执行真实 Shadowverse Minion 战斗；阶段 9 只证明了程序集加载、内容注册和独立 fixture 生命周期。
- Shadowverse 完整角色、卡牌、遗物、Power、敌人、事件、组件组合、演化和 UI 语义没有接入核心。
- 组件卡牌结果是受审计的最小 scalar result，不代表 Shadowverse 组件系统的完整序列化、合并、撤销或网络语义。
- 正式保存、地图注入、多人同步和表现层资源保持 Unsupported。
- 快照中的 PCK、DLL 和 manifest 继续只读；没有反编译、重打包、替换或覆盖任何 Workshop 输入。

## 下一阶段唯一建议入口

阶段 10 从固定快照的全量兼容矩阵开始，汇总 21 个条目的静态发现、依赖闭包、worker 隔离、已支持/Partial/Unsupported 分母和阶段 6 至 9 回归结果；不要扩大 Shadowverse 战斗语义。

是否提交或推送：**否**。
