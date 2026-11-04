"""API-only controls for the production full-card/enchantment action binder.

Uses one authentic enchanted query and its actual native dispatch. Does not run
the game host, edit the input evidence, or certify the enclosing run as passed.
"""
import argparse
import copy
import json
from pathlib import Path
import subprocess


def read(path):
    return json.loads(path.read_text(encoding="utf-8-sig"))


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--build", required=True, type=Path)
    parser.add_argument("--run", required=True, type=Path)
    parser.add_argument("--output", required=True, type=Path)
    args = parser.parse_args()
    if args.output.exists():
        raise RuntimeError("Use a fresh output directory; evidence is immutable")
    args.output.mkdir(parents=True)
    manifest = read(args.build / "build-manifest.json")
    exe = args.build / "bin/Release/net9.0/OfflineSearchHarness.dll"
    records = [read(p) for p in args.run.glob("solver-online-step-*.json")]
    step = min((s for s in records if s["action"]["CardEnchantmentId"]), key=lambda s: s["query"])
    query = read(args.run / f'solver-query-{step["query"]}.json')
    events = [json.loads(s)["payload"] for s in (args.run / "trace.jsonl").read_text(encoding="utf-8-sig").splitlines()]
    dispatch = [e for e in events if e["kind"] == "dispatch" and e["root"] == step["root"]]
    if len(dispatch) != 1:
        raise RuntimeError("Authentic unique native dispatch required")
    payload = dict(action=step["action"], query=query,
                   decision=dispatch[0]["detail"]["facts"]["decision"])
    selected = next(i for i, c in enumerate(query["binding"]["hand"]) if c["id"] == payload["decision"]["card"])

    controls = [("authentic", payload, 0)]

    def add(name, mutate):
        item = copy.deepcopy(payload)
        mutate(item)
        controls.append((name, item, 1))

    def action(name, value):
        return lambda p: p["action"].__setitem__(name, value)

    def enchanted(name, value):
        return lambda p: p["query"]["binding"]["hand"][selected]["enchantment"].__setitem__(name, value)

    add("missing-enchantment-id", action("CardEnchantmentId", ""))
    add("unknown-enchantment-id", action("CardEnchantmentId", "UNREGISTERED_CONTROL"))
    add("wrong-full-state-key", action("CardStateKey", payload["action"]["CardStateKey"] + "-wrong"))
    add("wrong-model-occurrence", action("CardOccurrence", 999))
    add("wrong-state-occurrence", action("CardStateOccurrence", 999))
    add("wrong-upgrade", action("CardUpgradeLevel", payload["action"]["CardUpgradeLevel"] + 1))
    add("wrong-native-instance", lambda p: p["decision"].__setitem__("card", "i-control-wrong"))
    add("wrong-amount", enchanted("amount", 999))
    add("wrong-status", enchanted("status", 999))
    add("wrong-receiver", enchanted("type", "Unregistered.Control"))
    add("wrong-assembly", enchanted("assembly", "Unregistered.Control"))
    add("not-registered", enchanted("registered", False))
    add("incomplete-hand-map", lambda p: p["query"]["binding"]["hand"].pop())
    add("duplicate-hand-map", lambda p: p["query"]["binding"]["hand"].append(copy.deepcopy(p["query"]["binding"]["hand"][selected])))
    add("unknown-protocol", lambda p: p["query"]["binding"].__setitem__("cardIdentity", "unknown/v999"))
    add("legacy-protocol-still-rejects-enchanted-action", lambda p: p["query"]["binding"].pop("cardIdentity"))
    add("non-card-enchantment", action("Kind", "EndTurn"))
    outcomes = []
    for name, item, expected in controls:
        path = args.output / (name + ".json")
        path.write_text(json.dumps(item, ensure_ascii=False), encoding="utf-8")
        proc = subprocess.run(["dotnet", str(exe.resolve()), "--check-solver-action-binding", str(path.resolve())],
                              capture_output=True, text=True, encoding="utf-8", timeout=15)
        outcomes.append(dict(name=name, expected=expected, exit=proc.returncode,
                             passed=proc.returncode == expected, stderr=proc.stderr.strip()))
    summary = dict(scope="production action-binder API controls, no native execution; enclosing run status unchanged",
                   buildId=manifest["buildId"], run=str(args.run), query=step["query"], root=step["root"],
                   positive=1, negative=len(controls)-1, outcomes=outcomes,
                   passed=all(x["passed"] for x in outcomes))
    (args.output / "summary.json").write_text(json.dumps(summary, indent=2, ensure_ascii=False), encoding="utf-8")
    print(json.dumps({k: v for k, v in summary.items() if k != "outcomes"}))
    return 0 if summary["passed"] else 1


if __name__ == "__main__":
    raise SystemExit(main())
