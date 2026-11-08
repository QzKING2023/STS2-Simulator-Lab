# 第三幕自然入口、首战与连续节点

2026-11-03。状态：实现及分层验证完成，待独立审查。完整 M1 继续进行；本轮没有到达第三幕 Boss 或 RunVictory。

## 实际进展

基于已通过独审的第二幕冻结，新增两个明确的自然场景，仍为固定 v0.111.0 / 41cef1ea、Ironclad/A0、独立全解锁零局数档案。每条路径都从原生 Neow 开始，前367个动作及状态/RNG戳与原第二幕成功路径完全一致，没有拼接或跳房。

| 场景 | 实际原生结果 |
| --- | --- |
| `major-natural-act3` | 387/387动作，SliceBoundary/Ongoing。a368真实 EnterMapCoord 到第三幕 Tanx，按A0原生回满HP；a369选择实际候选 MeatCleaver，取得新遗物；首战 ScrollsOfBiting 胜利和奖励后HP32、金币525，位于第三幕第2层。 |
| `major-natural-act3-route` | 当前版本 `oracle-legal-natural-act3-route/v2-axe`。Tanx实际选择 ThrowingAxe，第三幕两场胜利并领奖；随后在第4层 Fabricator 战斗实际死亡，418/418动作、HP0、金币539，Completed/Death。没有发放死亡战斗的胜利奖励。 |
| 原预登记 DEV01/02 | 仍为第一幕真实 Death93/Death156，未替换失败种子。 |

Tanx 的实际候选是 ThrowingAxe、MeatCleaver、WarHammer。原生选项生成和 RelicCmd.Obtain 负责全部效果。连续路径选择 ThrowingAxe 后，第三幕三场战斗共有12条原生 CardPlayStarted/Finished 历史记录标明 PlayCount=2，证明执行了原生重复出牌机制。开发版v1选择MeatCleaver后Death422也保留。当前v2前两场胜利时HP为47、26；v1为32、11。这是同一开发种子的探索性比较，没有独立胜率含义，不是预登记留出集。

## 实现与契约

`NativeThirdAct.cs` 保留原生准备投票/EnterNextAct、地图边、Ancient初始化、实际选择、战斗、奖励与旅行。有限首战场景和连续节点场景分开命名。连续节点只使用当前生成的公共地图作有限路径排序，不读取未来抽牌或RNG来挑结果。尚未到达的分支不计支持。

原 `ActScope` 在真实下一幕首战/奖励完成后归档前一幕，以当前幕索引新建作用域；全局任务、房间、历史和动作计数继续累计。Ancient首节点的实例登记按每次真实入口建立独立范围。不同幕的 first-node 阶段分别登记，不能借用上一幕完成条件。

`NativeThirdAncientContract.cs` 核对第三幕实际 Tanx、三项唯一候选、已登记的 EventOption 类型、固定版本候选池、完整事件RNG记录、实际选中选项、实际新增遗物身份和具体类型。简单遗物取得后仅允许明确的遗物追加、事件完成和选项清空，其他完整投影保持。TriBoomerang附魔选择未由此场景支持；未知效果不合成结果。当前实际取得证据只有MeatCleaver和ThrowingAxe，不能据候选池校验宣称所有Tanx遗物都已业务验收。

生产检查器同时验证新阶段的必需/唯一、动作/上下文/实例归属、顺序和完整静止状态；沿用原战斗胜利、真实Death、失败前缀、奖励和跨幕契约。新增18个损坏副本全部拒绝，3个正常/等价副本通过，包含整体缺失、重复、旧动作借用、错误候选与具体类型、共同篡改完整状态、遗物旧实例、错误幕目标、提前完成和展示适配错归属。副本使用生产封签工具后调用生产检查器，不只验证封签失配。

## 无界面依赖与失败保留

第一次第三幕入口进程在Tanx构造候选遗物时崩溃：Whistle的眩晕提示调用AtlasManager，触及没有Godot宿主的FileAccess静态原生接口。原始目录 `.build/runs/native-act3-dev01-main/` 保存rawExit=-1073741819、367确认/368派发和真实stderr；没有伪造result或把中断判通过。

静态检查锁定程序集的 `StunIntent.GetStaticHoverTip` 后，仅在新第三幕场景替换该展示方法：保留原有标题和描述LocString，纹理为null，不加载图集。原生Tanx候选生成、随机打乱、取得及战斗效果均保留。适配走现有严格 PatchMethod，拒绝未知补丁，记录方法token/MVID和处理器，纳入全局补丁清单稳定性检查。

最终构建对该新方法做了实际Harmony前后注入负控：外来prefix真实织入后安装被拒；移除后正常安装；晚加prefix后Ensure拒绝；移除后恢复预期的未初始化宿主隔离门。API夹具目标业务执行0次，不与战斗业务测试混计。证据 `tip-audit-final04.json`。没有扩大执行权限、调用真实Steam/网络、启动UI或触及用户存档。

## 最终冻结与验收集合

统一源码/生产器/检查器冻结：**`2EE02774AE02C77D25AA310382F08C5930580987D78F4DB7D70649A01A664FD3`**。

构建清单 `.build/native-build/act3-dev04/build-manifest.json`：94项dirty源码、58项编译输入、3项游戏输入、7项产物。源码副本在本轮 `source-final04/`，清单为 `source-final04.json`。15项锁定Mod输入、52项兼容源码保持；Core98使用同源码/产物历史证据，本轮未重新执行。

- 最终构建15个原生进程：第三幕首战live/replay，连续路径live/replay/minimal/off，DEV01/02，旧第二幕/精英/首战冻结重放，以及DEV03和原DEV01各一组skip/take。实际为6次Death、9次SliceBoundary，不计为15次胜利。
- 连续路径836个完整派发/确认状态在live/replay/minimal/off一致；首战有限切片774个状态重放一致。包括全部已声明run/player RNG的counter和s0–s3、动作、子选择、奖励选择及最终状态。
- 原DEV01 skip24/take25另行真实复跑通过。先跑的DEV03两条20动作烟测也保留，未用它们替代原24/25基准。
- 完整冻结证据集合571份由当前生产检查器重验，包含555份上一轮集合、本轮两个已完成开发运行、13个主验收进程和1份真实崩溃前缀。中断证据必须拒绝，未补造缺失result/seal。另两份原种子基准读取和21份新控制，使本轮验收集合为594次读取；所有预期接受/拒绝均相符。这些是验收集合计数，不含零散诊断读取，不代表新启动594局。

总目录 `.build/runs/native-act3-work01/`，主原生矩阵 `.build/runs/native-act3-final04/`。`closeout.json`、`final-checks.json`、`evidence-final04/summary.json`、`controls-final04/summary.json` 和 `baseline-extra.json` 给出明细。最终构建、两个早期开发构建及中断进程分别保留来源，不混称同一生产器。

本轮一次exec服务断连发生在测试全部结束后，汇总命令尚未创建进程。普通只读Get-Location确认恢复后才继续收尾；没有重复启动测试。汇总脚本自身的表达式和元数据类型处理问题已修正，未涉及生产代码或原始结果。

## 复现及剩余范围

所有输出必须使用新目录，例如：

```powershell
pwsh -NoProfile -File tools/native-run/run-native-slice.ps1 -BuildManifest .build/native-build/act3-dev04/build-manifest.json -OutputDirectory .build/runs/<fresh-third-first> -Scenario major-natural-act3 -Seed LAB_V0111_NATIVE_DEV_03
pwsh -NoProfile -File tools/native-run/run-native-slice.ps1 -BuildManifest .build/native-build/act3-dev04/build-manifest.json -OutputDirectory .build/runs/<fresh-third-route> -Scenario major-natural-act3-route -Seed LAB_V0111_NATIVE_DEV_03
```

重放使用同场景/种子，并传本次live的route.json为ReplayPath。minimal/off运行使用Observation参数。`suite.py`、`review-final.py` 和控制脚本保存在本轮目录供复核；它们拒绝/不适用于直接覆盖既有证据，应改新输出目录。

当前第四步自然房间组合仍有缺口；第五步已真实进入第三幕并继续多节点，尚未通过第三幕Boss与自然Architect/RunVictory；第六步必要数据和最终同构建全量门未完成。完整JSON投影不包含全部原生内部状态；Surrounded.Facing、ThrowingAxe使用标记、遗物状态/计数等已知机械数据须进入最终必要数据盘点。当前结果是native-only，不声称新增模拟差分认证或零售演出/RNG时序等价。

下一步见 [交接](NATIVE_RUN_ACT3_NEXT_HANDOFF.md)。继续既有授权，无需逐轮请批；本轮无提交推送、角色改动或环境审批阻塞。
