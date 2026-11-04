# 4D2 当前事件生命周期契约

实施中的有限范围：event-natural 固定 DEV_03，延续旧 rooms-natural 到 WhisperingHollow，真实金币/两瓶药水分支，合法继续到下一 Play；event-combat 为 BattlewornDummy V1 原生事件房间 API 夹具，先用声明的攻击优先/领取首个攻击卡策略真实完成沿途节点，再使用原生房间 API 夹具；实际选项、子战斗、原生奖励、实际 Resume Task、原生地图离开原房间至下战 Play。死亡、时间到和执行故障不冒充成功恢复。

| 边界 | 必需性、归属与状态 |
| --- | --- |
| Unified 启用 | 一次，根外已确认边界；原完整状态保持，只新增 inventory 投影。旧自然前缀不更换 seed/奖励策略 |
| 父事件 | 原生 Room/Event 实例与当前状态绑定，stack 深度记录；选项请求绑定实际合法候选/原生顺序 |
| 子战斗 | 原生 Setting1，真实 CombatRoom、父 Event/room、V1 Encounter、depth+1，不直接构造胜利 |
| 胜利 | 原生 EndCombat 回调与清理前历史；真实 Death 不发奖励、不恢复为成功 |
| 战后奖励 | RewardsCmd.OfferForRoomEnd 实际生成；统一会话复用4C完整父子/选择/实例规则，空奖励可合法 |
| Proceed | dispatch 完整状态；原生 ProceedFromTerminalRewardsScreen 一次，退出子房间并恢复同一父房间 |
| 真正 Resume | 具体原版 override Task，唯一开始/完成/异常，关联 event/exited child/root/context；外层返回不是任务完成 |
| 返回及确认 | 实际 Resume 完成后的外层/Proceed/确认完整状态绑定，保留合法恢复与奖励业务变化 |
| 完成 | 事件实际 IsFinished，原生地图行进、下一真实 Play 及全部永久牌的 DeckVersion/模型/升级关联。失败保留当前已发生副作用与确认前缀 |

正负控将使用生产 reader：缺失/重复/错序/错归属、父子实例错误、全结构共同篡改后恢复、嵌套 RNG/新增字段、完成前终止，以及合法业务/等价副本。新增方法保持实际来源/MVID/未知 patch 拒绝；原始失败不覆盖。

本文件不是完整 M1 验收；Boss、自然跨幕和全 Run 仍按依赖后续推进。仅 Lab 内离线，不改角色/安装/用户存档，不运行游戏 UI/真实 Steam 网络，不提交推送。

Hollow 金币独立契约从其一次已推进的 xoshiro 状态反推上次输出，按原版有界整数算法核对精确费用。Resume 入口由 Proceed 完整状态按真实退出子战斗/恢复父事件的四处投影转换推导，父事件内容取其不可变旧快照；未知字段仍完整比较。

原生子房间 Exit 后仍保留玩家战斗对象的 turn/energy/phase/piles；仅 CombatManager 的 round/side/enemies 失去当前战斗上下文。生产 reader 保留这些实值，不把整个 combat 投影假定为 null。

所有 ef-* 类型必须属于已注册的事件场景；旧奖励/战斗场景插入事件完成记录必须明确拒绝，不能因类型已知而忽略。独立跨场景反例保留在 work01/out-of-profile-counterexample。
