# 自然第一幕 Boss 与下一幕首决策契约

本内部配置名为 `major-natural-boss`。它从正常 Ironclad/A0 开局出发，沿原生地图合法边、战斗、休息、已审计事件和宝箱推进，实际击败本次生成的 Boss、领取原生奖励、ready 并等待真实 `EnterNextAct`，最后进入第二幕原生起点的可决策状态。该切片尚不选择 Ancient 奖励、不表示完整多幕或完整 M1。

## 归属和生命周期

| 阶段 | 原生权威及生产检查 |
| --- | --- |
| 开局与地图 | 原生 `EnterAct` 内生成的地图；generation0 的 NullActMap 不充当地图。保存完整有向图、起点/Boss、地图实例、生成代次和实际生成模型。图为 Oracle 审计数据，策略只按公开边/节点类型排序 |
| 地图决策 | 当前节点的完整原生 Children；候选坐标、类型和数目与不可变图一致。正常开局和新幕起点独立，不能使用 Debug 房间跳转 |
| Boss | 实际抵达的 Boss 节点、CombatRoom/Encounter 实例及生成模型绑定。途中普通胜利、精英胜利均不代替目标 Boss |
| 胜利与奖励 | 延续已经验收的本动作条件锚点、存活/敌人终态、历史清除、确认、清场、原生奖励集与实际取得实例契约 |
| 跨幕 | ready 原生任务真正完成；第二幕新地图实例、MapRoom、generation、位置及完整持久状态连续。复制旧地图或只改幕号不能满足完成 |
| 起点候选 | 唯一静止观察，与最后确认的 MapRoom 完整状态绑定；候选为真实 `StartingMapPoint`，当前 node 为 null |
| `NextActStart` | 仅跨幕后、候选存在时执行原生 `EnterMapCoord`。结果必须是新 EventRoom、实际生成 Ancient 及真实起点；当前有限配置要求未被 Hook 屏蔽的 Tezcatara |
| 起点结果 | A0 原生 Ancient 入场恢复至 maxHP；永久资源、地图和其余状态保持。允许新 EventRoom、EventModel、原生选项及其初始 RNG，按固定版结构验证。各新实例必须在本次派发后首次登记，不能借用旧房间或旧选项 |
| 首决策 | `room-entered` 后唯一 `natural-first-node`，静止完整状态及父动作归属一致；仍为未完成的 Ancient，尚未选择任何奖励 |
| 切片完成 | 同 run 目标 Boss 胜利、奖励、真实跨幕、首节点确认及首决策记录齐备后，才允许 `major-complete` 和奖励契约结束 |

Tezcatara 的固定 v0.111.0 独立规则是三个初始选项，分别来自三个原生池，初始 EventModel RNG 消耗三次。校验完整选项数量、顺序、来源、可用性、实例和 RNG 结构；该期望来自锁定程序集静态检查，不从运行时实际数组长度反推。其他 Ancient 或禁用 Ancient 的 Hook 在进入起点前明确 Unsupported。

旧 `major-prepared-*` 仍保持独立 API 目标夹具。自然配置禁用 `fixture-setup-declared` 和 `MajorFixture`，来源始终是 natural-run；重放/观察模式单列，不能把真实死亡改标为切片成功。

## 策略、失败与展示依赖

`oracle-legal-natural-boss/v1` 复用已有合法卡牌/药水排序、奖励选择和休息决策。公开地图的路径成本通过有界 DAG 记忆化计算；未知房间和未经组合的事件战斗/商店明确停止。本配置不是公平信息策略评估，也没有全局最优保证。

保留 DEV01/02/03，不以成功替换死亡或 Unsupported。源轨迹发生非法动作、预算、任务错误和数据错误时，保留已确认动作及未完成动作边界；重放输入损坏负控必须先具备完整选择和奖励选择文件，才能证明在预期动作处拒绝。

Tezcatara 原生选项构造会创建 Power 悬浮提示。锁定版本 `PowerModel.Icon` 只调用纹理加载，不读 RNG 或执行玩法；无 UI 配置仅把该 getter 的纹理返回值适配为 null，并继续执行原生选项生成。方法来源/未知补丁检查和运行期间补丁清单稳定性继续适用。未适配版本曾在 Godot ResourceLoader 初始化时 native crash，原始前缀和退出码应保留。此适配不证明零售 UI 显示或演出等价。

本轮保留一 run 一进程、MemorySaveStore、保存/replay 和 Steam/网络隔离、15 锁定输入及 52 兼容源码。后续通用多幕控制器仍需真正按 room/session 所有的奖励和宝箱状态、Ancient 选择及事件/商店模块组合，不能靠清零旧计数或切换 scenario 绕过契约。
