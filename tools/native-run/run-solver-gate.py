"""Persistent, non-resuming bounded ledger for the small solver integration gate."""
from pathlib import Path
import datetime as dt, json, subprocess, sys, time
LAB = Path(__file__).resolve().parents[2]
WORK = LAB / '.build/runs/solver-gate-work01'
LEDGER = WORK / 'ledger.json'
def utc(): return dt.datetime.now(dt.timezone.utc)
def save(d): LEDGER.write_text(json.dumps(d, indent=2), encoding='utf-8')
def size():
    roots = [WORK, LAB / '.build/pinned-inputs/combatsolver-0.50.1-gate01']
    roots += list((LAB / '.build/native-build').glob('solver-gate-*'))
    roots += list((LAB / '.build/runs').glob('solver-gate-exec-*'))
    return sum(p.stat().st_size for root in roots for p in root.rglob('*') if p.is_file())
if sys.argv[1] == 'register':
    if LEDGER.exists(): raise ValueError('Existing ledger; inspect, never reset')
    WORK.mkdir(parents=True, exist_ok=True)
    start = utc()
    d = dict(start=start.isoformat(), deadline=(start+dt.timedelta(minutes=20)).isoformat(),
             cap=4, byteCap=2*1024**3, processSeconds=120, entries=[], initialBytes=size(),
             scope='ordinary Ironclad normal-start capture/search purity + one native action/diff/requery; no Boss tuning')
    save(d); print(json.dumps(d)); sys.exit(0)
d = json.loads(LEDGER.read_text())
if len(d['entries']) >= d['cap'] or utc() >= dt.datetime.fromisoformat(d['deadline']) or size() >= d['byteCap']:
    raise RuntimeError('Registered gate stop reached')
if any('ended' not in e for e in d['entries']): raise RuntimeError('Uncertain prior operation: inspect it first')
manifest = Path(sys.argv[1]).resolve(); name = sys.argv[2]
out = LAB / ('.build/runs/solver-gate-exec-' + name)
scenario = sys.argv[3] if len(sys.argv)>3 else 'slice-solver-gate'
replay = sys.argv[4] if len(sys.argv)>4 else ''
seconds = min(120, int((dt.datetime.fromisoformat(d['deadline'])-utc()).total_seconds()))
if seconds<1 or out.exists(): raise RuntimeError('Deadline or existing output')
entry = dict(name=name, manifest=str(manifest), output=str(out), scenario=scenario, replay=replay, start=utc().isoformat())
d['entries'].append(entry); save(d)
args = ['pwsh', '-NoProfile', '-File', str(LAB/'tools/native-run/run-native-slice.ps1'),
        '-BuildManifest', str(manifest), '-OutputDirectory', str(out), '-Scenario', scenario,
        '-Seed', 'LAB_V0111_NATIVE_DEV_01', '-ProcessTimeoutSeconds', str(seconds)]
if replay: args += ['-ReplayPath', replay]
with (WORK/(name+'-wrapper.log')).open('w', encoding='utf-8') as log:
    child = subprocess.Popen(args, stdout=log, stderr=subprocess.STDOUT)
    entry['wrapperPid'] = child.pid; save(d); began=time.monotonic()
    while child.poll() is None:
        used=size()
        if utc() >= dt.datetime.fromisoformat(d['deadline']) or used >= d['byteCap'] or time.monotonic()-began>seconds+15:
            # Exact owned child only; Kill(true) includes its native dotnet child.
            subprocess.run(['pwsh','-NoProfile','-Command',f'[System.Diagnostics.Process]::GetProcessById({child.pid}).Kill($true)'], check=True)
            entry['stoppedByGate']=True; break
        time.sleep(0.5)
    child.wait()
entry.update(ended=utc().isoformat(), wrapperExit=child.returncode, bytesAfter=size())
save(d); print(json.dumps(entry)); sys.exit(child.returncode)
