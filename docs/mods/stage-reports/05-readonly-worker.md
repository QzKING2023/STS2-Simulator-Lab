# 阶段 5 交接报告：黑盒 Mod 只读 worker 与进程隔离

## 阶段状态

**PASS**

本阶段建立了一个独立的 `SimulatorLab.ModWorker` 进程。它只接受固定快照清单解析出的精确 `0.111.0` 依赖闭包，校验获批 manifest、DLL 和 PCK 的长度与 SHA-256，再在可回收 `AssemblyLoadContext` 中读取 DLL 元数据。核心模拟器仍只依赖 `SimulatorLab.Core.Mods` 的 JSON 友好标准模型与证据记录。

## 当前分支和 git status 摘要

- 分支：`main`。
- 开始和结束时都保留了已有未提交修改：`README.md` 修改、大量旧 `docs/` 删除，以及已有的 `docs/`、`src/` 和其他阶段文件新增/修改。
- 本阶段没有 `reset`、`clean`、`checkout`、提交、推送或远程 PR。
- 当前状态还包含本阶段新增的 worker 文件和本报告；没有把忽略的 `.build/` 运行证据加入版本控制。

## 实际修改文件

本阶段新增：

- `src/SimulatorLab.Core/Mods/ModWorkerContracts.cs`
  - 定义只含标量、路径和证据的 worker 输入、错误、发现摘要、隔离证据和 run manifest 契约。
  - 错误码固定为 `MOD-W001` 至 `MOD-W008`，不引入 DLL 类型、Godot 对象或 Harmony 对象。
- `tools/mod-worker/ModWorker.csproj`
- `tools/mod-worker/Program.cs`
  - 固定快照身份和内容摘要校验。
  - 精确 `0.111.0` 依赖闭包选择，拒绝缺 manifest、缺依赖、版本不匹配和路径逃逸。
  - 只读取清单列出的 manifest、DLL、PCK，校验长度和 SHA-256。
  - 使用独立进程和 collectible `AssemblyLoadContext` 做静态程序集发现；不执行 Mod 入口点，不把程序集实例返回核心。
  - 拒绝 Workshop、存档、设置、正式游戏数据枚举和访问；写入尝试记录为 `WriteBlocked`，运行不能伪装成功。
  - 生成最小 `run-manifest.json`、`input-summary.json` 和 `worker.log`，包含 seed、build ID/content key、replay 摘要、发现摘要、错误和 teardown 证据。
- `tools/mod-worker/run-mod-worker.ps1`
  - 绑定既有 NativeRun `build-manifest.json` 的 source/input 哈希，建立新运行目录，启动新 worker 进程，并校验进程退出码与 run manifest 一致。
- `tools/mod-worker/README.md`
- `tools/validation/TestModWorker.ps1`
  - 覆盖正常静态加载、两次确定性摘要、缺 manifest、错误 DLL 类型/路径逃逸、缺依赖和 Workshop/存档写入阻止。

未修改的关键文件和输入：

- `local-mods/workshop-snapshot-20261106-163352/` 和对应 `.manifest.json` 未修改、未重打包、未替换、未反编译。
- 快照清单复核成功：`SnapshotDirectory=workshop-snapshot-20261106-163352`、`ItemCount=21`、`FileCount=142`、摘要为 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- `tools/native-run/`、既有 `NativeRun` 入口、核心模拟器注册表和无关 SnowLuma 项目未修改。

## 执行过的命令及结果

- `git branch --show-current; git status --short`：分支为 `main`；确认既有修改并保留。
- 读取 `AGENTS.md`、`docs/agents/domain.md`、`docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`、固定快照目录和 manifest：成功；没有 `GLOSSARY.md`，按仓库规则静默继续。
- `dotnet build tools/mod-worker/ModWorker.csproj --ignore-failed-sources -p:NuGetAudit=false`：成功，0 错误。
- `dotnet build tools/mod-worker/ModWorker.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false`：成功，0 错误。
- `powershell -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestModWorker.ps1`：成功，输出 `PASS mod-worker ... deterministic=True failures=5`。
- `powershell ... tools/mod-worker/run-mod-worker.ps1 ...`：成功，输出 `MOD_WORKER_EXIT=0`，run manifest 绑定测试 build ID、content key 和 seed。
- `dotnet build tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj --no-restore`：成功，0 错误；有既有 `NU1900` 网络警告。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj --no-build --no-restore`：成功，`PASS all 134 contract checks`。

## 验收标准逐项结果

| 验收项 | 结果 | 证据 |
| --- | --- | --- |
| Mod DLL 只在独立 worker 进程加载，核心只依赖标准模型 | 通过 | `ModWorker.csproj` 独立可执行项目；核心契约仅为 JSON/标量记录；正常 run manifest 的 `workerProcess=true`、`coreLoadedModAssembly=false` |
| worker 只看到固定快照获批 manifest、DLL、PCK 路径 | 通过 | 固定目录名和清单摘要绑定；依赖闭包文件来自清单精确行；请求额外路径返回 `MOD-W005` |
| 目标版本精确为 `0.111.0`，不静默回退兼容变体 | 通过 | worker 固定目标常量并复用 `DependencyResolver`；版本不匹配返回 `MOD-W003` |
| 真实 Workshop、正式存档、设置和游戏数据访问被阻止并记录 | 通过 | `AccessAttempts` 产生 `AccessBlocked`；路径策略覆盖 Workshop、userdata、saves、settings、config、AppData、正式安装目录等 |
| 所有写入尝试进入隔离错误记录，不能返回成功假象 | 通过 | `WriteAttempts` 产生 `WriteBlocked`，状态为 `blocked`、退出码为 2；验证脚本检查 MOD-W006/MOD-W007 |
| 每次运行新进程、新目录、最小 run manifest、日志和输入摘要 | 通过 | worker 拒绝已存在输出目录；桥接脚本生成 GUID 目录；输出 `run-manifest.json`、`worker.log`、`input-summary.json`，并可复制绑定 build manifest |
| 与 NativeRun/build manifest/run manifest/seed/replay 衔接 | 通过 | 桥接脚本校验 build manifest source/input 存在性和 SHA-256；run manifest 记录 build ID、content key、seed、replay 摘要；退出码一致性有检查 |
| 正常静态加载路径 | 通过 | Watcher + BaseLib 真实快照 DLL/PCK 发现成功；报告发现 2 个程序集和 2 个 PCK |
| 缺 manifest、错误 DLL、依赖缺失、路径逃逸和写入尝试 | 通过 | 验证脚本覆盖缺 manifest、把 PCK 当 DLL、非法 DLL 路径、缺 Mod 依赖、路径逃逸、Workshop 枚举和存档写入；每项都有稳定错误码 |
| 两次相同输入产生相同发现摘要 | 通过 | 两个新进程的 `discovery.digest` 均为 `7ea316da8517e1edd502517ff49a7c088f67dad8f688f4aa6178c6ea88711e8a` |
| worker 结束后无 Mod 注册、Harmony 补丁或临时文件残留 | 通过 | run manifest 记录 `modRegistrationCount=0`、`harmonyPatchCount=0`、`temporaryFileCount=0`、`assemblyLoadContextUnloaded=true`；验证脚本扫描运行目录没有 DLL/PCK/TMP 残留 |

## 已知缺口和未支持语义

- 本阶段实现的是静态黑盒发现和隔离边界，没有执行 Mod 入口点、Godot 场景、PCK 资源脚本或玩法生命周期；“发现成功”不代表角色、敌人、事件或奖励兼容。
- 任意第三方代码的操作系统级写入无法在未执行其入口点时被动态拦截；当前协议把写入/访问请求作为拒绝性探针记录，并且 worker 不向 Mod 暴露文件系统 API。未来若要执行运行时代码，必须继续保持独立 worker，并补充受限 host API 与 OS 级沙箱。
- 错误 DLL 在固定快照内会以长度/SHA-256 或 `BadImageFormat` 失败；外部 DLL 路径先按路径逃逸拒绝，不能借此加载任意测试程序集。
- 没有接入角色玩法、敌人、遭遇、事件或奖励语义，也没有改变既有 NativeRun 的游戏入口。

## 下一阶段唯一建议入口

以本阶段 `ModWorkerRunManifest` 的成功发现摘要和 `ModAdapterConverter` 标准模型为输入，进入阶段 6 的 Watcher 单角色首战纵向切片；任何需要执行 Godot/PCK/Mod 玩法语义的工作必须继续由新进程和新运行目录承载。

## 是否提交或推送

否。未提交、未推送、未创建远程 PR。
