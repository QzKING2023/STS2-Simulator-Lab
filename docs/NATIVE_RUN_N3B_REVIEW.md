# M1 步骤 4B 独立验收：两项数据完整性必修

日期：2026-11-02。结论：**暂未通过；原生多节点执行证据成立，B10 生产完整性门仍有两项必修。** 本轮只读实现和原始运行证据，新增独立审查副本与本文；没有修改运行实现、重新运行游戏场景或进入 4C。

冻结：`AF641CFD85515D90898375F3C2C75E5A77CEB89F8D349E1D33F370F475587A16`。NativeRun DLL SHA256：`FE6D4810A3933B03D7955F9276377D90A8266850C88280162D4B45D458E382B4`。

审查入口：`.build/runs/n3b-independent-review01/summary.json`；独立脚本 `review.py`、`regression_review.py`；独立重新计算的构建标识 `build-id-check.json`。实现报告为 [N3B_VALIDATION](NATIVE_RUN_N3B_VALIDATION.md)，验收要求为 [N3B_HANDOFF](NATIVE_RUN_N3B_IMPLEMENTATION_HANDOFF.md)。

## 已确认的实际执行与范围

| 原始场景 | 独立核对结果 |
| --- | --- |
| DEV_01 | 正常 Neow 开局，同一进程、同一 Run，未出现 fixture setup。71/71 动作、7 房间、3 次原生战斗胜利，SunkenStatue 与 DenseVegetation 实际事件效果、Rest Smith、下战真实 Play；最终 HP50、Gold468。选择 `o1` 对应永久对象 `i2`，实际升为一级；后续战斗副本具有该 DeckVersion，未要求首手抽到。 |
| DEV_02 | 138/138，真实敌方 EndTurn 经 ProcessPendingLoss，Death/Completed、HP0、Losses1、WinReturns0。Neow 两次独立 CardReward 选择与完成均在同一 opening 根动作内，指定的两个不同对象实际进入永久 deck。 |
| DEV_03 | WhisperingHollow 实际入房时 Unsupported，77 confirmed / 78 dispatched，exit2，LastBoundary=SkipRewards:77，DataComplete 与 seal.complete 均为 false。保留已执行但未确认的 Travel 前缀；最终物理状态未冒充最后确认状态。 |

`NativeRooms.cs:82` 等待原生 RestSiteSynchronizer.ChooseLocalOption 的 Task<bool>；Smith 经真实 CardSelectCmd.FromDeckForUpgrade 和 CardCmd.Upgrade。取消返回 false，并保持选项与完整状态，之后实际重选成功。显式 fixture 通过原生 EnterRoomDebug 与 CreatureCmd.Damage 建立输入，Heal 40→64、上限 75→80、AbyssalBaths ABSTAIN 40→50、IMMERSE→EXIT 40→39/maxHP82 的独立规则与原始证据相符。

`NativeRooms.cs:157` 使用原生 EnterMapCoord 进下一合法节点；自然流程的原生 Travel 负责退出旧房间，没有另外调用第二次 Exit。事件由 EventSynchronizer 的原始选择及任务执行；已审计三个事件模型的 OnRoomEnter 均为继承的基类空实现，Lab 补调用有精确类型限制。未知模型先检查故障账本，再分类 Unsupported。

连续 Neow 奖励的修改位于 `NativeChoices.cs:144`：保留原候选引用，等待原 CardReward.OnSelect Task 完成，再核对对应永久 deck 对象并结束 pending 选择。原生静态参考确认实际获得发生在 CardPileCmd.Add，原候选随后会被移除，Lab 没有手动添加卡牌。此结论仅覆盖当前普通奖励对象；alternative、替换 Hook 等仍在 4C 范围。

`room-task-fault` 在真实治疗后注入 Task 故障，TaskHelper 的原始异常被 NativeHost.Ensure 传播为 EngineError，保留 3/4 前缀；没有误判房间完成。预算控制与自然失败分别标注来源。自然 DEV_01/02 冻结重放，以及 DEV_01、Heal、Phases 的 detailed/minimal/off 对照，完整 route、choices、final-state 文件一致，包含完整 RNG 流。

## R1：选择结果边界未绑定唯一的已确认请求

必修位置：`tools/native-run/NativeRoomContract.cs:94`、`:110`、`:155`。

生产检查器只验证 `room-option-boundary` 引用的 request 曾有 return，以及它的结果字段等于该历史 return；没有验证这个 request 属于刚刚确认的根动作，也没有检查 boundary 恰好一次。Finish 只比较 starts 与 returns。

以下三个独立、重新封装合法 hash/seal 的坏副本，均被最终生产 DLL 的 `--review` 接受（exit0）：

| 副本目录（均在独立审查目录下） | 实际改动 |
| --- | --- |
| `control-independent-missing-option-boundaries` | 删除取消和重选的全部 `room-option-boundary`。 |
| `control-independent-duplicate-option-boundary` | 重复一次取消结果边界。 |
| `control-independent-stale-cancel-boundary-attached-to-upgrade` | 第二次升级的边界仍归属 a5，却把 request 从 s1q2 改成 s1q1，并将 success=true 改成上次取消的 false。真实 a5 return 与 confirmed 仍表示升级成功。 |

这会使消费者漏掉选择结果、重复计数，或把旧答复归到新动作；属于本轮已要求的选择身份、顺序与数据完整性范围。

最小修复：为每个选择请求保存不可变的 session、room、root/context、dispatch/start/return/confirmed/boundary 生命周期。return 必须属于当前请求；boundary 必须属于该请求刚确认的同一根动作，恰好一次，结果和完整状态匹配该次 return/confirmation。新请求、离开房间及成功 Finish 前检查已完成操作的 boundary 闭合，不能用“存在任意历史 return”代替。失败中断仍应保留不完整前缀，不能补写虚构完成。

## R2：Travel、战斗及奖励记录存在动作与完整状态绑定遗漏

必修位置：`tools/native-run/NativeRoomContract.cs:67`、`:119`、`:122`—`:124`。

当前 confirmation 与原生返回完整状态比较只适用于 RoomOption/RoomExit，漏掉实际承担房间退出的 Travel。战斗胜利与奖励完成的分支只推进布尔标志，没有关联真实活动根动作/最后确认根动作，也没有校验静止边界的完整状态。

以下六个独立坏副本同样被生产检查器接受（exit0）：

| 副本目录 | 实际改动 |
| --- | --- |
| `control-independent-travel-return-gold-drift` | 首次跨房 Travel 的 `room-exit-return.state.gold` 改为999，随后原始 confirmed 仍为266。 |
| `control-independent-detached-battle-victory-before-cleanup` | 首次胜利回调 root/context 改为不存在的 a0/missing-context。 |
| `control-independent-detached-battle-victory-cleanup` | 首次战斗清理完成同样脱离真实动作。 |
| `control-independent-static-battle-victory-cleanup-gold-drift` | 首次清理完成快照 gold 改为999，已确认真实值仍249。 |
| `control-independent-detached-rewards-completed` | 首次奖励完成脱离真实已确认动作。 |
| `control-independent-static-rewards-completed-gold-drift` | 首次奖励完成快照 gold 改为999，前后真实确认值仍266。 |

最小修复：将每个房间生命周期操作绑定到 session/room 实例及明确的根动作。对于有旧房间退出的 Travel，原生返回后到 confirmation 之间的完整结构应相等；首次入房没有旧退出操作，不能误加条件。胜利前回调必须属于当前战斗的实际活动根动作；清理完成必须紧随对应已确认动作且关联同一战斗。奖励完成需与当前战斗的奖励操作及最后确认边界关联。对于这些静止观察边界使用完整不可变快照比较，而非增加 gold 等逐字段特判；真实业务执行中的合法清场与资源变化应保留在执行区间内。

这些修复应落在生产语义检查器及其实际状态模型中，不只在 suite 汇总脚本识别副本名。原 4A 的 Closed false→true 特例不应泛化到新资源字段。

另行核对 DEV_01/02 共24个现存 Travel返回、战斗清理完成、奖励完成边界，其完整快照均与对应真实 confirmed 相等，见 `closeout.json`。这些正常实例为上述绑定提供正向证据；不得把执行区间内的清场前快照也强行等同到清场后。

## 独立核查数量与复现

本轮执行 **306 次最终生产检查器**，没有启动新的原生游戏场景：

- 18 个新独立副本：2 个等价副本通过，16 个损坏副本中7个正确拒绝、9个误接受。
- 23 个本轮既有 room 控制，9 个本轮正常包，全部符合原期望。
- 163 个历史包、88 个当前 4A 控制、3 个当前 4A 正常包、2 个 N2 skip/take 正常包，256 次全部符合期望。历史包仍标记原 E261（151）或30C9（12）生产身份，读取器使用最终 AF641；未将它们算作最终构建重新执行的游戏证据。
- 178 项独立元数据、原始结果、实例、重放及观测核对全部通过。当前四种 raw exit 数量为80×0、39×1、2×2、3×3，与原124进程逐一一致，非零预期结果没有被外层通过掩盖。

原始统一矩阵的34+16+74=124进程、195+273+580=1048检查，以及 Core98、497补充检查均核对一致。Core98 为已有实际执行及同 Core 产物核验，本轮没有再次运行 Core。42 source、25 compiled、3原版 inputs、7 artifacts 的 hash 与重新计算 Build ID 一致；15 Mod inputs、52兼容源码未变。复制来源的128个当前原始文件和1024个回归原始文件分别做前后 hash 检查，均保持；本轮未修改这些证据。

直接复现任一反例，无需游戏初始化：

```powershell
dotnet .build/native-build/n3b-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3b-independent-review01/control-independent-stale-cancel-boundary-attached-to-upgrade
dotnet .build/native-build/n3b-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3b-independent-review01/control-independent-travel-return-gold-drift
```

当前错误结果为 exit0。副本中的链、计数、seal 均已按生产封装重新计算，误通过发生在语义门，不能用通用 seal 校验修复代替。修复验收应要求九个坏副本拒绝、等价副本和真实业务清场继续通过，保留原失败证据，再针对最终新冻结完成约定矩阵。

## 4C 交接与仍未覆盖的范围

[4C_HANDOFF](NATIVE_RUN_N3C_IMPLEMENTATION_HANDOFF.md) 的依赖与接口方向可执行：先奖励集合及嵌套 Offer 的根动作归属，再原生 Relic/Potion 获得、Treasure、Potion 使用；保留既有 legacy skip/take 回归。**进入4C前先闭合上述 R1/R2 并独立复核。** 不需要因此改变主路线或新增 Mod 专项。

当前只证明限定事件、Rest 和原版多节点片段；普通选择、展示隔离、TestMode及原生程序集之间的边界仍按既有报告。没有证明所有事件/替换 Hook、商店、药水、精英Boss、正常开局跨幕完整通关、策略胜率或所有零售演出/RNG等价。一Run一进程，Death不承诺零售run-loss清理或同进程复用。4C–4D、5–6仍待执行。

当前没有权限或执行环境阻塞。没有运行游戏UI、真实Steam/游戏网络、用户存档，没有修改Mod/角色安装，没有提交或推送。待修的是生产数据验证缺口，原已成立的原生执行成果应保留。
