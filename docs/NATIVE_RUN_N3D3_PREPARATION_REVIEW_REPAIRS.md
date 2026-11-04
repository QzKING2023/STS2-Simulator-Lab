# 4D3 准备路径 R1–R3 修复与统一验证

状态：本轮实施与冻结回归完成，等待独立复审；4D3及M1继续进行。原独审和原始失败证据保留。

冻结 `B1FB0F818977EF57C6CF77014F33FC2695D4C56261C3685BF28F50D453917DDD`，Native DLL SHA256 `B24C179B91E5EB222BF8A383B26D6D958E11A016719111DE413F1EB21E44B9C0`。以实际dirty源码82文件、49编译输入副本、3游戏输入、7产物和逐文件快照为准，游戏v0.111.0/41cef1ea。最终所有执行及新控制绑定final02；final01保留为未收口的历史运行。

## 生产修复

| 必修 | 契约 |
| --- | --- |
| R1胜利事实与完整观察 | 只读postfix观察原生CombatManager.IsCombatEnding真实返回，不修改返回值或业务。在当前Major有限profile中，为存活玩家、无pending loss、敌人列表为空且仍inProgress的真实结束条件保留mandatory major-victory-condition，所有观察模式均记录。绑定动作/房间/历史游标；胜利回调完整状态必须等于最后有效原生条件观察。真正胜利与清理必需唯一，历史清理与游标闭合 |
| R2跨幕 | ActReady参数严格等于当前act/map，ready事实绑定当前Boss房间。入口完整状态从dispatch仅允许SDK真实floor+1/ActStarts+1；完成从入口构造完整合法变更：新登记MapRoom引用、act+1、floor0、node null、map+1、单层房间栈、旧majorCombat移除和战斗上下文清理，其余状态完整保留。后续静止边界绑定已核实完成状态 |
| R3精英遗物 | 当前Major取得契约覆盖MealTicket：新引用、offered content、登记type/model及持有条目一致，完整状态仅追加正确遗物。该遗物没有拾取效果，MerchantRoom进入治疗不属于当前CombatRoom领取。其他Major遗物拾取Hook在原生领取前明确Unsupported；旧独立奖励/遗物场景继续回归 |

原生条件可能在一个动作中真实调用多次，允许更新锚点，不把这些观察计作多次胜利。普通历史不包含所有内部阶段变化；开发探针证明部分回合开始效果会在最后历史之后移除敌人，所以没有把“最后历史必须已清场”作为通用规则，也没有手工模拟这些效果。锚点来自真实原生条件，完整状态/存活/敌人终态共同验证；关闭详细观察也不能缺证据通过。

新增接口使用原有严格Register/patch inventory。实际Harmony API夹具验证安装前foreign prefix拒绝、安装后patch变化拒绝、配置稳定性，目标业务调用0；此项是审计API证据，真实观察由最终原生运行覆盖。

## 最终证据

- 323次原生执行、4074项suite检查、实际Core98；13个执行任务按最终定义全部通过。
- 原始退出码240×0、70×1、8×2、5×3分别核对。Death、Unsupported、InvalidAction、EngineError、BudgetExceeded、DataError及确认前缀保留，没有把非零退出归成胜利。
- 新语义控制148项（132拒绝、16合法/等价通过）加2项登记type/model控制，共150项，0意外结果。旧34反例先在旧冻结全部复现，新冻结拒绝；对完整新证据施加相同变造的对应控制验证实际语义门，不能仅靠缺新事件取得假阳性。
- 准备矩阵14执行/166检查及39原控制通过；历史包原240控制保留，增加7项当前格式正例后共247项，加47个真实原生边界核对。历史8/29/82阶段反例继续拒绝。
- 重要格式迁移：其中7份旧合法历史证据缺新增mandatory条件观察。原旧读取器仍实际返回0，新读取器明确返回1，7个当前构建对应正常/等价副本返回0。记录originalExpected=0、legacyReaderExit和新格式拒绝原因；没有修改旧证据，没有为旧格式放宽生产门。final01原始7失败及外层exit1保留；final02不是抹去这些失败后直接改绿。
- 汇总1062项核对0失败；15锁定输入、52兼容源码与旧证据未变。六个既有源文件修复和一个新控制工具为预期差异。
- 新冻结详细/重放/minimal/off的route、choice、reward-choice、完整final-state（含全部三组RNG）逐结构相等。五条基础路径与旧冻结的四种完整JSON也相等，新增观察在这些样本中未改变原生状态、身份或RNG结果。

Elite DEV03仍为API夹具113/113：5场合法准备战斗后固定BYGONE_EFFIGY_ELITE胜利，原生Gold35、PommelStrike i215、MealTicket i212，HP12→18。Boss DEV03仍为API夹具184/184：9场准备战斗后固定CEREMONIAL_BEAST_BOSS胜利，Gold100、DarkEmbrace i403，HP11→17，再真实ActReady/EnterNextAct到act1/MapRoom/mapGeneration2。DEV01/02准备阶段93/156步真实死亡及自然DEV01的77步死亡保留。没有改HP/资源/牌组或拼接失败路径。

## 覆盖限制和开发失败

这是有限Major策略组合；MealTicket之外的Major拾取契约、附属敌仍留场的胜利投影、新AfterActEntered业务变化均未新增覆盖。不能从当前跨幕RNG不变推断所有幕/所有遗物不消耗RNG。真实卡牌、抽牌、普通奖励和原生清场的变化由既有路径和完整状态正例保留。

未验证自然Boss胜利、第二幕首节点/Ancient、通用多幕控制器、正常开局默认Acts到Architect/RunVictory、策略胜率或最优性。两个成功目标使用显式EnterRoomDebug API夹具，不能称自然完整通关。

开发记录完整保留：dev01锚点误选最后历史且源变化保护阻止一次replay；dev02过早条件观察（死敌尚未从列表移除）和MapRoom登记表缺失；dev03两个定向探针后进入冻结。final01所有原生执行成立但7旧格式控制不符合新协议；修改的是回归驱动的迁移验证，生产门未放宽。登记模型补充控制首版分析误要求命中新末端门，实际更早的offered-content/model门已拒绝，保留summary-first及原日志，仅修正分析预期。这些旧运行不混入最终323。

## 文件和复现

生产改动：NativeHost.cs、NativeObservation.cs、NativeMajorContract.cs、NativeRewardContract.cs、NativeRewards.cs；回归驱动major_transition_controls.py；新增preparation_repair_controls.py。完整源码快照、冻结清单、API探针、开发失败和13任务driver位于`.build/runs/n3d3-preparation-repair-work01/`。最终证据在`.build/runs/n3d3-preparation-repair-final02-*/`。

```powershell
python -P tools/native-run/run-native-preparation.py --manifest .build/native-build/n3d3-preparation-repair-final02/build-manifest.json --output .build/runs/<fresh-preparation-dir>
python -P tools/native-run/preparation_repair_controls.py --manifest .build/native-build/n3d3-preparation-repair-final02/build-manifest.json --source .build/runs/<completed-preparation-dir> --output .build/runs/<fresh-control-dir>
```

输出目录必须全新。各控制包含原证据hash锁、reseal及实际生产reader日志。游戏反编译仅在本地.build参考，不进入项目公开文档或代码。未提交推送、启动UI、接触真实Steam/网络/用户存档、修改Mod/安装或重置用户工作；无执行或权限阻塞。

下一步先独立复审本轮修复；通过后接既有自然Boss、下一幕首节点、通用run组合及M1步骤5–6授权。
