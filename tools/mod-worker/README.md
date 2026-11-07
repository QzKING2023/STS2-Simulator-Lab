# Isolated Mod worker

`SimulatorLab.ModWorker` is the only process in this repository that loads a
Workshop Mod DLL. The simulator core receives only JSON records from
`SimulatorLab.Core.Mods`; it never receives a Mod assembly, Godot object, or
Harmony state.

The worker accepts one JSON input and one **new** output directory:

```powershell
dotnet run --project tools/mod-worker/ModWorker.csproj -- \
  --input .build/mod-worker-input.json \
  --output .build/runs/mod-worker-<new-id>
```

`run-mod-worker.ps1` is the NativeRun bridge. It binds an existing
`build-manifest.json`, creates a fresh run directory, starts a fresh process,
and verifies the worker exit code against `run-manifest.json`.

The input must name the fixed snapshot
`local-mods/workshop-snapshot-20261106-163352` and its sibling manifest. The
worker resolves one requested Mod and its dependency closure for exactly
`0.111.0`, then reads only manifest-listed files in the selected exact variant.
It validates file length and SHA-256 before loading DLL metadata in a
collectible `AssemblyLoadContext`. It does not execute Mod entry points or
expose a filesystem view.

The run directory contains only the small run manifest, input summary, worker
log, launcher logs, and (when supplied) a copy of the bound build manifest.
`AccessAttempts` and `WriteAttempts` are rejection probes: they become
explicit `AccessBlocked` or `WriteBlocked` errors, and the run cannot report
success. The run manifest records zero Mod registrations, zero Harmony patches,
zero temporary files, and collectible-context teardown evidence.
