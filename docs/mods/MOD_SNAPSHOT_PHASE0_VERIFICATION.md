# Mod 快照阶段 0 复核报告

日期：2026-11-06
复核者：独立只读校验 Agent
阶段：0 快照和来源固定（只读复核，不改代码）

## 阶段状态

**PASS**

## 范围

对固定 Workshop 快照 `local-mods/workshop-snapshot-20261106-163352` 与清单 `local-mods/workshop-snapshot-20261106-163352.manifest.json` 做只读逐文件复核。全程未访问实时 Workshop 目录（`D:\SteamLibrary\...`），未在快照内写入任何字节。

## 复核环境

- 分支：`main`（HEAD `3d12a14`）。
- 既有未提交变更（本次会话之前已存在，未触碰）：219 个删除、25 个修改、26 个未跟踪路径，共 270 条。主要为 `docs/COMBAT_SOLVER_*.md` 系列删除、`README.md` 与 `THIRD_PARTY_NOTICES.md` 修改，以及 `docs/` 下新目录结构。未执行任何 reset、clean、checkout、commit、push。
- 复核脚本与产物位于 `.build/phase0-verify/`（git 已忽略）：
  - `verify_snapshot.py` — 主复核脚本（只以 `rb` 打开快照文件）。
  - `explore_aggregate_digest.py`、`explore_aggregate_digest2.py` — 聚合摘要公式探索。
  - `phase0-verification.json` — 机器可读复核结果。

## 验收标准逐项结果

| # | 验收项 | 结果 | 证据 |
| --- | --- | --- | --- |
| 1 | 快照目录存在且包含 21 个 Workshop 条目 | 通过 | 顶层 21 个 Workshop ID 目录，与清单 `ItemCount: 21` 一一对应 |
| 2 | 清单记录 142 个文件和 6,974,558,103 bytes | 通过 | `FileCount: 142`、`TotalBytes: 6974558103`；逐条目 `sum(Length)` 与各条目 `TotalBytes` 全部一致；全清单路径去重后恰为 142 |
| 3 | 按相对路径、文件长度和 SHA-256 重算内容摘要 | 通过 | 142 个文件全部重算 SHA-256（流式读取），相对路径、文件长度、SHA-256 与清单逐项一致，`mismatchCount: 0`，无重复相对路径 |
| 4 | 无额外 Mod 内容文件、无缺失文件 | 通过 | 磁盘枚举 142 个文件与清单 142 条完全相等：`missing: 0`、`extra: 0`；磁盘总字节数 6,974,558,103 与清单一致 |
| 5 | 不访问实时 Workshop 目录、不改变快照 | 通过 | 脚本仅打开 `local-mods/workshop-snapshot-20261106-163352` 与清单文件；复核结束后复扫快照仍为 142 文件 / 6,974,558,103 bytes |

完成标准（清单摘要与重算摘要相同）：达成。零差异，无 BLOCKED 项，无首个差异路径。

## 游戏版本固定 0.111.0 的静态核对

清单内兼容变体布局与计划文档一致：

- RitsuLib（3747602295）：`compat/` 下存在 `0.107.1`、`0.109.0`、`0.110.0`、`0.111.0` 四组变体，`0.111.0` 组含 `STS2-RitsuLib.dll`、`STS2-RitsuLib.Runtime.dll`、`compat-target.txt` 等 7 个文件。精确匹配目标可用。
- YukiMakoto（3829924471）：`lib/` 下存在 `0.107.1` 与 `0.111.0` 两组变体，`0.111.0` 组含 `YukiMakoto.dll`。精确匹配目标可用。
- intentgraph2（3747528152）：内嵌 `intentgraph2core-lt-0.110.0.dll`，未发现 `0.111.0` 变体段。它不是运行时玩法目标，仅作后续依赖解析的静态事实记录。

## 已知缺口和未支持语义

1. **顶层聚合摘要 `SnapshotContentSha256` 无法独立复现**。计划只说明"内容摘要由排序后的路径、大小和 SHA-256 计算"，未记录精确序列化；生成工具未入库。本次尝试了约 30 种规范序列化（`path|length|sha256` 及其分隔符/换行/排序/大小写变体、sha256sum 格式、内容字节拼接、逐条目链式、JSON 紧凑序列化等）均不等于 `7d23eddd…48a73`。这不影响本阶段验收：聚合摘要的全部输入（142 条路径+长度+SHA-256）已被逐项证实与磁盘一致。建议后续把聚合摘要生成工具入库或在计划中写明公式；新快照创建时再采用。
2. 清单 `Metadata` 中的 JSON Mod 元数据（`min_game_version` 等）本次仅做只读抽样核对，未做全量 schema 校验——那是阶段 1（输入审计/ModCatalog）的范围。
3. `Acheron` 的 `min_game_version` 值为 `v0.107.1`（带 `v` 前缀），与其他条目的裸版本格式不一致。阶段 1 的版本约束解析需要容忍或拒绝该格式，属预期工作项而非快照缺陷。

## 执行过的命令及结果摘要

| 命令 | 结果 |
| --- | --- |
| `git branch --show-current` / `git status --short` | `main`；270 条既有未提交变更（219 D / 25 M / 26 ??），全程未改动 |
| 清单结构检查（Python，只读） | Schema `sts2-simulator-lab/workshop-snapshot/v1`，结构、计数、字节全部符合 |
| `python -I .build/phase0-verify/verify_snapshot.py` | 判定 PASS：missing=0、extra=0、mismatch=0、142 文件、6,974,558,103 bytes |
| 聚合摘要变体探索（两个脚本） | 约 30 种变体均不匹配（见已知缺口 1） |
| 0.111.0 变体静态核对 | RitsuLib 与 YukiMakoto 的 `0.111.0` 变体存在 |
| 复核后快照复扫 | 仍为 142 文件 / 6,974,558,103 bytes，快照未被改变 |

## 实际修改文件

- 新增 `docs/mods/MOD_SNAPSHOT_PHASE0_VERIFICATION.md`（本报告）。
- 新增 `.build/phase0-verify/` 下 3 个脚本与 1 个 JSON 结果（git 忽略，不入库）。

未修改关键文件：快照目录与清单（零写入）、`docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md`、全部源码、既有未提交的 270 条变更。

## 下一阶段唯一建议入口

阶段 1（输入审计，只读）：`docs/prompts/MOD_ADAPTATION_STAGE_PROMPTS.md` 中阶段 1 提示词，以上述 `docs/mods/MOD_SNAPSHOT_PHASE0_VERIFICATION.md` 与 `docs/mods/MOD_INTEGRATION_EXECUTION_PLAN.md` 为输入。核心目标：21 个条目的 manifest/PE 元数据全量解析、版本约束语义（含 `v` 前缀格式）与 `0.111.0` 精确变体选择的输入侧盘点。

## 提交与推送

未提交、未推送、未创建远程 PR、未修改 Git 历史。
