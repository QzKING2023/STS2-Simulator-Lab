# M2-1 通用终态运行协议阶段报告

## 阶段状态

**PARTIAL**。M2-1 的通用控制运行协议、终态识别、四路输入契约和失败分类已经实现并通过控制验收；奖励边界已经在真实原版流程中观察到，但固定 seed 的奖励后路径在进入下一节点时预算停止，未宣称奖励后仍能在同一运行到达 RunVictory/Death。没有接入新的角色 Mod。

## 票据与工作区

- 子票：#3，开始前确认 `OPEN`、`ready-for-agent`，唯一阻塞 #2 已 `CLOSED`；已按仓库约定认领。
- 分支：`main`。
- HEAD：`7718d76399da8b515794fe5d68c2bfdaba59ebbd`。
- Git status：保留开始前已有的 `README.md`、文档、`GLOSSARY.md`、`docs/adr/`、M2-0 报告和维护脚本修改；本阶段新增/修改见下文；未 reset、clean、checkout、提交或推送。

## 固定输入

- 游戏：`0.111.0`，commit `41cef1ea`；构建：`.build/native-build/m2-1-protocol-v5/build-manifest.json`。
- Workshop 快照：`local-mods/workshop-snapshot-20261106-163352`。
- 快照内容 SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 快照 manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- 控制 canonical input：seed `LAB_M2_1_BASELINE_01`，Mod `vanilla@0.111.0`，依赖闭包为空，worker `SimulatorLab.NativeRun`，adapter `native-run/v1`，collector `isolated NativeRun/GameProbe; native-trace/v1`。
- 每次运行均使用新的 `.build/runs` 子目录并写入 `canonical-input.json`、`input-summary.json`、`run-manifest.json`。

## 实际修改

- `src/SimulatorLab.Core/NativeRunExecution.cs`：增加 `RuntimeFailure`、`StrategyStopped`、`ProcessCrashed`，严格禁止把非 `Completed` 的结果提升为真实终态，并保留明确退出码和异常类型。
- `src/SimulatorLab.Core/RunEvidenceContract.cs`：增加 canonical input、run manifest、生命周期/状态/RNG/trace digest 及 primary/repeat/replay/failure 比较契约。
- `tools/native-run/Program.cs`：增加真实 Death 终态检查、自然战斗推进、奖励/清理边界以及可控失败注入；没有模拟死亡或注入结果。
- `tools/native-run/NativeScenario.cs`：登记 M2-1 场景和失败控制。
- `tools/native-run/run-native-slice.ps1`：绑定精确游戏/快照输入，写入运行证据和失败进程 manifest；无 `result.json` 的进程崩溃也记录为 `ProcessCrashed`。
- `tools/native-run/run-native-contract.ps1`：编排四个独立进程并比较完整状态、RNG、trace、生命周期和 canonical input。

关键未修改输入：固定 Workshop 目录、DLL、PCK、资源和 manifest；没有修改 Mod 原有代码或二进制。没有新增角色、卡牌、遗物或 Mod adapter。

## 执行与证据

| 命令/检查 | 退出码 | 证据 |
|---|---:|---|
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | 输出 `PASS all 137 contract checks` |
| `run-native-contract.ps1`，v5，`natural-death` | 0 | `.build/runs/m2-1-control-v5-20261108/contract-summary.json` |
| 原版 `run-native-slice.ps1`，`slice-skip`，seed `LAB_V0111_STAGE10_M1_20261108` | 0 | `.build/runs/m2-1-original-slice-v5-20261108/run-manifest.json` |
| `TestModWorker.ps1` | 0 | `.build/runs/mod-matrix-20261108/`，deterministic `492e1a020377b4e1618341874611fec31c5bc9496f9219db2d43c763b64af206` |
| `TestWatcherStage6.ps1` | 0 | `MOD-010` Unsupported，确定性 digest `45034b14022be5d704baa84ffc9a9376fc8c16d17f212e363a5480dde49a7b58` |
| `TestRitsuStage7.ps1` | 0 | digest `ad0a70dbd7791f5626df713d27234b86574f4d59a4c524458ca5810dcffb684d` |
| `TestKaguyaStage8.ps1` | 0 | digest `681c9ba43749b1b78c36151160ec7d31a41efaedfd5676b97f7db111a1fd320` |
| `TestMinionStage9.ps1` | 0 | digest `cf5618439be7f5567ed461bf45fee33fbc1f2b099a6d6154b577526a2f6710ee` |
| `TestMinionStage10.ps1` | 0 | digest `0731c8af4ea1323971580234d3f46325a6aebf50be74e6e13721e2fde626d87c` |

## 四路终态与 digest

合同目录：`.build/runs/m2-1-control-v5-20261108/`。

| 路径 | 终态/停止 | input digest | state digest | RNG digest | semantic trace digest |
|---|---|---|---|---|---|
| primary | `Death / Completed` | `e6c7adc1d7804e46b8a9b8b891e3b8d1fb102798a9ec17cdfbd0b36b51836fd5` | `3b5d93de60c623a537f03881cafbdced2bed8542a9a15486e2e59ec0ba6cdf58` | `11522fc7bb5355fe14a198894199d1317e6329a06c7732a288f69e690cc3c5a0` | `d7d9a82a9dbf12442288cf10a79d770fd59753ce66e869a5cb83bdd4b2fb5091` |
| repeat | `Death / Completed` | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 |
| replay | `Death / Completed` | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 | 与 primary 相同 |
| failure | `Unknown / BudgetExceeded` | `e943fc19430ac5ded66e918fa0eee3018114963830f3d2af714b47b8bcee322f` | `d637ea856d25f5f45c2ffb58b5e44f2f107284203a4afc7d2407ffb872a26a28` | `eef3af9e01ae67f24878e6d61c5eea6375ab9d379bae33cdeca85d2e9124c54b` | `18ef895122cf4ee85cb7c342097bea38bcb46ebc3d3129fe4c6e083ea8edef74` |

primary/repeat 的 boundary、终态和 digest 完全一致；replay 通过完整状态/RNG/trace 比较。`dotnet <native dll> --review` 对 primary/repeat/replay 返回 0；failure 按协议返回 1 并保留 `BudgetExceeded` 原因。

奖励/清理证据：`.build/runs/m2-1-reward-checkpoint-v5-20261108/run-manifest.json` 包含 `battle-victory-cleanup`、`reward-offer`、`rewards-completed`，随后在下一节点进入 `BudgetExceeded`，因此仅作为奖励生命周期证据，不提升为 Run 终态。

## 失败分类控制

独立新进程目录均在 `.build/runs/m2-1-failure-v5-20261108-*`：

- `runtime-failure` → `RuntimeFailure`，保留 `NativeRuntimeFailureException` 原因。
- `environment-blocked` → `EnvironmentBlocked`，保留 `NativeEnvironmentBlockedException` 原因。
- `strategy-stop` → `StrategyStopped`，保留 `NativeStrategyStoppedException` 原因。
- `crash` → `ProcessCrashed`，无 `result.json`，保留 raw exit `-2146232797` 和 stderr；父脚本仍生成 manifest。
- 合同 failure → `BudgetExceeded`，保留 `TimeoutException: labelled budget control`。
- 既有 `subchoice` → `Unsupported`，保留 `NotSupportedException`；既有 `async-fault` → `EngineError`，保留 aggregate async fault。

没有把预测死亡、战斗 Victory 或预算结束提升为 RunVictory/Death。

## 验收标准

1. **PASS**：新进程控制运行真实到达 `Death`，`Completed`；trace 含 `death`、`terminal-stamp`、`terminal-contract`。
2. **PASS**：独立 primary/repeat 的边界、终态、state/RNG/trace digest 一致。
3. **PASS**：replay 通过完整状态/RNG/trace 校验。
4. **PASS**：BudgetExceeded、Unsupported、RuntimeFailure、EnvironmentBlocked、StrategyStopped、EngineError、ProcessCrashed 均保留原始原因。
5. **PASS**：canonical input、run-manifest、生命周期边界、route/choice/collector 摘要均可关联到运行目录。
6. **PASS（脚本验收）**：Core 137、原版 slice、现有 Mod worker、Stage 6–10 回归命令均退出 0；Stage 6 的 `MOD-010` 是被保留的预期 Unsupported。

## Foundation gate、矩阵与缺口

- **Foundation gate：PARTIAL**。M2-0 已冻结 BaseLib/RitsuLib/MinionLib 的注册和依赖输入；共享玩法语义尚未形成可声明的跨 Mod 完整 gate，本阶段没有用单 Mod 特判绕过。
- **Content unit**：本阶段只有原版 Ironclad 合法选择 → Neow opening → map child → native combat → actual Death 的 vanilla unit；奖励 unit 作为真实 boundary probe 单独记录。
- **Lifecycle matrix**：`new-run`、opening、map-candidates、combat opening/decision、battle cleanup、death、terminal stamp、footer 均已观察；奖励 probe 观察 `reward-offer`/`rewards-completed`；RunVictory 未在本阶段控制 seed 中声称通过。
- **Interaction risk**：Core ↔ NativeRun contract 已有明确 stop/terminal 互斥；Mod worker 仍隔离在独立进程；Workshop/Steam/UI/save/network 不进入核心输入。
- **分母**：M2-1 控制运行 `primary=1 / repeat=1 / replay=1 / failure=1`；成功真实终态 `1/1`（Death），RunVictory `0/1` 未运行；失败控制分类 `7` 类已覆盖。历史 Mod 矩阵继续为：Ritsu `31 partial / 114 unsupported`，Kaguya `3 unsupported`，Watcher `MOD-010` 保留；Eleanor `Blocked/Unrun`；Stage 10 UI/Map/Save/ComplexComponent/ReplayNetwork 仍是 Unsupported。
- **未覆盖语义**：完整 RunVictory、奖励后继续到终态、真实存档/加载、UI 可见性、地图全局策略、网络/Steam/云端、未运行角色及 Eleanor；这些没有被本阶段的控制运行冒充覆盖。

## 下一阶段唯一建议入口

先解决固定 seed 下奖励完成后的合法节点推进，并以同一 canonical input 契约证明奖励后真实 `RunVictory` 或 `Death`；在此之前不要接入新的角色 Mod 或扩展玩法语义。

是否提交或推送：**否**。
