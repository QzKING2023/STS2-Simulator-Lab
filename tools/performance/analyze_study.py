"""Analysis of registered immutable search records; not a new native execution."""
import argparse
import hashlib
import json
import statistics
from collections import Counter, defaultdict
from pilot import LAB, read, write_new
from summarize_repairs import compare


def analyze(work):
    roots, repeat_pairs, dop_pairs, profile_pairs, observer = [], [], [], [], []
    chosen = {c: work / (('clean-' if c == 'silent' else 'b02-') + f'study-{c}-live')
              for c in ['ironclad', 'silent', 'regent', 'necrobinder', 'defect']}
    for char, directory in chosen.items():
        for q in [1, 6]:
            data = read(directory / f'search-study-{q}.json')
            source = read(directory / f'solver-query-{q}.json')
            rows = data['records']
            assert data['complete'] and len(rows) == 8
            step = read(directory / f'solver-online-step-{q}.json')
            assert data['nativeState'] == source['nativeState'] and data['continuation'] == source['root']
            # Query precedes dispatch: diagnostic Root/Context still identify the last
            # completed native dispatch, not the forthcoming solver action.
            assert data['root'] == f"a{step['oldSequence']}" and data['nativeState'] == step['nativeBefore']
            assert data['information'] == 'Oracle'
            assert {(r['profileName'], r['dop'], r['repeat']) for r in rows} == {
                (p, d, i) for p in ['LabBounded', 'UpstreamLow'] for d in [1, 2] for i in [1, 2]}
            for r in rows:
                assert r['sourceUnchanged'] and r['cancellationSeconds'] == 10
                assert r['error'] is not None or r['result'] is not None and r['legalFirst']
                p = r['requestedProfile']
                expected = (8, 256, 32, 18, 24, 10000) if r['profileName'] == 'LabBounded' else (45, 60000, 24, 12, 16, 60000)
                assert tuple(p[k] for k in ['BeamWidth', 'MaxExpandedNodes', 'MaxCardBranchesPerNode',
                    'MaxPileChoiceBranchesPerAction', 'MaxHandChoiceBranchesPerAction', 'SoftTimeBudgetMilliseconds']) == expected
                assert not p['StopPortfolioAtHpTarget']
                # Result hash is checked against the actual serialized action payload too.
                # JSON serializer differences make Python byte serialization inappropriate for recomputing C# hash.
            grouped = {(p, d): [r for r in rows if (r['profileName'], r['dop']) == (p, d)]
                       for p in ['LabBounded', 'UpstreamLow'] for d in [1, 2]}
            def facts(r):
                v = r['result']
                return None if v is None else {k: v[k] for k in ['boundary', 'actionHash', 'actions', 'quality', 'DeathTurn', 'CombatEndedTurn']}
            for (p, d), pair in grouped.items():
                repeat_pairs.append(dict(character=char, query=q, profile=p, dop=d,
                    sameOutcome=facts(pair[0]) == facts(pair[1]),
                    sameWork=pair[0]['result'] is not None and pair[1]['result'] is not None and pair[0]['result']['TotalExpandedNodes'] == pair[1]['result']['TotalExpandedNodes'],
                    wallMilliseconds=[r['wallMilliseconds'] for r in pair],
                    errors=[r['error'] for r in pair]))
            for p in ['LabBounded', 'UpstreamLow']:
                a, b = grouped[p, 1], grouped[p, 2]
                ma, mb = [statistics.median(r['wallMilliseconds'] for r in x) for x in [a, b]]
                dop_pairs.append(dict(character=char, query=q, profile=p, singleMedianMs=ma, dualMedianMs=mb,
                    singleOverDual=ma/mb, samePredictedOutcome=facts(a[0]) == facts(b[0]),
                    observedParallel=[r['result']['MaxParallelExpansionConcurrency'] if r['result'] else None for r in b]))
            for d in [1, 2]:
                a, b = grouped['LabBounded', d][0]['result'], grouped['UpstreamLow', d][0]['result']
                profile_pairs.append(dict(character=char, query=q, dop=d,
                    labQuality=a['quality'] if a else None, lowQuality=b['quality'] if b else None,
                    sameActions=a is not None and b is not None and a['actions'] == b['actions'],
                    labBoundary=a['boundary'] if a else None, lowBoundary=b['boundary'] if b else None))
            roots.append(dict(character=char, query=q, records=rows, elapsedMilliseconds=data['elapsedMilliseconds']))
        baseline = LAB / f'.build/runs/performance-pilot-work02/b01-{char}-01-live'
        comparison = compare(baseline, directory)
        # Deliberately different collector builds, both using the same solver/input pin.
        comparison['scope'] = 'cross-build observer check only; same solver/input and baseline policy, not same-build replay'
        comparison['unexpectedDifferences'] = [x for x in comparison['mismatches'] if x != 'sameBuild']
        observer.append(comparison)
    total = [r for root in roots for r in root['records']]
    by_cell, members = {}, defaultdict(Counter)
    for p in ['LabBounded', 'UpstreamLow']:
        for d in [1, 2]:
            rows = [r for r in total if r['profileName'] == p and r['dop'] == d]
            by_cell[f'{p}:DOP{d}'] = dict(n=len(rows), medianWallMs=statistics.median(r['wallMilliseconds'] for r in rows),
                sumWallMs=sum(r['wallMilliseconds'] for r in rows), sumCpuMs=sum(r['cpuMilliseconds'] for r in rows),
                allocatedBytes=sum(r['allocatedBytes'] for r in rows),
                boundaries=dict(Counter(r['result']['boundary'] if r['result'] else r['error'] for r in rows)),
                maxObservedConcurrency=max(r['result']['MaxParallelExpansionConcurrency'] if r['result'] else 0 for r in rows))
    for r in total:
        if r['result']:
            for m in r['result']['work']:
                key = r['profileName'] + ':' + m['Mechanism']
                for k in ['ElapsedMilliseconds', 'ExpandedNodes', 'Transitions', 'WorkerAllocatedBytes', 'GcPauseMilliseconds']:
                    members[key][k] += m.get(k, 0)
    return dict(schema='performance-search-study-analysis/v1', roots=roots, byCell=by_cell,
        repeatPairs=repeat_pairs, dopPairs=dop_pairs, profilePairs=profile_pairs, memberAttribution=dict(members),
        observerComparisons=observer,
        counts=dict(roots=len(roots), searches=len(total), repeatedPairs=len(repeat_pairs),
                    repeatedOutcomeEqual=sum(x['sameOutcome'] for x in repeat_pairs),
                    repeatedWorkEqual=sum(x['sameWork'] for x in repeat_pairs),
                    dopOutcomeEqual=sum(x['samePredictedOutcome'] for x in dop_pairs)),
        caveats=['ten roots in five first battles, one seed, not independent population or late-run coverage',
                 'predicted candidate quality only; alternatives not natively deployed',
                 '10 second cooperative cancellation separate from 60 second Low soft budget',
                 'cold startup/JIT/order and two repetitions limit timing precision; repeats reversed',
                 'upstream member attribution overlaps; do not sum GC pauses or call it exclusive CPU',
                 'original Silent measurement retained but excluded from timing due worker-build overlap'])


if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('--work', required=True); p.add_argument('--output', required=True)
    a = p.parse_args(); output = (LAB/a.output).resolve()
    if not output.is_relative_to(LAB/'.build/runs'): raise ValueError('Lab-only output')
    r = analyze((LAB/a.work).resolve()); write_new(output, r)
    print(json.dumps(dict(counts=r['counts'], cells=r['byCell'], observer=[x['unexpectedDifferences'] for x in r['observerComparisons']])))
