# M1 步骤 4C：原生奖励、宝箱／遗物与药水

日期：2026-11-02。前置[4B最终独立验收](NATIVE_RUN_N3B_FINAL_REVIEW.md)已通过；全六步已授权，现在可接续实施。本交接未执行4C，4D商店／嵌套事件／精英Boss、5自然跨幕完整run、6批测交付继续待办。

基线Build ID：`C632744968AA67F2BF4E472681E2B0F5B285C49F748B9F33A3420AF3D78AD438`，manifest为`.build/native-build/n3b-exit-state-final02/build-manifest.json`；AF641为修复前历史基线。继续原版v0.111.0、Ironclad/A0、无UI、MemorySaveStore、一run一进程、native-only／NotCompared。建议Sol xhigh / Standard实施、Astra xhigh / Standard验收，不扩公开Mod API。

## 依赖顺序与关键设计

1. **先将4B新的流程接入原生奖励集合会话。** 当前Program.Rewards仍是旧N2固定Gold/Card脚本，保留其24／25动作入口回归。新会话记录原生生成、全部合法候选、领取／跳过条件、取得结果和原生完成；空集合、零金币合法，不沿用单offer且正金币的假设。不能自动skip未知必选奖励或用TestMode首项替代策略。
2. **根／子事务先闭合，再扩类型。** OfferForRoomEnd或OfferCustom可能由战斗控制器或Rest／事件效果调用。建议顶层以一个原生Offer任务根动作拥有整个集合，内部决策作为子请求；嵌套Offer继续归属原根。不要在未确认根动作内调用Program.Action开启平级根，也不要将仍等待策略的原生任务提前确认。沿用已有request/context/stamp/实例机制，非卡对象不能伪装CardModel。
3. **Gold／Card → Potion／Relic → Treasure → Potion使用。** 每类先明确局部夹具，再做自然片段；不为效果写simulator mirror。卡奖励保留4B新增的原候选和真实永久取得检查，继续核验多次选择、跳过／替代奖励、可能的原生替换结果身份；不得因未支持Hook而假报完成。
4. **完成与观察分开。** 继续room-leave-ready／原生Exit／room-complete，静止边界绑定完整确认状态；保留room-exit-start与当前不可变dispatch的完整结构绑定。原生任务中间的合法变化按实际规则记录，不能将Closed例外泛化到资源／RNG。新场景有自己的有限生产契约，保留已验4B及旧4A、N2回归profile的要求。

## 已知原生入口与UI中承载的语义

- RewardsCmd、RewardsSet及RewardsSetSynchronizer生成／领取恰好一次。所有真实奖励、alternative与可跳过状态公开给内部策略；未知合法分支保留并标Unsupported。额外奖励沿原生顺序，尤其Rest Heal后的Hook／OfferCustom。
- TreasureRoom.EnterInternal已经BeginRelicPicking，不能重复生成。NTreasureRoom.OpenChest先DoNormalRewards，再DoExtraRewardsIfNeeded，之后遗物选择。RelicsAwarded只是分配通知；原UI订阅者会从canonical建立mutable并调用RelicCmd.Obtain，等待该真实任务和Hook才能认为取得。保持单人skip及OnRoomExited的实际结束语义，不能手工Add relic。
- PotionModel.EnqueueManualUse→UsePotionAction→OnUseWrapper执行移除、效果、Hook和history。入队不代替完整时点／目标校验；自动触发药水不可冒充主动动作。先核实Player.Potions是否为紧凑集合或槽结构，明确capacity、slot和实例映射，不能从当前数组顺序推定槽永久不变。
- 新外部展示依赖只可在精确固定版本的纯视觉／外部服务边界处理；保留原生命令、扣款、取得和效果。需要额外权限或弱化审计时停该依赖，给具体接口提案，继续独立安全部分。

原生只读资料在`.build/review/n3-handoff/`的TreasureRoom、TreasureRoomRelicSynchronizer、NTreasureRoom／Collection、OneOffSynchronizer、PotionModel、UsePotionAction，以及当前CardReward静态资料`.build/runs/n3b-work01/static/Rewards.CardReward.cs`。实现前按固定DLL核对方法签名／token／MVID，不发布反编译代码。

## 有界验收矩阵

| 工作组 | 最少真实正场景 | 拒绝／失败证据 |
| --- | --- | --- |
| 奖励会话 | Gold、Card take／skip、空集合、两次连续集合、一个嵌套Offer | disallowSkip拒绝、缺失／多余／过期／跨集合答复、重复领取、取得任务故障保留前缀 |
| Card取得 | 指定非0实例实际进入永久deck，后续战斗副本关联；现有Neow连续两次保留 | 仅选择未取得不得完成；原候选被原生替换时明确身份映射或Unsupported，不猜成功 |
| Relic | 原生取得mutable实例，后续战斗有至少一个真实Hook效果 | 仅RelicsAwarded不能算取得；重复／错对象／缺Hook完成拒绝 |
| Treasure | 原生普通奖励→额外奖励→分配→取得，合法take与skip | 不重复Begin／开箱；未完成额外奖励或原生取得前不得离开 |
| Potion获得／容量 | 原生获得到槽；满容量时真实合法丢弃再领取 | 满槽、旧槽被替换、重复答复、旧实例拒绝，不直接覆盖库存 |
| Potion使用 | 指定敌方目标与自用各一例，原生消费并记录实际效果 | 死目标／错误目标／错误时点／旧实例预派发拒绝；效果后异常保留已消费前缀 |
| 数据与生命周期 | 来源关联、完整RNG、前后库存／HP／金币／deck／relic／potion、任务顺序 | 生产reader拒绝缺取得、重复发放、错来源、提前完成、静止快照漂移；独立数值／结构断言防双方同错 |

局部API夹具允许明确原生setup，但不能改自然流程资源来保证成功。每个正场景新进程重放；每组至少一条minimal/detailed对照，比较完整状态和已盘点RNG。给观察日志与原生history分别标来源，记录其必要语义，不把TestMode省略的原生历史伪装成存在。

## 自然集成与收口

保留4B三种子的原结果，扩展路线／策略时登记新profile和目标，不把DEV_02死亡或DEV_03 Unsupported替换为成功。首个自然目标为同一正常run取得一个本轮新类型奖励，继续至后续战斗并实际使用药水或观察遗物Hook；不要求把所有类型强塞同一轨迹，不要求胜利，不伪造首手或资源。若自然可达性未完成则明确该门未过，夹具不能代替。

预算沿用4B有界默认，新增集合／嵌套选择上限并写入header。未知结果、原始exit不符、内部异常或缺关键数据均失败，Death／BudgetExceeded／Unsupported分别记录。未知节点继续原生抽取，不能跳过商店／事件来伪称已具备整幕能力。

最终同构建跑4C新矩阵、4B自然／夹具、4A全部状态绑定与历史负控、N2完整矩阵及Core；15个Mod输入和52个兼容源码保持。原legacy入口与新profile结果分列，不宣称根动作数永远不变。交付新冻结、实际源／输出hash、来源与未覆盖清单、原始退出、复现入口和4D交接，再独立验收。保留MIT／归属、所有dirty工作，不提交推送，不改角色、安装或用户存档，不启动真实UI／Steam／游戏网络。
