# 第二幕精英、Boss胜利与再次跨幕

日期：2026-11-03。状态：实现和分层验证完成，待独立审查。正常开局第二幕胜利、原生奖励和第三幕MapRoom已实际到达；完整M1仍未完成。

## 实际流程进度

| 固定样本/路径 | 原生结果 | 确认/派发 | 最远边界 |
| --- | --- | --- | --- |
| DEV01主路径 | Death | 93/93 | 第一幕第8层 |
| DEV02主路径 | Death | 156/156 | 第一幕第7层 |
| DEV03主路径 | SliceBoundary / Ongoing | 367/367 | 第二幕Boss胜利、奖励、原生再次跨幕，第三幕MapRoom |
| DEV03独立精英路线 | SliceBoundary / Ongoing | 318/318 | 第二幕INFESTED_PRISMS_ELITE胜利与奖励 |

主路径从原生Neow正常开局，保持同一个run。第二幕KAISER_CRAB_BOSS在a365实际结束，玩家HP12；a366通过原生奖励同步器领取100金币及新卡IMPERVIOUS实例i777，金币411→511。a367设置本地准备投票并等待原生EnterNextAct真实任务完成，最终act=2、MapRoom、node=null、HP18。ActStarts/ActReturns均为2，WinStarts仍为0。没有将第二幕胜利或MapRoom当成RunVictory。

精英是另一次正常开局的合法地图路线，不与主路径拼接。a317真实战斗胜利时HP32；a318原生领取41金币及MealTicket新实例i689，奖励后HP38、金币356→397。当前样本主动跳过该场卡奖励，没有伪造新增卡。该路线完成自己的有限精英目标，不算一条完整run。

两种子的死亡和之前360/306动作死亡证据保留。固定种子重复用于复现，不是独立胜率样本；本轮针对DEV03数据进行策略开发，不能将结果解释为留出集策略评估。

## 具体变化与失败保留

新增内部`NativeAct2Policy`，仅在Act2家族场景且原生当前幕号大于0时选择策略。原生游戏仍负责合法性、支付、伤害、奖励、选择和状态改变。它按当前牌组降低重复卡及缺少配合的条件能力牌优先级，通过真实Skip选项跳过低优先级奖励，调整可用升级候选优先级，实际使用已持有的ExplosiveAmpoule。被包围时采用明确的固定朝向目标规则；其依据来自原版Surrounded/BackAttack机制，仍是有限启发式，不是最优策略或通用强度结论。

主路径策略版本为`oracle-legal-natural-act2/v7-facing`，精英为`oracle-legal-act2-nearest-elite/v4-facing`。第一幕采用此前规则；主路径前188个完整动作记录与旧360动作样本一致，a189起第二幕决策不同。读取兼容仅允许明确列出的旧策略描述，其他字段仍必须完整精确匹配，不修改旧producerBuildId。

开发失败保存在`.build/runs/act2-strategy-dev01-main`（v5，第一幕Death199）、`dev02-main`（v6，第二幕Death357）及`dev03-main`（v7，Crusher死亡回调空音频接收者，355派发/354确认，EngineError）。没有以更换种子、改HP/资源、重写存档或拼接路径得到成功。失败后按其真实原因修复或迭代，失败仍参与证据重验。

Crusher.BeforeDeath暴露了与Rocket相同的空NAudioManager调用。`NativeBossPresentation`现在逐一审查并安装两份具体回调，各只替换一个PlayOneShot(string,float)接收者调用；原回调、IsOverOrEnding查询及其余分支保留。未知补丁预检查、唯一IL锚点、方法token/MVID/IL哈希和全局补丁清单稳定性继续有效。实际主路径在a355记录Crusher guard、a365记录Rocket guard，各一次；没有替换业务方法体。

两方法均补了实际Harmony注入API负控：未知prefix实际织入后，生产Install拒绝；生产窄transpiler可安装；后加未知prefix被生产Ensure拒绝，移除后恢复原隔离门。目标业务回调执行次数为0。此为隔离审计API夹具，没有初始化真实游戏/Steam或执行战斗，不能冒充业务测试。初次夹具遗漏初始化Harmony owner而失败的JSON也保留；修正的是夹具初始化，未放宽生产门。最终两份证据为`rocket-patch-final04.json`和`crusher-patch-final04.json`。

## 同一冻结与分层验收

最终源码、生产及检查器统一冻结：**`D59D778F3360FBBFF7B65A419725D321F20A7B541FBC2E91CDAF45FDCFC45910`**，`.build/native-build/act2-strategy-dev04/build-manifest.json`。虽目录保留dev04命名，之后无运行代码变化；92项dirty源码输入、56项编译输入、3项游戏输入、7项产物有清单和源码副本绑定。

| 本轮证据 | 实际范围 |
| --- | --- |
| 最终构建15次原生执行 | 固定三种子live/replay、DEV03 minimal/off、旧206动作切片、skip24/take25、精英live/replay、双宝箱烟测及首次367动作主路径 |
| 最终主矩阵 | 11进程、91检查，全部符合各自真实预期；另4次独立执行也符合预期 |
| 完整原始证据重读 | 389份原始记录，保留各producer/原exit；包括历史371份和本轮18份，不把未完成记录当成功 |
| 历史控制重读 | 122项，包含上轮独立反例；与原始证据合计511次当前生产检查器调用 |
| 新生产控制 | 21项：18项损坏全部拒绝，3项正常/等价通过；修改副本重新封签，验证语义门而非仅哈希链 |
| 实际补丁API负控 | Rocket和Crusher分别验证安装前未知patch及安装后patch变化拒绝；目标业务调用0 |

合计532次生产检查器调用，全部符合预期；2652项收尾一致性检查无失败。最终15次原生执行是4次真实Death、11次合法SliceBoundary，不能计为15次通关。此前3次开发执行另列，含1次EngineError。

主路径live/replay/minimal/off比较完整route、choices、reward-choices、final-state及734个派发/确认完整状态；精英比较636个状态，均包含完整RNG内部字。两个正常live主路径进程也得到相同完整结果。观察模式对照未扰动本次输出。新增控制覆盖胜利玩家存活、真实谓词、奖励必需和归属、精英完成、第二次跨幕和MapRoom、音频所有者、原生跳过的取得事实/完整牌组、快照及描述的新增字段。原始失败及正常等价副本均保留。

15项Mod输入和52项兼容源码未变。Core98沿用已审查的相同源码/产物历史证据，未在本轮重跑。分层依据是策略及两处窄音频依赖变化：重跑受影响自然流程、重放及少量原生烟测，完整冻结历史证据由当前检查器重读；没有宣称替代最终M1同构建全量门。

## 证据和未完成项

收尾索引：`.build/runs/act2-strategy-work01/closeout.json`。同目录包含`historical-review-final04/`、`controls-final04/`、`source-final04.json`和逐条检查；固定主矩阵在`.build/runs/act2-strategy-final04-matrix/`。目录内反编译参考只用于核对合法本机依赖，未放入项目公开代码。

复现使用当前冻结清单和全新输出目录，禁止覆盖旧结果：

```powershell
python -P tools/native-run/run-native-act2.py --manifest .build/native-build/act2-strategy-dev04/build-manifest.json --output .build/runs/<fresh-matrix>
pwsh -NoProfile -File tools/native-run/run-native-slice.ps1 -BuildManifest .build/native-build/act2-strategy-dev04/build-manifest.json -OutputDirectory .build/runs/<fresh-elite> -Scenario major-natural-act2-elite -Seed LAB_V0111_NATIVE_DEV_03
```

精英重放将上述新结果的route.json作为ReplayPath，仍使用相同种子和场景；主矩阵已自带三种子重放及DEV03 minimal/off对照。审查仅读取副本时使用冻结DLL的`--review <copy-directory>`，不另启原生run。审计API夹具源码和产物在本轮`patch-probe-final04/`，每个方法必须使用独立进程，避免前一次已安装补丁污染后一次安装前负控。

完整状态比较指契约声明的JSON投影全部字段，尚不等于所有原生对象内部字段。当前特定能力（例如Surrounded.Facing）未有独立类型化投影；实际原生效果及重放已执行，但完整M1必要数据门仍需判断并补足这类对实验有意义的内部状态。仍不承诺零售演出/RNG时序等价或死亡后同进程run复用。

下一步见[交接](NATIVE_RUN_ACT3_IMPLEMENTATION_HANDOFF.md)：组合第三幕入口、现有房间能力、其他Ancient及最终Architect正常路径，完成必要数据与最终全量验收。第四步尚有通用组合缺口，第五步目前只验证到第三幕MapRoom；完整M1和第六步预登记评估未完成。当前无实质环境阻塞；保持无UI、MemorySaveStore、保存/replay拦截、无真实Steam/网络/用户存档，不改角色或安装，不提交推送。
