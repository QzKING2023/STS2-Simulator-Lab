# M1 步骤4C：原生奖励、宝箱与药水验证

后续状态：本页保留6392BBCE构建的原始实现报告。其独立审查发现三组数据校验必修，见[审查报告](NATIVE_RUN_N3C_REVIEW.md)；当前修复及替换同构建回归见[审查修复](NATIVE_RUN_N3C_REVIEW_REPAIRS.md)，独立复审待执行。以下旧计数不代表原候选已独立验收通过。

日期：2026-11-02。4C实现与最终同构建验收已完成，交独立审查。依据[4C交接](NATIVE_RUN_N3C_IMPLEMENTATION_HANDOFF.md)，当前完成原生奖励集合及其子选择、Card/Potion/Relic取得、宝箱业务链和药水实际使用；4D及步骤5–6继续待办，完整M1尚未完成。

## 实际结果与覆盖

新自然profile从正常Neow开局，在同一Run内完成首战原生Victory，真实生成并领取Gold/Potion/Card，合法进入下一战，核对取得卡的永久牌／战斗副本关系，并实际使用领取的BlockPotion。最终24/24确认根动作、HP70、金币266、永久牌11张，药水从真实槽位消耗。全程native-only/NotCompared，无Mod或CombatSolver前置，没有摆放首手或补自然流程资源。

根动作现在可拥有完整原生任务；奖励领取、卡菜单、嵌套集合是子操作，不能将24个根动作理解为全部游戏效果或与旧24步切片逐一对应。

| 验证组 | 实际覆盖 |
| --- | --- |
| 集合 | 原生Gold7、Gold0、空集合、合法skip、disallowSkip、两次连续7+11金币；原生完成／跳过同步器 |
| 卡奖励 | 两个真实候选中取非0的Defend实例、原生Skip替代选项、完整永久牌内容/升级/引用保留；进入下一战后检查任意牌堆DeckVersion关联 |
| 嵌套奖励 | 真实Cauldron取得触发原生OnObtain与嵌套Offer，真实生成和领取药水 |
| 药水容量 | 固定3槽，真实满槽领取失败→原生丢弃→重试原生领取；最终FirePotion与其余原实例保留 |
| 遗物 | 原生RelicReward取得Vajra mutable实例，下一战真实AfterRoomEntered提供Strength1 |
| 药水使用 | FirePotion指定敌方目标20伤害、BlockPotion自用12格挡；原生入队、UsePotionAction、OnUseWrapper、Hook/历史和确切实例消耗 |
| 宝箱 | 原生Enter触发生成，DoNormalRewards→DoExtraRewardsIfNeeded→原生投票分配→mutable RelicCmd.Obtain→原生Exit；take取得Gorget，skip不取得，两条均获得原生普通金币并正确关闭会话 |
| 故障 | 缺失/非法/过期/跨集合/多余/重复答复、禁止skip；实际旧药水丢弃替换后拒绝、错误/死亡目标、错误时点；原生领奖后/药水生效后故障保留未确认前缀 |

宝箱、固定卡候选、容量、Vajra、数值药水和死亡目标是明确API夹具。局部Debug房间和原生命令setup都有api-fixture/fault-control来源；它们不属于自然完成率或自然通关证据。宝箱候选Gorget受本固定Lab档案的原生首次宝箱规则影响，未手工指定它。

## 实现与执行语义

新增内部`NativeRewards`会话及有限`NativeRewardContract`，独立于旧4B/N2的单offer/正金币协议。4C的Neow EventOption也持有完整原生任务及嵌套奖励提供器，避免TestMode默认领取替代策略决策。操作有唯一ID、parent/root/context、一次答复、start、真实return、根confirmation后的boundary；保存不可变开始、返回与确认完整快照。原生变化保留在正确执行区间。

集合声明绑定真实集合ID、实例、全候选顺序、类型、金额和内容身份；子请求绑定当前集合、剩余奖励和真实槽位。静止阶段比较包含未知新增字段的完整结构。取得记录同时保留selected reward与实际claimed内容实例；卡奖励走原生OnSelect、同步与加入永久牌组，未手动append。

新的`NativeTravel`根等待原生EnterMapCoord及可行动Play后再确认，奖励卡随后通过全部战斗牌堆检查关联。药水记录`PotionSlots`而非把紧凑`Potions`枚举当槽号；手动使用按原生usage、时点、可用状态、当前战斗目标和实例守卫，实际effects后异常不补写return或确认。

宝箱的`RelicsAwarded`只是通知。无UI控制器按本版本UI所承载的业务顺序，存储并等待异步`RelicCmd.Obtain`任务和Hook；没有async void，也没有把通知直接当库存。新增展示seam仅是精确固定方法`PreloadManager.LoadRoomTreasureAssets`的资源加载；原生奖励、投票、取得、清理和RNG保留。既有Godot/保存/Steam隔离与补丁稳定性检查继续生效，不声明零售演出或所有视觉RNG等价。

## 冻结及可复现入口

- Build ID：`6392BBCE3ACEF0AD5C1B53DCD5628066F84031B7860368F306AA469D084CF3DB`。
- Native DLL SHA256：`E14EFF2BABF66D91EF52163D671DBB07F6B3E298908505B4A75C1CB733AB8458`。
- Manifest：`.build/native-build/n3c-final05/build-manifest.json`；0 warning/0 error。
- 汇总：`.build/runs/n3c-work01/summary.json`、`inspection.json`、`closeout.json`。
- 最终场景目录：`.build/runs/n3c-final05-suite/`、`n3c-final05-n3b/`、`n3c-final05-n3a/`、`n3c-final05-n2/`。
- 实际48份源码输入快照：工作目录`source-snapshot-final05/`及其索引；30个编译输入、3个原生输入、7个产物全部hash核对，Build ID独立重算一致。
- 固定游戏v0.111.0/41cef1ea，Ironclad/A0，原独立已解锁Lab档案，一Run一进程。

```powershell
pwsh -NoProfile -File tools/native-run/run-native-n3c.ps1 -BuildManifest .build/native-build/n3c-final05/build-manifest.json -OutputDirectory .build/runs/local-n3c-check
```

输出目录必须尚不存在并位于Lab`.build/runs`。旧三套runner使用相同manifest，分别为`run-native-n3b.ps1`、`run-native-n3a.ps1`、`run-native-n2.ps1`。离线检查原始包使用最终DLL的`--review`入口，不初始化新的原生场景。

## 同构建验收

| 集合 | 实际结果 |
| --- | --- |
| 4C | 55个原生进程、354项检查，0失败 |
| 4C正场景 | 16种×新执行/冻结重放=32条，另10条代表性minimal/off对照，共42次正执行 |
| 4C故障 | 13条原生进程，实际退出1及规定分类/前缀保留 |
| 4C生产reader控制 | 85份：83损坏拒绝，2等价接受；按完整payload独立检查实际变更 |
| 原4B | 34进程、307检查，0失败 |
| 原4A | 16进程、273检查，0失败 |
| 原步骤1–3/N2 | 74进程、580检查，0失败 |
| 最终原生总计 | **179进程、1514项suite检查**，均使用final05 |
| Core | 实际98项通过，使用Core DLL与冻结artifact相同 |
| 补充核对 | 636项，0失败，脚本及详细结果在工作目录 |
| 历史审查包 | 最终reader读取115份旧包，全部符合预期，460个源文件hash保持；输入包保留原构建身份，未称为新原生执行 |

原生退出分布为122×0、52×1、2×2、3×3。检查逐一对照子进程原始退出、result、trace header、构建和来源；预期错误、Unsupported、预算停止未被外层成功覆盖。

生产负控包括10类新state事件的missing/null/非对象、必需/唯一、归属、完整静止快照漂移、错误集合/类型/内容身份、遗漏合法候选、缺宝箱阶段。连贯改写实际领取后的金币，同时保留状态边界一致，仍被独立“金币7”契约拒绝；不能仅靠hash/seal或双方相等通过。

16种正场景在新进程冻结重放，比较route、普通choices、新reward-choices及最终完整状态；5组的minimal/off对照同时比较上述文件和完整RNG投影。固定数值包括7、7+11、0、槽位3、Fire20、Block12、Vajra Strength1；未从被测DynamicVars循环导出期待值。

旧DEV_01保留71/71、7房间/3战斗胜利/休息升级/后续Play；DEV_02保留138/138真实Death；DEV_03保留WhisperingHollow的77/78未确认Travel及Unsupported。原自然skip24/take25、RNG、重放与原有生产负控保持，业务状态与C632基线完整文件hash一致。15个Mod锁定输入和52个兼容源码不变，未扩大Mod专项。

## 修复过程与剩余范围

早期编译的命名空间/构造签名错误、CLI回放路径被进度输出污染的失败均保留。dev04的16条正例通过属于旧候选证据，未合并为最终同构建结果。

final04完整4C矩阵发现满槽恢复策略在丢弃后仍保留失败标记，实际选择了skip；独立容量断言正确拒绝两条证据。修复为丢弃成功后清除失败标记、重试真实领取，未放宽断言。final05重新冻结并完整执行所有适用矩阵；该缺陷及旧失败包保持原样。

当前认证范围为上述固定场景与类型。未知奖励类型明确Unsupported；同一次CardReward.OnSelect内的额外重选/reroll，以及取得时替换卡实例/特定药水替换Hook，仍有明确停点或未覆盖边界。没有宣称覆盖所有遗物、药水、修饰器、触发链、商店、事件或Boss组合。当前自然路径证明奖励到下一战的真实数据连续性，不是整幕/完整run或胜率验收。

新增代码、复现脚本及[数据契约扩展](NATIVE_RUN_DATA_CONTRACT.md)均保留MIT/上游归属；未发布游戏DLL或反编译源码，未改角色/安装/真实存档，未启动UI/真实Steam/网络，未提交推送。下一步按[4D交接](NATIVE_RUN_N3D_IMPLEMENTATION_HANDOFF.md)在本轮独立审查后继续，无需用户追加逐步授权。
