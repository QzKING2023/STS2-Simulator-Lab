# Juggernaut 窄排序修正：预登记自然路径结果

本轮实现及五次执行已收口：规则正负控和原生重放通过，三个预登记自然样本全部真实死亡，**没有 BossVictory、RunVictory 或完整 M1**。这次不能认定为策略强度改进。

最终冻结：`6D1CC702EC39F84C99F8B5519BB716D22F3E4EC321C005859EDA195049B574CE`，清单 `.build/native-build/juggernaut-policy-final02/build-manifest.json`。统一使用同一产物；两次构建均0警告/0错误，第一次构建只用于预检，未执行原生游戏。证据以 `.build/runs/juggernaut-policy-work01/closeout.json` 为索引。

## 修改与适用范围

本轮只改两个生产文件：

- `NativeThirdPolicy.cs`：在 TestSubject 原有生存、自伤和防御缺口判断之后，当原评分为负、当前防御缺口为0、牌为非攻击且有正格挡量、玩家实际有正 Juggernaut、敌人非 Intangible 且非复起时，评分提高到800。已有非负分、迫近伤害的防御判断和其他牌的前置限制保留。规则不计算牌效，也不读取未来牌序或 RNG。
- `NativePreparation.cs`：策略标识更新为 `v40-juggernaut-block`，并把旧 v39 完整配置加入已有的只读历史兼容列表。生成仍只写 v40；兼容时继续比较完整配置，不能只凭版本文字接受。

初次预检发现旧 v39 证据被新版本门拒绝；当时 ledger 为空，尚无原生执行。保留 `preflight-attempt01/` 和第一次不可变构建，修复历史兼容后重新冻结，未改写旧证据。没有把这次预检失败算作游戏 Death 或 Unsupported。

Core、采集器、原生业务执行器和检查器其他契约没有修改；15个锁定输入与52个兼容源码哈希不变。旧 `juggernaut-work01` 的100个执行/配置文件仍匹配原锁。

## 原生结果

三个种子在执行前登记，顺序为 DEV_03、DEV_01、DEV_02；都从正常 Neow 开局开始，没有载入旧前缀或手改生命、卡牌、资源。原生游戏程序集固定 v0.111.0 / 41cef1ea。所有结果为 native-only / Comparison.NotCompared。

| 样本 | 终态与原始退出 | 确认/派发动作 | 最远位置 | 实际死亡遭遇 |
| --- | --- | ---: | --- | --- |
| DEV_03 | Death / Completed / 0 | 618/618 | 第三幕，actFloor15，(3,14) | TestSubject Boss，剩119HP |
| DEV_01 | Death / Completed / 0 | 93/93 | 第一幕，actFloor8，(5,7) | Fogmog 普通战斗 |
| DEV_02 | Death / Completed / 0 | 156/156 | 第一幕，actFloor7，(4,6) | Overgrowth Crawlers 普通战斗 |

三条路径分别有23/3/5次实际战斗胜利回调，但均未完成 run。DEV_01/02 死亡时的6/33是当前普通敌人的 HP，**不是 Boss HP**；早期实验脚本通用摘要字段名 `bossHp` 过宽，正式结果以 final-state 的 `majorCombat.type/encounter` 和本表为准。没有用更换种子或奖励选项替掉死亡样本。本轮未遇 Unsupported，也不能据此证明所有内容都支持。

按事先登记的最远流程规则选中 DEV_03，随后用相同构建做详细自然重复，再做一个新进程完整冻结重放。两次都 Death618 / Boss119HP；minimal 与 detailed 的全部派发/确认 payload 一致，详细与重放的完整状态/RNG、route、choices、reward-choices、final-state 全部相同。选择 DEV_03 是为了认证最远流程，不是按不同 Boss 的剩余 HP 评定策略优劣。重复/重放也不是新增独立种子样本。

## 为何未改善

原参考路线的 `juggernaut-baseline` 是带冻结前缀的分支实验，最佳33HP还包含 a617 后两张防御的显式干预；本轮是全程 live 自然策略。因此119HP相对旧59/33HP确实是较差的已观测终态，**不能把全部差异直接归因成一项评分规则的严格全程因果效果**；本轮没有在第五次之后追加在线 v39 对照。

`first-divergence.json` 证明新旧参考的前575个确认动作和完整状态/RNG相同。a576 的派发前状态仍完全一致：玩家81HP、余1E、有 Juggernaut12，手牌为一张防御和 Anger。旧冻结动作是 Anger，新 live 动作是防御。新规则并非仅在之前研究的 a617 才起作用。

详细原生轨迹进一步显示，新路径在第二形态的 turn3 提前打出了 Impervious，第二形态到 turn5 才结束；第三形态在 turn6 开始时仅55HP。最终 turn10 结束时玩家4HP、18Block，Boss119HP且有 Intangible；原生敌方攻击格挡18、实际扣4HP、产生25 overkill，玩家死亡。记录在 `boss-key-decisions.json` 和原详细 trace。提前使用消耗型格挡、攻击/抽牌时序改变与后续劣化同时出现；它们是下一轮需判别的原因候选，不是已经证明的单一原因。

这也说明：a617 两次真实12伤害只支持特定窗口的局部价值，不能直接推广成全形态的高优先级策略。

## 正负控、重验与预算

冻结 DLL 中实际的纯输入评分方法通过15项API控制：有效收益、无能力、零/负格挡、Intangible、复起、攻击牌、防御缺口、已有正分等。它们不是原生效果执行，也不能证明整场策略更强；真实效果由上述自然运行和重放验证。

生产检查器共12次有效调用：2份旧合法尾部、3个原R1坏副本、5份新实际输出，以及未知策略版本/被改节点预算的2个重新封签负控。结果符合预期，历史完整配置约束没有放宽为任意版本或任意字段。另有66项来源、版本、输入/产物、隔离、动作前缀与重放核对通过。具体条目在 `verification.json`、`ranking-controls.json`、`readers.json`、`profile-controls.json`。

预登记上限为5次原生执行、20分钟、2GiB、每进程120秒。实际恰好5次：3条minimal自然路径、1次详细重复、1次完整冻结重放；登记到最后一次重放认证约5分10秒，原生进程累计约100.88秒。全部构建、工作材料和原始运行输出合计约0.90GiB，精确字节以closeout为准。达到5次门后没有再运行游戏或调参重试，后续只核对证据、运行检查器与写报告。两个构建和预检失败材料均计入数据范围，没有删除原始证据。

所有实际进程均有120秒门限且未超时；MemorySaveStore、shouldSave/UI false、Steam未初始化、Telemetry0、host faults为空。没有Mod/CombatSolver加载，没有UI、真实用户存档或其他项目写入，没有提交推送。该轮按策略变化做影响范围内回归，未机械重跑无关原生矩阵；最终 M1 同构建全量门仍待完成。

## 必要后续

本轮预算已用尽，保留 v40 作为已测候选，不能将其标作胜率提升或取代较好冻结路线的证据。独立审查应先确认新规则、失败前缀与版本兼容均符合声明，再决定下一次实验。

若继续 TestSubject，优先只判别两个相关因素：把纯格挡输出收益限制到已有直接证据的第三形态非 Intangible 窗口，及没有迫近伤害时保护 Impervious 等消耗资源。采用相同可信前缀、预登记有限候选和明确终态指标，避免同时重写整个策略；是否能赢仍需实际执行。若继续 DEV_01/02，应先针对 Fogmog / Overgrowth 的具体致死回合诊断基础防御，而不是把早死解释成流程全覆盖。上述均未在本轮实施或追加执行。

M1 仍缺自然第三幕 Boss 胜利至真实 RunVictory、预持折扣遗物原价的定向覆盖、以及最终统一构建验收。三条合法死亡和重复性通过不能关闭这些验收门。

## 复现入口

`registration.json` 保存固定顺序、选择规则和预算，`experiment.py` 拒绝对已有 ledger 重启。`ranking-controls.ps1` 调用冻结产物中的实际方法，`profile-controls.py` 只在派生副本运行生产检查器。`verify.py` 读取原始输出、比较完整边界和版本锁。历史预检失败与最终有效验收分目录保存。

未来重新登记批次时，可用下列已有包装器复现单条路径，输出必须换成新的 Lab 目录；当前批次已到5次门限，本轮未再次执行此命令：

```powershell
pwsh -NoProfile -File tools/native-run/run-native-slice.ps1 `
  -BuildManifest .build/native-build/juggernaut-policy-final02/build-manifest.json `
  -OutputDirectory .build/runs/NEW-REGISTERED-OUTPUT `
  -Scenario major-natural-act3-route -Seed LAB_V0111_NATIVE_DEV_03 `
  -Observation minimal -ProcessTimeoutSeconds 120
```
