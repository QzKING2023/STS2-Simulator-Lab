# 奖励闭环三项修复：最终独立验收

结论：**当前有限奖励闭环通过独立复验，三项必修已关闭。** 可以接续 [4D2 事件与事件战斗交接](NATIVE_RUN_N3D2_IMPLEMENTATION_HANDOFF.md)。本结论不表示完整 4D 或 M1 已完成。

本轮只读生产实现与原证据，新增独立检查脚本、副本、日志和本报告。核对冻结 `AFB0E42BE7173FFDF371CE7E0DDBEC605430151608F171177F9436C12C289A52`，Native DLL SHA256 `B8ACA53758BB161E3D8A432FB4C14C0F55BF8904DA02F317F914A3091B15872E`；重新计算 manifest 的 Build ID 一致。证据目录为 `.build/runs/reward-closure-repair-independent-review01/`，入口 `closeout.json`。

## 独立执行与来源核查

| 实际操作 | 结果及口径 |
| --- | --- |
| 用最终 DLL 的生产 `--review` 读取原 13 个反例的新副本 | 13 个全部在对应语义门拒绝，未靠 seal 失败 |
| 读取本轮 171 个控制的新副本 | 161 损坏拒绝、10 合法等价接受，与声明一致 |
| 读取 10 个完整正常业务副本 | 全部接受，包括拒收完成、真实替换、reroll 后取卡/跳过、Neow 和 SpoilsMap |
| 新增 33 个独立控制，重新计算有效 seal | 28 损坏拒绝、5 合法等价接受；无误接受或误拒绝 |
| 来源断言重新执行 | 530 项全通过，当前源码/快照/产物/游戏输入/280 执行记录和原始退出匹配 |
| 额外支持核对 | 111 项全通过，包含 16 个完整静止边界、72 个重复/观察模式完整 JSON 对照、7 个故障前缀及历史原件/日志检查 |

本轮共 **227 次实际生产 reader 调用：202 拒绝、25 接受**。没有重新运行 280 次游戏业务或 Core98，也没有把读证据的调用计为新的游戏执行。

原最终矩阵仍为 **280 原生进程、3343 套件检查、另计 Core98**。逐组为 closure35/419、transactions18/471、economy23/361、merchant25/371、suite55/561、n3b34/307、n3a16/273、n2 74/580。原始退出分布 206×0、67×1、4×2、3×3；预期故障、Unsupported 和预算停止均保留。Core98 日志与实际检查器/Core 产物哈希已读，Core 依赖与冻结 Native 构建一致。

993 次历史 reader 执行的既有记录、对应副本内容及实际日志逐一复核，3888 个历史原文件哈希保持。本轮没有再执行这 993 次；其子集按上述 227 次独立读取。15 个锁定 Mod 输入、52 个兼容源码和 HEAD `d73039d688eb54c9513f5b9508a03947e689aa18` 保持。读取来源脚本最初使用了错误的字段大小写、来源枚举及 CRLF 比较，已按实际 schema 和文本换行修正；初始脚本结果保留为 `supporting-checks-initial-script-mismatches.json`，未改原证据或生产逻辑。

## R1：Sozu 的处理完成与取得事实

`NativeRewardClosureContract.cs` 的当前采购 profile 只声明 Sozu 空槽/满槽两条路径。它要求处理前持有真实 Sozu，并要求 `success=false`、`NotAllowed`、`actual=null` 以及完整状态不变；外层已处理完成和 claimed 来源另行绑定。旧 4C 普通成功、TooFull、丢弃重试仍由原契约验证。

原两个伪取得副本均拒绝。新增控制还覆盖拒收却改变槽位、错误 claimed 指向已持有 BlockPotion、把 NotAllowed 完成改为失败，以及采购开始/返回共同篡改。拒收完成的正常副本和属性顺序等价均接受。故本次关闭“拒收却伪报取得”，并未把所有药水采购一概认定为拒收。

## R2：完整父子静止状态

`NativeRewardContract.cs` 现在将 card-menu 入口绑定当前 reward-item 的不可变初态/最后子返回，将直接 card-add 入口绑定当前 RewardSetup；RewardOffer 与具有直接 Add 子项的 RewardSetup 返回绑定最后子返回。原有集合/菜单/答复/完成/确认链继续要求完整、唯一和正确归属。全结构比较保留未知嵌套字段，不只检查金币或 HP。

独立分析 10 个正常流程得到 16 个适用入口/返回静止边界，全结构相等。原八个共同篡改反例拒绝，新增父返回后所有快照共同修改、第二代 reroll/Neow 菜单共同修改后恢复、嵌套 RNG 扩展均拒绝。真实 Populate、reroll、Add、Neow 和无子项 Setup 的业务变化仍通过；这些真实业务区间没有被误当静止。

## R3：新旧对象、类型与卡内容

`NativeRewardClosureContract.cs` 保留登记类型和 model，并用固定游戏程序集的真实 Cards/Relics/Potions 类型元数据交叉核对；只查询类型，不初始化 ModelDb。永久牌组实例 id 必须唯一；selected 和 actual 都须位于旧永久牌组之外。新增 actual 与末尾完整卡投影、原生内容、升级、旧牌组前缀、Fresnel 修改者和历史增量互相绑定。

原三个身份/类型/升级反例全部拒绝。新增验证包括：selected 冒充已持有同模型对象、替换 actual 指回 selected、把 actual/native/deck 的升级一起改为 9、把类型改成另一个真实 CardModel，以及实际卡与永久牌组的原生 floor 同时篡改。合法替换与同实例取得均接受。三个将原生省略升级零显式写为零的等价副本也接受，证明原生默认值没有被误判。

## 保持的真实行为与边界

当前真实业务证据仍为 native-only/NotCompared。Driftwood 原生重选及两代候选/RNG、Fresnel 候选阶段修改与 Add 阶段替换、Sozu 拒收但奖励完成、Neow 两次原生取卡及后续 DeckVersion 关联均保持。重跑与 detailed/minimal/off 对照比较完整 route、choices、reward-choices、final-state JSON，包含记录的完整 RNG；此处重复执行与旧自然路径冻结重放分别记录，不混称为新 frozen replay。

七个闭环故障控制继续保留 3 确认/4 派发、实际已发生的 reroll/Add 和未完成根动作，数据不标完整。旧自然 skip24/take25、多节点、真实 Death、Unsupported 和尾部夹具在同构建矩阵中保留。四方法、八未知补丁拒绝仍是隔离 Harmony 审计 API 证据，目标业务调用为零，不能当作额外游戏覆盖。

固定原版 `ShouldAddToDeck` 没有覆盖且基类返回 true，健康奖励卡由该 Hook 拒收在本版本不适用；原始 PE 元数据证据保持，未注入假拒收。死亡所有者等 Add 前置失败不因此被宣布永远成功。SpoilsMap 的 44 基础金币 +600 任务、移卡及历史仍是明确 API 夹具，不是自然跨幕证据。

本轮未发现新的必修项或执行阻塞。特殊拾取、多取卡、卡/遗物补货、更多原版 Hook 和连续 session 仍需后续内容门；有限 profile 不能自动推广。原生事件战斗恢复、Elite/Boss、正常同 Run 跨幕完整终局及预登记批测继续待办。未修改运行实现、Mod、安装或用户存档，未启动 UI、真实 Steam/网络，未提交推送。
