# M1 步骤4C：三组审查修复与同构建回归

日期：2026-11-02。对应 [4C独立审查](NATIVE_RUN_N3C_REVIEW.md) 的R1/R2/R3。三组修复和最终同构建回归已完成，独立复审待执行。4D、步骤5–6继续待办，本轮不代表M1完整流程已完成。

## 修复范围

本轮改动五个运行／验证文件：`NativeRewardContract.cs`、`NativeRewardControls.cs`、`NativeRewards.cs`、`NativeTreasure.cs`、`run-native-n3c.ps1`，均在`tools/native-run/`。没有改卡牌、原生效果或Mod项目。此前实现与原始失败证据保留。

| 审查项 | 生产实现修复 | 保留的合法行为 |
| --- | --- | --- |
| R1：必需子选择、顺序、完成字段 | 有限父子kind和决策形状；父start之后才允许子操作；CardReward尝试必须有且只有一个已完成menu；根／set必须返回真实完成字段。menu结果与item结果一致，原生alternative的AfterSelected决定奖励是否完成 | empty set、整集合skip没有card-menu；卡牌Skip alternative可以返回false，再由原生集合skip结束；Cauldron嵌套Offer仍归属当前根。未知kind、缺失／重复menu、提前子操作和伪完成均拒绝 |
| R2：子会话静止快照 | set声明／最后子返回形成不可变完整锚点，下一item open与其相等；set-completed快照独立保存并绑定随后的set return。比较完整结构及未来嵌套字段 | Populate、领取、Hook、嵌套Offer、宝箱和Travel的实际业务区间仍允许状态变化；没有把父子任务全程强行相等 |
| R3：真实取得身份、金额、卡属性 | 卡menu返回记录真实候选的不可变`sourceCards`、原生`sourceAlternatives`和取得后的`acquiredCard`。候选与来源、选中对象与永久deck完整属性交叉验证；固定未升级Strike/Defend夹具有独立0级期望。宝箱记录canonical→created mutable→actual acquired，并验证是领取前不存在的正确模型实例；金币返回值、实际delta及前后金币一致 | 读取原生任务和对象，不手工增牌／加金币。通用数据保留选择前和取得后升级；实例替换／拒绝取得的完整适配仍为后续工作，不能冒充same-instance成功 |

卡奖励source记录在返回原生选择之前复制，包含实例、model、upgrade以及alternative真实OptionId/AfterSelected；不是从最终deck倒推。取得后再记录实际卡属性。来源和后态可以表达真实取得Hook带来的变化，本轮固定卡夹具仍独立要求未升级，防止把两处同时写99的控制循环当作期望。

宝箱本轮认证`native-unmodified`金额账本。实际原生返回金额若与观察到的金币增量不一致，会在原生效果发生后明确停Unsupported并保留未确认前缀；没有把修改后金额强行改回预期。涉及金币修饰／SpoilsMap的完整来源账本列入后续原版整合，不能宣称已经覆盖。遗物替换或卡替换也保持明确停点，不猜测取得身份。

## 反例与生产负控

原十个误接受反例在新的正常执行包上重新制作，实际调用最终DLL的生产`--review`，均在相应语义门返回1。对应表在`.build/runs/n3c-repair-work01/repair-inspection.json`的`fixedIndependentCases`，保留旧独立反例目录不动。

| 原反例 | 当前控制 | 拒绝门 |
| --- | --- | --- |
| 整段card-menu缺失 | repair-missing-menu | 必需／唯一子操作 |
| 子选择早于父start | repair-child-before-start | 父子kind/start/order |
| set完成false | repair-false-set | set完成与静止状态 |
| 根完成false | repair-false-root | root completion |
| set return新增未知状态字段 | repair-set-return-extension | 完整静止状态 |
| set return金币999 | repair-set-return-gold | 完整静止状态 |
| item open/answer/start一起漂移 | repair-item-open-drift | 当前集合不可变锚点 |
| 取得指向旧库存遗物 | repair-old-relic | 新实例／来源 |
| 宝箱返回金币−999 | repair-gold-negative | 原生返回与资源账本 |
| 卡候选升级99 | repair-menu-upgrade | 真实候选来源属性 |

新增32个控制还覆盖重复menu、新ID重复事务、未知kind、完成字段缺失/null/错误类型、RNG／未知字段漂移、created/acquired同时指旧遗物、模型不符、金额正值矛盾、来源与取得属性矛盾、候选／来源同时改99、alternative来源／完成语义缺失或错配。保留原85个控制，合计117份：115损坏拒绝、2等价接受。它们是修改并重封的证据副本，不计原生执行。

`inspect-repairs.py`独立核对整个payload实际变化、十个明确拒绝诊断和正常相邻状态／来源；371项通过。并非只看控制命令退出码，也未用hash损坏替代语义拒绝。

## 冻结与复现

- Build ID：`5E6BCC9F929004CB9659B361DBF66518527249847BA34A0B4971D9EDD7FA0E3B`。
- Native DLL SHA256：`44B20B043D226C65D1253EDFB11C730C59543BEDA8997762A305BD4DE0ABD74A`。
- Manifest：`.build/native-build/n3c-repair-final01/build-manifest.json`；编译0 warning / 0 error。
- 实际48份源快照及索引：`.build/runs/n3c-repair-work01/source-snapshot-final01/`与`source-snapshot-final01.json`；Build ID独立重算记录为`build-id-final01-check.json`。
- 4C执行与控制：`.build/runs/n3c-repair-final01-suite/`；旧回归分别为`n3c-repair-final01-n3b/`、`-n3a/`、`-n2/`。
- 串行驱动／进度：工作目录的`run-final01.ps1`、`final01-progress.json`；最终汇总和收尾检查在`summary.json`、`closeout.json`，状态为passed-pending-independent-review／passed，无挂起或结果不确定的操作。

## 最终同构建结果

| 矩阵 | 实际结果 |
| --- | --- |
| 4C | 55原生进程、386检查，0失败；42正常执行／重放／观察对照、13故障控制 |
| 4B | 34原生进程、307检查，0失败 |
| 4A | 16原生进程、273检查，0失败 |
| N2／步骤1–3 | 74原生进程、580检查，0失败 |
| 总计 | **179原生进程、1546 suite检查**，全部绑定上述最终冻结 |
| Core | 98项实际执行通过；驱动7个产物hash与旧锁一致，Core DLL与本次冻结产物一致 |
| 补充核对 | 原矩阵636项＋修复专项371项，全部通过；入口分别为`inspect-final.py`和`inspect-repairs.py` |
| 历史审查 | 最终reader读取115份保留原构建身份的旧包，结果符合预期；460个原始文件hash未变，不计为新增原生执行 |
| 固定输入 | 15个锁定Mod输入、52个兼容源码hash不变；48实际源快照、30编译输入、3游戏输入、7产物全部匹配 |

179个原始进程退出分布为122×0、52×1、2×2、3×3；这些包含预期错误、Unsupported和预算停止，没有把它们重记为正常完成。4C全部正场景新进程重放及5组minimal/off对照保持route、choices、reward-choices、完整状态和已盘点RNG一致。

正常同run片段仍为24/24根动作，实际首战Victory→原生领取→下一战Play→使用真实获得的药水，并保存永久卡／战斗副本关联。旧DEV01为71/71连续片段，DEV02为138/138真实Death，DEV03仍为77/78 WhisperingHollow Unsupported。奖励与药水效果后故障继续保留2/3、4/5真实前缀及未确认效果。原自然skip24/take25、RNG与旧冻结业务结果保持。

```powershell
pwsh -NoProfile -File tools/native-run/run-native-n3c.ps1 -BuildManifest .build/native-build/n3c-repair-final01/build-manifest.json -OutputDirectory .build/runs/local-n3c-repair-check
dotnet .build/native-build/n3c-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3c-repair-final01-suite/rewards-natural-1
```

复跑目录必须是Lab内全新目录。其他原生矩阵沿用`run-native-n3b.ps1`、`run-native-n3a.ps1`、`run-native-n2.ps1`并传同一manifest。历史审查reader另行保留原包构建身份。

开发候选dev02已完成55进程／383检查，但随后增加alternative来源及完成语义校验，因此最终矩阵使用重新冻结的final01，不能混用dev02结果。旧失败证据不覆盖、不计入新最终通过数。

4D交接中Driftwood reroll、实际卡替换／拒绝、Sozu拒收和新session的Neow连续奖励迁移仍保留，另需完整处理宝箱金币修饰来源；本轮不实施这些机制。当前自然证据仍仅为同一run的24根动作奖励到下一战片段，native-only／NotCompared，不是自然完整run。无提交／推送、无角色／安装／真实存档改动，无游戏UI或真实Steam／网络调用。
