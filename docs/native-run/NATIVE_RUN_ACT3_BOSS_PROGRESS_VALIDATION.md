# 第三幕自然休息、精英奖励与 Boss 到达

2026-11-03。本轮正常开局的同一 Run 已从旧 Death545 推进到第三幕 TestSubject Boss，第八回合 **Death610**。此前实际完成 MechaKnight 精英胜利、原生 Orichalcum 奖励领取及后续节点。定向验证通过，待独立审查；**第三幕 Boss 胜利、RunVictory 和完整 M1 仍未完成**。

最终源码/构建冻结为 **`D7044F05BB2123D9C555CD92B5EB430A38D46E1B5D527073194EB41440DD9E76`**，manifest 在 `.build/native-build/act3-boss-progress-dev08/`。实际自然执行在 `.build/runs/act3-boss-progress-dev08/`；同构建矩阵在 `.build/runs/act3-boss-progress-final01/summary.json`；总入口为 `.build/runs/act3-boss-progress-work01/closeout.json`。这些均是本地忽略的执行证据，不属于拟发布的游戏内容。

## 实际解决的问题

旧路径在 `(1,9)`、HP8/maxHP96 时没有选择立即可达的 `(0,10)` Rest，而走向 `(2,10)` Unknown。当时金币再次达到找商店阈值，`seekShop` 优先于 `seekRest`；剩余公开地图实际已无可达商店，`ShopDistance` 的 1000 哨兵值仍被用于排序，导致休息策略失效。

`NativeThirdAct.cs` 现在先在公开 DAG 验证确有可达商店，才允许找商店覆盖找休息。没有读取未揭示节点内容或未来 RNG。最终策略版本为 `oracle-legal-natural-act3-route/v23-reachable-services`，保留原版战斗和奖励排序。新 Run 的前 **527 个动作、1054 个派发/确认完整 payload** 与旧自然路线一致；a528 才实际改走合法 Rest。原生 HEAL 使 HP8→51、maxHP96 保持：固定版基础休息回复为向下取整的 30%，已持有 RegalPillow 再加 15，即 `8 + floor(96×0.30) + 15 = 51`。

本轮最初的独立事实断言遗漏 RegalPillow，错误期待 HP36，已保留失败于 `facts-initial-rest-expectation.json`。核对锁定程序集的实际遗物实现后修正独立契约，未改游戏或 Lab 的回复执行，也未为此重跑原生矩阵。

休息后真实战胜 `MECHA_KNIGHT_ELITE`，早期进程在第 569 次派发处停止为 Unsupported：568 个已确认动作，金币奖励已由原生领取，Orichalcum 尚未领取；原始退出 2，未伪报完成。它保存在 `act3-boss-progress-dev07`。后续进程从正常开局重新运行，前 568 个完整动作边界相等，没有拼接存档或失败前缀。

`NativeRewards.cs` 与 `NativeRewardContract.cs` 为本次实际生成的 Orichalcum 增加窄的原生取得契约。只允许精确的 MealTicket/Orichalcum 类型、无 UponPickup 效果、继承默认 `RelicModel.AfterObtained` 且该方法无 Harmony patch；其他遗物继续显式 Unsupported。实际领取仍调用原生 `SelectLocalReward`。生产检查器要求本次 offered/acquired、新增具体身份/模型与完整状态一致，仅允许 relics 增加该对象。Orichalcum 的后续战斗 Hook 继续由原生执行；这里没有复制其格挡效果或增加公开 Mod API。新增取得审计的实际 Harmony 注入负控本轮未运行，不把源码中的拒绝检查称作已注入实测。

## 实验与保留的失败

本轮先调查了计算伤害、群攻和召唤物目标排序。它们没有改善该固定样本，最终运行代码已恢复原战斗/选牌策略。全部实验和各自构建保存，未换种子、未删失败、未合并成胜率样本。

| 自然开发进程 | 策略尝试 | 实际结果 |
| --- | --- | --- |
| dev01 | v18 收紧第二幕起选牌、计算伤害/群攻 | 第二幕 Boss Death347 |
| dev02 | v19 收紧范围到第三幕 | Fabricator Death442 |
| dev03 | v20 主体优先 | Fabricator Death430 |
| dev04 | v21 收回群攻/选牌，仅计算伤害和主体优先 | Fabricator Death434 |
| dev06 | v22 仅当前存在召唤物时改目标 | Fabricator Death437 |
| dev07 | v23 可达服务修复，恢复原战斗策略 | 第三幕精英已胜；领奖 Unsupported，568/569，退出 2 |
| dev08 | 同一 v23，补 Orichalcum 原生取得契约 | 第三幕 Boss Death610，退出 0 |

dev05 在编译时发生局部变量重名，未启动原生执行；失败日志保留，修正后使用新目录。各策略的声明先于其运行，见工作目录的 `experiment-v18.json` 至 `experiment-v23.json`。这些是同一种子的开发比较，重复运行用于复现，不能估计独立胜率。

## 同构建定向验收

最终构建 SDK10.0.201，编译成功、两个诊断计数为零；manifest ID 已独立重算。相对上一独审冻结 A3A35E44…，只有四份生产源码变化：`NativePreparation.cs`、`NativeThirdAct.cs`、`NativeRewards.cs`、`NativeRewardContract.cs`。`NativeThirdPolicy.cs`、`NativeAct2Policy.cs` 已逐字节恢复，采集器/Core/其他检查器未改。15 个固定输入、52 份兼容源码保持。

- **9 个同构建原生进程**：本次自然 live、其 detailed replay/minimal/off、旧 545 路线冻结重放、原版 skip24/take25、实际故障和预算前缀。各原始退出与报告一致；故障仍是 EngineError 3确认/4派发、退出1，预算仍是 BudgetExceeded 4/4、退出3，二者检查器退出1。
- 本次 live 与三种重放/观察模式的 **1220 个完整派发/确认 payload**（包括全部投影状态、RNG、root/context）、动作、子选择、奖励选择及最终状态相等。旧 545 冻结路线的 1090 个完整边界和选择/状态亦相等。
- **80 份**受影响冻结原始/历史/控制副本，用最终生产检查器重验，全部符合预期且原始输入未变。包括旧商店六反例、已有商店正负控、失败/预算前缀和本轮各旧策略结果。旧 11 份缺玩家锚点、2 份缺 maxHP 中间观察继续未认证；dev07 Unsupported 前缀也不能被新能力误认证完成。
- **14 个**新增 Orichalcum 生产控制：2 合法/等价接受，12 错误拒绝，覆盖错误具体类型/类别/model、新旧对象错配、取得后的非法金币/额外状态变化、失败语义、缺失/重复/脱离当前动作。副本合法重封后实际调用 `--review`，不是依靠 seal 损坏拒绝。
- 总计 **103 次最终检查器调用**（9+80+14），**308 项**指纹、来源、原始退出、完整状态/RNG、公开地图、原生休息、精英与奖励事实核对通过。中间 v22 构建的另 78 次重读单列保留，未混算成最终同构建证据。

本轮按[分层原则](../architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)只重跑受影响原生路线和上述烟测。Core 与更早全部节点矩阵未重新执行，不宣称全量回归；最后 M1 同构建全套验收仍保留。哈希证明来源一致，实际执行结论同时依赖受控进程、原生命令任务、原始生命周期与重放。

## 剩余范围与折扣原价

当前自然 Run 确有第三幕精英胜利和原生奖励，Boss 实际执行到死亡。没有 Boss 胜利奖励，没有 Architect，也没有 RunVictory。策略在 TestSubject 的后续变化中仍不能存活；不能把合法死亡当完整胜利路径覆盖。

本次路径只有一个集成 MerchantRoom，入店时尚未持有 MembershipCard，原价来源仍满足已审查契约。没有遇到“预先已有折扣遗物再次入店”的输入；这类原价采集继续未覆盖，检查器的拒绝门保留。后续有实际需要时采用窄的版本化采集：在固定版原生 entry 上只读采集 `_cost` 为独立 rawCost，保留 Cost 的实际显示值；绑定同一房间/库存/entry/生成代次，验证进入、购买折扣变化及出栈恢复的完整状态。用明确 API 夹具加一次后续自然可达路径，验证已持有 MembershipCard 的奇数原价、真实扣金、离店及观察/RNG对照；负控包含 rawCost 错来源、共同修改后恢复、显示价冒充原价、过期 entry 和错登记模型。原生生成/采集一旦变化，仅重跑受影响商店/离店路径和适用原始证据，再决定扩大范围，不无条件重跑全部原生矩阵。

MemorySaveStore、保存/replay 拦截、Steam 未初始化、UI 空实例保持；无角色/安装/真实存档修改，无提交推送。后续见 [本轮交接](NATIVE_RUN_ACT3_BOSS_NEXT_HANDOFF.md)，无需重新请求已授权步骤的许可。
