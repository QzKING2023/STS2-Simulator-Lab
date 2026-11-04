# 第二幕终态与重复宝箱：独立分层审查

日期：2026-11-03。结论：**本轮声明范围通过，未发现可复现的必修缺口。** 第二幕精英/Boss胜利、其奖励、再次跨幕和完整M1仍未验证。

本次重新读取生产代码、原始执行文件和构建清单，实际调用冻结生产检查器；未修改生产实现或旧证据，未新增原生游戏执行。依据[证据信任边界](NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)采用有限契约控制，没有重跑完整原生矩阵。

## 冻结与来源

- 17次最终原生执行的生产构建：`DEE290B603413137770A9C52ED4B905E2ED40701843D42FDD1655A18CF1BE957`，`.build/native-build/act2-lifecycle-final03/`。
- 当前源码与本次检查器：`908DE0CD7A852365FC40B55CE691C7AF36D0BF8A95742D024C056CABBC59328F`，`.build/native-build/act2-lifecycle-final04/`。
- 两份构建清单的规范化标识、编译输入、游戏输入和产物哈希均重新核对；当前91项源码、55项编译输入、3项游戏输入、7项产物一致。15项Mod锁定输入及52项兼容源码未变。
- final03到final04仅`NativeMajorContract`头部调用及`NativePreparation.MatchesDescription`读取兼容改变。完整差异确认旧v2/v3策略仅作为明确历史描述读取；生成用的Describe、宿主、采集器和策略不变。17次原生执行仍归属final03，不能标为final04生成或最终M1同构建全量验收。

构建及文件哈希证明来源一致；实际执行事实另由进程原始退出、动作、原生回调、任务和重放支持。未作操作系统镜像或外部进程级写入的独立证明。

## 原始事实核验

| 样本 | 独立读取结果 | 本轮可确认范围 |
| --- | --- | --- |
| DEV03正常开局主路径 | 360/360确认/派发；第二幕第16层KAISER_CRAB_BOSS；EndTurn:360，HP0 | 原生ProcessPendingLoss输入、返回、动作确认和Death终态有序且绑定同一动作；Crusher仍存活116HP，不能记为Boss胜利 |
| DEV03自然精英路径 | 306/306；第二幕第10层INFESTED_PRISMS_ELITE；HP0 | 正常Neow开局和合法地图路线；真实死亡，没有最后精英奖励或胜利记录 |
| 同一主路径三个宝箱 | i183、i644、i650；打开a90/a285/a290，退出a92/a287/a292 | 每个具体房间唯一原生Exit；第二幕实际连续遇到两个宝箱，旧房间完成不被借给新房间 |
| 双宝箱API夹具 | 9/9；i19领取一次、i22跳过；最终RestSiteRoom | 原生管理器转房拥有退出子任务；领取/跳过与实际实例一致。保留api-fixture标签，不作为自然路径 |

主路径共有16场真实胜利，第二幕6场；最终Boss不在这些胜利中。原生终态记录中的Losses=1、WinStarts=0，与最后敌方动作及HP0一致。17次最终执行是10次Death和7次合法切片边界，raw exit均为0，不能据此称17次胜利。

逐字段比较三种子的live/replay路线、子选择、奖励选择和完整最终状态。DEV03 detailed/replay/minimal/off全部720个派发/确认状态相同；精英612个、双宝箱18个状态相同，包含run/player完整RNG内部字。观察模式对照证明本次配置未扰动这些边界；不推导零售演出/RNG等价或独立样本胜率。

历史Unsupported、EngineError、InvalidAction、BudgetExceeded及DataError保持原始分类和确认前缀。例如Ranwid停207派发/206确认、Doll240/239、InfestedAutomaton294/293，旧Rocket空音频错误355/354，均未改为成功。

## 受影响调用链

`NativeHost.VictoryConditionAfter`读取原生`CombatManager.IsCombatEnding`实际返回值，并要求无pending loss、战斗进行中、没有存活主敌人。原生方法自身仍执行结束Hook；观察器没有重算或再次调用它。`Program.EnemyState`保留原投影并新增原生IsAlive/IsPrimaryEnemy。真实a282胜利留下HP0、非主敌人的PARAFRIGHT；该记录有当前动作原生历史及结束判定锚点，回调完整状态相等。早于本动作历史追加的查询不能在detailed模式借前一动作历史授权胜利；每次派发重置胜利锚点。

Rocket的窄transpiler仅将一个`NAudioManager.PlayOneShot(string,float)`调用转换为受限接收者包装。原BeforeDeath、IsOverOrEnding查询及其余分支保留；固定原生PlayOneShot在TestMode本来无业务效果，空接收者此前会在进入方法前抛错。实际a356记录一次guard；最终仍真实Death。安装检查已有Harmony所有者、唯一IL锚点，并记录原方法token/MVID/IL哈希；全局补丁清单稳定性仍在。**新方法逐项实际Harmony注入负控本轮未执行**，不能称已具备该项动态审计证据；当前结论依赖固定无Mod宿主、静态安装门和已执行路径。最终全量门应补该窄API负控。该适配不证明真实音频、动画或零售时序等价。

自然宝箱的退出开始以原Travel派发完整快照为锚点，仅允许实际目标node/floor和当前房间出栈；退出返回与开始完整状态一致。API夹具通过`NativeRepeatTreasure`包装实际Exit任务，`NativeRewardContract`将子任务归属、具体宝箱、顺序和完整状态绑定父RewardSetup。两个路径都保留原生业务变化，没有提前手动退出再重复退出。

## 本次生产检查器实测

证据目录：`.build/runs/act2-lifecycle-independent-review01/`。

- `reread-summary.json`：371份原始执行证据全部重验，282份完整证据通过、89份失败/未完成证据按预期拒绝；另外70项历史控制和24项实现者本轮控制符合预期。合计465次生产读取，1107项来源、隔离、退出及完整重放核对无失败。
- `controls/summary.json`：独立新增28项，其中24项损坏副本全部由生产语义门拒绝，4项正常/对象字段重排等价副本通过。损坏副本重新封签，排除仅因哈希链损坏而拒绝。
- 新负控覆盖自然第三宝箱缺失/重复/错序退出、旧房间和旧动作借用、成对完整状态及RNG变更；API子任务父归属、跳过结果和取得实例；存活主敌人、false结束判定/pending loss、缺当前锚点、minimal/off跨动作借用和旧history epoch；音频来源/上下文；Boss/精英死亡状态与终态位置、重复loss返回。
- `facts.json`：29项直接原始事实核对通过。

合计**493次生产检查器调用，0次新增原生游戏执行，0个预期之外结果**。所有原始文件和冻结实现保持不变。没有把预期拒绝计为游戏完成，也没有删除diff字段或修改原日志来获得通过。

## 下一步边界

[现有交接](NATIVE_RUN_ACT2_LIFECYCLE_NEXT_HANDOFF.md)可继续执行：以实际伤害、格挡、药水、构筑和公开地图数据改善合法策略，保留预登记失败样本和策略版本，取得第二幕精英/Boss胜利、原生奖励及真实第二次跨幕证据。新增Elite完成分支当前没有实际胜利样本；Monster限制已放开并不自动证明其成功分支可用。

商店、嵌套事件与其他Ancient的通用组合、默认多幕至Architect的正常开局RunVictory、必要数据缺失门与最终同构建全量验收继续待办。死亡用例仍一run一进程；不承诺零售run-loss清理或同进程复用。Core98本轮复用既有同源码/产物证据，未重跑。当前无环境或权限阻塞，无生产实现、角色、安装或真实存档改动，无UI、真实Steam/网络调用，无提交推送。
