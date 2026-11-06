"""Preserve prior evidence and seal the finite repair/full-run pilot."""
import hashlib
import json
from datetime import datetime, timezone
from pathlib import Path
from pilot import LAB, read, write_new, size

work = LAB / '.build/runs/performance-pilot-work02'
build = LAB / '.build/native-build/performance-repair-01'
manifest = read(build / 'build-manifest.json')
measurements = read(work / 'measurements.json')
audit = []


def digest(path):
    with path.open('rb') as f:
        return hashlib.file_digest(f, 'sha256').hexdigest().upper()


def check(path, expected, group):
    audit.append(dict(path=str(path.relative_to(LAB)) if path.is_relative_to(LAB) else str(path),
                      group=group, passed=path.exists() and digest(path) == expected))


for section in ['source', 'inputs', 'compiled', 'artifacts']:
    for row in manifest['manifest'][section]:
        p = build / row['path'] if section in ['compiled', 'artifacts'] else Path(row['path'])
        check(p, row['sha256'], 'current-build-' + section)
for file, group in [(work / 'prior-pilot-and-dependency-seal.json', 'prior-pilot-and-solver'),
                    (LAB / '.build/runs/performance-pilot-work01/protected-baseline.json', 'M1-snapshot'),
                    (LAB / '.build/runs/juggernaut-phase-work01/old-fixed-inputs.json', '15-inputs-52-compat')]:
    for path, expected in read(file).items():
        check(LAB / path, expected, group)
m1 = LAB / '.build/runs/m1-acceptance-work07'
for row in read(m1 / 'native-evidence-seal.json')['files']:
    check(m1 / row['path'], row['sha256'], 'M1-raw')
write_new(work / 'preservation-audit.json', dict(checks=len(audit), groups={g:sum(a['group'] == g for a in audit) for g in sorted({a['group'] for a in audit})}, mismatches=[a for a in audit if not a['passed']]))
assert all(a['passed'] for a in audit), 'Preservation mismatch'

launches = [json.loads(s) for s in (work / 'launches.jsonl').read_text().splitlines()]
processes = [read(work / r['name'] / 'process.json') for r in launches]
end = max(datetime.fromisoformat(p['endedUtc']) for p in processes)
window = (end - datetime.fromtimestamp(launches[0]['startedUnix'], timezone.utc)).total_seconds()
reg = read(work / 'registration.json')
reviews = [read(p) for p in work.glob('review-*.json') if p.name not in ['review-entry-incident.json', 'review-full-dev-01.json']]
assert len(reviews) == len(launches) and all(r['exit'] == 0 and r['buildId'] == manifest['buildId'] for r in reviews)
assert len(launches) == 12 and len(launches) <= reg['maxNativeProcesses'] and window < reg['nativeWallSeconds'] and size(work) < reg['maxEvidenceBytes']
assert all(p['buildId'] == manifest['buildId'] and p['rawExit'] == 0 and p['exitConsistent'] for p in processes)
assert all(not r['mismatches'] for r in measurements['replays'])
controls = read(work / 'discard-controls/summary.json')['controls']
assert len(controls) == 10 and all(c['matched'] for c in controls)
pet = [dict(directory=p.parent.name, **read(p)) for p in work.glob('b01-*/solver-pet-controls.json')]
assert len(pet) == 9 and all(p['passed'] for p in pet)
native = [m for m in measurements['measurements'] if m['mode'] == 'live']
full = [m for m in native if Path(m['directory']).name.startswith('full-')]
assert len(full) == 2 and {m['result']['Terminal'] for m in full} == {'RunVictory', 'Death'}
seal = []
for r in launches:
    directory = work / r['name']
    for p in sorted(directory.rglob('*')):
        if p.is_file():
            seal.append(dict(path=str(p.relative_to(work)), bytes=p.stat().st_size, sha256=digest(p)))
write_new(work / 'native-evidence-seal.json', dict(schema='pilot-native-file-seal/v1', buildId=manifest['buildId'], files=seal))
write_new(work / 'closeout.json', dict(schema='performance-repair-closeout/v1', utc=datetime.now(timezone.utc).isoformat(),
    outcome='PASS declared repair and finite Oracle cost-pilot scope', buildId=manifest['buildId'],
    solverSha256=digest(LAB / '.build/pinned-inputs/combatsolver-0.50.1-osty-root01/CombatSolver.dll'),
    nativeExecutions=len(launches), certifiedReviews=len(reviews), liveFirstBattles=7, fullOnlineSamples=2,
    replays=measurements['replays'], pureDiscardControls=10, branchApiControls=len(pet),
    budget=dict(limit=reg['maxNativeProcesses'], windowSeconds=window, windowLimitSeconds=reg['nativeWallSeconds'],
                nativeSeconds=sum(p['elapsedSeconds'] for p in processes), evidenceBytes=size(work), byteLimit=reg['maxEvidenceBytes']),
    preservation=dict(checks=len(audit), mismatches=0, newRawFiles=len(seal)),
    limitations=['All results Oracle; player-visible information isolation not implemented',
                 'Discard execution coverage is two first-battle seeds/Survivor; no independent multi-discard, upgrade, Sly or arbitrary hook coverage',
                 'Osty A/B/A is a branch API fixture; native pet coverage two Necrobinder first battles including cleanup, not all pet mechanics or full class runs',
                 'Two complete online Ironclad samples are known M1 development/evaluation seeds, not held-out policy-quality evidence',
                 'No new native EVAL02 replay in this batch; DEV01 plus repaired first-battle replays only',
                 'Nonexclusive timing scopes and upstream GC/allocator attribution do not establish an exclusive CPU profile',
                 'No performance improvement, reliable win rate, all-content support, or 100-run claim'],
    retainedIncident='Wrong slice-review entry rejected full-run provenance; corrected to existing full-run reader without raw changes or native rerun',
    next=['Independent review of narrow repair and measurement artifacts', 'Establish player-visible projection and hidden-information invariance before fair policy comparison',
          'Measure upstream search member settings in a separate bounded paired experiment before optimizing or scaling']))
print(json.dumps(dict(outcome='PASS', build=manifest['buildId'], nativeExecutions=len(launches), auditChecks=len(audit), windowSeconds=window)))
