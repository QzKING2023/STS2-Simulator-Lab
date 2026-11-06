"""Seal a finite pilot including explicit unsupported and differential failures."""
import hashlib
import json
from collections import Counter
from datetime import datetime, timezone
from pathlib import Path
from pilot import LAB, read, write_new, size

work = LAB/'.build/runs/performance-study-work01'
build = LAB/'.build/native-build/performance-study-02'
manifest = read(build/'build-manifest.json')
def digest(p):
    with p.open('rb') as f: return hashlib.file_digest(f,'sha256').hexdigest().upper()
checks=[]
def check(path, expected, group):
    checks.append(dict(path=str(path.relative_to(LAB)) if path.is_relative_to(LAB) else str(path),group=group,
                       passed=path.exists() and digest(path)==expected))
for section in ['source','inputs','compiled','artifacts']:
    for r in manifest['manifest'][section]:
        check(build/r['path'] if section in ['compiled','artifacts'] else Path(r['path']),r['sha256'],'build-'+section)
for file,group in [(work/'prior-evidence-seal.json','prior-pilot-and-frozen-build'),
    (LAB/'.build/runs/performance-pilot-work01/protected-baseline.json','M1-snapshot'),
    (LAB/'.build/runs/performance-pilot-work02/prior-pilot-and-dependency-seal.json','earlier-pilot-and-solver'),
    (LAB/'.build/runs/juggernaut-phase-work01/old-fixed-inputs.json','15-inputs-52-compat')]:
    for p,h in read(file).items():check(LAB/p,h,group)
m1=LAB/'.build/runs/m1-acceptance-work07'
for r in read(m1/'native-evidence-seal.json')['files']:check(m1/r['path'],r['sha256'],'M1-raw')
write_new(work/'preservation-audit.json',dict(checks=len(checks),groups=dict(Counter(x['group'] for x in checks)),mismatches=[x for x in checks if not x['passed']]))
assert all(x['passed'] for x in checks),'Preservation failed'
launches=[json.loads(s) for s in (work/'launches.jsonl').read_text().splitlines()]
processes=[read(work/l['name']/'process.json') for l in launches]
reg=read(work/'registration.json')
assert len(launches)==11 and len(launches)<=reg['maxNativeProcesses']
assert all(p['buildId']==manifest['buildId'] and p['exitConsistent'] and not p['watchdogExpired'] for p in processes)
window=(max(datetime.fromisoformat(p['endedUtc']) for p in processes)-datetime.fromtimestamp(launches[0]['startedUnix'],timezone.utc)).total_seconds()
assert window<reg['nativeWallSeconds'] and size(work)<reg['maxEvidenceBytes']
measure=read(work/'measurements.json');search=read(work/'search-analysis.json');visible=read(work/'visible-policy-review.json')
assert all(not r['mismatches'] for r in measure['replays']) and len(measure['replays'])==1
assert search['counts']['searches']==80 and all(not r['unexpectedDifferences'] for r in search['observerComparisons'])
assert all(r['passed'] for r in visible['controls']) and read(work/'core-checks.json')['exit']==0
assert all(r['matched'] for r in read(work/'discard-controls/summary.json')['controls'])
for name in ['review-study-ironclad','review-study-silent','review-study-silent-clean','review-study-regent','review-study-necrobinder','review-study-defect','review-full-silent-live']:
    assert read(work/(name+'.json'))['exit']==0
for name in ['review-full-silent-replay','review-full-regent-prefix-v2','review-full-necrobinder-prefix-v2']:
    assert all(read(work/(name+'.json'))['checks'].values())
failed=read(work/'review-full-defect-failure.json')
assert failed['result']['Stop']=='DataError' and failed['production']['exit']==1 and not failed['checks']['successfulPrefix']
raw=[]
for l in launches:
    for p in sorted((work/l['name']).rglob('*')):
        if p.is_file():raw.append(dict(path=str(p.relative_to(work)),bytes=p.stat().st_size,sha256=digest(p)))
write_new(work/'native-evidence-seal.json',dict(schema='pilot-native-file-seal/v1',buildId=manifest['buildId'],files=raw))
full=[m for m in measure['measurements'] if '-full-' in m['directory'] and m['mode']=='live']
outcomes=[dict(character=m['character'],stop=m['result']['Stop'],terminal=m['result']['Terminal'],
    confirmed=m['result']['Confirmed'],dispatched=m['result']['Dispatched'],exactSteps=m['exactSteps'],steps=m['steps'],
    seconds=m['wallSeconds'],searchSeconds=m['measuredScopes']['solver-search-wall']/1000) for m in full]
write_new(work/'closeout.json',dict(schema='performance-study-closeout/v1',utc=datetime.now(timezone.utc).isoformat(),
    outcome='PARTIAL: bounded study and visible DTO controls passed; 1 completed full sample, 2 Unsupported, 1 DataError; no all-character or fair-policy certification',
    buildId=manifest['buildId'],solverSha256=digest(build/'bin/Release/net9.0/CombatSolver.dll'),
    nativeExecutions=len(launches),searchStudy=search['counts'],fullSamples=outcomes,replays=measure['replays'],
    visible=dict(consumerControls=len(visible['controls']),probeRoots=len(visible['probes']),isolationScope='trusted DTO projection and pure consumer only',fullPolicyIsolationPassed=False),
    coreChecks=98,discardControls=10,
    budget=dict(executions=len(launches),limit=reg['maxNativeProcesses'],windowSeconds=window,limitSeconds=reg['nativeWallSeconds'],
        nativeSeconds=sum(p['elapsedSeconds'] for p in processes),evidenceBytes=size(work),limitBytes=reg['maxEvidenceBytes']),
    preservation=dict(checks=len(checks),mismatches=0,newRawFiles=len(raw)),
    incidents=['Silent initial timing retained/excluded due potential separate worker-build overlap; one reserve clean repeat used',
       'Analyzer initially confused continuation and native action root; corrected analysis using oldSequence, source state and continuation, no raw rewrite',
       'Initial unsupported-prefix wrapper wrongly required zero task faults; separate isolation and exact propagated failure checks added, old rejection retained',
       'Worker build emitted NU1900 vulnerability-service metadata warning; cached dependency build completed, no settings or credentials changed'],
    next=['Diagnose Defect end-turn terminal card-history reset before any larger Defect batch',
          'Reuse existing generic native selector/token contract for Duplicate/ApplyRetain with exact instance/effect tests',
          'Deploy visible DTO policy in bounded native loop with fresh legal tickets, class-visible fields and hidden-world invariance before fair comparisons',
          'Use later-battle immutable roots and multiple preregistered seeds before choosing new presets or scaling samples'],
    allRegisteredProcessesExited=True,committed=False,pushed=False))
print(json.dumps(dict(outcome='PARTIAL',nativeExecutions=len(launches),preservationChecks=len(checks),windowSeconds=window,newRawFiles=len(raw))))
