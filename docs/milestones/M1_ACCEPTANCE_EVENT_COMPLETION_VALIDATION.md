# M1 第五批验收：Reflections、CrystalSphere 与固定样本停止分类

日期：2026-11-04 UTC。结论：本批事件实现与定向回归完成，**M1 整体验收尚未通过，不提交或推送**。原始证据位于 `.build/runs/m1-acceptance-work05/`，机器汇总见 `closeout.json`。下一轮见 [事件完成之后的验收交接](M1_ACCEPTANCE_EVENT_COMPLETION_NEXT_HANDOFF.md)。

## 预算与构建身份

预登记 `plan.json`：16 次原生执行、75 分钟、6 GiB；实际 **16/16**，最后一个原生进程在 18:25:23 UTC 退出，距登记约 26.75 分钟。证据约 2.56 GiB，精确值见 closeout。所有进程已退出；未增加性能实验。上一批 18/18 和失败运行保留。

| 身份 | SHA256 构建标识 | 实际用途 |
| --- | --- | --- |
| generator final01 | `AAD6239FA558CCB02DB13AE59158A7462AEF68EBE3F2D4F769B1AD619D26269E` | 四事件 live 夹具、六固定样本 |
| generator final02 | `07B58FFD3AAAF66455F436DD3CC3BB53AE53A7F7262CF5C42CB926EE127AABD3` | 四事件冻结重放、两整局回归 |
| checker final03 | `504F4B5A0283AD5232C36364231B3A96A7AA7058D06FD6EED71D76AC7BD280A5` | 最终生产检查器重读；未启动原生执行 |

final01→final02 仅两个编译检查器文件变化；final02→final03 仅 `NativeRoomContract.cs` 的夹具检查变化。`final-generator-binding.json` 保留原始生成器源码、输入和产物指纹，`final-binding-final03.json` 的 1,255 项哈希核对通过。哈希证明文件一致性，执行事实另由受控进程与原始轨迹证明。此次分层重读不替代 M1 最后一次同构建完整验收。

游戏固定 v0.111.0 / 41cef1ea；CombatSolver 0.50.1 `tainted-root.1` DLL SHA256 `F5B61652AB4809C8C6AC30EB376916643F1014CF93E5988F28576DC3E0DC04BC`。15 个原锁定输入和 52 个兼容源码保持。新增独立 RandomForeseer 0.13.19 `native05-reflections` 快照含 13 个输入，旧快照未覆盖；复制脚本复现的 14 个文件（含 lock）字节一致。

## 实际事件覆盖

- **Reflections**：审计真正覆盖的 `OnRoomEnter` 及异步方法，执行其原生无 UI 分支。Touch 复用 RandomForeseer 的克隆 RNG/卡牌投影，核对原实例升级和完整事件 RNG；重复预测不改变父状态。Shatter 执行原生复制与诅咒命令，独立检查 10 张原牌→21 张牌、原实例保留、新克隆身份及 BadLuck。Shatter 预测明确未知。
- **CrystalSphere**：复用既有屏幕入口桥，仍由原生棋盘、点击、完成任务及 OneOff 奖励驱动。三个及六个点击分别完成；实际金币 349→298，以及另一分支金币不变、Debt/原生奖励入牌组。补足驱动任务失败传播与取消等待；该异常注入尚未进行原生验证。
- 四个场景均为显式 **API 夹具**：正常 Neow 后使用调试房间入口，必要金币/升级通过原生命令建立。不能称为自然遇到 CrystalSphere 或覆盖全部棋盘/工具/奖励。
- 四个 live 和四个 frozen replay 均 5 个确认动作、exit 0、SliceBoundary。完整状态/RNG 观察分别 37、37、67、85 项一致，路线、选择、奖励与最终状态一致。它们是 final01→final02 的**跨构建重放**，生成逻辑不变，不称同构建运行。
- EVAL04 的自然 Reflections 原先在 622 确认/623 派发处 Unsupported；现在真实执行原策略 Shatter 并达到 **691 确认动作**。前 622 个确认状态及 RNG 一致，没有更换种子或事件选项。

证据：`event-fixture-evidence.json`、`event-replay-comparison.json`、`sample-prefix-comparison.json`、`fixture-readers-final03.json`。

## 六固定样本：没有把策略停止当终局

六条路径全部由 final01 生成，固定种子、策略、搜索预算和并行度未改。累计 3,100 个确认动作、2,594 步实际 native/sim 差分。表内幕号从 1 开始；所有停点玩家仍存活，原生终态均为 **Ongoing**。

| EVAL | 确认/派发 | 差分步 | 幕/层 | HP | 原始退出与原因 |
| --- | --- | --- | --- | --- | --- |
| 01 | 733/733 | 630 | 3/15 | 16 | exit 2，候选预测当前回合死亡，策略拒绝派发 |
| 02 | 381/381 | 319 | 2/13 | 86 | exit 3，返回总展开 4,915 超过 Lab 4,096 门 |
| 03 | 440/440 | 368 | 2/16 | 26 | exit 2，当前回合死亡预测保护 |
| 04 | 691/691 | 591 | 3/15 | 4 | exit 2，当前回合死亡预测保护 |
| 05 | 466/466 | 365 | 3/15 | 7 | exit 2，当前回合死亡预测保护 |
| 06 | 389/389 | 321 | 2/16 | 10 | exit 2，当前回合死亡预测保护 |

最终检查器接受五条合法停止前缀；EVAL02 仍明确拒绝完整执行声明，保留原始 TimeoutException/预算分类和 exit 3。`final-readers-final03.json` 的预期分类检查通过，不等于这些路径通关。

`policy-budget-diagnosis.json` 对照真实查询和上游配置：当前 Lab 自定义 beam 8 / nodes 256 / 10 秒 / 并行 2，并非上游 Low 档。EVAL02 的 4,915 展开由 OpeningPowerRouteMember 4,608、Primary 256、Refinement 51 构成；4,096 是返回后采用门，不是精确熔断或墙钟超时。上游 `StopFullAutoOnDeathTurn` 默认开启；有限搜索的 OnlyDeathRoutesFound 不能证明所有合法策略必败。本批没有为通过而关保护或改评分。

## 整局回归与检查器修复

- final02 的 detailed/minimal 两次冻结整局路径均真实 **RunVictory：499 动作、393 步差分**。499 个确认状态和 4,082 个共同完整状态/RNG 观察一致。detailed 共 7,642 个状态观察；minimal 按既定模式减少事件，不删除快照字段。
- 两次均保留在线求解调用，因此不能将耗时与 live 简单相减得出搜索成本。`telemetry-summary.json` 仅整理必需运行既有日志，缺指标标未知。
- 受控隔离记录为 MemorySaveStore、shouldSave=false、Steam 未初始化、无 UI；Progress/Replay 计数是拦截次数，不是用户存档写入。
- 自查关闭六个错误接受案例：Reflections 实际 RNG 归属、Crystal 整组记录必需及真实 RNG/完成绑定。旧失败 `boundary-controls-before.json` 保留。
- 八个事件原始轨迹曾被旧 Rest 专用 room-assertion 格式错误拒绝。final03 为明确登记的四个夹具定义前后金币、事件身份/完成、唯一原生选择及 Shatter 实例契约，原 Rest 契约保留；不改变原始运行。旧检查失败保留。

最终 **142 项有限生产/反射控制全部通过**：事件 26、边界 9、完整轨迹夹具 20、实际 Harmony 来源审计 7、历史契约 80。Harmony 控制没有执行原生游戏业务。Core 98 通过，Core 产物与 final02/final03 相同；整局比较 19 项通过。未重跑无关全量原生矩阵，也不宣称本批已完成旧 N2/Mod 全套回归。

## 证据输出事故

复用控制脚本时，final02/final03 的四项条件谓词检查误保留上一批输出路径，覆盖了 `m1-acceptance-work04/conditional-controls-final06.json` 这一**派生控制结果**。没有覆盖原始原生轨迹或失败运行。已保留当前字节和哈希，修正为 work05 新路径并重新执行四项纯谓词控制；无法证明被覆盖前字节，不能声称全部历史派生文件未变。详见 `derived-evidence-output-incident.json`。后续脚本复制必须校验读写路径分别所属批次。

## 剩余门与复现

仍需独立定义通用有界运行到终态策略和搜索成员预算，保留当前受保护基线，再推进固定队列实际终态和同构建重放。最终预登记节点/终态/异常/隔离/数据查询及适用历史回归尚待统一收口；一个固定胜利路径不代表全内容支持。没有外部权限阻塞；本批执行预算已耗尽，下一批需先登记，不启动性能里程碑。

本地重读入口（不启动游戏）：

```powershell
dotnet .build/native-build/m1-acceptance-b05-final03/bin/Release/net9.0/OfflineSearchHarness.dll --review .build/runs/m1-acceptance-work05/event-reflections-touch-final01
dotnet .build/native-build/m1-acceptance-b05-final03/bin/Release/net9.0/OfflineSearchHarness.dll --review-full-run .build/runs/m1-acceptance-work05/eval-04-final01
pwsh -NoProfile -File .build/runs/m1-acceptance-work05/fixture-reader-controls-final03.ps1
```

运行清单、参数、原退出和时间见 `execution-ledger.json`、各运行的进程报告；原始重放输入已留存。源变更集中在 Reflections、CrystalSphere、事件夹具/协议、对应检查器及 RF 输入复制工具，未修改角色项目或保护的兼容实现。未提交、未推送。
