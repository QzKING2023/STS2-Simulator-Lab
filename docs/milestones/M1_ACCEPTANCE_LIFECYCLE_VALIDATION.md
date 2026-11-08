# M1 第三批验收：先古、阶段选择与生命周期

状态：**有界实现与回归已收口，M1 整体验收仍未通过，未提交/推送。** 本批没有外部权限或环境阻塞；后续为已授权范围内的技术缺口。上一批18/18及本批失败产物均保留。

## 固定来源、预算与实测

- 最终宿主冻结：`1C0C5855F33AABBF144A61CFAFD3C1911AD981444FE6939A67DB1A74921F407E`，目录 `.build/native-build/m1-acceptance-b03-final07/`。编译0错误、4条既有警告。
- 游戏仍为 v0.111.0 / 41cef1ea；CombatSolver固定衍生DLL `B6A3AE711C0CA2338BD6BF0710128FED47278D5BE34D2ABCFD4F00A3392A87CC`，RandomForeseer固定native04-slippery输入。本轮没有修改搜索评分、档位、线程数或固定种子。
- `frozen-bindings.json`：1169项源码/输入/编译及输出哈希匹配；15个输入和52个兼容源码均未变。哈希只证明一致性，执行真实性仍由受控进程和原始记录支持。
- 原生执行 **17/18**，至最后原生退出 **51.58/60分钟**，当前证据约 **2.53/6 GiB**。`execution-ledger.json` 每项都有实际退出；没有未确认或运行中的原生进程。预算按批次登记，未为采样增加性能实验。
- 最终同构建详细/最小观察重放均 **RunVictory 499/499**；393步完整战斗差分与父状态检查通过，499确认状态相等，全部4070个共同完整状态（含RNG）相等；详细模式另有3560条战斗历史，共7630状态。未删状态字段来制造相等。
- 同构建 TrueGrit/FirePotion夹具2步差分、真实致死夹具Death5/5及生产reader通过。死亡是明确API前置夹具，不是自然样本死亡。
- 21项最终比较检查、98项Core检查（Core DLL与宿主一致）、7项运行计划控制通过。生产生命周期有限正负控46项通过（14合法/等价、32拒绝），范围为先古交易、子房间、修改金币、阶段选择和终态隐式选择；原始脚本调用参数错误结果保留，不计作语义负控通过。

## 本批实现与真实覆盖

1. **Stratagem 回合开始选择。** Lab等待区间涵盖下一玩家回合开始，原先错误复用上游UI部署的EnemyTurn截断列表。现在保留完整TurnStartChoices，复用上游语义选择与实例Token；只有实际 StratagemPower.AfterShuffle 原始异步调用栈、真实玩家Power/Draw候选、原方法无未知补丁时接受。EVAL05 final06实际进入并执行，6个运行时拒绝/2个合法匹配控制通过；最终检查器重验其证据。未宣称支持任意玩家回合选择。
2. **先古真实拾取交易。** PaelsHorn原生加入两张Relax，TouchOfOrobas原生以BlackBlood替换BurningBlood。`HasUponPickupEffect=false`并不保证无拾取副作用；不再把该标记当纯追加。只登记已核验实际方法/版本，观察原CardPileCmd.Add与RelicCmd.Replace原Task及新旧实例；完整前后状态/RNG按实际交易核对。EVAL02/03通过原停点，未知先古仍拒绝。
3. **宝箱真实金币修正。** 记录原GainGold输入、Hook.ModifyGoldGained结果和真实Gold setter，不再用名义金额硬等最终差额。EVAL04通过原宝箱停点，正常与缺失/错配/静止状态等控制通过。
4. **事件入口复用原生。** WarHistorianRepy、ColorfulPhilosophers、ColossalFlower经原方法审计后走原生选项/奖励，不复制牌池效果或随机逻辑。EVAL01/02/03真实通过对应节点。RF尚未逐节点实证的预测标未知，原生可执行不等于多步预测可用。
5. **终态隐式选择。** 原FromCombatPile在IsEnding时直接返回空集合，已规划的Headbutt选择不能强行搬牌。仅在实际原生结束门和空隐式请求中消费取消计划，保留完整终态差分、真实胜利回调及空返回；EVAL05通过旧81动作停点，最终生产检查器9项控制通过。
6. **CrystalSphere尚未验证。** 代码仅替代展示入口，拟调用原CellClicked和原完成Task；补充旧前缀实验在16确认动作后、下一根派发前因当前选择与冻结动作不匹配停止。未到达Crystal，不算任何棋盘/奖励正向覆盖。该补充实验不替换固定EVAL06。

上述改变涉及19个宿主/检查器源码，具体清单在 `changed-source-scope.json`。原始游戏反编译参考只保留在忽略的 `.build` 中，不作为项目源码发布。未知补丁拒绝门保留，但新增先古/Crystal方法逐项实际Harmony注入负控本批未全部执行，不以静态门控代替该证据。

## 六固定样本最新结果

| 样本后缀 | 实际生成构建 | 确认/派发 | 结果与准确含义 |
|---|---|---:|---|
| 01 | final06 | 633/634 | Unsupported；第三幕ThisOrThat显示损失6、实际损失5，当前无修正契约拒绝 |
| 02 | final07 | 381/381 | BudgetExceeded / exit3；ColorfulPhilosophers后下一战触发既定搜索aggregate返回门 |
| 03 | final07 | 440/440 | Unsupported；ColossalFlower后被当前回合预测死亡保护停止，非真实Death |
| 04 | final06 | 383/383 | DataError；IronWave击杀后Tainted清除时机真实差分，保留全部字段 |
| 05 | final06 | 120/120 | Unsupported；EYE_WITH_TEETH复活Heal未建模，未派发下一候选 |
| 06 | final06 | 339/339 | Unsupported；LiquidMemories牌堆选择被本地生成药水能力门拒绝 |

不能将这些混合构建的进展拼成“最终同构建六样本通过”。同种子重复也不是独立统计样本，未丢弃/替换任何失败种子。最终全量验收、独立复核及发布仍待完成。

## 检查器、失败与信任边界

`prefix-reviews-final07.json`记录最终reader重读六个final06前缀均exit0，**只是有限结构一致性**；其中EVAL04的原始DataError仍是失败。EVAL02 final07的生产reader则在同一aggregate预算门返回exit1，原生原始退出仍exit3，不能记成reader通过或原生执行错误。`final-production-reviews.json`分别保留。

当前损坏控制针对已声明契约，不无限扩展协同伪造语义；原生Task之间的真实业务变化不要求静止。新增命令观察没有替代实际奖励、拾取、资源、随机或清理效果。MemorySaveStore、SaveProgress/replay离线拦截、无UI/Steam均继续；没有访问真实存档或操作其他项目。

## 复现与后续

原始结果根：`.build/runs/m1-acceptance-work03/`。`closeout.json`、`execution-ledger.json`、`final-comparison.json`、`frozen-bindings.json`、`telemetry-summary.json`为收口入口。

在Lab内使用固定构建检查原始证据：

```powershell
dotnet .build/native-build/m1-acceptance-b03-final07/bin/Release/net9.0/OfflineSearchHarness.dll --review-full-run .build/runs/m1-acceptance-work03/dev-detailed
pwsh -NoProfile -File .build/runs/m1-acceptance-work03/lifecycle-unit.ps1 -Build final07 -Cases transactions-unit -Output transactions-review-new.json
```

旧执行目录不可覆盖；原生运行须先登记新预算和输出。下一轮按 [技术交接](M1_ACCEPTANCE_LIFECYCLE_NEXT_HANDOFF.md) 优先处理真实Tainted差分、复用上游药水选择、事件修正账本、复活Heal与明确预算/死亡停止政策，再恢复六样本和最终同构建门。

性能数据只整理原验收日志的耗时、查询、成员工作量、动作/楼层/结果、峰值工作集与策略轨迹。在线与重放不同，不能简单相减推算搜索成本；成员时长可能重叠。缺失系统资源指标标未知。未启动性能/策略新里程碑。
