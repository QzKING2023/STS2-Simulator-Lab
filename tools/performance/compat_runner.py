"""Serial registered search-grid and cross-character run pilot; fresh outputs only."""
import argparse
import json
import shutil
import subprocess
import time
from datetime import datetime, timezone
from pilot import LAB, read, write_new, sha, size, extract


def run(work, build, character, full=False, replay=None, label='b03-',visible=False,replay_attempt=False,event_choice_profile='default'):
    reg=read(work/'registration.json')
    scope=reg
    if character not in scope['characters'] and not (visible and character=='Ironclad'): raise ValueError('Unregistered character')
    ledger=work/'launches.jsonl'
    prior=[json.loads(s) for s in ledger.read_text().splitlines()] if ledger.exists() else []
    first=prior[0]['startedUnix'] if prior else time.time()
    def stop():
        if time.time()-first>=reg['nativeWallSeconds']:return 'batch-time'
        if size(work)>=reg['maxEvidenceBytes']:return 'batch-storage'
        return None
    if len(prior)>=reg['maxNativeProcesses'] or stop():raise RuntimeError('Batch exhausted')
    if replay and not (full or visible):raise ValueError('Only registered full replays')
    name=label+('visible-' if visible else 'full-' if full else 'study-')+character.lower()+('-replay' if replay else '-live')
    output,inp=work/name,work/(name+'-input')
    if output.exists() or inp.exists():raise RuntimeError('Refusing evidence overwrite/duplicate launch')
    inp.mkdir()
    for file in ['route.json','choices.json','reward-choices.json']:
        if replay and (replay/file).exists():shutil.copyfile(replay/file,inp/file)
        elif replay and not (visible and file=='reward-choices.json'):raise FileNotFoundError(replay/file)
        else:write_new(inp/file,[])
    if replay_attempt:
        if not replay:raise ValueError('Attempt replay needs original source')
        end=read(replay/'result.json')['result']
        if end['Dispatched']!=end['Confirmed']+1 or end['Stop'] not in ['Unsupported','InvalidAction','DataError','EngineError']:raise ValueError('Source has no single unconfirmed dispatched attempt')
        pending='a'+str(end['Dispatched']);requests=[]
        with (replay/'trace.jsonl').open(encoding='utf-8-sig') as f:
            for line in f:
                row=json.loads(line)['payload']
                if row['root']==pending and row['kind']=='root-request':requests.append(row)
        if len(requests)!=1:raise ValueError('Missing/duplicate attempted root evidence')
        route=read(inp/'route.json');request=requests[0]['detail']
        # This is a new replay input, never an edit of the original trace/result.
        # No successful after-state is asserted. Unexpected completion fails replay.
        route.append(dict(action=request['action'],before=request['before'],after=None,confirmed=None,dispatched=end['Dispatched']))
        (inp/'route.json').write_text(json.dumps(route,indent=2),encoding='utf-8')
        write_new(inp/'unconfirmed-attempt.json',dict(source=str(replay),traceSha256=sha(replay/'trace.jsonl'),root=pending,request=requests[0],expectation='Original failure tail; after state unknown, not a confirmed action'))
    if full:
        plan=read(LAB/'.build/runs/m1-acceptance-work07/online-plan.json')
        plan.update(Character=character,Pilot='character-full/v1',SearchStudy=False,EventChoiceProfile=event_choice_profile)
    else:
        plan=dict(Schema='solver-coordinator-prefix/v1',PrefixCount=0,MaxActions=64,Probe=False,Character=character,SearchStudy=True)
    plan.update(InputSha=sha(inp/'route.json'),ChoiceSha=sha(inp/'choices.json'),RewardSha=sha(inp/'reward-choices.json'))
    write_new(inp/'plan.json',plan)
    scenario=('visible-battle' if visible else 'solver-native-darv-act2' if full else 'solver-character-pilot')+('-replay' if replay else '')
    cmd=['pwsh','-NoProfile','-File',str(LAB/'tools/native-run/run-native-slice.ps1'),'-BuildManifest',str(build),'-OutputDirectory',str(output),
         '-Seed',scope['seed'],'-Scenario',scenario,'-Observation','detailed','-ReplayPath',(str(inp/'route.json') if replay or not visible else ''),'-SolverPlan',('' if visible else str(inp/'plan.json')),
         '-ProcessTimeoutSeconds',str(reg['perFullRunSeconds'] if full else reg['perShortSeconds'])]
    launch=dict(name=name,startedUnix=time.time(),utc=datetime.now(timezone.utc).isoformat(),buildId=read(build)['buildId'],command=cmd,inputHashes={p.name:sha(p) for p in inp.iterdir()})
    with ledger.open('a',encoding='utf-8') as f:f.write(json.dumps(launch)+'\n')
    forced=None
    with (work/(name+'-wrapper.log')).open('x',encoding='utf-8') as log:
        p=subprocess.Popen(cmd,cwd=LAB,stdout=log,stderr=subprocess.STDOUT)
        while p.poll() is None:
            forced=stop()
            if forced:
                subprocess.run(['pwsh','-NoProfile','-Command',f'[Diagnostics.Process]::GetProcessById({p.pid}).Kill($true)'],check=True)
                p.wait(timeout=10);break
            time.sleep(1)
    completion=dict(name=name,wrapperExit=p.returncode,forcedStop=forced,endedUtc=datetime.now(timezone.utc).isoformat(),bytes=size(work))
    if (output/'process.json').exists():completion['measurement']=extract(output)
    write_new(work/(name+'-completion.json'),completion)
    print(json.dumps(completion,ensure_ascii=False),flush=True)
    if forced:raise RuntimeError(forced)
    return output


if __name__=='__main__':
    a=argparse.ArgumentParser()
    a.add_argument('--work',type=lambda s:(LAB/s).resolve(),required=True)
    a.add_argument('--build',type=lambda s:(LAB/s).resolve(),required=True)
    a.add_argument('--character',required=True)
    a.add_argument('--full',action='store_true')
    a.add_argument('--visible',action='store_true')
    a.add_argument('--replay-attempt',action='store_true')
    a.add_argument('--replay',type=lambda s:(LAB/s).resolve())
    a.add_argument('--label',default='b03-')
    a.add_argument('--event-choice-profile',choices=['default','amalgamator-strikes','field-resist'],default='default')
    x=a.parse_args()
    if not x.work.is_relative_to(LAB/'.build/runs') or any(c not in 'abcdefghijklmnopqrstuvwxyz0123456789-' for c in x.label):raise ValueError('Invalid Lab output')
    run(x.work,x.build,x.character,x.full,x.replay,x.label,x.visible,x.replay_attempt,x.event_choice_profile)
