# M1 固定样本：真实终态策略与差分修复

日期：2026-11-04。证据目录：`.build/runs/m1-acceptance-work06/`。本批最终构建 `F7C4A5B6270122BC0E4551310BD71C3FD7880B28A47B53933D16EE511BC1AD98`（`m1-acceptance-b06-final06`）。本报告只说明本批可核实范围，**M1 整体验收尚未通过，未提交或推送**。

## 为什么之前六条样本停住

五条是候选预测当前回合死亡触发质量保护；一条是累计成员展开 4,915 超过 Lab 返回后 4,096 准入门。它们不能改标签为实际死亡、也不是新发现的牌效 Unsupported。

已核查上游 CombatSolver 0.50.1 的配置与 `SolverController`：当回合死亡及 EndTurn 恶化保护可配置。Lab 保持原 `Guarded` 默认，新增明确的 `RunToTerminal` 选项，复用其一般语义，在支持范围内继续真实执行到终态。没有改搜索评分、对 Boss 特判或替换失败种子。固定六种子的本批配置统一，返回后累计准入为 16,384；beam 8、单成员节点 256、10 秒、DOP 2 保持。它是 Lab 自定义配置，不是上游 Low 档。

每一步仍要求真实状态捕获、合法动作与实例/子选择、父状态及 RNG 不扰动、完整 shadow/native continuation 相等。TimeLimit、取消、无候选、未建模风险、准确性失败仍停止。上游死亡候选只是预测，不证明任何策略都必死。具体规则见 [策略契约](M1_ACCEPTANCE_TERMINAL_POLICY.md)。

## 暴露的真实问题与通用修复

| 问题及保留证据 | 根因及修改 | 已运行验证与边界 |
|---|---|---|
| `eval-01-live-final01`，q636 EndTurn，739 确认，DataError | `CurrentSide` 改变后每回合抽牌/出牌计数缓存未失效；上游历史谓词依赖 round、side、player turn。同步失效既有五类计数缓存。 | 旧/新 setter 各 10 项及原生历史谓词 3 项；后续自然死亡不再出现该差分。 |
| `eval-01-live-final02`，q424 Hemokinesis+，495 确认，DataError | HP 费用被放在主攻击后；原生在 HP1 时先死，不再造成主攻击伤害。移动既有 sealed HP-cost 组至 before-primary，复用伤害 Hook、分叉状态和续算。 | `self-cost-01-final03` 原生夹具 7 根动作、2 步差分：HP10 正常费用及伤害、HP1 致死且敌人不受主攻击。自然 EVAL01 后续真实 Death495。其他两张组内卡及费用 Hook 子选择续算未各自单测。 |
| `eval-03-live-final04` 和 `final05`，q207 PerfectedStrike，248 确认，DataError | 伤害期间缓存的能力数值在死亡移除后才写回，重新产生 FlutterPower。先恢复上游整卡执行 scope/延迟死亡结算仍复现，随后在通用死亡移除前同步 pending amount states，避免晚写回恢复已删除能力。 | `fixture-power-final06` 原生非致死/致死两次 Strike，能力 5→4→移除，2 步完整差分；最终自然 EVAL03 同 q207 精确一致并继续到 Death415。未声称所有复活/死亡 Hook 覆盖。 |

四次真实 DataError、失败的首次 self-cost 编译输入和首次控制预期不匹配文件均保留。没有删除差分字段或把失败降级成成功。自然运行未强行改 HP/资源/牌序；夹具明确标记，不能充当自然路径。

### 独立锁定依赖

游戏仍为 v0.111.0 / `41cef1ea`；RandomForeseer 仍为本地 `0.13.19-native05-reflections`（13 项输入）。15 项 Mod 输入和 52 项兼容源码不变。

| CombatSolver 本地派生身份 | 父版本 | DLL SHA256 |
|---|---|---|
| `turn-history-side.1` | `tainted-root.1` | `E9007C17EFB79647AE1EFBE0BB5D8975637FCD3BE03850239B6830D6DD2788CA` |
| `self-cost-order.1` | `turn-history-side.1` | `962324D9794F7B83FDDD7EBE95E33A013C6E47A47E7BA243292C3FE43AF56F6B` |
| `death-amount-sync.1` | `self-cost-order.1` | `A5AEFA38D53A84B4F67BF71EDA2F3B1EEF7B0EE0FFCE3FC0C09CDE3413D2C35E` |

对应 `prepare-solver-turn-history-inputs.py`、`prepare-solver-self-cost-inputs.py`、`prepare-solver-death-sync-inputs.py` 保留来源与补丁构建方法。全部为 Lab 内独立输入；原 MIT 许可、归属和父版本哈希保留，未部署、未分发游戏二进制或反编译游戏源码。

## 验收和数据口径

本轮已完成的自然终态如下。表内“幕”从 1 开始；底层数据 `act` 从 0 开始。final05 两条还缺最终构建重跑，不能据此把整张表认证为同构建。

| 固定样本 | 实际执行构建 | 真实终态 | 确认动作 | 战斗差分步 | 死亡位置 |
|---|---|---|---:|---:|---|
| EVAL01 | final05 | Death | 495 | 424 | 第二幕第 16 层 |
| EVAL02 | final05 | Death | 215 | 181 | 第一幕第 17 层 |
| EVAL03 | final06 | Death | 415 | 345 | 第二幕第 16 层 |
| EVAL04 | final06 | Death | 498 | 430 | 第二幕第 16 层 |
| EVAL05 | final06 | Death | 467 | 366 | 第三幕第 15 层 |
| EVAL06 | final06 | Death | 406 | 338 | 第二幕第 16 层 |

这些路径在声明的执行策略下得到实际终态，没有把保护/资源停止重标为死亡。不代表每条路径都走过旧轨迹的同一停点、策略提升或完整 M1 通过；三项语义修复也可能改变早期搜索选择，旧新路径不是单变量策略对照。

EVAL03–06 的 **4 对 final06 live/replay 均通过生产检查器及 60 项汇总检查**：合计 1,786 根动作、1,479 步战斗差分；每对分别 5,417 / 6,789 / 6,946 / 5,540 个完整状态与 RNG 观察一致（单侧合计 24,692）。同根动作、子选择、奖励、最终状态、各步父状态与 continuation 一致。报告明确列出缺失的 final06 EVAL01/02，而不是空集合自动通过。

四条 final06 live 合计实际采用 13 个“候选预测当前回合死亡”的动作，随后仍执行逐步差分；EVAL03 有一次成员累计 5,427 节点，超过旧 4,096 准入、低于本批 16,384，实际采用和原生差分通过。这是通用策略/资源门的真实覆盖。EVAL02 自身新路径较早改变，没有重现其旧 4,915 节点查询。

- `policy-controls-final06-dependency-aware.json`：24 项生产准入正负控。新旧默认、准确性与 TimeLimit 等停止门保持；旧证据使用旧依赖，不能拿新依赖对旧 hash 的正确拒绝误称业务失败。
- `death-controls-final06.json`：12 项调用实际终态检查器的控制，包含 2 个合法副本和 10 个错误副本；覆盖完整状态、实际死亡、最后动作/上下文、回调/确认顺序及缺失/重复。它们是证据负控，不是另一次原生死亡。
- 同构建重放比较全体有状态事件（完整 RNG 字段）、根动作/子选择/奖励、实际派发实例、每步 continuation 和父分支、终态回调与原始退出；不只比较最后 HP。
- Replay 仍会重新求解，冻结输入用于准入一致性；**不是关闭搜索的性能基线**。`Comparison=NotCompared` 顶层字段没有自行改写，完整差分由各 `solver-online-step-*` 与复验报告单列。
- `CoreChecks` 98 项执行记录来自本批 final05 检查，Core DLL 与 final06 哈希相同；不能据此声称所有原生回归都已在 final06 重跑。
- `data-query-examples.json` 从 EVAL03 原始日志追溯 Q1 实际致死动作、Q2 原生领取 Feral `i24`→手牌副本 `i147`→a58 真正打出，以及 Q3 构建/输入/决策/耗时资源来源。五项关联检查通过；只证明实际事件与实例链，不估计卡牌胜率价值。
- `telemetry-summary.json` 只整理必须运行的验收日志。成员耗时可能重叠，CPU 利用率及搜索独占墙钟未知；不相减 live/replay 耗时，不新增性能实验。

## 证据完整性与未完成门

上一批脚本曾覆盖 work04 的一个派生控制结果；事故及当前副本保留于 work05，原始轨迹没有因此重写。无法恢复事故前派生文件字节。本批起点封存 work04/work05 共 46,679 文件、6,899,784,481 字节；后续哈希只证明此封存点之后未变，不等于事故前原件认证。

所有本批输出使用新目录。检查器以只读 `--review-full-run` 入口重读，派生汇总拒绝覆盖已有文件。哈希证明来源一致，不单独证明原生执行真实性；真实业务仍由原始任务、回调、状态及进程证据核验。

`binding-final06-closeout.json` 共 47,943 项一致性检查、0 失配：源文件 1,041、编译产物 137、构建附件 16、顶层输入锁 3、受保护 67、旧证据 46,679。这个计数是文件绑定检查数，不能混称业务测试数。

## 本批预算与复验入口

预登记上限为 18 次原生执行 / 90 分钟 / 8 GiB。实际 **18/18**：4 次准确性失败、2 次明确 API 夹具、8 次自然终态 live（包含不同构建的重复）、4 次 final06 重放。全部原生进程已收尾；没有用重复样本增加独立样本数。时间、字节和每个原始退出见 `execution-ledger.json`、`telemetry-summary.json`、`closeout.json`。达到次数上限后没有继续启动新原生运行。

最后一次原生执行在登记后 53.93 分钟结束，封存收尾为 57.10 分钟；18 次原生进程报告墙钟累计 1,497.51 秒。收尾时本批 4,467,753,630 字节（约 4.16 GiB），运行时观测峰值工作集 2,563,923,968 字节；这些诊断数不是隔离性能基准。`native-evidence-seal.json` 逐文件封存 29,975 份本批原生输出、4,429,671,644 字节，seal SHA256 为 `0CA18F8AA48B3BAE3AC7009AE99453776C1089A004BBBBCC9B92E01A6ABFCF1B`。未归档删除、重写旧目录或隐藏四次 exit1；14 次 exit0 包含夹具，不能统称 14 局成功。

只读生产复验入口（示例）：

```powershell
dotnet .build/native-build/m1-acceptance-b06-final06/bin/Release/net9.0/OfflineSearchHarness.dll --review-full-run .build/runs/m1-acceptance-work06/eval-03-live-final06
```

实际新执行需使用新的输出目录和明确登记的新预算；不可重用旧目录。原始 wrapper 命令、seed、scenario、solver plan 与冻结 replay 输入 SHA 都在 ledger，依赖及生成链在各 `build-manifest.json`。本批汇总、数据查询及哈希复核入口拒绝覆盖已有输出；其他控制脚本再次使用前必须核对输出路径，复验应写到新的审查目录。

本批修复消耗了预登记原生额度。EVAL01/02 的 final04/final05 实际死亡必须与最终 final06 表分列；不能拼成六样本最终同构建通过。最终构建尚需补这两条 live/replay、Guarded RunVictory 回归及主方案 N6 全部适用门和独立验收。旧完整胜利证据保留，不能替代当前变更后的统一验收。具体接续见 [下一步](M1_ACCEPTANCE_TERMINAL_NEXT_HANDOFF.md)。
