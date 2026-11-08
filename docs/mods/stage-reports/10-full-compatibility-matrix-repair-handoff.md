# 阶段 10 后续修复交接

## 目的

下一会话只修复阶段 10 暴露的 worker 工程和静态元数据读取阻塞，然后重跑全量矩阵。不要在同一会话接入新的角色、性能、策略或大规模内容。

## 当前基线

- 分支：`main`，相对 `origin/main` ahead 8。
- 工作区已有大量用户修改、删除和未跟踪文件，必须原样保护。
- 固定快照：`local-mods/workshop-snapshot-20261106-163352`。
- 快照清单：`local-mods/workshop-snapshot-20261106-163352.manifest.json`。
- 目标游戏：`0.111.0`。
- 快照摘要：`7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73`。
- 条目/文件：21/142。
- 详细矩阵：[10-full-compatibility-matrix.md](10-full-compatibility-matrix.md)。

## 已确认结果

- 核心检查：137 项通过。
- 阶段专用项目 `WatcherStage6`、`RitsuStage7`、`KaguyaStage8`、`MinionStage9`、`MinionStage10` 均可 Release build。
- 全量静态 worker：21 个新进程，19 个成功、2 个失败；所有 run manifest 都记录了只读隔离证据。
- 原版 `slice-skip` M1 回归：退出 0，22/22 动作确认。
- Eleanor 冻结证据：历史报告和输入锁不在当前工作区，复核脚本退出 1；不要伪造通过。

## 必须修复的两个阻塞

### 1. `ModWorker.csproj` 顶层入口编译冲突

当前项目只排除了 `WatcherStage6Program.cs;RitsuStage7Program.cs;KaguyaStage8Program.cs;MinionStage9Program.cs`，但 `MinionStage10Program.cs` 仍被默认 Compile 包含。命令：

```powershell
dotnet build tools/mod-worker/ModWorker.csproj -c Release --ignore-failed-sources -p:NuGetAudit=false
```

当前结果：`CS8802`，`Program.cs` 与阶段顶层入口同时进入项目。

修复要求：保持独立阶段项目的显式 Compile 边界；修复后上述命令必须 0 错误。不要删除阶段入口，不要把多个顶层程序合并成空壳。

### 2. 两个静态 worker `MOD-W008`

- `intentgraph2`（Workshop `3747528152`）：包内同时有 `intentgraph2core.dll` 和 `intentgraph2core-lt-0.110.0.dll`，当前 collectible `AssemblyLoadContext` 发现同名程序集已加载。
- `shadowversebydmod`（Workshop `3747644378`）：`AssemblyName.GetAssemblyName`/相关读取路径对约 3.28 GB DLL 触发 .NET “files less than 2 gigabytes” 限制。

修复要求：

- 只改 worker 的受控元数据发现路径；核心模拟器仍不能引用外部 Mod 类型、Godot 对象或 DTO。
- `intentgraph2` 必须明确报告每个候选 DLL 的身份、选择/拒绝理由和精确游戏版本；不能静默选用 `0.110.0`。
- `shadowversebydmod` 必须使用安全的分段/内存映射或等价受控元数据读取；不能把超大文件截断、复制、重打包或跳过后声称发现成功。
- 如果无法在不改变语义的前提下读取，保留稳定 `RuntimeFailure`/`Unsupported`，并给出精确原因。

## 不要改动

- 固定快照目录及其 manifest、DLL、PCK、资源。
- 实时 Workshop 目录、玩家存档、正式设置和正式游戏数据。
- `src/SimulatorLab.Core` 的外部类型边界。
- Watcher、LexNinja2、Kaguya、Shadowverse 的既有能力矩阵语义。
- SnowLuma 或无关项目。
- 历史证据、删除状态、用户已有未提交修改。

## 修复验收

1. `dotnet build tools/mod-worker/ModWorker.csproj ...` 通过。
2. `TestModWorker.ps1` 通过，既有 5 个负控保持稳定。
3. 重新跑 21 条目矩阵；每条目都有新 run manifest、seed、worker 身份和输入摘要。
4. `intentgraph2` 和 `shadowversebydmod` 的结果必须能区分：成功发现、明确 Unsupported 或 RuntimeFailure；不能静默跳过。
5. 21/21 隔离证据保持：`coreLoadedModAssembly=false`、正式数据访问为 false、无 patch 残留、ALC teardown 完成。
6. 核心 137 项、阶段 7/8/9/10 既有检查和原版 `slice-skip` 回归保持通过。
7. 快照摘要和关键文件哈希保持不变。

## 结束条件

完成修复和矩阵重跑后，写新的修复交接报告并停止。报告必须说明：修改文件、每个阻塞的新旧结果、错误码和分母、未支持语义、是否仍有 Eleanor 数据阻塞、下一唯一入口、是否提交或推送。禁止自行开始 YukiMakoto 或其他未运行角色。

