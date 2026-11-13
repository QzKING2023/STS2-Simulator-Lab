# M2-5 St2bs 交接报告（2026-11-13）

## 阶段结论

**BLOCKED：固定输入和静态依赖发现已完成，但当前 NativeRun 没有 St2bs 专用加载/生命周期适配器，因此不能安全启动 St2bs 的原生流程。**

本阶段没有把 vanilla 路径、静态程序集类型名或静态 worker 成功误报为 St2bs 的 flow/content 兼容性。

## 固定输入

| 项目 | 值 |
| --- | --- |
| 游戏 | `0.111.0` / commit `41cef1ea` |
| 游戏程序集 | `sts2.dll` SHA-256 `0861bfa1df347538d932f22d580e75420f08082792eb914e53b4882764acdbe9` |
| Workshop 快照 | `workshop-snapshot-20261106-163352` |
| 快照摘要 | `7d23eddd8a238e248b921a0f12056c2a6a2a264936a5ad56ce9ee65b30148a73` |
| manifest 摘要 | `09a553d42f860a7ad22fa7d62bd66d4614cc618d06756218e6be068181e8e5d3` |
| Mod | `St2bs@v2.3.0` / Workshop `3749061519` |
| 依赖闭包 | `BaseLib@3.4.7 -> St2bs@v2.3.0` |
| St2bs DLL | `b58ac78fe6cb7c1e771af483450d7afb402db7e9ac7fb5a113970d3844bbc0b2` |
| St2bs PCK | `d801125d78ca376571eaba265f76d3b1a2813b6ae29b3be2dd3460ac6ebea6b1` |
| canonical seed | `LAB_M2_5_ST2BS_20261113_STATIC` |

St2bs 的 manifest 明确声明只支持游戏 `0.111.0`，但 BaseLib 最低版本为空；本报告采用快照中唯一的 BaseLib `3.4.7`，没有进行版本回退。

## 已完成的静态阶段

新的独立 worker 进程已完成只读发现：

```text
dotnet tools/mod-worker/bin/Release/net9.0/SimulatorLab.ModWorker.dll \
  --input .build/runs/m2-5-st2bs-20261113/static-input.json \
  --output .build/runs/m2-5-st2bs-20261113/static-worker \
  --build-manifest .build/mod-worker-validation-build-manifest.json \
  --seed LAB_M2_5_ST2BS_20261113_STATIC
```

结果：退出码 `0`，`mod-worker-run/v1` 状态 `succeeded`，依赖闭包为 `BaseLib -> St2bs`，发现 6 个文件、2 个程序集、2 个 PCK；worker 隔离字段保持 `coreLoadedModAssembly=false`、`harmonyPatchCount=0`、`assemblyLoadContextUnloaded=true`。

机器可读证据目录：

`.build/runs/m2-5-st2bs-20261113/`

- `static-worker/run-manifest.json`
- `canonical-input.json`
- `runtime-preflight.json`
- `content-matrix.json`
- `lifecycle-matrix.json`
- `interaction-risk.json`
- `four-path-status.json`

静态程序集扫描确认了以下候选类型，但这些只是静态候选，不能作为 runtime registration identity：

- `St2bs.St2bsCode.Character.St2bs`
- `St2bs.St2bsCode.Character.Leike`
- `St2bs.St2bsCode.Cards.ArmoryCard`
- `St2bs.St2bsCode.Monsters.WaxDoll`

## 阻塞证据

当前 NativeRun 的场景分派只包含 `NativeFoundationModLoop`、`WatcherNativeModLoop`、`KaguyaNativeModLoop` 和 `MinionNativeModLoop`；`tools/native-run/Program.cs` 的主流程没有 St2bs 分支，也没有 St2bs 的 snapshot resolver、initializer、ModelDb augmentation 或内容 probe。

因此本阶段将 flow gate 和 content gate 都保留为 `BLOCKED`：

| 路径/门 | 状态 | 原因 |
| --- | --- | --- |
| Primary | `Unrun` | 没有 St2bs 原生适配器 |
| Repeat | `Unrun` | Primary 尚未建立 |
| Replay | `Unrun` | 没有 Primary route |
| Failure | `Unrun` | 没有可合法启动的 St2bs native process |
| Flow gate | `BLOCKED` | 不能证明真实 `RunVictory` 或 `Death` |
| Content gate | `BLOCKED` | 没有 runtime registration 和 lifecycle evidence |
| Foundation gate | 沿用共享 foundation 状态 | 静态 BaseLib 发现不等于完整 foundation 通过 |

运行 vanilla 场景会产生与 St2bs 无关的证据，因此没有用 vanilla 路径冒充 St2bs 结果。`runtime-preflight.json` 明确记录了 `MOD-ST2BS-ADAPTER-MISSING`，并禁止终态提升。

## 继续完成 #13 所需的最小工作

1. 在 NativeRun/NativeModLoop 边界增加可复用的 St2bs profile：只加载快照中的 BaseLib 和 St2bs，并禁止正式存档、Workshop 和 UI 访问。
2. 在初始化阶段调用 St2bs 的 `St2bs.St2bsCode.MainFile.Initialize`，记录 Harmony ownership、注册失败和清理结果。
3. 将 St2bs 的 `Character.St2bs` 或 `Character.Leike` 加入 ModelDb，并建立真实角色选择和首战路径。
4. 选择一个可闭合的自定义 Card、Relic、Power、Enemy 或 Event，产生真实 lifecycle checkpoint 和 interaction risk evidence。
5. 用同一 canonical input 生成 Primary、Repeat、Replay、Failure 四路新进程证据；只有真实 native `RunVictory`/`Death` 才能通过 flow gate。
6. 完成 runtime teardown 和 append-only content inventory 后，再更新本票状态。

本次没有修改 St2bs、BaseLib、固定快照或共享源码，也没有删除旧证据、提交或关闭 GitHub Issue #13。
