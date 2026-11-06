"""Create a new MIT CombatSolver derivative for post-orb terminal commitment.

Reuse upstream CheckWinCondition and terminal stamp. Never edit
the parent pin or deploy a Mod. No history fields are removed or normalized.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess

LAB = Path(__file__).resolve().parents[2]
ID = 'orb-terminal-boundary.1'
POLICY = 'upstream-explicit-conditions/no-root-fallback/v1'
def sha(p): return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def read(p): return json.loads(p.read_text(encoding='utf-8-sig'))

def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--base',type=Path,required=True)
    ap.add_argument('--output',type=Path,required=True)
    a=ap.parse_args();base=a.base.resolve();out=a.output.resolve()
    lock=read(base/'solver-lock.json');parent=read(base/'derivative-lock.json')
    if not out.is_relative_to(LAB/'.build') or out.exists(): raise RuntimeError('Fresh Lab output required')
    if lock['producerBuild']!='osty-root-identity.1' or parent['id']!='osty-root-identity.1' or len(lock['source'])!=826: raise RuntimeError('Reviewed parent required')
    for r in lock['source']:
        if sha(base/'source'/r['path'])!=r['sha256']: raise RuntimeError('Changed parent source')
    for r in lock['assemblies']:
        if sha(base/r['file'])!=r['sha256']: raise RuntimeError('Changed parent assembly')
    for name,expected in [('LICENSE',lock['licenseSha256']),('CombatSolver.json',lock['manifestSha256']),('derivative-lock.json',lock['producerManifestHash']),('build/SolverLab.csproj',parent['projectSha256']),('build/English.json',parent['resource']['sha256'])]:
        if sha(base/name)!=expected: raise RuntimeError('Changed reviewed build/license input')
    out.mkdir(parents=True)
    for r in lock['source']:
        p=out/'source'/r['path'];p.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(base/'source'/r['path'],p)
    for name in ['LICENSE','CombatSolver.json']: shutil.copy2(base/name,out/name)
    changes=[]
    def edit(name,old,new):
        p=out/'source'/name;t=p.read_text(encoding='utf-8-sig');before=sha(p)
        if t.count(old)!=1: raise RuntimeError('Exact source anchor changed: '+name)
        p.write_text(t.replace(old,new),encoding='utf-8',newline='\n')
        changes.append(dict(path=name,before=before,after=sha(p)))
    edit('src/Engine/InCombat/Simulation/CombatPredictionSimulator.EndTurn.cs',
         '\n    internal bool SimulateEndPlayerTurnAfterOrbPassives(int playerTurn)\n    {\n        if (IsOverOrEnding)\n            return true;',
         '\n    internal bool SimulateEndPlayerTurnAfterOrbPassives(int playerTurn)\n    {\n        if (IsOverOrEnding)\n        {\n            // Orb callbacks have returned: commit the existing terminal fact before\n            // any flush/side transition can invalidate this turn history.\n            CheckWinCondition(playerTurn);\n            return true;\n        }')
    build=out/'build';build.mkdir();project=(base/'build/SolverLab.csproj').read_text(encoding='utf-8-sig')
    if project.count('Value="osty-root-identity.1"')!=1 or 'CopyMod' in project or 'ModOutputDir' in project: raise RuntimeError('Safe build template changed')
    (build/'SolverLab.csproj').write_text(project.replace('Value="osty-root-identity.1"','Value="'+ID+'"'),encoding='utf-8')
    shutil.copy2(base/'build/English.json',build/'English.json')
    with (out/'build.log').open('w',encoding='utf-8') as log:
        p=subprocess.run(['dotnet','build',str(build/'SolverLab.csproj'),'-c','Release','--nologo','--ignore-failed-sources','-p:NuGetAudit=false','-p:RestoreSources='+str(Path(os.environ['USERPROFILE'])/'.nuget/packages')],stdout=log,stderr=subprocess.STDOUT,timeout=180)
    if p.returncode: raise RuntimeError('Dependency compile failed; retained build.log')
    for r in lock['assemblies']:
        src=build/'.godot/mono/temp/bin/Release/CombatSolver.dll' if r['name']=='CombatSolver' else base/r['file']
        shutil.copy2(src,out/r['file'])
    derivative=dict(schema='solver-lab-derivative/v1',id=ID,policy=POLICY,baseLockSha256=sha(base/'solver-lock.json'),baseSolverSha256=sha(base/'CombatSolver.dll'),baseDerivative='osty-root-identity.1',changedSources=changes,projectSha256=sha(build/'SolverLab.csproj'),resource=dict(sha256=sha(build/'English.json')),toolSha256=sha(Path(__file__)),buildLogSha256=sha(out/'build.log'),boundaryPolicy='PowerCmd.Apply/IsOverOrEnding',scope='commit post-orb terminal before side/history transition; preserve original callbacks and all stamp fields; no scoring or character special case')
    (out/'derivative-lock.json').write_text(json.dumps(derivative,indent=2),encoding='utf-8')
    lock.update(producerBuild=ID,producerManifestHash=sha(out/'derivative-lock.json'),source=[dict(path=r['path'],sha256=sha(out/'source'/r['path'])) for r in lock['source']],assemblies=[dict(name=r['name'],file=r['file'],sha256=sha(out/r['file'])) for r in lock['assemblies']],derivative=derivative,scope=derivative['scope'])
    (out/'solver-lock.json').write_text(json.dumps(lock,indent=2),encoding='utf-8')
    print(json.dumps(dict(output=str(out),solverSha256=sha(out/'CombatSolver.dll'),id=ID,changedFiles=len({c['path'] for c in changes}))))
if __name__=='__main__': main()
