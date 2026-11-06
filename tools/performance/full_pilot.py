"""Run only preregistered complete-run pilot samples under one shared budget."""
import argparse
import json
import shutil
import subprocess
import time
from datetime import datetime, timezone
from pilot import LAB, read, write_new, sha, size, extract


def run(work, build, seed, replay=None):
    reg = read(work / 'registration.json')
    scope = 'fullReplay' if replay else 'fullRun'
    if 'Ironclad/' + seed not in reg['cases'][scope]:
        raise ValueError('Unregistered full sample')
    # Do not promote native exit 0 without the production correctness gate.
    for c, i in [('Silent', 1), ('Silent', 2), ('Necrobinder', 1), ('Necrobinder', 2),
                 ('Ironclad', 1), ('Regent', 1), ('Defect', 1)]:
        r = read(work / f'review-{c.lower()}-{i:02d}.json')
        if r['exit'] != 0 or r['buildId'] != read(build)['buildId']:
            raise RuntimeError('Prerequisite repair/regression not certified on this build')
    ledger = work / 'launches.jsonl'
    prior = [json.loads(s) for s in ledger.read_text().splitlines()]
    first = prior[0]['startedUnix']
    def stop():
        if time.time() - first >= reg['nativeWallSeconds']: return 'batch-wall-budget'
        if size(work) >= reg['maxEvidenceBytes']: return 'batch-storage-budget'
        return None
    if len(prior) >= reg['maxNativeProcesses'] or stop():
        raise RuntimeError('Pilot budget exhausted')
    mode = 'replay' if replay else 'live'
    name = 'full-' + seed.removeprefix('LAB_V0111_NATIVE_').lower().replace('_', '-') + '-' + mode
    target, inp = work / name, work / (name + '-input')
    if target.exists() or inp.exists():
        raise RuntimeError('Refusing duplicate launch or overwrite')
    inp.mkdir()
    for file in ['route.json', 'choices.json', 'reward-choices.json']:
        if replay:
            shutil.copyfile(replay / file, inp / file)
        else:
            write_new(inp / file, [])
    template = LAB / '.build/runs/m1-acceptance-work07/online-plan.json'
    plan = read(template)
    plan.update(InputSha=sha(inp / 'route.json'), ChoiceSha=sha(inp / 'choices.json'),
                RewardSha=sha(inp / 'reward-choices.json'))
    write_new(inp / 'plan.json', plan)
    write_new(inp / 'provenance.json', dict(profileSource=str(template.relative_to(LAB)),
               profileSha=sha(template), information='Oracle', scoreChanges=False,
               interpretation='online measurement or frozen-action replay; not a player-visible policy'))
    cmd = ['pwsh', '-NoProfile', '-File', str(LAB / 'tools/native-run/run-native-slice.ps1'),
           '-BuildManifest', str(build), '-OutputDirectory', str(target), '-Seed', seed,
           '-Scenario', 'solver-native-darv-act2' + ('-replay' if replay else ''),
           '-Observation', 'detailed', '-ReplayPath', str(inp / 'route.json'),
           '-SolverPlan', str(inp / 'plan.json'), '-ProcessTimeoutSeconds', str(reg['perFullRunSeconds'])]
    launch = dict(name=name, startedUnix=time.time(), utc=datetime.now(timezone.utc).isoformat(),
                  buildId=read(build)['buildId'], command=cmd, inputHashes={p.name: sha(p) for p in inp.iterdir()})
    with ledger.open('a', encoding='utf-8') as f:
        f.write(json.dumps(launch) + '\n')
    forced = None
    with (work / (name + '-wrapper.log')).open('x', encoding='utf-8') as log:
        p = subprocess.Popen(cmd, cwd=LAB, stdout=log, stderr=subprocess.STDOUT)
        while p.poll() is None:
            forced = stop()
            if forced:
                subprocess.run(['pwsh', '-NoProfile', '-Command',
                                f'[Diagnostics.Process]::GetProcessById({p.pid}).Kill($true)'], check=True)
                p.wait(timeout=10)
                break
            time.sleep(1)
    completion = dict(name=name, wrapperExit=p.returncode, forcedStop=forced,
                      endedUtc=datetime.now(timezone.utc).isoformat(), batchBytes=size(work))
    if (target / 'process.json').exists():
        completion['measurement'] = extract(target)
    write_new(work / (name + '-completion.json'), completion)
    print(json.dumps(completion, ensure_ascii=False), flush=True)
    if forced: raise RuntimeError(forced)


if __name__ == '__main__':
    ap = argparse.ArgumentParser()
    ap.add_argument('--work', type=lambda s: (LAB / s).resolve(), required=True)
    ap.add_argument('--build', type=lambda s: (LAB / s).resolve(), required=True)
    ap.add_argument('--seed', required=True)
    ap.add_argument('--replay', type=lambda s: (LAB / s).resolve())
    a = ap.parse_args()
    if not a.work.is_relative_to(LAB / '.build/runs'):
        raise ValueError('Output must stay in Lab')
    run(a.work, a.build, a.seed, a.replay)
