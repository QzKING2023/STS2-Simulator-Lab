"""Raw evidence and independent starter-rule checks; does not execute gameplay."""
from pathlib import Path
import hashlib,json,sys
LAB=Path(__file__).resolve().parents[2];WORK=LAB/'.build/runs/solver-adapter-repair-work01'
def rd(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
checks=[]
def ck(n,v):checks.append(dict(name=n,passed=bool(v)))
for n in ['01','02','03','04']:
    root=LAB/('.build/runs/solver-adapter-exec-'+n);r=rd(root/'result.json');p=rd(root/'process.json')
    ck(n+'/raw-prefix',r['rawExit']==p['rawExit']==0 and p['exitConsistent'] and r['result']['Dispatched']==r['result']['Confirmed']==(4 if n in ['01','02'] else 7))
    ck(n+'/scope',r['result']['Terminal']=='Ongoing' and r['result']['Stop']=='SliceBoundary' and r['result']['Comparison']=='NotCompared')
    iso=r['guards']['isolation'];ck(n+'/isolation',not iso['steamInitialized'] and not iso['shouldSave'] and not iso['uiInstance'] and iso['backend']=='MemorySaveStore' and iso['networkService']=='NetSingleplayerGameService' and r['guards']['TelemetryCalls']==0 and not r['guards']['faults'])
    ck(n+'/dependencies','CombatSolver'in r['loadedAssemblies'] and not any(x in r['loadedAssemblies'] for x in ['MyMod','BaseLib']))
    rows=[json.loads(l)['payload'] for l in (root/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    ck(n+'/Oracle',rows[0]['detail']['information']==rd(root/'solver-profile.json')['information']=='Oracle')
    reviewed=WORK/('controls/normal-live' if n=='01' else 'controls/normal-replay' if n=='02' else 'extra-controls/loop-live' if n=='03' else 'extra-controls/loop-replay')
    ck(n+'/production-reader',rd(reviewed/'trace-review.json')['status']=='passed' and sha(root/'trace.jsonl')==sha(reviewed/'trace.jsonl'))
    if n in ['01','02']:
        t=rd(root/'solver-ticket-checks.json');ck(n+'/fresh-stale',all(t[k] for k in ['freshBefore','unconsumedAtRejection','staleRejected','consumedRejected','zeroAdditionalDispatch','stateChanged']) and t['oldSequence']==3 and t['currentSequence']==4)
        s=rd(root/'solver-step.json');b,a=s['nativeBefore'],s['nativeAfter'];ck(n+'/literal-Defend',b['combat']['energy']-a['combat']['energy']==1 and a['block']-b['block']==5 and b['hp']==a['hp'])
    else:
        for i in range(1,5):
            s=rd(root/f'solver-loop-step-{i}.json');b,a=s['nativeBefore'],s['nativeAfter'];act=s['action']
            ck(n+f'/step{i}-stamp-parent-ticket',bool(s['expected']) and s['expected']==s['actual'] and all(s[k] for k in ['exact','parentUnchanged','consumedRejected','staleRejected','unconsumedAtRejection','zeroAdditionalDispatch']) and s['currentSequence']==s['oldSequence']+1)
            ck(n+f'/step{i}-permanent',all(b[k]==a[k] for k in ['deck','gold','relics','potions','maxHp']))
            if i==1:ck(n+'/Defend-rule',act['CardId']=='DEFEND_IRONCLAD' and b['combat']['energy']-a['combat']['energy']==1 and a['block']-b['block']==5)
            if i in [2,3]:
                be=next(x for x in b['combat']['enemies'] if x['id']==s['target']);ae=next(x for x in a['combat']['enemies'] if x['id']==s['target'])
                ck(n+f'/Strike{i}-rule',act['CardId']=='STRIKE_IRONCLAD' and act['CardUpgradeLevel']==0 and be['hp']-ae['hp']==6 and b['combat']['energy']-a['combat']['energy']==1 and be['block']==0 and b['hp']==a['hp'])
            if i<4:ck(n+f'/step{i}-no-rng-spend',all(b[k]==a[k] for k in ['runRng','playerRng','eventRng']))
            else:ck(n+'/enemy-and-next-draw',act['Kind']=='EndTurn' and b['hp']==80 and a['hp']==73 and b['combat']['turn']==1 and a['combat']['turn']==2 and a['combat']['energy']==3 and len(a['combat']['hand'])==5 and a['combat']['phase']=='Play' and a['combat']['side']=='Player')
        for i in range(1,6):
            q=rd(root/f'solver-query-{i}.json');ck(n+f'/query{i}-bounds',q['boundary']=='None' and q['sourceUnchanged'] and 0<q['TotalExpandedNodes']<=1000 and q['Profile']['BeamWidth']==8 and q['budget']['dop']==1)
        ck(n+'/requery-new-turn',rd(root/'solver-query-5.json')['nativeState']==rd(root/'solver-loop-step-4.json')['nativeAfter'])
        c=rd(root/'solver-loop-controls.json');ck(n+'/mapping-controls',c['negativeMappings']==11 and all(c[k] for k in ['allRejected','sourceUnchanged','zeroAdditionalDispatch','endTurnFieldsAccepted']))
for a,b in [('01','02'),('03','04')]:
    left=LAB/('.build/runs/solver-adapter-exec-'+a);right=LAB/('.build/runs/solver-adapter-exec-'+b)
    files=['route.json','choices.json','solver-profile.json','final-state.json']+[p.name for p in left.glob('solver-query-*.json')]+[p.name for p in left.glob('solver-*step*.json')]
    if a=='01':files+=['solver-ticket-checks.json']
    for f in files:ck(a+'-'+b+'/replay/'+f,rd(left/f)==rd(right/f))
    def boundaries(root):return [e for e in map(json.loads,(root/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()) if e['payload']['kind'] in ['dispatch','confirmed']]
    ck(a+'-'+b+'/boundary-payloads',[x['payload'] for x in boundaries(left)]==[x['payload'] for x in boundaries(right)])
for name in ['solver-repair-final01','solver-loop-final02','solver-loop-native-default01']:
    build=LAB/'.build/native-build'/name;m=rd(build/'build-manifest.json')['manifest']
    for g in ['compiled','artifacts','inputs']:
        ck(name+'/'+g,all(sha((LAB if g=='inputs' else build)/r['path'])==r['sha256'] for r in m[g]))
    drift=[r['path'] for r in m['source'] if sha(Path(r['path']))!=r['sha256']]
    if name!='solver-repair-final01':ck(name+'/current-source',not drift)
    else:
        allowed={'Program.cs','NativeScenario.cs','NativeEvidence.cs','NativeSolverGate.cs','NativeSolverContract.cs'}
        ck(name+'/planned-phase2-source-drift',all(Path(p).name in allowed for p in drift))
        (WORK/'repair-source-drift.json').write_text(json.dumps(dict(scope='phase1 compiled snapshots preserved; these live source paths advanced for phase2',paths=drift),indent=2),encoding='utf-8')
fixed=rd(LAB/'.build/runs/juggernaut-phase-work01/old-fixed-inputs.json');ck('fixed15+52',len(fixed)==67 and all(sha(LAB/p)==h for p,h in fixed.items()))
for name in ['execution-lock.json','work-lock.json']:
    d=rd(LAB/'.build/runs/solver-gate-work01'/name);ck('historical/'+name,all(sha(LAB/p)==h for p,h in d.items()))
for name in ['production-controls.json','extra-production-controls.json']:
    rows=rd(WORK/name);ck(name,all(x.get('passed',x.get('matched',False)) for x in rows))
d=rd(WORK/'ledger.json');ck('budget4-deadline-bytes',len(d['entries'])==4 and all(e['wrapperExit']==0 and e['ended']<d['deadline'] and e['bytesAfter']<d['byteCap'] for e in d['entries']))
report=dict(count=len(checks),checks=checks,failed=[x for x in checks if not x['passed']],verifierSha256=sha(Path(__file__)))
(WORK/'verification.json').write_text(json.dumps(report,indent=2),encoding='utf-8');print(json.dumps(dict(count=len(checks),failed=report['failed'])));sys.exit(bool(report['failed']))
