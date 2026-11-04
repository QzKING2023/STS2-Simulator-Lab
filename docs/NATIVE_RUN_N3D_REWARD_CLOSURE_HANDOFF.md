# 4D 下一有界轮次：奖励语义闭环

前置：[4D1C 最终独立验收](NATIVE_RUN_N3D1C_FINAL_REVIEW.md) 已通过，基线 `1348A7FAD6C9BDA35E91EC469911FD57CC1800BB8B76CD3B19B30F9872D3163B`。本交接细化 [4D1C 后续](NATIVE_RUN_N3D1C_NEXT_HANDOFF.md)，不改变 [M1 总方案](MILESTONE_1_NATIVE_RUN_PLAN.md)。用户持续完成 M1 的授权有效，按以下依赖接续，无需逐步请批。

## 本轮目标与边界

打通原生奖励的多菜单决策、实际取得/替换/拒收、Neow 同协议路径和宝箱修改账本。继续使用原生 RewardsSetSynchronizer、CardReward.OnSelect、CardPileCmd.Add 和 PotionCmd.TryToProcure 执行业务；决策 provider 提交合法选择，观察器保存实际结果。不开公开 SDK，不补 Mod 镜像，不把所有商店监听器一起纳入本轮。

允许拆为两个可验收子回合：先 R-A～R-C 的共享结果协议与定向边界，再 R-D～R-E 的正常开局接入和宝箱账本。拆分不取消后者，也不能仅凭夹具通过宣布整段完成。

## 已核实的具体缺口

| 入口 | 当前事实 | 实施要求 |
| --- | --- | --- |
| `NativeRewards.cs:125` CardChoice | pendingCards 按 reward 只容纳一个未结束菜单；原生 reroll 会在同一 OnSelect 内再次请求选择 | 同 reward 建立有序菜单代次；每个请求有独立身份、候选与结束原因 |
| `NativeRewards.cs:134` CardAfter | 当前必须取得所选的同一实例；末尾才结束单个菜单 | 将菜单结束与领取结束分开；从实际原生 Add 返回值记录成功、实际实例、修改来源 |
| `NativeRewardContract.cs:162`、`:182` | 强制一个 card-menu、same-instance；不允许继续选择型 alternative | 扩展精确父子状态机，保留旧单菜单契约作为可验证子集 |
| `NativeRewards.cs:112` Collect | PotionReward 返回 true 但原药水不在槽位即 Unsupported | 分开 rewardComplete、procureSuccess、failureReason、claimed 与 actualAdded |
| `NativeChoices.cs:119`、`:146` | 只有 Active 且 InSession 走新桥，其余 CardReward 走旧 pendingRewards | Neow 迁移须同时接通会话作用域与原生异步 Task，不能只改 selector 分支 |

只读原生参考保留在本机 `.build/`，不发布反编译源码：

- `.build/runs/n3b-work01/static/Rewards.CardReward.cs`：OnSelect 原生循环、Add 返回、最终 CardChoices、Reroll；Reroll 会记录旧候选未选中、关闭 CanReroll、清空并原生 Populate。
- `.build/runs/n3c-independent-review01/static/Driftwood.cs`：真实奖励修改 Hook 设置 CanReroll。测试应真实取得遗物并走该 Hook，不能直接写 CanReroll 冒充覆盖。
- 同目录 `FresnelLens.cs`：它既能修改奖励候选，又能修改加入牌组的卡；两处都可能克隆并附魔。通常候选已附魔，不应假设正常领取必再次替换。必须构造明确、合法的原生 API 夹具使实际 Add 替换发生，并与正常候选生成路径分列。
- 同目录 `CardPileCmd.cs:447`、`:494`：CardsGained 在后续 ModifyCardBeingAddedToDeck 前记录；CardPileAddResult 返回实际 cardAdded 和 modifyingModels，CardReward 最终 CardChoices 使用实际取得卡。不同历史字段可能对应不同业务阶段，不能强制全部等于最终实例或手工修历史。
- `.build/runs/n3c-work01/static/Rewards.PotionReward.cs` 与 `Commands.PotionCmd.cs`：TooFull 返回 false，其他失败（包括 Sozu NotAllowed）可令奖励完成并设置 ClaimedPotion，但没有实际入槽；NotAllowed 先于满槽检查。不能将 ClaimedPotion 当取得证据。
- `.build/runs/n3c-independent-review01/static/Sozu.cs`：原生 ShouldProcurePotion 拒绝本玩家取得。

这些是静态核实，尚不是新增业务执行证据。开始实现时核对固定游戏输入与具体方法签名；SpoilsMap 的具体修改入口和返回语义仍须先读取实际本机代码，不能从名称猜效果。

## 串行工作包

### R-A：先建立领取与菜单的完整契约

保留外层 reward-set/reward-item，增加同一 CardReward 的 selection-session 与有序 menu attempt（名称可按现有约定调整）。父级 subject 必须是同一真实 reward，menu generation 单调递增，原生 choiceId、root/context、候选对象及模型/升级/附魔来源一并保存。

每个菜单须恰有请求、答复、开始及终结；终结区分 reroll/继续、领取、跳过/未完成。继续型 alternative 结束当前菜单但不结束 OnSelect 或外层领取。原生异步任务的真实完成、异常和嵌套奖励分别绑定；不能在第二次 selector 调用时凭推断补一个“原生已完成”事件。优先观察原生 alternative Task/Reroll 返回等确定边界，新增方法仍经严格 Host 注册审计。

静止点保留完整快照，业务区间有明确来源/结果契约。全量状态比较不能放宽为只核对金币或牌数；需要新增业务字段时先更新采集与契约。支持数据协议可版本化，旧原包保留；原反例在新 schema 上重建后再证明语义拒绝。

### R-B：Driftwood 的真实 reroll

真实 RelicCmd.Obtain、奖励生成/修改、RewardsSetSynchronizer.SelectLocalReward → 原生 alternative.OnSelect/Reroll/Populate → 第二菜单合法选择。策略必须能选择非首项，第二菜单不能借用第一菜单答复或实例。验证旧候选退出、实际新候选、CanReroll 原生变化、历史顺序及完整 RNG。

先各做 reroll→取卡、reroll→skip、直接取卡对照；不要以手动 Populate、替换奖励索引或换 seed 避开失败。对原生允许“继续选更多卡”的额外 Hook 先明确检测/Unsupported；本轮无需顺带适配所有多取卡遗物，但协议不得混淆它与 reroll。

### R-C：真实 card-add 与 potion-procure 结果

在当前选中卡/药水的领取作用域内，观察原生调用参数、实际 Task 返回和所有修改来源。默认卡、发生真实替换的卡、原生拒收分别保存 offered/selected/actual、成功与原因；永久牌组完整内容/升级/实例、附魔状态、历史与后续战斗复制来源交叉验证。

FresnelLens 至少分两条：正常候选生成已被修改；合法 API 夹具的未处理可附魔候选在 Add 时被实际替换。需证明旧实例未错误入牌组、实际新实例确实入组，modifyingModels 指向真实遗物。CardPileCmd 拒收需寻找本版本真实 ShouldAddToDeck 分支；若无可达最小实证，明确未覆盖，不能注入假返回并称原生拒收。

Sozu：正常取得、无 Sozu 满槽→丢弃→重试、Sozu 有空槽、Sozu 满槽均记录实际结果。后两者应由真实业务决定 NotAllowed 与奖励完成；不为“成功”手动放入槽位。未知的额外取得 Hook 保留显式边界。

### R-D：Neow 迁移到同一奖励会话

复用已知固定 seed 的真实 Neow CardReward，保留正常开局、真实 EventOption 和连续嵌套奖励任务。迁移 selector 与 CardReward Task 作用域时，避免旧 pendingRewards 与新协议双计数。先保证 GoldenPearl 旧 24 动作路径，以及原卡奖励自然 24/25 动作路径仍重放一致，再增加 Neow 取卡→下一合法节点/战斗首行动证据。

自然路径与取得遗物的 API 夹具分开，不能将 R-B/R-C 夹具拼成 normal-start run。模型状态/动作/选择 identity 继续对同构建重复与冻结重放比较；若身份号因观察顺序改变，使用明确完整映射，不删除比较字段。

### R-E：宝箱修改来源账本

先核实 SpoilsMap 实际业务方法，再通过真实取得、原生宝箱生成/奖励/分配/取得/退出执行。来源账本区分原始生成、Hook 修改、最终可选、选中和实际取得；金额与新旧实例绑定各自原生阶段，不能将“取走”默认成“新增”。保留普通宝箱和 skip 基准、真实修改夹具、替换/拒收分支的覆盖边界。这个包之后再切事件战斗与 Boss，不在奖励轮次启动无界全内容适配。

## 最小验收矩阵

| 类别 | 实证门 |
| --- | --- |
| 普通领取回归 | 原 take/skip、非零候选、升级/来源、连续 reward-set、容量/丢弃/使用；实际原生任务和永久状态 |
| reroll | 两菜单顺序与代次、旧候选历史、新候选真实生成、领取/skip、完整 RNG、非首项实际控制 |
| 替换 | 正常生成 Hook 与实际 Add 替换分列；selected≠actual 的真实执行、旧实例未入组、后续复制来源 |
| Sozu | 空槽/满槽均覆盖 NotAllowed；与 TooFull 非完成区分；槽位未新增却 rewardComplete 的事实可核验 |
| Neow/宝箱 | 自然开局卡奖励与普通路径；真实修改 API 夹具；完整父子链及退出状态 |
| 非法/缺失答复 | 缺失、多余、过期、跨菜单、重复消费、非法实例；晚到选择保存真实已执行前缀，不假称零派发 |
| 数据负控 | 整段子操作缺失/重复/倒序/错父，候选与结果共同篡改，登记/代次/新旧对象错配，静止完整状态共同篡改，历史/RNG 缺失，合法等价通过 |
| 错误传播 | 真实异步业务之后注入的明示控制故障，保存副作用及未确认动作；异常不得被外层成功吞掉 |
| 重放/观测 | 同种子同决策重复、冻结重放、完整/最小或关闭额外观察模式结果与完整 RNG 一致 |
| 隔离/冻结 | 未知补丁实际审计 API 负控，MemorySaveStore/存档与遥测拦截，15 输入52兼容源码不变，全部适用矩阵同最终构建 |

正常/损坏/等价样本事先登记，保留失败和原始退出；死亡、Unsupported、预算停止不算语义通过。最终记录所有实际源码、输出和游戏输入哈希；后续代码修正须重建同构建证据。只读证据重检、审计 API 和原生业务执行分别计数。

## 后续 M1 必须保留的工作

完成本轮后接 4D2 WhisperingHollow/事件战斗恢复及 4D3 Elite/Boss act-ready，再正常同 Run 跨幕至真实终局与多种子数据验证。商店特殊拾取、卡/遗物补货、更多 Hook 和多商店 session 仍是原版可达内容门；依据实际路由和内容范围逐项落实，不能用静默不执行制造完整 run。

只写 Lab 和隔离证据，不改角色、安装、真实存档，不启动游戏 UI、不调用真实 Steam/网络、不提交推送。实施者自主处理可恢复问题；只有实质权限阻塞或需用户决定的范围变化才暂停相应依赖。
