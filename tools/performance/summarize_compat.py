"""Read-only synthesis for the bounded compatibility batch; never rewrite artifacts."""
import json
from pilot import LAB,read,write_new
from summarize_repairs import summarize,compare
w=LAB/'.build/runs/performance-compat-work01'
launches=[json.loads(x) for x in (w/'launches.jsonl').read_text().splitlines()]
measurements=[]
for l in launches:
 p=w/l['name']
 if (p/'process.json').exists():
  m=summarize(p);qs=[read(q) for q in p.glob('solver-query-*.json')]
  m['parallelActual']=dict(requestedDop=sorted(set(q['budget']['dop'] for q in qs)),maxObserved=max([q['work']['MaxParallelExpansionConcurrency'] for q in qs]or[0]),queriesWithConcurrencyAbove1=sum(q['work']['MaxParallelExpansionConcurrency']>1 for q in qs),waves=sum(q['work']['ParallelExpansionWaves'] for q in qs),workItems=sum(q['work']['ParallelExpansionWorkItems'] for q in qs))
  measurements.append(m)
 else:measurements.append(dict(directory=str(p.relative_to(LAB)),nativeNotStarted=True,wrapper=read(w/(l['name']+'-completion.json'))))
pairs=[('final-full-defect-live','final-full-defect-replay'),('final-full-silent-live','final-full-silent-replay'),('visible2-visible-ironclad-live','visible3-visible-ironclad-replay')]
replays=[]
for a,b in pairs:
 if not (w/b/'result.json').exists():continue
 r=compare(w/a,w/b)
 if a.startswith('visible'):
  files=[*sorted((w/a).glob('visible-execution-*.json')),*sorted((w/a).glob('visible-boundary-*.json')),w/a/'visible-complete.json']
  r['visibleFiles']=len(files);r['visibleMismatches']=[f.name for f in files if read(f)!=read(w/b/f.name)]
 replays.append(r)
write_new(w/'measurements.json',dict(schema='performance-compat-measurements/v1',measurements=measurements,replays=replays))
print(json.dumps(dict(samples=len(measurements),replays=replays)))
if any(r['mismatches'] or r.get('visibleMismatches') for r in replays):raise RuntimeError('Replay mismatch retained')
