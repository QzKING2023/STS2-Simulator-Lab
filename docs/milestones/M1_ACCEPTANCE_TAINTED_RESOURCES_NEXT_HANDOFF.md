# M1 第四批后续交接

**继续授权有效，M1 未通过。** 不重复本批已经完成的执行，不自动扩大 18 次预算，不提前提交推送或启动性能里程碑。

基线及证据见 [第四批验证](M1_ACCEPTANCE_TAINTED_RESOURCES_VALIDATION.md)。最终冻结 `B64C6BF07A97EB40F286D2834E46CC0518AE6230F74952A9579E0D86A81D219C`，原始材料 `.build/runs/m1-acceptance-work04/`，所有本批运行已结束。

## 下批最小顺序

1. **先有限独审本批实质修复。** 核对原始 Tainted DataError、实际非致死／附着移除／死亡脱离夹具；重点 owner 快照初始化、fork 不变与两条真实自然差分。核对药水实际来源与 token、ThisOrThat 原生命令资源账本、Nimble 的继承 OnPlay 准入。80 项有限控制和最终同构建两次 RunVictory 可以重读，不为检查器审查重跑全量。
2. **Reflections 优先复用原生与既有 RF。** EVAL04 final06 在 622 确认／623 派发停止。只读核实：该类覆盖 OnRoomEnter，用 NEventRoom 的空条件访问产生展示效果，不能直接塞进“继承空入口”列表。TouchAMirror 原生随机降级两张／升级四张；Shatter 原生克隆原牌组并加诅咒，不能手工 append 代替。RF 有 `ReflectionsPrediction`，其 TouchAMirror 使用克隆 RNG 和 PredictedCard，先核实当前固定子集、许可、纯度和结果；Shatter 不在该预测分支。保持预登记实际选项，不为避开问题更换选择。
3. **CrystalSphere 用明确夹具补真实边界。** 现有适配只替换展示入口，复用 CellClicked／完成 Task／原生奖励；上一批前缀尝试在到达事件前失败。先确认任务异常能传播，再以明确 API 夹具或真实可达路径验证点击、RNG、取得对象和完成，不能拼接自然胜利。当前未认证。
4. **预算与策略停止单列处理。** EVAL02 是总展开量预算；01/03/05/06 是上游当回合死亡保护，均非准确性失败或真实死亡。不得关闭准确性门来得到终局。若要改变一般搜索预算／候选采用契约，先记录上游现成档位、正确性与资源影响，作为新的配置实验；保持旧六样本原结果，不能为单场调评分或把额外预算结果覆盖原预算。
5. **最后才做完整同构建验收。** 当前分层样本跨 final03/04/06；最终宿主仍须六固定样本的 live 与重放、真实终局、原版节点／失败／预算回归、独立构建的冻结 Mod 回归和数据查询。已有单路径 RunVictory 不等同所有内容支持。

## 复验入口与边界

- 构建：`tools/native-run/build-native-run.ps1`，使用上文固定 solver 与现有 RF 快照，输出新目录，不覆盖 pin 或旧构建。
- 执行：`tools/native-run/run-native-slice.ps1`；固定样本 scenario `solver-native-darv-act2`，完整基准 scenario `solver-native-darv-act2-replay`。当前注册输入与预算在本批 `plan.json`，不要让过期批次脚本自动重新开跑。
- 生产原始证据重读：`dotnet <冻结宿主 DLL> --review-full-run <原始目录>`。部分 Unsupported 前缀的退出 0 不是整局通过；预算样本会在原预算门再次退出，须保留分类。
- 参考本批 `compare-regressions.py`、`new-controls-final06.ps1` 和 `enchantment-probe/`。Harmony 实际注入控制用 net9；PowerShell CLR10 不兼容的尝试不能计成功。控制是独立 API 实验，没有原生业务派发。
- `final-binding.json` 核对 1,178 项及 67 个受保护文件；哈希证明一致，不代替受控执行来源。版本和生成器关系不可混用。
- 继续整理现有性能日志，保留未知字段；不侵入式分析、不额外性能运行、不以重放和在线时间相减。

只在 M1 全部既定验收通过后，按现有用户授权审查代码／文档、许可证、敏感内容与生成物边界，再整体提交并推送。此前禁止提交推送，不动其他项目／真实存档／安装，不启动游戏 UI。
