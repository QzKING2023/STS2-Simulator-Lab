# 4D1B R1–R3：完整状态、必需阶段与实际Travel结果

状态：**R1–R3修复及最终同构建统一回归通过，待独立复审。** 原审查的9个错误副本全部由新生产检查器拒绝；合法原始包保持通过。旧失败证据及此前中断恢复证据完整保留。

原问题与复现见 [独立审查](NATIVE_RUN_N3D1B_REVIEW.md)。这次仍限定4D1B，无新商店效果或Mod适配。

## 实现变化

仅修改四个项目文件：`NativeMerchantEconomyContract.cs`、`NativeMerchantContract.cs`、`NativeMerchantEconomyControls.cs`、`run-native-n3d1b.py`。价格、付款、库存、历史、保存隔离和原生动作执行代码没有变化；没有新增运行时转接或游戏效果替身。

R1使用现有真实边界完成完整状态绑定，无需增加自证的旁路记录：

1. 第一价格子操作记录所属库存生成根动作/context，只允许实际MerchantSetup或进入Shop的Travel。
2. 后续价格start必须完整等于上一个价格return，三个子操作连续且唯一。每次原生Shops状态转移和价格算法的独立断言保持。
3. 最后价格return必须完整绑定该生成动作的真实confirmed。自然Travel没有额外状态豁免；API Setup只允许引入实际新库存的merchant投影。
4. 自然Attach只允许从已确认的生成状态引入实际库存投影，其余主状态完整保持；三条价格记录的具体entry/model/cost仍绑定实际生成库存。生成确认和Attach各一次，最终收尾要求全部完成。

这是当前固定原生生成顺序下的有限契约：卡/遗物/药水选取在价格链相应前缀发生，最后药水价格之后到父确认没有当前profile允许的额外角色业务变化。没有强行令价格快照等于原Travel dispatch，也没有忽略整组RNG或任意状态字段。未来有真实房间Hook业务变化时，应显式增加原生业务区间，不删除当前比较。

R2给每次适用自然Travel建立阶段欠账：

| 边界 | 生产门 |
| --- | --- |
| Travel派发 | 清空本次stats完成标记，保留上次不可变确认状态；前次到达/完成欠账必须已结清 |
| stats start/return | 属于当前Travel/ MerchantTravel，恰好一对，顺序正确；有旧session时先退出开始、再统计，不能挪到退出返回后 |
| Travel confirmed | 必须已完成stats，即使首次从Neow出发没有Lab旧session；已有旧房间时必须已返回退出 |
| 旧room-complete | 必须对应本次待完成旧会话，仅一次，完整静止状态与确认绑定 |
| 新room-entered | 必须对应本次待到达房间，在旧完成之后，唯一且完整状态/实际房间实例绑定 |
| 下一根请求与最终contract/Finish | 不能带着缺失room-complete或room-entered进入后续动作，也不能靠sessions总数代替最后一次到达 |

R3将MerchantTravel的有限结果结构绑定到实际动作：结果字段恰为closed/from/to；from同时等于本次decision和dispatch节点，to同时等于decision和返回节点，closed绑定实际商店退出。缺失、错误类型、额外字段和矛盾返回均被拒绝。

## 新控制与历史反例

新经济矩阵仍为23个实际原生场景，**344项检查、148个生产控制，0失败**。原117控制保留；新增31个为29个损坏与2个合法等价。合计144损坏拒绝、4等价接受。

- 单对及全部价格快照共同篡改Gold、HP、完整牌组、遗物、未知扩展字段、非Shops RNG；通过完整父状态链拒绝，而不是按字段逐项补丁。
- 删除首次/全部商店前/全部stats对、重复完整对、挪到退出返回后；删除首次/最后/全部新房到达、同时删除最后旧完成与到达。
- Travel返回from/to/closed矛盾，null/错误类型，缺少必要字段或额外字段。
- 价格快照属性重排、结果字段重排保持通过；原全状态一致扩展控制也保持通过。真实业务变化、合法取消/拒绝不受影响。

最终reader另重查原审查全部167包，0预期不符，包含原9误接受反例；原文件逐项哈希保持。另231个更早历史包全部符合预期，924原文件不变。两批都是新reader重读旧身份数据，不计作新原生执行。

candidate01编译0warning/0error，先完成167包定向复查；之后修正一个尚未执行的新调序控制的源行选择，避免选到没有旧session的首次Travel。最终final01重新冻结，候选不混作最终统一原生证据。

## 原生路径、RNG与审计门

最终构建三个自然种子仍分别实际确认72/41/83个动作：正常Neow、原生战斗/奖励、合法Shop购买、原生退出/历史、下一战实际Play。最终均Ongoing/SliceBoundary。牌组/实际商品/下一战副本、Gold/HP、价格与完整RNG检查保持。

新旧构建三条自然路径的route、choices、完整final-state逐项相等（9个文件比较）。这证明这三个固定场景的执行投影保持，不扩大成所有内容或零售UI等价。

三个方法的真实Harmony未知补丁探针在**本次最终DLL**重新运行，三个隔离进程退出0，预置与后加补丁共六项拒绝通过，目标业务调用计数均0。见 `patch-summary.json` 及 `patch-price/exit/stats.json`。仍然只是生产审计API夹具：清单正常时Ensure停在预启动隔离不满足边界，不宣称它是已初始化游戏运行。正常原生宿主路径由实际矩阵另行证明。

## 冻结与证据

- Build ID：`20EB7F9259E9EC2806AB9D91F2AFAA9EA73ED70DD0FCE8DC7FA21D816BD0D276`。
- Native DLL SHA256：`D251B8F0A6EF23959AE13599FB278AAB88B5DCF6201FF1481D0F733A81BAF4AC`。
- 清单：`.build/native-build/n3d1b-repair-final01/build-manifest.json`；0warning/0error，ID独立重算一致。
- 最终新矩阵：`.build/runs/n3d1b-repair-final01-economy/summary.json`、`controls.json`。
- 统一任务：`.build/runs/n3d1b-repair-work01/run-final01.ps1`；状态 `final01-progress.json`。
- 原9反例与其余审查包：`n3d1b-repair-final01-reader.json`；更早231包：`old-review-final01.json`。
- 历史证据保护：`historical-evidence-lock.json`，锁定此前运行/恢复/独立审查目录2622个文件；不覆盖其原记录。
- 最终整体计数、源码/输入/产物检查及dirty源码快照：同目录 `summary.json`、`source-snapshot-final01.json`、`closeout.json`。

最终统一回归为 **227个完整原生执行 / 2436项套件检查，另列Core98**：新经济23/344、旧商店25/371、4C 55/561、4B 34/307、4A 16/273、N2 74/580。实际原生退出为164×0、58×1、2×2、3×3，预期故障/Unsupported/BudgetExceeded明确保留；来源、原始退出、输入/输出哈希、隔离及快照的248项补充检查全部通过。不能把外层exit0当作所有路径都成功。15锁定输入与52兼容源码继续冻结；旧恢复的19已完成场景和header-only中断尝试均保留，新的最终构建执行使用全新目录。

复现单个旧坏副本（预期退出1）：

```powershell
dotnet .build/native-build/n3d1b-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-independent-review01/independent-price-pair-gold
dotnet .build/native-build/n3d1b-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-independent-review01/independent-missing-next-room-arrival
dotnet .build/native-build/n3d1b-repair-final01/bin/Release/net9.0/SimulatorLab.NativeRun.dll --review .build/runs/n3d1b-independent-review01/independent-contradictory-travel-result
```

全部新场景和控制可用 `python tools/native-run/run-native-n3d1b.py --manifest .build/native-build/n3d1b-repair-final01/build-manifest.json --output <全新Lab运行目录>`。共享缓存继续串行构建，原始目录不可覆盖。

没有提交推送、角色/安装/真实存档修改或游戏UI/真实Steam网络调用。复审通过后按 [4D1C交接](NATIVE_RUN_N3D1C_IMPLEMENTATION_HANDOFF.md) 继续原生遗物/Hook/补货，其余4D、步骤5–6仍待完成；本轮不把有限商店片段当完整M1。
