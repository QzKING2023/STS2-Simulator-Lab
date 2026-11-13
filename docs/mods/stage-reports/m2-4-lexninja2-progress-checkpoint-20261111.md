# M2-4 LexNinja2 进度记录点

记录时间：2026-11-11（Asia/Shanghai）

阶段按用户要求暂停，原因是供电不稳定。收到明确的“授权重启”后，从本记录点继续。暂停期间不启动游戏、worker、build 或新的 probe。

## 工作区状态

- 分支：`main`
- HEAD：`9f6a6fc9c37c2c895777ea48d50910cd46be7f98`
- 未提交的用户已有修改仍需保护：`README.md`、`docs/architecture/NATIVE_BUILD_STORAGE_POLICY.md`
- 既有未跟踪报告和维护脚本保持原样
- 本次阶段报告仍是早先的 PARTIAL 版本，路径为 `docs/mods/stage-reports/m2-4-lexninja2-full-run-report-20261111.md`
- 未提交、未推送、未创建远程 PR
- 当前未发现 `SlayTheSpire2` 或 `dotnet` 运行进程

## 固定输入

- 目标游戏：`0.111.0`
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`
- snapshot content sha256：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`
- manifest sha256：`09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3`
- RitsuLib：`3747602295/compat/0.111.0`，`0.6.10`
- LexNinja2：`3747530622`，`v3.0.11-beta-0-g358712f`
- 运行根目录：`.build/runs/m2-4-lexninja2-completion12-20261111`
- worker 当前源码：`.build/runs/m2-4-lexninja2-completion12-20261111/worker/MainFile.cs`
- worker 最近一次 build 成功，生成 DLL 后已复制到该运行根的 isolated runtime

## 已完成的真实证据

- 两个此前 Unsupported 卡牌已补齐
  - `CARD.LEX_NINJA2_CARD_I_SEE_YOU` 通过真实回合结束和 `OnTurnEndInHand` 生命周期
  - `CARD.LEX_NINJA2_CARD_MOSQUITO_HAND` 通过真实 `CardReward.OnSelect`、牌组注册和 `AfterRoomEntered`
- 三个事件已通过原生 `EventRoom`、真实选项回调和清理
  - `EVENT.LEX_NINJA2_EVENT_DRINK`
  - `EVENT.LEX_NINJA2_EVENT_MAN_YU_DIAN`
  - `EVENT.LEX_NINJA2_EVENT_THE_GREAT_SEAL`
  - `EVENT.LEX_NINJA2_EVENT_THE_SPECTRE`
- 三个 singleton 已完成真实完整流程运行并写出 `ContentLifecycleCompleted`
  - `SINGLETON.LEX_NINJA2_SINGLETON_DIRTY_DATA_SINGLETON`
  - `SINGLETON.LEX_NINJA2_SINGLETON_HOLY_SINGLETON`
  - `SINGLETON.LEX_NINJA2_SINGLETON_LEX_KELA_SINGLETON`
  - `SINGLETON.LEX_NINJA2_SINGLETON_SCIENCE_SINGLETON`
- `CARD_POOL.LEX_NINJA2_CARD_POOL` 已通过真实注册、`AllCards` 发现和清理
- `RELIC_POOL.LEX_NINJA2_RELIC_POOL` 已通过真实注册、`AllRelics` 发现和清理
- 四路主流程旧证据仍有效
  - primary/repeat/replay 均为 native Death/Completed，digest 相同
  - failure 为 Unknown/BudgetExceeded
  - 旧 canonical digest：`daa87168174e36fad401f3faa7bada24241321195d56911a67c6771bae7844f0`

## 当前未完成内容

- `POTION_POOL.LEX_NINJA2_POTION_POOL` 尚未得到有效的内容终态
  - `other-potionpool2` 至 `other-potionpool12` 都是 native Death/Completed，未出现 `content-pool` 或 `content-terminal`
  - `target-other-selected` 事件显示该 ID 在内容清单中，但 `found` 仍为 null
  - 需要在恢复后先检查 `TargetOther` 环境变量和 PotionPool 的实际运行时类型，再做真实注册/空池语义 probe
- `ENCOUNTER.LEX_NINJA2_ENCOUNTER_MAN_YU_DIAN_ENCOUNTER` 旧 probe 的 RuntimeFailure 原因已保留
  - canonical model 被直接用于 `CombatRoom`，需要确认 mutable encounter 入口后重跑
  - 当前未有成功的 encounter content terminal
- 12 个 Other 的最终矩阵和阶段报告尚未重建
- 仍不能把当前阶段标为 PASS，也不能关闭或更新票据为完成

## 恢复入口

恢复后从 `.build/runs/m2-4-lexninja2-completion12-20261111/worker/MainFile.cs` 和 `run-path.ps1` 开始。最新 worker 已增加 `other-primary` 路径、事件真实 probe、池发现 probe 和 encounter debug 入口。

优先检查：

1. 读取 `other-potionpool12/game-output/trace.jsonl`、`result.json` 和 `run-manifest.json`
2. 检查 `TargetOther` 的实际字符长度与字符码，并从 `ModelDb.All` 输出 PotionPool 类型、程序集和 ID 的直接映射
3. 修正 PotionPool 选择后重新 build，复制 isolated worker DLL，再运行新的 `.build/runs` 子目录
4. 修正 encounter 使用 mutable model 的入口并重新验证真实 Victory/cleanup
5. 只有 12 个 Other 都有内容终态后，重建 content matrix、能力矩阵和阶段报告
6. 最后按仓库协议更新 issue #10 证据并决定是否关闭

暂停要求：等待用户明确授权重启后继续。默认不提交、不推送。
