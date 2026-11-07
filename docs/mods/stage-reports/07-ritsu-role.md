# 阶段 7 交接：RitsuLib 单角色扩展

## 阶段状态

**PASS**。本阶段只选择并扩展了 LexNinja2，没有进入事件、敌人或 MinionLib。

选择前先完成了三个候选的静态 PE 范围和真实 Godot ModelDb 范围核对：

| 角色 | 静态类型范围 | 真实运行时范围 | 选择结论 |
| --- | --- | --- | --- |
| YukiMakoto | 1 Character、117 Card、6 Relic、102 Power | 1 Character、270 Card、4 Relic、281 Power | 未选；运行时表面最大 |
| Remilia | 4 Character、89 Card、9 Relic、25 Power | 1 Character、88 Card、8 Relic、24 Power | 未选；没有自有兼容目录 |
| LexNinja2 | 1 Character、107 Card、10 Relic、40 Power | 1 Character、103 Card、9 Relic、37 Power | 选中；精确支持游戏 0.111.0，且首战边界可控 |

LexNinja2 使用固定的 RitsuLib `0.6.10 / compat/0.111.0` 变体。真实运行时日志记录了 `Host=v0.111.0; runtime=0.111.0`、RitsuLib 432 个核心补丁全部成功、LexNinja2 179 个自动注册操作全部成功，以及角色、起始卡组和起始遗物注册。

## 当前分支和工作区

- 分支：`main`，相对 `origin/main` ahead 6。
- 工作区已有大量用户未提交修改，包括 `README.md`、大量历史 `docs/` 删除和 `docs/` 新目录；本阶段没有 reset、clean、checkout、提交或推送。
- 固定快照目录和 manifest 均存在且未写入。快照摘要仍为 `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。

## 实际修改文件

- `src/SimulatorLab.Core/Mods/ModAdapterModels.cs`
  - 增加标准 `TargetSelectionDefinition`、`CardEffectDefinition`、`RelicTriggerDefinition` 和 `PowerLifecycleDefinition`。
  - 扩展 Card、Relic、Power 标准模型；没有引入 Godot、RitsuLib 或 Mod DTO。
- `src/SimulatorLab.Core/Mods/ModAdapterInput.cs`
  - 增加适配器边界的目标、效果、遗物触发和 Power 生命周期输入值。
- `src/SimulatorLab.Core/Mods/ModAdapterConverter.cs`
  - 将上述边界值转换到标准模型，缺字段和非法持续时间继续返回稳定错误码。
- `src/SimulatorLab.Core/Mods/RitsuStage7.cs`
  - 固定 LexNinja2、RitsuLib、快照和游戏版本。
  - 读取标量 runtime probe，转换角色、103 张 Card、9 个 Relic、37 个 Power。
  - 记录 mapped、partial 和 Unsupported 能力，不把未观测效果静默当作完整兼容。
  - 绑定 seed、probe digest、replay digest、精确兼容变体和写入隔离。
- `tools/mod-worker/RitsuStage7.csproj`
- `tools/mod-worker/RitsuStage7Program.cs`
- `tools/mod-worker/ModWorker.csproj`
  - 将阶段 7 顶级语句文件从通用 ModWorker 编译目标排除，避免多个顶级入口冲突。
- `tools/validation/TestRitsuStage7.ps1`
  - 阶段 7 验收和边界检查。
- 本报告文件。

以下关键输入和阶段 6 文件未修改：固定 Workshop 快照及其 manifest、`docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`、阶段 6 Watcher 代码和交接报告、核心 NativeRun 入口、正式游戏目录、玩家存档和正式设置。

## 运行证据

- 两个全新真实 Godot 进程：
  - `.build/runs/ritsu-stage7-native-primary-20261107`
  - `.build/runs/ritsu-stage7-native-replay-20261107`
- 两个 probe 均为 `ritsu-stage7-runtime-probe/v1`，`terminal=battle-complete`，150 个 LexNinja2 runtime identities，31 个实际 combat 记录。
- 两次摘要完全一致：
  - `primaryDigest = 62d868cc3c9fe0144f5c1e3da9005f3e31e7157206d400826d996b282c40be1b`
  - `replayDigest = 62d868cc3c9fe0144f5c1e3da9005f3e31e7157206d400826d996b282c40be1b`
  - buildId `RITSU7-179BB0D8F4B6EA25AAD2D864`
  - contentKey `8602c26bffd2937e3841835fd8360ef8`
- 阶段 worker 最终报告：`.build/runs/ritsu-stage7-evaluation-native-20261107-final2/stage7-report.json`
  - status `Pass`
  - `errors=0`
  - `deterministic=true`
  - determinism digest `ad0a70dbd7791f5626df713d27234b86574f4d59a4c524458ca5810dcffb684d`
  - input digest `a23b58961017e353dbd371c9d4e310b5d9492dd531f85abd9b0abebbc115bd79`

## 内容表面和边界

| 内容表面 | 已支持 | 部分支持 | Unsupported | 边界和停止理由 |
| --- | --- | --- | --- | --- |
| Card 目标选择 | `StrikeNinja` 的 PrimaryEnemy、`YiCut` 的无显式目标、`DefendNinja` 和 `ShakeShakeHands` 的 Self/无目标 | `MosquitoHand` 只映射 identity 和 cost | 98 张未在有限首战中观测到效果的卡 | 每个 partial/Unsupported capability 都保留 `reason` 和 `evidenceRef`；未观测卡牌停在 `gameprobe.json` identity 边界 |
| Card 效果 | 实际重放了伤害、格挡和 ApplyPower；4 张实际卡均有标准效果记录 | 未进入首战的卡只保留 cost/identity | 具有 `OnPlay` 或生成语义但没有本阶段动作证据的卡 | `TestRitsuStage7.ps1` 检查 targeted、untargeted、damage、block 和 Power delta；没有用空效果冒充完整支持 |
| Relic 触发时机 | `combat-start`、`combat-end`、`reward-boundary` 证据；3 个 RelicTrigger capability | `LotusBox` 的 `BeforeDeath` 只看到声明，未运行死亡路径 | 其它未在首战触发的遗物语义 | partial 项保留“effect semantics were not exercised”停止理由；死亡触发留给后续阶段 |
| Power/Buff 获得和持续 | Lex Pain 的战斗初始状态；`ShakeShakeHands` 后 `WeakPower` 获得；turn-end、下一回合和 cleanup 记录 | 26 个 Lex Power 只有 identity、cost/lifecycle 声明 | 11 个含未覆盖 lifecycle method 的 Power | 每个 partial/Unsupported Power 均有 lifecycle method 或未观测理由；未声明无期限清理成功 |
| 死亡和战斗结束清理 | 战斗胜利、reward boundary、cleanup 事件和 Power 快照均已重放 | `BeforeDeath` 未触发 | 真实死亡路径及死亡中断重放 | 仅在真实 `battle-complete` 边界报告通过；死亡语义停止在明确的未运行证据 |

## 验收结果

- 依赖闭包和加载顺序稳定：通过；worker 报告选中 `STS2-RitsuLib/compat/0.111.0`，闭包为 BaseLib、RitsuLib、LexNinja2。
- 新战斗语义在真实原生生命周期中可重放：通过；目标选择、伤害、格挡、Power 获得和回合边界均来自真实 Godot 战斗。
- 同输入两次转换和运行摘要一致：通过；两个新进程 digest 相同，阶段报告 `deterministic=true`。
- RitsuLib 特有 API 没有进入核心模拟器：通过；RitsuStage7 只消费 JSON 标量 probe，核心仅使用标准模型。
- 快照、正式设置和正式存档写入隔离：通过；每个进程使用新的 `.build/runs` appdata/localappdata，报告的两个 write-block 字段均为 true。
- 阶段边界测试：`TestRitsuStage7.ps1` 通过，逐项检查每个 Partial/Unsupported 的停止理由和证据引用，并验证目标、效果、Relic timing、Power lifecycle 边界。
- 核心契约：`dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-build --no-restore` 通过，`PASS all 137 contract checks`。
- ModWorker 回归：`TestModWorker.ps1` 通过，`deterministic=True failures=5`。
- Watcher 阶段 6 回归：`TestWatcherStage6.ps1` 通过，两个无 probe 安全停点均为 `Blocked/MOD-010`，摘要稳定。
- Release 构建：`ModWorker.csproj`、`WatcherStage6.csproj`、`RitsuStage7.csproj` 均为 0 错误、0 警告。

## 已知缺口和未支持语义

- 98 张 Card、11 个 Power 和 5 个 Relic 的具体语义没有进入本阶段的有限动作脚本；它们在报告中是显式 Unsupported 或 Partial，不能据此声称整包玩法兼容。
- `OnPlay`、生成卡、额外目标选择、RitsuLib secondary resource、事件、敌人、Minion 和真实死亡路径没有接入核心模型。
- Relic `BeforeDeath` 只有静态生命周期声明；本阶段没有运行死亡中断来证明触发顺序。
- YukiMakoto 和 Remilia 的 runtime scope 只完成独立 ModelDb 探针，未进入它们的战斗适配；候选范围证据保留在 `.build/runs/ritsu-candidate-scope-*`。
- 真实 Godot 日志仍可能包含表现层告警；本阶段没有用空节点或静默跳过隐藏它们。

## 执行过的命令及结果

- `git branch --show-current; git status --short --branch`：分支 `main`，已有未提交修改保留。
- 固定快照存在性检查和 manifest 读取：目录存在，21 条目、142 文件、固定摘要匹配。
- PE 静态范围探针：Remilia `Card=89/Relic=9/Power=25`，LexNinja2 `Card=107/Relic=10/Power=40`，YukiMakoto `Card=117/Relic=6/Power=102`。
- 两个 Lex 真实 Godot 进程：退出码 0，`battle-complete`，摘要一致。
- `dotnet build`：Core、ModWorker、WatcherStage6、RitsuStage7 全部通过。
- `dotnet run ... SimulatorLab.Core.Checks`：`PASS all 137 contract checks`。
- `pwsh ... TestModWorker.ps1`：通过。
- `pwsh ... TestWatcherStage6.ps1`：通过。
- `pwsh ... TestRitsuStage7.ps1`：通过。

## 下一阶段唯一建议入口

如果用户明确授权继续，唯一入口是从 LexNinja2 的一个已列为 Partial 的 Power 或 Relic 语义开始，先为该单项补充真实边界脚本和死亡/清理证据；不要扩大到事件、敌人或 MinionLib。

是否提交或推送：**否**。
