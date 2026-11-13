# Native build storage policy

This policy describes how `tools/native-run/build-native-run.ps1` (script
version `native-build-run/v3`, log format `native-build-run-log/v1`) generates
and retains build outputs, and how those outputs relate to the three maintenance
entry points. It retains the build-storage refactor's boundaries — the
original refactor changed only this build script and this document —
`tools/native-run/run-native-slice.ps1`,
`tools/maintenance/cleanup-native-build.ps1`, the NativeRun C# sources, tests
and configuration are untouched, and no existing evidence directory was
deleted or rewritten.

Three maintenance entry points exist and stay separate:

- **In-script cache maintenance**: `build-native-run.ps1 -Cleanup` (review)
  and `-Cleanup -ApplyCleanup` (apply) manage `.build/native-build/cache`
  only. See "Cache expiry and cleanup".
- **Batch bin/obj maintenance**: `tools/maintenance/cleanup-native-build.ps1`
  remains an independent review-first helper for
  `.build/native-build/<batch>/{bin,obj}` and is unchanged by this refactor.
- **Workspace output maintenance**: `tools/maintenance/cleanup-workspace.ps1`
  is the review-first boundary for explicit native build directories,
  temporary native game roots, and named scratch directories. It removes only
  direct children that are past the cutoff, have no tracked or retained-run
  reference (including references found in ignored `.build/runs` evidence),
  and pass the reparse-point and path checks. It never removes
  `.build/runs`, `local-mods`, pinned inputs, source trees, or Git metadata.
  The scheduled task installed for this workspace runs it daily with the
  fourteen-day default retention window.

The normal review/apply commands are:

```powershell
pwsh -NoProfile -File tools/maintenance/cleanup-workspace.ps1
pwsh -NoProfile -File tools/maintenance/cleanup-workspace.ps1 -Apply -Confirm:$false
```

The first command is read-only. The second also invokes the batch helper for
old `bin/obj` children and records its transcript under `.build/`.

### Batch helper (unchanged)

The batch helper removes only the fixed `bin`/`obj` children of a valid batch
(a direct child of `.build/native-build` whose root contains regular-file
`NativeRun.csproj` and `build-manifest.json` markers; reparse points anywhere
disqualify). Age is evaluated in UTC against the newest observed mtime
(default cutoff 14 days, `-Before` overrides; a future value is rejected). It
is review-first: a plain run deletes nothing; `-Apply` is required, `-WhatIf`
is supported. Size guards: a total above 2 GiB is flagged for manual review,
above the 5 GiB default `-MaxDeleteBytes` the deletion is blocked without
explicit `-Force` (which changes only the size decision, never the
whitelist). Targets are re-validated immediately before each removal; a
target or marker that changed between review and removal is a hard stop, and
a delete failure stops the run without retry. The timestamped stdout lines
are the audit record — retain them with the review ticket; do not put an
audit log under a protected evidence directory. There is no Git automatic
cleanup.

## What a normal build keeps

A normal build (no `-CaptureEvidence`) publishes exactly this set:

| Kept | Why |
| --- | --- |
| `bin/Release/net9.0/**` (as produced, incl. solver assemblies when the solver profile is active) | The runnable output. `run-native-slice.ps1` dispatches `bin/Release/net9.0/<executable>` and hash-verifies every `artifacts` row against this directory, before and after each run. |
| Compiled sources copied into the build root: `*.cs`, `NativeRun.csproj`, `UPSTREAM-LICENSE`, `RANDOM-FORESEER-LICENSE`, `SOLVER-LICENSE`, `foreseer-lock.json` | The `compiled` rows of `build-manifest.json` bind these files; `run-native-slice.ps1` hash-verifies them. They are the frozen record of what was actually compiled (the `GameBootstrap.cs` copy is edited during the build, so the Lab copy is not the compiled content). |
| `build-manifest.json` | Provenance and identity for the build; required by existing callers (`buildId`, `manifest.source/inputs/compiled/artifacts`, `executable`, `toolchain`, `profile`, …). Its payload schema stays `native-build/v2`. |
| `run-manifest.json` | The small machine-readable metadata file (schema `native-build-run/v2`; all v1 fields retained). |
| `run.log` | The single complete detailed log (header + full status/build output). |
| `.ready` | Cache readiness marker; content: `contentKey`, `buildId`, `schema=native-build/v2`, `logFormat`, `scriptVersion`, `completedUtc`. |

A normal build never retains:

- `obj/` — pruned (validated `-LiteralPath` removal) after `dotnet build` and
  the post-build solver/foreseer assembly copies succeed, before the manifest
  is written; a prune failure fails the build (classification
  `cleanup-failure`) rather than publishing the extra bulk.
- `source-snapshot/`, `compiled/`, `evidence-manifest.json` — evidence-package
  content, produced only by `-CaptureEvidence`.
- Duplicate detailed logs — the build output holds exactly one (`run.log`).
- Decompiled copies, temporary solver dependencies, or anything else
  regenerable from source, Git, lock files and the toolchain.

Three storage classes are distinguished by construction:

1. **Transient staging**: `.staging-<contentKey|-outputName>-<guid>` (under the
   cache root, or a sibling of an explicit `-OutputRoot`) and
   `.evidence-staging-<guid>` (inside a build output during evidence capture).
   Never published, never reusable, no `.ready`.
2. **Reusable minimal cache**: `.build/native-build/cache/<contentKey>` — the
   kept set above, published only complete-and-verified.
3. **Explicit evidence package**: the kept set **plus** `source-snapshot/`,
   `compiled/`, `evidence-manifest.json`, only under `-CaptureEvidence`.
4. **Failure diagnostics**: a staging directory reduced to `run-manifest.json`
   (failure variant) + `run.log` after `bin/` and `obj/` were pruned from it.

## The single detailed log and the small metadata file

Two files, never more copies:

- **`run.log`** (log format `native-build-run-log/v1`): a `key=value` header
  followed by `----- detailed log -----` and the complete buffered log of the
  invocation (every status line plus the full `dotnet build` stdout/stderr).
  The header records: `logFormat`, `schema`, `runId`, `startedUtc`,
  `finishedUtc`, `exitStatus`, `seed`, `seedSource`, `mode`, `output`,
  `outputRule`, `contentKey`, `cacheHit`, `cacheState`, `captureEvidence`,
  `sourceRevision`, `scriptVersion`, `scriptSha256`, `toolchain`,
  `solverVersion`, `foreseerVersion`, `gameVersion`, `gameCommit`, `buildId`,
  the parameter summary, `build.dotnetExit`, `build.stderrLineCount`, the
  run-output boundary pointers, and — on failure — `failure.classification`,
  `failure.phase`, `failure.message`. It is overwritten by each new invocation
  of the same output (latest invocation wins; older invocation logs are not
  accumulated).
- **`run-manifest.json`** (schema `native-build-run/v2`): the same fields in
  JSON plus `parameters` (game/host/snapshot paths, raw `-OutputRoot` value,
  switches, retention days), `version` (script version + sha256, log format,
  git revision, toolchain, solver/foreseer/game versions), `build`
  (`dotnetExit`, `stderrLineCount`; `not-executed` on a cache hit) and
  `runOutputs`.

### Seed recording

The build stage receives no seed and never invents one. Every record states
`seed=not-supplied` with
`seedSource=run stage only: tools/native/run-native-slice.ps1 -Seed (default
LAB_V0111_NATIVE_DEV_01); the build stage receives and consumes no seed`.

### Build output vs run output boundary

Run outputs are produced by `run-native-slice.ps1` in their own fresh
`.build/runs/<run-dir>/` directories: `console.log` (complete process stdout),
`stderr.log` (process stderr), `trace.jsonl` (the in-run native event trace,
see `docs/NATIVE_RUN_DATA_CONTRACT.md`), `process.json`, plus the copied
`build-manifest.json`. The build output **never** duplicates these; the
metadata points at them via the `runOutputs` block (`runConsoleLog`,
`runStderrLog`, `inRunTrace`, `producedBy`, `policy`).

## `-CaptureEvidence` (default off)

- Off unless the switch is passed explicitly. The default flow generates no
  `source-snapshot/`, no `compiled/`, and no duplicate logs.
- With `-CaptureEvidence`, the script captures into the build output:
  - `source-snapshot/` — the compiled sources currently in the build root
    (`*.cs`, `*.csproj`, the three license files);
  - `compiled/` — a flat copy of the contents of `bin/Release/net9.0`;
  - `evidence-manifest.json` — schema `native-build-evidence/v2`, keeping the
    v1 fields (`capturedUtc`, `contentKey`, `sourceSnapshot`, `compiled`) and
    adding: `logFormat`, `buildId`, `createdUtc`, `exitStatus`, `seed` +
    `seedSource`, `version` block, `config` (parameter summary), `inputs`
    (game assembly paths + sha256) and `outputs` — a file list of
    `source-snapshot/**`, `compiled/**`, `build-manifest.json`,
    `run-manifest.json`, `run.log`, each with sha256 and byte size.
- **Atomic publish**: everything is staged in `.evidence-staging-<guid>`,
  every staged file is hash-verified against its source, the manifest is
  written into the stage, then the content directories are moved into place
  and `evidence-manifest.json` is moved **last**. The manifest is the
  completion marker: a package without it is incomplete by construction, and
  the script refuses to recapture over a partial state (specific paths are
  reported with the refusal) or to overwrite an existing manifest (including
  v1 manifests from the previous script, which report their schema in the
  error). A completed v2 package makes recapture idempotent.
- On capture failure the staging tree is cleaned (failures reported with the
  exact path and error), the build output itself remains ready and reusable,
  and the invocation exits non-zero (classification
  `evidence-capture-failure`).
- **Naming and lifecycle**: evidence lives inside the build output that
  produced it. In the stable cache this deliberately inflates that one cache
  entry; it is opt-in, covered by the same retention as the entry, and the
  batch `bin/obj` maintenance helper never manages cache entries. Evidence in
  explicit output directories is protected by the same rules as other
  evidence (see "Protected paths").

## `-OutputRoot` rules

- **Empty or whitespace** (the default): build into the stable cache at
  `.build/native-build/cache/<contentKey>`.
- **Explicit value**: resolved with `[IO.Path]::GetFullPath` (relative paths
  resolve against the current working directory), then asserted to stay under
  the Lab root by the unchanged `Assert-UnderLab` check — out-of-Lab paths
  are rejected. The resolved directory **must not already exist** (`Use a
  fresh immutable build directory: <path>`); an existing directory is never
  overwritten and never silently redirected. Content is built in a staging
  sibling and moved into place only after the manifest and `.ready` are
  complete.
- The chosen rule and final output path are recorded in `run.log`
  (`output`, `outputRule`), `run-manifest.json`, and announced by `-DryRun`.

## Cache design

### Cache key inputs (schema `native-cache-key/v2`)

The content key is SHA-256 (first 32 hex chars) over a canonical JSON payload
containing:

- `sourceDigest` — SHA-256 over sorted `(absolute path, sha256)` rows of every
  input that `build-manifest.json` later binds: the four shared host source
  files, the host `LICENSE`, **every file directly in `tools/native-run/`**
  (all extensions, including this script), `src/SimulatorLab.Core/*.cs` and
  its `.csproj`, the two check-project files, the three game assemblies
  (`sts2.dll`, `GodotSharp.dll`, `0Harmony.dll`), and — when supplied — the
  foreseer `input-lock.json` and every input it lists;
- `sourceRevision` (git `rev-parse HEAD`), `workingTree` (hash of
  `git status --porcelain`), `scriptVersion` (`native-build-run/v3`),
  `scriptSha256`, `toolchainVersion` (`dotnet --version`);
- `solverVersion` + `solverLockSha256`, `foreseerVersion` +
  `foreseerLockSha256` (lock schemas stay pinned);
- `gameRelease`, `gameCommit`, `gameAssemblySha256`, and the relative host
  source path.

No timestamp participates in the key. The digest closes the previous gap
where a content edit inside an already-modified file could leave the
git-status-derived key unchanged while the manifest inputs changed. Solver
assembly contents are additionally hash-verified against the solver lock at
build time.

### Staging, ready marker, and reuse

1. A fresh build writes into `.staging-<contentKey>-<guid>` under the cache
   root (or a staging sibling for explicit outputs).
2. After `dotnet build`, the `obj/` tree is pruned; sources are copied; the
   manifest rows are computed; `build-manifest.json` is written atomically
   (tmp file + move); only then is `.ready` written.
3. The staging directory is moved to `.build/native-build/cache/<contentKey>`.
4. Reuse requires `.ready` **plus** `NativeRun.csproj`, `build-manifest.json`
   and `bin` (reparse points anywhere in the chain disqualify). A directory at
   the cache key without `.ready` is never reused, never overwritten, and the
   build fails with the exact path and the instruction to remove or review it.
5. **Race**: if the cache key appears while building and is ready, the losing
   staged tree is discarded and the ready entry is used (`cache-race-hit`); if
   it appeared and is not ready, the build fails (`cache-conflict`) without
   touching the foreign directory. A previously ready entry is never modified
   by a failed flow.
6. **Cache-hit integrity re-check**: on a cache hit the manifest rows are
   recomputed and compared against the entry's recorded `source`, `inputs`,
   `compiled` and `artifacts` rows; any drift fails as
   `cache-integrity-failure` instead of refreshing a manifest over unknown
   content. With the digest-pinned key, drift means tampering or corruption,
   not a legitimate rebuild.
7. Cache states (`cache-hit`, `cache-miss`, `cache-race-hit`,
   `present-but-not-ready`, `explicit-output-root`) are printed by `-DryRun`
   and recorded in `run-manifest.json` (`mode`, `cacheState`, `cacheHit`).

### Cache expiry and cleanup

- `build-native-run.ps1 -Cleanup` reviews; `-Cleanup -ApplyCleanup` applies.
  `-ApplyCleanup` requires `-Cleanup`.
- Cutoff: `now - RetentionDays` (default 14, `1..3650`), UTC. A ready entry is
  compared against the newest of its `.ready` and `run-manifest.json` mtimes,
  so reuse keeps an entry alive. Every candidate is listed with path, mtime
  and reason before any removal; removals are direct-child-validated
  `-LiteralPath` deletions; reparse points are skipped and reported.
- **Stale staging cleanup** (new in v3): interrupted-build staging directories
  (`.staging-*` direct children of the cache root) are listed and, past the
  same cutoff, removed under the same direct-child + name-prefix +
  reparse-point checks. An in-progress staging directory is minutes old and
  protected by the cutoff. This is the only deletion set `-Cleanup` gained in
  this refactor; key-named cache directories without `.ready` remain
  report-only.
- This in-script maintenance never touches anything outside
  `.build/native-build/cache`, and the batch helper's deletion scope is
  unchanged (it still manages only `.build/native-build/<batch>/{bin,obj}`
  under its own review-first, size-guarded flow).

## Failure and interruption

Failure classification (`failure.classification` in the retained metadata and
`build-failed classification=… phase=… reason=…` on the console):

| Classification | Phase | Meaning |
| --- | --- | --- |
| `input-validation-failure` | `input-validation` | Pinned game/host/snapshot checks failed before any output exists. |
| `cache-conflict` | `cache-conflict` | Cache key occupied without `.ready`, or a not-ready key appeared during the build. |
| `cache-integrity-failure` | `cache-integrity` | Cache entry rows drifted from the key. |
| `build-failure` | `build`, `startup` | `dotnet build` failed or a pre-build step threw. |
| `cleanup-failure` | `cleanup` | The `obj/` prune failed; nothing is published. |
| `metadata-write-failure` | `manifest`, `run-metadata` | Manifest/metadata writes failed. |
| `publish-failure` | `publish` | Publishing the staged tree failed. |
| `evidence-capture-failure` | `evidence-capture` | Evidence staging/verification/publish failed; the build output remains ready. |

Behavior:

- **After staging exists**: `bin/` and `obj/` are pruned from the staging
  directory (each prune validated and individually reported — a prune failure
  prints the exact path and error and retains the tree for review), then the
  failure `run-manifest.json` (with `startedUtc`, `finishedUtc`,
  `exitStatus=failed`, classification, exception message, seed record,
  versions, parameter summary, output path + rule, log format, stderr excerpt)
  and the complete `run.log` are retained in staging. The retained path is
  printed. If even that metadata write fails, the fact and error are printed
  and the console transcript is the record.
- **Before staging exists** (input validation, cache conflict): no files are
  created — nothing may poison the cache key — and the console transcript is
  the record.
- **No `.ready` is ever written on a failure path** (single `.ready` write
  site, on the success path after the manifest), so failed or interrupted
  output can never be cache-hit.
- **Ctrl+C / hard interruption**: cannot be trapped reliably; a staging
  directory (never a key-named directory) remains without `.ready`, is
  reported by `-Cleanup`, and is removable by `-Cleanup -ApplyCleanup` after
  the retention cutoff. The console transcript is the immediate record.
- **Existing ready caches are never overwritten or broken by a failed flow**;
  failures write only into their own staging.
- Cleanup failures (staging prune, evidence staging cleanup) are reported
  with specific paths and errors, never swallowed.

## Protected paths

Unchanged: `.build/local-reference`, `.build/decompiled`, `.build/core-study01`,
`.build/runs`, `.build/review`, `.build/pinned-inputs`,
`.build/source-snapshot`, `.build/compiled`, `.build/logs`, manifests and
provenance files, licenses, `src/`, `tests/`, `.git/`. This script's deletions
are confined to: the `obj/` child of its own current staging/output, `bin`/
`obj` children of a failed staging, stale `.staging-*` direct children of the
cache root (past cutoff, `-ApplyCleanup` only), expired ready cache entries
(`-ApplyCleanup` only), its own atomic-write temp files, a lost race's own
staging tree, and its own evidence staging tree. Every deletion is a validated
`-LiteralPath` removal; there is no wildcard, repository-recursive, or Git
cleanup anywhere.

## Estimated disk occupancy of a normal build

**Estimate — not measured**: this machine has no `.build/` tree and the task
forbids real builds, so the numbers below derive from repo-side measurements
plus documented batch totals.

- Compiled sources + licenses + locks in the build root: **~1.2–2 MiB**
  (measured: 139 `tools/native-run/*.cs` ≈ 1.13 MiB are the bulk; the host
  source files are not present locally but are four source files of ordinary
  size).
- `build-manifest.json` + `run-manifest.json` + `run.log` + `.ready`:
  **< 1 MiB** (row lists for ~150 hash-bound files plus a log of ordinary
  build output).
- `bin/Release/net9.0`: **tens of MiB, up to low hundreds of MiB with the
  solver profile** (game assemblies are referenced `Private="false"` and not
  copied; solver assemblies are). Basis: documented batch closeouts in this
  repo range from ~145 MB (batch incl. builds + runs + inputs) to ~486 MB
  (frozen build + new dependencies + outputs), so a single build's `bin` is
  below those totals.
- `obj/`: **0 after this refactor** (pruned before publish). Previously the
  SDK intermediate tree, typically of the same order as `bin`, was retained —
  its removal is the main normal-build saving.
- `-CaptureEvidence` adds a full second copy of `bin/Release/net9.0`
  (`compiled/`) plus a ~1–2 MiB `source-snapshot/` — i.e., roughly doubling
  the build output; that is why it is opt-in.

A normal run (`run-native-slice.ps1`) additionally creates its own
`.build/runs/<run-dir>/` (console/stderr logs, trace, process metadata);
that behavior is unchanged and out of scope here.

## Validation

Runtime validation requires PowerShell 7 (`pwsh`), matching the script's
`#requires -Version 7.0`:

```powershell
pwsh -NoProfile -Command "& { [void][System.Management.Automation.Language.Parser]::ParseFile((Resolve-Path '.\tools\native-run\build-native-run.ps1'), [ref]$null, [ref]$null) }"
pwsh -NoProfile -File .\tools\native-run\build-native-run.ps1 -?
pwsh -NoProfile -File .\tools\native-run\build-native-run.ps1 -GamePath .\__missing_game__ -HostSource .\__missing_host__ -DryRun
```

Static guarantees that hold without execution: `-CaptureEvidence` is a
non-mandatory switch (false unless passed), the `-DryRun` branch performs no
filesystem mutation and returns before any output directory is created, all
`Remove-Item` call sites use `-LiteralPath`, no `git clean/reset/checkout/
restore` exists in the script, and `.ready` has a single write site on the
success path after `build-manifest.json`.
