"""Preserve M1/previous pilots and seal this explicitly partial compatibility result."""
import hashlib,json
from collections import Counter
from datetime import datetime,timezone
from pathlib import Path
from pilot import LAB,read,write_new,size
w=LAB/'.build/runs/performance-compat-work01';b=LAB/'.build/native-build/performance-compat-02';m=read(b/'build-manifest.json')
def sha(p):
 with p.open('rb')as f:return hashlib.file_digest(f,'sha256').hexdigest().upper()
checks=[]
def check(p,h,g):checks.append(dict(path=str(p.relative_to(LAB))if p.is_relative_to(LAB)else str(p),group=g,passed=p.exists()and sha(p)==h))
for section in ['source','inputs','compiled','artifacts']:
 for r in m['manifest'][section]:check(b/r['path']if section in ['compiled','artifacts']else Path(r['path']),r['sha256'],'build-'+section)
for file,g in [(w/'prior-study-seal.json','prior-study'),(LAB/'.build/runs/performance-pilot-work01/protected-baseline.json','M1-snapshot'),(LAB/'.build/runs/performance-pilot-work02/prior-pilot-and-dependency-seal.json','earlier-pilot-and-solver'),(LAB/'.build/runs/juggernaut-phase-work01/old-fixed-inputs.json','15-inputs-52-compat')]:
 for p,h in read(file).items():check(LAB/p,h,g)
m1=LAB/'.build/runs/m1-acceptance-work07'
for r in read(m1/'native-evidence-seal.json')['files']:check(m1/r['path'],r['sha256'],'M1-raw')
write_new(w/'preservation-audit.json',dict(checks=len(checks),groups=dict(Counter(c['group']for c in checks)),mismatches=[c for c in checks if not c['passed']]))
assert all(c['passed']for c in checks),'Preservation mismatch retained'
launches=[json.loads(x)for x in(w/'launches.jsonl').read_text().splitlines()];reg=read(w/'registration.json');processes=[read(w/x['name']/'process.json')for x in launches if(w/x['name']/'process.json').exists()]
window=(max(datetime.fromisoformat(p['endedUtc'])for p in processes)-datetime.fromtimestamp(launches[0]['startedUnix'],timezone.utc)).total_seconds()
assert len(launches)<=reg['maxNativeProcesses']and window<reg['nativeWallSeconds']and size(w)<reg['maxEvidenceBytes']
assert all(p['exitConsistent']and not p['watchdogExpired']for p in processes)
measure=read(w/'measurements.json');assert len(measure['replays'])==3 and all(not r['mismatches']and not r.get('visibleMismatches')for r in measure['replays'])
for name in ['final-defect-review','review-defect-replay','review-silent-live','review-silent-replay']:assert all(read(w/(name+'.json'))['checks'].values())
for name in ['visible-review01','review-visible-replay','core-checks']:assert read(w/(name+'.json'))['exit']==0
for name in ['regent-failure-assessment','necrobinder-failure-assessment']:
 assert all(read(w/(name+'.json'))['checks'].values())and not read(w/(name+'.json'))['sampleAcceptedAsComplete']
controls={}
for name in ['duplicate-controls','retain-controls','visible-controls']:
 a=read(w/name/'summary.json')['controls'];assert all(x['passed']for x in a);controls[name]=len(a)
raw=[]
for l in launches:
 for p in sorted((w/l['name']).rglob('*')):
  if p.is_file():raw.append(dict(path=str(p.relative_to(w)),bytes=p.stat().st_size,sha256=sha(p)))
write_new(w/'native-evidence-seal.json',dict(schema='pilot-native-file-seal/v1',buildId=m['buildId'],files=raw))
full=[]
for x in measure['measurements']:
 if x.get('buildId')==m['buildId']and 'final-full-'in x['directory']and x['mode']=='live':full.append(dict(character=x['character'],stop=x['result']['Stop'],terminal=x['result']['Terminal'],confirmed=x['result']['Confirmed'],dispatched=x['result']['Dispatched'],exactSteps=x['exactSteps'],steps=x['steps'],seconds=x['wallSeconds'],searchSeconds=x['measuredScopes']['solver-search-wall']/1000,parallelActual=x['parallelActual']))
write_new(w/'closeout.json',dict(schema='performance-compat-closeout/v1',utc=datetime.now(timezone.utc).isoformat(),outcome='PARTIAL: three requested mechanisms exercised; Defect and Silent terminal/replay passed; Regent later InvalidAction and Necrobinder later DataError retained; visible first-battle deployment passed, whole-run fair policy not certified',buildId=m['buildId'],solverSha256=sha(b/'bin/Release/net9.0/CombatSolver.dll'),fullSamples=full,replays=measure['replays'],productionControls=controls,coreChecks=98,visible=dict(nativeActions=16,hostActions=19,solverQueries=0,outcome='BattleVictory',stateRngObservations=143,policyFilesReplayed=33,wholeRunFairPolicyCertified=False,trustedSubsetDeployed=True),budget=dict(registeredLaunchAttempts=len(launches),nativeProcesses=len(processes),maxNativeProcesses=reg['maxNativeProcesses'],inputAssemblyFailureBeforeLaunch=1,windowSeconds=window,limitSeconds=reg['nativeWallSeconds'],nativeSeconds=sum(p['elapsedSeconds']for p in processes),bytes=size(w),limitBytes=reg['maxEvidenceBytes']),preservation=dict(checks=len(checks),mismatches=0,rawFiles=len(raw)),remaining=['Regent FromHand/MoveToDrawTop after native draw, including choice-time identity','Necrobinder Doom victory boundary decrements Vulnerable too far in shadow','Complete class-visible observation and full-run trusted visible-policy deployment','Later/multiple-seed preset comparisons; no measured quality improvement claim'],incidents=read(w/'incident-notes.json')['incidents']+[read(w/'reviewer-interpretation-incident.json')],allRegisteredProcessesExited=True,committed=False,pushed=False))
print(json.dumps(dict(checks=len(checks),mismatches=0,rawFiles=len(raw),nativeProcesses=len(processes),launches=len(launches),windowSeconds=window,bytes=size(w))))
