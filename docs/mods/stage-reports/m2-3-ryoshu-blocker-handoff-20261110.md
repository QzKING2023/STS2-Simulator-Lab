# M2-3 Ryoshu 阻塞交接文档

日期：2026-11-10

## 当前结论

Ryoshu 的 M2-3 子票 #8 当前为 **BLOCKED**，保持 OPEN。

已完成真实 native 发现和首战入口，但在第一张合法攻击牌 `CARD.RYOSHU_STRIKE` 的原始 `OnPlay` 中触发稳定的 CLR `MissingMethodException`：

```text
MegaCrit.Sts2.Core.Commands.Builders.AttackCommand
  .FromCard(MegaCrit.Sts2.Core.Models.CardModel)
```

固定游戏 `0.111.0` 中不存在这个单参数成员。反射证据显示当前唯一可用方法是：

```text
MegaCrit.Sts2.Core.Commands.Builders.AttackCommand
  .FromCard(
    MegaCrit.Sts2.Core.Models.CardModel,
    MegaCrit.Sts2.Core.Entities.Cards.CardPlay)
```

因此当前 flow/content gate 不能通过：

```text
Covered=0
Partial=1
RuntimeFailure=1
Unrun=154
Unsupported=0
```

没有把未运行内容猜测为 Unsupported，也没有把失败提升为成功。

## 票据状态

- GitHub issue：[#8 M2-3: Close Ryoshu’s flow/content slice](https://github.com/QzKING2023/STS2-Simulator-Lab/issues/8)
- #8：OPEN，标签 `ready-for-agent`，已认领。
- 唯一阻塞票 #4：CLOSED。
- #8 已有两条交接评论：初始失败证据和本次 API blocker-resolution 证据。
- 不要关闭 #8，除非新的合法输入下 flow/content/stability 验收全部通过。

## 分支和工作区

- 分支：`main`
- HEAD：`4cae8b4cf90d1be358619bdbf5a9e79d53c896b4`
- 当前已有未提交修改必须保留：
  - `README.md`
  - `docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md`
  - `tools/maintenance/cleanup-workspace.ps1`
  - `tools/maintenance/register-workspace-cleanup.ps1`
  - `tools/maintenance/run-workspace-cleanup-task.ps1`
  - 已有 Aemeath、figure_Saya、Watcher 阶段报告
- 不得 reset、clean、checkout、覆盖复制或删除用户已有文件。
- 本阶段没有提交、推送或创建 PR。

## 固定输入

- 目标游戏：`0.111.0`
- 游戏 commit：`41cef1ea`
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`
- 快照内容 SHA-256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- manifest SHA-256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- 快照规模：142 个文件、`6,974,558,103` bytes
- Mod：Ryoshu，Workshop ID `3747528329`，版本 `0.3.6`
- Ryoshu DLL SHA-256：`0ff08a3187df4969a6d4ecc9a3c79e28bbb6ab51ba6feb7bc0cc6ffb2605de9a`
- Ryoshu PCK SHA-256：`91c699a8d79d0b3d17c5b01470165d12370ab2ac96144d0428afbb0770205210`
- 没有外部依赖声明；没有第二个精确 `0.111.0` Ryoshu 兼容变体。
- registration identity：`REG.RYOSHU.assembly#1@0.111.0`
- seed：`LAB_M2_3_RYOSHU_01`
- worker：`SimulatorLab.NativeLifecycleProbe/Ryoshu-M2-3`
- adapter：`ryoshu-m2-3-probe/v1-no-runtime-patch`
- collector：真实 Godot .NET 进程 + `SimulatorLab.NativeLifecycleProbe`、`native-trace/v1`、`sentry-disabled-overlay/v1`

## API blocker 证据

证据入口：[attack-command-api.json](../../../.build/runs/m2-3-ryoshu-20261110/attack-command-api.json)

- `.build` 中全部 31 份本地 `sts2.dll` 副本均相同：
  - SHA-256：`0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9`
  - 长度：`9,757,184` bytes
- 当前类型 `MegaCrit.Sts2.Core.Commands.Builders.AttackCommand` 的 `FromCard` 只有双参数版本。
- Ryoshu 的四路新进程都在原始 `RyoshuStrike.OnPlay` 稳定触发单参数缺失错误。
- 普通 adapter、extension method 或 probe wrapper 无法满足已经编译进 `Ryoshu.dll` 的 CLR member reference。

在当前共同边界下，以下方式禁止执行：

- 修改、替换或重写 `sts2.dll`、Ryoshu DLL、PCK 或快照资源；
- 运行时 Harmony/MonoMod/JIT patch、改写调用点或注入结果；
- 修改 Mod 原始代码；
- 用原版卡、假 DTO、直接伤害或跳过回调伪造玩法；
- 新增包含 Ryoshu 名称、Workshop ID 或单包分支的 adapter。

如果新会话判断必须使用上述方式才能解决，必须停在 BLOCKED，并报告需要的外部状态变化；不得自行越过边界。

## 已有 native 运行证据

聚合入口：[evidence-index.json](../../../.build/runs/m2-3-ryoshu-20261110/evidence-index.json)

四路 accepted cohort：

| 路径 | 终态 / stop | input digest | state digest | trace digest |
| --- | --- | --- | --- | --- |
| primary | `Unknown / RuntimeFailure`，`MissingMethodException` | `da8880d113f0c9baf44fd2bc98a52d4b72702875867b110b91466ed317110995` | `28141ef485ee8f9d358b34f9a57467ea28c2337017a29151b622f10400428ccb` | `f7255b2f5c9096673c17d6d37104683a87f30cca01af0262a57864e3241ec17c` |
| repeat | 同 primary | 同 primary | 同 primary | 同 primary |
| replay | 同 primary；没有完整 route/state 可 replay | 同 primary | 同 primary | 同 primary |
| failure | `Unknown / BudgetExceeded` | 同 input | `2cdb2c44ad66a2216492d1428eb28d8c67497dd2d0c489caedfc2bcc99ee2f34` | `2064b3d44c6555375f50121ab18f18740de4ebcae680d9f23c5d645ff643e8ff` |

primary/repeat/replay trace 已见证：

```text
role-selected
initial-state
room-opening-complete
map-transition
combat-enter
combat-action(CARD.RYOSHU_STRIKE)
runtime-failure
```

failure trace 在同一首战入口以受控 `BudgetExceeded` 停止，没有伪造终态。

## 内容和能力矩阵

真实 ModelDb inventory：

```text
Character=1
Card=107
Power=35
Relic=13
Other=4
标准分母=156
```

角色身份和初始状态：

```text
CHARACTER.RYOSHU
75 HP
5x CARD.RYOSHU_STRIKE
5x CARD.RYOSHU_DEFEND
CARD.SACRIFICE_MEMORY
CARD.RECLAIM_MEMORY
RELIC.TIANSHA_STAR_BLADE_ARAYASHIKI
```

矩阵入口：

- [content-matrix.json](../../../.build/runs/m2-3-ryoshu-20261110/content-matrix.json)
- [lifecycle-matrix.json](../../../.build/runs/m2-3-ryoshu-20261110/lifecycle-matrix.json)
- [interaction-risk.json](../../../.build/runs/m2-3-ryoshu-20261110/interaction-risk.json)
- [capability-matrix.json](../../../.build/runs/m2-3-ryoshu-20261110/capability-matrix.json)
- [contract-summary.json](../../../.build/runs/m2-3-ryoshu-20261110/contract-summary.json)

当前 gate：

- Foundation：`PARTIAL`；共享 BaseLib/RitsuLib/MinionLib 语义仍未全局闭合。
- Flow：`BLOCKED`；没有 native Victory/Death。
- Content：`BLOCKED`；156 个标准单元尚未闭合。
- Stability：`PARTIAL`；失败 boundary 稳定，但没有完整 state/RNG/route replay。

## 新会话允许的最小调查顺序

1. 读取仓库 `AGENTS.md`、`GLOSSARY.md`、domain/issue/triage 文档、ADR、M2 计划、此交接文档和启动提示词。
2. 执行 `. .\tools\maintenance\Set-GitHubSession.ps1`，读取 #8 和其 blocker 状态；确认 #8 仍 OPEN、`ready-for-agent`、#4 CLOSED。
3. 读取当前 branch、HEAD、git status；保护已有未提交修改。
4. 确认固定快照、manifest 和当前证据目录存在；不要修改输入。
5. 仅使用反射/元数据检查确认 exact 0.111.0 API；不要反编译 DLL/PCK。
6. 只有在发现合法、跨 Mod 可复用、无需运行时 patch/二进制重写/Mod 修改的兼容输入后，才建立新的 probe 和新的 `.build/runs` 子目录。
7. 如果 blocker 仍是单参数 `FromCard` 缺失，保留原证据并停止；不要把 Unrun 改成 Unsupported/ Covered。

## 完全通过的必要条件

只有满足以下全部条件才可关闭 #8：

- Ryoshu identity 和 initial state 仍来自真实 native lifecycle；
- 首战真实完成，reward offer/selection、跨房间推进和 cleanup 可观察；
- primary/repeat/replay/failure 四路在同一 canonical input 下有完整 state/RNG/trace/route 证据；
- 实际 native `RunVictory` 或 `Death`；
- 156 个标准 content unit 全部有适用 lifecycle checkpoint 和 interaction evidence；
- 没有 hidden stop、预测死亡、原版替代、结果注入、空实现或静默跳过；
- foundation/flow/content/stability/platform capability 状态可独立审计；
- 更新 issue 评论后再关闭 #8。

## 当前唯一外部决策

若要继续，维护者必须提供以下之一：

1. 一个经过批准、与游戏 `0.111.0` 精确匹配且不修改冻结快照边界的 Ryoshu 兼容输入；或
2. 明确批准改变当前“禁止运行时 patch、二进制改写和 Mod 修改”的边界，并为该变化建立新的票据/ADR。

在此之前，正确状态是 BLOCKED。
