# 4D3：合法准备、固定精英/Boss胜利及实际跨幕

状态：本轮新场景已实际完成精英、Boss胜利与对应原生奖励；Boss成功路径通过原生ready进入下一幕。最终统一回归结果以`.build/runs/n3d3-preparation-work01/closeout.json`为准，交独立审查；本报告不关闭整个4D3或M1。

冻结`FAEC900A695EEDA64586342FB70DF2F118F5B4A941296BA34BF714B064CB423C`，Native DLL SHA256 `3752953ADF7580AC301646D3478B5EE4DA23D0A992773C7E14D9330E34583E0C`。固定游戏、15个输入与52份兼容源码沿用已审基线。

## 实际完成的路径

| 场景 | 来源 | 最终证据 |
| --- | --- | --- |
| `elite-03` | 原生Neow开局，合法地图上的5场普通战斗、奖励及休息/事件；随后声明`EnterRoomDebug`进入原定精英 | 113/113动作；`BYGONE_EFFIGY_ELITE`真实Victory及奖励；HP18、Gold171、永久牌组16张 |
| `boss-03` | 同一原生run，合法地图上的9场普通战斗、奖励、休息/事件及宝箱；随后声明原定Boss夹具 | 184/184动作；`CEREMONIAL_BEAST_BOSS`真实Victory及奖励；原生ActReady/EnterNextAct完成，act1/MapRoom/mapGeneration2；HP17、Gold344、永久牌组20张 |
| `boss-01`、`boss-02` | 同一预设准备策略的另外两个开发种子 | 分别93/93、156/156动作，在普通战斗中真实Death，均未到目标Boss；失败样本未替换 |
| `natural-01` | 正常地图的原生自然精英接近路径，无Debug跳转 | 77/77动作真实Death；该最终策略的自然精英胜利不作通过声明 |

上述精英和Boss成功路径均明确为`api-fixture`。它们的准备没有手动补HP、金币、能量、牌组或敌人数据；最后的原生Debug入口是公开记录的场景构造，不能将这两条轨迹改标为自然第一幕通关。到达第二幕MapRoom证明实际幕转换任务完成，**第二幕首节点/Ancient选择尚未执行**。

成功、死亡、冻结重放与观察模式对照共14次新原生执行；166项检查和39个生产控制通过。三个Boss开发seed预先固定为DEV01/02/03，另外明确指定Elite DEV03与自然DEV01。重复同种子是复现证据，不能当独立样本或胜率结论。

## 实现与生命周期契约

- `NativePreparation`在host安装前只配置一次，绑定scenario和策略版本；重复配置、运行中scenario漂移均拒绝。有限内部组合复用现有战斗、奖励、休息/升级、Hollow普通奖励和宝箱接口。商店和嵌套事件战斗尚待接入本组合；命中时明确停止，未静默跳过。
- `oracle-legal-preparation/v5`只改变合法候选的排序/选择：公开地图路径成本、当前原生攻击意图、击杀估计、格挡缺口、卡牌/升级候选与已取得药水。状态/RNG在意图及卡牌排序查询前后保持。估计只服务策略，真实伤害、费用、抽牌和奖励始终由原生方法执行；没有预测差分或最优性声明。
- 普通奖励依旧走原生生成/领取与完整子选择。卡牌选择有显式策略优先项，保留原候选顺序和实例，冻结重放消费原答案。休息使用原生同步器；升级通过真实卡选择提供者，不直接改升级字段。
- 宝箱的取得复用原生投票/分配/获得流程。新组合将**唯一一次Exit交由后续原生地图转移执行**，不再手动Exit后又由地图重复Exit。启动时登记`TreasureRoom.Exit`观察包装，等待实际Task返回并检查picking关闭。生产检查器将退出开始完整state绑定到当前派发的合法“节点更新、弹出旧房”阶段，返回完整state绑定该输入，再由确认/实际新房到达闭合。
- 固定目标的Victory/奖励按本次具体room和Encounter计数。准备途中的其他精英胜利不能替代目标Boss胜利；ActReady只允许在目标Boss实际胜利及奖励完成后执行。仍调用原生ready同步器，等待实际EnterNextAct任务及新地图生成。
- 新遇到的药水满槽失败按真实失败结果记录，必须成功=false、无取得对象、完整状态不变；后续丢弃/重试保留原生命令。补入前部空槽时，严格按槽位顺序重建`Player.Potions`期望投影，不再错误地追加到末尾。相关状态字段没有删减。

完整静止状态比较、必需/唯一/顺序、父子归属、跨房历史身份、失败前缀、未知类型/字段与既有审计门均保留。新`prep-*`记录仅允许在注册的准备profile中出现。

## 正负控与安全证据

新39个控制直接对有效重新封装副本调用生产检查器：宝箱退出、ready、act-start/completed及返回的缺失/重复/错归属/完整state变更；退出开始/返回共同改写；关闭事实；配置缺失/冲突；跨幕持久字段矛盾；满槽失败改变无关状态；药水实例顺序矛盾。38个坏副本拒绝，1个结构属性重排等价副本通过。原8个跨房反例、前29个反例、82个阶段控制及其余240控制在最终构建重跑。

新增TreasureRoom.Exit还执行了实际Harmony未知补丁审计API探针：安装前的未知prefix拒绝，后加prefix使生产patch inventory检查拒绝；能力配置重复/漂移拒绝。目标业务调用数为0，不能把这些API夹具计作游戏效果验证。首次探针错误地尝试在未初始化bootstrap的API夹具中安装自有补丁，失败证据保留为`first-patch-probe-*`；修正探针范围后通过，生产代码没有为探针放宽审计。

各实际run均独立进程、MemorySaveStore、shouldSave=false、Steam未初始化、无游戏UI/Mod/CombatSolver。原生SaveProgress/replay入口继续被离线拦截；探针与run输出仅写Lab。没有部署或提交推送。

## 同构建复现与证据

构建使用现有`build-native-run.ps1`及合法本机游戏输入。完成同一构建后执行：

```powershell
python -P tools/native-run/run-native-preparation.py --manifest .build/native-build/n3d3-preparation-final01/build-manifest.json --output .build/runs/<fresh-preparation-directory>
```

输出目录必须全新。现有完整结果在`.build/runs/n3d3-preparation-final01-prepared/`，不要覆盖；`route.json`、`choices.json`、`reward-choices.json`、`final-state.json`、逐步JSONL、结果及实际进程退出均保留。Boss/Elite成功路径的live/replay/minimal/off按完整JSON比较，包含全部RNG；所有死亡样本也保留新进程重放。

同构建适用旧原生矩阵309次执行/3908项检查，加新14次/166项，合计323次/4074项，另有实际Core98。原始退出为240×0、70×1、8×2、5×3；外层成功表示预期结果/拒绝被验证，未将Unsupported、预算停止和注入故障改称胜利。旧Mod本轮仅核对15输入/52兼容源码保持，未增加或重跑Mod专项。

来源快照、开发失败迭代、最终任务日志、严格核对及API探针在`.build/runs/n3d3-preparation-work01/`；最终closeout明确来源/产物/检查计数和限制。历史v3、v4、v5开发构建的不同结局均保留，不能拼成一次run或混入最终构建结论。

## 下一依赖

见[后续交接](NATIVE_RUN_N3D3_PREPARATION_NEXT_HANDOFF.md)。仍需正常开局同一run经自然Boss到下一幕、第二幕首节点/Ancient、通用节点能力组合、完整默认Acts至Architect/RunVictory以及预登记批次。当前有限组合没有替代这些验收门；正式M1保持进行中。
