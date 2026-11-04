"""Native possession root-state derivative, reusing the reviewed safe build project.

Reuse the existing branch-local IsEnding predicate at the common power application
entry to match native PowerCmd.Apply. No card/monster special case, scoring change,
deployment target or audit relaxation is added.
"""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess

LAB=Path(__file__).resolve().parents[2]
ID='constant-intent.1'
POLICY='upstream-explicit-conditions/no-root-fallback/v1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--base',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    a=ap.parse_args();base=a.base.resolve();out=a.output.resolve();lock=read(base/'solver-lock.json');parent=read(base/'derivative-lock.json')
    if not out.is_relative_to(LAB/'.build') or out.exists():raise RuntimeError('Fresh Lab .build output required')
    if lock['version']!='0.50.1' or parent['id']!='possessed-root.1' or len(lock['source'])!=826:raise RuntimeError('Reviewed terminal-hooks base required')
    for r in lock['source']:
        if sha(base/'source'/r['path'])!=r['sha256']:raise RuntimeError('Base source changed')
    for r in lock['assemblies']:
        if sha(base/r['file'])!=r['sha256']:raise RuntimeError('Base DLL changed')
    template=base/'build/SolverLab.csproj';resource=base/'build/English.json'
    if sha(template)!=parent['projectSha256'] or sha(resource)!=parent['resource']['sha256']:raise RuntimeError('Reviewed build template changed')
    out.mkdir(parents=True)
    for r in lock['source']:
        d=out/'source'/r['path'];d.parent.mkdir(parents=True,exist_ok=True);shutil.copy2(base/'source'/r['path'],d)
    for name in ['LICENSE','CombatSolver.json']:shutil.copy2(base/name,out/name)
    path='src/Prediction/IntentForecaster.cs';p=out/'source'/path;original=sha(p);text=p.read_text(encoding='utf-8-sig')
    before='attack.DamageCalc?.Target != null && !IsKnownStableAttack(monster, move.Id)'
    after='attack.DamageCalc?.Target != null && !IsKnownConstantIntent(attack) && !IsKnownStableAttack(monster, move.Id)'
    if text.count(before)!=1:raise RuntimeError('Forecast anchor changed')
    text=text.replace(before,after)
    addition="""
    // Recognize the game's integer-value constructor, not a monster or move name.
    // Do not evaluate arbitrary callbacks to guess whether they are constant.
    internal static bool IsKnownConstantIntent(AttackIntent intent)
    {
        if (intent.GetType() != typeof(SingleAttackIntent) || intent.DamageCalc == null) return false;
        var ctor = typeof(SingleAttackIntent).GetConstructor([typeof(int)])!;
        if (HarmonyLib.Harmony.GetPatchInfo(ctor) is { Owners.Count: > 0 }) return false;
        var candidate = intent.DamageCalc;
        if (candidate.GetInvocationList().Length != 1 || candidate.Method.Module != typeof(SingleAttackIntent).Module
            || HarmonyLib.Harmony.GetPatchInfo(candidate.Method) is { Owners.Count: > 0 }) return false;
        var reference = new SingleAttackIntent(0).DamageCalc!;
        return candidate.Method == reference.Method && candidate.Target?.GetType() == reference.Target?.GetType();
    }

"""
    anchor='    private static MoveState RollNext('
    if text.count(anchor)!=1:raise RuntimeError('Insertion anchor changed')
    p.write_text(text.replace(anchor,addition+anchor),encoding='utf-8',newline='\n')
    changes=[dict(path=path,before=original,after=sha(p))]
    build=out/'build';build.mkdir();project=template.read_text(encoding='utf-8-sig')
    if project.count('Value="possessed-root.1"')!=1 or 'CopyMod' in project or 'ModOutputDir' in project:raise RuntimeError('Safe template identity/deployment guard')
    (build/'SolverLab.csproj').write_text(project.replace('Value="possessed-root.1"','Value="'+ID+'"'),encoding='utf-8');shutil.copy2(resource,build/'English.json')
    cache=Path(os.environ['USERPROFILE'])/'.nuget/packages'
    with (out/'build.log').open('w',encoding='utf-8') as log:
        proc=subprocess.run(['dotnet','build',str(build/'SolverLab.csproj'),'-c','Release','--nologo','--ignore-failed-sources','-p:NuGetAudit=false','-p:RestoreSources='+str(cache)],stdout=log,stderr=subprocess.STDOUT,timeout=180)
    if proc.returncode:raise RuntimeError('Ending power dependency compile failed; output retained')
    for r in lock['assemblies']:
        source=build/'.godot/mono/temp/bin/Release/CombatSolver.dll' if r['name']=='CombatSolver' else base/r['file'];shutil.copy2(source,out/r['file'])
    derivative=dict(schema='solver-lab-derivative/v1',id=ID,policy=POLICY,baseLockSha256=sha(base/'solver-lock.json'),baseSolverSha256=sha(base/'CombatSolver.dll'),baseDerivative='possessed-root.1',changedSources=changes,projectSha256=sha(build/'SolverLab.csproj'),resource=dict(sha256=sha(build/'English.json')),toolSha256=sha(Path(__file__)),buildLogSha256=sha(out/'build.log'),boundaryPolicy='PowerCmd.Apply/IsOverOrEnding',scope='recognize original SingleAttackIntent integer constructor; dynamic delegates remain approximate; no scoring/monster rules')
    (out/'derivative-lock.json').write_text(json.dumps(derivative,indent=2),encoding='utf-8')
    lock.update(producerBuild=ID,producerManifestHash=sha(out/'derivative-lock.json'),source=[dict(path=r['path'],sha256=sha(out/'source'/r['path'])) for r in lock['source']],assemblies=[dict(name=r['name'],file=r['file'],sha256=sha(out/r['file'])) for r in lock['assemblies']],derivative=derivative,scope='constant-intent.1 derivative of possessed-root.1; exact game integer constructor provenance; no Mod initialization')
    (out/'solver-lock.json').write_text(json.dumps(lock,indent=2),encoding='utf-8')
    print(json.dumps(dict(output=str(out),id=ID,policy=POLICY,changedSources=1,solverSha256=sha(out/'CombatSolver.dll'))))
if __name__=='__main__':main()
