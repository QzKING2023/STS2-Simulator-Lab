"""Review completed/explicitly stopped pilot runs without relabelling stops as success."""
import argparse
import copy
import json
import subprocess
from pilot import LAB, read, write_new, sha
from review_repairs import call


def full(build, directory, output):
    result = call(build/'bin/Release/net9.0/OfflineSearchHarness.dll', ['--review-full-run', directory])
    report, process = read(directory/'result.json'), read(directory/'process.json')
    end, guards = report['result'], report['guards']; iso = guards['isolation']
    steps = [read(p) for p in directory.glob('solver-online-step-*.json')]
    expected = {'Completed': 0, 'Unsupported': 2, 'BudgetExceeded': 3}.get(end['Stop'])
    checks = dict(production=result['exit'] == 0, classified=expected is not None,
        rawExit=process['rawExit'] == expected and process['exitConsistent'] and not process['watchdogExpired'],
        successfulPrefix=bool(steps) and all(s['exact'] and s['parentUnchanged'] and s['expected'] == s['actual'] for s in steps),
        counts=0 <= end['Dispatched'] - end['Confirmed'] <= 1,
        terminal=(end['Terminal'] in ['Death','RunVictory'] and end['DataComplete'] and end['Confirmed']==end['Dispatched']) if expected == 0
                 else end['Stop'] != 'Completed',
        faults=(not guards['faults']) if expected == 0 else all(
            f.splitlines()[0] == (end['Error'] or '').splitlines()[0] for f in guards['faults']),
        isolation=iso['backend']=='MemorySaveStore' and not iso['shouldSave'] and not iso['steamInitialized'] and not iso['uiInstance'])
    payload=dict(buildId=read(build/'build-manifest.json')['buildId'], directory=str(directory.relative_to(LAB)),
        result=end, checks=checks, production=result, taskFaults=[f.splitlines()[0] for f in guards['faults']],
        scope='terminal correctness if Completed; confirmed prefix and explicit stop classification otherwise')
    write_new(output,payload)
    print(json.dumps(dict(stop=end['Stop'],terminal=end['Terminal'],checks=checks)))
    if not all(checks.values()): raise RuntimeError('Review failed; raw evidence retained')


def visible(work, worker, output):
    controls=[]; probes=[]
    def run(name, packet, expected, decision=None):
        p=subprocess.run(['dotnet',str(worker)],input=json.dumps(packet)+'\n',capture_output=True,text=True,encoding='utf-8',timeout=10)
        controls.append(dict(name=name,expected=expected,exit=p.returncode,stdout=p.stdout.strip(),stderr=p.stderr.strip(),
            passed=p.returncode==expected and (decision is None or decision==p.stdout.strip())))
    for char in ['ironclad','silent','regent','necrobinder','defect']:
        directory=work/(('clean-' if char=='silent' else 'b02-')+f'study-{char}-live')
        for q in [1,6]:
            probe=read(directory/f'visible-boundary-{q}.json')
            assert probe['sourceUnchanged'] and probe['zeroDispatch'] and probe['unknownFieldsRejected']==5
            assert len(probe['tests'])==5 and all(t['passed'] for t in probe['tests'])
            packet=json.loads(probe['wire']);run(f'{char}:q{q}',packet,0,probe['decision'])
            probes.append(dict(character=char,query=q,projectionControls=probe['tests'],unknownRejected=5,zeroDispatch=True))
    first=read(work/'b02-study-ironclad-live/visible-boundary-1.json'); source=json.loads(first['wire'])
    mutations=[(field,lambda p,f=field:p.update({f:'hidden'})) for field in ['seed','root','drawOrder','solverResult','logPath']]
    mutations += [('nested-private',lambda p:p['hand'][0].update(privateState='hidden')),
                  ('oracle-label',lambda p:p.update(information='Oracle')),
                  ('wrong-slot',lambda p:p['hand'][0].update(slot=99)),
                  ('wrong-token',lambda p:p['legalActions'][0].update(token='secret-root-hash')),
                  ('duplicate-action',lambda p:p['legalActions'].append(copy.deepcopy(p['legalActions'][0]))),
                  ('unsupported-action',lambda p:p['legalActions'][0].update(kind='UsePotion'))]
    for name,mutate in mutations:
        p=copy.deepcopy(source);mutate(p);run(name,p,1)
    p=copy.deepcopy(source);p['hp']-=1;run('public-change-valid',p,0,first['decision'])
    deps=read(worker.with_suffix('.deps.json'));libs=list(deps['libraries'])
    assert set(x.split('/')[0] for x in libs)=={'VisiblePolicyWorker','SimulatorLab.Core'}
    result=dict(schema='visible-policy-consumer-review/v1',controls=controls,probes=probes,dependencies=libs,
        hashes={p.name:sha(p) for p in worker.parent.iterdir() if p.is_file()},
        scope='trusted pure policy reads stdin DTO only; no game/solver references or callbacks, no alternative action deployment',
        limitations=['not an OS sandbox for hostile code; worker could access filesystem if modified',
          'hidden substitutions use fixed public legality vector, not alternative native worlds',
          'common subset omits class resources, intents and other visible fields; no full-run fair-policy claim'])
    write_new(output,result);print(json.dumps(dict(controls=len(controls),passed=all(r['passed'] for r in controls),probes=len(probes))))
    if not all(r['passed'] for r in controls):raise RuntimeError('Visible consumer failed')


if __name__=='__main__':
    p=argparse.ArgumentParser();p.add_argument('mode',choices=['full','visible']);p.add_argument('--build');p.add_argument('--directory');p.add_argument('--work');p.add_argument('--worker');p.add_argument('--output',required=True)
    a=p.parse_args();out=(LAB/a.output).resolve()
    if not out.is_relative_to(LAB/'.build/runs'):raise ValueError('Lab output required')
    if a.mode=='full':full((LAB/a.build).resolve(),(LAB/a.directory).resolve(),out)
    else:visible((LAB/a.work).resolve(),(LAB/a.worker).resolve(),out)
