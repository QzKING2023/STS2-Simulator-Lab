"""Seal this pilot's measured results without changing historical raw evidence."""
import json
import pathlib
import subprocess
from datetime import datetime, timezone
from pilot import LAB, read, sha, write_new, extract, size

work = LAB / ".build/runs/performance-pilot-work01"
build = LAB / ".build/native-build/performance-pilot-04"
dll = build / "bin/Release/net9.0/OfflineSearchHarness.dll"
frozen = read(build / "build-manifest.json")
audit = []
for section in ["source", "inputs", "compiled", "artifacts"]:
    for row in frozen["manifest"][section]:
        path = build / row["path"] if section in ["compiled", "artifacts"] else pathlib.Path(row["path"])
        audit.append(dict(path=str(path), ok=sha(path) == row["sha256"]))
for path, digest in read(work / "protected-baseline.json").items():
    audit.append(dict(path=path, ok=sha(pathlib.Path(path)) == digest))
m1base = LAB / ".build/runs/m1-acceptance-work07"
seal = read(m1base / "native-evidence-seal.json")
for row in seal["files"]:
    path = m1base / row["path"]
    audit.append(dict(path=str(path), ok=path.stat().st_size == row["bytes"] and sha(path) == row["sha256"]))
write_new(work / "final-preservation-audit.json", dict(checks=len(audit), mismatches=[x for x in audit if not x["ok"]], m1RawFiles=len(seal["files"])))
assert all(x["ok"] for x in audit)

reviews = []
for path in sorted(work.glob("b02*-*")):
    if not path.is_dir() or not (path / "result.json").exists():
        continue
    result = read(path / "result.json")["result"]
    if result["Stop"] != "SliceBoundary":
        continue
    proc = subprocess.run(["dotnet", str(dll), "--review", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace")
    reviews.append(dict(name=path.name, generator=read(path / "process.json")["buildId"], checker=frozen["buildId"], exit=proc.returncode, message=proc.stderr.strip()))
controls = []
for path in sorted((work / "production-review").glob("binding-*.json")):
    proc = subprocess.run(["dotnet", str(dll), "--check-pilot-binding", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace")
    expected = 0 if path.stem == "binding-valid" else 1
    controls.append(dict(name=path.stem, exit=proc.returncode, expected=expected, passed=proc.returncode == expected, message=proc.stderr.strip()))
assert all(c["passed"] for c in controls)

def states(path):
    rows = [json.loads(s)["payload"] for s in (path / "trace.jsonl").read_text(encoding="utf-8-sig").splitlines()]
    return [(r["kind"], r["root"], r["context"], r["detail"]["state"]) for r in rows if isinstance(r.get("detail"), dict) and "state" in r["detail"]]

replays = []
for character in ["defect", "necrobinder"]:
    live = work / ("b02-" + character + "-01-live")
    replay = work / ("b02r-" + character + "-01-replay")
    checks = dict(sameBuild=read(live / "process.json")["buildId"] == read(replay / "process.json")["buildId"],
                  route=read(live / "route.json") == read(replay / "route.json"),
                  choices=read(live / "choices.json") == read(replay / "choices.json"),
                  fullStateAndRng=states(live) == states(replay))
    for step in live.glob("solver-online-step-*.json"):
        a, b = read(step), read(replay / step.name)
        for field in ["action", "children", "expected", "actual", "nativeBefore", "nativeAfter", "parentUnchanged"]:
            checks[step.stem + ":" + field] = a[field] == b[field]
    assert all(checks.values())
    replays.append(dict(character=character, checks=len(checks), passed=True, fullStateObservations=len(states(live)),
                        scope="same-build reproducibility; Necrobinder isolation failure remains a failure"))
failure = read(work / "b04-necrobinder-01-live/solver-parent-failure-14.json")
changes = [(a, b) for a, b in zip(failure["before"].split(";"), failure["after"].split(";")) if a != b]
failed = read(work / "b04-necrobinder-01-live/result.json")
assert failed["rawExit"] == 1 and failed["result"]["Stop"] == "DataError"
launches = [json.loads(s) for s in (work / "launches.jsonl").read_text().splitlines()]
measurements = [extract(work / row["name"]) for row in launches]
live_matrix = [m for m in measurements if pathlib.Path(m["directory"]).name.startswith("b02-")]
assert len(live_matrix) == 10 and len(launches) == 14
processes = [read(work / row["name"] / "process.json") for row in launches]
elapsed = (datetime.fromisoformat(processes[-1]["endedUtc"]) - datetime.fromtimestamp(launches[0]["startedUnix"], timezone.utc)).total_seconds()
write_new(work / "closeout.json", dict(
    schema="performance-pilot-closeout/v1", utc=datetime.now(timezone.utc).isoformat(),
    outcome="capability pilot completed; two character blockers, no all-character full-run claim",
    head="3d12a14ed0398fe1954f009383e72c042f5b7bc9", finalBuild=frozen["buildId"],
    matrix=live_matrix, measurements=measurements, reviews=reviews, controls=controls, replays=replays,
    failurePropagation=dict(rawExit=1, stop="DataError", confirmed=17, dispatched=17, query=14, parentChanges=changes),
    budget=dict(nativeLaunches=len(launches), nativeLimit=14, windowSeconds=elapsed, wallLimitSeconds=1800,
                nativeSeconds=sum(p["elapsedSeconds"] for p in processes), evidenceBytes=size(work), storageLimit=2**31),
    preservation=dict(checks=len(audit), mismatches=0, m1RawFiles=len(seal["files"])),
    limitations=["10-sample matrix generated on build02, final build04 only failure propagation rerun",
                 "Silent discard choice unsupported after native dispatch", "Necrobinder parent stamp unstable after terminal cleanup",
                 "Player-visible strategy not implemented", "No new full-run performance sample", "No benchmark speedup or win-rate claim"],
    next=["Reuse upstream discard token protocol", "Resolve branch-local Osty identity/lifecycle in a new pinned dependency if necessary",
          "Then preregister a few complete online runs and information-isolated policy tests"]
))
print(json.dumps(dict(nativeLaunches=len(launches), elapsedSeconds=elapsed, parentChanges=changes, auditChecks=len(audit), finalBuild=frozen["buildId"])))
