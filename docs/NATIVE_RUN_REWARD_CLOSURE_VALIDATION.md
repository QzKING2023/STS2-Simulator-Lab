# 原生奖励语义闭环验证

当前：**实现及同最终构建统一回归通过，待独立验收。完整 M1 尚未完成。**

本轮承接 [实施交接](NATIVE_RUN_N3D_REWARD_CLOSURE_HANDOFF.md)，实现 [内部契约](NATIVE_RUN_REWARD_CLOSURE_CONTRACT.md) 的五项闭环。固定游戏 v0.111.0 /41cef1ea，Ironclad/A0，全部业务由本机原生程序集执行，Comparison 为 NotCompared。

## 实现与实际路径

| 路径 | 已执行事实 | 覆盖来源 |
| --- | --- | --- |
| Driftwood reroll→取卡 | 真正取得遗物、原生奖励修改、同一次 OnSelect 的两个菜单、原生 Reroll/Populate、第二菜单非首项取卡 | API 夹具 |
| reroll→skip | 原生重选后选择 Skip；OnSelect 未完成取卡，随后原生跳过奖励集合；没有取得卡 | API 夹具 |
| FresnelLens 候选修改 | 原生 Populate 修改 Defend 候选，来源为真实遗物；领取同一已附魔实例 | API 夹具，明确候选源 |
| FresnelLens 入牌组替换 | 原生 CardPileCmd.Add 将未附魔的新 Defend 替换为新实例，Nimble 2，实际永久牌组及 modifyingModels 对应 | 直接 Add API 夹具，未冒称该卡来自奖励界面 |
| Sozu 空槽/满槽 | 原生 TryToProcure 返回 NotAllowed；奖励处理完成，ClaimedPotion 指向原药水，但 acquired=null，实际槽位未新增 | API 夹具；与旧 TooFull→丢弃→重试回归分列 |
| Neow Kaleidoscope | DEV_02 正常开局，同一原生奖励会话两次取卡，进入下一合法战斗的 Play 状态，验证永久牌与战斗副本的来源 | natural-run，3 个根动作；没有声称在该路径实际打出新卡 |
| SpoilsMap 宝箱账本 | 原生创建/入组任务卡、原生第二幕地图生成目标坐标，宝箱原生基础金币 44、任务金币 600、任务完成与移卡、遗物领取和退出 | API 夹具：直接 EnterAct(1) 并进入原生生成的任务坐标，非正常跨幕路线 |

SpoilsMap 实际是任务卡，不是遗物奖励 Hook。`OneOffSynchronizer` 的原生宝箱流程先给基础金币，再调用任务完成；本轮分别记录、交叉检查实际金额、牌组移除、completed_quests/cards_removed 历史，最终返回 644，未修改任务字段、坐标或金币来制造结果。

## 协议与完整性

新 profile 为 `reward-closure/v2`。外层仍使用原生 RewardsSetSynchronizer 和原 4C 操作树；新增事件 `rc-menu`、`rc-reroll-*`、`rc-add-*`、`rc-procure-*`、`rc-quest-*` 都要求非 null 对象状态。菜单关联实际 reward、代次、native choiceId、当前 root/context 和完整候选来源；每个请求、开始、结果及父子操作必须完整且唯一。

原生测试 selector 在 reroll 后仍传入旧 alternative 列表。provider 依据实际 CanReroll 过滤已禁用的 REROLL，再用原对象引用返回选择；没有替换重选或生成业务。缺失、过期、多余、重复消费与禁用重选请求明确拒绝。

实际卡牌取得通过原生 Add Task 的参数及返回记录；候选生成修改与入牌组修改分开。永久牌组投影含原生序列化内容，核对模型、升级、附魔、新旧引用及真实修改来源。CardsGained 写入早于 Add 替换，CardChoices 使用实际取得对象，按各阶段验证，未手工修正原生历史。

Sozu 的 rewardComplete、procureSuccess、failureReason、claimedPotion 与 actual acquired 独立记录。原生拒收完成奖励不能记为实际取得。菜单、业务返回、外层结果、确认与静止观察点通过完整快照绑定，真实业务变化按限定契约允许；RNG 重放比较保留完整状态，不删除字段求一致。

旧 4C profile 的单菜单及来源验证继续启用。新增直接 Add 子操作仅在 closure profile 内允许，没有放宽旧 profile 的操作树。

## 最终冻结与新矩阵

- Build ID：`FE913EB8BEFAB02D2FAF45BA7FC658A19EBCABEB946E8202EDF7BBD2F769FAFF`。
- Native DLL SHA256：`480D7107DEA07282E997A1CA6B4313F390529B5E0B368CB170DB30CD729E3BF0`。
- Manifest：`.build/native-build/reward-closure-final04/build-manifest.json`，0 warning /0 error；68 个实际源输入、44 个编译输入、3 个游戏输入、7 个产物。
- dirty 源码快照：`.build/runs/reward-closure-work01/source-snapshot-final04.json`。
- 新矩阵：`.build/runs/reward-closure-final04-closure/summary.json`，**35 次原生进程 /380 项检查 /132 个生产检查器控制，失败 0**。

10 个正例各 live 执行并冻结重放一次；4 类场景另做 minimal/off 观察对照，共 28 个正常进程。其余 7 个是明确故障控制，均保留 3/4 确认/派发前缀：非法、过期、缺失、额外字段、重复与禁用第二菜单为 InvalidAction；真实 Add 及其观察完成后的控制故障为 EngineError。它们没有计作正常完成，也没有宣称零根派发。

132 个控制均对新完整证据副本重新计算有效 seal 后调用实际生产 reader，含 122 个损坏拒绝、10 个等价通过。覆盖九类新事件的缺失/重复/错误归属/快照缺失与错误类型、整个菜单子树遗漏、代次/登记身份/来源/新旧实例、共同篡改、历史、外层 receipt、实际取得内容、禁用重选候选及合法属性顺序/静态扩展。见 `controls.json`；原包保持。

## 同构建统一回归与来源

`.build/runs/reward-closure-work01/provenance.json` 的 **530 项核对全部通过**，重新计算 Build ID 一致，68 份源码和保存副本、44 份编译输入、3 个游戏输入和7个产物匹配。

同一最终构建共 **280 个原生进程 /3304 项套件检查，另 Core98 通过**。分组（进程/检查）：奖励闭环35/380、原交易18/471、商店经济23/361、旧商店25/371、4C55/561、4B34/307、4A16/273、N2 74/580。原始退出为206×0、67×1、4×2、3×3；预期失败、Unsupported、预算停止仍逐项保留，不能以外层 exit0 宣称全为正常完成。

830 个历史包已在新副本上实际调用最终生产 reader 重读，均符合预期，原始3236个被读取文件保持。四个新增方法的实际 Harmony 审计 API 探针通过，共8个未知补丁拒绝，目标业务调用0；不将审计 API 当业务覆盖。旧自然 skip/take、24/25动作卡奖励、71/138/Unsupported片段、原生真实Death、跨幕/Architect夹具以及旧自然商店72/41/83均保留同构建回归。15个锁定Mod输入、52个兼容源码、2622+993历史锁定文件保持，未重新扩展Mod功能。

新增35个进程与其132个控制见 `.build/runs/reward-closure-final04-closure/`，其余七组为 `.build/runs/reward-closure-final04-*`。统一入口 `run-final04.py`、来源检查 `inspect-final04.py`、历史重读 `recheck-history-final04.py`、实际补丁探针、失败修复与最终汇总保存在 `.build/runs/reward-closure-work01/`；原生进程、生产reader、审计API和静态来源核对分别计数。

## 开发失败与修复证据

保留 `.build/runs/reward-closure-work01/` 中的各 dev 构建和结果，没有删除失败以替换样本。

- 早期编译缺少实际命名空间/属性名；按固定 SDK 修正。
- dev03：原生场景执行结束，但 reader 正确指出 Add 会原生更新 FloorAddedToDeck，以及探针重复结束；修正观察阶段与完成边界。
- dev06：直接 API 的检查器栈、Neow 取得集合关联未同步；修正后实际业务和 reader 均通过。
- final02：额外自查发现 CanReroll 及菜单来源清单可被篡改/删除；132 控制的前版还发现任务返回可以错误归属。集中绑定完整来源、结果、领取声明与动作，原反例在新 schema 上重建后拒绝。
- 第一次统一启动给 Python 加 `-P` 导致相邻控制模块未导入，没有执行奖励进程。改用显式模块路径加载后，标准 `python -P` 入口可重跑；原启动失败日志保留。
- 连接曾短断，先核实源文件写入和已完成结果，再继续，没有重复不确定的动作。

## 范围和剩余限制

所有新增原生业务使用 MemorySaveStore、shouldSave=false、无游戏 UI、无真实 Steam/网络；原版主路径无 Mod/CombatSolver。没有角色、安装、真实存档修改或提交推送。

这是固定版本、固定场景与种子的有限 profile。没有声称所有奖励监听器、任意多取卡 Hook、所有 alternative 效果、全角色或所有种子已支持。多取卡等未闭合的原生事务明确停止，不能将其当空效果。固定 SDK 中未发现原版 ShouldAddToDeck 覆盖，故真实卡牌拒收用例为 **not run**，没有用合成返回值冒充。

普通药水取得、TooFull、丢弃重试仍由旧原生回归验证；新 profile 的定向业务为 Sozu NotAllowed，不把它扩大成全部采购 Hook。宝箱任务路径包含明示跳转夹具，没有自然跨幕、整幕或正常开局通关结论。

接续事项见[下一交接](NATIVE_RUN_REWARD_CLOSURE_NEXT_HANDOFF.md)：事件战斗恢复、Elite/Boss、通用 run 会话集成、自然跨幕和预登记批测仍待推进，完整 M1 不在本轮关闭。

## 复现

使用新鲜输出目录，最终构建对应源文件必须保持哈希一致：

```powershell
python -P tools/native-run/run-native-reward-closure.py --manifest .build/native-build/reward-closure-final04/build-manifest.json --output .build/runs/reward-closure-reproduce-01
```

完整统一回归、来源检查与历史重读辅助入口位于 `.build/runs/reward-closure-work01/`；原始日志和结果供本机核验，不作为公开源码发布。
