# 奖励闭环修复：状态和实际取得契约

本文件承接 [三项独立审查](NATIVE_RUN_REWARD_CLOSURE_REVIEW.md)，限定当前 reward-closure/v2。原生业务调用保持，修复生产 reader；旧 4C 的采购、容量、丢弃重试与单菜单契约继续单独回归。

| 分支/区间 | 输入与处理 | 返回及可信数据要求 |
| --- | --- | --- |
| RewardOffer→reward-set | 原生 Offer 可在进入会话前 Populate，允许真实候选生成/RNG变化 | 会话结束至 Offer 返回无额外业务，完整状态绑定最后子返回 |
| reward-set→reward-item | 当前原生集合、剩余候选及有序选项 | 请求/答复/开始唯一，入口绑定上一子结果或集合初态；完成及返回绑定实际最后结果 |
| reward-item→card-menu | 当前选择的实际 CardReward；首次 selector 前无本 profile 的投影状态变化 | 首次入口绑定父状态；reroll 后下一菜单绑定父级最新子返回及实际生成结果 |
| card-menu→reroll | 当前实际 alternative、CanReroll、原候选、原生 choiceId | 原生旧候选历史、Rewards RNG 和新候选为业务变化；余下完整状态不变；菜单结束再进入下一代 |
| card-menu→Add | 本次选中候选、原生序列化内容和升级 | selected 不在旧永久牌组；Add 前绑定菜单；实际返回后只允许真实牌组与历史变化 |
| RewardSetup→直接 Add | 只有明确直接 API 夹具可产生 card-add 子项 | 子入口绑定父当前完整状态；子返回后至 Setup 返回静止。没有子项的遗物/药水/跨幕 Setup 仍执行真实业务 |
| 同实例取得 | native 返回对象等于所选对象 | 新对象原先不在永久牌组；保留全部旧对象内容/顺序，新增唯一实际对象，绑定模型/类型/升级/native 内容 |
| FresnelLens 替换 | 所选未附魔对象、实际 Add Task 与真实修改者 | actual 与 selected 不同且不在旧牌组，旧 selected 不得误入组；真实 Nimble2、新引用和原生历史分阶段绑定 |
| 候选 FresnelLens | 原生候选生成已修改，领取已附魔对象 | 此路径 Add 维持同实例；不得伪称又发生一次替换 |
| Sozu 空槽/满槽 | 真实持有 Sozu 的当前玩家，TryToProcure 在容量检查前拒收 | 本 profile 必须 NotAllowed、procureSuccess=false、actual=null，完整状态/槽位/历史不变；rewardComplete=true、claimed=原药水独立记录 |
| 其他采购返回 | 当前 closure 尚未声明一般采购成功/其他 Hook | 显式拒绝未声明分支；不能因返回字段自洽而放行。旧 4C 普通成功/TooFull/丢弃重试保持原契约 |
| menu→item→集合返回 | 实际领取、skip 或 reroll 的最后业务结果 | 必需子项完整唯一、顺序/父级/root/context 正确；外层 acquired 绑定实际返回，全部静止点完整比较 |
| Neow | 真实事件内统一奖励会话，连续原生 CardReward | 每个菜单受上述契约约束；保留真实事件业务变化，永久卡到战斗 DeckVersion 关联另验 |
| 宝箱/SpoilsMap | 原生基础金币、真实任务完成、移卡/完成历史 | 基础 RNG、任务600、真实移除和结果账本独立校验；仍明确 API 夹具，非正常跨幕路径 |
| 错误/中止 | 未知或非法选择明确拒绝；原生异步业务后故障可保留副作用 | 保留已确认前缀和未确认根动作；不将不完整证据当正常完成 |

所有永久牌组投影要求 id 唯一；登记类型必须是固定原生程序集内对应 Cards/Relics/Potions 类别的真实派生类型，并与 model 名称对应。reader 仅查询类型元数据，不执行 ModelDb 初始化。卡牌 Fact 与牌组均交叉检查 native id 与 `current_upgrade_level`；原生省略默认值按零处理，字段存在时必须为合法整数。

正负控包括原审查13反例、子区间整体共同篡改后恢复、完整 RNG/嵌套字段、Neow 第二菜单、旧同模型实例冒充新对象、不同类型登记、重复牌组引用、实际/投影/native 升级矛盾。合法旧对象前缀、原生替换、属性顺序和全程静态扩展继续通过，未减少差分字段或关闭审计。
