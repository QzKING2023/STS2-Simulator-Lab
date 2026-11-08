# Claude Code 执行启动提示词  
## Simulator Lab 构建产物生成重构

你正在 D:\STS2-Simulator-Lab 中执行一次受限的构建存储重构。目标是降低普通 NativeRun 构建和运行的磁盘占用，同时保留可审计的最小 provenance，并把完整证据包变成显式选择。

---

## 任务目标

重构 NativeRun 构建产物生成和存储策略，使普通构建/运行只保留必要日志与最小元数据，只有显式传入 -CaptureEvidence 时才生成完整证据包。

本任务只允许修改以下两个文件：

~~~text
tools/native-run/build-native-run.ps1
docs/NATIVE_BUILD_STORAGE_POLICY.md
~~~

不得修改、删除、重命名或格式化其他文件。不得擅自修改 run-native-slice.ps1、cleanup-native-build.ps1、NativeRun C# 源文件、测试、README、其他文档、配置或 Git 元数据。如果发现必须修改其他文件才能满足要求，立即停止，报告阻塞原因、所需文件和建议方案，不要越权编辑。

工作区已有未提交改动。必须先记录并尊重现有状态，不得覆盖、回滚、清理或重写与本任务无关的修改。

---

## 开始前的强制检查

1. 从项目根目录向下查找所有 AGENTS.md。先读取根目录的 AGENTS.md，再读取路径相关的子目录 AGENTS.md，严格遵守其中的规则。即使当前检查未发现，也必须重新执行查找并在最终报告中写明结果。
2. 读取并理解以下文件的当前内容和调用关系：

~~~text
tools/native-run/build-native-run.ps1
tools/native-run/run-native-slice.ps1
tools/maintenance/cleanup-native-build.ps1
docs/NATIVE_BUILD_STORAGE_POLICY.md
docs/NATIVE_RUN_EVIDENCE_TRUST_BOUNDARY.md
docs/NATIVE_RUN_DATA_CONTRACT.md
tools/validation/TestStageOneEvidence.ps1
tools/validation/TestStageOneBuildManifest.ps1
tools/validation/NewStageOneBuildManifest.ps1
tools/validation/run-stage-one.ps1
tools/validation/StageOneEvidenceRules.ps1
~~~

3. 使用 rg 检索与以下内容相关的调用方、字段名和输出文件名，确认兼容约束：

~~~text
build-native-run.ps1
run-native-slice.ps1
BuildManifest
build-manifest.json
run-manifest.json
run.log
console.log
stderr.log
evidence-manifest.json
source-snapshot
compiled
.ready
CaptureEvidence
OutputRoot
contentKey
cache
seed
~~~

4. 检查 git status --short 和目标文件的 diff。不要修改或重置现有工作区改动。目标文件中的既有逻辑必须在可行范围内保持兼容。
5. 不运行完整 Simulator Lab、不运行完整构建、不运行大规模回放、不运行性能测试。开始阶段只能做静态阅读和轻量检查。

---

## 当前实现的重点事实

以实际读取到的文件为准，不要假设下面的描述永远正确。当前 build-native-run.ps1 已经存在以下概念，重构时需要审计并保持兼容或明确迁移：

- -OutputRoot
- -SolverSnapshot
- -ForeseerSnapshot
- -CaptureEvidence
- -DryRun
- -Cleanup 和 -ApplyCleanup
- contentKey 和基于源码、脚本、工具链、锁文件等输入的缓存键
- staging 目录
- .ready 标记
- build-manifest.json
- run-manifest.json
- run.log
- source-snapshot
- compiled
- evidence-manifest.json
- 缓存清理和保留天数逻辑

当前 run-native-slice.ps1 会在 .build/runs 下创建运行目录，并写入 build-manifest.json、console.log、stderr.log、process.json 等文件。它还使用 -Seed，并对构建 manifest、source、input、compiled、artifact 做哈希绑定。由于本任务禁止修改该脚本，任何兼容性调整必须在允许的两个文件内完成，或在确实无法完成时停止报告。

---

## 硬性约束

### 文件修改范围

- 只能修改 tools/native-run/build-native-run.ps1
- 只能修改 docs/NATIVE_BUILD_STORAGE_POLICY.md
- 不得因为测试、格式化、调试或临时工作而写入其他项目文件
- 不得把临时文件、日志、缓存、证据、测试输出写进 Git 工作区受保护路径
- 不得提交或推送

### 安全禁止事项

绝对禁止以下操作：

~~~text
git clean
git reset --hard
git checkout -- <path>
git restore <path>
通配符删除整个项目或不受严格校验的目录
删除源码、文档、测试、Git 文件、SnowLuma 或其输入
修改或覆盖用户已有的未提交改动
运行完整 Simulator Lab
运行大规模回放
执行性能测试
~~~

删除必须使用明确的、经过路径校验的 -LiteralPath。不得通过通配符、模糊前缀或仓库级递归删除实现清理。

---

## 必须实现的行为

### 1. 普通流程的最小保留

普通构建/运行默认只保留：

- 一份完整详细日志
- 日志头部，或一个很小的 metadata 文件
- 构建结果正常工作所必需的最小文件
- 兼容现有调用方所必需的 manifest 文件

不得默认保留完整 source snapshot、compiled snapshot、反编译副本或重复的详细日志。

普通流程的完整日志和最小 metadata 至少要记录以下字段，字段名可以沿用现有格式，但必须能被机器读取和人工审查：

- 随机种子。若构建阶段没有直接收到 seed，必须明确记录 not-supplied、not-applicable 或等价值，并记录 seed 的实际来源。不得静默省略。
- 完整运行日志
- 局内日志
- 标准错误和错误信息
- Git 提交哈希或其他明确版本标识
- 配置和参数摘要
- 构建脚本、工具链或 NativeRun 版本
- 开始时间
- 结束时间
- 退出状态
- 日志格式版本
- 输出路径和输出路径规则
- 是否命中缓存
- content key 或等价缓存身份
- 失败或中断时的原因

如果运行日志来自现有运行目录，必须在策略文档中说明 build 输出和 run 输出的边界，避免在构建目录中再复制一份大日志。不得为了满足字段而复制完整日志多次。

### 2. 默认不保留的内容

以下内容默认不得保留在普通构建产物中：

- bin
- obj
- 可重建程序集副本
- 重复的 source snapshot
- compiled 快照
- 反编译副本
- 临时 solver 依赖
- 可以由源码、Git、锁文件和工具链重新生成的其他文件

如果运行所需的程序集必须短暂存在，必须放在受控的临时 staging 或缓存位置，并在成功或失败路径按策略清理。不得永久保存完整 bin/obj 副本作为普通运行输出。

必须明确区分：

- 构建过程中的短暂临时文件
- 成功后可复用的最小缓存
- 显式证据包中的完整文件
- 失败或中断时必须保留的最小诊断信息

### 3. -CaptureEvidence 开关

增加或修正明确的 -CaptureEvidence 行为：

- 默认关闭
- 只有显式传入 -CaptureEvidence 才生成完整证据包
- 默认流程不能因为证据功能而增加大量磁盘占用
- 证据包必须有 manifest
- manifest 至少记录输入、输入哈希、随机种子、版本、配置、开始时间、结束时间、退出状态和输出文件清单
- manifest 必须能指出证据包的 schema/log format 版本
- 证据包写入必须先进入临时 staging，内容完整并校验成功后再原子移动到最终位置
- 证据包生成失败时不得留下一个看起来完整但实际不完整的证据目录
- 证据包必须明确标明 source snapshot、compiled 内容和日志文件的相对路径
- 证据包和普通缓存的命名、生命周期、清理规则必须写入策略文档

必须验证默认模式下 -CaptureEvidence 为 false，且不会生成大体积 source-snapshot、compiled 或重复详细日志。

### 4. -OutputRoot 兼容性

保留现有 -OutputRoot 的兼容行为：

- 现有调用方传入 -OutputRoot 时仍能得到预期的输出位置
- 保留现有 Lab 内路径安全检查
- 明确记录显式输出和默认缓存输出的路径规则
- 绝对路径、相对路径、空值和非法越界路径必须有清晰行为
- 不得默默把显式 -OutputRoot 改写为另一个目录
- 如果显式目录必须是全新目录，必须保留该约束并在帮助、错误信息和策略文档中说明
- 输出路径必须写入最小 metadata 或 manifest

### 5. 缓存设计

如果实现或保留缓存，必须满足全部条件：

- 缓存键必须是稳定输入哈希
- 输入必须覆盖源码、项目文件、配置、关键脚本、NativeRun/build script 版本、工具链版本，以及现有锁文件、游戏版本和必要的输入哈希
- 不得只使用时间戳
- 计算缓存键时使用规范化、稳定、可审计的输入表示
- 构建必须先写入临时目录
- 所有必需内容生成、校验和 manifest 写入成功后，才可以写入 .ready 或等价 ready 标记
- 无 ready 标记的失败或中断产物绝不能复用
- 发现同一缓存键已有非 ready 目录时，必须拒绝复用并给出具体路径和处理建议，不能静默覆盖未知内容
- 并发构建出现竞态时，必须保证只有完整且 ready 的结果可被复用
- 缓存必须有明确的过期策略和清理策略，记录 cutoff、保留天数、清理入口和安全边界
- 清理策略不得依赖永久保存完整 bin/obj 副本
- 不能把 CaptureEvidence 关闭时的普通缓存伪装成完整证据包
- cache hit、cache miss、cache race、not-ready 等状态必须进入日志/metadata

### 6. 失败和中断

失败、异常、Ctrl+C、中断或子进程非零退出时：

- 保留错误日志、标准错误和最小 metadata
- metadata 必须记录开始时间、结束时间、退出状态、异常消息、seed、版本、配置摘要、输出路径和日志格式版本
- 不得写入 .ready 或等价 ready 标记
- 尽力清理临时 staging 目录
- 清理失败不能被静默吞掉
- 必须报告无法清理的具体路径和错误
- 失败的完整 evidence staging 不得被发布为可复用证据包
- 已存在的旧 ready 缓存不得被失败流程覆盖或破坏
- 失败日志应能区分 build failure、input validation failure、cache conflict、cleanup failure、interrupted

---

## 实现建议

下面是实现方向，不得替代对当前代码的审查：

1. 复用现有 Assert-UnderLab、reparse-point 检查、原子写入、contentKey 和 staging 逻辑，避免引入新的不一致路径校验。
2. 把普通流程的产物分成最小可运行输出、最小 metadata、单份详细日志三个概念。不要复制整个 build tree 来满足日志要求。
3. 保持 build-manifest.json 对现有 run-native-slice.ps1 的兼容字段，包括 build identity、source/input/artifact 哈希、toolchain、executable 等调用方需要的字段。
4. 对 seed、运行日志和局内日志要使用已有调用链能提供的真实数据。不得编造 seed 或把普通构建日志冒充 NativeRun 局内日志。
5. 如果只允许修改两个文件导致某个字段无法真实采集，必须在实现前停止并报告，而不是修改被禁止的调用方。
6. 证据 manifest 采用明确 schema，例如 native-build-evidence/v2，但只有在理解现有 schema 兼容关系后才能升级版本。若旧消费者依赖旧 schema，必须保留兼容字段或在策略文档中写清迁移边界。
7. ready 标记内容应包含 content key、build identity、schema/log format 版本和完成时间，但不能依赖单独的时间戳来决定缓存身份。
8. 所有新增删除逻辑都使用 -LiteralPath、Lab 根路径断言、直接子目录校验和 reparse-point 拒绝。
9. 不要为了“清理”删除已有受保护证据目录。普通流程只改变后续新产物生成规则。
10. 保留现有 -Cleanup 行为的安全边界。若策略文档需要更新，说明 cleanup helper 仍是独立入口，且本次重构没有扩大删除范围。

---

## 轻量验证范围

修改后只做以下验证。不得运行完整构建或完整 Simulator Lab。

### PowerShell 语法和参数

~~~powershell
pwsh -NoProfile -Command "& { [void][System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path '.\tools\native-run\build-native-run.ps1'), [ref]$null, [ref]$null) }"
pwsh -NoProfile -File .\tools\native-run\build-native-run.ps1 -? 
pwsh -NoProfile -File .\tools\native-run\build-native-run.ps1 -GamePath .\__missing_game__ -HostSource .\__missing_host__ -DryRun
~~~

如果当前脚本要求的参数使某条命令无法直接执行，使用最小安全占位路径并记录实际命令和结果，不创建大型构建输出。

### 默认模式

- dry-run 默认不启用 -CaptureEvidence
- dry-run 输出包含 planned output、OutputRoot 规则、content key/cache state、版本摘要、日志格式版本和 seed 来源
- 默认模式不生成大体积 bin/obj
- 默认模式不生成 source snapshot、compiled snapshot 或重复详细日志
- 默认模式只留下预期的小型 metadata 和一份详细日志，或在 dry-run 中明确显示将要保留的文件

### -CaptureEvidence

- 未传入开关时，断言 captureEvidence=false
- 显式传入开关时，断言 captureEvidence=true
- 只使用最小安全夹具或 dry-run 验证开关解析和路径计划
- 证据 manifest 包含输入、输入哈希、seed、版本、配置、时间、退出状态和输出清单
- 普通模式与证据模式的目录差异可被清晰列出

### -OutputRoot

- 验证默认输出路径规则
- 验证显式 -OutputRoot 的路径保持兼容
- 验证越界路径被拒绝
- 验证显式目录冲突时不会覆盖已有输出

### ready 和失败行为

使用最小临时夹具或已有 dry-run 分支验证：

- 成功完成前没有 .ready
- 成功写完 manifest 和所有必需文件后才出现 .ready
- 失败或中断不会写 .ready
- 无 .ready 的目录不会被缓存命中
- 清理临时目录失败时会报告具体路径和错误
- 日志包含 seed、Git/version 标识、参数摘要、开始/结束时间、退出状态和 log format 版本

每条验证命令都要记录退出码和关键输出。不要通过真实大体积构建来验证。

---

## 策略文档必须更新的内容

更新 docs/NATIVE_BUILD_STORAGE_POLICY.md，使其准确描述实现后的行为，至少包括：

- 普通构建/运行保留什么
- 普通构建/运行明确不保留什么
- 单份详细日志和小 metadata 的位置、格式和字段
- seed、版本、配置、开始/结束时间、退出状态和 log format 的记录规则
- -CaptureEvidence 默认关闭和显式开启方式
- evidence manifest schema、必备字段、输出清单和原子发布规则
- -OutputRoot 的兼容行为和路径规则
- 缓存键的完整输入组成
- staging、ready 标记、缓存命中和失败缓存的处理
- 过期、保留和清理策略
- 失败和中断时保留与清理的内容
- 哪些路径是受保护的
- 本次重构未修改哪些脚本和未扩大的删除范围
- 普通运行预计磁盘占用。使用可验证的范围或数量级，不能编造精确值。若无法测量，明确标注为估算并说明依据。

---

## 完成前的自检

在结束前必须检查：

1. git diff --name-only 只包含以下两个目标文件，外加用户原先已有的工作区改动。不得把其他文件加入本次改动。
2. build-native-run.ps1 没有引入通配符删除、仓库级递归删除、Git 清理或覆盖式 checkout。
3. 失败路径不会写 ready。
4. 默认路径不会生成完整 evidence snapshot。
5. -CaptureEvidence 只有显式开启才生效。
6. -OutputRoot 兼容行为有测试或静态证据。
7. 日志和 metadata 字段齐全，且没有把未知值伪装成真实 seed 或真实运行日志。
8. 策略文档与代码行为一致。
9. 没有运行完整构建、完整 Simulator Lab、大规模回放或性能测试。
10. 没有提交、推送或修改禁止文件。

如果任何一项无法证明，停止并在报告中标为未完成或阻塞，不要通过扩大修改范围来绕过。

---

## 最终报告格式

最终回复必须按以下顺序列出：

1. 修改文件  
2. 默认流程变化  
3. 默认保留的文件  
4. 默认不保留的文件  
5. -CaptureEvidence 用法  
6. -OutputRoot 用法  
7. 缓存键组成与 ready 标记机制  
8. 失败和中断处理  
9. 验证命令、退出码和结果  
10. 普通运行预计磁盘占用及估算依据  
11. 剩余风险和无法在本次范围内解决的问题  
12. 未修改文件  
13. 未执行的高成本操作  
14. AGENTS.md 查找结果  
15. 工作区原有改动如何被保留

报告必须给出精确的修改路径、验证输出摘要和任何未完成项。不要声称执行了没有执行的构建或运行。

