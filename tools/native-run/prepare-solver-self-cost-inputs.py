"""Fixed Lab derivative of MIT CombatSolver. Reuse branch attachment and power state; preserve parent pins and attribution."""
from pathlib import Path
import argparse
import hashlib
import json
import os
import shutil
import subprocess

LAB=Path(__file__).resolve().parents[2]
ID='self-cost-order.1'
POLICY='upstream-explicit-conditions/no-root-fallback/v1'
def sha(p):return hashlib.sha256(p.read_bytes()).hexdigest().upper()
def read(p):return json.loads(p.read_text(encoding='utf-8-sig'))
def main():
    ap=argparse.ArgumentParser(description=__doc__)
    ap.add_argument('--base',type=Path,required=True);ap.add_argument('--output',type=Path,required=True)
    a=ap.parse_args();base=a.base.resolve();out=a.output.resolve();lock=read(base/'solver-lock.json');parent=read(base/'derivative-lock.json')
    if not out.is_relative_to(LAB/'.build') or out.exists():raise RuntimeError('Fresh Lab .build output required')
    if lock['version']!='0.50.1' or parent['id']!='turn-history-side.1' or len(lock['source'])!=826:raise RuntimeError('Reviewed terminal-hooks base required')
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
    def edit(path, old, new):
        p=out/'source'/path;before=sha(p);text=p.read_text(encoding='utf-8-sig')
        if text.count(old)!=1:raise RuntimeError('Exact upstream source anchor changed: '+path)
        p.write_text(text.replace(old,new),encoding='utf-8',newline='\n');changes.append(dict(path=path,before=before,after=sha(p)))
    edit('src/Prediction/CardEffectSpecRegistry.cs',
        """            case BloodWall or Breakthrough or Hemokinesis:
                simulator.Damage(
                    card.Owner.Creature,
                    card.DynamicVars.HpLoss.IntValue,
                    ValueProp.Unblockable | ValueProp.Unpowered | ValueProp.Move,
                    card.Owner.Creature);
                applied = true;""",
        """            case BloodWall or Breakthrough or Hemokinesis:
                // Paid before the primary effect by the OnPlay facade.
                applied = true;""")
    edit('src/Engine/InCombat/Mirrors/Cards/OnPlay/CardOnPlayMirrors.cs',
        """        MirrorDispatchResult result;
        using (simulator.BeginExecutionDispatch())""",
        """        // These sealed native OnPlay methods await their HP cost before
        // invoking attack/block. Reuse existing damage hooks and continuations.
        if (card.MutablePreview is BloodWall or Breakthrough or Hemokinesis)
        {
            using (simulator.BeginExecutionDispatch())
                simulator.Damage(card.MutablePreview.Owner.Creature, card.MutablePreview.DynamicVars.HpLoss.IntValue,
                    MegaCrit.Sts2.Core.ValueProps.ValueProp.Unblockable |
                    MegaCrit.Sts2.Core.ValueProps.ValueProp.Unpowered |
                    MegaCrit.Sts2.Core.ValueProps.ValueProp.Move, card.MutablePreview.Owner.Creature);
            if (simulator.HasPendingChoice)
            {
                simulator.AppendExecutionContinuation(new CardPrimaryAfterCostFrame(card, cardPlay));
                return new(MirrorDispatchKind.Handled);
            }
        }
        return InvokePrimaryAfterCost(simulator, card, cardPlay);
    }

    private static MirrorDispatchResult InvokePrimaryAfterCost(
        CombatPredictionSimulator simulator, PredictedCard card, CardPlay cardPlay)
    {
        MirrorDispatchResult result;
        using (simulator.BeginExecutionDispatch())""")
    edit('src/Engine/InCombat/Mirrors/Cards/OnPlay/CardOnPlayMirrors.ExecutionContinuation.cs',
        '    private sealed record CardSpecExecutionFrame',
        """    private sealed record CardPrimaryAfterCostFrame(PredictedCard Card, CardPlay Play) : ICombatPredictionExecutionFrame
    {
        public IEnumerable<CardPlay> ActiveCardPlays => [Play];
        public void PrepareFork(PredictionForkContext context) => CombatPredictionSimulator.PrepareExecutionCardPlay(Card, Play, context);
        public ICombatPredictionExecutionFrame Fork(PredictionForkContext context)
            => this with { Card = context.RequireRemap(Card), Play = context.RequireRemap(Play) };
        public bool Resume(CombatPredictionSimulator simulator)
        {
            InvokePrimaryAfterCost(simulator, Card, Play);
            return !simulator.HasPendingChoice;
        }
    }

    private sealed record CardSpecExecutionFrame""")
    build=out/'build';build.mkdir();project=template.read_text(encoding='utf-8-sig')
    if project.count('Value="turn-history-side.1"')!=1 or 'CopyMod' in project or 'ModOutputDir' in project:raise RuntimeError('Safe template identity/deployment guard')
    (build/'SolverLab.csproj').write_text(project.replace('Value="turn-history-side.1"','Value="'+ID+'"'),encoding='utf-8');shutil.copy2(resource,build/'English.json')
    cache=Path(os.environ['USERPROFILE'])/'.nuget/packages'
    with (out/'build.log').open('w',encoding='utf-8') as log:
        proc=subprocess.run(['dotnet','build',str(build/'SolverLab.csproj'),'-c','Release','--nologo','--ignore-failed-sources','-p:NuGetAudit=false','-p:RestoreSources='+str(cache)],stdout=log,stderr=subprocess.STDOUT,timeout=180)
    if proc.returncode:raise RuntimeError('Ending power dependency compile failed; output retained')
    for r in lock['assemblies']:
        source=build/'.godot/mono/temp/bin/Release/CombatSolver.dll' if r['name']=='CombatSolver' else base/r['file'];shutil.copy2(source,out/r['file'])
    derivative=dict(schema='solver-lab-derivative/v1',id=ID,policy=POLICY,baseLockSha256=sha(base/'solver-lock.json'),baseSolverSha256=sha(base/'CombatSolver.dll'),baseDerivative='turn-history-side.1',changedSources=changes,projectSha256=sha(build/'SolverLab.csproj'),resource=dict(sha256=sha(build/'English.json')),toolSha256=sha(Path(__file__)),buildLogSha256=sha(out/'build.log'),boundaryPolicy='PowerCmd.Apply/IsOverOrEnding',scope='existing sealed HP-cost card group pays before primary mirror; reuse damage hooks, forkable continuations and existing terminal guards; no scoring changes')
    (out/'derivative-lock.json').write_text(json.dumps(derivative,indent=2),encoding='utf-8')
    lock.update(producerBuild=ID,producerManifestHash=sha(out/'derivative-lock.json'),source=[dict(path=r['path'],sha256=sha(out/'source'/r['path'])) for r in lock['source']],assemblies=[dict(name=r['name'],file=r['file'],sha256=sha(out/r['file'])) for r in lock['assemblies']],derivative=derivative,scope='self-cost-order.1 derivative of turn-history-side.1; before-primary cost phase; no scoring/Mod changes')
    (out/'solver-lock.json').write_text(json.dumps(lock,indent=2),encoding='utf-8')
    print(json.dumps(dict(output=str(out),id=ID,policy=POLICY,changedSources=len(changes),solverSha256=sha(out/'CombatSolver.dll'))))
if __name__=='__main__':main()
