# CombatSolver 选择与药水首批独立审查

2026-11-04。结论：**原五个动作绑定反例修复通过；固定原生夹具的业务事实成立；新增检查器有两项必修，尚不能关闭本批验收或第 2 步。**

本轮只读审查生产实现，使用已冻结文件和生产 `--review` 入口处理明确标记的派生副本。没有新原生游戏执行，没有重跑原生矩阵，没有修改原始证据、生产代码、角色或存档，没有提交推送。

## 已证实的结果

- 原五个反例全部返回非零：错误目标 CombatId、目标 index、牌 occurrence、StateKey、EndTurn 多余牌字段。四条旧原始路径均接受。旧 live/replay 的每步动作、完整前后状态、continuation 和 final-state 独立比较一致。
- 新成功原始执行是 `.build/runs/solver-choice-potion-exec-04/`：6 次实际派发/确认，API fixture、live、`Ongoing / SliceBoundary / NotCompared`。两个步骤另有精确差分证据。
- 升级 TrueGrit 实际消耗 1 能量、获得 9 格挡，从原生候选中选择 index 1 的 i31 并耗尽，另一个 Defend i32 留在手牌。该 token 的 OptionOccurrence=0，不能称已覆盖非零重复实例。
- FirePotion 原生使用后敌人 HP 43→23，实际药水移槽，能量保持 2。预测复用 fork 槽对象和 `PotionExecutionSupport.Prepare/Complete`；原生使用 `EnqueueManualUse` 并等待实际 ActionExecutor。
- 两步完整 continuation 相同，父 fork 不变；已记录的 run/player/event RNG、永久牌组、HP、maxHP、金币和遗物保持一致。实际查询分别展开 809、119 节点，来源状态不扰动，两次返回的首动作都是 Defend。
- 因此，这两个动作是**固定 API 夹具动作**，不是在线求解器采用的建议。15 个托管 token/selector 负控是同进程 API 验证，不是 15 条自然错误路径。现有 `SolverLoop` 仍明确拒绝选择和药水。

独立检查共 24 次生产 reader 调用：7 个正常/等价情况接受，13 个错误情况拒绝，4 个错误副本被误接受，归为下述两项根因。另有 57 项来源/业务/锁核对、8 项旧重放与预算事实核对、3 个构建 ID 重算全部通过。计数不代表整场覆盖。

## R1：子选择描述没有绑定真实手牌的模型和升级

位置：`tools/native-run/NativeSolverFixtureContract.cs` 第 51–61 行。

当前 token 的模型/升级只与 `children[].candidates[]` 的描述互相比较；绑定真实 `before.combat.hand` 时只检查 id 存在和 occurrence。候选 StateKey 虽绑定捕获映射，但并未把候选的 model/upgrade 绑定实际手牌对象。

两份实际误接受副本均只改 `solver-fixture-step-1.json`，原生 trace、完整状态、映射 key 和 continuation 保持不变：

| 副本 | 改动 | 应有结果 / 实际结果 |
| --- | --- | --- |
| `child-upgrade-vs-actual-hand` | 主计划 token、child token、候选 i31 的 upgrade 一起由 0 改为 1；真实 i31 仍为未升级 Defend | 拒绝 / exit 0 接受 |
| `child-model-vs-actual-hand` | 三处模型描述改为 Bash；真实 i31 和 key 仍为 Defend | 拒绝 / exit 0 接受 |

最小修复：以候选 id 取得当前原生手牌记录，检查 model、upgrade 与候选和 token 一致，再检查 key、实例 occurrence 及实际返回/耗尽对象。当前窄夹具没有出牌中生成候选，不需要新增游戏规则解释器。保留显示 Title 变化合法的正控，以及原有缺失/错误选择负控。运行时 `NativeSolverChoices.Resolve` 会查真实 CardModel；本发现是证据读取缺口，没有证明本次实际执行用了错误牌。

## R2：药水映射只自洽，没有绑定实际派发槽位及原生模型

位置：`tools/native-run/NativeSolverActionContract.cs` 第 53–54 行。

当前检查将计划的 slot/model 与 `input.binding.potions` 比较，再将 id 与派发 potion id 比较；缺少 `action.PotionSlot == decision.slot` 以及映射 model 与 `nativeState.potions` 中同 id 实例的核对。

两份实际误接受副本只改 `solver-fixture-step-2.json`，原生请求/派发、完整状态、continuation 不变：

| 副本 | 改动 | 应有结果 / 实际结果 |
| --- | --- | --- |
| `potion-plan-map-vs-actual-slot` | action.PotionSlot 和映射 slot 同改为 2；真实请求和派发仍为槽 0 | 拒绝 / exit 0 接受 |
| `potion-plan-map-vs-actual-model` | action.PotionId 和映射 model 同改为 BlockPotion；真实持有对象 i28 仍为 FirePotion | 拒绝 / exit 0 接受 |

单改 dispatch.slot 的控制已被根请求/派发一致性门拒绝，但不能代替上述计划到实际动作的交叉绑定。最小修复是检查计划、映射、派发槽位一致，且同 id 的原生持有药水 model 一致；保持槽位/实例唯一性并拒绝不存在对象。无需手工重算任何药水效果。

这四个反例保留实际业务记录作为反证，只改附属计划/候选描述，属于既定有限身份契约；不要求检查器识别任意整份日志协同伪造。

## 版本、来源与预算

| 用途 | 已重算并核对的构建 ID |
| --- | --- |
| 成功原生生成器 final06 | `6000695356411BEEB98FD7A4CB27EF5778A41FBDEAF50FF2C22CBC6165767447` |
| 本轮实际调用的 checker10 | `52CBA2FA019E799A89A66E8A07DD94EBE50F2A885AEF48AACB5E331A7CFC7328` |
| 默认原生配置编译/API 构建 | `F29C8F9B23E7EDF60D9C02A36E69FD5D70DF8493D680340D7793FE9C1DA28849` |

final06 与 checker10 的 compiled/source 差异确实只有 `NativeSolverFixtureContract.cs` 和 `NativeTraceContract.cs`，内容为检查器契约修复。排除这两文件的生成链指纹一致：`E421E2E92F544F5AE155B5773C135B5433B279DECFB509F4E64ABA22BDAB082F`。成功 gameplay 属于 final06，不能归为 checker10 或默认配置重新执行。两个新身份问题也可按纯检查器修改分层修复。

独立重算 inputs/compiled/artifacts；最终源码、826 个固定 solver 源文件、6 个依赖程序集、MIT/manifest、15 个输入和 52 个兼容源码均匹配。当前 61 个原始执行文件、2721 个工作证据文件以及此前 94/804 文件锁保持一致。哈希证明一致性；执行事实另外由原始退出、任务与动作记录、原生调用链、实际结果及旧重放支持。

4 次注册尝试没有扩额：01 为 EngineError（5/5 前缀，错误 live 药水对象进入 fork，未派发药水）；02 为 DataError（6/6 前缀，原生实际 20 伤害而低层模拟路径未执行该药水效果）；03 是 manifest 未落盘的包装器前置失败，没有原生结果；04 完整成功。总计 3 个原生进程约 22.09 秒，均在 120 秒硬门内，第五次调用确实在启动前拒绝。记录的保守证据量约 0.54 GiB，低于 2 GiB；所有尝试在 20 分钟截止前结束。

失败未被重新分类成通过，03 也没有返还次数。新夹具没有完整重放，必须保持 `replayExecuted=false`。默认配置本批只有编译/API 和旧证据重读，没有新原生烟测。隔离记录是 MemorySaveStore、ShouldSave=false、Steam 未初始化、无 UI 实例、单机网络服务、零遥测，MyMod/BaseLib 未加载。

## 最小后续交接

1. 先修 R1/R2，共享按实例查找和比对的逻辑，复验四份反例、旧五反例、正常/等价副本和适用完整原始证据。仅检查器变化无需新原生游戏执行，也无需重跑全矩阵。
2. 抽取当前已有的单动作预测与原生执行流程供 `SolverLoop` 复用。使用 fork 自身药水槽、`PotionExecutionSupport`、`GetActionChoicesInExecutionOrder`、`BeginActionChoices/EndActionChoices` 和有范围的 `NativeSolverChoices`；保持未支持 capability 明确拒绝，不复制搜索算法。
3. 在实际采用 `SolverQuery` 返回首动作的位置接入选择/药水。先以真实有序候选与 token 验证当前边界；原生执行完成后必须完整消费所有子选择、核对 continuation/父 fork/RNG，再重新求解。不能因为方法名相同就放开全部 FromHand 语义；首批仍按已验证效果/来源/时机能力门。
4. 新批可预登记不超过 4 次原生执行、20 分钟、2 GiB、单进程 120 秒，优先两条窄场景各 live/replay。允许以明确 API 夹具建立输入，但采用动作必须来自未篡改的求解结果，不能强迫选牌或更换评分。预算内未实际选到某种能力就记未覆盖。至少证实选择和药水各一次在线采用及下一次查询；重复实例的非零 occurrence、错误/晚到选择的真实未确认动作前缀按受影响范围补齐。
5. 连续/回合开始选择、药水槽替换、终态及整场仍列后续门。终态支持未建立前继续明确停止；通过后再回到 M1 自然路径，最终同构建全量验收保留。

本轮无环境或审批阻塞。R1/R2 是进入在线通用部署前的定向修复项；没有证明原生 TrueGrit/FirePotion 业务失败。第 1 步结论保留，第 2 步和完整 M1 均未完成。

## 可复核证据

目录：`.build/runs/solver-choice-potion-independent-review01/`。

- `facts.json`、`additional-facts.json`、`build-id-checks.json`：独立事实、旧 live/replay、冻结重算。
- `controls.json`：20 次生产 reader，包括原五反例、4 条旧路径、新夹具和首个升级误接受。
- `related-controls.json`：模型、槽位和合法显示标题的 3 次控制。
- `potion-model-control.json`：真实模型矛盾的第 24 次调用。
- `copies/<上述反例名>/` 和同名 `.log`：实际派生副本与原始 reader 退出/输出。全部标为控制副本，不是原生运行。
- `closeout.json`、`immutability-after.json`：最终结论和审查后完整性核对。

`review.py`、`related_controls.py`、`additional_facts.py` 记录可复现操作。再次运行时须使用新审查目录；脚本拒绝覆盖旧副本。只可把新派生副本传给生产 `--review`，避免修改原始证据中的 review 输出。
