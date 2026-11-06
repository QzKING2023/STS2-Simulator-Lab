"""Read completed pilot evidence, with explicit nonexclusive timing scopes."""
import argparse
import json
from collections import Counter, defaultdict
from itertools import zip_longest
from pilot import LAB, read, write_new, extract


def states(directory):
    with (directory / 'trace.jsonl').open(encoding='utf-8-sig') as f:
        for line in f:
            r = json.loads(line)['payload']
            if isinstance(r.get('detail'), dict) and 'state' in r['detail']:
                yield r['kind'], r['root'], r['context'], r['detail']['state']


def compare(live, replay):
    checks = dict(sameBuild=read(live / 'process.json')['buildId'] == read(replay / 'process.json')['buildId'])
    for name in ['route.json', 'choices.json', 'reward-choices.json']:
        if (live / name).exists() or (replay / name).exists():
            checks[name] = (live / name).exists() and (replay / name).exists() and read(live / name) == read(replay / name)
    sentinel = object()
    mismatch, observations = [], 0
    for i, (a, b) in enumerate(zip_longest(states(live), states(replay), fillvalue=sentinel), 1):
        observations += 1
        if a != b: mismatch.append(i)
    checks['fullStateRng'] = not mismatch
    steps = sorted(live.glob('solver-online-step-*.json'))
    checks['stepSet'] = {p.name for p in steps} == {p.name for p in replay.glob('solver-online-step-*.json')}
    for f in steps:
        a, b = read(f), read(replay / f.name)
        for k in ['action', 'children', 'expected', 'actual', 'nativeBefore', 'nativeAfter', 'parentUnchanged']:
            checks[f.stem + ':' + k] = a[k] == b[k]
    return dict(live=live.name, replay=replay.name, checks=len(checks), observations=observations,
                mismatches=[k for k, v in checks.items() if not v], stateMismatchIndices=mismatch)


def summarize(directory):
    result = extract(directory)
    acts, rooms, members = defaultdict(Counter), defaultdict(Counter), defaultdict(Counter)
    for path in directory.glob('solver-query-*.json'):
        q = read(path)
        state = q['nativeState']
        for group, key in [(acts, str(state['act'])), (rooms, f"{state['act']}:{state['node']}")]:
            group[key]['queries'] += 1
            group[key]['expanded'] += q['TotalExpandedNodes']
            group[key]['reportedSearchMilliseconds'] += q.get('searchElapsedMilliseconds', 0)
        for m in q.get('work', {}).get('attributions', []):
            name = m['Mechanism']
            for k in ['ExpandedNodes', 'Transitions', 'ChoiceBranches', 'ElapsedMilliseconds',
                      'WorkerAllocatedBytes', 'Gen0Collections', 'Gen1Collections', 'Gen2Collections',
                      'GcPauseMilliseconds', 'RecordedSolverCount']:
                if k in m: members[name][k] += m[k]
    terminal, events = [], Counter()
    with (directory / 'trace.jsonl').open(encoding='utf-8-sig') as f:
        for line in f:
            r = json.loads(line)['payload']; events[r['kind']] += 1
            if r['kind'] in ['full-act-enter', 'full-act-boss-complete', 'native-run-victory', 'native-player-death']:
                terminal.append(dict(kind=r['kind'], root=r['root'], context=r['context'], facts=(r.get('detail') or {}).get('facts')))
    files = sorted(((p.stat().st_size, str(p.relative_to(directory))) for p in directory.rglob('*') if p.is_file()), reverse=True)
    result.update(perAct=dict(acts), perCombatLocation=dict(rooms), memberTelemetry=dict(members),
                  lifecycle=terminal, eventCounts=dict(events), largestFiles=[dict(bytes=n, path=p) for n, p in files[:8]],
                  caveats=['search wall, native pump, root execution and observation scopes overlap; do not sum',
                           'member elapsed/allocation/GC are upstream attribution, not exclusive process CPU or unique GC pauses',
                           'per-act wall duration unavailable; only query-attributed search duration is grouped',
                           'replay still queries the solver; live minus replay is not search duration',
                           'all strategy inputs are Oracle, not player-visible'])
    return result


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--work', type=lambda x: (LAB / x).resolve(), required=True)
    ap.add_argument('--output', type=lambda x: (LAB / x).resolve(), required=True)
    a = ap.parse_args()
    launches = [json.loads(s) for s in (a.work / 'launches.jsonl').read_text().splitlines()]
    measurements = [summarize(a.work / r['name']) for r in launches]
    replays = []
    for r in launches:
        if r['name'].endswith('-replay'):
            replays.append(compare(a.work / (r['name'].removesuffix('-replay') + '-live'), a.work / r['name']))
    write_new(a.output, dict(schema='performance-repair-measurements/v1', measurements=measurements, replays=replays))
    print(json.dumps(dict(samples=len(measurements), replays=replays)))
    if any(r['mismatches'] for r in replays): raise RuntimeError('Replay mismatch retained')
