"""Finish production reviews without starting any native simulation."""
import sys,json,hashlib,subprocess
from pilot import LAB,read,write_new,sha
from review_repairs import call
from review_study import full
w=LAB/'.build/runs/performance-compat-work01';b=LAB/'.build/native-build/performance-compat-02';dll=b/'bin/Release/net9.0/OfflineSearchHarness.dll'
for label in ['defect-replay','silent-live','silent-replay']:
 if not (w/('review-'+label+'.json')).exists():full(b,w/('final-full-'+label),w/('review-'+label+'.json'))
 else:assert all(read(w/('review-'+label+'.json'))['checks'].values())
r=read(w/'review-visible-replay.json') if (w/'review-visible-replay.json').exists() else call(dll,['--review',w/'visible3-visible-ironclad-replay'])
if not (w/'review-visible-replay.json').exists():write_new(w/'review-visible-replay.json',r)
assert r['exit']==0
for char in ['regent','necrobinder']:
 if (w/(char+'-failure-assessment.json')).exists():
  assert all(read(w/(char+'-failure-assessment.json'))['checks'].values());continue
 p=w/('final-full-'+char+'-live');r=call(dll,['--review-full-run',p]);end=read(p/'result.json')['result'];proc=read(p/'process.json');r['originalResult']=end
 steps=[read(f) for f in p.glob('solver-online-step-*.json')];r['sampleAcceptedAsComplete']=False;r['scope']='Production review checks partial evidence too; exit0 is not a terminal-success assertion'
 r['checks']=dict(originalFailure=end['Stop']==('InvalidAction'if char=='regent'else'DataError'),rawExit=proc['rawExit']==1 and proc['exitConsistent'],notComplete=not end['DataComplete']and end['Stop']!='Completed',productionBehavior=r['exit']==(0 if char=='regent'else 1),exactConfirmedPrefix=all(t['exact']and t['parentUnchanged']for t in steps)if char=='regent'else all(t['exact']and t['parentUnchanged']for t in steps if t['query']<77)and not next(t for t in steps if t['query']==77)['exact'])
 write_new(w/(char+'-failure-assessment.json'),r);assert all(r['checks'].values())
core=LAB/'tests/SimulatorLab.Core.Checks/bin/Release/net9.0/SimulatorLab.Core.Checks.dll'
assert sha(core.parent/'SimulatorLab.Core.dll')==sha(b/'bin/Release/net9.0/SimulatorLab.Core.dll')
r=call(core,[]);r.update(coreAssemblySha256=sha(core.parent/'SimulatorLab.Core.dll'),testAssemblySha256=sha(core),sameCoreAsNativeBuild=True,rebuiltChecksAgainstCurrentCore=True);write_new(w/'core-checks.json',r);assert r['exit']==0
print(json.dumps({'completedReviews':4,'correctlyClassifiedFailureSamples':2,'coreExit':r['exit']}))
