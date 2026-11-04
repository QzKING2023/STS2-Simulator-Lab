"""Finite event lifecycle evidence mutations, evaluated only by the production reader."""
import copy
k=lambda r:r['payload']['kind']
d=lambda r:r['payload']['detail']
f=lambda r:d(r).get('facts',{})
s=lambda r:d(r).get('state')
def specifications():
 cases=[]
 def add(name,source,fn,expected=1):cases.append((name,source,fn,expected))
 def change(rs,kind,mode):
  r=next(r for r in rs if k(r)==kind);i=rs.index(r)
  if mode=='missing':rs.pop(i)
  elif mode=='duplicate':rs.insert(i,copy.deepcopy(r))
  elif mode=='missing-state':d(r).pop('state')
  elif mode=='null-state':d(r)['state']=None
  elif mode=='nonobject-state':d(r)['state']=[]
  elif mode=='root':r['payload']['root']='a99999'
  elif mode=='context':r['payload']['context']='wrong-room'
  elif mode=='extension':s(r)['independentExtra']={'nested':[3,4]}
 for kind in ['ef-enable','ef-parent','ef-subcombat','ef-battle-complete','ef-proceed-start','ef-room-resume-return','ef-resume-start','ef-resume-return','ef-proceed-return','ef-complete']:
  for mode in ['missing','duplicate','missing-state','null-state','nonobject-state','root','context','extension']:add(kind+' '+mode,'event-combat-1',lambda rs,kind=kind,mode=mode:change(rs,kind,mode))
 def field(rs,kind,key,value):f(next(r for r in rs if k(r)==kind))[key]=copy.deepcopy(value)
 for kind in ['ef-subcombat','ef-proceed-start','ef-room-resume-return','ef-proceed-return','ef-complete']:
  add(kind+' wrong parent','event-combat-1',lambda rs,kind=kind:field(rs,kind,'parent',{'$ref':'i1'}))
 for kind in ['ef-resume-start','ef-resume-return']:
  for key in ['eventModel','exited']:add(kind+' wrong '+key,'event-combat-1',lambda rs,kind=kind,key=key:field(rs,kind,key,{'$ref':'i1'}))
 add('wrong native parent model ID','event-combat-1',lambda rs:field(rs,'ef-subcombat','ParentEventId',{'Category':'EVENT','Entry':'WHISPERING_HOLLOW'}))
 add('wrong complete goal','event-combat-1',lambda rs:field(rs,'ef-complete','goal','pretend full run'))
 add('false timed out outcome','event-combat-1',lambda rs:field(rs,'ef-battle-complete','timedOut',True))
 add('false battle callback count','event-combat-1',lambda rs:field(rs,'ef-battle-complete','victories',999))
 def swap_resume(rs):
  a=next(r for r in rs if k(r)=='ef-resume-start');b=next(r for r in rs if k(r)=='ef-resume-return');i,j=rs.index(a),rs.index(b);rs[i],rs[j]=rs[j],rs[i]
 add('actual resume complete before start','event-combat-1',swap_resume)
 def coedit(rs,begin,end,extension=False):
  a=next(i for i,r in enumerate(rs) if k(r)==begin);b=next(i for i,r in enumerate(rs) if i>=a and k(r)==end)
  for r in rs[a:b+1]:
   if isinstance(s(r),dict):
    if extension:s(r)['runRng']['independentExtra']={'nested':[1,2]}
    else:s(r)['gold']+=7
 for b,e in [('ef-resume-return','ef-proceed-return'),('ef-proceed-return','confirmed'),('room-exit-start','room-exit-return')]:
  for extension in [False,True]:add(b+' coherent range '+str(extension),'event-combat-1',lambda rs,b=b,e=e,x=extension:coedit(rs,b,e,x))
 def explicit_default(rs):
  for r in rs:
   if isinstance(s(r),dict):s(r)['allStaticExtension']={'nested':[1,{'x':2}]}
   if k(r)=='ef-enable':f(r)['before']['allStaticExtension']={'nested':[1,{'x':2}]}
 for source in ['event-natural-1','event-combat-1']:add('all boundary extension '+source,source,explicit_default,0)
 def reorder(rs):
  def go(x):
   if isinstance(x,dict):
    it=list(x.items())[::-1];x.clear();x.update(it)
    for v in x.values():go(v)
   elif isinstance(x,list):
    for v in x:go(v)
  for r in rs:go(r)
 for source in ['event-natural-1','event-combat-1']:add('property order '+source,source,reorder,0)
 def false_support(rs,model):
  row=next(r for r in rs if k(r)=='room-candidates' and any(model in o['key'] for o in f(r)['options']));f(row)['options'][1]['supported']=True
 for model,source in [('WHISPERING_HOLLOW','event-natural-1'),('BATTLEWORN_DUMMY','event-combat-1')]:add('undeclared branch support '+model,source,lambda rs,model=model:false_support(rs,model))
 return cases+repair_specifications()+state_binding_specifications()

def repair_specifications():
 """4D2 review repairs: whole operations, not only one altered numeric field."""
 cases=[]
 def add(name,fn,expected=1):cases.append(('repair '+name,'event-combat-1',fn,expected))
 def one(rs,kind,root=None):return next(r for r in rs if k(r)==kind and(root is None or r['payload']['root']==root))
 def root_of(rs,kind):return next(r['payload']['root'] for r in rs if k(r)=='dispatch' and f(r)['kind']==kind)
 def reward_set(rs):
  root=root_of(rs,'EventRewards');row=next(r for r in rs if k(r)=='n3c-open' and r['payload']['root']==root and f(r)['kind']=='reward-set');return root,f(row)['id']
 def move(rs,row,before):rs.remove(row);rs.insert(rs.index(before),row)
 def early_resume(rs,target):
  root=root_of(rs,'EventProceed');row=one(rs,'ef-resume-return');before=next(r for r in rs if k(r)==target and r['payload']['root']==root and f(r).get('id')!=next(f(x)['id'] for x in rs if k(x)=='n3c-open' and x['payload']['root']==root))
  if target=='n3c-return':before=next(r for r in rs if k(r)==target and r['payload']['root']==root and f(r).get('result',{}).get('consumed') is None and f(r).get('result',{}).get('acquired') is not None)
  move(rs,row,before)
 for target in ['n3c-return','n3c-set-completed']:add('actual completion before '+target,lambda rs,t=target:early_resume(rs,t))
 def outer(rs,before_start):
  row=one(rs,'ef-room-resume-return');anchor=one(rs,'ef-resume-start');d(row)['state']=copy.deepcopy(s(anchor));rs.remove(row);rs.insert(rs.index(anchor)+(0 if before_start else 1),row)
 add('outer return before actual start',lambda rs:outer(rs,True))
 # A persisted scheduling-equivalent control; not evidence of a new native delayed run.
 add('legal outer return while actual task pending',lambda rs:outer(rs,False),0)
 def decision(rs,field):
  root=next(r['payload']['root'] for r in rs if k(r)=='dispatch' and f(r)['kind']=='RoomOption' and 'SETTING_1' in f(r)['decision']['key'])
  for r in rs:
   if r['payload']['root']==root and k(r) in ['root-request','dispatch','confirmed']:
    a=d(r)['action'] if k(r)=='root-request' else f(r) if k(r)=='dispatch' else f(r)['action'];v=a['decision']
    if field in ['session','request','key']:v[field]='review-wrong-'+field
    elif field=='index':v[field]=1
    else:v[field]={'$ref':'i1'}
 for field in ['session','request','index','key','option']:add('root choice '+field,lambda rs,t=field:decision(rs,t))
 for kind in ['room-option-start','room-option-return','room-option-boundary']:
  for field in ['root','context','request','session']:
   def edit(rs,kind=kind,field=field):
    root=next(r['payload']['root'] for r in rs if k(r)=='dispatch' and f(r)['kind']=='RoomOption' and 'SETTING_1' in f(r)['decision']['key']);row=one(rs,kind,root)
    (row['payload'] if field in ['root','context'] else f(row))[field]='review-wrong'
   add(kind+' '+field,edit)
 def false_finished(rs):
  root=next(r['payload']['root'] for r in rs if k(r)=='dispatch' and f(r)['kind']=='RoomOption' and 'WHISPERING_HOLLOW' in f(r)['decision']['key'])
  for kind in ['room-option-return','room-option-boundary']:f(one(rs,kind,root))['eventFinished']=False
 add('both false completion facts',false_finished)
 def cleanup(rs,mode):
  root=one(rs,'ef-battle-complete')['payload']['root'];row=one(rs,'battle-victory-cleanup',root)
  if mode=='missing':rs.remove(row)
  elif mode=='duplicate':rs.insert(rs.index(row),copy.deepcopy(row))
  elif mode=='early':move(rs,row,one(rs,'confirmed',root))
  elif mode in ['root','context']:row['payload'][mode]='review-wrong'
  elif mode=='gold':s(row)['gold']+=9
  elif mode=='rng':s(row)['runRng']['rngs']['Niche']['s0']^=1
  else:s(row)['reviewExtra']={'nested':[3,4]}
 for mode in ['missing','duplicate','early','root','context','gold','rng','extension']:add('battle cleanup '+mode,lambda rs,t=mode:cleanup(rs,t))
 def victory_owner(rs):
  root=one(rs,'ef-battle-complete')['payload']['root'];one(rs,'battle-victory-before-cleanup',root)['payload'].update(root='a1',context='wrong')
 add('victory callback ownership',victory_owner)
 def arrival(rs,mode):
  root=one(rs,'ef-complete')['payload']['root'];row=one(rs,'room-entered',root)
  if mode=='missing':rs.remove(row)
  elif mode=='duplicate':
   another=copy.deepcopy(row);f(another)['session']='review-different-session';rs.insert(rs.index(row)+1,another)
  elif mode=='early':move(rs,row,one(rs,'confirmed',root))
  elif mode in ['root','context']:row['payload'][mode]='review-wrong'
  else:s(row)['reviewExtra']={'nested':[1,2]}
 for mode in ['missing','duplicate','early','root','context','extension']:add('arrival '+mode,lambda rs,t=mode:arrival(rs,t))
 def rewards(rs,mode):
  root,id=reward_set(rs);rows=[r for r in rs if k(r).startswith('n3c-') and f(r).get('id')==id]
  if mode=='missing':rs[:]=[r for r in rs if r not in rows]
  elif mode=='duplicate':
   copies=copy.deepcopy(rows)
   for r in copies:
    f(r)['id']='review-second-set'
    if 'set' in f(r):f(r)['set']=9999
   rs[rs.index(rows[-1])+1:rs.index(rows[-1])+1]=copies
  else:
   for r in rs:
    if r['payload']['root']==root and(k(r) in ['confirmed','n3c-boundary'] or k(r)=='n3c-return' and f(r).get('id')!=id):
     if mode=='gold':s(r)['gold']+=9
     elif mode=='rng':s(r)['runRng']['rngs']['Niche']['s0']^=1
     else:s(r)['reviewExtra']={'nested':[1,2]}
 for mode in ['missing','duplicate','gold','rng','extension']:add('EventRewards whole child '+mode,lambda rs,t=mode:rewards(rs,t))
 for field in ['parent','child']:
  def root_identity(rs,field=field):
   root=root_of(rs,'EventProceed')
   for r in rs:
    if r['payload']['root']==root and k(r) in ['root-request','dispatch','confirmed']:
     a=d(r)['action'] if k(r)=='root-request' else f(r) if k(r)=='dispatch' else f(r)['action'];a['decision'][field]={'$ref':'i1'}
  add('Proceed root '+field,root_identity)
 for field in ['root','context']:
  def exit_owner(rs,field=field):one(rs,'room-exit-return',one(rs,'ef-complete')['payload']['root'])['payload'][field]='review-wrong'
  add('exit return '+field,exit_owner)
 return cases


def state_binding_specifications():
 """Persisted scheduling and parent/child frame controls, not new native runs."""
 cases=[]
 def add(name,fn,expected=1):cases.append(('state binding '+name,'event-combat-1',fn,expected))
 def one(rs,kind,id=None):return next(x for x in rs if k(x)==kind and(id is None or f(x).get('id')==id))
 def operation(rs,root_kind,child_kind):
  root=next(x['payload']['root'] for x in rs if k(x)=='dispatch' and f(x)['kind']==root_kind)
  return root,next(f(x)['id'] for x in rs if k(x)=='n3c-open' and x['payload']['root']==root and f(x)['kind']==child_kind)
 def edit(st,mode):
  if mode=='gold':st['gold']+=9
  elif mode=='hp':st['hp']-=1
  elif mode=='run-rng':st['runRng']['rngs']['Niche']['s0']^=1
  elif mode=='reward-rng':st['playerRng']['rngs']['Rewards']['s1']^=1
  elif mode=='counter':st['playerRng']['rngs']['Rewards']['counter']+=1
  elif mode=='event-rng':st['lifecycle']['eventModel']['rng']['s2']^=1
  else:st['extraFrameField']={'nested':[1,{'changed':True}]}
 modes=['gold','hp','run-rng','reward-rng','counter','event-rng','extra']
 def pending(rs,stage,mode=None):
  root,collection=operation(rs,'EventProceed','reward-set');_,item=operation(rs,'EventProceed','reward-item')
  anchor=one(rs,'ef-resume-start') if stage=='resume-start' else one(rs,stage.split(':')[0],collection if stage.endswith(':set') else item)
  outer=one(rs,'ef-room-resume-return');rs.remove(outer);d(outer)['state']=copy.deepcopy(s(anchor))
  if mode:edit(s(outer),mode)
  rs.insert(rs.index(anchor)+1,outer)
 for stage in ['resume-start','n3c-open:set','n3c-start:item','n3c-return:item','n3c-return:set']:
  add('legal outer pending at '+stage,lambda rs,stage=stage:pending(rs,stage),0)
  for mode in modes:add('outer pending '+stage+' '+mode,lambda rs,stage=stage,mode=mode:pending(rs,stage,mode))
 def interval(rs,root_kind,mode,from_item=False):
  root,id=operation(rs,root_kind,'reward-item' if from_item else 'reward-set')
  begin=next(i for i,x in enumerate(rs) if k(x)==('n3c-return' if from_item else 'n3c-open') and f(x).get('id')==id)
  end=next(i for i,x in enumerate(rs) if i>=begin and k(x)==('room-leave-ready' if root_kind=='EventProceed' else 'n3c-boundary') and x['payload']['root']==root)
  for x in rs[begin:end+1]:
   if isinstance(s(x),dict):edit(s(x),mode)
 for root in ['EventRewards','EventProceed']:
  for mode in modes:
   # CombatRoom has no event-local RNG, so only Resume has that source.
   if root=='EventRewards' and mode=='event-rng':continue
   add(root+' entire child+parent '+mode,lambda rs,root=root,mode=mode:interval(rs,root,mode))
 for mode in modes:add('Resume item return through parent '+mode,lambda rs,mode=mode:interval(rs,'EventProceed',mode,True))
 def wrong_generated_status(rs):
  root,id=operation(rs,'EventProceed','reward-set');begin=rs.index(one(rs,'n3c-open',id))
  for x in rs[begin:]:
   if x['payload']['root']==root and isinstance(s(x),dict):s(x)['lifecycle']['eventModel']['IsFinished']=False
 add('generation falsely preserves unfinished event',wrong_generated_status)
 return cases
