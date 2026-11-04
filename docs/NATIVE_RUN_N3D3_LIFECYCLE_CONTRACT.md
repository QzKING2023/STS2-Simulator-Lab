# 4D3 首批精英/Boss驱动契约

本文件限定首批内部控制器的生产检查责任，不代表整个4D3已完成。固定游戏v0.111.0/41cef1ea、Ironclad/A0、原生执行、独立内存档案，预测为NotCompared。

输入预登记见`.build/runs/n3d3-work01/preregister.json`。同一正常开局先完成一场真实Monster及原生奖励，夹具再通过原生`EnterRoomDebug`进入当前默认幕对应池中ID按Ordinal排序的首个精英/Boss。该规则在首次观察战斗结果前固定；实际为Overgrowth的BygoneEffigyElite和CeremonialBeastBoss。没有更换失败样本、修改HP、敌人、能量或牌组。

`major-natural`使用正常地图Children及原生行进守卫，目标为可见地图中最近的精英。三个开发seed01/02/03全部保留。它当前尚未整合已存在的商店和Hollow控制器，遇到这些内容显式Unsupported，不跳过或换路伪装完成。

## 观察与执行边界

| 事务 | 原生执行和生产数据门 |
| --- | --- |
| 根动作 | 复用一次性native票据、当前状态/序列/合法候选检查、完整选择消费及统一结果分类。每次dispatch绑定上次不可变确认完整状态。 |
| 当前战斗等待 | `NativeHost.LastVictoriousRoom`只在实际EndCombatInternal前缀登记；WaitBoundary必须确认获胜对象就是当前CombatRoom。累计胜利数不再单独决定本场胜利。无新游戏补丁。 |
| 原生计划 | `major-plan`在开局后静止边界唯一出现，记录实际默认幕、两个原生Encounter池、固定选择规则、目标和策略。支持来源natural/fixture与replay方式分开。 |
| 地图 | 候选快照绑定当前节点、唯一目的地和当前确认状态。Travel必须来自候选，返回节点等于实际决定。原生执行再次检查Children与Hook守卫。 |
| 到达 | Travel/夹具动作确认后必须且只能有一次`room-entered`，绑定实际新房间和完整确认状态，然后允许战斗。 |
| 夹具进入 | Canonical Encounter只用于发现/选择，实际通过原生ToMutable建立房间实例；保留独立fixture声明。它不等于自然地图进入精英/Boss。 |
| 战斗开始 | `major-battle-start`与`first-battle-opening`分别必需、唯一，绑定具体房间、Encounter、RoomType、ShouldGiveRewards和完整静止状态；第二个夹具战斗必须等于预登记目标。 |
| 战斗执行 | 复用实际TryManualPlay、EndPlayerTurnAction、原生敌方调度；清理前复制历史。当前策略v2依赖实际当前敌意，审慎标为Oracle，不声明所有敌意都是玩家可见。没有未来RNG/未来回合预测。 |
| 战斗胜利 | 实际胜利回调必须来自当前房间和当前根动作；该根确认后才允许清理记录，清理快照绑定完整确认状态。实际回血和清场在业务区间发生，保留HP71→77等旧回归。 |
| 战斗结果 | `major-battle-result`必须在对应清理后出现，完整快照不变；普通战斗胜利不成为RunVictory。 |
| 奖励开始/返回 | 记录实际ShouldGiveRewards，实际调用RewardsCmd.OfferForRoomEnd；如果应发奖励，必须完成本次RewardOffer与原生集合，父返回绑定最后子返回，随后才能确认/离开。无奖励分支需明确且无伪造变化。 |
| 奖励生成/取得 | 复用原生候选/集合/实例/卡菜单协议；当前生成区间完整保留非Rewards-RNG状态，并核对该流的counter与状态字按步进一致。抽取次数取记录counter差值并受128预算限制，**没有独立重算全部池抽取次数或候选生成算法**。金币按实际报价全量状态校验；卡牌新增实例/内容/升级和其它完整状态绑定；有限普通药水按实际首空槽取得。未来遗物/Hook需按实际触达补门，不由此声明全部奖励语义覆盖。 |
| 房间选择 | 请求/开始/返回/确认/边界必需唯一，绑定当前请求、session、候选与根动作；结果快照及完成事实不能共同脱离实际返回。复用实际原生事件任务和选择提供者，未知事件保留明确停止点。 |
| 死亡 | 真实ProcessPendingLoss开始/完成、HP0、实际停止战斗；death→terminal-stamp→terminal-contract必须有序且唯一，最后确认前缀完整。Closed只有false→true允许变化，终态后旧动作零派发拒绝。死亡不领奖、不调用胜利清理、每Run独立进程。 |
| 失败与预算 | `major-action-fault`在实际卡任务完成后、确认前注入包装异常；`major-action-budget`在第一个真实战斗动作确认后停止。它们是明确control，不冒充原生引擎缺陷。原始退出和已发生效果/确认前缀保留，不认证为成功。 |
| Boss后ready | 代码要求本次真实Boss胜利及对应奖励返回，才调用原生SetLocalPlayerReady并等待真实EnterNextAct Task；任务完成、持久状态、返回/确认分别校验。**首批Boss死亡，当前未取得该分支实际执行证据**。 |

生产检查器为`NativeMajorContract`与共享`NativeRewardContract`，共用外层NativeTraceContract的任务/选择/历史/状态结构及原始证据链检查。新major事件在其它profile中拒绝。每类静止观察使用完整JSON结构比较，包含新增字段；真实业务变化只在明确区间允许，不以删除字段或压缩日志获取通过。

## 源码、支持和收口

同构建验收包括首批精英/Boss真实死亡、自然样本、冻结重放、minimal/off对照、实际错误/预算控制，及适用旧1–4D2原生矩阵和Core。生产反控覆盖记录缺失/重复、动作归属、完整快照、候选/结果、整子树共同篡改和终态矛盾，等价副本保留。

首轮attack-first策略结果保留；第二轮增加当前敌意下优先格挡到10，再按攻击/Bash优先的合法排序。策略差异不冒充同策略重复，也不用于胜率推断。固定候选仍死亡，不能关闭Boss胜利、精英/Boss奖励、act-ready或自然跨幕验收。下一轮需以真实沿途奖励、升级、遗物/药水和合法策略改善可达性，并连接既有节点能力；禁止为获胜强改资源或敌人。

该驱动不增加宿主权限、不安装软件、不加载Mod/CombatSolver、不启动UI、不调用真实Steam/网络/用户保存，不提交或推送。合法游戏程序集仍是原生执行依赖。完整M1的自然RunVictory和步骤5–6继续待办。

最终自查收口：离开就绪为跨房间动作前置且唯一；到达session/type/source绑定实际房间，所有房间选择请求不可复用。原生Loss/Act/平台与run终态计数逐个关联观察到的实际调用，静止完整快照与业务回调投影结构同时检查。最终报告见[N3D3验证](NATIVE_RUN_N3D3_VALIDATION.md)。

## R1–R3独审修复补充

最新有限契约以[三项修复报告](NATIVE_RUN_N3D3_REVIEW_REPAIRS.md)为准：死亡输入→完成仅允许固定原生阶段变化，完整完成快照继续绑定根确认和终态事实；详细history/清理绑定当前动作，checkpoint绑定最后确认边界；夹具声明必须唯一且与目标、完整派发、实际新房间和开场一致。76源输入的最终冻结004B16F2…35AA8ECC完成同构建309/3908+Core98及最终153 reader控制，待独立复审。原契约与旧失败证据不删除，胜利/奖励/act-ready未运行的限制仍适用。
