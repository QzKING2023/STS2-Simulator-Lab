# 第二幕自然推进：独立分层验收

日期：2026-11-03。结论：**本轮声明范围通过；未发现影响现有277动作证据的必修缺口。第四步与完整M1仍未完成。**

本次只读生产实现和原始证据，在独立副本上实际调用冻结生产检查器。没有新增原生游戏运行、构建或生产代码修改。验收遵循 [有限信任边界](NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)，没有尝试从任意协同伪造的整份日志重新实现游戏规则。

## 冻结、来源与断电恢复

- 检查器及当前源码：`EE0A62AF261D0F9A9786338F130BBCC98ED293F0E434DAFFE16D16DE41C3490F`。
- 本轮11次原生执行的生产构建：`6FF6F3528D673DEC235644E08AC7FA35F91E7BD20302CA937263CBC791182994`。
- 两份构建ID均按原始manifest序列化规则重新计算一致。当前89个源码输入、53个编译输入、3个游戏输入、7个产物，以及保存的dirty源码副本均匹配。生产构建的编译副本、输入和产物也匹配。
- final02到final03唯一编译源码差异是 `NativeRewardContract.cs` 中特殊卡来源引用校验。原生生成链、策略、采集器和宿主隔离没有在这一步变化。因此复用11次原生执行并用final03重验符合分层规则，不能将它们改标为final03执行。
- 15个锁定Mod输入、52个兼容源码未变；Core98是旧相同源/产物证据的复用，本次没有重新执行Core检查。

恢复记录、完整批次文件和原始进程时间一致：11次原生执行、346份原始证据重验及18控制在恢复前已落盘；恢复仅补24项历史控制。当前独审重新哈希并实际读取这些原始记录，没有重复原生任务。历史时点“无遗留进程、24控制目录尚不存在”来自恢复回合记录；本轮不能回溯证明当时OS进程状态，文件时间也不单独证明执行真实性。

## 实际流程核验

三种子保持原样：DEV01 Death93、DEV02 Death156、DEV03 Death277。Death是合法已完成终局，raw exit0不表示通关。原始报告均明确native-only、`Comparison=NotCompared`。

DEV03从正常Neow开始，同一run产生第一幕Boss胜利、原生奖励、跨幕和Tezcatara/Soup选择。全程14场实际胜利，其中第二幕为 BOWLBUGS_WEAK、THIEVING_HOPPER_WEAK、CHOMPERS_NORMAL、BOWLBUGS_NORMAL 四场，各自有当前房间原生奖励完成记录。随后THE_OBSCURA_NORMAL敌方动作导致HP0，在第二幕第6层 `MapCoord (0, 5)` 停止，Gold403，确认/派发277/277，最后边界 `EndTurn:277`。没有MajorFixture动作，没有正常开局完整RunVictory。

CARD.INFLAME对象 `i448` 在a206取得、a211敌方回合前仍在永久牌组，a211确认后已经离开牌组。a216原生SpecialCardReward提供并实际返还同一对象，升级0，原生入组楼层从19更新为20。宿主通过 `RewardsSetSynchronizer.SelectLocalReward` 等待原生选择，私有 `_card` 仅用于读取具体来源；固定版本原生 `SpecialCardReward.OnSelect` 执行 `CardPileCmd.Add`。Lab没有手动追加牌组。原生拒收或替换为其他对象尚不属于该特殊奖励分支支持范围，会显式停止。

独立比较三种子live/replay及DEV03 minimal/off：route、普通选择、奖励选择、final-state均完整结构相等；DEV03每次554个派发/确认快照一致，包含完整run/player RNG状态，未只比较计数。真实跨房、抽牌、奖励、原生卡序列化和资源变化均保留。重复同种子只证明复现，不是独立胜率样本。

开发失败也仍保留：dev02在221派发/220确认时遇到Unsupported，raw exit2、DataComplete=false、LastBoundary=EndTurn:220；原始trace计数与报告一致。未把未确认奖励动作计入成功前缀。

## 生产检查器实测

独立证据目录：`.build/runs/native-act2-independent-review01/`。

| 类别 | 本次实际结果 |
| --- | --- |
| 完整原始证据重验 | 346/346符合预期；262通过，84原错误/未完成拒绝 |
| 上轮18新控制与24历史控制 | 42/42符合预期；保留其原producer身份 |
| 本次独立定向控制 | 28/28符合预期；24损坏拒绝，4正常/等价通过 |
| 总生产检查器调用 | 416次；新增原生执行0次 |

84份拒绝包括53 InvalidAction、15 EngineError、8 Unsupported、5 BudgetExceeded、3 DataError，不计作成功游戏路径。独立的2605项来源、哈希、退出、计数、隔离及重放核对见 `checks.json`，它们不是2605个原生测试。

本次控制使用final02实际277动作证据副本，覆盖第二幕目标/act/作用域时序、RNG内部字改变但计数不变、特殊奖励结果缺失/重复/错root/错误成功标记/旧实例/序列化内容、父子共同快照改变、当前Major宝箱不得退回旧格式缺房间投影、退出开始缺失与静止快照联动改变、死亡快照/归属/终态矛盾、后续战斗及奖励归属。副本重新封签后调用实际生产检查器；24损坏被语义检查拒绝，非仅哈希失败。正常详细日志、冻结重放、minimal和JSON对象属性重排均通过。

现有宝箱旧格式兼容仍受具体登记类型、动作和子操作约束；Major场景明确要求当前房间投影。当前正常证据包含真实领取和退出，但不证明同一run第二个自然宝箱已执行。

## 下一步的明确边界

本轮没有阻断现有证据验收的缺陷。以下是继续M1时必须闭合的能力，不能因本轮通过而略过：

1. `NativeMajorContract.cs` 的 `major-battle-result` 分支仍对所有act=1战斗要求 `type==Monster`（当前约第236行）。这是从旧第二幕首战切片遗留的限制：第二幕精英或Boss即使原生胜利，也会在这里被拒绝。推进该范围前需将首战Ancient前置与后续战斗类型分开，保留真实目标/房间/胜利事实门，并用实际精英或Boss流程验证。本轮只有普通战斗，未用篡改日志冒充未发生的Boss验证。
2. 两个不同宝箱、第二个Boss奖励及第二次跨幕累计计数尚无实际连续证据。可先用明确标注的原生API夹具补生命周期，再取得自然路径证据，不能以夹具关闭自然通关门。
3. 当前控制器仍限定两幕、Tezcatara/Soup和有限非战斗房间；自然商店、嵌套事件等需要组合已有入口。事件恢复必须等待EventModel.Resume真实任务。
4. 策略在第二幕第6层死亡，可继续改进合法路线、奖励和战斗决策；保留版本、预算和预登记失败分母，不强改资源或以成功样本替换失败。
5. 正常开局完整默认Acts至原生RunVictory、必要数据缺失门、预登记评估及最终同构建全量原生/共享回归仍待执行。观察投影中的unavailable字段与不完整触发因果图限制保持。

继续执行见 [已有下一交接](NATIVE_RUN_ACT2_NEXT_HANDOFF.md)，先处理上述第二幕战斗类型限制和重复生命周期证据。没有新的用户审批需求。本轮未修改生产代码、原证据、角色或游戏安装，未启动UI、真实Steam/网络/用户存档，也未提交推送。

## 可核验文件

- `closeout.json`：本轮结论、数量、范围及未覆盖项。
- `reread-summary.json`、`reread/case-*/review.log`：346原始证据与42既有控制的实际退出和语义诊断。
- `controls/summary.json`、`controls/case-*/review.log`：28项独立控制。
- `checks.json`、`build-id-check.json`、`input-lock.json`：来源与冻结核对。
- `natural-facts.json`、`prefix-review.json`、`recovery-review.json`：实际胜利/奖励/死亡、返还牌、失败前缀及断电恢复核对。
- `review.py`、`controls.py`：独立审查脚本。输出目录固定为本次新建目录，不应直接重复覆盖；重验时复制脚本到新的审查目录。
