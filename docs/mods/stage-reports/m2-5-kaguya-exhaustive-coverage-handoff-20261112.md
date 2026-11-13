# M2-5 Kaguya 全量覆盖交接报告

日期：2026-11-12

## 阶段状态

**BLOCKED**。

固定快照中的 640 个内容单元已经逐项进入全量矩阵。当前矩阵为 `Covered=634`、`RuntimeFailure=6`、`Partial=0`、`Unsupported=0`、`Unrun=0`。剩余失败均保留了真实原生堆栈和独立 run 目录，没有被改写为成功。

## 票据、分支和工作区

- 子票：#11，已重新打开并保持 `ready-for-agent`。阻塞票 #9、#10 仍为 `CLOSED`。
- 分支：`main`。
- 本阶段没有提交、推送或创建远程 PR。
- 既有用户修改继续保留。`README.md`、`docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md`、既有阶段报告和维护脚本没有被覆盖、重置或删除。

本阶段新增或修改的工作文件包括：

- `tools/native-run/Program.cs`
- `tools/native-run/KaguyaNativeModLoop.cs`
- `tools/native-run/KaguyaNativeEncounter.cs`
- `tools/native-run/KaguyaNativeEvent.cs`
- `tools/native-run/NativeChoices.cs`
- `tools/native-run/NativeScenario.cs`
- `tools/native-run/NativePerformancePilot.cs`
- `tools/validation/RunKaguyaContentProbes.ps1`
- `tools/validation/BuildKaguyaExhaustiveEvidence.ps1`
- 本报告

## 固定输入

| 输入 | 值 |
| --- | --- |
| 游戏 | `0.111.0`，commit `41cef1ea` |
| 固定快照 | `local-mods/workshop-snapshot-20261106-163352` |
| 快照内容摘要 | `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73` |
| manifest SHA-256 | `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3` |
| Kaguya | Workshop `3747548629`，版本 `3.2.4` |
| BaseLib | `3.4.7` |
| Kaguya.json SHA-256 | `0961dc7c5e98235ebfc40220bcebb3735ee57ec55bff078adb9c77742c458532` |
| Kaguya.dll SHA-256 | `f565b7232975212e59088bebc2e2ef169c78f0c5ef36e0f698807f9c9942abce` |
| Kaguya.pck SHA-256 | `f60ba6e6975245de6c8ed98aa0c69a92864fe608f15329b7c8f17d246ffdcb8a` |

固定快照、Mod 二进制、PCK、manifest 和正式游戏输入保持只读。全量注册通过快照内真实 `KaguyaModInitializer` 完成。`Kaguya.Cards.Clash` 与原版 `CARD.CLASH` 的真实身份冲突单独在 collision-isolated profile 中运行，原版 profile 的 `DuplicateModelException` 证据仍保留。

## 全量矩阵

机器可读证据位于 `.build/runs/m2-5-kaguya-20261112-exhaustive-final6/`。

| 状态 | 数量 |
| --- | ---: |
| Covered | 634 |
| Partial | 0 |
| Unsupported | 0 |
| Unrun | 0 |
| RuntimeFailure | 6 |

覆盖范围包括 334 Card、73 Relic、159 Power、14 Encounter、23 Enemy、16 Event、2 Character、10 Potion、1 Enchantment、18 辅助模型和 1 个事件奖励身份。

## 失败单元和保留证据

1. `Kaguya.Encounters.TsukitoBosatsuBossEncounter`
2. `Kaguya.Monsters.TsukitoBosatsu`
3. `Kaguya.Events.EightThousandYearsMemory`
4. `Kaguya.Events.KaguyaEnding`
5. `Kaguya.Events.MoonNews`
6. `Kaguya.Events.YachiyoCupEvent`

`TsukitoBosatsu` 在真实 `AfterAddedToRoom` 路径创建 Godot `Timer` 和原生随机调用。当前 headless host 无法构造该 Godot 节点。`KaguyaEnding` 进入真实 `Proceed` 后调用正式 `RunManager.OnEnded`/game-over UI 状态，离线 profile 没有该终局节点。其它事件的失败来自真实事件回调的音乐停止或 Mod 自身状态依赖。对应 `stderr.log`、`result.json`、`trace.jsonl` 和 canonical input 均位于各自的 `.build/runs` 子目录。

## Full-run cohort

既有 full-run cohort 仍保持稳定：

- `primary`、`repeat`、`replay`：真实 native `Death / Completed`，state、RNG、trace、route、choice digest 完全一致。
- `failure`：`Unknown / BudgetExceeded`，raw exit `3`，独立失败摘要保留。
- 原有 primary canonical input digest：`a9118c4ea2081d5dd575731ccc16765c6a11978f609031fe0060514d1b433c42`。
- 原有 replay canonical input digest：`6ee50ca4aff99e1918b0378af1906941e4019583f0e6e4941cc1b191d0ebba3b`。

## Foundation gate 和平台能力

- Foundation gate：`PASS`。只接受 exact `BaseLib@3.4.7 -> Kaguya@3.2.4` 闭包。
- Registration gate：`PASS`。运行时登记 638 个 Kaguya 模型，声明分母 639，另加 1 个事件奖励身份。
- Full-run flow gate：`PASS`。primary/repeat/replay 到达真实 `Death`。
- Stability gate：`PASS`。成功 cohort digest 一致。
- Headless native execution：`Covered`。
- Audio、video、hover-tip、asset cache、visible UI、map presentation、正式 save/load、network、Steam/cloud：平台能力分别保留在 run 证据中。为执行兼容性建立的 headless bypass 不宣称 visible-policy compatibility。

## 执行和验证

全量 probe 使用 `tools/validation/RunKaguyaContentProbes.ps1`，每个内容单元拥有独立 `.build/runs` 目录、seed、snapshot digest、版本闭包、worker、adapter 和 collector 摘要。聚合命令为：

```powershell
pwsh -NoProfile -ExecutionPolicy Bypass -File tools/validation/BuildKaguyaExhaustiveEvidence.ps1 ...
```

关键结果：

- NativeRun 多轮构建：退出码 `0`，最终可复用 build 为 `.build/native-build/kaguya-full-20261112-r31`。
- 全量 Card、Relic、Power、Potion、Enchantment、Encounter、Enemy、Event、Character probe 已运行。
- `git diff --check`：通过。
- 既有 Core 137 项 contract checks、Stage 8 regression 和 ModWorker build 保持通过。

## 验收标准逐项结果

| 验收标准 | 结果 |
| --- | --- |
| 自定义身份实际见证 | 通过。所有 640 个冻结单元均进入注册或 probe 矩阵 |
| 真实 RunVictory 或 Death | 通过。full-run primary/repeat/replay 为真实 native Death |
| 所有冻结单元有 lifecycle evidence 或明确终态 | 通过。当前分母没有 Partial、Unsupported 或 Unrun，6 个单元为明确 RuntimeFailure |
| primary/repeat/replay/failure 稳定 | 通过。原有 cohort digest 一致，failure 保留 BudgetExceeded |
| foundation gate | 通过 |
| 不修改 Mod 或快照 | 通过 |
| 所有内容完成可执行兼容 | 阻塞。6 个真实 RuntimeFailure 仍未闭合 |

## 下一阶段唯一建议入口

先为 `TsukitoBosatsu` 的 `AfterAddedToRoom` 建立不触碰 Godot 节点的真实 native lifecycle seam，同时为 `KaguyaEnding` 提供受控终局状态。两者闭合后再重跑 6 个 RuntimeFailure 单元和 full matrix。不要修改 Kaguya Mod、游戏快照或把失败回调替换成静态结果。

是否提交或推送：**否**。
