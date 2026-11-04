# 第三幕自然进展独立审查

2026-11-03。结论：**本轮声明的自然三胜、奖励、FakeMerchant 不交易离开及 Death474 范围通过独立审查。已保留的回合开始自伤死亡仍有必须修复的数据认证缺口；完整 M1 未通过。** 本次没有发现上述已通过路径的新误接受反例。

审查冻结为 `59EB82B365107C244603DBE2F530EB3538F6441BBF656AF9EB01E32CCC4DE9FE`。证据目录为 `.build/runs/act3-progress-independent-review01/`，入口 `closeout.json`，生产检查器实际输出位于 `reread/` 和 `controls/`。仅新增独立报告、审查脚本及副本，没有修改生产实现、原始证据、角色项目或安装目录，没有运行原生对局、UI、真实存档、Steam/网络或提交推送。

## 已确认的实际流程

- 同一正常 Neow 开局，前两幕原 367 个动作及 734 个完整派发/确认状态保持一致。
- 第三幕 a387 ScrollsOfBiting、a417 DevotedSculptor、a458 Fabricator 均有原生结束条件、清理前胜利回调及真实清理后结果。玩家存活，清理后 HP 分别为 76、46、26。
- 三场实际原生奖励金币分别为 14、14、18。前两场合法跳过卡牌；第三场实际取得 `i954 / CARD.TRUE_GRIT / upgrade=1`，进入永久牌组。不能把“完成奖励流程”概括成三次都取得卡牌。
- a460 通过原生 Travel 进入 FakeMerchant；a461 在实际 Travel 内记录当前六个原生库存对象及明确不交易决定，然后原生 `EnterMapCoord` 退出事件并进入 ConstructMenagerie。金币、永久牌组、遗物、药水在该次离开前后未被交易改变。
- a474 在敌方阶段真实死亡，第三幕第 6 层、HP0、金币557；原生死亡回调、474/474 确认前缀与终态一致，`WinStarts=0`。没有第三幕 Boss 胜利或 RunVictory。
- a405 原生 Fortifier 将 Block10 变为30，HP75不变，药水实际消耗。策略只排序合法动作，实际伤害/格挡仍由原生方法执行。

FakeMerchant 原生 `BeforeEventStarted` 从九种假遗物洗牌选六个并生成实例，初始选项为空；`EventModel.SetEventState` 将其标记完成。继承的 `OnRoomEnter` 无业务内容。离开走原生 `EventRoom.Exit → EnsureCleanup → FakeMerchant.OnEventFinished`，不交易是合法策略选择，没有补造 EventOption 或空业务动作。当前只覆盖不交易离开：价格没有查询，`pricesEvaluated=false`；购买、FoulPotion 战斗仍未覆盖，不能据此声称整个自定义商店已支持。

## 分层证据与有限对抗控制

1. 独立核对 96 份实际 dirty 源文件、60 编译输入、3 游戏输入、7 产物；规范化 manifest 哈希与目标冻结一致。最终十次原生执行的 manifest、process、result、原始退出状态和同构建关联一致。15 锁定输入、52 兼容源码未变。
2. 重验完整既有 693 份检查器证据：381 接受、312 拒绝，均符合预先声明的退出预期。包含原始正常、失败与历史腐坏副本；**拒绝坏副本或保留已知失败不是原生业务成功**。原 v5 Unsupported 保留459确认/460派发；v11 Death448仍由检查器拒绝。
3. 新增15个独立有限控制，实际调用当前生产检查器：2个正常/属性顺序等价副本接受；13个错误副本拒绝。覆盖当前事件主体、必需标签/额外字段、嵌套RNG、共同改写派发与拒绝观察后恢复、库存登记所属动作、模型缺失/null、跨动作借用、死亡HP/RNG、奖励动作归属及伪改RunVictory。拒绝发生在语义或必需字段门，并非只靠封条；副本均独立重封，原件不改。
4. 43项独立事实检查通过。live、frozen replay、minimal、off 的948个完整派发/确认投影相等，包含run/player每个RNG的counter与s0–s3，route/choices/reward-choices/final-state一致。该比较不等于全部隐藏机械状态已采集。
5. 3698项来源/绑定断言通过，3255个独立锁定文件结束时无变化。计数包含重复绑定断言，不能解读成3698个独立场景。当前SDK为10.0.201。游戏输入仍为固定v0.111.0的已锁定程序集；没有重新构建或重跑原生矩阵。

独立事实脚本最初错误假设每场奖励都新增卡牌，得到两项断言失败。实际记录表明两场选择Skip；初次脚本及结果保留在 `facts-initial-assumption.*`，更正为“菜单实际取得实例与永久牌组增量一致，合法Skip保持牌组”后43项通过。未更改游戏记录或放宽生产检查器。

## 已复现的自伤死亡认证缺口

来源 `.build/runs/act3-strategy-dev10-main/`，不是最终采用的策略。独立生产读取输出为 `reread/662/review.log`：exit1，`Major loss input complete latest native history state`。原生raw0与`DataComplete=true`仅是producer结果，不能覆盖此次认证失败。

同一a448的trace第6605行（seq6605）是epoch38/index215的致死 `DamageReceivedEntry`：第7回合、Player/Start、玩家对自己造成1点不可格挡伤害、HP0、WasTargetKilled=true。紧接第6606行是 `battle-loss-start`，完整状态只发生两处变化：

- powers：Strength3、Inferno6、Stampede1 → 空数组。
- combat.phase：Start → End。

该epoch的原生历史索引1–215完整，未发现漏掉CombatHistory条目。精确快照、差异及原记录保存在 `facts.json`。从锁定游戏DLL重新反编译的只读本机核查副本位于审查目录 `native-source/`，不属于公开代码：

- `CreatureCmd.cs:320` 在伤害后钩子和Kill之前写入DamageReceived历史；`:431`再执行Kill。
- `CreatureCmd.cs:548`附近的死亡分支执行AfterDeath，`:564`附近调用 `RemoveAllPowersAfterDeath` 并逐项await AfterRemoved，随后清理Orb/停用玩家钩子；`Kill`最后调用LoseCombat设定pending loss。
- `Creature.cs:673`按原生保留条件移除能力，并不是所有死亡一律无条件清空。此次三个能力均消失与这条路径一致。
- `CombatManager.cs:924`抽牌后调用AfterPlayerTurnStart；`:802`起发现死亡玩家则自动SetReadyToEndTurn。原生回合循环随后进入EndPlayerTurnPhaseOne，`:1553`设置End，`:1560`检查pending loss，最终进入`:1277`的ProcessPendingLoss。
- `InfernoPower.cs:30`附近的AfterPlayerTurnStart确实有对自身施加不可格挡伤害的原生调用，与当前机制、阶段和伤害记录相符。但历史只给Dealer/Receiver且CardSource为空，没有直接记录触发模型；不能把静态推断当已记录的source身份。

因此，两处变化具有合法原生语义。`NativeObservation`订阅历史Changed，在原生后续死亡钩子之前采样；`NativeLifecycle`在ProcessPendingLoss入口才再采样。两者之间缺少可验证的死亡/阶段迁移观察边界。`NativeMajorContract.cs:308`把两个不应恒等的时点强制全等，造成真实记录被拒绝。这不是缺失一个DamageReceived条目，也没有证据证明是GamblersBrew语义实现错误。

同一实验的terminal-stamp仍写`native ProcessPendingLoss after actual enemy action`，源于 `Program.cs:186`固定文本。实际致死发生在下一玩家回合开始，修复时须改成准确的pending-loss说明，或从已观察的伤害/生命周期事实派生原因；未知触发来源应明确未知。

## 最小修复和验收要求

后续实施应先关闭此已复现缺口，再继续后段策略和Boss，不必等待用户逐步批准：

1. 在原生死亡和回合阶段真实调用点增加只观察、严格审计的有限生命周期记录，至少能绑定致死历史的root/context/战斗/玩家、实际能力移除和真实阶段变更，再与ProcessPendingLoss入口及完成状态接续。若包装Task必须await真实任务、传播fault/cancel；不得用外层调用返回代替完成。
2. 以这些原生边界为状态锚点：静止点完整比较，合法操作只允许已证实的实际变化，未知额外钩子变化明确报不支持或数据不完整。保留能力可能不被移除的原生规则。不要简单删掉完整状态比较、允许任意powers/phase差异，或改写旧历史快照使其看似相等。
3. 用旧v11动作/选择证据复现真实同类自伤死亡（可另标明专用原生夹具作补充），保留旧失败文件。验证详细、minimal/off一致及真实确认前缀；原敌方死亡474、合法死亡清理和失败传播应保持。若重放身份变动，明确新旧版本身份映射，不能换种子悄悄取消该病例。
4. 生产检查器负控覆盖新记录缺失/重复/错序、借旧root/context/实例、非法能力保留或移除、阶段矛盾、共同篡改后恢复、未完成任务/异常。错误不得被native raw0或producer DataComplete掩盖。
5. 这会改变采集链，应重跑受影响原生死亡路径、至少一条旧自然烟测和观察/重放对照；历史原始证据完整重验，说明旧版缺锚点的兼容边界。无需现在重复全部原生矩阵，最终M1同构建全量门仍保留。

本次审查到此收口，不要求为任意协同伪造整份日志构造语义解释器。剩余第三幕Boss、Architect/RunVictory、必要机械数据投影和最终全量验收仍待推进。当前无环境或权限阻塞。
