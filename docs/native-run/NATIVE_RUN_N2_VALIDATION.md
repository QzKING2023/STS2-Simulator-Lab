# M1 步骤3：N2 选择与数据可信门

日期：2026-11-02。对应 [N2 交接](NATIVE_RUN_N2_IMPLEMENTATION_HANDOFF.md)。本文保存final06的本地结果；其后[独立审查](NATIVE_RUN_N2_REVIEW.md)指出完整性与来源标签两项必修。**现已完成修复并以新构建复跑，见[审查修复验证](NATIVE_RUN_N2_REVIEW_REPAIRS.md)，待独立复审。** 下文74个子进程、248项检查和92项补充核对为旧final06证据，Core98项通过；不替代修复版的74进程/379检查/114补充核对。计数不相加当独立场景，整个新M1仍未完成。

## 冻结与复现

- 最终构建：`.build/native-build/n2-final06/build-manifest.json`。
- Build ID：`FC9ECD0B89FE31A7FD3635DEAAF1369D835BD77A050FE206169672B376D8E9CD`。
- NativeRun DLL：`25DC2234C1F77B2F131A726D1DE910EB28B860FD2B657242997CAAD7FD913B68`。
- Core DLL：`32B54F0DFA32AD3A3607F16B666B869E41A6C17C3AB44035E0A12B1B7B622DF3`，共享 Core 源码／输出未改。
- 30条 source、15条 compiled、3个原版输入、7个输出；构建0 warning／0 error。HEAD仍为 `d73039d688eb54c9513f5b9508a03947e689aa18`；使用的dirty源码由manifest逐文件绑定。
- 最终矩阵：`.build/runs/n2-final06-suite/summary.json`；候选／旧封版产物保留，不合入最终计数。
- 独立补充核对入口：`.build/runs/n2-final06-inspection/inspection.json`，包括最终hash、真实子进程退出、候选查询不变、公开DTO、奖励实例、Core与保留检查。

~~~powershell
pwsh -NoProfile -File tools/native-run/build-native-run.ps1 `
  -GamePath <legal-local-game-directory> -HostSource <reviewed-local-host-source> `
  -OutputRoot <fresh-Lab-build-directory>
pwsh -NoProfile -File tools/native-run/run-native-n2.ps1 `
  -BuildManifest <build-manifest> -OutputDirectory <fresh-Lab-runs-directory>
~~~

单场入口仍为 `run-native-slice.ps1`，新增 `-Scenario` 和 `-Observation off|minimal|detailed`；冻结重放使用 `-ReplayPath <route.json>`，并强制读取同目录 `choices.json`。缺文件、未知字段／过期答复不能用默认首项替代。原始记录可由同一DLL的 `--review <run-directory>` 独立检查并重算汇总。见 [数据契约与查询示例](../architecture/NATIVE_RUN_DATA_CONTRACT.md)。

## 实现的实际边界

独立原版宿主仍不加载 MyMod／BaseLib／Ritsu／CombatSolver。原版 v0.111.0／41cef1ea、Ironclad／A0／Standard、已锁定独立内存档案和DEV01种子不变。所有执行为native-only／NotCompared；没有缩减旧差分字段或给模拟器虚构结果。

根动作的简单策略与冻结重放均从当前合法候选选择，重放可以选择实际候选中的指定动作。普通卡牌选择使用原生自动selector入口；原方法负责过滤、implicit／empty处理、原生日志及后续牌效。支持的窄入口为ChooseACard、SimpleGrid、CombatPile、Hand／HandForDiscard／HandForUpgrade、DeckForUpgrade／DeckGeneric，以及Bundle；CardReward通过原生无UI reward selector路径接入。其他From*方法明确Unsupported，未扩公开SDK。

当前根候选仍按baseline阶段和动作类别组织，例如有可打牌时优先提供PlayCard候选；尚未提供覆盖全部合法根动作的任意计划语言。该限制属于策略／动作空间覆盖，不影响已记录选择实际驱动原生命令的事实。

Bundle仅替换固定async状态机中TestMode默认首组的单一索引／get_Item位置，检查唯一匹配并登记原ILhash、MVID、token和handler。原生ending／空集合守卫、ReserveChoiceId和日志保留；实际非0选择及“两个组共享同一首卡对象”均验证，返回整组原对象。空的组内选项仍明示Unsupported；ending／空外层集合守卫以原IL保留审查为依据，没有声称本轮另外跑了所有终局组合。

身份按真实对象引用注册，永久卡、战斗副本、生成对象不因同名合并；详细观察开关不控制注册。原生有choiceId时记录，selector自动路径本来不保留原生choiceId时写null，不补造网络消息。请求／响应、原生任务和根动作分别关联与计数；source方法、root及实例图可追溯。当前是单run单进程的内部协议，未承诺外部会话／SDK兼容。

## 验收矩阵与证据

最终队列包含20组正例各执行及冻结重放（40进程）、23个实际运行负控、4个保留宿主控制、4个观察模式重放及3个损坏输入重放，共74个子进程。另有17种隔离日志／结果腐坏控制和原始数据检查；总计248项检查。负控的非零原始退出属于预期拒绝，不能改记为自然内容失败或全体exit0。

| 门 | 实际场景／验证 |
| --- | --- |
| C01／C02 | grid、hand、choose-card、multi；指定非0同名实例、多选顺序；duplicate／too-few／too-many／非法候选；选择输入错误的raw exit1和InvalidAction保留 |
| C03 | empty、single、manual-single；原生合法空集合、自动单选与必须确认的单选区分；softlock控制经原生ReportSoftlock得到EngineError，未当合法空选择 |
| C04 | sequential同根连续两次请求，子请求有root父关联；missing／extra／wrong-request／stale／cross-room／reused／unknown-field分别拒绝。重复答复在真实消费者的已消费集合中拒绝；没有宣称任意递归深度内容覆盖 |
| C05 | bundle／bundle-shared及非法／缺失／额外组答复；实际非0组返回正确引用，原生ReserveChoiceId恰好1次。原TestMode分支没有Sync消息，不称零售网络同步验证 |
| C06 | 原生Armaments经TryManualPlay／ActionExecutor；格挡5、支付1能量、仅升级指定非0战斗实例，永久牌组完整内容／升级／引用不变；非法子选择保留block5、energy2、4 confirmed／5 dispatched和0合法choice提交 |
| C07 | 原生Headbutt，实际9点HP下降后指定弃牌对象放draw top；多／单／空弃牌分别覆盖。未覆盖Headbutt升级，不从该夹具推广全部卡牌 |
| C08 | deck-upgrade／deck-generic；原生候选过滤、非0永久实例、原生Upgrade及引用连续。未实现休息室／移除／变牌整个房间流程 |
| C09 | 同DEV01自然skip与take链各实际执行及重放；Neow→首战Victory→原生Gold→奖励→下一战Play。skip为24动作／deck10；take为25动作／deck11，卡选项序号1实际领取Pommel Strike，永久实例和下一战DeckVersion关联 |
| C10 | EventOption.Chosen真实调用链的任务故障注入；原生效果已发生但故障门仍以EngineError终止，保留1 confirmed／2 dispatched、gold249；不冒充自然Neow缺陷 |
| D01 | 类型化DamageResult、Block、Energy、CardPlay／Draw／Discard／Exhaust／Generated、MonsterMove、Power及原生清理前后记录；原始→汇总由production reader重算，引用可解析 |
| D02 | gap、repeat、truncate、wronghash、dangling、missing-response、cleanup、emptyrng、inner-fault、wrong-exit、header／seal／result schema、未知result outcome、unknown-event、missing-state-field、missing-rng-field。语义控制重建chain／footer，证明拒绝来自实际语义门而非仅hash变化 |
| D03 | skip自然链及Armaments夹具的off／minimal／detailed：实际route、choices、完整最终投影逐字节相同，含RNG；baseline-first与provider-first在同一原生API下最终状态相同 |
| D04 | sink IOException及小JSONL配额控制；DataError、有效已确认前缀和当前未确认动作保留，seal不完整，未伪造成功footer |
| D05 | 公开根策略DTO白名单只有Handle／Kind／Model；私有RNG／draw／stamp留在oracle资料。自然skip／take各21次候选查询的前后完整投影相同；反映实际覆盖查询，不是所有原生getter的普遍证明 |
| G01 | 同一最终构建的74进程和全部生产检查器；Core98项及最终Core依赖hash相同；旧原版／Eleanor兼容回归按独立旧构建身份列出 |

正例中的牌组API和Armaments／Headbutt等是明确的api-fixture：在自然Neow或首战Play后，用原生CreateCard／Add构造测试布局，既不修改永久牌组，也不冒充自然获得这些牌。baseline-first是独立自动selector对照；其“implicit”表示broker未索取显式答复，区别于single场景的原生自动单候选规则。自然C09没有插牌、改HP／金币或强迫抽中被领奖卡。

## 数据修复、隔离与回归

本轮发现并修复了旧JSON配置未序列化SerializableRng字段的问题。旧N0/N1的动作复现仍有证据，部分RNG值为空对象，不能继续当完整RNG比对。新构建显式输出run／player／event synchronizer的counter及状态字；生产reader对空对象、空inventory、整段缺失及缺counter／状态字均拒绝。旧报告与原始证据保留，修正说明见数据契约。

原生日志采用JSONL、逐条序号／hash链和seal，结果独立使用Core原生结果契约验证；完整性失败不以JSON存在或外层成功替代。兼容events.json仍保留，因此性能数据中的compatibility-json-write是实际成本。JSONL配额目前约束正式trace，兼容镜像文件另计；扩大run长度前应统一总存储配额并减少整数组重写。

继续TestMode、MemorySaveStore、shouldSave=false、Steam未初始化、无Daily、无NGame实例、单人本地服务，SaveProgress／WriteReplay／遥测拦截和完整Harmony inventory稳定性检查。UI单例／网络服务类型现已从“只记录”升级为主动守卫。没有启动真实游戏界面、部署或修改角色／游戏安装／用户存档。

Core98项重跑，Core源码没有改，最终Core DLL与检查程序依赖同为32B54F…DF3。保留回归在 `.build/runs/n2-final03-regressions/`：旧harness SHA `9EBF76E26E467D763545F374E0AACE73D3F70104BD59BE8EC8AAE568D7951452`，原版battle×2／RNG×2均通过；Eleanor实际16步Victory、122检查0失败。其构建身份独立，不能算作最新NativeRun二进制的测试；本轮没有共享Core或兼容实现改动，因此保留这些范围的回归证据即可，不扩Mod适配。

## 保留限制与交接

- 默认DEV01是开发样本；重复是复现，不是胜率样本。未执行预登记评估集，不宣称完整爬塔或整角色支持。
- 原生历史投影仍有显式unavailable／reason；没有完整遗物／power触发因果图、所有事件／同步器RNG或任意checkpoint恢复。来源字段与前后状态按当前支持内容解释。
- off／minimal仍做必要身份／状态工作，当前二者行为相同；对照只证明关闭详细记录在所测投影中不扰动，不能推广成全部观察逻辑的数学证明。
- Bundle原IL审查及软锁控制不能代替所有终局组合；其他选择方法仍明确Unsupported。
- 内部非法答复通过失败结果、未决请求及有效前缀保留；尚未提供外部交互SDK。预算／故障注入与自然错误分列。真实原生Death／取消／watchdog及完整后期生命周期留后续门。
- N1-T仍需在广泛N3扩房间前执行。下一步由Astra独立复审N2，然后按既定授权继续N1-T／N3房间与多节点；不重新扩Mod专项。

开发期编译／运行失败及候选构建均保留在 `.build/native-build/n2-dev*`、`n2-candidate01`、`n2-final01..05` 和对应runs／日志中；最终计数仅来自final06。所有原始大型产物及本机游戏静态参考继续留本地忽略目录，不进入远程。没有提交／推送。
