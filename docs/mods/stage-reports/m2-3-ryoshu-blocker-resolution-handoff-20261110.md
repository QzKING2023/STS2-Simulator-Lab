# M2-3 Ryoshu blocker-resolution 阶段交接报告（2026-11-10）

## 阶段结论

**BLOCKED**。

本阶段只在不可变输入边界内完成了新的元数据复核，没有找到可批准、跨 Mod 可复用、无需运行时补丁或二进制重写的兼容 seam。固定 `0.111.0` 的 `sts2.dll` 反射只暴露：

```text
MegaCrit.Sts2.Core.Commands.Builders.AttackCommand
  FromCard(MegaCrit.Sts2.Core.Models.CardModel,
           MegaCrit.Sts2.Core.Entities.Cards.CardPlay)
```

不存在单参数 `FromCard(CardModel)`。对固定 `Ryoshu.dll` 做 ECMA-335 MemberRef 读取确认，其 `AttackCommand.FromCard` 引用的参数计数为 1（token `167772719`，signature blob `20011281391280a5`）；此前四路真实新进程已经在同一原始 `OnPlay` 处观察到 `MissingMethodException`。因此不能建立合法 fixture 或继续首战后的 reward、跨房间、terminal、cleanup 和 156 个内容单元逐项生命周期运行；已有 `RuntimeFailure`/`Unrun` 证据原样保留。

唯一下一入口是获得一个经过批准、精确匹配 `0.111.0` 且不改变冻结快照边界的 Ryoshu 兼容输入，或由维护者以新 ADR/票据明确改变“禁止运行时 patch、调用点改写和二进制重写”的边界。

## 票据、分支、HEAD 与工作区

- 子票：[#8](https://github.com/QzKING2023/STS2-Simulator-Lab/issues/8)，开始前读取结果为 `OPEN`，标签为 `ready-for-agent`。
- 阻塞票 #4 开始前读取结果为 `CLOSED`。
- 分支：`main`。
- HEAD：`4cae8b4cf90d1be358619bdbf5a9e79d53c896b4`。
- 开始前已有未提交修改已保护；未执行 `reset`、`clean`、`checkout`、覆盖复制或删除用户文件。
- 工作区原有修改仍在：`README.md`、`docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md`、workspace cleanup 脚本，以及已有 Watcher/Aemeath/figure_Saya/Ryoshu 阶段报告和提示词。
- 本阶段新增：本报告，以及只读元数据审计证据目录 `.build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/`。
- 未提交、未推送、未创建重复票据，未运行 `/triage`。

交接时 `git status --short --branch` 为：

```text
## main...origin/main [ahead 2]
 M README.md
 M docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md
?? docs/mods/stage-reports/m2-3-aemeath-full-run-report-20261110.md
?? docs/mods/stage-reports/m2-3-figure-saya-full-run-report-20261110.md
?? docs/mods/stage-reports/m2-3-ryoshu-blocker-handoff-20261110.md
?? docs/mods/stage-reports/m2-3-ryoshu-blocker-resolution-handoff-20261110.md
?? docs/mods/stage-reports/m2-3-ryoshu-full-run-report-20261110.md
?? docs/prompts/MOD_M2_3_RYOSHU_BLOCKER_RESUME_PROMPT_20261110.md
?? tools/maintenance/cleanup-workspace.ps1
?? tools/maintenance/register-workspace-cleanup.ps1
?? tools/maintenance/run-workspace-cleanup-task.ps1
```

`.build/` 证据目录受仓库忽略规则保护，因此不出现在上述短状态中；其文件已保留在工作区。

## 实际修改与审计证据

只新增诊断/证据文件，没有修改任何固定输入、游戏 DLL、Ryoshu DLL、PCK、manifest、资源或原始 Mod 代码：

- [SeamAudit.csproj](../../../.build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/SeamAudit.csproj)：无 NuGet 依赖的 metadata-only 审计项目。
- [Program.cs](../../../.build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/Program.cs)：使用 `System.Reflection` 和 `System.Reflection.Metadata`，不反编译、不执行 Mod 方法、不启动游戏进程。
- [seam-audit.json](../../../.build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/seam-audit.json)：固定 `sts2.dll` 反射、快照 DLL MemberRef、文件摘要。
- [seam-audit-summary.json](../../../.build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/seam-audit-summary.json)：结论化的 seam 复核结果。
- [canonical-input.json](../../../.build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/canonical-input.json)：审计输入摘要。
- [run-manifest.json](../../../.build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/run-manifest.json)：`metadata-audit`、`Blocked`、input digest 和退出信息。

本次 metadata audit input digest：

```text
1ac6983a7df8b9f9335712ee21697ed21cdd97ca25ff9c61dab7ccc1829dfd26
```

审计扫描到工作区 `.build` 下 42 份 `sts2.dll` 副本，全部为同一 SHA-256；此前固定证据中的 31 份 canonical 副本也保持该值。快照中 `3747528329` 只有一项 Ryoshu，4 个文件，没有第二个精确 `0.111.0` 变体。

## 固定输入

- Game：`0.111.0`。
- Game commit：`41cef1ea`。
- Snapshot：`local-mods/workshop-snapshot-20261106-163352`。
- Snapshot SHA：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- Manifest SHA：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`。
- Mod：Ryoshu `0.3.6` / `3747528329/Ryoshu.dll`。
- Ryoshu DLL SHA：`0ff08a3187df4969a6d4ecc9a3c79e28bbb6ab51ba6feb7bc0cc6ffb2605de9a`。
- Ryoshu PCK SHA：`91c699a8d79d0b3d17c5b01470165d12370ab2ac96144d0428afbb0770205210`。
- canonical seed：`LAB_M2_3_RYOSHU_01`。
- Registration：`REG.RYOSHU.assembly#1@0.111.0`。
- Worker：`SimulatorLab.NativeLifecycleProbe/Ryoshu-M2-3`。
- Adapter：`ryoshu-m2-3-probe/v1-no-runtime-patch`。
- 原生 collector：真实 Godot .NET 进程 + `SimulatorLab.NativeLifecycleProbe`；`native-trace/v1`；`sentry-disabled-overlay/v1`。
- metadata collector：`metadata-only; no game process`。

## 命令与退出码

### 本阶段新执行

| 命令 | 退出码 | 结果 |
| --- | ---: | --- |
| `. .\tools\maintenance\Set-GitHubSession.ps1; gh issue view 8 --json number,title,body,labels,state,comments` | 0 | #8 `OPEN`、`ready-for-agent` |
| `. .\tools\maintenance\Set-GitHubSession.ps1; gh issue view 4 --json number,title,body,labels,state` | 0 | #4 `CLOSED` |
| `dotnet build .build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/SeamAudit.csproj -c Release --ignore-failed-sources` | 0 | 审计项目构建成功；仅有 NuGet audit 网络警告 |
| `dotnet run --project .build/runs/m2-3-ryoshu-20261110-seam-audit-20261110/SeamAudit.csproj -c Release --no-restore --ignore-failed-sources` | 0 | 反射/ECMA-335 metadata audit 完成 |
| `dotnet run --project tests/SimulatorLab.Core.Checks/SimulatorLab.Core.Checks.csproj -c Release --no-restore` | 0 | `PASS all 137 contract checks` |
| `git diff --check` | 0 | 无新增 whitespace error；仅报告既有 LF/CRLF 提示 |
| 固定 manifest、Ryoshu DLL/PCK、pinned `sts2.dll` 的 SHA-256 复核 | 0 | 与 canonical input 完全一致 |

### 原有四路 native evidence（本阶段未重跑）

| 命令/批次 | 退出码 | 证据 |
| --- | ---: | --- |
| `dotnet restore .build/diag/m2-3-ryoshu-probe/M23RyoshuProbe.csproj --ignore-failed-sources -p:NuGetAudit=false` | 0 | probe assets |
| `dotnet build .build/diag/m2-3-ryoshu-probe/M23RyoshuProbe.csproj -c Release --no-restore --ignore-failed-sources -p:NuGetAudit=false` | 0 | probe DLL |
| `prepare-root.ps1 -Suffix '-r5'` | 0 | hardlink overlay runtime root |
| `run-game.ps1 -RunKind primary` | 0 | primary-r7 |
| `run-game.ps1 -RunKind repeat` | 0 | repeat-r7 |
| `run-game.ps1 -RunKind replay -ReplayPath ...primary-r7/game-output/route.json` | 0 | replay-r7；route 不存在因 primary 首战失败 |
| `run-game.ps1 -RunKind failure` | 0 | failure-r7；受控 `BudgetExceeded` |
| `python .build/diag/m2-3-ryoshu-probe/build-evidence.py` | 0 | 聚合矩阵 |

## primary / repeat / replay / failure

四路均为独立新进程，同一 canonical input digest：

```text
da8880d113f0c9baf44fd2bc98a52d4b72702875867b110b91466ed317110995
```

| 路径 | 终态 / stop | failure classification | state digest | RNG digest | trace digest | route digest |
| --- | --- | --- | --- | --- | --- | --- |
| primary | `Unknown / RuntimeFailure` | `MissingMethodException` | `28141ef485ee8f9d358b34f9a57467ea28c2337017a29151b622f10400428ccb` | — | `f7255b2f5c9096673c17d6d37104683a87f30cca01af0262a57864e3241ec17c` | — |
| repeat | `Unknown / RuntimeFailure` | `MissingMethodException` | 同 primary | — | 同 primary | — |
| replay | `Unknown / RuntimeFailure` | `MissingMethodException` | 同 primary | — | 同 primary | — |
| failure | `Unknown / BudgetExceeded` | `BudgetExceededException` | `2cdb2c44ad66a2216492d1428eb28d8c67497dd2d0c489caedfc2bcc99ee2f34` | — | `2064b3d44c6555375f50121ab18f18740de4ebcae680d9f23c5d645ff643e8ff` | — |

primary/repeat/replay 的 trace 保留 `role-selected`、`initial-state`、`room-opening-complete`、`map-transition`、`combat-enter`、`combat-action(CARD.RYOSHU_STRIKE)`、`runtime-failure`；failure 以受控 `budget-stop` 结束。没有 native `RunVictory` 或真实 `Death`，没有 hidden stop、原版替代或结果注入。

## seam 调查结果

- `sts2.dll` 反射：`AttackCommand.FromCard` 只有一个实例方法，双参数精确签名；`exactOneArgCardModel=false`、`exactTwoArgCardModelCardPlay=true`。
- `Ryoshu.dll` MemberRef：`AttackCommand.FromCard` 参数计数为 1；这是已编译进 Mod 的 CLR member reference，外部扩展方法或 probe wrapper 不参与该解析。
- 快照 MemberRef 对比：Ryoshu 和 `YukiMakoto/lib/0.107.1` 使用单参数；Remilia、Kaguya、Shadowverse、Aemeath、St2bs 和 `YukiMakoto/lib/0.111.0` 使用双参数。该对比只证明版本/调用契约差异，不构成可用于 Ryoshu 的兼容 seam。
- 固定快照没有第二个 Ryoshu 程序集或精确 `0.111.0` 兼容目录。
- 现有宿主的 Harmony/MonoMod 等 `NativeHost` 能力属于运行时 patch 路径，不能在本票边界内用来满足该 MemberRef。
- 因此没有建立 generic fixture，也没有启动新的四路 native run。继续尝试只能落入禁止的 `sts2.dll`/Ryoshu 重写、运行时方法 patch 或调用点改写。

## foundation / flow / content gate

- **Foundation：`PARTIAL`。** BaseLib、RitsuLib、MinionLib 的跨房间、callback/before-death、registry 与 teardown 语义仍未全局闭合；Ryoshu 额外暴露 native API 版本 mismatch。单 Mod adapter 不能绕过 foundation gate。
- **Flow：`BLOCKED`。** 角色选择、初始状态、首个地图点和首战入口已经真实见证；首个合法 `CARD.RYOSHU_STRIKE` 的原始 `OnPlay` 在 effect resolution 前 `RuntimeFailure`；没有 native `RunVictory`/`Death`。
- **Content：`BLOCKED`。** 156 个标准单元中只有首张攻击牌真实触发到失败边界；其余未运行内容保持 `Unrun`。
- **Stability：`PARTIAL`。** primary/repeat/replay 的输入、失败 envelope 和 trace 边界一致；由于没有完整 state/RNG/route，replay 不能宣称通过；failure control 独立保持 `BudgetExceeded`。

## content lifecycle matrix

标准分母为 156：`Character=1`、`Card=107`、`Power=35`、`Relic=13`；raw `Other=4` 保留在 inventory，不进入标准分母。

| 生命周期检查点 | 状态 | 证据/原因 |
| --- | --- | --- |
| DLL/PCK discovery、Mod initialization、ModelDb registration | `Covered` | native inventory、Godot log；未修改输入 |
| Character construction、`CHARACTER.RYOSHU` role selection、75 HP、12 张起始牌、starting relic | `Covered`（Character 单元总体为 `Partial`） | primary trace `role-selected`/`initial-state` |
| opening map transition、Monster combat entry | `Covered` | primary trace `map-transition`/`combat-enter` |
| `CARD.RYOSHU_STRIKE` activation/effect resolution | `RuntimeFailure` | 原始 `OnPlay` 的 `MissingMethodException` |
| reward offer/selection、deck persistence、跨房间推进 | `Unrun` | 首战未完成 |
| Power apply/stack/expiry/remove | `Unrun` | 35 单元未达到生命周期 |
| Relic obtain/trigger/remove、before-death/combat-end | `Unrun` | 13 单元未达到生命周期 |
| native Victory/Death、terminal cleanup、ALC teardown | `Unrun` | 没有 native terminal |

状态分母保持：

```text
Covered=0
Partial=1
RuntimeFailure=1
Unrun=154
Unsupported=0
Total=156
```

没有把 `Unrun` 改成 `Unsupported` 或 `Covered`。

## interaction risk table

| 交互契约 | 状态 | 证据/风险 |
| --- | --- | --- |
| registration / initialization | `Covered` | 真实 DLL/PCK load、ModelDb registration、原始 Harmony patch 日志 |
| custom role identity / initial state | `Covered` at observed checkpoints | `CHARACTER.RYOSHU`、75 HP、12-card deck、starting relic |
| RyoshuStrike native attack callback | `RuntimeFailure` | 单参数 `AttackCommand.FromCard` 与当前双参数 API 不匹配 |
| 其余 106 Cards 的 target/effect/cleanup | `Unrun` | 首张攻击牌失败后没有安全入口 |
| reward generation / deck persistence | `Unrun` | reward boundary 未到达 |
| Relic ownership / callbacks | `Unrun` | 13 Relics 未完成 activation/cleanup |
| Power activation / stacking | `Unrun` | 35 Powers 未完成 apply/trigger/remove |
| cross-room state / cleanup | `Unrun` | terminal 前置未完成 |
| shared library foundation | `Partial` | M2-0/M2-2 foundation 语义仍有缺口 |
| visible-policy compatibility | `Unsupported` | probe 使用 TestMode/internal state；不证明玩家可见策略 |
| UI/map/save/network/Steam/cloud | `Unsupported` | 当前 collector 是隔离 headless native process |

## 平台能力缺口

PCK 在原生进程真实加载，headless screen-shake 由通用 probe seam 提供；这不覆盖正式 UI、地图呈现、save/load、网络 replay、Steam 或 cloud。上述项目保持独立 `platform capability` 缺口，不计入 flow/content 通过，也不改变本次 blocker 分类。

## 下一唯一入口

在不改变本票不可逆边界的前提下，下一步只能等待以下外部状态之一：

1. 提供经过批准、与游戏 `0.111.0` 精确匹配、保留原始 Mod 语义且不修改冻结快照的 Ryoshu 兼容程序集/输入；或
2. 由维护者批准新的 ADR/票据，明确改变当前禁止运行时 patch、调用点改写和二进制重写的边界。

获得合法输入后，才可先建立跨 Mod 的 generic fixture 和 regression，再以全新进程重跑 Ryoshu primary/repeat/replay/failure；在真实首战完成前，不继续 reward、跨房间、terminal、cleanup，也不扩大内容矩阵。#8 继续保持 OPEN。
