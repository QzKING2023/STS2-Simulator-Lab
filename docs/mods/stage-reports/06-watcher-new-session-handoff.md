# Watcher 阶段 6 新会话交接

## 目标

继续阶段 6，只处理 Watcher。目标仍是固定快照 `workshop-snapshot-20261106-163352`、游戏 `0.111.0`、BaseLib `3.4.7`、固定 seed 和固定动作脚本，最终完成角色选择、初始卡组/遗物、真实战斗、奖励边界、离开战斗、清理和同构建双进程重放。

用户已在测试版游戏中同时加载并运行全部 Mod，确认安装环境没有因为游戏版本或 Mod 组合而拒绝加载。新会话应把这条事实作为环境前提，同时继续使用仓库固定快照和哈希绑定的运行输入；不要重新扩展到其他角色。

## 当前状态

- 分支：`main`，已有工作区修改必须保留；没有提交或推送。
- 固定 Workshop 快照未修改，摘要：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 已复制游戏最小输入到 `.build/pinned-inputs/sts2-0.111.0`，版本 `v0.111.0`，commit `41cef1ea`，`sts2.dll` 哈希匹配 `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`。
- 已锁定 NativeRun 宿主源到 `.build/pinned-inputs/watcher-native-host/host-lock.json`。
- Watcher 阶段 runner、ModWorker 和核心 134 项契约均可构建/通过。
- 原版基线 NativeRun `slice-skip` 在最终构建上通过，退出码 `0`，证据目录：`.build/runs/watcher-stage6-vanilla-final`。

## 已确认的真实失败

失败命令的核心形式：

```powershell
$env:SIMULATORLAB_WATCHER_STAGE6='1'
$env:SIMULATORLAB_WATCHER_SNAPSHOT_ROOT=(Resolve-Path 'local-mods/workshop-snapshot-20261106-163352').Path
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/native-run/run-native-slice.ps1 `
  -BuildManifest .build/native-build/watcher-stage6-final/build-manifest.json `
  -OutputDirectory .build/runs/watcher-stage6-debug-<fresh-id> `
  -Scenario watcher-stage6 `
  -Seed LAB_V0111_WATCHER_STAGE6_01 `
  -ProcessTimeoutSeconds 120
```

已观察到的信号：

- `process.json.rawExit = -1073741819`，即 Windows `0xC0000005`。
- Watcher 进程已经进入独立 NativeRun 和 ModManager。
- 事件显示 `BaseLib:Loaded:1`、`Watcher:Loaded:1`。
- 崩溃发生在 Watcher 初始化或将 Watcher 模型纳入 `ModelDb` 的阶段；没有生成可信的角色选择、战斗、奖励或 replay probe。
- 失败证据：`.build/runs/watcher-stage6-primary-v18/stderr.log`。

这不是静态依赖解析失败，也不是“加载成功即可兼容”。在定位并修复前，阶段状态保持 `BLOCKED`。

## 新会话第一轮

按 `/diagnosing-bugs` 执行，先建立并收紧红灯反馈回路：

1. 读取 `AGENTS.md`、`docs/agents/domain.md`、`docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`、本文件和 `docs/mods/stage-reports/06-watcher-first-battle.md`。
2. 读取当前 `git branch` 和 `git status`，保留已有修改。
3. 用全新 `.build/runs` 目录重跑上面的 `watcher-stage6` 命令，确认仍是 `0xC0000005`。
4. 记录最后一个成功边界和第一个崩溃边界；不要先猜测或改动多个初始化步骤。
5. 建立最小化对照：原版 `slice-skip`、只加载 BaseLib、加载 BaseLib 加 Watcher 程序集但不注册模型、注册 Character、注册 Card/Relic/Power 分组。每次只改变一个变量，所有运行使用新进程和新输出目录。
6. 对每个候选原因写出可证伪预测，再做带 `[DEBUG-WATCHER-...]` 前缀的定向日志；修复后移除这些日志。

优先检查：

- `Watcher.Code.WatcherMainFile.Initialize` 内触发的 Harmony/资源/音效初始化；
- Watcher 模型类型的静态构造是否触碰 Godot `StringName`、PCK 或 UI 节点；
- `ModelDb.Init` 接收的 Watcher 类型集合是否包含表现层、测试类或不应进入离线模型注册的类型；
- BaseLib 的原始内容发现补丁和 Watcher 初始化顺序是否重复注册或访问未初始化的 Godot 全局状态。

## 修复边界

- 只接入 Watcher；不接入 Aemeath、RitsuLib 角色、MinionLib 或其他 Mod。
- 固定快照只读；不从实时 Workshop 目录取运行时输入。
- 不把 Godot 对象、任意 Mod 类型或外部 DTO 放进 `SimulatorLab.Core`。
- 不用跳过 Watcher 初始化、空模型、静默忽略异常或“加载成功”替代玩法兼容。
- 若最小修复仍无法安全绕过 Godot 原生边界，输出精确 `RuntimeFailure`/`Unsupported`，保留失败证据并停止阶段 6。

## 完成条件

只有以下证据齐全才可将阶段改为 `PASS`：

- 同一固定 build 中两个新进程都完成 Watcher 角色选择、初始状态、真实战斗、奖励边界、离开和清理；
- 每个实际发现的卡牌、遗物、Power、Hook 和奖励身份都有标准模型映射或明确 `Unsupported`；
- 两次 probe 的快照、版本、buildId、contentKey、seed、动作脚本和摘要一致；
- 快照、存档和正式设置没有写入；
- 原版回归和既有 Eleanor 冻结证据保持有效；
- 新交接报告记录命令、证据路径、缺口和是否提交/推送。

## 路由建议

本轮先走 `/diagnosing-bugs` 的重现、最小化、假设、定向 instrumentation 和回归验证。若修复需要改变阶段边界或新增长期设计，再在同一工作区进入 `/grill-with-docs`，之后按需要转 `/to-spec` 与 `/to-issues`；不要在没有明确修复证据时直接进入下一阶段。

## 本轮交接结果（2026-11-07）

阶段历史红灯已在本轮最终验证中解除。分支仍为 `main`；本轮只处理 Watcher，没有接入其他角色或复合 Mod，没有提交、推送、删除文件、重写历史或覆盖已有未提交修改。

红灯已在干净 NativeRun build `.build/native-build/watcher-stage6-final-v2` 和两个新进程中重现：

- buildId：`74395B7FA3A917256F19354F4F178BE9D94467DEEA5BC0540AB6A8E2EA6A4DCD`
- build contentKey：`baf5ffdcc5310d570c999de72cde285c`
- 原版回归：`.build/runs/watcher-stage6-vanilla-final-v2`，`slice-skip` 退出码 `0`
- Watcher A：`.build/runs/watcher-stage6-final-v2-primary`，`rawExit=-1073741819`
- Watcher B：`.build/runs/watcher-stage6-final-v2-recheck`，`rawExit=-1073741819`

两个 Watcher 进程均在以下首个原生边界停止：

```text
Godot.NativeInterop.NativeFuncs.godotsharp_dotnet_module_is_initialized()
Godot.Bridge.ScriptManagerBridge.LookupScriptsInAssembly(Assembly)
Watcher.Code.WatcherMainFile.Initialize()
```

定向最小化已完成。只启用 BaseLib 的 `.build/runs/watcher-diag4-base-only` 和 `BaseLib + Watcher 程序集（不执行 initializer）`、Watcher Card 95 个、Relic 8 个、Power 25 个模型分组均可越过 ModelDb；Watcher Character 4 个类型在 `ResourceLoader.Exists` 触发相同原生边界。临时绕过脚本注册后，第二边界是 `FmodStudio` 静态 `StringName`；临时再绕过资源检查只说明宿主可以继续走托管流程，不能作为玩法兼容修复。所有诊断开关和 `[DEBUG-WATCHER-...]` 日志代码已从源文件移除，诊断运行证据仍保留在 `.build/runs/watcher-diag2-*`、`.build/runs/watcher-diag3-*` 和 `.build/runs/watcher-diag4-base-only`。

本轮验证结果：`ModWorker` 和 `WatcherStage6` Release 构建通过；核心契约 `PASS all 134 contract checks`；`TestWatcherStage6.ps1` 两个 worker 进程均为 `Blocked/MOD-010`，确定性摘要保持 `45034b14022be5d704baa84ffc9a9376fc8c16d17f212e363a5480dde49a7b58`。角色选择、初始卡组/遗物、真实战斗、奖励、离开、清理和合法 replay 均未运行，因为没有可信 `watcher-runtime-probe/v1`。

修改文件仍限于阶段 6 既有 Watcher 接入、NativeRun 接缝和本报告；本轮没有留下诊断控制修改。下一唯一入口是提供能初始化 Godot .NET module、Watcher PCK/资源和 FMOD 语义的可审计 NativeRun 宿主，或正式接受当前 `RuntimeFailure`/`Unsupported` 停点。没有双进程 Watcher probe/replay 前不得进入阶段 7。是否提交/推送：否。

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

### 最终交接

- 原版回归：`.build/runs/watcher-stage6-vanilla-authorized-20261107`，退出码 `0`。
- 托管 NativeRun 红灯重放：`.build/runs/watcher-stage6-authorized-red-20261107`，`process.json.rawExit=-1073741819`，报告缺失与 stderr 栈一致。
- 真实 Watcher 主战斗：两个新的实际 Godot 进程均退出码 `0`，primary/replay digest 相同；奖励边界、离开和清理均在 `gameprobe.json` 的 `terminal=battle-complete` 证据中完成。
- 带原始真实 probe 的阶段 runner：退出码 `2`，`Blocked/MOD-010`；`determinismDigest=34a6ce20300c779c2b8b68f4ffb0049120ec5fa2ffa4ff0f6a106c02378a3519`，`inputDigest=586f03cd0252c0fca8dbae2381a947043d7a1c29e6fc1c25de0853cd7a5a49be`，6 个 Unsupported token Card 均有逐项错误。适配器现在会根据真实 `Cost=-1` 自动做该分类，不依赖手工改 probe。
- 固定快照摘要仍为 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`；`.build/native-build/watcher-stage6-final-v2` buildId 为 `74395B7FA3A917256F19354F4F178BE9D94467DEEA5BC0540AB6A8E2EA6A4DCD`，contentKey 为 `baf5ffdcc5310d570c999de72cde285c`。
- 分支仍为 `main`；没有提交、推送、删除文件、重写历史，也没有进入阶段 7。下一唯一入口是把真实 Godot .NET 主机封装成能被 `run-native-slice.ps1` 审计和复放的 NativeRun 启动链，并为 6 个 token Card 明确标准模型决策。
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

