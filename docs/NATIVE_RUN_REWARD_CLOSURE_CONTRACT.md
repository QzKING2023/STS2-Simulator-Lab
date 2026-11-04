# 奖励闭环内部契约

本轮基于 4D1C 最终冻结，扩展原生奖励执行；旧 4C profile 继续使用原契约。新 `reward-closure/v2` 场景采用以下生命周期，不以记录存在或最后牌数代替实际取得。

| 边界 | 必需关联和状态规则 |
| --- | --- |
| 根动作与 reward-set/item | 原 root/context、实际 set/reward 身份，请求/答复/开始/返回必需且唯一；静止边界完整状态相等 |
| card-menu | 同 reward 单调菜单代次，当前原生候选与 alternative，当前选择必须来自该菜单；重选结束当前菜单，OnSelect 继续 |
| reroll | 实际 alternative/Reroll 调用与返回，旧候选历史、重新生成的候选和完整 RNG；不手动 Populate 或重建奖励 |
| card-add | 原生参数为本次选中对象，原 Task 返回 success/cardAdded/modifyingModels；完整永久牌组和真实历史绑定；成功替换需 selected≠actual，旧对象不得被误记取得 |
| potion-procure | 原生 TryToProcure 的 success/failureReason、真实槽位、claimed 与 actualAdded；NotAllowed 可完成奖励但不取得，TooFull 不完成 |
| menu/领取返回 | 绑定本次业务返回和外层结果；所有已开始子操作必须闭合，不能借用旧操作结果 |
| Neow/宝箱 | 自然事件作用域与专用 API 夹具独立标记；修改前、来源 Hook、修改后、实际取得分阶段记录 |
| 失败 | 保留真实已派发前缀及尚未确认根动作；未知业务明确 Unsupported，不能按无效果执行 |

新投影包括永久牌组原生序列化内容和原生地图历史，包含附魔/升级及阶段性历史差异。历史必须按原生写入阶段解释：CardsGained 可早于 card-add 替换，CardChoices 使用最终实际取得对象。

生产检查器与正负控随实现同步。缺失、重复、错序、错父、过期菜单/实例、共同篡改静止状态和结果必须拒绝，合法属性顺序或全程静态扩展应通过。最终全部适用回归绑定同一源码、构建和输入；本文件不声明尚未执行场景已经通过。

最终实现与同构建实证见 [验证报告](NATIVE_RUN_REWARD_CLOSURE_VALIDATION.md)。CanReroll 为 false 时过滤原生测试接口保留的旧 REROLL 项；菜单来源清单、完整实际取得内容、claimedPotion 与实际槽位以及任务返回归属均有必需交叉检查。
