# Tanx 新遗物身份模型绑定修复

2026-11-03。R1已完成实现与分层验证，待独立复审。两份原反例由旧检查器exit0变为新检查器exit1，错误原因均是登记模型与实际取得模型不匹配。真实第三幕路径最远仍是Fabricator Death418；本轮没有新增原生对局，Boss、RunVictory及完整M1仍未完成。

## 改动与构建身份

只修改 `tools/native-run/NativeMajorContract.cs`：身份字典同时保留具体类型和不可变model值；Tanx取得时，具体类型匹配实际选项，登记model必须是字符串并与本次新增持有实例model相等。原有选择键、取得状态、实例及完整快照校验继续执行。非模型对象合法的null登记不受此取得规则约束。MapRoom类型检查仅适配字典结构，语义不变。

- 新检查器冻结：`E1A092E0D80D318100627B1BDB4F99FC4C96232230EBCB675F479995E5C3351D`。
- 原第三幕生产构建仍是 `2EE02774AE02C77D25AA310382F08C5930580987D78F4DB7D70649A01A664FD3`，原报告和producerBuildId未改写。
- 新清单：`.build/native-build/act3-model-repair01/build-manifest.json`，94源码、58编译输入、3游戏输入、7产物。编译0警告、0错误。
- 对比旧构建仅一份源码/编译源变化，即上述检查器；仅NativeRun DLL/PDB两项产物变化。DLL SHA256 `A5BDC7AE45C9083DAEAC125180A156691A98C6C488F210C1554F1F693787B748`；检查器源码SHA256 `599C99C9620E96D978B77C62C0945C791D1075B1692588FB6EA9998B816F1565`。

`NativeEvidence.Review`创建`NativeTraceContract`，后者持有这个检查器。宿主、采集器、策略、游戏输入、原生生成入口均未变化，故按[分层约定](../architecture/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md)采用完整证据重验与检查器正负控，不重复原生矩阵。这里是新reader审查旧producer证据，没有宣称全部原生对局来自新构建。M1最终同构建全量验收仍保留。

## 复现和验证

证据目录 `.build/runs/act3-model-repair-work01/`。

| 项目 | 实际结果与证据 |
| --- | --- |
| 修改前复现两个独审反例 | 新副本均由旧reader误接受；`before.json`，进程exit0 |
| 完整冻结集合重验 | 594份既有集合加22份第三幕独审控制，共616次；346接受、270拒绝，全部符合预期；`summary.json`，进程exit0 |
| 两个原反例修复后 | 重验集合最后22项的前两项均exit1，命中新模型绑定门；原副本及原始证据不改 |
| 新增控制 | MeatCleaver和ThrowingAxe各10场景，共20次：错误/null/缺失/对象/数字/空模型、错误类型、矛盾取得状态、正常及属性顺序等价；实际16拒绝、4接受 |
| 来源和产物核验 | 新构建及源码副本、旧冻结编译源/产物、原始证据、15锁定输入和52兼容源码均按哈希核验；`input-lock.json`、`source-snapshot/` |

新增控制的首版脚本把所有拒绝都预期为新绑定门的专用错误，因此对8个实际已被更早结构/不可变快照门拒绝的案例报诊断断言不匹配，脚本真实exit1及`controls/summary.json`保留。它们的实际reader退出均已是预期的1，没有误接受。`evaluate-controls.py`逐项按明确命中的生产语义门复核：缺失字段由既有身份结构读取拒绝、对象/数字由字符串类型门拒绝、单点取得状态矛盾由完整确认状态门拒绝。`controls-evaluated.json`保留原断言、实际诊断和对应门，复核exit0；未修改原始结果、预期退出或重复运行这些案例。

总计636次生产检查器调用。新增控制及历史样本中的普通EventOption等对象仍合法登记model=null；正常Tanx两种实际取得均通过。原有眩晕展示桥和Harmony审计未修改，沿用已独立验证的证据，本轮未重复初始化或执行审计目标业务。

可使用副本重现修复后的拒绝：

```powershell
dotnet .build/native-build/act3-model-repair01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/act3-model-repair-work01/evidence/case-594
dotnet .build/native-build/act3-model-repair01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/act3-model-repair-work01/evidence/case-595
```

完整重验计划在`plan.json`，逐调用日志在`evidence/`，补充控制在`controls/`，生产最小差异在`production.diff`。脚本使用固定新目录，不要覆盖已有证据；需要再次执行时应改用新的输出目录。

## 交接

独审优先核对这一个R1、两个原反例、正常两种取得与早期结构门的诊断处置，按分层原则复验冻结原始证据。通过后直接接续[第三幕自然流程交接](NATIVE_RUN_ACT3_NEXT_HANDOFF.md)，继续合法策略、后续节点、第三幕Boss及真实Architect/RunVictory；既有完整M1授权继续有效，无需逐步向用户请批。

本轮没有修改角色或游戏安装，没有UI、真实Steam/网络/用户存档操作，没有提交推送；旧失败证据和全部无关工作保留。
