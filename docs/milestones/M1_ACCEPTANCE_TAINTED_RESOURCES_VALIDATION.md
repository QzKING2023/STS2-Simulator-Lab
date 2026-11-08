# M1 第四批验收：Tainted、选择与原生资源

日期：2026-11-04。**M1 未通过，不提交或推送。** 本批修复真实差分和接入缺口，并保持完整胜利路径；没有用已有单路径胜利替代六个固定样本。

## 冻结与预算

- 最终宿主：`.build/native-build/m1-acceptance-b04-final06/`。
- 冻结 ID：`B64C6BF07A97EB40F286D2834E46CC0518AE6230F74952A9579E0D86A81D219C`。
- 固定游戏：v0.111.0 / 41cef1ea；本地 HEAD 仍为 `d73039d688eb54c9513f5b9508a03947e689aa18`，dirty 源码由构建清单独立标识。
- 新 solver 快照：`.build/pinned-inputs/combatsolver-0.50.1-tainted-root01/`，DLL SHA256 `F5B61652AB4809C8C6AC30EB376916643F1014CF93E5988F28576DC3E0DC04BC`。
- `tainted-lifecycle.1`、`illusion-admission.1`、`tainted-root.1` 是 MIT 上游 0.50.1 的独立 Lab 派生快照；父快照、许可证和来源不变。未冒称上游新版本。
- 原始证据：`.build/runs/m1-acceptance-work04/closeout.json`、`execution-ledger.json`、`final-binding.json`。
- 预登记上限：18 次、75 分钟、6 GiB。使用 18/18 次预算，其中 17 个原生进程，1 次包装器在派发前因输入路径错误退出，仍保守计入次数；最后原生结束时累计 57.66 分钟。收口测得约 3.87 GiB，所有执行已结束。
- 1,178 项源码／编译副本／输入／产物哈希检查无差异；15 个锁定输入和 52 个兼容源码保持不变。宿主构建 0 error、5 warning，未称零警告。

## 修复与真实覆盖

### Tainted 生命周期

先用旧构建复现 EVAL04 的真实 DataError：query327 / action383，击杀 InfestedPrism 后，模拟器清掉 Tainted，原生清理前仍保留。

原生死亡先把敌人脱离 CombatState，再移除能力；`VitalSparkPower.AfterRemoved` 在 owner 已脱离时不清卡牌附着。旧正常化逻辑把“有效能力消失”当成该回调已清卡，语义不符。

派生实现复用上游分支 attachment 与 power 状态，记录不可变 owner/amount 快照，区分仍附着时的实际移除与死亡脱离。根捕获时初始化这份快照，fork 共享旧不可变值、正常化时整体替换。没有调用全局真实状态冒充分支，也没有改角色数值。

`tainted-fixture-final03/` 是明确 API 夹具：原生非致死出牌、原生命令移除仍附着的能力、原生致死出牌；两次出牌差分和一次能力移除差分一致，父 fork 不变，致死清理前保留 Tainted。人工设定 HP 的夹具不称自然胜利。最终自然 EVAL04 已越过原停点，至 622 确认动作。

### 药水选择与复活

- 复用 `PotionChoiceSupport.GetSpec`、既有 token 匹配及 `PotionExecutionSupport.Prepare/Complete`，接入上游声明的原生手牌／抽牌堆／弃牌堆选择表面；未另写药水效果。
- EVAL06 final03 的 query282 / action340：求解器主动使用 Liquid Memories，从真实弃牌堆选择 Unrelenting；原生派发、完整 continuation/RNG 和父状态检查一致。其他新增药水表面不因这个样本而宣称全部通过。
- Illusion 复用已有 BeginIllusionRevive／ResolveReviveMove，仅补精确的 Heal 意图准入：真实 IllusionPower 处于复活状态、owner 相同、REVIVE_MOVE、一次性移动与后续状态均满足。未按敌人名字放行未知 Heal。
- EVAL05 final03 实际 EndTurn 的 EyeWithTeeth HP 0→6（query93），以及 Parafright 的 0→15、0→12（query225/235），均有原生／分支一致证据。见 `revival-observed.json`；不能推及所有复活机制。

### 事件真实资源与原生入口

- ThisOrThat Plain 改为观察原始 Damage／GainGold 的输入、真实修正、setter 写入、异步完成与净资源。未重复调用修正 Hook，也未用显示数值代替实际效果。
- EVAL01 final03 action634：显示伤害输入 6，实际 HP 48→43；金币输入和净增均为 47。原生结果与同批新账本一致，且和上一批无账本的原生前后完整状态相同（**跨构建对照**，不是同构建模式实验）。随后继续至 733 确认动作。
- SelfHelpBook 和 TheFutureOfPotions 在核实继承入口及未知补丁审计后复用原生选项、通用选择和奖励会话。分别实际完成指定技能 Nimble 附魔，以及药水移除、升级卡奖励和事件结束。
- RandomForeseer 已有 TheFutureOfPotions 预测，但当前锁定子集未接入；没有另造预测，也不称已比对。SelfHelpBook 在所查事件预测目录没有对应预测。

### 附魔能力准入

SelfHelpBook 暴露 Lab 的准入过严：原版 Nimble 继承空 OnPlay，格挡效果在 `EnchantBlockAdditive`；上游 `MethodMirrorRegistry` 对这种 OnPlay 使用 `NotOverridden`，不需要显式处理器。

Lab 现在按原生程序集、声明类型、module/token 和无未知 Harmony patch 检查继承方法，同时保留显式镜像分支。记录区分 `registered:false / onPlaySupport:InheritedBase` 与显式 `Handled`，不伪造注册。其他效果仍须根审计和执行前影子差分。

final06 EVAL04 query516 / action606 实际打出 Nimble Defend，格挡 0→7，完整差分和父状态检查通过。未知补丁控制在 .NET 9 独立进程实际注入／卸载，未执行原生游戏命令；10 项能力和映射控制通过。PowerShell CLR10 的 Harmony 不兼容尝试及 final05 的继承 MethodInfo 比较错误均保留，没有算通过。

## 六个固定样本

沿用原种子、策略、奖励选择与搜索配置，没有替换失败样本。表中不同构建是分层复验的事实，**不是最终 M1 同构建矩阵**。

| EVAL | 生成构建 | 确认／派发 | 原生差分步 | 原始退出与真实停点 |
|---|---|---:|---:|---|
| 01 | final03 | 733 / 733 | 630 | exit2；第三幕，当回合死亡预测保护 |
| 02 | final04 | 381 / 381 | 319 | exit3；第二幕，Coordinator 总展开量返回预算 |
| 03 | final04 | 440 / 440 | 368 | exit2；第二幕，当回合死亡预测保护 |
| 04 | final06 | 622 / 623 | 531 | exit2；第三幕，Reflections 入口未审计，保留未确认 Travel |
| 05 | final04 | 466 / 466 | 365 | exit2；第三幕，当回合死亡预测保护 |
| 06 | final03 | 389 / 389 | 321 | exit2；第二幕，当回合死亡预测保护 |

以上最新路径没有已记录的失败差分；但策略保护不是实际 Death，预算不是完成，未确认 Travel 不是成功推进。原始 Tainted DataError 及修复过程的失败全部保留。

final06 生产检查器重读五份非预算路径退出 0，只证明有限证据契约。EVAL02 重读仍因真实预算门退出 1，未将其包装成通过或准确性故障；原生 exit3 单独保留。见 `final-readers.json`。

## 回归与证据含义

- 最终同构建 `dev-detailed`、`dev-minimal` 均为真实 RunVictory、499 确认／派发动作、exit0。
- 393 步完整差分、父状态、动作、选择和最终状态一致；499 个确认完整状态一致；4,082 个共用完整状态观察（含完整 RNG）一致。详细模式共 7,642 个状态观察，额外战斗 history 明确分列，不删状态字段来制造一致。
- 这是固定输入的原生重放，入口仍执行求解查询；不能当纯动作播放器，也不能用它与在线耗时相减推算搜索耗时。
- `final-comparison.json` 的 19 项检查全部通过；Core 98 项通过，测试引用的 Core DLL 与最终宿主相同。
- 最终生产方法有限正负控共 80 项：历史 46、事件／药水 20、条件预览 4、附魔 10。条件预览控制反射提供已捕获 proof，仅检查准入谓词，不冒充运行时来源审计。
- `final03→final06` 的宿主源码差异只有事件证据检查器、两事件入口和附魔准入；`final04→final06` 只有附魔准入。被保留的早期原生结果用其实际生成器标识。
- 原有 MemorySaveStore、保存／重放拦截、Steam 与 UI 隔离继续通过。没有修改角色项目、游戏安装或真实存档，没有 git 写操作。

`telemetry-summary.json` 仅整理本就必须运行的日志：耗时、成员展开量、动作、结果及进程资源。缺失字段列未知，没有增设性能实验、改线程或策略预算。

## 保留的失败与未完成项

1. 最初 Tainted fixture 被场景分派误送入普通战斗；其 exit0 已撤销为该夹具的证据，不删除目录。final03 才是真正专用夹具。
2. 一次重放路径转义错误在包装器派发前失败；两次 final01 运行暴露条件后缀编码损坏；专用 fixture final02 暴露首次正常化未初始化根能力基线。均有具体修复与后续实证。
3. CrystalSphere 的 CellClicked、完成任务和奖励仍未实际执行；不能凭原生代码审查或适配编译成功认证。
4. Reflections 原生入口覆盖、固定样本的真实终局及全套最终同构建门仍待完成；预算／策略停止保持独立分类。
5. 完整原版节点回归、单列构建的冻结 Mod 回归和数据查询验收仍在 M1 收口清单，未被两次胜利替代。

后续见 [交接](M1_ACCEPTANCE_TAINTED_RESOURCES_NEXT_HANDOFF.md)。本批次数已经用完；下一批先登记新预算，保留本批和此前 17/18 的全部证据。M1 整体验收通过后才执行已授权的整体提交推送。
