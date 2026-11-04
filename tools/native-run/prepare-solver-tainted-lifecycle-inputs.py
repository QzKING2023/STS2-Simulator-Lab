"""Fixed Lab derivative of MIT CombatSolver. Reuse branch attachment and power state; preserve parent pins and attribution."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess

LAB=Path(__file__).resolve().parents[2]
ID='tainted-lifecycle.1'
POLICY='upstream-explicit-conditions/no-root-fallback/v1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--base',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    a=ap.parse_args();base=a.base.resolve();out=a.output.resolve();lock=read(base/'solver-lock.json');parent=read(base/'derivative-lock.json')
    if not out.is_relative_to(LAB/'.build') or out.exists():raise RuntimeError('Fresh Lab .build output required')
    if lock['version']!='0.50.1' or parent['id']!='constant-intent.1' or len(lock['source'])!=826:raise RuntimeError('Reviewed terminal-hooks base required')
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
    changes=[]
    path='src/Search/SimulatedCombatState.PowerLifecycle.cs';p=out/'source'/path;original=sha(p);text=p.read_text(encoding='utf-8-sig')
    text=text.replace('private int? _lastNormalizedVitalSparkAmount;', 'private Dictionary<Creature, int>? _lastNormalizedVitalSparkSources; // immutable snapshot shared by forks')
    start=text.index('        bool hasVitalSpark = vitalSparkAmount > 0;')
    end=text.index('        IReadOnlyList<Player> players = Players;',start)
    replacement="""        // A vanished listener does not prove AfterRemoved can affect cards.
        // Native death detaches the owner before AfterRemoved; that hook returns
        // when oldOwner.CombatState is null. Compare actual branch owners/amounts.
        var previousSources = _lastNormalizedVitalSparkSources;
        var currentSources = new Dictionary<Creature, int>();
        bool removedAttachedVitalSpark = false;
        int? changedAttachedVitalSparkAmount = null;
        var owners = powers.OfType<VitalSparkPower>().Select(p => p.Owner)
            .Concat(previousSources?.Keys.AsEnumerable() ?? Enumerable.Empty<Creature>()).Distinct();
        foreach (Creature source in owners)
        {
            bool attached = simulator.State.IsAttachedToCombat(source);
            int amount = attached ? GetAmount<VitalSparkPower>(source) : 0;
            int prior = previousSources?.GetValueOrDefault(source) ?? 0;
            if (previousSources != null && attached && amount != prior)
            {
                if (prior > 0 && amount <= 0) removedAttachedVitalSpark = true;
                else if (amount > 0) changedAttachedVitalSparkAmount = amount;
            }
            currentSources[source] = amount;
        }
        _lastNormalizedVitalSparkSources = currentSources;
"""
    text=text[:start]+replacement+text[end:]
    text=text.replace('if (!hasVitalSpark)', 'if (removedAttachedVitalSpark)')
    text=text.replace('else if (vitalSparkAmountChanged && tainted.Amount != vitalSparkAmount)\n                        card.MutablePreview.Affliction!.Amount = vitalSparkAmount;', 'else if (changedAttachedVitalSparkAmount is int changedAmount && tainted.Amount != changedAmount)\n                        card.MutablePreview.Affliction!.Amount = changedAmount;')
    if '_lastNormalizedVitalSparkAmount' in text or 'vitalSparkAmountChanged' in text:raise RuntimeError('Unreplaced lifecycle anchor')
    p.write_text(text,encoding='utf-8',newline='\n');changes.append(dict(path=path,before=original,after=sha(p)))
    path='src/Search/SimulatedCombatState.Fork.cs';p=out/'source'/path;original=sha(p);text=p.read_text(encoding='utf-8-sig')
    if text.count('_lastNormalizedVitalSparkAmount = _lastNormalizedVitalSparkAmount,')!=1:raise RuntimeError('Fork lifecycle anchor changed')
    text=text.replace('_lastNormalizedVitalSparkAmount = _lastNormalizedVitalSparkAmount,','_lastNormalizedVitalSparkSources = _lastNormalizedVitalSparkSources,')
    p.write_text(text,encoding='utf-8',newline='\n');changes.append(dict(path=path,before=original,after=sha(p)))
    build=out/'build';build.mkdir();project=template.read_text(encoding='utf-8-sig')
    if project.count('Value="constant-intent.1"')!=1 or 'CopyMod' in project or 'ModOutputDir' in project:raise RuntimeError('Safe template identity/deployment guard')
    (build/'SolverLab.csproj').write_text(project.replace('Value="constant-intent.1"','Value="'+ID+'"'),encoding='utf-8');shutil.copy2(resource,build/'English.json')
    cache=Path(os.environ['USERPROFILE'])/'.nuget/packages'
    with (out/'build.log').open('w',encoding='utf-8') as log:
        proc=subprocess.run(['dotnet','build',str(build/'SolverLab.csproj'),'-c','Release','--nologo','--ignore-failed-sources','-p:NuGetAudit=false','-p:RestoreSources='+str(cache)],stdout=log,stderr=subprocess.STDOUT,timeout=180)
    if proc.returncode:raise RuntimeError('Ending power dependency compile failed; output retained')
    for r in lock['assemblies']:
        source=build/'.godot/mono/temp/bin/Release/CombatSolver.dll' if r['name']=='CombatSolver' else base/r['file'];shutil.copy2(source,out/r['file'])
    derivative=dict(schema='solver-lab-derivative/v1',id=ID,policy=POLICY,baseLockSha256=sha(base/'solver-lock.json'),baseSolverSha256=sha(base/'CombatSolver.dll'),baseDerivative='constant-intent.1',changedSources=changes,projectSha256=sha(build/'SolverLab.csproj'),resource=dict(sha256=sha(build/'English.json')),toolSha256=sha(Path(__file__)),buildLogSha256=sha(out/'build.log'),boundaryPolicy='PowerCmd.Apply/IsOverOrEnding',scope='preserve Tainted on detached owner death; actual attached source removal/amount transition; fork-local immutable source snapshots')
    (out/'derivative-lock.json').write_text(json.dumps(derivative,indent=2),encoding='utf-8')
    lock.update(producerBuild=ID,producerManifestHash=sha(out/'derivative-lock.json'),source=[dict(path=r['path'],sha256=sha(out/'source'/r['path'])) for r in lock['source']],assemblies=[dict(name=r['name'],file=r['file'],sha256=sha(out/r['file'])) for r in lock['assemblies']],derivative=derivative,scope='tainted-lifecycle.1 derivative of constant-intent.1; native power removal attachment semantics; no scoring/Mod changes')
    (out/'solver-lock.json').write_text(json.dumps(lock,indent=2),encoding='utf-8')
    print(json.dumps(dict(output=str(out),id=ID,policy=POLICY,changedSources=2,solverSha256=sha(out/'CombatSolver.dll'))))
if __name__=='__main__':main()
