"""Persistent execution, time and storage gates for declared solver batches."""
from pathlib import Path
import datetime as dt, json, subprocess, sys, time, re
LAB=Path(__file__).resolve().parents[2]
BATCH='solver-adapter'
if len(sys.argv)>2 and sys.argv[1]=='--batch':
    BATCH=sys.argv[2]
    if not re.fullmatch(r'solver-[a-z0-9-]+',BATCH):raise RuntimeError('Invalid Lab batch identifier')
    sys.argv=sys.argv[:1]+sys.argv[3:]
WORK=LAB/('.build/runs/solver-adapter-repair-work01' if BATCH=='solver-adapter' else '.build/runs/'+BATCH+'-work01')
LEDGER=WORK/'ledger.json'
def utc():return dt.datetime.now(dt.timezone.utc)
def save(d):LEDGER.write_text(json.dumps(d,indent=2),encoding='utf-8')
def size():
    roots=[WORK]+list((LAB/'.build/native-build').glob('solver-repair-*'))+list((LAB/'.build/native-build').glob('solver-loop-*'))+list((LAB/'.build/native-build').glob('solver-choice-potion-*'))+list((LAB/'.build/runs').glob(BATCH+'-exec-*'))+list((LAB/'.build/native-build').glob(BATCH+'-*'))
    return sum(p.stat().st_size for root in set(roots) for p in root.rglob('*') if p.is_file())
if sys.argv[1]=='register':
    if LEDGER.exists():raise RuntimeError('Never reset existing ledger')
    WORK.mkdir(parents=True,exist_ok=True);start=utc()
    save(dict(start=start.isoformat(),deadline=(start+dt.timedelta(minutes=20)).isoformat(),cap=3 if BATCH in ('solver-coordinator','solver-coordinator-battle','solver-darv','solver-enchantment') else 4,byteCap=2*1024**3,processSeconds=120,entries=[],scope='declared Coordinator prefix then bounded generic native actions, native Darv and source-reused Foreseer scalar predictions; same-producer replay; no scoring changes' if BATCH in ('solver-darv','solver-enchantment') else 'declared Coordinator prefix then bounded generic native actions and same-producer replay; no scoring changes' if BATCH.startswith('solver-coordinator') else 'fixed solver action bridge; plain loop regression and labelled TrueGrit/FirePotion API fixture; no Boss tuning' if BATCH!='solver-adapter' else 'repair stale/Oracle/capture; then generic targeted attack and EndTurn; no Boss tuning'))
    sys.exit(0)
d=json.loads(LEDGER.read_text())
if d.get('closedUtc') or len(d['entries'])>=d['cap'] or utc()>=dt.datetime.fromisoformat(d['deadline']) or size()>=d['byteCap']:raise RuntimeError('Registered stop reached')
if any('ended' not in e for e in d['entries']):raise RuntimeError('Inspect uncertain operation before retry')
manifest=Path(sys.argv[1]).resolve();name=sys.argv[2];scenario=sys.argv[3] if len(sys.argv)>3 else 'slice-solver-gate';replay=sys.argv[4] if len(sys.argv)>4 else '';plan=sys.argv[5] if len(sys.argv)>5 else ''
out=LAB/('.build/runs/'+BATCH+'-exec-'+name)
if out.exists():raise RuntimeError('Immutable output exists')
seconds=min(120,int((dt.datetime.fromisoformat(d['deadline'])-utc()).total_seconds()))
if seconds<1:raise RuntimeError('Deadline')
entry=dict(name=name,manifest=str(manifest),output=str(out),scenario=scenario,replay=replay,start=utc().isoformat());d['entries'].append(entry);save(d)
args=['pwsh','-NoProfile','-File',str(LAB/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(manifest),'-OutputDirectory',str(out),'-Scenario',scenario,'-Seed','LAB_V0111_NATIVE_DEV_01','-ProcessTimeoutSeconds',str(seconds)]
if replay:args+=['-ReplayPath',replay]
if plan:args+=['-SolverPlan',plan]
with (WORK/(name+'-wrapper.log')).open('w',encoding='utf-8') as log:
    child=subprocess.Popen(args,stdout=log,stderr=subprocess.STDOUT);entry['wrapperPid']=child.pid;save(d);began=time.monotonic()
    while child.poll() is None:
        if utc()>=dt.datetime.fromisoformat(d['deadline']) or size()>=d['byteCap'] or time.monotonic()-began>seconds+15:
            subprocess.run(['pwsh','-NoProfile','-Command',f'[System.Diagnostics.Process]::GetProcessById({child.pid}).Kill($true)'],check=True);entry['stoppedByGate']=True;break
        time.sleep(.5)
    child.wait()
entry.update(ended=utc().isoformat(),wrapperExit=child.returncode,bytesAfter=size());save(d);print(json.dumps(entry));sys.exit(child.returncode)
