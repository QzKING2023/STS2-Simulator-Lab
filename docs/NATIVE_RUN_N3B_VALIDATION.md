# M1 步骤 4B：房间控制器、休息、简单事件及自然片段

日期：2026-11-02。依据[4B交接](NATIVE_RUN_N3B_IMPLEMENTATION_HANDOFF.md)。**4B实现及最终同构建回归已通过，待独立验收。** 4C交接已准备；4C–4D、步骤5–6及完整M1仍待完成。

## 冻结与主要结果

- Build ID：`AF641CFD85515D90898375F3C2C75E5A77CEB89F8D349E1D33F370F475587A16`。
- NativeRun DLL SHA256：`FE6D4810A3933B03D7955F9276377D90A8266850C88280162D4B45D458E382B4`。
- Manifest：`.build/native-build/n3b-final01/build-manifest.json`；42个source、25个compiled、3个原版inputs、7个artifacts逐项hash匹配，Build ID重算一致。
- HEAD仍为`d73039d688eb54c9513f5b9508a03947e689aa18`，实际dirty源码和产物纳入冻结；最终编译0 warnings／0 errors。
- 总汇总：`.build/runs/n3b-work01/summary.json`；额外内容核对：`inspection.json`，497项通过。

固定v0.111.0／41cef1ea、Ironclad/A0、独立内存档案、一run一进程。原生执行为NotCompared，未引入Mod或CombatSolver。

| 预登记自然种子 | 实际执行与结果 | 覆盖及限制 |
| --- | --- | --- |
| DEV_01 | 71/71根动作，Ongoing／SliceBoundary；新进程重放、minimal、off均一致 | 同一次正常Neow开局，7个房间、3场真实Victory、SunkenStatue与DenseVegetation原生事件、Rest指定实例升级，进入随后战斗Play；HP50、金币468；升级永久实例i2与战斗副本DeckVersion关联 |
| DEV_02 | 138/138，真实Death／Completed；新进程重放一致 | 保留Neow原生连续两张卡奖励、实际战斗及敌方致死；HP0、金币152。Death是正确执行结果，不补足该种子的休息／目标片段覆盖 |
| DEV_03 | 77已确认／78已派发，Unknown／Unsupported，exit2 | 真实进入WhisperingHollow后在未审查入口停止；保留已发生前缀和未确认Travel。不是自然片段完成；收尾状态HP56、金币142也不是新的已确认边界 |

种子完整名为`LAB_V0111_NATIVE_DEV_01/02/03`，运行前登记于`.build/runs/n3b-work01/registration.json`。未换种子或删除失败。DEV_01已包含事件和休息两类非战斗房间，但商店、宝箱、药水、精英／Boss等4C／4D内容未因此验收。没有正常开局完整通关或胜率结论。

## 实现与契约

新增`NativeRooms.cs`提供内部有限房间循环、原生候选读取、票据、选择、退出及夹具。自然路线只依据公开地图寻找Rest，沿真实Children调用EnterMapCoord；Unknown节点仍由原生抽取。战斗沿用实际原生执行，策略保持简单合法；没有改HP、奖励、牌组或RNG来保证自然成功。

生命周期“采集开关”与“场景验证器”分开：新4B启用生命周期／事件RNG及room快照，旧4A专用TerminalContract仍只验证其原场景；4B使用新的`NativeRoomContract`。旧N2／4A入口、必需state、选择和完整RNG门保留。

事件顺序明确为：room-entered → room-candidates → 根动作内option-start／原生return → 确认及option-boundary → **room-leave-ready** → 原生Exit开始／成功返回 → 根确认 → **room-complete**。休息的显式离开可以放弃剩余选项，但只有原生BeforeLocalRestSiteExited、AfterAllRestSitesCompleted及Exit返回后才发完成事件。自然Travel拥有原生Exit，未额外调用一次Exit。

根选项票据绑定room对象、请求序号、完整状态和答复；执行前检查index、enabled、真实实例及状态。策略可见候选和oracle完整状态分开记录，原生合法性与当前adapter支持能力分别保留。普通事件／Rest效果中的卡选择沿用现有子请求树，不启动嵌套平级root。

生产room契约独立校验：room／事件身份、map generation及stack；候选顺序／handle／实例／enabled；request到dispatch的完整状态；原生开始／返回／确认及session关联；静止观察与不可变确认快照；取消结果与实际不变状态；每房间胜利／奖励／退出顺序；自然升级永久实例与后续战斗副本。缺失、重复、提前完成或状态漂移均不能靠`valid=true`通过。

## 原生能力与独立规则

| 交接项 | 已执行结果 |
| --- | --- |
| B01 Rest入房／读取 | 使用原生EnterRoomDebug局部夹具或自然EnterMapCoord，BeginRestSite生成选项；重复入房／旧session拒绝，观察对照完整RNG一致 |
| B02 Heal／上限 | 明示夹具maxHP80、HP40→64；HP75→80；执行原生Heal及后续Hook／当前空附加奖励链 |
| B03 Smith／身份 | 原生FromDeckForUpgrade选择重复牌中的非0实例，仅该永久对象升级；完整deck身份／模型／等级核对；自然后续副本不要求首手出现 |
| B04 Cancel／重试 | 原生Cancelable空答复返回false，完整状态不变，选项仍在；随后真实选牌升级成功 |
| B05 离开／非法答复 | 负index、旧状态、重复票据、多余字段、disabled、未完成事件离开均在新增派发前拒绝；原生Rest Exit实际完成 |
| B06 普通事件非首项 | AbyssalBaths ABSTAIN，真实治疗10，HP40→50并完成事件 |
| B07 多阶段事件 | IMMERSE原生MaxHP+2、治疗2再伤害3，HP40→39、maxHP80→82；第一步不结束事件；随后EXIT_BATHS才可离开 |
| B08 子选择／错误前缀 | 非法升级子答复保留3/4前缀；共享N2缺失／多余／重复／过期及冻结选择回归保留。内部脚本和重放接口已覆盖，不声称新的外部交互SDK |
| B09 故障／预算 | 真实治疗发生后注入task故障仍EngineError，3/4且HP50；未完成事件离开4/4 InvalidAction；边界预算3/3 BudgetExceeded。预算负控为声明注入，不冒称等满10分钟 |
| B10 生产数据负控 | 21损坏副本全部拒绝，2等价副本通过，见下节 |
| B11 重放／观察 | 七个正夹具各真实执行＋新进程重放；Heal／Phases及自然DEV_01做minimal／off；完整route、choices、final-state逐文件hash相等 |
| B12 自然片段 | DEV_01完成目标；DEV_02 Death、DEV_03 Unsupported单列，不宣称全部种子完成 |

夹具均来自正常Neow后的原生debug/API局部房间，受伤由声明的CreatureCmd.Damage建立，明确标api-fixture。固定数值规则来自已审查原生实现／常量，不由运行后值倒推。没有把夹具拼接成自然路线。

新增普通事件范围为AbyssalBaths、SunkenStatue的DIVE_INTO_WATER、DenseVegetation的TRUDGE_ON。后两者为DEV_01实际可达性所需的窄原生分支：取得原生金币、执行实际伤害后结束。SunkenStatue遗物分支与DenseVegetation事件战斗分支仍显示为合法但当前不支持，选择时显式Unsupported。其它事件模型不外推支持。

三个已接通事件均是密封类型且继承基类无效果OnRoomEnter，运行时检查DeclaringType后调用原方法。原生EventRoom.EnterInternal已执行AfterEventStarted；这个有限配置不声称适用于有自定义OnRoomEnter的事件，也不声称无UI视觉分支与零售演出RNG等价。

## 过程中修复的真实缺口

1. **升级取消。** 原ICardSelector只收到min/max，丢失原生CardSelectorPrefs.Cancelable；桥现在从真实方法作用域读取prefs，只有Cancelable才允许min=0。其余最少选择门未放宽，N2旧负控全部回归。
2. **Neow连续卡奖励。** DEV_02的真实初始boon连续发两次CardReward，旧桥只在显式战后领奖时确认，且只保留一个pending。现在在精确原生CardReward.OnSelect任务上观察成功返回，保留原候选引用，核实所选对象确实存在于永久deck后才完成对应请求；原方法会移除已选候选，因此不能在返回后再从剩余候选推断。没有手工加牌或假发完成。旧N2行为保持，其手动完成入口仍可回归。
3. **完成时点和错误传播。** 将可离开与原生退出完成分开；未知模型分类前先检查原生fault账本。已有错误不能被新的Unsupported覆盖。

开发证据保留在`n3b-dev01-*`、`n3b-dev02-*`、`n3b-dev03-natural-*`、`n3b-dev04-*`、`n3b-dev05-*`和`n3b-dev06-suite/`；本地`.build/runs/n3b-work01/dev*-summary.json`给出诊断。早期InvalidAction是Lab桥缺口，不算游戏规则缺陷或真实策略失败。最终结论只用final01，未把旧产物结果合并为同构建通过。

## 最终回归与负控

| 范围 | 最终结果 |
| --- | --- |
| 4B | 34个原生子进程、195项检查、0失败；`.build/runs/n3b-final01-suite/summary.json` |
| 4A | 16个原生子进程、273项检查、0失败；`.build/runs/n3b-final01-n3a/summary.json` |
| 步骤1–3／N2 | 74个原生子进程、580项检查、0失败；`.build/runs/n3b-final01-n2/summary.json` |
| Core | 实际98项通过，Core DLL与冻结相同；`n3b-work01/core.log` |
| 内容／版本／退出补充核对 | 497项通过；`n3b-work01/inspection.json` |
| 历史包读取器 | 163次全部符合预期；原652文件hash保持；`n3b-work01/historical-reader/summary.json` |

合计124原生子进程、1,048项suite检查。实际raw exit为80×0、39×1、2×2、3×3，逐进程与expected／process／result及validation交叉核对。预期负控、预算和Unsupported不会由外层exit0掩盖。重复执行是复现证据，不是独立胜率样本。

`room-controls.json`包括：缺room快照／局部事件RNG、错误事件或room身份、错序／重复／缺失原生返回、候选丢失、错误dispatch、取消假成功、事件未结束即离开、确认后新增字段漂移、缺奖励完成、错误升级实例等。尤其`coherent-wrong-heal`把返回、确认及后续快照一致改成HP65，仍被独立固定治疗契约拒绝，未仅依赖“前后彼此相同”。未改动重封装与对象属性顺序等价副本通过。所有控制实际调用冻结生产reader，并重新封装合法链／seal。

4A的80负控／8等价副本、N2的全部104旧腐坏控制保持；历史163包分别来自E261和30C9旧冻结，读取器为本轮AF641，身份分列。旧4A场景及自然skip24／take25的route、choices、完整final-state与原final05基线相同。15个锁定Mod输入、52个兼容源码hash不变；本轮未执行新的Mod适配实验。

## 复现、隔离与限制

~~~powershell
pwsh -NoProfile -File tools/native-run/run-native-n3b.ps1 `
  -BuildManifest .build/native-build/n3b-final01/build-manifest.json `
  -OutputDirectory .build/runs/local-n3b-check
~~~

输出目录须不存在。旧回归入口仍为run-native-n3a.ps1、run-native-n2.ps1，传入同一final01 manifest。实际预算为每片段5000根动作／10分钟、每房间32决策、每战1000动作、单等待不超过45秒及剩余片段时间、512MiB trace；新profile watchdog为12分钟，旧profile仍5分钟。三个最终suite各写独立目录，构建串行完成后执行，无共享可写游戏状态。

MemorySaveStore、shouldSave=false、Steam未初始化、单人服务、无UI及原保存拦截保持。DEV_01实际拦截SaveProgress12次、WriteReplay3次；DEV_02分别11／4，Telemetry均0。计数是被抑制的原生命令调用，不是磁盘写入；未声称做OS级全系统IO审计。没有改角色、Mod、安装、真实存档或Steam Cloud，没有启动游戏UI／真实Steam／游戏网络，没有提交推送。

改动限于8个既有native-run文件和4个新增实现／脚本：`NativeRooms.cs`、`NativeRoomContract.cs`、`NativeRoomControls.cs`、`run-native-n3b.ps1`；另新增本文与[4C交接](NATIVE_RUN_N3C_IMPLEMENTATION_HANDOFF.md)。既有dirty工作保留。

仍未支持任意事件／任意奖励／药水／商店／精英Boss／完整幕／整局；新增卡奖励桥仅验证当前实际原候选→永久对象路径，不外推所有替换Hook。死亡进程不复用。非空Rest额外奖励、奖励alternative等广义交互留4C；未验证内容不能用本轮结果推定支持。当前无权限或执行阻塞，交独立验收后接续4C。
