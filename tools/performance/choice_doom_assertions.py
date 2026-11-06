"""Bounded mechanism assertions over retained native evidence, never effect synthesis."""
import json,re
from pilot import LAB,read,write_new

w=LAB/'.build/runs/performance-choice-doom-work01'
results=[]
def check(name,value):
 results.append(dict(name=name,passed=bool(value)))

for char in ['regent','necrobinder']:
 d=w/f'final-full-{char}-live'; p=read(d/'process.json'); r=read(d/'result.json')['result']
 check(char+':same-original-seed',p['seed']=='LAB_V0111_PERF_PILOT_01')
 check(char+':terminal-and-complete',r['Stop']=='Completed' and r['Terminal'] in ['Death','RunVictory'] and r['Confirmed']==r['Dispatched'] and r['DataComplete'])

reg=read(w/'final-full-regent-live/solver-online-step-201.json')
check('PhotonCut:actual-card',reg['action']['CardId']=='PHOTON_CUT')
check('PhotonCut:actual-hand-topdeck',reg['action']['Choice']['Effect']=='MoveToDrawTop' and reg['action']['Choice']['SourcePile']=='Hand')
check('PhotonCut:entire-continuation-and-parent',reg['expected']==reg['actual'] and reg['exact'] and reg['parentUnchanged'])

necro=w/'final-full-necrobinder-live'
doom=read(necro/'solver-online-step-77.json')
check('Doom:terminal-Vulnerable-2',bool(re.search(r'0:VULNERABLE_POWER=2/0',doom['actual'])))
check('Doom:entire-terminal-continuation-and-parent',doom['expected']==doom['actual'] and doom['exact'] and doom['parentUnchanged'])
check('Doom:post-cleanup-powers-distinct-from-terminal',doom['nativeAfter']['powers']==[])

bone=read(necro/'solver-online-step-146.json')
old=read(w/'fixed-full-necrobinder-live/solver-online-step-146.json')
check('AfterAttack:original-failure-preserved',old['action']['CardId']=='BONE_SHARDS' and not old['exact'] and old['actual']!=old['expected'])
check('AfterAttack:same-native-input-action',old['nativeBefore']==bone['nativeBefore'] and old['action']==bone['action'])
check('AfterAttack:same-actual-terminal-result',old['actual']==bone['actual'])
check('AfterAttack:Flatten-cost-retained',bool(re.search(r'FLATTEN\+0/False:2/',bone['actual'])))
check('AfterAttack:entire-continuation-and-parent',bone['expected']==bone['actual'] and bone['exact'] and bone['parentUnchanged'])

field=[]
for char in ['regent','necrobinder']:
 d=w/f'final-full-{char}-live'
 rows=[json.loads(l)['payload'] for l in (d/'trace.jsonl').read_text(encoding='utf-8-sig').splitlines()]
 starts=[r for r in rows if r['kind']=='room-option-start' and r['detail']['facts']['key'].startswith('FIELD_OF_MAN_SIZED_HOLES.')]
 check(char+':native-field-event',len(starts)==1)
 start=starts[0]; own=[r for r in rows if r['root']==start['root']]
 ret=next(r for r in own if r['kind']=='room-option-return')
 check(char+':field-completed',ret['detail']['facts']['eventFinished'])
 check(char+':field-not-synthetic-deck',start['detail']['state']['deck']==ret['detail']['state']['deck'])
 # The summary deliberately omits native enchantment serialization. Validate
 # the subsequent original capture's existing upstream enchantment binding.
 captured=[]
 for f in d.glob('solver-query-*.json'):
  q=read(f)
  for c in q.get('binding',{}).get('hand',[]):
   if (c.get('enchantment')or{}).get('id')=='PERFECT_FIT': captured.append(dict(query=q.get('query'),card=c))
 check(char+':PerfectFit-actual-native-capture',bool(captured))
 field.append(dict(character=char,root=start['root'],option=start['detail']['facts']['key'],captured=captured[:2],scope='original effect and subsequent native card capture; summary deck omits enchantment metadata'))

write_new(w/'mechanism-assertions.json',dict(checks=results,field=field,scope='actual original-seed execution; not exhaustive hook proof or all-event semantics'))
print(json.dumps(dict(checks=len(results),failed=[x['name']for x in results if not x['passed']])))
assert all(x['passed']for x in results)
