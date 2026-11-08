# 阶段 8 交接：Kaguya Taketori Monogatari 事件

## 阶段状态

**PASS**。本阶段只选择 Kaguya 的一个事件内容表面：`TaketoriMonogatariEvent`。原生事件进入了两个选项，重放了 `READ` 选项，记录了事件结果、资源账本、房间清理和 teardown。没有把第四幕、角色包、敌人包、地图或完整 Mod 初始化声明为兼容。

## 当前分支和工作区

- 分支：`main`，相对 `origin/main` ahead 7。
- 工作区在开始时已有用户未提交修改，包括 `README.md`、大量历史 `docs/` 删除和新增的 `docs/` 目录；这些修改均保留，没有 reset、clean 或 checkout 覆盖。
- 固定快照目录存在：`local-mods/workshop-snapshot-20261106-163352`。
- 固定 manifest 存在，21 个 Workshop 条目、142 个文件、总大小 6,974,558,103 bytes，内容摘要为 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 目标游戏固定为 `0.111.0`；本阶段没有使用兼容回退目录。

## 实际修改文件

- `src/SimulatorLab.Core/Mods/KaguyaStage8.cs`
  - 增加 Kaguya 阶段 8 的标量 probe、事件、奖励、资源账本、能力、错误分母和 determinism 报告契约。
  - 转换只生成标准 `EventDefinition`、`RewardDefinition` 和 `ResourceResult`，没有把 Godot 对象或 Kaguya 类型放入核心模型。
- `tools/mod-worker/KaguyaStage8.csproj`
- `tools/mod-worker/KaguyaStage8Program.cs`
  - 增加独立 worker；run manifest 记录 Mod 身份、依赖闭包、输入摘要、事件/奖励身份、错误分母和重放摘要。
- `tools/native-run/KaguyaNativeModLoop.cs`
  - 在独立 NativeRun 进程中只读加载快照内 BaseLib 与 Kaguya DLL。
  - 使用模型白名单支持事件及其必要的角色、池、Power 类型，跳过无关的完整 Kaguya 初始化表面。
  - 拦截配置写入路径到本次 `.build/runs`，并拦截 Godot ScriptManager/用户数据路径，保持正式数据隔离。
- `tools/native-run/KaguyaNativeEvent.cs`
  - 建立真实 `EventRoom` 生命周期：进入、两个选项、原生选项执行、事件结果、资源账本、房间 Exit、cleanup、teardown。
- `tools/native-run/NativeLifecycle.cs`
- `tools/native-run/NativePerformancePilot.cs`
- `tools/native-run/NativeRooms.cs`
- `tools/native-run/NativeScenario.cs`
- `tools/native-run/NativeSolverProfile.cs`
- `tools/native-run/Program.cs`
- `tools/native-run/run-native-slice.ps1`
  - 注册阶段 8 场景、Kaguya 进程隔离、事件入口和输出绑定。
- `tools/validation/TestKaguyaStage8.ps1`
  - 增加阶段 8 逐项验收脚本。
- 本交接报告。

以下关键输入未修改：固定 Workshop 快照及其 manifest、`docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`、阶段 6/7 报告、正式游戏目录、正式设置和玩家存档。

## 原生运行证据

- NativeRun build：`.build/native-build/kaguya-stage8-v22/build-manifest.json`，Release 构建 0 错误、4 个既有分析警告。
- 主进程：`.build/runs/kaguya-stage8-native-primary-event-v22-20261107`。
- 新进程重放：`.build/runs/kaguya-stage8-native-replay-event-v22-20261107`。
- 主进程和重放都为 `SliceBoundary`、退出码 0、`nativeRuntimeAvailable=true`、`terminal=battle-complete`。
- 事件身份：`EVENT.TAKETORI_MONOGATARI_EVENT`。
- 两个选项：`...options.READ`、`...options.IGNORE`；本次选择 `READ`。
- 事件结果身份：`REWARD.KAGUYA_TAKETORI_OPTION`；金币账本为 `99 -> 99`，结果没有伪造金币或卡牌获得。
- 主进程和重放的 probe digest 相同：`689abf7a485b941f4b522b35585d6e69b06fcb01d6a4254817b227be6774b767`。
- 事件生命周期证据包含：`new-run`、`kaguya-event-entered`、`kaguya-event-option`、`kaguya-event-result`、`kaguya-resource-ledger`、`kaguya-cleanup`、`kaguya-teardown`。

## 阶段 worker 证据

- 阶段报告：`.build/runs/kaguya-stage8-evaluation-v24-20261107/stage8-report.json`。
- run manifest：`.build/runs/kaguya-stage8-evaluation-v24-20261107/run-manifest.json`。
- 报告状态：`Pass`。
- determinism digest：`681c9ba43749b1b78c36151160ec7d31a41efaedfd5676b97f7db111a1fd3200`。
- run manifest 记录：`ModId=Kaguya`、`WorkshopId=3747548629`、依赖闭包 `BaseLib,Kaguya`、输入摘要、`Event=EVENT.TAKETORI_MONOGATARI_EVENT`、`Reward=REWARD.KAGUYA_TAKETORI_OPTION`。
- 错误分母为 `total=5`、`errorCount=0`；三项未覆盖能力被作为显式 Unsupported capability 记录，不冒充失败或兼容。
- 阶段输出没有复制 DLL/PCK；固定快照 manifest、Kaguya JSON、DLL 和 PCK 的 SHA-256 验收保持不变。

## 执行过的命令及结果

- `git branch --show-current; git status --short --branch`：`main`，已有用户修改保留。
- 固定快照存在性检查、manifest 读取和内容摘要检查：通过。
- `build-native-run.ps1 ... -OutputRoot .build/native-build/kaguya-stage8-v22`：通过，0 错误。
- 两次 `run-native-slice.ps1`，场景分别为 `kaguya-stage8` 和 `kaguya-stage8-replay`：均通过，退出码 0，digest 相同。
- `dotnet build tools/mod-worker/KaguyaStage8.csproj -c Release ...`：通过，0 错误。
- `dotnet tools/mod-worker/bin/Release/net9.0/KaguyaStage8.dll ...`：`Pass`，run manifest 写入成功。
- `pwsh tools/validation/TestKaguyaStage8.ps1 ...`：通过。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-build --no-restore`：`PASS all 137 contract checks`。
- `pwsh tools/validation/TestModWorker.ps1`：通过，`deterministic=True failures=5`。
- `pwsh tools/validation/TestWatcherStage6.ps1`：通过，两个安全 Unsupported 停点摘要一致。
- `pwsh tools/validation/TestRitsuStage7.ps1`：通过，`mapped=31 partial=31 unsupported=114`。

## 验收标准逐项结果

| 验收项 | 结果 |
| --- | --- |
| 一个真实原生生命周期闭环可重放 | 通过；事件进入、两个选项、READ 执行、结果、Exit、cleanup、teardown 均来自两个新进程。 |
| 事件身份、依赖、输入摘要和错误分母进入 run manifest | 通过；见 `run-manifest.json` 的 `identity`、`dependencies`、`inputDigest`、`errorDenominator`。 |
| 失败不会污染下一次新进程运行 | 通过；此前失败探针均停在独立新目录，随后主进程和 replay 新进程均通过；快照摘要未变化。 |
| 原版和已通过角色回归保持通过 | 通过；核心 137 项、Watcher 阶段 6、LexNinja2 阶段 7 均通过。 |
| UI、地图、保存语义明确停在 Unsupported | 通过；报告包含 `kaguya-combat-ui`、`kaguya-map-injection`、`kaguya-save-schema` 三项 Unsupported，均带 reason 和 evidenceRef。 |

## 已知缺口和未支持语义

- 只接入 `TaketoriMonogatariEvent` 的两个选项和 READ 分支；没有声明 Kaguya 其它事件、角色、卡牌、遗物、敌人、Boss、第四幕或地图兼容。
- 为保持本阶段范围，Kaguya 完整 Mod initializer、完整卡牌/遗物/药水池和 UI 表现层没有进入运行面；这些语义保留在明确的范围边界内。
- READ 分支的原生结果本次没有增加金币或卡牌；资源账本仍记录了 `99 -> 99`，不会以“有奖励”文字冒充资源变化。
- 没有覆盖事件 UI 渲染、地图生成/注入、正式保存和读取；这些能力在报告中显式 Unsupported。

## 下一阶段唯一建议入口

阶段 9 从 MinionLib 依赖闭包开始，只选择一个最小 Minion 生命周期，并复用本阶段的 run manifest、重放和失败隔离边界；不要扩大 Kaguya 内容面。

是否提交或推送：**否**。
