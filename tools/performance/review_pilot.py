"""Finite production-contract checks; no game initialization or native runs."""
import copy
import json
import subprocess
from pilot import LAB, read, write_new

work = LAB / ".build/runs/performance-pilot-work01"
build = LAB / ".build/native-build/performance-pilot-02"
dll = build / "bin/Release/net9.0/OfflineSearchHarness.dll"
out = work / "production-review"
out.mkdir(exist_ok=False)
results = []
for directory in sorted(work.glob("b02-*-live")):
    result = read(directory / "result.json")["result"]
    # Whole-battle certification is only applicable to completed slices.
    if result["Stop"] == "Unsupported":
        results.append(dict(name=directory.name, certified=False, reason="Unsupported; unfinished native action preserved", result=result))
        continue
    proc = subprocess.run(["dotnet", str(dll), "--review", str(directory)], capture_output=True, text=True, encoding="utf-8", errors="replace")
    write_new(out / (directory.name + ".json"), dict(exit=proc.returncode, stdout=proc.stdout, stderr=proc.stderr))
    results.append(dict(name=directory.name, certified=proc.returncode == 0, checkerExit=proc.returncode,
                        reason=proc.stderr.strip(), nativeExit=read(directory / "process.json")["rawExit"]))

rows = [json.loads(s)["payload"] for s in (work / "b02-ironclad-01-live/trace.jsonl").read_text(encoding="utf-8-sig").splitlines()]
original = dict(header=rows[0]["detail"], rows=[r for r in rows if r["kind"] == "new-run"])
cases = [("valid", original, 0)]
for name, mutate in [
    ("wrong-character", lambda x: x["header"]["coordinatorProfile"].update(Character="Defect")),
    ("unknown-character", lambda x: x["header"]["coordinatorProfile"].update(Character="Unknown")),
    ("false-visible-mode", lambda x: x["header"].update(information="PlayerVisible")),
    ("changed-limit", lambda x: x["header"]["coordinatorProfile"].update(MaxActions=65)),
    ("changed-parallelism", lambda x: x["header"]["coordinatorProfile"].update(SearchParallelism=2)),
    ("missing-origin", lambda x: x.update(rows=[])),
    ("duplicate-origin", lambda x: x["rows"].append(copy.deepcopy(x["rows"][0]))),
]:
    value = copy.deepcopy(original)
    mutate(value)
    cases.append((name, value, 1))
controls = []
for name, value, expected in cases:
    path = out / ("binding-" + name + ".json")
    write_new(path, value)
    proc = subprocess.run(["dotnet", str(dll), "--check-pilot-binding", str(path)], capture_output=True, text=True, encoding="utf-8", errors="replace")
    controls.append(dict(name=name, exit=proc.returncode, expected=expected, matched=proc.returncode == expected, message=proc.stderr.strip()))
write_new(out / "summary.json", dict(buildId=read(build / "build-manifest.json")["buildId"], runs=results, controls=controls))
print(json.dumps(dict(certified=sum(r["certified"] for r in results), samples=len(results), controls=controls), ensure_ascii=False))
assert all(c["matched"] for c in controls)
