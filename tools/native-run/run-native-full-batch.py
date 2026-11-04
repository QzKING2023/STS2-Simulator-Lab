"""Explicit bounded full-run executions; independent from legacy 120s batches."""
from pathlib import Path
import datetime as dt,hashlib,json,re,subprocess,sys,time
LAB=Path(__file__).resolve().parents[2]
if len(sys.argv)<4 or sys.argv[1]!='--batch' or not re.fullmatch(r'solver-[a-z0-9-]+',sys.argv[2]):raise RuntimeError('Explicit batch required')
batch=sys.argv[2];args=sys.argv[3:];work=LAB/'.build/runs'/(batch+'-work01');ledger=work/'ledger.json'
def utc():return dt.datetime.now(dt.timezone.utc)
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def size():
 roots=[work,LAB/'.build/pinned-inputs/combatsolver-0.50.1-ending-powers02']+list((LAB/'.build/native-build').glob(batch+'-*'))+list((LAB/'.build/runs').glob(batch+'-exec-*'))
 return sum(p.stat().st_size for root in set(roots) for p in root.rglob('*') if p.is_file())
def save(d):ledger.write_text(json.dumps(d,indent=2),encoding='utf-8')
if args[0]=='register':
 if ledger.exists():raise RuntimeError('Never reset an existing ledger')
 plan=read(work/'plan.json')
 if plan['nativeCap']!=3 or plan['minutes']!=30 or plan['byteCap']!=2147483648 or plan['processSeconds']!=480:raise RuntimeError('Unexpected registered envelope')
 start=utc();save(dict(start=start.isoformat(),deadline=(start+dt.timedelta(minutes=30)).isoformat(),cap=3,byteCap=2147483648,processSeconds=480,entries=[],planSha256=sha(work/'plan.json')));sys.exit(0)
d=read(ledger)
if d.get('closedUtc') or len(d['entries'])>=d['cap'] or utc()>=dt.datetime.fromisoformat(d['deadline']) or size()>=d['byteCap']:raise RuntimeError('Full-run batch stop reached')
if d['planSha256']!=sha(work/'plan.json') or any('ended' not in e for e in d['entries']):raise RuntimeError('Plan changed or previous execution uncertain')
if (dt.datetime.fromisoformat(d['deadline'])-utc()).total_seconds()<d['processSeconds']+15:raise RuntimeError('Insufficient remaining declared batch time')
manifest=Path(args[0]).resolve();name=args[1];scenario=args[2];replay=args[3];plan=args[4];out=LAB/'.build/runs'/(batch+'-exec-'+name)
if out.exists():raise RuntimeError('Immutable output exists')
tool_paths=[Path(__file__),LAB/'tools/native-run/run-native-slice.ps1',LAB/'tools/native-run/NativeProcessBudget.ps1'];tools={str(p):sha(p) for p in tool_paths}
entry=dict(name=name,start=utc().isoformat(),manifest=str(manifest),output=str(out),scenario=scenario,replay=replay,solverPlan=str(Path(plan).resolve()),solverPlanSha256=sha(Path(plan)),tools=tools);d['entries'].append(entry);save(d)
cmd=['pwsh','-NoProfile','-File',str(LAB/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(manifest),'-OutputDirectory',str(out),'-Scenario',scenario,'-Seed','LAB_V0111_NATIVE_DEV_01','-ProcessTimeoutSeconds','480','-ReplayPath',replay,'-SolverPlan',plan]
with (work/(name+'-wrapper.log')).open('w',encoding='utf-8') as log:
 child=subprocess.Popen(cmd,stdout=log,stderr=subprocess.STDOUT);entry['wrapperPid']=child.pid;save(d);began=time.monotonic()
 while child.poll() is None:
  if utc()>=dt.datetime.fromisoformat(d['deadline']) or size()>=d['byteCap'] or time.monotonic()-began>495:
   subprocess.run(['pwsh','-NoProfile','-Command',f'[System.Diagnostics.Process]::GetProcessById({child.pid}).Kill($true)'],check=True);entry['stoppedByBatchGate']=True;break
  time.sleep(.5)
 child.wait()
entry.update(ended=utc().isoformat(),wrapperExit=child.returncode,bytesAfter=size(),toolHashesUnchanged=all(sha(Path(p))==h for p,h in tools.items()),solverPlanUnchanged=sha(Path(plan))==entry['solverPlanSha256']);save(d)
if not entry['toolHashesUnchanged'] or not entry['solverPlanUnchanged']:raise RuntimeError('Execution tool or config changed during execution')
print(json.dumps(entry));sys.exit(child.returncode)
