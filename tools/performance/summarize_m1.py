"""Read sealed M1 inputs; write a fresh, derived pilot baseline only."""
import json
import pathlib
import statistics
from pilot import LAB, extract, sha, write_new

work = LAB / ".build/runs/performance-pilot-work01"
rows = []
for batch in ["m1-acceptance-work06", "m1-acceptance-work07"]:
    parent = LAB / ".build/runs" / batch
    for path in sorted(parent.iterdir()):
        if not path.is_dir() or not path.name.endswith("final06"):
            continue
        if not (path.name.startswith("eval-") and ("-live-" in path.name or "-replay-" in path.name)
                or path.name in ["dev-live-final06", "dev-replay-detailed-final06", "dev-replay-minimal-final06"]):
            continue
        measurement = extract(path)
        measurement["sourceHashes"] = {n: sha(path / n) for n in ["process.json", "result.json", "timing.json", "trace.jsonl", "build-manifest.json"]}
        rows.append(measurement)
live = [r for r in rows if r["mode"] == "live"]
assert len(live) == 7 and len(set(r["buildId"] for r in rows)) == 1
seconds = [r["wallSeconds"] for r in live]
evidence = [r["evidenceBytes"] for r in live]
write_new(work / "m1-cost-baseline.json", dict(
    scope="Seven existing Ironclad runs, one RunVictory and six Deaths; descriptive costs only, not a random performance population",
    rows=rows, live=dict(count=len(live), wallMin=min(seconds), wallMedian=statistics.median(seconds),
                         wallMax=max(seconds), serial100MinutesRange=[min(seconds)*100/60, max(seconds)*100/60],
                         evidence100GiBRange=[min(evidence)*100/2**30, max(evidence)*100/2**30]),
    extrapolation="Conditional arithmetic, same workload/policy/hardware/observation; no 100-run execution and no cross-character guarantee",
    timingWarning="Nested scopes/member work can overlap. Replay re-solves. Do not sum or subtract as exclusive search cost.",
    unknown=["whole-process CPU for old runs", "exclusive phase CPU", "all-character full-run cost", "unseen seeds", "fair-policy outcomes"]
))
print(json.dumps(dict(rows=len(rows), liveSeconds=seconds, buildId=rows[0]["buildId"])))
