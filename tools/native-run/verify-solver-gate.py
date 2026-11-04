"""Independent raw-evidence assertions for the bounded integration gate (no gameplay)."""
from pathlib import Path
import hashlib, json, sys

lab = Path(__file__).resolve().parents[2]
work = lab / '.build/runs/solver-gate-work01'
def rd(p): return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
checks = []
def check(name, ok): checks.append(dict(name=name, passed=bool(ok)))
live = lab / '.build/runs/solver-gate-exec-02'
replay = lab / '.build/runs/solver-gate-exec-03'
native = lab / '.build/runs/solver-gate-exec-04'
for root in [live, replay]:
    report, process = rd(root/'result.json'), rd(root/'process.json')
    check(root.name+'/actual-prefix', report['rawExit']==process['rawExit']==0 and process['exitConsistent'] and report['result']['Confirmed']==report['result']['Dispatched']==4)
    check(root.name+'/slice-not-win', report['result']['Terminal']=='Ongoing' and report['result']['Stop']=='SliceBoundary' and report['result']['Comparison']=='NotCompared')
    guard = report['guards']; iso = guard['isolation']
    check(root.name+'/offline', iso['backend']=='MemorySaveStore' and not iso['steamInitialized'] and not iso['shouldSave'] and not iso['uiInstance'] and guard['TelemetryCalls']==0 and not guard['faults'])
    check(root.name+'/actual-inputs', 'CombatSolver' in report['loadedAssemblies'] and not any(n in report['loadedAssemblies'] for n in ['MyMod','BaseLib']))
    step=rd(root/'solver-step.json'); before=step['nativeBefore']; after=step['nativeAfter']
    check(root.name+'/complete-continuation', bool(step['expected']) and step['expected']==step['actual'] and step['exact'])
    # Fixed v0.111 starter Defend: literal contract, independent of solver valuation.
    check(root.name+'/independent-defend', step['action']['CardId']=='DEFEND_IRONCLAD' and before['combat']['energy']-after['combat']['energy']==1 and after['block']-before['block']==5 and before['hp']==after['hp'])
    check(root.name+'/no-rng-cost', all(before[k]==after[k] for k in ['runRng','playerRng','eventRng']))
    check(root.name+'/permanent-state', all(before[k]==after[k] for k in ['deck','relics','potions','gold','maxHp']))
    check(root.name+'/exact-instance-moved', any(c['id']==step['card'] and c['model']=='CARD.DEFEND_IRONCLAD' for c in before['combat']['hand']) and not any(c['id']==step['card'] for c in after['combat']['hand']) and any(c['id']==step['card'] for c in after['combat']['discard']))
    for q in [1,2]:
        query=rd(root/f'solver-query-{q}.json')
        check(root.name+f'/query{q}-bound', query['boundary']=='None' and 0<query['ExpandedNodes']<=1000 and query['Profile']['BeamWidth']==8 and query['budget']['dop']==1)
        check(root.name+f'/query{q}-state', query['sourceUnchanged'] and query['nativeState']==(before if q==1 else after))
    controls=rd(root/'solver-controls.json')
    check(root.name+'/negative-mapping', controls==dict(negativeMappings=13,allRejected=True,zeroAdditionalDispatch=True))
    check(root.name+'/ticket-parent', step['parentUnchanged'] and step['consumedRejected'])
    records=[json.loads(l)['payload'] for l in (root/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
    end=[e for e in records if e['kind']=='solver-gate-complete']
    check(root.name+'/requery-closure', len(end)==1 and end[0]['detail']['facts']['queries']==2 and end[0]['detail']['facts']['staleRejected'])
    check(root.name+'/reader-passed', rd(root/'trace-review.json')['status']=='passed')
for name in ['route.json','choices.json','solver-step.json','solver-query-1.json','solver-query-2.json','final-state.json']:
    check('full-replay/'+name, rd(live/name)==rd(replay/name))
left=[json.loads(l)['payload'] for l in (live/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
right=[json.loads(l)['payload'] for l in (replay/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
check('full-replay/dispatch-confirmed', [e for e in left if e['kind'] in ['dispatch','confirmed']]==[e for e in right if e['kind'] in ['dispatch','confirmed']])
report=rd(native/'result.json')
check('native-only/regression24', report['result']['Confirmed']==report['result']['Dispatched']==24 and report['rawExit']==0)
check('native-only/no-mod-load', not any(n in ['CombatSolver','MyMod','BaseLib'] or n.startswith('STS2-RitsuLib') for n in report['loadedAssemblies']))
check('native-only/evidence22', rd(native/'evidence-review.json')['failed']==0 and rd(native/'evidence-review.json')['count']==22)
fixed=rd(lab/'.build/runs/juggernaut-phase-work01/old-fixed-inputs.json')
check('fixed15+52', len(fixed)==67 and all(sha(lab/p)==h for p,h in fixed.items()))
for name in ['solver-gate-final03','solver-gate-native-regression01']:
    build=lab/'.build/native-build'/name;m=rd(build/'build-manifest.json')['manifest']
    for group in ['source','compiled','inputs','artifacts']:
        check(name+'/'+group,all(sha(Path(row['path']) if group in ['source','inputs'] else build/row['path'])==row['sha256'] for row in m[group]))
profile=rd(work/'profile-api-controls.json')
check('profile-method-controls8', profile['passed'] and len(profile['checks'])==8)
reader=rd(work/'production-reader-controls.json')
check('production-reader-controls', all(x['passed'] for x in reader))
ledger=rd(work/'ledger.json')
check('native-cap4',len(ledger['entries'])==4 and all(e['wrapperExit']==0 and e['bytesAfter']<ledger['byteCap'] and e['ended']<ledger['deadline'] for e in ledger['entries']))
out=dict(checks=checks,count=len(checks),failures=[x for x in checks if not x['passed']],verifierSha256=sha(Path(__file__)))
(work/'verification.json').write_text(json.dumps(out,indent=2),encoding='utf-8')
print(json.dumps(dict(checks=len(checks),failures=out['failures'])));sys.exit(bool(out['failures']))
