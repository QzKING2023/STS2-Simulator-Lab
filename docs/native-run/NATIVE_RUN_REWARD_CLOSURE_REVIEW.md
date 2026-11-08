# 奖励语义闭环独立验收：三项必修

结论：**本轮尚未通过独立验收。** 原生正常路径和冻结来源成立；生产数据检查器仍有三组实质误接受，需先修复再接事件战斗。本次只读实现及原证据，新增审查副本、检查脚本和本报告，没有修改运行实现或原结果。

核对冻结 `FE913EB8BEFAB02D2FAF45BA7FC658A19EBCABEB946E8202EDF7BBD2F769FAFF`，Native DLL SHA256 `480D7107DEA07282E997A1CA6B4313F390529B5E0B368CB170DB30CD729E3BF0`。独立重算 Build ID 一致。审查证据目录：`.build/runs/reward-closure-independent-review01/`；结论与清单见 `closeout.json`、`summary.json`。

## 已核实的事实与计数

- 复核同构建的 280 个原生进程、3304 项套件检查与 Core98。原始退出分布仍为 206×0、67×1、4×2、3×3；未将预期故障、Unsupported 或预算停止改称正常完成。新矩阵 35 进程/380 检查成立。此次没有重跑这 280 次游戏业务或 Core98。
- 实际重读当前源码、编译输入、产物、输入锁、日志和各进程元数据，原 530 项来源断言均通过。68 源输入、44 编译输入、3 游戏输入、7 产物匹配；15 锁定输入、52 兼容源码及历史锁保持。830 次历史 reader 调用的原日志、退出与原包锁已复核，本轮没有再执行这 830 次。
- 本次实际调用最终生产 `--review` 共 **163 次**：原 132 控制的新副本全部符合预期（122 拒绝、10 等价通过）；另 31 次独立检查包含 10 正常副本通过、20 个实质损坏副本（7 拒绝、13 误接受）和 1 个保留的初步反例。初步反例意外同时改变身份登记而被拒绝，随后保留登记重建，见 `identity-followup-0`，没有用初步拒绝掩盖实际缺口。
- 新副本全部使用有效重新计算的 seal，误接受均为生产 reader 原始退出 0。不是 seal 破坏、未知 schema 或脚本替身的结果。原始包及六份原文档保持，运行源码未变。
- 新 10 正例的完整 route/choices/reward-choices/final-state 重放一致；四类 minimal/off 对照仍比较完整 JSON，包括完整 RNG。七个故障控制保留 3/4 确认/派发前缀、真正完成的 reroll/Add 副作用和未完成根动作。

## R1：Sozu 成功声明未绑定实际库存与拒收条件

位置：`tools/native-run/NativeRewardClosureContract.cs:82`、`:87`、`:93`、`:109`；旧检查器在 `NativeRewardContract.cs:175` 将 closure 药水移交新契约。

反例 `initial-0`、`initial-1` 分别来自空槽和满槽 Sozu 正常原包。仅将 `rc-procure-return` 改为 `success=true/failure=None/actual=本次药水`，同步外层 `acquired`；所有状态、槽位及原生历史完全不变。两个副本均被接受。满槽仍只有原三个 BlockPotion，空槽仍为空，却能声明成功取得 FirePotion。

根因：成功分支只比较 actual 与 offered，并豁免 inventory/potions/mapHistory；没有要求实际新增槽位/对象/历史。终末 Sozu 断言只要求调用次数及持有 Sozu，没有约束真实拒收语义。外层结果与伪造的子返回一致，不能作为独立取得证据。

最小修复：当前 Sozu 两场景应由持有者/适用条件独立约束 `NotAllowed + procureSuccess=false + actual=null`，奖励可完成、claimed 保留。任何允许的成功分支必须绑定实际前后库存、具体新实例和原生历史；尚未声明支持的分支明确拒绝。保留旧正常采购、TooFull、丢弃重试契约，不能把所有药水一律判拒收。

## R2：子操作内部一致，但与父操作的静止边界断开

位置：`NativeRewardContract.cs:53`、`:88`、`:156`；`NativeRewardClosureContract.cs:39`、`:40`。首次 card-menu/direct card-add 自己保存的 Open 没有绑定父级实际状态；RewardOffer 及本轮直接 Add 的 RewardSetup 返回也没有绑定最后子返回。

反例 `boundaries-0`～`boundaries-7`：

- 直接 Fresnel Add 的整个 card-add 子区间共同加 7 金币，父 RewardSetup 返回恢复原值，仍接受；在同一区间新增嵌套状态字段也接受。
- reroll-take、直接取卡、候选 Fresnel 三条路径，从首菜单至 reward-set 返回共同加 7 金币，外层 RewardOffer 返回恢复原值，均接受；同样三个区间仅增加嵌套状态字段也接受。

这些没有修改实际 Add、reroll 或历史的业务差值，恰好说明仅检查子区间自洽不够。`static-boundaries.json` 核对当前正常包的 **14 个**相应入口/返回静止点，全结构确实相等。

最小修复：按本轮实际调用链，把首次菜单入口、直接 Add 入口及明确无业务的父返回，绑定不可变的父派发/最新确认子状态，做完整结构比较。不要将整个 EventOption、Populate 或全部 RewardSetup 盲目当静止区间；真实候选生成 RNG、Add 的牌组/历史变化、Neow 事件完成等仍按业务契约保留。增加同类共同篡改、未知字段、合法完整等价和真实业务变化控制。

## R3：实际替换对象的鲜活身份、类型和升级投影未完整绑定

位置：`NativeRewardClosureContract.cs:19`、`:32`、`:34`、`:55`、`:60`。登记表仅保留 model；Add 检查新列表末项却不要求 actual 不在旧牌组，也未将末项 upgrade 与 actual/native 升级交叉验证。

三个实质反例均来自 `closure-lens-add-1`：

| 副本 | 实际破坏 | 生产退出 |
| --- | --- | --- |
| `identity-followup-0` | 保留全部身份声明，把新 actual 的引用及末项牌组 id 改为已持有的同模型 Defend；旧牌不动，永久牌组出现同 id 的两个不同内容 | 0 |
| `identities-1` | Add 返回后实际新牌的所有永久牌组投影 upgrade 改为 9；actual 仍 upgrade0，native 序列化仍未升级 | 0 |
| `identities-2` | 实际新 Defend 的登记 type 改为 FirePotion，model 和后续引用不动 | 0 |

最小修复：按明确 fresh-add/replacement 契约保证 actual 为本次真实新实例，旧 selected 未误入组，永久牌组引用唯一；保留原有对象完整前缀。登记类型、模型、实际返回、永久牌组及 native 升级/附魔表示应相互一致。使用字段语义处理 native 默认未升级的省略值，不能只比较牌数或只检查 model。类型/内容和引用负控要保留同模型相似实例及合法克隆的正常证据。

## 原生调用链与范围判断

独立读取固定游戏程序集的对应类型和本轮包装代码。Reroll 真实清 CanReroll、记录旧候选未选中并 Populate；provider 过滤原生测试 selector 保留的已禁用 REROLL，再以原 alternative 引用返回。当前两菜单业务和 RNG 实证成立，未手动替换奖励结果。

FresnelLens 的候选修改和 Add 替换确实是两条原生路径：候选已 Nimble2，领取同一候选且 Add 无修改者；直接 Add 夹具选中未附魔 `i18`，原生返回新 `i19`、Nimble2、真实 FresnelLens 修改者。它们分别记录。R3 是检查器无法可靠拒绝矛盾数据，不是已有正常结果中的替换未发生。

Sozu 原生 `ShouldProcurePotion` 对所属玩家返回 false，`TryToProcure` 在检查容量前返回 NotAllowed；PotionReward 随后可返回 true 并设置 ClaimedPotion。已有两个正常包正确保存这个事实，R1 必须阻止对它的错误改写。

Neow DEV_02 同一次正常开局的三根动作，实际统一会话连续取得 Tactician/ParticleWall，再合法 Travel 到下一战 Play，永久牌与战斗副本关联存在。这里未证明新卡实际打出。SpoilsMap 是明示 API 夹具：原生 EnterAct(1) 生成任务位置，再直接进入该坐标；44 基础金币 +600 任务金币、任务完成/移卡和遗物取得链成立。不得称作自然跨幕。

### 卡牌拒收的准确结论

对固定 SHA 的程序集做独立 PE 元数据扫描，`ShouldAddToDeck` 只出现于 AbstractModel 与 Hook 调度器；AbstractModel 的方法体 IL 为 `17 2A`，即返回 true，没有原版覆盖。证据 `card-rejection-independent.json`。所以 **由该 Hook 引起的健康正常奖励拒收在当前原版范围不适用，not run 不是本轮阻断，也不要求注入假返回补齐**。

不能扩大为“CardPileCmd.Add 永不失败”：原生另有已移除卡、死亡持有者等前置失败分支。它们属于非法/终态调用边界，不是本轮未发现的原版奖励拒收遗物；后续控制器仍须拒绝这类调用并保留真实失败。

## 修复后接续

先集中修 R1～R3，保留本次 13 个误接受副本与原正常包。按相同明确契约补齐生产校验与正负控，再冻结实际源码/产物，统一复跑适用矩阵和独立反例。无需扩 Mod 或改角色，也无需用户重新批准。

通过复审后沿 [下一交接](NATIVE_RUN_REWARD_CLOSURE_NEXT_HANDOFF.md) 继续 4D2：原 DEV_03/WhisperingHollow 的事件→战斗→奖励→原事件恢复与合法离开，保留原 Unsupported 前缀作为基线；随后 4D3 Elite/Boss/act-ready，再步骤5正常同 Run 跨幕与真实终局、步骤6预登记批测。当前不将尾部/任务夹具当完整 M1。

审查没有调用真实游戏 UI、Steam、网络或用户存档，没有改 Mod/安装，没有提交推送。一次执行服务断连后先确认命令未创建目标副本，再继续；现无操作权限或环境阻塞，待处理的是上述三项验收缺陷。
