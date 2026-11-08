# 原生 N2 数据契约与查询入口

步骤4A的有限终态／尾部扩展见[N3A验证](../native-run/NATIVE_RUN_N3A_VALIDATION.md)。原N2的20种必需state、16种metadata及自然切片门保留；新场景增加12种必需state事件（loss、OnEnded、WinRun、act-transition的开始／完成、terminal stamp/contract、tail candidates/contract）和3种metadata（lifecycle-fault、platform-offline-guard、architect-presentation）。当前EventModel的独立RNG与原生终态完成顺序进入新场景投影；Death不承诺TestMode省略的零售loss收尾，尾部RunVictory为局部生命周期fixture，不能当自然完整run。

该契约用于固定 v0.111.0 的原版首战切片及 N2 明列 API 夹具。所有实际执行为 native-only／NotCompared；数据完整性只针对声明的投影，不能推出完整 run、全部触发来源或仿真忠实度通过。

## 文件与信息权限

| 文件 | 内容／权限 |
| --- | --- |
| build-manifest.json、profile.json、process.json | 版本、实际输入／源码／产物、独立实验档案、子进程真实退出／峰值内存／起止时刻；审计资料 |
| trace.jsonl、trace-seal.json | 主线程追加事件、序号／前链／当前hash、最终条数／字节／尾hash／完整性声明；oracle及诊断资料，不直接给玩家可见策略 |
| public-requests.json | 根策略实际收到的白名单候选：本次handle、kind、可见model；不含原生对象、private instance ID、draw顺序、seed、RNG或状态stamp |
| choices.json | 实际子选择请求和答复，包括公开候选handle／model／upgrade／bundle内容，以及私有context／stamp。整个文件属于oracle重放资料，不能当作公开策略输入 |
| route.json、final-state.json | 已确认根动作前缀、前后投影hash，以及最后实际状态；包括隐藏pile顺序与RNG，属于oracle资料 |
| events.json | 保留的兼容观察格式，包含结构化状态；正式完整性门使用JSONL，不以该文件单独证明完整性 |
| result.json、trace-review.json | 原生终态／停止原因／数据完整性，及从原始事件重算的计数和类型化效果汇总 |
| timing.json | 初始化、原生await/pump、身份／状态投影、choice处理、JSONL序列化／落盘、兼容JSON写入的累计墙钟，另有退出前读到的进程累计峰值working set。阶段互相嵌套，不能相加当总耗时；内存读数之后的退出尾部不在该测量点内 |

`off`／`minimal` 关闭可选详细历史事件，保留身份基础设施、执行守卫及必要状态投影；二者当前行为相同，分别保留配置名供后续演进。它们用于不扰动对照，不能提供详细模式相同的效果统计，也不表示彻底关闭全部观察。默认验收和数据分析使用 `detailed`。

## 事件、身份和来源

每行包含 `seq`、`prev`、`payload`、`hash`。payload schema 为 `native-event/v1`，记录kind、root、context、detail；hash由前链及规范序列化payload计算。footer计数由检查器从原始记录重新计算。自然路径要求指定生命周期顺序；fixture要求真实断言及fixture-contract，不把fixture当自然run。

N2审查修复版另外验证实际内容与关联：combat-history带epoch/index，原生Clear和最终history-checkpoint提供真实条数；详细模式不得缺失/重复/乱序。普通选择必须闭合到method return；原生卡奖励以choice-reward-completed闭合后才能确认根动作。fixture按登记场景验证必要断言集合和valid=true。详见[修复验证](../native-run/NATIVE_RUN_N2_REVIEW_REPAIRS.md)。旧final06的完整性门存在已记录缺口，不能仅凭其passed标记获得同样保证。

最终[快照存在性修复](../native-run/NATIVE_RUN_N2_SNAPSHOT_REPAIRS.md)将36类事件明确分为20类必须带state的观察事件与16类无需state的元数据。必需state先验证存在、非null及object类型，再校验字段/RNG；缺整个state不能绕过内容门。C166修复版尚有此遗漏，不能将其旧passed标记等同于最终存在性校验通过。death等schema分支列入契约不代表本轮执行了真实死亡流程。

场景元数据将来源与执行方式独立记录：ScenarioKind为natural-run/api-fixture/fault-control，ExecutionKind为live/frozen-replay/observation-control，并保留Replay、HostControl、InputControl。header/result/process须与有限登记表一致。夹具重放保留api-fixture来源，损坏输入保留fault-control来源；重放本身不产生自然样本。

`identity`先定义实例，后续使用 `{"$ref":"i..."}` 关联。相同卡名的不同实例不合并；永久卡与战斗副本通过DeckVersion关联，生成对象另有身份。身份注册在所有观察模式中都执行；编号是本次执行的审计句柄，不能用它来推断玩家可见信息。

choice-requested中的oracleOptions保存公开候选handle对应的私有代表对象和整组引用；reader校验resolved和returned的真实对象/顺序，包括共享首对象的Bundle。它属于oracle文件，不增加到公开策略DTO中。

以下是缩略格式说明，不是可直接重放的完整记录：

~~~json
{
  "schema": "native-event/v1",
  "kind": "combat-history",
  "root": "a5",
  "context": "act:node:room-instance",
  "detail": {
    "facts": {
      "type": "DamageReceivedEntry",
      "stage": "native-history-before-after-hook",
      "round": 1,
      "side": "Player",
      "facts": {
        "Receiver": {"$ref": "receiver-instance"},
        "CardSource": {"reference": {"$ref": "card-instance"}},
        "Result": {"BlockedDamage": 0, "UnblockedDamage": 9, "OverkillDamage": 0}
      }
    },
    "state": {"hp": 80, "gold": 249}
  }
}
~~~

DamageReceived、BlockGained、EnergySpent、卡牌开始／结束／抽取／弃置／消耗／生成、MonsterMove、Power等取自即时原生CombatHistory事实。状态在观察点复制，历史清理单独记录；不把清理后空History作为有效效果证明。原生history多在相应After Hook之前通知，这个阶段标签不能理解为整个动作已经结束。

记录保留实际CardSource／dealer／receiver、CardPlay、目标与原生结果中可得来源；没有具体遗物／power触发链时保留缺失原因。父root只说明发生于该动作执行期间，不能冒充精确效果触发来源。复杂原生类型若在当前投影之外，会写 `unavailable` 和reason；查询不得把这些占位符当作完整数据。

## 三个可复现查询

1. **为什么掉血或停止？** 先看result的终态、Stop、Confirmed／Dispatched、Error；再按root关联dispatch、类型化history与confirmed。`unblocked`包含所有目标且可能含overkill，不能直接当玩家HP损失。实际HP变化、复活和战后回血需结合前后状态；非确认根动作保留真实已发生效果。
2. **奖励如何进入下一战？** 关联reward-offer、choice-requested／resolved、card-reward-acquired与下一Play快照。检查选中实例的永久deck身份、升级和战斗副本的DeckVersion，而不是只比较牌组数量。skip路径仍原生领取金币，再合法跳过剩余奖励。
3. **能否重现及成本在哪里？** 使用同构建、同档案、同seed与冻结route＋choices，在新进程执行；比较每步private stamp、完整最终投影及实际RNG字段。timing和process提供阶段成本／进程内存，日志体积由seal与文件大小核对。重复同seed属于复现，不是独立策略样本。

离线原始记录检查并重算汇总：

~~~powershell
dotnet <frozen-build>/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review <run-directory>
Get-Content <run-directory>/trace-review.json
~~~

检查器拒绝未知schema、序号缺失／重复／乱序、损坏hash、悬空引用、未完成请求／任务／根动作、缺生命周期、内层fault、空RNG、截断文件、矛盾退出和不完整seal。哈希链证明文件未发生未解释的改变，不是数字签名或对恶意重写整套证据的防护；真实来源仍由冻结构建、输入锁与原生执行证据共同约束。

## RNG范围和历史修正

N2显式启用字段序列化，run／player／event synchronizer的SerializableRng现在包含counter和内部状态字段。原N0/N1构建使用的JSON默认配置遗漏这些字段，使部分RNG值写成空对象；原来的同seed动作复现成立，但“完整RNG状态已比较”的表述超出了那些文件能证明的范围。旧产物保留，N2以新构建重新验证该自然路径，并在生产完整性门中拒绝空RNG。

当前没有完整盘点所有后续事件、所有同步器、全部敌人专用随机流，也不支持任意中途checkpoint恢复。未覆盖流必须在扩房间时继续补充，不能从本轮三个已读类别推导整个游戏的RNG完备性。

## 4C：奖励集合、宝箱与药水协议

4C扩展保留上述N2、4A和4B独立回归profile，结果见[N3C验证](../native-run/NATIVE_RUN_N3C_VALIDATION.md)。新profile为`4C-native-collections/v1`；原生无Mod/solver，根任务等待真实业务与其子选择结束。Neow中的奖励也由同一提供器处理。新增根动作的计数单位是完整任务；子操作和原生效果分别记录，不能把根动作数当成全部效果数。

`reward-choices.json`保存4C实际请求和答复，属于oracle重放文件：包含request/root/context、候选实例、state stamp及选中handle。其公开投影写入`public-requests.json`，只含handle、类型、可见model/amount/upgrade；不传递私有实例、stamp、RNG或隐藏牌堆。与`route.json`、普通`choices.json`一起在新进程重放，并比较完整状态与已盘点RNG。

| 必需state事件 | 责任 |
| --- | --- |
| `n3c-open` | 唯一操作ID、父操作或根、kind、真实subject、完整候选、请求stamp；根开始与当前dispatch完整相等 |
| `n3c-answer` | 当前请求唯一合法答复，候选handle与实例绑定；快照与请求一致 |
| `n3c-start` | 答复已消费后开始（无选择任务明确options=null），必需且唯一，与请求完整状态一致 |
| `n3c-return` | 真实任务返回、取得／消耗实例和结果；子操作先闭合，不能提前确认根 |
| `n3c-boundary` | 根确认后的唯一结果边界，绑定同一操作、返回和confirmation完整状态 |
| `n3c-set` | 原生集合ID/实例、disallowSkip、全候选顺序、类型、金额和内容实例；集合ID唯一 |
| `n3c-set-completed` | 当前集合已实际全部领取或合法跳过；ID、请求及最后确认子结果绑定 |
| `n3c-card-continuity` | 真实取得的永久卡与下一战任意牌堆的DeckVersion对应；不强制首手 |
| `n3c-assertion` | 明确API夹具的独立数值／结构要求及初始状态来源 |
| `n3c-contract` | 场景规定任务、请求和必需边界闭合后的有限完成证明 |

10类事件的state均要求存在、非null且为object。静止观察点比较完整结构，包含后续新增字段；统一等价扩展和对象属性重排可通过，局部漂移拒绝。原生任务中间的合法资源变化保留在其执行区间，不以静止比较抹除。

新state中的`inventory`补充真实`PotionSlots`：capacity、CanUseOrRemove、每个slot的index、potion引用/model/queued。原`potions`是原生紧凑枚举，不能当作固定槽号；检查器核对二者对应。满槽失败、原生discard、重试取得分别记录，未真实取得不能算领取成功。药水使用通过原生入队/UsePotionAction/OnUseWrapper执行，结果关联确切消耗实例与目标，故障后已发生消费和效果保留。

宝箱复用原生Enter触发的候选生成，分别记录normal、extra、原生投票分配、mutable遗物取得和Exit关闭。`RelicsAwarded`分配通知不等于取得；只有真实`RelicCmd.Obtain`和Hook完成后记录库存结果。来源为专用夹具时保持api-fixture，不进入自然完成率。

当前只对已声明4C场景认证。原生生成的卡奖励替代选项进入菜单，Skip已实际覆盖；同一次OnSelect内重选／reroll及卡取得替换实例的额外适配仍明确Unsupported。数据没有补造完整触发因果图或所有随机流，也不表示公开Mod接口、全部节点或自然完整run已通过。

### 4C审查修复后的必需结果与状态关系

本内部profile的最新冻结和严格reader见[审查修复](../native-run/NATIVE_RUN_N3C_REVIEW_REPAIRS.md)。旧4C候选保留其原构建与未通过审查状态；新增必需结果字段不提供旧4C包的兼容承诺，旧N2/4A/4B仍分别回归。

操作kind有有限父子关系，父start先于子open；CardReward的每次尝试必须有完整menu，集合skip／empty set则无需menu。根的`completed`和set的`complete`必须存在且为true。卡menu的`success`按原生选卡／alternative AfterSelected解释，并与外层item一致；选Skip alternative返回false是合法结果。

集合中下一item的open/answer/start须绑定集合声明或最后子返回的不可变完整状态；set-completed须绑定其后set return。业务执行区间的状态变化保留，静止点的未知新增字段也参与比较。

card-menu结果新增`sourceCards`（真实候选reference/model/upgrade）、`sourceAlternatives`（reference/option/afterSelected）、`acquiredCard`（实际后态，未取卡为null）和明确`identityMode`。当前认证same-instance；真实替换／拒绝取得不能写成成功新增原对象。treasure-obtain记录canonical、created、acquired、model及identityMode，原库存中已有对象不能冒充新mutable取得。treasure-normal记录原生gold返回、actualGoldDelta与accounting；本轮仅认证native-unmodified，修改金额账本仍须后续原版覆盖。

后续[静止边界与取得结果修复](../native-run/NATIVE_RUN_N3C_BOUNDARY_REPAIRS.md)统一列出当前[全部父子生命周期](../native-run/NATIVE_RUN_N3C_LIFECYCLE_CONTRACT.md)。UsePotion及TreasureOpen/Pick/Exit的直接子入口和尾部均绑定不可变完整状态；实际业务任务内部的变化仍按表保留。CardReward外层item的`acquired`现在从实际原生OnSelect完成的子菜单取得结果消费一次，必须与本次card-menu的引用相同；未取得为null。GoldReward不得记录对象取得。旧4C外层固定null的布局不自动兼容，不能以旧包的布局拒绝替代本次反例复验。
