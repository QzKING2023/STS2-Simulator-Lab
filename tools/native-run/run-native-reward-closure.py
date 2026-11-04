"""Registered native reward scenarios and fresh-copy production-reader controls."""
from pathlib import Path
import argparse, json, subprocess, hashlib, shutil, copy
import importlib.util
_control_spec=importlib.util.spec_from_file_location("reward_closure_controls",Path(__file__).with_name("reward_closure_controls.py"))
_controls=importlib.util.module_from_spec(_control_spec);_control_spec.loader.exec_module(_controls)
specifications=_controls.specifications

def main():
    parser=argparse.ArgumentParser()
    parser.add_argument('--manifest',required=True);parser.add_argument('--output',required=True)
    args=parser.parse_args();root=Path(__file__).resolve().parents[2];out=Path(args.output).resolve();manifest=Path(args.manifest).resolve()
    if not out.is_relative_to(root/'.build/runs') or out.exists():raise ValueError('Fresh Lab runs directory required')
    out.mkdir();m=json.loads(manifest.read_text(encoding='utf-8-sig'));dll=manifest.parent/'bin/Release/net9.0/SimulatorLab.NativeRun.dll'
    checks=[];processes=[]
    rd=lambda p:json.loads(p.read_text(encoding='utf-8-sig'))
    def write(name,value):(out/name).write_text(json.dumps(value,indent=2),encoding='utf-8')
    def check(name,value,detail=None):checks.append(dict(name=name,passed=bool(value),detail=detail))
    def execute(id,case,expected=0,replay=None,mode='detailed'):
        d=out/id;seed='LAB_V0111_NATIVE_DEV_02' if case=='closure-neow' else 'LAB_V0111_NATIVE_DEV_01'
        cmd=['pwsh','-NoProfile','-File',str(root/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(manifest),'-OutputDirectory',str(d),'-Seed',seed,'-Scenario',case,'-Observation',mode]
        if replay:cmd+=['-ReplayPath',str(replay)]
        with (out/(id+'.log')).open('wb') as log:p=subprocess.run(cmd,stdout=log,stderr=subprocess.STDOUT,timeout=360)
        r=rd(d/'result.json');pr=rd(d/'process.json');result=r['result']
        check(id+' raw exit/build',p.returncode==expected==r['rawExit']==pr['rawExit'] and pr['exitConsistent'] and not pr['watchdogExpired'] and pr['buildId']==m['buildId'],result['Error'])
        check(id+' origin/mode',pr['scenarioKind']==('fault-control' if expected else 'natural-run' if case=='closure-neow' else 'api-fixture') and pr['executionKind']==('observation-control' if mode!='detailed' else 'frozen-replay' if replay else 'live'))
        iso=r['guards']['isolation'];check(id+' isolation',iso['backend']=='MemorySaveStore' and not any(iso[k] for k in ['steamInitialized','shouldSave','uiInstance']) and not set(r['loadedAssemblies'])&{'MyMod','CombatSolver','BaseLib'})
        if expected:
            check(id+' actual failure prefix',result['Stop']==('EngineError' if case=='closure-add-fault' else 'InvalidAction') and result['Confirmed']==3 and result['Dispatched']==4 and not result['DataComplete'] and not r['trace']['complete'])
            rows=[json.loads(l)['payload'] for l in (d/'trace.jsonl').read_text(encoding='utf-8').splitlines()]
            check(id+' actual completed business retained',sum(x['kind']==('rc-add-return' if case=='closure-add-fault' else 'rc-reroll-return') for x in rows)==1)
        else:
            p=subprocess.run(['dotnet',str(dll),'--review',str(d)],capture_output=True,timeout=60);(d/'production-review.log').write_bytes(p.stdout+p.stderr)
            check(id+' production semantic reader',p.returncode==0,(p.stdout+p.stderr).decode('utf-8',errors='replace'))
            check(id+' complete native boundary',result['DataComplete'] and result['Confirmed']==result['Dispatched'] and result['Comparison']=='NotCompared')
        processes.append(dict(id=id,scenario=case,expected=expected,rawExit=pr['rawExit'],confirmed=result['Confirmed'],dispatched=result['Dispatched'],stop=result['Stop']))
        write('progress.json',dict(processes=processes,failures=[x for x in checks if not x['passed']]))
        print(id,pr['rawExit'],result['Stop'],flush=True);return d
    positive=['closure-direct','closure-reroll-take','closure-reroll-skip','closure-lens-options','closure-lens-add','closure-sozu-empty','closure-sozu-full','closure-neow','closure-treasure','closure-spoils']
    def same(a,b):
        for name in ['route.json','choices.json','reward-choices.json','final-state.json']:check(b.name+' deterministic '+name,rd(a/name)==rd(b/name))
    for case in positive:
        a=execute(case+'-1',case);b=execute(case+'-2',case,replay=a/'route.json');same(a,b)
    for case in ['closure-reroll-take','closure-lens-add','closure-sozu-full','closure-spoils']:
        a=out/(case+'-1')
        for mode in ['minimal','off']:same(a,execute(case+'-'+mode,case,replay=a/'route.json',mode=mode))
    for case in ['closure-reroll-invalid','closure-reroll-stale','closure-reroll-missing','closure-reroll-extra','closure-reroll-duplicate','closure-reroll-disabled','closure-add-fault']:execute(case,case,1)
    controls=[];originals={};files=['trace.jsonl','trace-seal.json','result.json','process.json']
    for i,(name,source,mutate,expected) in enumerate(specifications(out)):
        d=out/('control-'+str(i));d.mkdir();(d/'.copy-for-control').write_text('registered corruption/equivalence copy',encoding='utf-8')
        for fn in files:
            src=out/source/fn;originals[str(src)]=hashlib.sha256(src.read_bytes()).hexdigest().upper();shutil.copy2(src,d/fn)
        rows=[json.loads(l) for l in (d/'trace.jsonl').read_text(encoding='utf-8').splitlines()];mutate(rows)
        (d/'trace.jsonl').write_text('\n'.join(json.dumps(x,ensure_ascii=False) for x in rows)+'\n',encoding='utf-8')
        p=subprocess.run(['pwsh','-NoProfile','-File',str(root/'tools/native-run/reseal-native-control.ps1'),'-Directory',str(d)],capture_output=True,timeout=60);(d/'reseal.log').write_bytes(p.stdout+p.stderr)
        q=subprocess.run(['dotnet',str(dll),'--review',str(d)],capture_output=True,timeout=60);message=(q.stdout+q.stderr).decode('utf-8',errors='replace');(d/'production-review.log').write_text(message,encoding='utf-8')
        ok=p.returncode==0 and q.returncode==expected and 'Trace chain/order/schema error' not in message
        check(name+' actual production control',ok,message);controls.append(dict(name=name,source=source,directory=d.name,expected=expected,resealExit=p.returncode,rawExit=q.returncode,passed=ok,message=message))
        if i%20==0:write('controls-progress.json',dict(completed=i+1));print('controls',i+1,flush=True)
    write('controls.json',controls);write('original-packet-lock.json',originals)
    check('original evidence retained',all(hashlib.sha256(Path(p).read_bytes()).hexdigest().upper()==h for p,h in originals.items()))
    write('summary.json',dict(buildId=m['buildId'],processCount=len(processes),processes=processes,checkCount=len(checks),checks=checks,controls=len(controls),failures=[x for x in checks if not x['passed']],normalStartFullRun=False))
    return 1 if any(not x['passed'] for x in checks) else 0

if __name__=='__main__':raise SystemExit(main())
