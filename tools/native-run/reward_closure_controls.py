"""Evidence mutations; all decisions about acceptance remain in the production reader."""
import copy,json
def kind(row):return row['payload']['kind']
def detail(row):return row['payload']['detail']
def facts(row):return detail(row).get('facts',{})
def state(row):return detail(row).get('state')
def specifications(out):
    sources={'rc-menu':'closure-reroll-take-1','rc-reroll-start':'closure-reroll-take-1','rc-reroll-return':'closure-reroll-take-1','rc-add-start':'closure-lens-add-1','rc-add-return':'closure-lens-add-1','rc-procure-start':'closure-sozu-full-1','rc-procure-return':'closure-sozu-full-1','rc-quest-start':'closure-spoils-1','rc-quest-return':'closure-spoils-1'}
    result=[]
    def add(name,source,fn,expected=1):result.append((name,source,fn,expected))
    def event_mutation(rows,k,op):
        row=next(r for r in rows if kind(r)==k);i=rows.index(row)
        if op=='remove':rows.remove(row)
        elif op=='duplicate':rows.insert(i,copy.deepcopy(row))
        elif op=='missing-state':detail(row).pop('state')
        elif op=='null-state':detail(row)['state']=None
        elif op=='wrongtype-state':detail(row)['state']=[]
        elif op=='detach':row['payload']['root']='a999'
        elif op=='unknown-state':state(row)['onlyHere']={'nested':[1,2]}
    for k,source in sources.items():
        for op in ['remove','duplicate','missing-state','null-state','wrongtype-state','detach','unknown-state']:add(k+' '+op,source,lambda rows,k=k,op=op:event_mutation(rows,k,op))
    def field(rows,k,path,value):
        x=facts(next(r for r in rows if kind(r)==k))
        for p in path[:-1]:x=x[p]
        x[path[-1]]=copy.deepcopy(value)
    for k in ['rc-menu','rc-reroll-start','rc-reroll-return','rc-add-start','rc-add-return']:
        for f in ['id','reward','generation']:
            value=2 if f=='generation' else {'$ref':'i1'} if f=='reward' else 'old-operation'
            add(k+' wrong '+f,sources[k],lambda rows,k=k,f=f,value=value:field(rows,k,[f],value))
    add('native choice ID wrong','closure-reroll-take-1',lambda rows:field(rows,'rc-menu',['nativeChoiceId'],999))
    add('reroll false CanReroll','closure-reroll-take-1',lambda rows:field(rows,'rc-reroll-return',['CanReroll'],True))
    add('new generation source wrong','closure-reroll-take-1',lambda rows:field(rows,'rc-reroll-return',['cards'],[]))
    add('replacement old instance','closure-lens-add-1',lambda rows:field(rows,'rc-add-return',['actual','reference'],{'$ref':'i1'}))
    add('replacement modifiers missing','closure-lens-add-1',lambda rows:field(rows,'rc-add-return',['modifiers'],[]))
    add('replacement enchant amount','closure-lens-add-1',lambda rows:field(rows,'rc-add-return',['actual','native','enchantment','amount'],99))
    for f,value in [('success',True),('failure','TooFull'),('actual',{'$ref':'i1'}),('reward',{'$ref':'i1'}),('potion',{'$ref':'i1'})]:add('Sozu false '+f,'closure-sozu-full-1',lambda rows,f=f,value=value:field(rows,'rc-procure-return',[f],value))
    for f,value in [('gold',599),('id','old-operation')]:add('quest wrong '+f,'closure-spoils-1',lambda rows,f=f,value=value:field(rows,'rc-quest-return',[f],value))
    add('quest wrong target','closure-spoils-1',lambda rows:field(rows,'rc-quest-start',['coord'],'MapCoord (0, 0)'))
    def coherent(rows,k,change):
        root=next(r['payload']['root'] for r in rows if kind(r)==k)
        kinds={'rc-reroll-start','rc-reroll-return'} if k=='rc-reroll-start' else {'rc-add-start','rc-add-return'}
        for r in rows:
            if r['payload']['root']==root and kind(r) in kinds:
                if change=='gold':state(r)['gold']+=7
                else:state(r)['onlyInPair']={'nested':42}
    for k in ['rc-reroll-start','rc-add-start']:
        for change in ['gold','extra']:add(k+' coherent pair '+change,sources[k],lambda rows,k=k,change=change:coherent(rows,k,change))
    def history(rows,k):
        r=next(r for r in rows if kind(r)==k);state(r)['mapHistory'][-1][-1]['player']['gold_gained']+=1
    for k in ['rc-add-return','rc-reroll-return','rc-quest-return']:add(k+' wrong history',sources[k],lambda rows,k=k:history(rows,k))
    def receipt(rows,k,fieldname,value):
        op=facts(next(r for r in rows if kind(r)==k))['id'];r=next(r for r in rows if kind(r)=='n3c-return' and facts(r)['id']==op);facts(r)['result'][fieldname]=value
    add('Sozu outer acquisition','closure-sozu-full-1',lambda rows:receipt(rows,'rc-procure-return','acquired',{'$ref':'i1'}))
    add('Spoils outer base','closure-spoils-1',lambda rows:receipt(rows,'rc-quest-return','baseGold',43))
    add('Spoils outer quest','closure-spoils-1',lambda rows:receipt(rows,'rc-quest-return','questGold',599))
    def equivalent(rows,extension):
        for r in rows:
            if isinstance(state(r),dict):
                if extension:state(r)['staticReviewExtension']={'nested':[1,2]}
                else:detail(r)['state']=dict(reversed(list(state(r).items())))
    for source in ['closure-reroll-take-1','closure-lens-add-1','closure-sozu-full-1','closure-neow-1','closure-spoils-1']:
        add(source+' property order',source,lambda rows:equivalent(rows,False),0);add(source+' static extension',source,lambda rows:equivalent(rows,True),0)
    def omit_menu(rows,n):
        begins=[r for r in rows if kind(r)=='n3c-open' and facts(r)['kind']=='card-menu'];start=begins[n];id=facts(start)['id'];end=next(r for r in rows if kind(r)=='n3c-return' and facts(r)['id']==id);i=rows.index(start);j=rows.index(end);del rows[i:j+1]
    for source in ['closure-reroll-take-1','closure-reroll-skip-1']:
        for n in [0,1]:add(source+' whole menu '+str(n),source,lambda rows,n=n:omit_menu(rows,n))
    for k in ['rc-menu','rc-reroll-start']:add(k+' false availability','closure-reroll-take-1',lambda rows,k=k:field(rows,k,['CanReroll'],False))
    add('reserved choice wrong root','closure-reroll-take-1',lambda rows:event_mutation(rows,'native-choice-reserved','detach'))
    def menu_result(rows,fieldname,value,remove=False):
        id=facts(next(r for r in rows if kind(r)=='rc-menu'))['id'];row=next(r for r in rows if kind(r)=='n3c-return' and facts(r)['id']==id);value_obj=facts(row)['result']
        if remove:value_obj.pop(fieldname)
        else:value_obj[fieldname]=copy.deepcopy(value)
    for key in ['success','acquired','sourceCards','sourceAlternatives','acquiredCard','identityMode','generation']:add('missing menu result '+key,'closure-reroll-take-1',lambda rows,key=key:menu_result(rows,key,None,True))
    add('wrong menu result source cards','closure-direct-1',lambda rows:menu_result(rows,'sourceCards',[]))
    add('wrong menu result alternatives','closure-direct-1',lambda rows:menu_result(rows,'sourceAlternatives',[]))
    add('wrong full acquired card','closure-direct-1',lambda rows:menu_result(rows,'acquiredCard',None))
    add('wrong acquisition mode','closure-direct-1',lambda rows:menu_result(rows,'identityMode','replacement'))
    add('wrong claimed potion','closure-sozu-full-1',lambda rows:receipt(rows,'rc-procure-return','claimedPotion',None))
    def disabled_reroll(rows):
        menu_rows=[r for r in rows if kind(r)=='n3c-open' and facts(r)['kind']=='card-menu'];id=facts(menu_rows[1])['id'];first=facts(next(r for r in rows if kind(r)=='rc-menu'))
        source=copy.deepcopy(next(a for a in first['alternatives'] if a['option']=='REROLL'));opts=facts(menu_rows[1])['options'];opts.append(dict(Handle='o'+str(len(opts)),Kind='Alternative',Reference=source['reference'],Model='REROLL',Amount=None,Upgrade=None))
        facts(next(r for r in rows if kind(r)=='rc-menu' and facts(r)['id']==id))['alternatives'].append(copy.deepcopy(source));facts(next(r for r in rows if kind(r)=='n3c-return' and facts(r)['id']==id))['result']['sourceAlternatives'].append(copy.deepcopy(source))
    add('disabled reroll cannot become legal option','closure-reroll-take-1',disabled_reroll)
    # Review repairs: results, full parent/child state and independent native instance content.
    def false_sozu_success(rows):
        r=next(x for x in rows if kind(x)=='rc-procure-return');q=facts(r);q['success']=True;q['failure']='None';q['actual']=copy.deepcopy(q['potion'])
        receipt(rows,'rc-procure-return','acquired',copy.deepcopy(q['potion']))
    for source in ['closure-sozu-empty-1','closure-sozu-full-1']:add('R1 Sozu false actual acquisition '+source,source,false_sozu_success)
    def scope_coedit(rows,operation,n,change):
        starts=[x for x in rows if kind(x)=='n3c-open' and facts(x)['kind']==operation];begin=starts[n];id=facts(begin)['id'];i=rows.index(begin)
        if operation=='card-menu':
            # Keep the bogus value through collection completion, then restore at its parent.
            parent=next(x for x in rows if kind(x)=='n3c-open' and facts(x)['id']==facts(begin)['parent'])
            set_id=facts(parent)['parent'];end=next(x for x in rows if kind(x)=='n3c-return' and facts(x)['id']==set_id)
        else:end=next(x for x in rows if kind(x)=='n3c-return' and facts(x)['id']==id)
        for r in rows[i:rows.index(end)+1]:
            if not isinstance(state(r),dict):continue
            if change=='gold':state(r)['gold']+=7
            elif change=='hp':state(r)['hp']-=1
            elif change=='rng':state(r)['runRng']['reviewNested']={'x':[1,2]}
            else:state(r)['reviewNested']={'x':[1,2]}
    for source in ['closure-direct-1','closure-reroll-take-1','closure-reroll-skip-1','closure-lens-options-1','closure-lens-add-1','closure-neow-1']:
        operation='card-add' if 'lens-add' in source else 'card-menu'
        for change in ['gold','hp','rng','extension']:add('R2 child scope '+source+' '+change,source,lambda rows,o=operation,c=change:scope_coedit(rows,o,0,c))
    for change in ['gold','extension']:add('R2 Neow second menu '+change,'closure-neow-1',lambda rows,c=change:scope_coedit(rows,'card-menu',1,c))
    def recurse_refs(x,old,new):
        if isinstance(x,dict):
            if x.get('$ref')==old:x['$ref']=new
            if x.get('id')==old and 'model' in x:x['id']=new
            for v in x.values():recurse_refs(v,old,new)
        elif isinstance(x,list):
            for v in x:recurse_refs(v,old,new)
    def alias_old_card(rows):
        start=next(r for r in rows if kind(r)=='rc-add-start');ret=next(r for r in rows if kind(r)=='rc-add-return');q=facts(ret)['actual'];old=q['reference']['$ref'];existing=next(c['id'] for c in state(start)['deck'] if c['model']==q['model'])
        for r in rows:
            if kind(r)!='identity':recurse_refs(r,old,existing)
    add('R3 actual aliases held same model','closure-lens-add-1',alias_old_card)
    def deck_upgrade(rows):
        ret=next(r for r in rows if kind(r)=='rc-add-return');id=facts(ret)['actual']['reference']['$ref']
        for r in rows[rows.index(ret):]:
            if isinstance(state(r),dict):
                for c in state(r)['deck']:
                    if c['id']==id:c['upgrade']=9
    add('R3 permanent/native upgrade mismatch','closure-lens-add-1',deck_upgrade)
    def type_mismatch(rows,k,fld,new_type):
        id=facts(next(r for r in rows if kind(r)==k))[fld]['reference']['$ref'];row=next(r for r in rows if kind(r)=='identity' and detail(r)['id']==id);detail(row)['type']=new_type
    for source in ['closure-lens-add-1','closure-direct-1','closure-lens-options-1']:
        add('R3 actual type '+source,source,lambda rows:type_mismatch(rows,'rc-add-return','actual','FirePotion'))
        add('R3 selected type '+source,source,lambda rows:type_mismatch(rows,'rc-add-start','selected','FirePotion'))
    def duplicate_deck(rows):
        row=next(r for r in rows if kind(r)=='rc-add-start');state(row)['deck'].append(copy.deepcopy(state(row)['deck'][0]))
    add('R3 duplicate permanent instance','closure-lens-add-1',duplicate_deck)
    def fact_upgrade(rows,k,fld):facts(next(r for r in rows if kind(r)==k))[fld]['upgrade']=9
    for k,fld in [('rc-add-start','selected'),('rc-add-return','actual')]:add('R3 fact native upgrade '+k,'closure-lens-add-1',lambda rows,k=k,fld=fld:fact_upgrade(rows,k,fld))
    return result
