"""Bounded native merchant economy cohort; all execution stays in existing isolated host."""
import argparse
import hashlib
import json
import subprocess
from pathlib import Path

parser=argparse.ArgumentParser()
parser.add_argument('--manifest',required=True)
parser.add_argument('--output',required=True)
args=parser.parse_args()
root=Path(__file__).resolve().parents[2]
out=Path(args.output).resolve()
assert out.is_relative_to(root/'.build/runs') and not out.exists(), 'Fresh Lab output required'
out.mkdir()
manifest=Path(args.manifest).resolve()
build=json.loads(manifest.read_text(encoding='utf-8-sig'))
dll=manifest.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
checks=[];processes=[];controls=[]
def read(p): return json.loads(Path(p).read_text(encoding='utf-8-sig'))
def write(p,v): Path(p).write_text(json.dumps(v,indent=2),encoding='utf-8')
def check(n,ok):
    checks.append(dict(name=n,passed=bool(ok)))
    if not ok: print('FAILED',n,flush=True)
def command(argv,log,timeout):
    p=subprocess.run(argv,cwd=root,capture_output=True,timeout=timeout)
    Path(log).write_bytes(p.stdout+p.stderr)
    return p.returncode
def rows(d): return [json.loads(l)['payload'] for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
def run(name,scenario,seed='LAB_V0111_NATIVE_DEV_01',replay=None,mode='detailed',expected=0):
    d=out/name
    argv=['pwsh','-NoProfile','-File','tools/native-run/run-native-slice.ps1','-BuildManifest',str(manifest),'-OutputDirectory',str(d),'-Scenario',scenario,'-Seed',seed,'-Observation',mode]
    if replay: argv+=['-ReplayPath',str(replay/'route.json')]
    raw=command(argv,out/(name+'.log'),780)
    a=read(d/'result.json');p=read(d/'process.json');g=a['guards']['isolation']
    check(name+' actual raw exit/build/isolation',raw==expected==a['rawExit']==p['rawExit'] and p['buildId']==build['buildId'] and p['exitConsistent'] and not p['watchdogExpired'] and g['backend']=='MemorySaveStore' and not g['shouldSave'] and not g['steamInitialized'] and not g['uiInstance'])
    check(name+' native-only',a['result']['Comparison']=='NotCompared' and not set(a['loadedAssemblies'])&{'BaseLib','MyMod','CombatSolver'})
    check(name+' origin',p['scenarioKind']==('fault-control' if expected else 'natural-run' if scenario=='merchant-natural' else 'api-fixture'))
    if not expected:
        code=command(['dotnet',str(dll),'--review',str(d)],out/(name+'-review.log'),30)
        check(name+' production reader',code==0)
        t=rows(d);prices=[p['detail']['facts'] for p in t if p['kind']=='merchant-price']
        golden={'LAB_V0111_NATIVE_DEV_01':[48,51,72],'LAB_V0111_NATIVE_DEV_02':[49,48,48],'LAB_V0111_NATIVE_DEV_03':[48,51,50]}
        check(name+' independent fixed cohort prices',[(p['cost'],p['before']['counter'],p['after']['counter']) for p in prices]==[(cost,25+i,26+i) for i,cost in enumerate(golden[seed])])
        check(name+' actual original exit once',sum(p['kind']=='merchant-exit-start' for p in t)==sum(p['kind']=='merchant-exit-return' for p in t)==1)
        if scenario=='merchant-natural':
            kinds=[p['kind'] for p in t];actions=[p['detail']['facts']['action']['kind'] for p in t if p['kind']=='confirmed']
            check(name+' natural same-run no fixture API',kinds.count('new-run')==1 and kinds.count('battle-victory-before-cleanup')>=1 and 'fixture-setup-declared' not in kinds and 'MerchantSetup' not in actions and 'MerchantAttach' in actions and 'MerchantTravel' in actions and actions[-1]=='PlayCard' and a['result']['Dispatched']==a['result']['Confirmed'])
            tail=read(d/'final-state.json');check(name+' real next combat Play/closed',tail['room']=='CombatRoom' and tail['combat']['phase']=='Play' and tail['merchant']['closed'] and tail['rooms']['stack']==1)
            acquired=[p['detail']['facts']['result']['acquired']['$ref'] for p in t if p['kind']=='n3d-return' and p['detail']['facts']['kind']=='MerchantBuy' and p['detail']['facts']['result']['success']]
            check(name+' acquired permanent and combat copy',len(acquired)==1 and any(c['id']==acquired[0] for c in tail['deck']) and any(c['deck']==acquired[0] for field in ['hand','draw','discard','exhaust'] for c in tail['combat'][field]))
        if replay:
            for f in ['route.json','choices.json','final-state.json']:
                check(name+' replay/fullRNG '+f,read(d/f)==read(replay/f))
    else:
        fault=scenario.endswith('task-fault')
        check(name+' failure prefix',a['result']['Stop']==('EngineError' if fault else 'InvalidAction') and a['result']['Confirmed']==3 and a['result']['Dispatched']==(4 if fault else 3) and not a['result']['DataComplete'])
        if fault:
            t=rows(d);before=[p['detail']['state'] for p in t if p['kind']=='dispatch'][-1];after=read(d/'final-state.json')
            check(name+' actual acquisition retained',len(after['deck'])==len(before['deck'])+1 and after['gold']==before['gold']-51 and a['result']['Terminal']=='Unknown')
    processes.append(dict(id=name,scenario=scenario,seed=seed,expected=expected,rawExit=raw,confirmed=a['result']['Confirmed'],dispatched=a['result']['Dispatched']))
    write(out/'progress.json',dict(completed=processes,failedChecks=[x for x in checks if not x['passed']]))
    print(name,'exit',raw,flush=True)
    return d

# Predeclared sample, not replaced when a strategy loses or encounters unsupported content.
write(out/'cohort.json',dict(seeds=['LAB_V0111_NATIVE_DEV_01','LAB_V0111_NATIVE_DEV_02','LAB_V0111_NATIVE_DEV_03'],policy='visible shortest shop distance / cheapest affordable card / optional removal cancel / next Monster',nodes=12,actions=5000,wallSeconds=600,natural=True))
for case in ['card','potion','remove','cancel']:
    s='merchant-economy-'+case;a=run(case+'-1',s);run(case+'-2',s,replay=a)
for i in range(1,4):
    seed='LAB_V0111_NATIVE_DEV_0'+str(i);a=run('natural-'+str(i)+'-1','merchant-natural',seed);run('natural-'+str(i)+'-2','merchant-natural',seed,replay=a)
for case in ['card','remove','natural-1']:
    source=out/('natural-1-1' if case=='natural-1' else case+'-1')
    for mode in ['minimal','off']:run(case+'-'+mode,'merchant-natural' if case=='natural-1' else 'merchant-economy-'+case,replay=source,mode=mode)
for case in ['stale','wrong-inventory','task-fault']:run(case,'merchant-economy-'+case,expected=1)

control_specs=[]
for kind in ['merchant-price-start','merchant-price','merchant-exit-start','merchant-exit-return','merchant-stats-start','merchant-stats-return','room-entered','room-leave-ready','room-exit-start','room-exit-return','room-complete']:
    for change in ['missing','duplicate','root','context','state-missing','state-null','state-array','extension','before']:control_specs.append((change+'|'+kind,1))
for change in ['price-cost','price-state-word','price-entry','price-coherent-state','exit-inventory','exit-before-start','purchase-history']+['exit-history-'+x for x in ['missing','clear','model','upgrade','picked','duplicate','gold','earlier','reorder']]:control_specs.append((change,1))
control_specs += [('equivalent-order',0),('equivalent-extension',0)]
for scope in ['first','all']:
    for field in ['gold','hp','deck','relics','extension','rng']:control_specs.append(('binding-price-'+scope+'-'+field,1))
for change in ['stats-missing-first','stats-missing-approach','stats-missing-all','stats-duplicate-pair','stats-after-exit','arrival-missing-first','arrival-missing-last','arrival-missing-all','arrival-and-completion-missing-last']+['travel-result-'+x for x in ['from','to','closed','null','type','missing-from','missing-to','extra']]:control_specs.append(('binding-'+change,1))
control_specs += [('binding-equivalent-price-order',0),('binding-equivalent-result-order',0)]
for change in ['stats-after-prices','stats-after-first-price','stats-return-after-price-start','prices-after-room-return','duplicate-stats-after-prices']:control_specs.append(('phase-'+change,1))
for scope in ['attach','projection']:
    for change in ['old-room','missing','null','array']:control_specs.append(('owner-'+scope+'-'+change,1))
control_specs += [('owner-equivalent-reference-renaming',0),('owner-equivalent-room-order',0)]
control_specs += [('owner-generation-old-room',1),('owner-inventory-old-room',1)]
for i,(control,expected) in enumerate(control_specs):
    d=out/('control-economy-'+str(i));d.mkdir()
    source=out/('natural-1-1' if control.startswith(('binding-','phase-','owner-')) or 'merchant-stats-' in control or '|room-' in control else 'card-1')
    for f in ['trace.jsonl','trace-seal.json','result.json','process.json']:(d/f).write_bytes((source/f).read_bytes())
    mutation=command(['dotnet',str(dll),'--mutate',str(d),'merchant-economy-review:'+control],d/'mutation.log',30)
    raw=command(['dotnet',str(dll),'--review',str(d)],d/'review.log',30)
    message=(d/'review.log').read_text(encoding='utf-8-sig')
    check(control+' production semantic control',mutation==0 and raw==expected and 'Trace chain/order/schema error' not in message)
    controls.append(dict(control=control,directory=d.name,expected=expected,rawExit=raw,mutationExit=mutation))
write(out/'controls.json',controls)
result=dict(schema='merchant-economy-suite/v1',buildId=build['buildId'],processCount=len(processes),processes=processes,checkCount=len(checks),checks=checks,controlCount=len(controls),failures=sum(not x['passed'] for x in checks))
write(out/'summary.json',result)
print(json.dumps({k:v for k,v in result.items() if k not in ['processes','checks']}),flush=True)
raise SystemExit(1 if result['failures'] else 0)
