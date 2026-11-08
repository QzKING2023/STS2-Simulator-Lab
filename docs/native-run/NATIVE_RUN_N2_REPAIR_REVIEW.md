# N2 R1/R2 复审：原修复成立，R1 仍缺快照存在性门

日期：2026-11-02。复审 [R1/R2 修复报告](NATIVE_RUN_N2_REVIEW_REPAIRS.md)及实际冻结源码/证据。**结论：R2通过；R1原六类错误已修复，但必要状态快照可以整体缺失而仍被验收，故N2本轮仍未通过。** 剩余必修限定为该存在性检查，不新增房间/策略/全run指标。

## 已独立确认的部分

完整冻结ID：`C16648CAF5F9B11E96472FB7C46F0549F802CCCC6A1AD44126AF5EA699C58C63`。

NativeRun DLL SHA256：`4031C3BE651D2364DAFD9CB02481C7BF785D85FEAD40359BD6A29983529C6D0E`。

实际重新计算Build ID、32条source、3个inputs、17条compiled、7个artifacts均一致。15个Eleanor锁定输入、旧冻结的52个兼容验证源文件均匹配；Core98原始运行日志与最终Core依赖hash一致。没有仅凭HEAD替代dirty源码。

最终suite确有74个进程和379条通过检查；补充检查文件确有114条通过检查。实际原始退出分布是44个0、28个1、1个2、1个3，均与对应控制预期相符。预期失败没有改记为普通完成。本次独立来源/进程核对164项通过，与下面的reader控制分别计数。

本次将**全部37种腐坏控制及4个正常结果复制到新的审查目录**，重新调用该冻结DLL的实际`--review`，原文件不变。37种错误副本全部返回1，4个正常副本全部返回0。读取并核对了错误副本的实际修改内容、同步重建的seal，以及生产调用链 `NativeEvidence.Review → NativeTraceContract.Accept/Finish`；不是只执行测试替身。

| 原审六类错误 | 本次实际reader结果 |
| --- | --- |
| 缺全部history | exit1，Missing/unexpected detailed history |
| runRng=null | exit1，Null/nonobject runtime RNG runRng |
| fixture assertion/contract=false | exit1，Failed/duplicate fixture assertion |
| 缺choice-returned | exit1，Confirmation before child choice/task completion |
| 错误choice root | exit1，Choice owner/order mismatch |
| 提前根动作确认 | exit1，Choice root/context mismatch |

这些错误在具体语义门拒绝，检查前已经重新计算链/footer/seal并同步内嵌result.trace，不依赖通用seal不一致才失败。额外同名对象错配、历史同数量重复/乱序、缺checkpoint及任务归属控制也由实际reader拒绝。

R2的登记表将来源和执行方式独立计算。独立按实际scenario/control/replay/mode核对74份process/header/result，22条原误标对应的新结果正确：18个fixture冻结重放、2个fixture观察对照、2个损坏输入冻结重放；softlock也明确是控制。全部来源为6次natural-run、38次api-fixture、30次fault-control；执行方式为47次live、23次frozen-replay、4次observation-control。6次natural-run包含重放/观察对照，仍只有原开发seed，不是6个自然独立样本。

自然skip仍24/24、take仍25/25，同run实际首战Victory→原生金币/奖励→下一战Play，HP70/Gold266，deck10/11。Pommel Strike永久i31与下一战副本i35.DeckVersion=i31存在。两路径route和final-state相对旧final06逐字节相同，真实RNG字段未丢失；新增私有候选映射、奖励完成记录与历史分段未改变这些执行状态。晚到非法Armaments与其他负控的失败前缀仍按原报告保留。

## 唯一剩余必修：先要求快照存在，再校验其内容

位置：

- `tools/native-run/NativeEvidence.cs:101`：只在 `detail.TryGetProperty("state", ...)` 为真时校验必要字段。
- `tools/native-run/NativeTraceContract.cs:32`：同样只在state存在时执行CheckState。
- `tools/native-run/Program.cs:70`：`Observe`对这些事件固定写入`{facts,state}`，表明状态是该事件契约的组成部分。

因此字段内部的null或缺失已经拒绝，但删除包裹这些字段的整个state会绕过两层检查。这个缺口仍属于R1的“必要事实/完整RNG/前后状态”要求，不是新增远期功能。

本次新增两个有限语义负控，均从最终构建的正常`slice-skip-1`复制，仅删除指定state属性，其余动作、历史、RNG配置声明、终局及结果均保持。重新计算hash链/seal并同步result.trace后，调用同一生产reader：

| 负控 | 改动 | 实际结果 | 要求 |
| --- | --- | --- | --- |
| `control-missing-all-state` | 删除全部182份事件状态快照 | exit0，仍生成passed与effects.complete=true | 必须拒绝 |
| `control-missing-one-boundary-state` | 仅删除第一条confirmed的状态快照 | exit0，仍通过 | 必须拒绝 |

原生history仍有105条、计数/顺序正确；因此此负控不能靠历史数量或RNG字段内部校验发现。单个确认边界缺状态也通过，说明并非只有整体重写证据才会发生。哈希链仍有效；本任务不要求防御恶意修改所有独立事实，但生产检查器应识别已声明必需快照缺失。

证据目录：`.build/runs/n2-repair-independent-review01/`。

- `snapshot-presence-controls.json`：两个新增控制的删除数量、实际退出与日志。
- 各`control-missing-*/trace.jsonl`、`trace-seal.json`、`result.json`及`trace-review.json`：隔离错误副本和实际错误通过结果。
- `production-reader-rerun.json`：37个旧负控/4个正常副本的独立reader复跑。
- `binding-and-existing-repairs.json`、`actual-evidence.json`：164项来源核对、六类语义拒绝、22条来源对应及实际自然结果。

## 最小修复交接

1. 给本轮事件kind建立明确的快照要求，按`Program.Observe`的实际契约列出需要state的类型，例如new-run、dispatch、confirmed、combat-history、生命期/奖励/fixture观察事件。缺state或非object必须先以明确DataError拒绝，之后继续现有完整内容检查。header、identity、choice/task等原本不带状态的元数据事件允许按其各自schema存在，不能一律要求state，也不能为缺快照合成默认值。
2. 原`CheckState`、RNG流集合、历史epoch/index、对象对应和请求顺序检查保留。避免两个条件式检查继续遗漏包裹字段；使用同一事件契约定义可减少重复规则。
3. 新增上述两个负控及state=null控制，重建所有传输hash/seal并验证实际生产reader的具体“缺必要快照”拒绝原因。正常有状态事件以及正常无状态元数据均需通过，off/minimal仍保留必要边界快照。
4. 建立新冻结，完成同产物的N2必需矩阵与N0/N1适用回归；保留本轮379/114及错误副本为历史，不改写旧结果。不需要扩新增Mod、触发图、所有隐藏状态或全run。

这是一项检查器/schema范围的收尾，未发现需要改变原生玩法、扩大宿主权限或重新设计执行架构的依据。六步用户授权继续有效，下一轮可直接修复，无需向用户重新申请。

## 步骤4交接状态与操作范围

由于N2仍有上述必修，本轮**暂不将步骤4标为可进入的实施交接**。N1-T/N3依赖与范围沿用[M1方案](../milestones/MILESTONE_1_NATIVE_RUN_PLAN.md)和[上一轮审查的后继表](NATIVE_RUN_N2_REVIEW.md)：修复复审后，先安全验证act-ready/EnterNextAct、TheArchitect原生回调/终态顺序的标注fixture，再推进真实休息/升级、事件、商店、遗物/药水、精英/Boss及连续节点。自然整幕/完整胜利分别仍属后续门，早死与尾部fixture不能替代。

本轮只新增审查文档与Lab内证据副本/脚本；未改实现、原始证据、锁定输入、正式路线图、角色或游戏安装/存档，未启动游戏UI、未提交推送。只运行冻结DLL的离线证据reader，没有重新启动74个原生run。当前无权限拒绝、审批等待或挂起进程；停止点是这一项已复现的N2验收缺口。
