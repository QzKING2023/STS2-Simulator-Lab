# 4D3 首批冻结独立审查

结论：**当前可信数据门未通过，需修复以下 R1–R3。** 原始精英/Boss 夹具及自然精英 Death 有真实原生调用和终态证据，保存的正常重放/观察对照一致；本轮没有发现这些原始执行被伪造。问题是生产检查器仍会接受与这些事实矛盾的证据副本。精英/Boss Victory、对应奖励和实际 act-ready 尚未触达，继续保持未验证，不能把死亡或夹具称为完整爬塔。

审查冻结：`963B389CA6C6E6B353000ED0CD67CD6B3F60EC397FA94135985FCE0CF601F20C`。
Native DLL SHA256：`30E9E360A41C2C23056ABFFAD53B8F3C406BF1DDAD8B2C5D0B5656D1B13A2FA7`。
独立证据：`.build/runs/n3d3-independent-review01/`；入口为 `closeout.json`、`summary.json`，每个副本有实际 `production-review.log`。

## 实际完成的核验

- 独立重算 canonical build ID；75 源输入、48 编译输入、3 游戏输入、7 产物及75源快照一致。15 Mod 锁定输入、52兼容源码未变。原始证据与生产文件在审查结束再次核对未变。
- 核对保存的309原生执行/3908套件检查，逐包检查 manifest、header、process/result、来源、退出码及隔离。退出分布226×0、70×1、8×2、5×3，未把 Unsupported、故障或预算抹成成功。Core98保存日志与所用Core DLL身份一致；本次没有重跑原生矩阵或Core测试。
- 578项独立来源/结果/重放/文件保持检查通过。live/replay/minimal/off 的 route、choices、reward-choices、final-state 均按完整JSON比较，包含记录的RNG；DEV02/03失败前缀重放保持。minimal/off仍保留必需观察，不代表零观察器。
- 本次实际调用同一最终DLL的生产检查器 **215次**：已有major控制95包全部符合预期；原事件独审79包全部符合预期，包含原16+9反例拒绝、合法业务变化和不完整控制；独立新增41次中19个坏副本误接受、13个坏副本拒绝、4个正常/等价包通过、5个不完整包拒绝认证。
- 所有修改副本重新生成有效seal，再调用真实 `--review`，没有以破坏哈希链代替语义检查。检查器只对新副本写审查结果，未修改原证据。

`provenance.json`保存577项初始核对；`summary.json`另含最终文件保持检查，共578项。215次reader调用不计入309原生执行。审查收集器的exit0表示收集完成，`closeout.json`与最终收尾gate明确为未通过，不以收集器退出状态掩盖19个误接受。

## R1：死亡回调完整状态及终态事实缺少绑定

位置：`tools/native-run/NativeMajorContract.cs:99`–`:103`、`:41`–`:47`。当前只检查Losses计数、HP0及inProgress等局部条件；没有保存不可变的死亡完成快照并绑定随后根动作确认，也没有核对终态facts中的完整native投影、来源和终态类型。

实际误接受13包，均为reader exit0：

| 副本 | 操作 |
| --- | --- |
| case-000 /002 /003 | 单独改写 battle-loss-start 的金币、RNG counter、未知嵌套字段 |
| case-004 /005 /006 | 单独改写 battle-loss-confirmed 的同类完整状态 |
| case-007 | 两个死亡回调共同增加金币，随后 confirmed 恢复原值 |
| case-008 | 死亡开始时HP改成80，随后恢复0 |
| case-009 | 死亡完成回调的Encounter改为另一Boss，原房间和后续状态不变 |
| case-010 | terminal-contract声称RunVictory，实际快照/报告仍Death |
| case-011 | terminal-stamp.native声称Won=true、WinReturns=1、Losses=0 |
| case-012 /013 | terminal-stamp声称natural-run来源或retailLossFinalization=true，与API夹具/宿主范围矛盾 |

三个真实正常包均显示：死亡开始前HP已经0；ProcessPendingLoss前后只改变 `majorCombat.inProgress` 与 `lifecycle.native.Losses`；死亡完成到根confirmed完整状态相等；confirmed到关闭只改变Closed。具体差异与致死DamageReceivedEntry见 `death-facts.json`。这不是要求将战斗整个执行区间冻结；真实伤害、回合流程和清场仍应允许其原生变化。

最小修复：按固定版本实际ProcessPendingLoss边界建立完整输入→完成转换和完成→根确认绑定，保存不可变锚点；仅允许该阶段经原生代码确认的变化。死亡前提及当前combat/room/encounter要一致。terminal-stamp的native事实须等于其快照、scope等于注册来源，retailLossFinalization保持实际未执行口径；terminal-contract的terminal须等于实际终态和结果协议。不得只继续添加金币或RNG的单字段特判，也不能把前后两份可共同篡改的回调互相比对后直接信任。

## R2：战斗历史未绑定实际所属动作和房间上下文

位置：`tools/native-run/NativeTraceContract.cs:112`–`:118`。当前检查epoch、索引、数量和清理顺序，但没有核对combat-history的root/context与实际当前动作。

`case-014`将首个历史记录root改为`a1`，`case-015`将context改为`other-room`，两包均exit0。历史的伤害/格挡/能量来源因而可以错误关联到另一个动作，影响本轮可信数据目标。

最小修复：在实际历史产生阶段绑定当前已派发动作及其context/战斗epoch；历史清理和末端checkpoint按各自真实生命周期归属处理。不要盲目要求所有profile、所有观察都必须处于根动作内部。`history-ownership-facts.json`核对了当前19个major包：所有存在的详细combat-history均位于活跃动作内，原root/context一致；minimal/off没有详细历史。若共享层修复涉及其它合法阶段，应通过已有同构建回归确认，不放宽未知归属。

## R3：夹具声明及选定Encounter未与完整根动作绑定

位置：`tools/native-run/NativeMajorContract.cs:32`、`:49`–`:71`，以及 `NativeTraceContract.cs:119`–`:129`。当前验证第二场实际Encounter等于预登记目标，但MajorFixture根决策、夹具声明及目标之间缺少一个完整的必需、唯一、有序关联。

四个误接受包：`case-028`删除全部fixture-setup-declared；`case-213`重复声明；`case-214`将声明Encounter改为VantomBoss；`case-215`同时把root-request、dispatch、confirmed内MajorFixture动作改为VantomBoss/Boss，并改声明，实际仍进入BygoneEffigyElite。全部exit0。仅改dispatch的原独立负控被通用动作一致性门拒绝，不能替代这种联动检查。

最小修复：将预登记目标→唯一夹具声明→MajorFixture根动作→实际room-entered/major-battle-start的实例、Encounter和RoomType绑定；声明与操作顺序、来源、完整静止状态均须一致。声明必须属于本场景，在真实普通战斗和奖励完成后、对应夹具派发前出现；不能由已正确的实际Encounter反推错误决策也正确。自然场景保持禁止夹具声明，真实状态变化仍由原生方法完成。

## 已成立的原生事实与保留边界

源代码执行链为实际TryManualPlay/EndPlayerTurnAction→原生ProcessPendingLoss前后观察→等待Death→TerminalStamp，LossBefore/After为void观察，不跳过原生主体。三包最后致死伤害有WasTargetKilled，回调和确认同root，HP0且inProgress=false，随后Closed。代表精英/Boss各51/51；自然精英93/93。精英/Boss包原普通战斗确有奖励，目标精英/Boss没有Victory回调或ready记录。

两种Unsupported分别保留35/35和99/100前缀；故障3/4、预算4/4、导出DataError51/51仍不认证完整成功。上述故障为明确控制注入。三类正常配置不等于独立胜率样本；API夹具先经过一场Monster后EnterRoomDebug，不等于自然走到Boss。

MemorySaveStore、shouldSave=false、Steam未初始化、无UI、单人服务守卫符合保存证据。精英/Boss分别有4次SaveProgress和1次replay被拦截，自然样本11/3次；这些是拦截调用，不是持久化写入。本次仅运行持久证据检查器，没有启动原生游戏路径、UI或真实Steam/网络/存档操作。

没有把LastVictoriousRoom扩大解释为全部同房间多阶段Boss支持。奖励RNG仅验证已声明的有限状态步进，不独立重算整个奖励池；这些限制与后续可达性工作继续保留。现有原始业务结果无需废弃，修复检查器后按新冻结重新验收。

## 复现与接续

任一坏副本可直接复现，例如：

```powershell
dotnet .build/native-build/n3d3-final02/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-independent-review01/case-007
dotnet .build/native-build/n3d3-final02/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d3-independent-review01/case-215
```

当前实际exit0，修复后应exit1并给出对应语义错误。`review.py`保留精确变换；该脚本使用完成记录续跑，本轮215次均独立计数。最初生成RNG控制时误用runRng中不存在的Rewards键，case-001尚未reseal/调用reader便停止；该未完成副本保留、不计入215次，随后只补未执行项。不存在重复原生测试。

下一实现回合先修R1–R3，纳入19个反例及同根因正负控，保留合法属性重排、完整一致扩展、真实死亡/战斗清理、奖励及RNG变化。新源码/产物统一冻结，复跑适用矩阵与原25事件反例，交独立复审。通过后继续 [4D3后续交接](NATIVE_RUN_N3D3_NEXT_HANDOFF.md) 的节点能力组合、合法资源准备、实际精英/Boss Victory→奖励→act-ready，再步骤5–6。用户持续授权有效，无需逐步请批。

本次仅新增本审查文档与隔离证据副本，未改生产实现/原报告/输入/Mod/安装/用户存档，未提交或推送。环境断连已经恢复，目前无外部阻塞；阻断本次验收的是上述可复现数据检查缺口。
