"""Bounded native pilot orchestration and read-only timing extraction.

Only fresh Lab run directories are written. Existing M1 evidence is never updated.
"""
import argparse
import hashlib
import json
import pathlib
import shutil
import subprocess
import time
from collections import Counter
from datetime import datetime, timezone

LAB = pathlib.Path(__file__).resolve().parents[2]


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def write_new(path, value):
    with path.open("x", encoding="utf-8") as f:
        json.dump(value, f, ensure_ascii=False, indent=2)


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest().upper()


def size(path):
    return sum(p.stat().st_size for p in path.rglob("*") if p.is_file())


def extract(directory):
    process = read(directory / "process.json")
    result = read(directory / "result.json") if (directory / "result.json").exists() else {}
    timing = read(directory / "timing.json") if (directory / "timing.json").exists() else {}
    queries = [read(p) for p in directory.glob("solver-query-*.json")]
    steps = [read(p) for p in directory.glob("solver-online-step-*.json")]
    members, member_time = Counter(), Counter()
    for query in queries:
        for member in query.get("work", {}).get("attributions", []):
            name = member.get("Mechanism", member.get("Name", "unknown"))
            members[name] += member.get("ExpandedNodes", 0)
            member_time[name] += member.get("ElapsedMilliseconds", 0)
    terminal, character, observations, battles = None, None, 0, 0
    for line in (directory / "trace.jsonl").open(encoding="utf-8-sig"):
        row = json.loads(line)["payload"]
        detail = row.get("detail") or {}
        if row["kind"] == "new-run":
            character = detail["facts"]["character"]
        if "state" in detail:
            observations += 1
        if row["kind"] == "solver-battle-complete":
            battles += 1
            terminal = detail["facts"]["outcome"]
    return dict(directory=str(directory.relative_to(LAB)), buildId=process["buildId"],
                seed=process["seed"], character=character, mode=process.get("executionKind"),
                rawExit=process["rawExit"], exitConsistent=process["exitConsistent"],
                result=result.get("result"), nativeBattleTerminal=terminal, battles=battles,
                wallSeconds=process["elapsedSeconds"], peakWorkingSetBytes=process.get("peakWorkingSet"),
                cpuMilliseconds=timing.get("processCpuMillisecondsBeforeExit"),
                measuredScopes=timing.get("milliseconds"), scopeCalls=timing.get("calls"),
                queries=len(queries), totalExpanded=sum(q["TotalExpandedNodes"] for q in queries),
                upstreamSearchMilliseconds=sum(q.get("searchElapsedMilliseconds", 0) for q in queries),
                memberExpanded=dict(members), memberMilliseconds=dict(member_time),
                searchBoundaries=dict(Counter(q["boundary"] for q in queries)),
                stateRngExactSteps=sum(s.get("exact", False) and s.get("expected") == s.get("actual") for s in steps),
                parentIsolationPassSteps=sum(s.get("parentUnchanged", False) for s in steps),
                exactSteps=sum(s.get("exact", False) and s.get("parentUnchanged", False) for s in steps),
                steps=len(steps), stateObservations=observations, evidenceBytes=size(directory),
                missingFields=[k for k in ["processCpuMillisecondsBeforeExit"] if k not in timing])


def run(work, build, character, seed_index, replay=None, label=""):
    registration = read(work / "registration.json")
    seed = registration["seeds"][seed_index - 1]
    if character not in registration["characters"]:
        raise ValueError("Unregistered character")
    ledger = work / "launches.jsonl"
    prior = [json.loads(s) for s in ledger.read_text().splitlines()] if ledger.exists() else []
    first = prior[0]["startedUnix"] if prior else time.time()
    if len(prior) >= registration["maxNativeProcesses"] or time.time() - first >= registration["nativeWallSeconds"] or size(work) >= registration["maxEvidenceBytes"]:
        raise RuntimeError("Registered pilot budget exhausted")
    mode = "replay" if replay else "live"
    if any(c not in "abcdefghijklmnopqrstuvwxyz0123456789-" for c in label):
        raise ValueError("Invalid evidence label")
    name = label + f"{character.lower()}-{seed_index:02d}-{mode}"
    target = work / name
    inp = work / (name + "-input")
    if target.exists() or inp.exists():
        raise RuntimeError("Refusing duplicate launch or overwrite")
    inp.mkdir()
    for filename in ["route.json", "choices.json", "reward-choices.json"]:
        if replay:
            if filename == "reward-choices.json" and not (replay / filename).exists():
                rows = [json.loads(s)["payload"] for s in (replay / "trace.jsonl").read_text(encoding="utf-8-sig").splitlines()]
                if rows[0]["detail"].get("rewardProfile") is not None or any(r["kind"].startswith(("n3c-", "reward", "rc-")) for r in rows):
                    raise RuntimeError("Missing reward transcript for an active reward session")
                write_new(inp / filename, [])  # This first-battle profile never opens a reward session.
            else:
                shutil.copyfile(replay / filename, inp / filename)
        else:
            write_new(inp / filename, [])
    plan = dict(Schema="solver-coordinator-prefix/v1", PrefixCount=0, MaxActions=64, Probe=False,
                InputSha=sha(inp / "route.json"), ChoiceSha=sha(inp / "choices.json"),
                RewardSha=sha(inp / "reward-choices.json"), Character=character)
    write_new(inp / "plan.json", plan)
    scenario = "solver-character-pilot" + ("-replay" if replay else "")
    cmd = ["pwsh", "-NoProfile", "-File", str(LAB / "tools/native-run/run-native-slice.ps1"),
           "-BuildManifest", str(build), "-OutputDirectory", str(target), "-Seed", seed,
           "-Scenario", scenario, "-Observation", "detailed", "-ReplayPath", str(inp / "route.json"),
           "-SolverPlan", str(inp / "plan.json"), "-ProcessTimeoutSeconds", "120"]
    launch = dict(name=name, startedUnix=time.time(), utc=datetime.now(timezone.utc).isoformat(),
                  buildId=read(build)["buildId"], command=cmd, inputHashes={p.name: sha(p) for p in inp.iterdir()})
    with ledger.open("a", encoding="utf-8") as f:
        f.write(json.dumps(launch) + "\n")
    forced = None
    with (work / (name + "-wrapper.log")).open("x", encoding="utf-8") as log:
        process = subprocess.Popen(cmd, cwd=LAB, stdout=log, stderr=subprocess.STDOUT)
        while process.poll() is None:
            if time.time() - first >= registration["nativeWallSeconds"]:
                forced = "batch-wall-budget"
            elif size(work) >= registration["maxEvidenceBytes"]:
                forced = "batch-storage-budget"
            if forced:
                subprocess.run(["pwsh", "-NoProfile", "-Command",
                                f"[Diagnostics.Process]::GetProcessById({process.pid}).Kill($true)"], check=True)
                process.wait(timeout=10)
                break
            time.sleep(1)
    completion = dict(name=name, wrapperExit=process.returncode, forcedStop=forced,
                      endedUtc=datetime.now(timezone.utc).isoformat(), batchBytes=size(work))
    if (target / "process.json").exists():
        completion["measurement"] = extract(target)
    write_new(work / (name + "-completion.json"), completion)
    print(json.dumps(completion, ensure_ascii=False), flush=True)
    if forced:
        raise RuntimeError(forced)


if __name__ == "__main__":
    ap = argparse.ArgumentParser()
    ap.add_argument("mode", choices=["run", "extract"])
    ap.add_argument("--work", type=pathlib.Path)
    ap.add_argument("--build", type=pathlib.Path)
    ap.add_argument("--character")
    ap.add_argument("--seed-index", type=int, choices=[1, 2])
    ap.add_argument("--replay", type=pathlib.Path)
    ap.add_argument("--label", default="")
    ap.add_argument("--directory", type=pathlib.Path)
    args = ap.parse_args()
    if args.mode == "run":
        work = args.work.resolve()
        if not work.is_relative_to(LAB / ".build/runs"):
            raise ValueError("Pilot writes must remain in Lab runs")
        run(work, args.build.resolve(), args.character, args.seed_index, args.replay, args.label)
    else:
        print(json.dumps(extract(args.directory), ensure_ascii=False, indent=2))
