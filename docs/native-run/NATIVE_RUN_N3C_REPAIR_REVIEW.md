# M1 步骤 4C 修复独立复审：剩余两项必修

日期：2026-11-02。审查 [原审查](NATIVE_RUN_N3C_REVIEW.md)、[修复报告](NATIVE_RUN_N3C_REVIEW_REPAIRS.md) 及实际生产检查器。结论：**原十个误接受反例均已闭合，原生执行证据继续成立；4C 尚未通过，仍有两项同范围的数据契约缺口。** 本轮只读运行实现与原始证据，在独立目录生成损坏或等价副本并调用生产 `--review`；未重跑原生游戏场景。

冻结 ID：`5E6BCC9F929004CB9659B361DBF66518527249847BA34A0B4971D9EDD7FA0E3B`。Native DLL SHA256：`44B20B043D226C65D1253EDFB11C730C59543BEDA8997762A305BD4DE0ABD74A`。

独立证据目录：`.build/runs/n3c-repair-independent-review01/`。主要入口为 `review.py`、`adjacent.py`、`facts.py`、`static-boundaries.py`；汇总为 `summary.json`、`facts.json`、`static-boundaries.json`。各副本保留 trace、重新计算的 seal、result、process 和实际 `reader.log`。重新计算 seal 用于检验语义门，不修改原始证据，不能把 seal 通过当成业务事实通过。

## 已独立核实

- 重新计算 Build ID 一致；48 个实际源码及其快照、30 个编译输入、3 个游戏输入、7 个产物全部匹配。15 个锁定 Mod 输入和 52 个兼容源码 hash 未变。
- 逐一检查实际 process/result/header/manifest：179 个原生进程确实绑定同一最终冻结，4C/4B/4A/N2 分别为 55/34/16/74 个进程、386/307/273/580 项 suite 检查，共 1546 项。原始退出分布仍为 122×0、52×1、2×2、3×3，均符合各自预定结果；故障、Unsupported、预算停止没有被改称自然成功。
- Core98 执行日志及七个实际驱动文件 hash 匹配，实际 Core DLL 与冻结产物一致。636 项原补充检查记录与实际数量匹配。独立进行了 195 项来源核对、229 项自然流程/重放/隔离/失败前缀核对及 62 项相邻边界/结果结构核对，全部通过。这些数量不代替下述生产 reader 的语义验收。
- 本轮实际调用生产 `--review` **320 次**：117 个已有生产控制、42 个正常/重放/观测对照包、115 个历史包，以及 46 个独立控制。前 274 次全部符合原预期；46 个独立控制包含 39 个坏证据、7 个等价副本，坏证据有 28 个拒绝、11 个误接受，7 个等价副本全部通过。115 个历史包保留原构建身份，只作为当前 reader 兼容回归，不能计入本轮原生执行。
- 原十个反例在新的正常证据上重新构造，实际全部返回 1，并到达对应的语义门：必需 card-menu、父 start 顺序、set/root completion、set return 与 item open 状态绑定、宝箱新对象/金币及卡来源升级。不是仅读取实现者的控制结果。
- 自然同一 run 仍为 24/24 根动作：正常 Neow Golden Pearl 开局、首战真实 Victory、原生 Gold/Potion/Card 奖励、下战 Play、永久牌与战斗 DeckVersion 关联、实际 BlockPotion 使用。最终 HP70、金币266、11 张永久牌、药水已消耗。仍为 native-only / NotCompared，不能称完整爬塔或独立模拟差分。
- 16 个正例的冻结重放和 5 组 minimal/off 对照，route、choices、reward-choices、完整 final-state（含完整 RNG）文件一致。真正原生变化保留：奖励故障的 +7 金币、药水故障的实际消耗/伤害；未确认根动作及 2/3、4/5 前缀仍为 EngineError。无效药水动作在派发前拒绝。
- 所核查 1184 个源码/原始证据文件保持不变。隔离记录仍为 MemorySaveStore、shouldSave=false、未初始化 Steam、无 UI、无 Mod/CombatSolver 主路径。这里没有新增 OS 全量写盘监控的证明。

## 必修 A：相邻静止边界仍未完整绑定（原 R2 的剩余部分）

位置：`tools/native-run/NativeRewardContract.cs` 87–90、145–151、197–220、221–222。当前修复覆盖了 reward-set → item、set-completed → set return，以及 Treasure 根操作 → 直接子操作的开始；但缺少以下真实静止区间：

1. `UsePotion` 根操作开始 → `potion-use` 开始。`NativeRewardProbe.cs` 63–64 直接进入子操作，之前没有原生效果；检查器却未把 UsePotion 纳入直接子操作完整状态绑定。
2. `potion-use` 返回 → `UsePotion` 根返回。
3. `treasure-extra` 返回 → `TreasureOpen` 根返回。
4. `treasure-choice` 返回 → `TreasurePick` 根返回，包括领取与合法跳过。
5. `treasure-exit` 返回 → `TreasureExit` 根返回。

这些尾部都是业务任务已完成后调用 `NativeRewards.End(child)`，返回回调，再由 `CAction` 调用 `NativeRewards.End(root)`。实际代码见 `NativeRewardProbe.cs` 26–30、63–69 和 `NativeTreasure.cs` 18、39–42；中间只有控制返回或取消订阅，没有可解释资源变化的业务执行。检查器保存了 `LastChild`，但上述根返回只校验子节点数量与 `completed`，没有与最后子返回做完整结构比较。根返回随后与 confirmed/boundary 自洽，掩盖了前一子返回的矛盾。

实际误接受的 10 个副本：

| 副本（独立目录下） | 损坏 | 预期/实际退出 |
| --- | --- | --- |
| `independent-static-child-return-extension-treasure-extra-reward-treasure-1` | 子返回独有未知嵌套状态字段 | 1 / 0 |
| `independent-static-child-return-extension-treasure-exit-reward-treasure-1` | 同上 | 1 / 0 |
| `independent-static-child-return-extension-treasure-choice-reward-treasure-1` | 领取后的选择返回独有字段 | 1 / 0 |
| `independent-static-child-return-extension-treasure-choice-reward-treasure-skip-1` | 合法跳过后的选择返回独有字段 | 1 / 0 |
| `independent-static-child-return-extension-potion-use-reward-block-1` | 药水子返回独有字段 | 1 / 0 |
| `independent-static-child-return-gold-treasure-extra` | 子返回金币999，根返回恢复实际值 | 1 / 0 |
| `independent-static-child-return-gold-treasure-exit` | 同上 | 1 / 0 |
| `independent-static-child-return-gold-potion-use` | 同上 | 1 / 0 |
| `adjacent-potion-open-extension` | potion-use open/start 同时增加字段，根快照不变 | 1 / 0 |
| `adjacent-potion-open-gold` | potion-use open/start 同时写金币999，根快照不变 | 1 / 0 |

独立检查现有正常包的 **38 个对应静止状态对**，全部完整相等：10 个 UsePotion 入口，10 个 UsePotion 尾部，TreasureOpen/Pick/Exit 各 6 个尾部。未知字段在所有对应快照一致存在、或对象属性顺序重排的等价副本也通过。

最小修复：按真实调用区间声明完整静止边，在现有不可变 Open/LastChild/ReturnedState 上做完整结构绑定；包括 UsePotion 直接入口和上述四种根操作的尾部。不要继续只增加金币/RNG字段比较，也不要把所有父子操作一律强制相等。`RewardOffer`、`EventOption`、Populate、嵌套 Offer、取得 Hook、NativeTravel 等可能包含真实业务变化，仍应按其实际生命周期处理。本项无需新增节点能力或改变原生效果。

## 必修 B：卡奖励外层 acquired 可错误指向旧对象

副本 `independent-item-return-card-acquired-old`：只把 `reward-card-1` 的外层 `reward-item` 返回 `result.acquired` 从真实的 null 改成取得前已经在永久牌组中的第一张牌。内部 card-menu 仍正确记录本次新 Defend，所有实际状态和其余字段不变；生产 reader 实际返回 **0**，预期 **1**。

位置：`NativeRewards.cs` 113–114 实际只为 PotionReward/RelicReward 记录外层 acquired；CardReward 的取得对象由 card-menu 的 acquired/acquiredCard 承载，因此外层 acquired 必须为 null。`NativeRewardContract.cs` 154–169 只对 Skip/Discard 和 Potion/Relic 校验这一字段，CardReward 分支仅比较 success 和完整状态，未检查外层 acquired 的来源。于是一个包可以同时声称取得真实新牌和一个旧对象，仍被验为可信。

最小修复：明确当前有限结果结构，对不会在外层记录 inventory acquisition 的奖励类型要求 acquired 为 null；继续在 card-menu 校验实际新牌。若以后统一改变结果结构，则必须同时改真实记录来源并绑定到子结果，不能直接相信任意已登记对象。当前无需为此引入 4D 的替换接口。现有正例中 24 个 GoldReward/CardReward 外层 acquired 全部为 null，见 `static-boundaries.json`。

## 复验与接续

将本轮 11 个误接受副本纳入实际生产检查器回归；覆盖这些边的缺失/错配、完整状态新增字段和合法等价，保留领取/跳过以及真实效果前后变化。原 R1 的整组必需菜单、唯一性、父子先后和 completion 修复已经成立；宝箱新实例/原生金币、卡来源升级的原 R3 修复也成立，无需重做已成立功能。

修复生产逻辑后重新冻结，并完成适用 1–4C 同构建回归与原 117 控制、原十反例、本轮反例及等价副本；保留原退出分类、失败前缀、自然24动作、重放/RNG及15输入/52源码不变。只读重现示例：

```powershell
dotnet .build/native-build/n3c-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3c-repair-independent-review01/independent-static-child-return-gold-treasure-extra
dotnet .build/native-build/n3c-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3c-repair-independent-review01/independent-item-return-card-acquired-old
```

当前上述命令均返回0，是本报告的失败证据。原始正常与历史证据未修改。

[4D 交接](NATIVE_RUN_N3D_IMPLEMENTATION_HANDOFF.md) 的商店、事件、精英/Boss顺序仍可用，但4C应先修复并复验。Driftwood reroll、真实 CardPileAddResult 替换/拒收、Sozu NotAllowed、Neow新会话迁移和修改后宝箱金币账本，仍是4D末/步骤5前的原生闭环要求；本次不把这些后续功能提前算作4C必修，也不以披露 Unsupported 代替最终M1闭环。

本轮无权限拒绝、挂起进程或执行结果不确定；阻断仅为上述已复现的数据契约问题。没有修改运行实现、角色、安装或用户存档，没有启动游戏UI、调用真实Steam/网络，没有提交或推送。全M1持续推进授权仍有效，修复接续无需逐步向用户请批。
