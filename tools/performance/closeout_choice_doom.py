"""Seal a bounded choice/duration batch without changing any historical evidence."""
from pathlib import Path
from datetime import datetime,timezone
from collections import Counter
import hashlib,json
from pilot import LAB,read,write_new,size
from summarize_repairs import summarize,compare
w=LAB/'.build/runs/performance-choice-doom-work01';b=LAB/'.build/native-build/performance-choice-doom-04'
def sha(p):
 with p.open('rb') as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
checks=[]
def check(p,h,g):checks.append(dict(path=str(p),group=g,passed=p.exists() and sha(p)==h))
m=read(b/'build-manifest.json')
for group in ['source','inputs','compiled','artifacts']:
 for r in m['manifest'][group]:check(b/r['path'] if group in ['compiled','artifacts'] else Path(r['path']),r['sha256'],'build-'+group)
previous=LAB/'.build/runs/performance-compat-work01'
prior=read(w/'previous-closeout-lock.json');check(LAB/prior['path'],prior['sha256'],'previous-closeout')
for r in read(previous/'native-evidence-seal.json')['files']:check(previous/r['path'],r['sha256'],'previous-raw')
for f,g in [('.build/runs/performance-pilot-work01/protected-baseline.json','M1-snapshot'),('.build/runs/juggernaut-phase-work01/old-fixed-inputs.json','15-inputs-52-compat')]:
 for p,h in read(LAB/f).items():check(LAB/p,h,g)
m1=LAB/'.build/runs/m1-acceptance-work07'
for r in read(m1/'native-evidence-seal.json')['files']:check(m1/r['path'],r['sha256'],'M1-raw')
write_new(w/'preservation-audit.json',dict(checks=len(checks),groups=dict(Counter(c['group'] for c in checks)),mismatches=[c for c in checks if not c['passed']]))
assert all(c['passed'] for c in checks)
launches=[json.loads(l) for l in (w/'launches.jsonl').read_text().splitlines()]
measure=[summarize(w/l['name']) for l in launches if (w/l['name']/'process.json').exists()]
replays=[compare(w/l['name'].replace('-replay','-live'),w/l['name']) for l in launches if l['name'].endswith('-replay') and (w/l['name']/'process.json').exists()]
write_new(w/'measurements.json',dict(measurements=measure,replays=replays))
assert all(not r['mismatches'] for r in replays if r['live']!='fixed-full-regent-live')
assert read(w/'regent-confirmed-prefix-review.json')['completeStateRngExact']
reg=read(w/'registration.json');processes=[read(w/l['name']/'process.json') for l in launches if (w/l['name']/'process.json').exists()]
assert len(processes)==len(launches),'Missing registered process result'
for l in launches:
 completion=read(w/(l['name']+'-completion.json'));process=read(w/l['name']/'process.json')
 assert completion['forcedStop'] is None and completion['wrapperExit']==process['rawExit']
for char in ['regent','necrobinder']:
 for suffix in ['', '-replay']:
  assert all(read(w/f'final-{char}{suffix}-review.json')['checks'].values())
controls={}
for name in ['topdeck-final04-controls','amalgamator-repaired-controls']:
 data=read(w/name/'summary.json')['controls'];assert all(c['passed'] for c in data);controls[name]=len(data)
assert all(c['passed'] for c in read(w/'mechanism-assertions.json')['checks'])
assert read(w/'core-checks.json')['exit']==0
window=(max(datetime.fromisoformat(p['endedUtc']) for p in processes)-datetime.fromtimestamp(launches[0]['startedUnix'],timezone.utc)).total_seconds()
assert len(launches)<=reg['maxNativeProcesses'] and window<reg['nativeWallSeconds'] and size(w)<reg['maxEvidenceBytes']
assert all(p['exitConsistent'] and not p['watchdogExpired'] for p in processes)
files=[]
for l in launches:
 for p in sorted((w/l['name']).rglob('*')):
  if p.is_file():files.append(dict(path=str(p.relative_to(w)),bytes=p.stat().st_size,sha256=sha(p)))
write_new(w/'native-evidence-seal.json',dict(schema='pilot-native-file-seal/v1',buildId=m['buildId'],files=files))
write_new(w/'closeout.json',dict(schema='performance-choice-duration-closeout/v1',buildId=m['buildId'],solverSha256=sha(b/'bin/Release/net9.0/CombatSolver.dll'),outcome='Requested PhotonCut/Doom fixes and generic terminal hook repair passed on original seed. Regent/Necrobinder native terminal samples and same-build replays; whole-run visible policy remains unimplemented. Historical failed/unsupported attempts retained.',samples=[dict(character=x['character'],mode=x['mode'],buildId=x['buildId'],directory=x['directory'],result=x['result'],steps=x['steps'],exactSteps=x['exactSteps'],wallSeconds=x['wallSeconds'],measuredScopes=x['measuredScopes']) for x in measure],replays=replays,productionControls=controls,mechanismAssertions=23,coreChecks=98,incidents=read(w/'incidents.json')['incidents'],budget=dict(attempts=len(launches),processes=len(processes),windowSeconds=window,nativeSeconds=sum(p['elapsedSeconds'] for p in processes),bytes=size(w),registration=reg),preservation=dict(checks=len(checks),mismatches=0,rawFiles=len(files)),wholeRunVisibleCertified=False,remaining=['Other-class full terminal regression on the new hook derivative','Whole-run visible decision deployment and noninterference, see PERFORMANCE_VISIBLE_RUN_NEXT.md','Multiple seeds/preset quality comparison; no win-rate or performance improvement claim'],allRegisteredProcessesExited=True,committed=False,pushed=False))
print(json.dumps(dict(buildId=m['buildId'],processes=len(processes),checks=len(checks),replays=replays,bytes=size(w))))
