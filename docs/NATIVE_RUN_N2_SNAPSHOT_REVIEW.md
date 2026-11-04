# N2 快照修复最终独立验收

日期：2026-11-02。审查对象为[快照修复报告](NATIVE_RUN_N2_SNAPSHOT_REPAIRS.md)及实际冻结源码、产物与证据。**结论：剩余 R1 通过，步骤 3（N2）在约定范围内验收通过，无剩余必修项。** R2 来源／执行模式修复保持有效。下一步按[步骤 4 交接](NATIVE_RUN_N3_IMPLEMENTATION_HANDOFF.md)推进；整个 M1 尚未完成。

## 冻结身份与审查方式

- Build ID：`B9C7B7EB283CA1FA895EB554901B3B79FEBAEEAF37B1FCCF724A85775B45BDED`。
- NativeRun DLL SHA256：`ABF89C7409733FC2D81F903A348FE79B5AA140ED967F35A3A2447D9C41D027BB`。
- Manifest：`.build/native-build/n2-snapshot-final02/build-manifest.json`。
- 本轮独立证据：`.build/runs/n2-snapshot-independent-review01/summary.json`、`review.py` 及其各控制副本。

逐文件核对 32 个源码条目、3 个原版输入、17 个编译输入、7 个产物，全部匹配；按构建脚本的规范 JSON 重新计算 Build ID 一致。15 个 Eleanor 锁定输入、52 个旧兼容源码均匹配既有冻结记录。

本轮没有启动游戏初始化或重跑原生场景。实际运行的是冻结 DLL 的 `--review`，该分支在 `Main` 中先于 AssemblyResolver、NativeHost 和 GameBootstrap 返回。原证据复制到新的审查目录后检查；432 个被复制原始文件在审查前后 hash 相同。运行代码和已有项目文档均未修改。

## 剩余 R1 的实际闭合

`NativeTraceContract.ReadSnapshot` 明确列出 20 个必需 state 的观察事件和 16 个不要求 state 的元数据事件，两组互斥、并集保持原 36 种事件。`NativeEvidence.Review` 先调用该函数，再对同一个快照校验必需结构、RNG 和语义状态机。

必需事件缺 state、state 为 null、state 非 object 均立即拒绝；无“只在字段出现时才检查”的入口。快照为空 object 时继续由既有必需字段校验拒绝，没有补默认数据。未知事件仍拒绝。元数据不因缺 state 而失败；显式携带 state 时仍验证其类型及内容。

独立使用生产 reader 检查 **108 份新副本**，对应 297 项文件、矩阵、内容和 reader 检查，0 失败：

| 副本组 | 实际数量 | 实际结果 |
| --- | ---: | --- |
| 原有腐坏控制 | 37 | 全部 exit 1 |
| 新快照控制 | 65 | 全部 exit 1，具体快照错误匹配 |
| 无 state 的 task-fault / selection-fault | 2 | 越过快照门，在 `Faulted inner task` 语义门 exit 1 |
| 正常 skip / take / grid / bundle-shared | 4 | 全部 exit 0 |

65 项实际内容逐项核对：20 种事件各 missing/null/array；删除全部 state、删除单个 confirmed state；confirmed 的 string/number/boolean。原 skip 有 182 份快照；全删副本为 0 份，在 new-run 报 `Missing required state snapshot`；单 confirmed 缺失为 181 份，在 confirmed 报同类错误。null 和其他 JSON 类型在相应具体门拒绝。

这些语义控制已重建 hash 链、footer、seal 和 result 中的 seal，因此拒绝来自快照／语义检查。六个 death / sink-failure-trigger 控制明确为 schema-only 类型替换，不证明执行过真实 Death 或 sink 生命周期。正常副本实际包含 14 种无 state 元数据；另两种 fault 元数据由上述负控验证，合计覆盖 16 种。

## 原矩阵与结果范围

已独立核对 `.build/runs/n2-snapshot-final02-suite/summary.json`：74 个原生子进程、580 项检查、0 失败；修复者的补充文件确有 114 项通过检查。Core 实际日志为 98 项通过，Core DLL 与本次冻结依赖相同；本轮未重复执行 Core。

580 = 原 379 + 65×3 + 2×3。它是断言数量，不能称为 580 条独立对局。原始子进程退出分布为 44×0、28×1、1×2、1×3；预期失败、Unsupported 和预算控制没有被改称正常完成。来源仍为 6 natural-run、38 api-fixture、30 fault-control；执行方式为 47 live、23 frozen-replay、4 observation-control。样本仍主要是一个开发 seed 的重复和夹具，不是胜率样本。

自然 skip 为 24 dispatched / 24 confirmed，take 为 25 / 25；同 run 首战实际 Victory、原生金币领取、卡奖励跳过／领取、合法下一战 Play 边界。两者 HP70、Gold266，永久牌组分别 10／11 张；take 的 Pommel Strike 永久实例 i31 与下战副本 i35.DeckVersion=i31 对应。没有据此宣称奖励卡被打出或第二战终局。

route/final-state 与 C166、旧 final06 字节一致；同构建冻结重放和 off/minimal 对照保持一致，完整 RNG 的 counter、s0–s3 仍存在，未删字段。SaveProgress 5 次、WriteReplay 1 次拦截保持。旧 N0/N1 仅有动作重放证据、其旧 RNG 序列化遗漏的限制继续保留。

## 非阻断限制与后续门

- N2 是原生单战至下一战边界和明确 API 夹具，结果 native-only / NotCompared；未完成全 run 或全内容支持。
- 新 host 尚未取得真实 Death／最终 RunVictory 证据。现有 RunWon 仅为 OnEnded 前置观察，不能独自认证终态；N1-T 必须检查成功完成与原生清场顺序。
- 当前 reader 的自然路径固定要求单次首战生命周期，控制器仍硬编码单场与下一战 Play。N3 扩多节点时应增加明确场景契约，保留旧单切片门，不能放宽它来容纳缺失生命周期。
- death/sink 的 schema 控制、元数据故障注入与真实执行故障分列；没有把故障注入当自然缺陷复现。
- 未展开所有未来 schema 值域、任意内容的 RNG 或精确触发因果图。它们按新增房间的必要数据逐项扩展，不是本次 R1 的剩余要求。

无需继续扩大步骤 3。步骤 4 的 N1-T 与 N3 实施已在总六步授权范围内；当前没有权限、环境或执行阻塞。没有提交／推送、启动游戏 UI 或修改角色、安装目录、真实存档。
