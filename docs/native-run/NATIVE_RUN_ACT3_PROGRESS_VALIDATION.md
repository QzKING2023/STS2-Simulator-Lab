# 第三幕策略、FakeMerchant 拒绝交易与自然第六层

2026-11-03。本轮封版完成了自然FakeMerchant的库存观察、拒绝交易和原生离开，以及第三幕三场胜利/奖励。最远自然路径为**Death474，第三幕第6层**，尚未到达第三幕Boss或RunVictory。实施与分层证据交独立审查；完整M1继续推进。

冻结 `59EB82B365107C244603DBE2F530EB3538F6441BBF656AF9EB01E32CCC4DE9FE`，清单 `.build/native-build/act3-strategy-final01/build-manifest.json`，96份真实dirty源码、60编译输入、3游戏输入、7产物。构建0警告、0错误。原型dev08的一条可空警告已在封版前修复，没有运行该原型构建的原生对局。15锁定输入及52兼容源码继续固定；没有用HEAD代替dirty源码身份。

## 已实际推进

封版策略为 `oracle-legal-natural-act3-route/v12-custom-event`。全程同一正常Neow开局，原生战斗、奖励、地图和跨幕；前两幕367条动作及734个完整派发/确认快照，与已通过基准完全一致。

| 实际动作 | 原生事实 |
| --- | --- |
| a369 | 实际Tanx选择ThrowingAxe，原生取得并执行战斗效果 |
| a387 / 后续奖励 | ScrollsOfBiting胜利，HP76 |
| a417 / 后续奖励 | DevotedSculptor胜利，HP46 |
| a458 / a459 | Fabricator胜利，HP26；原生金币/卡奖励，金币557 |
| a460 | 原生进入FakeMerchant，实际BeforeEventStarted生成库存；继承的OnRoomEnter无额外逻辑 |
| a461 | 记录当前六件原生库存，明确选择不交易；同一Travel实际执行EventRoom退出并进入下一战 |
| a474 | ConstructMenagerie真实敌方动作致死，HP0，第6层；无该战胜利/奖励或RunVictory |

FakeMerchant的合法离开要求原生IsFinished、零事件选项、未启动事件战斗及ShouldProceedToNextMapPoint为真。它没有普通EventOption，政策决定记录在真实Travel派发内部，随后由原生EnterMapCoord拥有退出；没有新增空动作充当业务完成。价格未查询，记录`pricesEvaluated=false`、购买0。当前覆盖**不交易离开**；购买、显示价格及FoulPotion事件战斗尚不在此闭环中。

`NativeCustomEvent.cs`直接读取实际Inventory.RelicEntries，登记真实对象；生产检查器要求当前动作/房间/事件归属、必需且唯一的离开决定、完整派发快照、六个不同模型及实例、本动作首次登记、实际类型/model、仍有库存且未持有、封闭字段与随后真实Travel确认。真正跨房业务变化沿用原生命周期规则。

## 策略实验及未通过项

所有版本、输入、失败和原始退出保留在`.build/runs/act3-strategy-dev*/`。开发样本一直是DEV03；另有原预登记DEV01/02回归，没有新种子替换失败。以下探索不构成胜率或全局最优结论。

- v3/v4增强致死压力下的防御，但仍在Fabricator Death430。
- v5修复带格挡攻击牌在无防御需求时被过滤的问题，改进Armaments及Fortifier时机；实际击败Fabricator，随后FakeMerchant Unsupported，459确认/460派发、原始exit2保留。
- v6按公开地图偏好已知节点，走另一条真实合法边后Death473；没有沿该失败路径跳过房间。
- v7–v9的全程领奖/统一战斗排序没有改善结果，分别保留第二幕Boss或第一幕死亡；未采用这些改动。DEV01/02的相应失败也保留。
- v10完成FakeMerchant不交易离开，Death474。
- v11尝试GamblersBrew原生弃抽，实际执行后Death448，但生产检查器拒绝致死状态认证。该实验未采用，当前封版不使用这项应急弃抽策略。

v11的拒绝揭示了**M1仍须处理的死亡边界**：最后历史记录是玩家回合开始、玩家对自身的1点不可格挡伤害；到致死回调之间，powers从三个力量/Inferno/Stampede状态变为空，combat.phase由Start变End。现有检查器要求最后历史状态与loss-start完全相等，因此拒绝。差异见`experimental-brew-loss-diff.json`。原生AfterDeath/回合开始清理链尚须核验，不能直接放宽完整状态校验，也不能将此证据算作通过。它不能被简单归因为药水接口不兼容。

封版只采用第三幕局部的合法防御/混合攻击排序及Fortifier使用，保留前两幕基准。a405实际Fortifier使Block10→30、HP75不变并移除药水；与固定原版效果“增加当前Block的两倍”的独立规则一致。角色参数、资源及永久牌组均由原生业务改变。

## 验证范围

按[分层原则](../architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)运行受影响原生流程，最终同构建10个进程：主路径live/replay/minimal/off、DEV01/02、原有限第三幕首战387、原DEV01 skip24/take25，以及原第二幕367动作冻结重放。全部通过生产读取检查；实际类别为6 Death、4 SliceBoundary。

主路径live/replay/minimal/off的948个完整派发/确认投影、动作、选择、奖励选择及最终状态相等；包含run/player每个RNG流的counter及s0–s3。新FakeMerchant观察与拒绝交易未改变这些对照结果。原生隔离、MemorySaveStore、保存/replay拦截、无UI/Steam初始化/远端网络及无Mod/CombatSolver主路径均回归。

另用封版生产检查器重验全部652份上轮集合、本轮11份开发原始记录及10份封版记录，共673次；新20个生产控制验证缺失、重复、错序、归属、完整静态快照、库存实例/type/model/数量、价格未采集及拒绝交易语义。认证集合为693次读取。v11读取拒绝作为**已知未通过实验**单列，表示拒绝机制保持，没有计为业务正确性通过。额外的逐原生运行读取和诊断调用不重复计入该集合。

证据目录`.build/runs/act3-strategy-work01/`，关键文件为`closeout.json`、`facts.json`、`evidence/summary.json`、`controls/summary.json`、`source-final01.json`；封版原生矩阵为`.build/runs/act3-strategy-final01/summary.json`。旧证据没有改写producer身份，开发版本不混作同一生产构建。Core98使用已说明的同源码/产物历史证据，本轮未重新执行；最终M1全量门保留。

## 六步状态与接续

步骤1–3原有验收基础保持；步骤4补齐了本次遇到的自定义事件合法离开，普通商店自然组合等仍有剩余；步骤5推进到自然第三幕第6层，Boss与RunVictory未完成；步骤6必要数据、预算评估及最终同构建全量验收尚未执行。详见[后续交接](NATIVE_RUN_ACT3_PROGRESS_NEXT_HANDOFF.md)。

完整投影仍不是全部隐藏机械状态；Surrounded.Facing、遗物计数等必要数据门继续保留。没有角色/游戏安装修改、真实存档/Steam/网络/UI操作、Mod专项或提交推送。
