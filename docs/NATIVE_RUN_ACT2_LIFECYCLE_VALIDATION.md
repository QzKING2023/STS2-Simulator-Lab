# 第二幕精英/Boss首达与重复宝箱生命周期

日期：2026-11-03。状态：本轮实现及分层验证完成，待独立审查。正常开局完整RunVictory、第二幕精英/Boss胜利及第二次跨幕仍未完成。

## 实际进展

从已独审的 [277动作基线](NATIVE_RUN_ACT2_INDEPENDENT_REVIEW.md) 继续，本轮修复第二幕后续战斗被过度限定为Monster的问题。首场仍保留Tezcatara选择和Monster前置；后续允许实际到达地图/房间所对应的Monster、Elite和Boss，胜利、奖励、实例、状态与动作归属校验继续生效。

路线策略 `oracle-legal-natural-act2/v4-route-heal` 使用公开生成地图减少连续普通战斗，并在第二幕HP未满时通过原生休息选项治疗。此前v2、v3-route和各次失败均保留。这是开发种子上的启发式可达性改进，没有总体胜率或最优性结论。

| 当前固定样本 | 原生实际结果 | 确认/派发 | 最远位置 |
| --- | --- | --- | --- |
| DEV01 主路线 | Death | 93/93 | 第一幕第8层 |
| DEV02 主路线 | Death | 156/156 | 第一幕第7层 |
| DEV03 主路线 | Death | 360/360 | 第二幕第16层，KAISER_CRAB_BOSS |
| DEV03 独立精英路线 | Death | 306/306 | 第二幕第10层，INFESTED_PRISMS_ELITE |

DEV03主路线从正常Neow开始，同run有16场原生胜利（第一幕10、第二幕6），随后在第二幕Boss战敌方回合死亡，HP0、Gold411，最后边界EndTurn:360。原生击杀过Rocket，左侧Crusher仍存活；没有将进入Boss战或击杀一侧记成Boss胜利。精英路线使用 `major-natural-act2-elite` 注册入口及公开地图到最近可达精英的有限路线，结果是真实精英死亡路径，不是夹具，也没有改HP或资源。

主路线还实际进入并完成三个不同宝箱：第一幕第10层i183，第二幕第9层i644和第11层i650。打开根分别a90/a285/a290，原生退出完成分别a92/a287/a292。地图转换拥有唯一原生Exit，没有主动退出后再重复退出。

`reward-repeat-treasure` 是另行标注的API夹具：原生EnterRoomDebug创建两个不同TreasureRoom，先领取、再跳过，通过原生管理器下一次房间转换退出，最后进入RestSite而不执行休息。9动作及冻结重放均通过；i19/i22各有自己的原生退出任务，首次取得1遗物、第二次0遗物。它补充跳过与房间隔离覆盖，不计作自然爬塔。

同构建还回归原有第一幕准备夹具：BYGONE_EFFIGY_ELITE真实胜利及奖励113动作；CEREMONIAL_BEAST_BOSS真实胜利、奖励和原生跨幕184动作。它们不证明第二幕相同类别已胜利。

## 原生兼容与数据修复

1. RanwidTheElder、DollRoom、InfestedAutomaton的入口已针对锁定程序集审查。三者继承原生无操作OnRoomEnter，原生BeforeEventStarted/选择/完成仍执行。DollRoom的音频启动原生已有TestMode门；策略选择原生随机选项。其余事件仍显式Unsupported。证据包含金币、遗物、牌组、HP与RNG实际变化，未手动合成效果或全部事件支持。
2. 原生IsCombatEnding以“没有存活主敌人”为胜利条件，召唤物列表可能未清空。Major状态投影新增原生IsAlive/IsPrimaryEnemy；检查器保留存活玩家、无存活主敌人、原生真谓词、当前动作历史、完整快照与清理时序门。旧版仅空列表的正常证据保持可读。没有删去敌人或减少差分字段。
3. 原生伤害可在PotionUsedEntry追加前让谓词为真。该较早查询可以记录已有游标，但不能授权当前动作胜利；必须等当前动作自身历史见证与清理前完整状态相同。观察器只在实际战斗根执行期间记录谓词，避免将转换期间的查询误认成战斗见证。没有再次调用原生谓词来制造见证。
4. Rocket.BeforeDeath直接调用缺失的NAudioManager实例，先于原生PlayOneShot自身的TestMode空操作而空引用。新窄transpiler仅将这一处callvirt改为缺接收者的安全调用，保留原回调及IsOverOrEnding查询、背景分支和全部战斗业务。它有准确方法/MVID/token/IL哈希、单一IL锚点、未知补丁拒绝与全局补丁稳定性检查。实际Boss记录有一次boss-audio-guard；未启动UI或音频服务。该调用适配不代表零售演出或时间等价。新方法逐项实际Harmony注入负测尚未运行，不能冒称已做。
5. 重复宝箱使用具体房间账本，父操作与原生退出子任务的归属、必需唯一、顺序和完整状态绑定。旧宝箱不再满足新房间阶段；真实退出先由管理器弹出房间，再进入下一房间，契约保留这一变化。
6. 检查器对已冻结主路线v2/v3-route和当前v4-route-heal保留有限描述兼容。只有policy可取明确历史值，其余整个描述必须精确匹配；缺features、未知policy或新字段均拒绝。原producerBuildId保留，历史策略不被改称新版本。

反编译参考仅保存在 `.build/runs/act2-lifecycle-work01/`，未写入可公开源码。宿主保持MemorySaveStore、ShouldSave=false、单人网络服务、Steam未初始化和无UI。SaveProgress/replay调用仍由既有离线拦截处理。

## 冻结与分层验收

- 17次最终原生执行：`DEE290B603413137770A9C52ED4B905E2ED40701843D42FDD1655A18CF1BE957`，`.build/native-build/act2-lifecycle-final03/`。
- 最终检查器/当前源码：`908DE0CD7A852365FC40B55CE691C7AF36D0BF8A95742D024C056CABBC59328F`，`.build/native-build/act2-lifecycle-final04/`。
- final03到final04仅NativeMajorContract头描述检查及NativePreparation新增reader-only MatchesDescription辅助改变；原Describe/策略/宿主/采集逻辑未变。按分层原则重验原始证据并补有限版本控制，未重复17次原生执行。不能把它们改标为final04生产，也不是M1最终同构建全量门。
- 当前91个源码输入、55编译输入、3游戏输入、7产物；dirty源码副本与manifest绑定。15个锁定Mod输入及52个兼容源码不变。Core源/产物保持，Core98复用旧相同版本证据，本轮未重跑。

| 当前验收证据 | 范围 |
| --- | --- |
| 17次最终原生执行 | 主路线三种子live/replay、DEV03 minimal/off、旧206与24/25切片、重复宝箱live/replay、第二幕精英live/replay、准备精英/Boss夹具 |
| 371份原始记录重验 | 346份此前基线、8份本轮开发执行、17份最终执行；保留原始退出和producer身份 |
| 70项历史控制重验 | 原18+24及上轮28项独立控制 |
| 24项本轮生产控制 | 20损坏拒绝、4正常/等价通过；含宝箱跨实例/退出/完整快照、召唤物胜利、缺谓词、音频归属、空快照及历史描述边界 |

最终共465次检查器调用，新增的原生执行与读侧调用分列；原始失败/未完成记录的拒绝不算游戏成功。最终17次原生结果均DataComplete=true：10个真实Death终局，7个合法切片。三种子重复及精英重放只证明复现。主路线live/replay/minimal/off完整比较route、choices、reward-choices、final-state与720个派发/确认快照，含完整RNG；精英612快照及双宝箱18快照也完整相同。

全部证据、检查明细、原始文件锁和源码副本在 `.build/runs/act2-lifecycle-work01/`，以 `closeout.json` 为收尾索引。曾经失败的开发目录和旧构建保持：入口Unsupported、召唤物校验过严、缺音频实例EngineError、历史游标误拒及旧策略版本误拒均未覆盖为通过。

## 复现与下一门

当前源码鲜运行可使用final04清单，必须指定新输出目录：

```powershell
python -P tools/native-run/run-native-act2.py --manifest .build/native-build/act2-lifecycle-final04/build-manifest.json --output .build/runs/<fresh-main-matrix>
pwsh -NoProfile -File tools/native-run/run-native-slice.ps1 -BuildManifest .build/native-build/act2-lifecycle-final04/build-manifest.json -OutputDirectory .build/runs/<fresh-repeat> -Scenario reward-repeat-treasure -Seed LAB_V0111_NATIVE_DEV_01
pwsh -NoProfile -File tools/native-run/run-native-slice.ps1 -BuildManifest .build/native-build/act2-lifecycle-final04/build-manifest.json -OutputDirectory .build/runs/<fresh-elite> -Scenario major-natural-act2-elite -Seed LAB_V0111_NATIVE_DEV_03
```

本轮应独立审查新胜利语义、原生音频窄桥和重复宝箱契约，再按 [后续交接](NATIVE_RUN_ACT2_LIFECYCLE_NEXT_HANDOFF.md) 持续推进。第二幕精英/Boss获胜及其奖励/第二次跨幕尚未真实验证；两幕完成分支不能标通过。商店/嵌套事件与后续Ancient组合、正常开局完整RunVictory、必要数据缺失门、预登记评估和最终同构建全量回归仍待完成。当前结果是native-only，不是全内容、全角色、平衡或整局胜率验收。
