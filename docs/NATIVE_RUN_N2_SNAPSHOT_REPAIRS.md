# N2 必需状态快照：剩余 R1 修复

日期：2026-11-02。对应 [N2 修复复审](NATIVE_RUN_N2_REPAIR_REVIEW.md)中唯一剩余项：快照整段缺失仍可通过。**本地修复与最终同构建矩阵通过，待独立复核。** 沿用原六步授权，未展开步骤4内容。

## 最终构建与证据

- Build ID：`B9C7B7EB283CA1FA895EB554901B3B79FEBAEEAF37B1FCCF724A85775B45BDED`。
- Manifest：`.build/native-build/n2-snapshot-final02/build-manifest.json`；32条source、17条compiled、3个原版inputs、7个outputs，实际hash及重新计算的Build ID一致。
- NativeRun DLL SHA256：`ABF89C7409733FC2D81F903A348FE79B5AA140ED967F35A3A2447D9C41D027BB`。
- Core仍为 `32B54F0DFA32AD3A3607F16B666B869E41A6C17C3AB44035E0A12B1B7B622DF3`。
- 游戏仍固定 v0.111.0 / 41cef1ea，HEAD仍为`d73039d688eb54c9513f5b9508a03947e689aa18`，dirty源码实际纳入manifest。
- 最终矩阵：`.build/runs/n2-snapshot-final02-suite/summary.json`。
- 快照负控：同目录`snapshot-controls.json`及`control-snapshot-*/`。
- 补充检查：`.build/runs/n2-snapshot-work01/inspection.json`；原审复现/覆盖/不变性证据见同目录`snapshot-analysis.json`、`schema-inventory.json`和`review-original-copies-final02/summary.json`。

~~~powershell
pwsh -NoProfile -File tools/native-run/run-native-n2.ps1 `
  -BuildManifest .build/native-build/n2-snapshot-final02/build-manifest.json `
  -OutputDirectory .build/runs/<fresh-output-directory>
dotnet .build/native-build/n2-snapshot-final02/bin/Release/net9.0/SimulatorLab.NativeRun.dll `
  --review .build/runs/<fresh-output-directory>/slice-skip-1
~~~

## 最小实现差异

本轮只改三份运行/验证文件：

- `NativeTraceContract.cs`：集中列出20类必需快照事件、16类元数据事件；新增`ReadSnapshot`，先检查快照存在、非null、object类型。
- `NativeEvidence.cs`：生产reader先调用该入口；返回的同一已验证snapshot供原来的字段/RNG检查及`NativeTraceContract.Accept`继续使用。原37类错误门保留。补充证据变异工具以生成缺失/错误类型控制。
- `run-native-n2.ps1`：完整保留原队列，新增快照和元数据控制及具体拒绝原因检查。

这取消了两处“只有发现state才校验”的入口漏洞。快照内容仍按原来的必要字段、类型、RNG流集合等规则检查，没有为缺快照补默认值，也未改变事件采集、原生牌效或R2来源语义。

明确的契约为：

| 类别 | 事件 | state要求 |
| --- | --- | --- |
| 根动作/运行边界 | dispatch、confirmed、new-run | 必须为object |
| 候选/开局/战斗 | event-candidates、opening-finished、map-candidates、first-battle-opening、combat-candidates、combat-history | 必须为object |
| 生命周期/奖励 | battle-victory-before-cleanup、battle-victory-cleanup、reward-offer、rewards-completed、card-reward-acquired、next-combat-first-play | 必须为object |
| 夹具/故障/终态观察 | fixture-setup-declared、fixture-assertion、fixture-contract、sink-failure-trigger、death | 必须为object；列入schema不代表本轮真实执行了Death |
| 元数据 | header、profile-lock、identity、root-request、history-clearing、history-checkpoint、choice-requested、choice-resolved、choice-returned、choice-reward-completed、native-choice-reserved、task-start、task-completed、task-fault、selection-fault、footer | 不要求state；若显式提供，仍校验其类型/内容 |

两组互不重叠，合并仍是原36类事件；未知kind继续拒绝。静态核对全部`Program.Observe`/相关调用与20类必需事件一致，控制清单也逐项覆盖，见`schema-inventory.json`。对元数据不会误加缺快照要求。

## 负控与真实结果

新增65项快照控制，全部通过实际生产reader拒绝，核对具体错误文本及exit1：

- 20类事件各自删除state、设为null、设为array，共60项。
- 原审的“删除全部state”和“只删除一个confirmed state”两项。
- confirmed的string/number/boolean三种额外错误类型。

自然skip原本有182份快照；全部删除后剩0，单边界删除后剩181。两者分别在`new-run`、`confirmed`处以`Missing required state snapshot: <kind>`拒绝。null得到`Null state snapshot: <kind>`，其他非object得到`Nonobject state snapshot: <kind>`。各控制重建了hash链/footer/seal和result内嵌seal，拒绝来自所需检查点。

其中death、sink-failure-trigger在正常切片没有成功写入的对应原生记录，六个相应控制明确标`schemaOnly=true`，用已有记录替换kind以检查该schema分支，不能称真实死亡或sink生命周期覆盖。其余控制直接修改同构建实际事件记录。控制副本不进入自然run样本队列。

另有两个元数据控制：在正常来源的证据副本中插入无state的task-fault/selection-fault，reader越过快照存在性门，因真实语义标记`Faulted inner task`退出1。普通无state元数据由原正常路径的reader通过共同验证；不把故障元数据错误认证为成功。

原审的两个错误副本也保留并重新复制，由最终reader检查；正常旧格式副本返回0，两个缺失快照副本均按上述具体原因返回1。该补充仅验证旧证据格式的reader行为，旧C166输入不混入最终原生run构建计数。

## 统一矩阵与保留成果

最终构建0 warnings / 0 errors。**74个原生子进程、580项检查、0失败；补充114项、0失败；Core98项通过。** 计数分别描述进程、检查和Core契约，不相加为独立场景数。

580 = 上轮379 + 65项新快照控制各3检查 + 2项元数据控制各3检查。原37类腐坏控制完整保留；元数据task-fault控制也复用原有inner-fault，因此不把重复检查当新增独立缺陷类别。

原生子进程退出仍为44×0、28×1、1×2、1×3，包含预期负控/Unsupported/预算控制，没有把它们全部记成普通完成。全队列仍是一条开发seed的有限场景及重放，不是新胜率样本。

- skip24/24、take25/25继续从正常开局经首战实际Victory、原生金币/奖励至下一战Play。HP70、Gold266、永久deck10/11；Pommel Strike永久i31与下一战i35.DeckVersion=i31的关联不变。
- 两路径的route.json与final-state.json相对C166和旧final06仍逐字节相同。完整RNG字段保留；详细/off/minimal和冻结重放对照通过。
- 非法Armaments仍保留4 confirmed /5 dispatched、Block5/Energy2及0合法choice提交；事件/IO/预算/不支持结果保持原分类和真实前缀。
- R2来源/执行方式交叉核对仍通过：6次natural-run、38次api-fixture、30次fault-control；47次live、23次frozen-replay、4次observation-control。原22条错误标签对应的新结果正确，旧标签未改写。
- 15个Mod pin及52个旧兼容验证源文件hash未变。Core实际重跑98项，依赖hash与最终构建一致。旧Mod二进制回归未重新启动，保留分离构建身份，未称为此NativeRun二进制的游戏测试。

`.build/native-build/n2-snapshot-final01`及对应suite保留了第一次完整复跑：65项快照控制通过，但两项元数据对照在更早的fault-control来源门被拒，没到预定检查点，外层明确exit1。修正控制构造后重新冻结final02并完整复跑，没有降低来源或语义门。补充核对脚本也保留了初次沿用旧变更白名单导致的一项范围检查失败；按本轮预定三文件改动纠正后重新核对，原始记录未删。

## 收尾与下一步

公开文档新增本文，更新数据契约及前轮修复报告入口；其他既有文件、旧构建和原始运行证据均保留。文件差异与hash核对见`inspection.json`和`preservation.json`。没有权限拒绝、挂起进程或外部等待，没有提交推送。

本轮完成的是N2剩余必修的本地修复；待Astra复核后，继续既定N1-T/N3及后续步骤4–6，无需用户追加批准。完整run、真实Death、所有房间/隐藏状态、公开Mod接口和策略胜率仍未在本轮验收。
