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
ID='possessed-root.1'
POLICY='upstream-explicit-conditions/no-root-fallback/v1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--base',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    a=ap.parse_args();base=a.base.resolve();out=a.output.resolve();lock=read(base/'solver-lock.json');parent=read(base/'derivative-lock.json')
    if not out.is_relative_to(LAB/'.build') or out.exists():raise RuntimeError('Fresh Lab .build output required')
    if lock['version']!='0.50.1' or parent['id']!='ending-powers.2' or len(lock['source'])!=826:raise RuntimeError('Reviewed terminal-hooks base required')
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
    path='src/Search/SimulatedCombatState.cs';p=out/'source'/path;original=sha(p);text=p.read_text(encoding='utf-8-sig')
    before='            if (power is DampenPower dampen)\n                CaptureDampenRootState(simulator, dampen);'
    after=before+'\n            if (power is PossessSpeedPower or PossessStrengthPower)\n                CapturePossessedRootState(power);'
    if text.count(before)!=1:raise RuntimeError('Root power materialization anchor changed')
    p.write_text(text.replace(before,after),encoding='utf-8',newline='\n')
    changes=[dict(path=path,before=original,after=sha(p))]
    path='src/Search/SimulatedCombatState.Possess.cs';p=out/'source'/path;original=sha(p);text=p.read_text(encoding='utf-8-sig')
    before='    private void RecordPossessedStatChange('
    addition="""    // Capture pre-existing native debt at root materialization. Reuse the
    // upstream forkable ledger, fingerprint and refund; never read live debt in a fork.
    private void CapturePossessedRootState(PowerModel power)
    {
        object? data = PowerInternalDataField.GetValue(power);
        if (data == null) return; // native lazy data has never recorded a debit
        string field = power is PossessSpeedPower ? "stolenDexterity" : "stolenStrength";
        var debt = data.GetType().GetField(field)?.GetValue(data)
            as IReadOnlyDictionary<Creature, decimal>
            ?? throw new InvalidOperationException("Possession native debt shape changed.");
        foreach (var (target, amount) in debt.ToArray())
        {
            if (!_rootCreatures.Contains(target) || target.Player == null || amount > 0
                || amount != decimal.Truncate(amount) || amount < int.MinValue)
                throw new InvalidOperationException("Unsupported possession debt target/amount.");
            if (amount == 0) continue;
            var ledger = power is PossessSpeedPower
                ? _stolenDexterity ??= [] : _stolenStrength ??= [];
            if (ledger.ContainsKey((power.Owner, target)))
                throw new InvalidOperationException("Duplicate possession root debt.");
            ledger[(power.Owner, target)] = (int)amount;
        }
    }

"""
    if text.count(before)!=1:raise RuntimeError('Upstream possession ledger anchor changed')
    p.write_text(text.replace(before,addition+before),encoding='utf-8',newline='\n');changes.append(dict(path=path,before=original,after=sha(p)))
    build=out/'build';build.mkdir();project=template.read_text(encoding='utf-8-sig')
    if project.count('Value="ending-powers.2"')!=1 or 'CopyMod' in project or 'ModOutputDir' in project:raise RuntimeError('Safe template identity/deployment guard')
    (build/'SolverLab.csproj').write_text(project.replace('Value="ending-powers.2"','Value="'+ID+'"'),encoding='utf-8');shutil.copy2(resource,build/'English.json')
    cache=Path(os.environ['USERPROFILE'])/'.nuget/packages'
    with (out/'build.log').open('w',encoding='utf-8') as log:
        proc=subprocess.run(['dotnet','build',str(build/'SolverLab.csproj'),'-c','Release','--nologo','--ignore-failed-sources','-p:NuGetAudit=false','-p:RestoreSources='+str(cache)],stdout=log,stderr=subprocess.STDOUT,timeout=180)
    if proc.returncode:raise RuntimeError('Ending power dependency compile failed; output retained')
    for r in lock['assemblies']:
        source=build/'.godot/mono/temp/bin/Release/CombatSolver.dll' if r['name']=='CombatSolver' else base/r['file'];shutil.copy2(source,out/r['file'])
    derivative=dict(schema='solver-lab-derivative/v1',id=ID,policy=POLICY,baseLockSha256=sha(base/'solver-lock.json'),baseSolverSha256=sha(base/'CombatSolver.dll'),baseDerivative='ending-powers.2',changedSources=changes,projectSha256=sha(build/'SolverLab.csproj'),resource=dict(sha256=sha(build/'English.json')),toolSha256=sha(Path(__file__)),buildLogSha256=sha(out/'build.log'),boundaryPolicy='PowerCmd.Apply/IsOverOrEnding',scope='root capture of pre-existing native possession debt into upstream forkable ledgers; scores/config, strict fallback and ending guard unchanged')
    (out/'derivative-lock.json').write_text(json.dumps(derivative,indent=2),encoding='utf-8')
    lock.update(producerBuild=ID,producerManifestHash=sha(out/'derivative-lock.json'),source=[dict(path=r['path'],sha256=sha(out/'source'/r['path'])) for r in lock['source']],assemblies=[dict(name=r['name'],file=r['file'],sha256=sha(out/r['file'])) for r in lock['assemblies']],derivative=derivative,scope='possessed-root.1 derivative of ending-powers.2; original root debt capture; no Mod initialization')
    (out/'solver-lock.json').write_text(json.dumps(lock,indent=2),encoding='utf-8')
    print(json.dumps(dict(output=str(out),id=ID,policy=POLICY,changedSources=2,solverSha256=sha(out/'CombatSolver.dll'))))
if __name__=='__main__':main()
