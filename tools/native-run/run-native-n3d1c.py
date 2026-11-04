"""Predeclared native transactions cohort; raw outcomes and production semantic controls."""
import argparse,json,subprocess,shutil
from pathlib import Path
p=argparse.ArgumentParser();p.add_argument('--manifest',required=True);p.add_argument('--output',required=True);a=p.parse_args();root=Path.cwd();out=Path(a.output).resolve();assert out.is_relative_to(root/'.build/runs') and not out.exists();out.mkdir();manifest=Path(a.manifest).resolve();build=json.loads(manifest.read_text(encoding='utf-8-sig'));dll=manifest.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
checks=[];processes=[];controls=[]
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def write(p,v):p.write_text(json.dumps(v,indent=2),encoding='utf-8')
def check(n,v):
 checks.append(dict(name=n,passed=bool(v)))
 if not v:print('FAIL',n,flush=True)
def command(args,log,timeout=780):
 q=subprocess.run(args,capture_output=True,timeout=timeout);log.write_bytes(q.stdout+q.stderr);return q.returncode
def run(name,scenario,seed='LAB_V0111_NATIVE_DEV_01',replay=None,mode='detailed',expected=0):
 d=out/name;args=['pwsh','-NoProfile','-File','tools/native-run/run-native-slice.ps1','-BuildManifest',str(manifest),'-OutputDirectory',str(d),'-Scenario',scenario,'-Seed',seed,'-Observation',mode]
 if replay:args+=['-ReplayPath',str(replay/'route.json')]
 raw=command(args,out/(name+'.log'));r=read(d/'result.json');pr=read(d/'process.json');g=r['guards']['isolation'];result=r['result']
 check(name+' exit/build/isolation',raw==pr['rawExit']==r['rawExit']==expected and pr['exitConsistent'] and not pr['watchdogExpired'] and pr['buildId']==build['buildId'] and g['backend']=='MemorySaveStore' and not any(g[x] for x in ['shouldSave','steamInitialized','uiInstance']))
 check(name+' native only',result['Comparison']=='NotCompared' and not set(r['loadedAssemblies'])&{'MyMod','BaseLib','CombatSolver'})
 if not expected:
  code=command(['dotnet',str(dll),'--review',str(d)],out/(name+'-review.log'),30);check(name+' production reader',code==0)
  t=[json.loads(l)['payload'] for l in (d/'trace.jsonl').read_text().splitlines()];returns=[x['detail']['facts']['result'] for x in t if x['kind']=='n3d-return' and x['detail']['facts']['kind']=='MerchantBuy']
  check(name+' confirmed actual transactions',len(returns)==(2 if scenario=='merchant-native-courier' else 1) and result['Confirmed']==result['Dispatched'])
  if scenario=='merchant-native-courier':check(name+' independent receipt/identity/discount',len(returns)==2 and all(x['restocked'] and x['success'] and x['offered']!=x['replacement'] for x in returns) and returns[1]['offered']==returns[0]['replacement'])
  if scenario=='merchant-native-hook':
   f=read(d/'final-state.json');bank=next(x for x in f['transactionRelics'] if x['model']=='RELIC.MAW_BANK');check(name+' actual MawBank disabled',bank['bought'] and bank['IsUsedUp'] and bank['status']=='Disabled')
  if scenario=='merchant-natural-relic':check(name+' natural no fixture and next play',r['provenance']['ScenarioKind']=='natural-run' and not any(x['kind']=='fixture-setup-declared' for x in t) and read(d/'final-state.json')['combat']['phase']=='Play' and returns[0]['success'])
  if replay:
   for fn in ['route.json','choices.json','final-state.json']:check(name+' full replay '+fn,read(d/fn)==read(replay/fn))
 else:
  check(name+' honest classified prefix',result['Stop']==('Unsupported' if expected==2 else 'EngineError' if scenario.endswith('fault') else 'InvalidAction') and not result['DataComplete'] and result['Confirmed']<=result['Dispatched'])
 processes.append(dict(id=name,scenario=scenario,expected=expected,rawExit=raw,childExit=pr['rawExit'],confirmed=result['Confirmed'],dispatched=result['Dispatched']));write(out/'progress.json',dict(processes=processes,failures=[x for x in checks if not x['passed']]));print(name,raw,flush=True);return d
def control_specs():
 for source in ['relic-1','hook-1','courier-1']:
  kinds=['mc-effect-start','mc-effect-return','mc-stock-start','mc-stock-return','mc-hook-start','mc-hook-return','mc-callback','merchant-exit-start','merchant-exit-return','n3d-return','n3d-boundary']
  if source=='relic-1':kinds+=['mc-pickup-start','mc-pickup-return']
  for kind in kinds:
   for change in ['missing','duplicate','root','context','state-missing','state-null','extension','before']:
    yield change+'|'+kind,1,source
  for change in ['coedit-stock-hook','coedit-effect-pickup','coedit-callback-return','receipt-old','replacement-old','price-cost','hook-rollback','exit-coedit-gold','exit-coedit-extra','setup-coedit-gold','setup-coedit-extra','exit-missing-all','exit-facts-wrong-room','exit-history-coedit','initial-inventory-price-coedit','equivalent-extension','equivalent-order']:
   yield change,0 if change in ['equivalent-order','equivalent-extension'] else 1,source
 # Explicit selection RNG anchor controls affect the production check before CalcCost.
 for c in ['rng-before-start-word','rng-before-start-counter']:yield c,1,'courier-1'
 # Full first/second-generation chains and source-schema controls.
 for n in [0,1]:
  for change in ['gold','extension','content','other-entry']:
   yield f'binding:price:{n}:{change}',1,'courier-1'
  for mode,changes in [('source',['missing','null','generation','type','model','extra']),('identity',['type','model'])]:
   for change in changes:yield f'binding:{mode}:{n}:{change}',1,'courier-1'
 yield 'binding:price:1:generation',1,'courier-1'
 yield 'binding:reuse:1:owned',1,'courier-1'
 for change in ['model','status','bought','extra']:yield f'binding:relic:0:{change}',1,'relic-1'
 for change in ['effect-missing','effect-null','effect-type','effect-model','effect-extra']:yield f'binding:source:0:{change}',1,'relic-1'
write(out/'cohort.json',dict(profile='native-merchant-transactions/v2',seeds=['LAB_V0111_NATIVE_DEV_01','LAB_V0111_NATIVE_DEV_02','LAB_V0111_NATIVE_DEV_03'],scope='fixture relic/Hook/refill; natural relic slice; not full run'))
for c in ['relic','hook','courier','poor']:
 d=run(c+'-1','merchant-native-'+c);run(c+'-2','merchant-native-'+c,replay=d)
for c in ['relic','courier']:
 for mode in ['minimal','off']:run(c+'-'+mode,'merchant-native-'+c,replay=out/(c+'-1'),mode=mode)
for c in ['stale','fault']:run(c,'merchant-native-'+c,expected=1)
for i in [1,2,3]:
 d=run('natural-'+str(i)+'-1','merchant-natural-relic','LAB_V0111_NATIVE_DEV_0'+str(i),expected=0 if i==1 else 2)
 if i==1:run('natural-1-2','merchant-natural-relic',replay=d)
# Production mutations operate only on sealed copies, not a separate validator substitute.
for i,(control,expected,source) in enumerate(control_specs()):
 d=out/('control-'+str(i));d.mkdir()
 for f in ['trace.jsonl','trace-seal.json','result.json','process.json']:shutil.copy2(out/source/f,d/f)
 mutation=command(['dotnet',str(dll),'--mutate',str(d),'transaction-review:'+control],d/'mutation.log',30)
 raw=command(['dotnet',str(dll),'--review',str(d)],d/'review.log',30);check(control+' semantic rejection/equivalence',mutation==0 and raw==expected and 'Trace chain/order/schema' not in (d/'review.log').read_text())
 controls.append(dict(control=control,expected=expected,rawExit=raw,mutationExit=mutation,directory=d.name))
write(out/'controls.json',controls);write(out/'summary.json',dict(buildId=build['buildId'],processCount=len(processes),processes=processes,checkCount=len(checks),checks=checks,controlCount=len(controls),failures=sum(not x['passed'] for x in checks)));raise SystemExit(1 if any(not x['passed'] for x in checks) else 0)
