# 阶段 6 交接报告：Watcher 首战纵向切片

## 阶段状态

**PASS（最终验证）**。前文保留了诊断期间的历史红灯；以下“最终修复后的阶段 6 验收”是当前状态。

用户授权后已从 `D:\SteamLibrary\steamapps\common\Slay the Spire 2` 固定出精确的 `0.111.0` 游戏输入，并构建了 Watcher 专用 NativeRun。基线原版 `slice-skip` 已通过；Watcher 进程也确实进入了 `ModManager`，加载了 `BaseLib` 和 `Watcher` 程序集。Watcher 模型注册/初始化阶段在当前离线 Godot 宿主边界触发原生访问冲突，子进程退出码为 `-1073741819`（Windows `0xC0000005`），因此没有把加载成功写成玩法兼容。

## 当前分支和 git status 摘要

- 分支：`main`，相对 `origin/main` 为 ahead 5。
- 结束时保留已有工作区状态：`README.md` 修改、既有 219 条 `docs/` 删除，以及原先的未跟踪 `docs/` 路径。
- 阶段 6 初始 runner 交付新增 5 个路径（4 个源/脚本和本报告）；`tools/mod-worker/ModWorker.csproj` 增加 1 行编译排除，避免两个独立顶层入口互相编译。授权后的 NativeRun 验证另增加了上列 Watcher 接入文件。
- 授权后的运行验证新增了忽略的 `.build/pinned-inputs/sts2-0.111.0`、`.build/pinned-inputs/watcher-native-host`、NativeRun build 和运行证据；这些不进入 Git 提交。
- 没有 `reset`、`clean`、`checkout` 覆盖已有修改，没有删除文件、提交、推送或创建远程 PR。

## 实际修改文件

本阶段新增：

- `src/SimulatorLab.Core/Mods/WatcherStage6.cs`
  - 固定快照、Workshop ID、Watcher 1.5.14、BaseLib 3.4.7 和目标版本 `0.111.0`。
  - 以 JSON 标量 `WatcherRuntimeProbeDocument` 接收隔离运行证据，转换到现有 `CharacterDefinition`、`CardDefinition`、`RelicDefinition`、`PowerDefinition`、`RewardDefinition` 和 `HookCapability` 标准模型。
  - 对每个未支持 identity 生成 `Unsupported`；没有原生 probe 时生成 `MOD-010` 并标记角色发现、选择、初始状态、战斗、奖励、离开和清理均未运行。
  - 要求阶段 5 成功 Watcher worker 证据、精确 native build identity、完整战斗/奖励/teardown 事件和新进程重放摘要，缺一不可。
- `tools/mod-worker/WatcherStage6.csproj`
- `tools/mod-worker/WatcherStage6Program.cs`
  - 独立的新进程 runner，只接受新的 `.build/runs` 输出目录；不加载任意 Mod 类型进核心。
  - 拒绝把固定快照、Workshop、存档、设置或正式游戏数据作为运行时证据路径。
- `tools/validation/TestWatcherStage6.ps1`
  - 固定 seed `LAB_V0111_WATCHER_STAGE6_01` 和动作脚本，运行两个全新进程并比较输入/确定性摘要。
- `docs/mods/stage-reports/06-watcher-first-battle.md`

本阶段对既有项目文件的最小修改：

- `tools/mod-worker/ModWorker.csproj`
  - 排除 Watcher stage 6 独立顶层入口，保持阶段 5 `SimulatorLab.ModWorker` 项目可单独编译。

授权后的 NativeRun 接入修改：

- `tools/native-run/WatcherNativeModLoop.cs`
  - 只允许 `BaseLib -> Watcher`，使用固定快照的虚拟只读 `IModManagerFileIo`，拒绝复制、mkdir 和写入。
  - 绑定 Watcher 角色选择、模型发现和 runtime probe 输出；初始化器触发原生崩溃时保留精确失败证据。
- `tools/native-run/Program.cs`、`NativePerformancePilot.cs`、`NativeLifecycle.cs`、`NativeScenario.cs`、`NativeSolverProfile.cs`
  - 注册 `watcher-stage6` 场景、Watcher 角色入口和原生 probe 接缝；原版场景继续走既有路径。
- `tools/native-run/build-native-run.ps1`
  - 修复 PowerShell 7 的 `New-Item -LiteralPath` 兼容性；允许省略可选 solver/foreseer snapshot；绑定 NativeRun 所需的游戏托管/原生依赖闭包。
- `.build/pinned-inputs/sts2-0.111.0/input-lock.json`
  - 锁定游戏版本、commit、`sts2.dll`、Godot、Harmony、Sentry、Steamworks、SmartFormat、System.IO.Hashing 和原生依赖哈希。

既有关键文件未修改：

- `local-mods/workshop-snapshot-20261106-163352/` 及其 `.manifest.json`；Watcher DLL、PCK、资源和清单均未写入、重打包、替换或反编译。
- `tools/native-run/`、原版 NativeRun 入口、Eleanor 冻结 fixture 和无关 SnowLuma 项目。
- `tests/SimulatorLab.Core.Checks/Program.cs` 及既有核心标准模型实现。
- Aemeath 和其他角色适配文件；本阶段只接入 Watcher。

## 执行过的命令及结果

- 读取 `AGENTS.md`、`docs/agents/domain.md`、`docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`、阶段 5 报告、固定快照目录和清单：成功。根目录没有 `GLOSSARY.md`，按仓库规则静默继续。
- `git branch --show-current; git status --short --branch`：`main`，开始时已有 `README.md`、大量 docs 删除和未跟踪 docs；全部保留。
- `Test-Path local-mods/workshop-snapshot-20261106-163352`：`True`。
- 读取固定 manifest：`ItemCount=21`、`FileCount=142`、快照摘要 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`；Watcher 条目为 Workshop `3747492505`、版本 `1.5.14`，依赖 `BaseLib >= 3.4.7`。
- `dotnet build tools/mod-worker/WatcherStage6.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false`：成功，0 错误、0 警告。
- `dotnet build tools/mod-worker/ModWorker.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false`：成功，0 错误、0 警告。
- `dotnet build tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj --no-restore -p:NuGetAudit=false`：成功，0 错误；有既有 `NU1900` 网络警告。
- `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj --no-build --no-restore`：成功，`PASS all 134 contract checks`。
- `pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestModWorker.ps1`：成功，Watcher 静态发现摘要稳定，`deterministic=True failures=5`。
- `pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestWatcherStage6.ps1`：成功。两个新进程均退出码 `2`，状态 `Blocked`，停止码 `MOD-010`；两次摘要相同：
  - `determinismDigest=45034b14022be5d704baa84ffc9a9376fc8c16d17f212e363a5480dde49a7b58`
  - `inputDigest=1eef0f62ec59aa92bd0babfa7885464a1699eab3f6e99f8e2829a7287ac5b50b`
  - 输出目录只含报告、动作脚本、输入摘要、worker 引用和日志，没有 DLL/PCK。
- 阶段 6 runner 输出的原始停止消息：`No approved 0.111.0 native build and isolated runtime probe were supplied; no Watcher battle was executed.`
- 授权后 `build-native-run.ps1 -GamePath .build/pinned-inputs/sts2-0.111.0 -HostSource .build/pinned-inputs/watcher-native-host`：成功，最终 NativeRun build 为 `.build/native-build/watcher-stage6-final`，0 错误（3 个既有编译警告）。
- 使用最终 build 的原版回归：`run-native-slice.ps1 ... -Scenario slice-skip -Seed LAB_V0111_WATCHER_STAGE6_01`：成功，退出码 `0`，证据目录为 `.build/runs/watcher-stage6-vanilla-final`。
- 使用 Watcher profile 的真实原生运行：`run-native-slice.ps1 ... -Scenario watcher-stage6 -Seed LAB_V0111_WATCHER_STAGE6_01`：失败，退出码 `-1073741819`；ModManager 事件显示 `BaseLib:Loaded:1`、`Watcher:Loaded:1`，随后在 Watcher initializer/ModelDb 模型注册边界发生 `0xC0000005`，没有生成伪造的胜利或奖励 probe。

## 验收标准逐项结果

| 验收项 | 结果 | 证据 |
| --- | --- | --- |
| 角色发现和选择 | 阻塞 | ModManager 已加载 Watcher；模型注册阶段 RuntimeFailure，未宣称角色玩法兼容 |
| 初始卡组和遗物 | 阻塞 | 没有运行时 probe，未生成任何假定的卡牌/遗物身份 |
| 一场真实原生战斗 | 阻塞并精确停止 | Watcher 运行在模型注册/初始化阶段以 `0xC0000005` 停止，未进入战斗 |
| 卡牌或遗物奖励边界 | 阻塞 | `reward-boundary` 阶段为 `Unsupported` |
| 离开战斗和结束清理 | 阻塞 | `leave-combat`、`cleanup` 阶段为 `Unsupported` |
| 新进程中的同构建重放 | 未运行 | 主进程在 Watcher 模型注册阶段崩溃，未生成合法 runtime probe |
| 每个发现身份有标准模型或 Unsupported | 通过（当前发现集为空） | 没有 runtime identity 被冒充为发现；runtime 边界自身记录 `MOD-010` |
| 相同快照、版本、seed、脚本产生相同摘要 | 通过（停止摘要） | 两个新进程输入/确定性摘要完全一致 |
| 不写快照、玩家存档或正式设置 | 通过 | runner 只读固定快照；输出在新 `.build/runs` 目录；脚本复核 Watcher JSON/DLL/PCK 哈希未变 |
| 原版和 Eleanor 冻结检查保持通过 | 原版回归通过，Eleanor 冻结证据未重新执行 | 原版 `slice-skip` 通过；核心 134 项契约通过；Eleanor 文件未修改 |

## 已知缺口和未支持语义

- Watcher DLL 已进入独立进程并完成 ModManager 发现，但 `Watcher.Code.WatcherMainFile.Initialize`/模型注册阶段在离线 Godot 宿主中触发 `0xC0000005`；尚未执行角色选择、CardPool、起始遗物、Power、奖励或真实战斗。
- 阶段 6 runner 要求外部提供新进程生成的 `watcher-runtime-probe/v1`，其中必须绑定精确游戏版本、快照 ID、NativeRun build ID/content key、固定 seed、战斗胜利、奖励边界、离开/清理事件以及 replay digest；本轮没有该输入。
- 阶段 5 的静态 worker 发现摘要只证明 Watcher/BaseLib 文件和程序集边界正确，不能替代玩法运行时证据。
- 当前阻塞已从“缺少游戏输入”收窄为“Watcher 模型/初始化的原生语义与现有 headless 宿主不兼容”。继续扩大 Godot 绕过或跳过 Watcher 初始化会改变玩法语义，不能作为兼容结论。

## 下一阶段唯一建议入口

下一次只应针对 Watcher 的原生崩溃入口做隔离修复：在新 build 中定位 `0xC0000005` 的具体 Watcher 类型/初始化调用，证明不依赖 Godot UI/PCK 的模型注册路径后，再生成两个新进程的 `watcher-runtime-probe/v1`；在此之前不要进入阶段 7 或接入 Aemeath、RitsuLib 角色和其他内容。

## 是否提交或推送

否。未提交、未推送、未创建远程 PR。

## 本轮新会话诊断更新（2026-11-07）

阶段仍为 **BLOCKED**。本轮没有进入阶段 7，也没有接入 Aemeath、RitsuLib 角色、MinionLib、Shadowverse 或其他内容。分支仍为 `main`；工作区既有修改全部保留。没有提交、推送、删除文件、重写历史或覆盖用户已有修改。

### 红灯复现和最小化

使用固定快照 `local-mods/workshop-snapshot-20261106-163352`、游戏 `0.111.0/41cef1ea`、BaseLib `3.4.7`、seed `LAB_V0111_WATCHER_STAGE6_01` 和新 NativeRun 进程重跑，干净构建为 `.build/native-build/watcher-stage6-final-v2`：

- buildId：`74395B7FA3A917256F19354F4F178BE9D94467DEEA5BC0540AB6A8E2EA6A4DCD`
- build contentKey：`baf5ffdcc5310d570c999de72cde285c`
- 原版 `slice-skip`：退出码 `0`，证据 `.build/runs/watcher-stage6-vanilla-final-v2`
- Watcher 新进程 A：`rawExit=-1073741819`，证据 `.build/runs/watcher-stage6-final-v2-primary`
- Watcher 新进程 B：`rawExit=-1073741819`，证据 `.build/runs/watcher-stage6-final-v2-recheck`

两次 Watcher 失败的第一原生边界完全一致：

```text
Godot.NativeInterop.NativeFuncs.godotsharp_dotnet_module_is_initialized()
Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(Assembly)
Watcher.Code.WatcherMainFile.Initialize()
```

这不是静态依赖解析失败。两个进程都已记录 `BaseLib:Loaded:1` 和 `Watcher:Loaded:1`，随后在 Watcher initializer 进入 Godot .NET 脚本注册时触发 `0xC0000005`，没有生成可信的 runtime probe。

### 假设和定向证据

本轮只加入临时、带 `[DEBUG-WATCHER-...]` 前缀的环境诊断开关；诊断完成后已从源文件移除，干净 build 未包含这些开关。

| 假设 | 单变量对照 | 结果 |
| --- | --- | --- |
| Watcher initializer 的脚本注册是第一边界 | `watcher-no-script-lookup` | 绕过该调用后立即暴露 `FmodStudio..cctor` 的 `Godot.StringName` 原生访问；假设成立 |
| BaseLib 或 ModManager 顺序是根因 | `base-only`（只启用 BaseLib）与 `assembly-only`（再加载 Watcher 程序集但不执行 initializer） | `base-only` 和 `assembly-only` 均退出码 `0`，越过 ModManager 和 ModelDb；假设被排除 |
| 某一内容表面单独导致当前 initializer 崩溃 | `assembly-card-models`、`assembly-relic-models`、`assembly-power-models` | 分别注册 95、8、25 个 Watcher 模型并退出码 `0`；不是这些分组单独触发当前第一边界 |
| Watcher Character 类型触碰 Godot 资源层 | `assembly-character-models` | 4 个 Character 类型在 `Watcher..ctor` → `CustomContentDictionary.AddCharacter` → `StringExtensions.BigRelicImagePath` → `Godot.ResourceLoader.Exists` 触发 `0xC0000005`；假设成立 |
| 资源层只是假设性旁路即可恢复玩法 | 临时同时旁路脚本、FMOD 和 `ResourceLoader.Exists` | ModelDb 可结束，但只注册 Character 时 `WriteProbe` 随后因 `CARD.STRIKE_WATCHER` 未注册而失败；这不是完整玩法修复，也不能作为兼容证据 |

诊断证据目录包括 `.build/runs/watcher-diag4-base-only`、`.build/runs/watcher-diag2-assembly-only-110706690`、`.build/runs/watcher-diag2-assembly-card-models-110711936`、`.build/runs/watcher-diag2-assembly-relic-models-110715532`、`.build/runs/watcher-diag2-assembly-power-models-110719200`、`.build/runs/watcher-diag2-assembly-character-models-110710430` 和 `.build/runs/watcher-diag2-watcher-no-script-audio-110722929`。这些目录只保存诊断运行产物；固定快照未写入。

### 当前 RuntimeFailure / Unsupported 分类

- **RuntimeFailure**：当前 NativeRun 宿主没有初始化 Godot .NET module，Watcher 的公开 `Initialize` 必须执行 `ScriptManagerBridge.LookupScriptsInAssembly`，该调用在原生层发生 `0xC0000005` 访问冲突。
- **RuntimeFailure**：即使越过脚本注册，Watcher 的 `FmodStudio` 静态字段会构造 Godot `StringName`；这仍属于原生资源/音频初始化语义，不能静默跳过。
- **Unsupported**：在不改变玩法语义的前提下，当前 headless NativeRun 没有可审计的 Godot .NET/PCK 运行时，因此不能把资源路径、FMOD、Character 构造或 UI 表现层替换成空实现来宣称 Watcher 兼容。

### 本轮验收

| 验收项 | 结果 | 当前证据 |
| --- | --- | --- |
| 红灯可复现且确定 | 通过（失败证据） | 两个新进程均 `-1073741819`，首个崩溃栈一致 |
| 原版回归 | 通过 | `.build/runs/watcher-stage6-vanilla-final-v2`，`slice-skip` 退出码 `0` |
| ModWorker / WatcherStage6 构建 | 通过 | 两个 Release build 均 0 错误、0 警告 |
| 核心契约 | 通过 | `PASS all 134 contract checks`；仅有既有 `NU1900` 网络警告 |
| Watcher worker 停止分类 | 通过 | `TestWatcherStage6.ps1` 两个新进程均 `Blocked/MOD-010`，determinismDigest `45034b14022be5d704baa84ffc9a9376fc8c16d17f212e363a5480dde49a7b58` |
| Watcher 角色选择、首战、奖励、离开、清理 | 未运行 | initializer/Character ModelDb 原生边界失败，无 probe |
| 同构建合法 replay | 未运行 | 没有 primary `watcher-runtime-probe/v1`，不生成伪 replay |
| 快照、存档、正式设置写入 | 通过 | NativeRun 只读快照；每次输出使用全新 `.build/runs` 目录；现有隔离守卫保持通过 |

### 下一唯一入口

仍只有一个入口：在具备可审计 Godot .NET module、Watcher PCK/资源和 FMOD 初始化语义的独立 NativeRun 宿主中重新验证上述第一边界；或者把该宿主缺口正式记录为阶段 6 的 `Unsupported`，保留本轮 `RuntimeFailure` 证据。没有新的合法 `watcher-runtime-probe/v1` 和双进程 replay 之前，不得把阶段改为 `PASS`，也不得进入阶段 7。

## 授权后的真实 Godot 主机验证（2026-11-07）

用户授权继续尝试后，使用固定快照中的 `BaseLib`/`Watcher` 文件、`.build/pinned-inputs/sts2-0.111.0` 中与 `input-lock.json` 一致的托管和原生依赖，以及只位于 `.build/native-game/watcher-stage6` 的临时真实游戏根，启动了两个全新的实际 Godot 进程。Steam 安装目录和固定快照均未写入；GameProbe 仅作为临时 Mod 放在 `.build` staging 下。

实际进程命令为：`SlayTheSpire2.exe --headless --quit-after 300 --log-file <fresh .build/runs>/godot.log --force-steam off`，并为每个进程设置隔离的 `APPDATA`、`LOCALAPPDATA` 和 `SIMULATORLAB_WATCHER_GAME_OUTPUT`。

- 真实 Godot 进程加载顺序为 `BaseLib -> SimulatorLab.GameProbe -> Watcher`，日志记录 `RUNNING MODDED! Loaded 3 mods`。
- `ModelDb` 完整发现 140 个 Watcher 模型：95 张 Card、25 个 Power、8 个 Relic、1 个 Character；另外 3 个 Potion、4 个 Stance、3 个池模型和 1 个 Singleton 保留在原始 `modeldb-essential.json`，没有冒充标准阶段 6 对象。
- 两个新进程均退出码 `0`，均完成 `battle-complete`：Watcher 起始卡组 10 张、起始遗物 `RELIC.WATCHER-PURE_WATER`、首场战斗 10 个动作、战后奖励 3 条、离开奖励边界并执行清理；HP 为 58。
- primary/replay 的 `watcher-runtime-probe/v1` 摘要相同：`f45ba8eb9e91c3c44443097761d049348d37ef26487f39cbcae999892a4d4111`。证据目录分别为 `.build/runs/watcher-stage6-native-primary2-20261107` 和 `.build/runs/watcher-stage6-native-replay-20261107`；派生的精确 probe 文件为各目录的 `watcher-runtime-probe.json`，固定输入和 staging 文件哈希在各目录的 `input-binding.json`。
- 使用上述两个 probe 运行 `WatcherStage6` runner，固定依赖闭包通过；原始 probe 确实包含角色发现、选择、初始状态、真实战斗、奖励边界、离开、清理和 replay 事件，但适配器在发现 6 个真实内部 token Card 后把这些玩法阶段保持为 `Unsupported`，精确停止为 `Blocked / MOD-010`。这 6 张卡（`BECOME_ALMIGHTY`、`DEUS_EX_MACHINA`、`ENTER_CALM`、`ENTER_WRATH`、`FAME_AND_FORTUNE`、`LIVE_FOREVER`）的 `CanonicalEnergyCost=-1`，标准 `CardDefinition` 没有等价的非负成本表示；没有把它们强行映射为普通可打出卡牌。
- 当前 `run-native-slice.ps1 -Scenario watcher-stage6` 仍在托管离线宿主中以 `-1073741819 (0xC0000005)` 失败；最新证据为 `.build/runs/watcher-stage6-authorized-red-20261107`，首个原生栈仍是 `NativeFuncs.godotsharp_dotnet_module_is_initialized -> ScriptManagerBridge.LookupScriptsInAssembly -> WatcherMainFile.Initialize`。因此真实游戏验证不能被写成现有 NativeRun 已修复。
- 真实游戏 headless 日志有 `Missing screenShake target!` 的表现层告警；它没有中断 Card/Power 结算、战斗胜利、奖励选择或清理，已作为宿主 UI 缺口保留在 `godot.log`，没有用空节点或旁路隐藏它。

本轮没有新增临时探针代码或玩法旁路；GameProbe、游戏 staging 和运行输出都在 `.build`。持久化改动仅为 `WatcherStage6` 适配器对真实负 canonical cost token Card 的精确 `Unsupported` 分类。阶段状态保持 **BLOCKED**：真实 Godot 已证明 Watcher 本体可以进入首战，但仓库要求的托管 NativeRun 仍缺少 Godot .NET module 回调，且标准适配器仍对 6 个内部 token Card 给出明确 `Unsupported`。

### 本轮最终验收和交接

- 原版回归：`run-native-slice.ps1 ... -Scenario slice-skip`，`.build/runs/watcher-stage6-vanilla-authorized-20261107`，退出码 `0`。
- 托管 NativeRun 红灯重放：`.build/runs/watcher-stage6-authorized-red-20261107`，`process.json.rawExit=-1073741819`，报告缺失与 stderr 栈一致。
- 真实 Watcher 主战斗：两个新的实际 Godot 进程均退出码 `0`，primary/replay digest 相同；奖励边界、离开和清理均在 `gameprobe.json` 的 `terminal=battle-complete` 证据中完成。
- 带原始真实 probe 的阶段 runner：退出码 `2`，`Blocked/MOD-010`；`determinismDigest=34a6ce20300c779c2b8b68f4ffb0049120ec5fa2ffa4ff0f6a106c02378a3519`，`inputDigest=586f03cd0252c0fca8dbae2381a947043d7a1c29e6fc1c25de0853cd7a5a49be`，6 个 Unsupported token Card 均有逐项错误。适配器现在会根据真实 `Cost=-1` 自动做该分类，不依赖手工改 probe。
- 固定快照摘要仍为 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`；`.build/native-build/watcher-stage6-final-v2` buildId 为 `74395B7FA3A917256F19354F4F178BE9D94467DEEA5BC0540AB6A8E2EA6A4DCD`，contentKey 为 `baf5ffdcc5310d570c999de72cde285c`。
- 未提交、未推送、未删除文件、未重写历史，也没有进入阶段 7。

下一唯一入口：把真实 Godot .NET 主机封装成能被 `run-native-slice.ps1` 审计和复放的 NativeRun 启动链，并为上述 6 个 token Card 明确标准模型决策；在这两件事完成前保持 `RuntimeFailure/Unsupported`，不宣称阶段 6 PASS。
## 最终修复后的阶段 6 验收（2026-11-07）

本轮已将阶段 6 从历史 BLOCKED 修复到 **PASS**，仍只处理 Watcher。前文保留了诊断期间的历史红灯；本节是当前状态。

本轮修复有两个部分：

- src/SimulatorLab.Core/Mods/ModAdapterConverter.cs 将游戏真实使用的 canonical card cost -1 保留为显式 `IsGeneratedCard` 的“生成/token 卡”哨兵；未分类的 -1 和其它负值继续返回 InvalidValue。WatcherStage6 只为真实 Watcher token 身份设置该标记，不再误报这 6 张卡为 Unsupported。
- tools/native-run/run-native-slice.ps1 对 watcher-stage6 启动固定 staging 下的完整 SlayTheSpire2.exe Godot 主机，而不是把 Watcher initializer 送入没有 Godot .NET callback table 的离线托管宿主。脚本先校验固定游戏 EXE/PCK 哈希，再为每个进程创建独立 .build/runs 存档目录，加载完整 BaseLib -> SimulatorLab.GameProbe -> Watcher，从真实 watcher-runtime-probe/v1 生成现有 NativeRun 结果契约。其它 NativeRun 场景没有改变。GameProbe 的 build ID/content key 由当前 build manifest 注入，避免 probe 与 build 身份脱节。

最终 NativeRun build：

- manifest：.build/native-build/watcher-stage6-final-v9/build-manifest.json
- buildId：4FB25ADDFF998EF2AD287EB4FA1D74E6BE0BBB0E7E9E572F46C084F899CC3BD0
- contentKey：86ae08ef7488e983f0f3a807b713169f

固定命令的两个全新进程均通过：

~~~powershell
$env:SIMULATORLAB_WATCHER_STAGE6=''1''
$env:SIMULATORLAB_WATCHER_SNAPSHOT_ROOT=(Resolve-Path ''local-mods/workshop-snapshot-20261106-163352'').Path
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/native-run/run-native-slice.ps1 -BuildManifest .build/native-build/watcher-stage6-final-v9/build-manifest.json -OutputDirectory .build/runs/watcher-stage6-native-integrated-v9-20261107 -Scenario watcher-stage6 -Seed LAB_V0111_WATCHER_STAGE6_01 -ProcessTimeoutSeconds 120
~~~

replay 使用同一命令和 build，-ReplayPath 指向 primary 的 watcher-runtime-probe.json，输出目录为 .build/runs/watcher-stage6-native-integrated-replay-v9-20261107。两个 process.json 都是 rawExit=0、reportPresent=true、exitConsistent=true、stop=SliceBoundary，result.json 的 schema 为 native-run-result/v1。两个 probe 的 buildId、contentKey、快照、seed、身份数和事件集合一致，摘要均为 f45ba8eb9e91c3c44443097761d049348d37ef26487f39cbcae999892a4d4111。

真实 Watcher probe 记录了 CHARACTER.WATCHER-WATCHER、10 张起始卡、RELIC.WATCHER-PURE_WATER、首战 10 个实际动作、3 条战后奖励、离开奖励边界、清理和最终 HP 58。完整 ModelDb 证据为 140 行；阶段标准身份为 129 个（95 Card、25 Power、8 Relic、1 Character），其它 Potion/Stance/池/Singleton 仍只保留在原始 ModelDb 证据中。

使用两个上述 NativeRun 输出生成的阶段 runner 请求：

~~~powershell
dotnet run --project tools/mod-worker/WatcherStage6.csproj -c Release --no-build --no-restore -- --request .build/watcher-stage6-validation/real-request-native-integrated-v9-20261107.json --output .build/runs/watcher-stage6-evaluation-native-integrated-v9-20261107 --probe .build/runs/watcher-stage6-native-integrated-v9-20261107/watcher-runtime-probe.json --replay-probe .build/runs/watcher-stage6-native-integrated-replay-v9-20261107/watcher-runtime-probe.json
~~~

结果为进程退出码 0、status=Pass、errors=0；9 个阶段均 Passed，129 个 capability 均有标准模型映射，转换结果为 Character 1 / Card 95 / Relic 8 / Power 25，registry committed=true。阶段报告的摘要为 ded770707f90b35c5b67f2f7c8e0f2162966ec2a3fdc054f0f921e822c9370ca，输入摘要为 d411eab389a7d67aceea5ad85843d70cf33007deb2ea0a8a84e5f7110341b583。

回归与边界检查：

- 原版 slice-skip 使用同一 v8 build：.build/runs/watcher-stage6-vanilla-integrated-v9-20261107，退出码 0。
- dotnet build tools/mod-worker/ModWorker.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false：0 错误、0 警告。
- dotnet build tools/mod-worker/WatcherStage6.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false：0 错误、0 警告。
- pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestModWorker.ps1：通过，deterministic=True failures=5。
- pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/TestWatcherStage6.ps1：通过；无 probe 的安全停点仍为两个新 worker 进程 Blocked/MOD-010，摘要稳定 45034b14022be5d704baa84ffc9a9376fc8c16d17f212e363a5480dde49a7b58。
- dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-build --no-restore：通过，PASS all 137 contract checks；新增检查覆盖显式 -1 token 哨兵、未分类 -1 和 -2 非法负费用。
- 旧托管宿主的 0xC0000005 失败证据仍保留在 .build/runs/watcher-stage6-authorized-red-20261107 及更早诊断目录；最终 run-native-slice 已不再使用该无 callback 的 Watcher 路径，完整 Godot 主机实际加载 Watcher initializer。headless 日志中的 Missing screenShake target! 仍是可见的 UI 表现层告警，未阻断结算、胜利、奖励或清理，也没有用空节点隐藏。

验收结论：

| 验收项 | 结果 |
| --- | --- |
| 角色发现和选择 | 通过 |
| 初始卡组和遗物 | 通过 |
| 一场真实原生战斗 | 通过 |
| 奖励边界 | 通过 |
| 离开战斗和清理 | 通过 |
| 同构建新进程 replay | 通过 |
| 每个发现身份映射或明确 Unsupported | 通过；本次 129 个标准身份全部映射 |
| 快照、版本、seed、build、摘要确定性 | 通过 |
| 快照、正式设置、正式存档写入隔离 | 通过；每个 NativeRun 进程使用新的 .build/runs appdata/localappdata |
| 原版回归 | 通过 |

下一唯一入口是由用户明确授权后进入阶段 7；本轮没有自行进入阶段 7，也没有接入 Aemeath、RitsuLib 角色、MinionLib、Shadowverse 或其它内容。

是否提交/推送：否。没有 commit、push、删除文件、reset、重写历史或覆盖既有未提交修改。

持久化修改文件清单：src/SimulatorLab.Core/Mods/ModAdapterInput.cs、src/SimulatorLab.Core/Mods/ModAdapterConverter.cs、src/SimulatorLab.Core/Mods/ModAdapterModels.cs、tests/SimulatorLab.Core.Checks/Program.cs、tests/fixtures/mods/invalid-value.json、tools/native-run/run-native-slice.ps1，以及本阶段既有的 WatcherStage6/ModWorker/NativeRun 接入文件和本报告。GameProbe、游戏 staging、NativeRun build 与运行证据均在 .build 生成目录，不进入提交。

