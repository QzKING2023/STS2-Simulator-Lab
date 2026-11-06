"""Bounded controls of the actual Amalgamator production reviewer and source audit."""
import argparse,copy,json
from pilot import LAB,read,write_new
from review_repairs import call
p=argparse.ArgumentParser();p.add_argument('--build',required=True);p.add_argument('--directory',required=True);p.add_argument('--output',required=True);a=p.parse_args()
b=LAB/a.build;d=LAB/a.directory;o=LAB/a.output;o.mkdir(exist_ok=False);dll=b/'bin/Release/net9.0/OfflineSearchHarness.dll'
rows=[json.loads(l)['payload'] for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
start=next(r for r in rows if r['kind']=='room-option-start' and r['detail']['facts']['key'].startswith('AMALGAMATOR.'));value=[r for r in rows if r['root']==start['root']]
def row(x,k):return next(r for r in x if r['kind']==k)
def added(x):
 before={c['id']for c in row(x,'room-option-start')['detail']['state']['deck']};return next(c for c in row(x,'room-option-return')['detail']['state']['deck']if c['id']not in before)
mutations=[('wrong-generated-model',lambda x:added(x).update(model='CARD.STRIKE_IRONCLAD')),('wrong-upgrade',lambda x:added(x).update(upgrade=1)),('missing-removal',lambda x:row(x,'choice-returned')['detail']['objects'].pop()),('duplicate-removal',lambda x:row(x,'choice-returned')['detail']['objects'].__setitem__(1,copy.deepcopy(row(x,'choice-returned')['detail']['objects'][0]))),('missing-visual',lambda x:x.remove(row(x,'amalgamator-visual-guard'))),('unfinished',lambda x:row(x,'room-option-return')['detail']['facts'].update(eventFinished=False)),('wrong-selector',lambda x:row(x,'choice-requested')['detail'].update(source='FromHand')),('wrong-order',lambda x:x.reverse())]
cases=[('valid',value,0)]
for name,fn in mutations:
 x=copy.deepcopy(value);fn(x);cases.append((name,x,1))
results=[]
for name,v,e in cases:
 f=o/(name+'.json');write_new(f,v);r=call(dll,['--check-amalgamator-events',f]);results.append(dict(name=name,expected=e,passed=r['exit']==e,**r))
r=call(dll,['--check-amalgamator-source']);source=json.loads(r['stdout'][r['stdout'].find('{'):]);results.append(dict(name='actual-unknown-harmony',expected=0,passed=r['exit']==0 and source['actualHarmonyRejected']==5 and source['businessExecutions']==0,**r))
write_new(o/'summary.json',dict(buildId=read(b/'build-manifest.json')['buildId'],controls=results));print(json.dumps(dict(controls=len(results),passed=all(r['passed']for r in results))))
assert all(r['passed']for r in results)
