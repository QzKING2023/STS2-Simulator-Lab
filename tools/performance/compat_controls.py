"""Finite production checks for the newly transported hand operations and visible dispatch."""
import argparse,copy,json
from pilot import LAB,read,write_new
from review_repairs import call

def controls(build,directory,output,effect):
 output.mkdir(exist_ok=False)
 rows=[json.loads(l)['payload'] for l in (directory/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 if effect=='Visible':
  step=read(directory/'visible-execution-1.json');probe=read(directory/'visible-boundary-1.json')
  value=dict(rows=[r for r in rows if r['root']==step['root']],step=step,probe=probe,n=1);entry='--check-visible-execution'
  mutations=[('wrong-root',lambda x:x['step'].update(root='a999')),
   ('wrong-context',lambda x:x['step'].update(context='wrong')),
   ('oracle-policy',lambda x:x['step'].update(information='Oracle')),
   ('wrong-card-instance',lambda x:x['step'].update(card='i99999')),
   ('wrong-token',lambda x:x['step'].update(token='end')),
   ('wrong-source-state',lambda x:x['step']['before'].update(gold=999)),
   ('wrong-result-state',lambda x:x['step']['after'].update(hp=999)),
   ('hidden-input',lambda x:x['step'].update(wire=json.dumps({**json.loads(x['step']['wire']),'seed':'hidden'}))),
   ('impure-projection',lambda x:x['probe'].update(sourceUnchanged=False))]
 else:
  steps=[read(p) for p in directory.glob('solver-online-step-*.json')]
  step=sorted((s for s in steps if (s['action'].get('Choice')or{}).get('Effect')==effect),key=lambda s:s['query'])[0]
  root=step['root'];value=dict(rows=[r for r in rows if r['root']==root],step=step,query=read(directory/f"solver-query-{step['query']}.json"),dispatch=next(r for r in rows if r['kind']=='dispatch' and r['root']==root),confirmed=next(r for r in rows if r['kind']=='confirmed' and r['root']==root));entry='--check-hand-discard-evidence'
  def token(x):return x['step']['action']['Choice']['Cards'][0]
  def child(x):return x['step']['children'][0]
  def selected(x):return next(c for c in child(x)['mutation']['after'] if c['id']==child(x)['selected'][0])
  def added(x):
   ids={c['id'] for c in child(x)['mutation']['before']};return next(c for c in child(x)['mutation']['after'] if c['id'] not in ids)
  mutations=[('stale-key',lambda x:token(x).update(StateKey='wrong')),('wrong-occurrence',lambda x:token(x).update(SourceOccurrence=999)),
   ('missing-child',lambda x:x['step'].update(children=[])),('duplicate-child',lambda x:x['step']['children'].append(copy.deepcopy(child(x)))),
   ('wrong-owner',lambda x:child(x).update(root='a999')),('uncompleted-effect',lambda x:child(x)['mutation'].update(complete=False)),
   ('selected-model',lambda x:selected(x).update(model='CARD.STRIKE_IRONCLAD')),('changed-deck',lambda x:x['step']['nativeAfter'].update(deck=[]))]
  if effect=='ApplyRetain':mutations.append(('retain-absent',lambda x:selected(x).update(retain=False)))
  if effect=='MoveToDrawTop':mutations += [('wrong-choice-state',lambda x:child(x)['mutation']['choiceState'].update(gold=99999)),('wrong-draw-top',lambda x:x['step']['nativeAfter']['combat']['draw'].reverse()),('wrong-pile',lambda x:selected(x).update(pile='Hand'))]
  if effect=='Duplicate':mutations += [('not-clone',lambda x:added(x).update(clone=False)),('old-instance',lambda x:added(x).update(id=child(x)['selected'][0]))]
 cases=[('valid',value,0)]
 for name,fn in mutations:
  v=copy.deepcopy(value);fn(v);cases.append((name,v,1))
 results=[]
 for name,v,expected in cases:
  p=output/(name+'.json');write_new(p,v);r=call(build/'bin/Release/net9.0/OfflineSearchHarness.dll',[entry,p]);results.append(dict(name=name,expected=expected,passed=r['exit']==expected,**r))
 write_new(output/'summary.json',dict(effect=effect,buildId=read(build/'build-manifest.json')['buildId'],controls=results))
 print(json.dumps(dict(effect=effect,count=len(results),passed=all(x['passed'] for x in results))))
 if not all(x['passed'] for x in results):raise RuntimeError('Production control mismatch; retained')

if __name__=='__main__':
 a=argparse.ArgumentParser();a.add_argument('--build',required=True);a.add_argument('--directory',required=True);a.add_argument('--output',required=True);a.add_argument('--effect',choices=['Duplicate','ApplyRetain','MoveToDrawTop','Visible'],required=True);x=a.parse_args();out=(LAB/x.output).resolve()
 if not out.is_relative_to(LAB/'.build/runs'):raise ValueError('Lab output required')
 controls((LAB/x.build).resolve(),(LAB/x.directory).resolve(),out,x.effect)
