"""Render user-starred skills and event cues, with explicit local Unity deployment."""
import argparse
import copy
import json
import uuid
from pathlib import Path
import numpy as np
import render_basic_attack_audio as m


def main():
    parser=argparse.ArgumentParser();parser.add_argument('--apply-local',action='store_true');args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source'
    p=m.load(source/'recipes.json');selection=m.load(source/'selections/user-skill-selections-revision12.json')
    actions={a['id']:a for a in selection['actions']};audio=p['audio'];groups=selection['sharedGroups']
    out=m.RES/'audio/gamemaster-skills-selected';runtime=out/'runtime';runtime.mkdir(parents=True,exist_ok=True)
    (out/'source').mkdir(exist_ok=True)
    catalog=[];outputs=[];resolved=[];cache={}
    def samples(key):
        if key not in cache:
            info=audio[key];raw=(source/'audio'/info['file']).read_bytes();assert m.sha(raw)==info['sha256'],key
            cache[key]=np.asarray(m.decode(raw))
        return cache[key]
    smoke=source/'smoke-release-14/smoke14-07-smoother.wav';smokeraw=smoke.read_bytes()
    smoke_key='approved/smoke';cache[smoke_key]=np.asarray(m.decode(smokeraw))
    audio[smoke_key]=dict(file='../smoke-release-14/'+smoke.name,sha256=m.sha(smokeraw),peakTime=0,duration=len(cache[smoke_key])/m.RATE)
    def render(events):
        result=np.zeros(max(1,max((round((e['time']+e['duration'])*m.RATE) for e in events),default=1)))
        absolute=np.zeros(len(result))
        for e in events:
            x=samples(e['audio']);rate=e.get('rate',1)
            if rate!=1:x=np.interp(np.arange(0,len(x)-1,rate),np.arange(len(x)),x)
            if e.get('loop'):
                n=round(e['duration']*m.RATE);x=np.resize(x,n)
                fade=min(round(e.get('fadeSeconds',.035)*m.RATE),n//4)
                x[:fade]*=np.linspace(0,1,fade);x[-fade:]*=np.linspace(1,0,fade)
            x=x*e['gain'];at=round(e['time']*m.RATE)
            if at<0:x=x[-at:];at=0
            n=min(len(x),len(result)-at)
            if n>0:result[at:at+n]+=x[:n];absolute[at:at+n]+=abs(x[:n])
        safe=min(1,.88/max(.001,float(max(absolute))))
        result*=safe
        return m.wav_bytes(result),safe
    def emit(id,weapon,events,number=1,impact=0,duration=0,verbatim=None):
        raw,safe=(verbatim,1) if verbatim is not None else render(events)
        filename=id+f'_{number:02}.wav';(runtime/filename).write_bytes(raw)
        n=len(m.decode(raw));entry=next((c for c in catalog if c['id']==id),None)
        resource='SelectedSkillAudio/'+filename[:-4]
        if entry is None:
            entry=dict(id=id,clip=resource,variants=[],weapon=weapon,basic=False,samples=n,frequency=m.RATE,
                nominalDuration=duration,nominalImpact=impact,silent=not events and verbatim is None)
            catalog.append(entry)
        entry['variants'].append(resource)
        outputs.append(dict(id=id,number=number,file=filename,sha256=m.sha(raw),bytes=len(raw),samples=n,events=events,safeGain=safe))
    def event(layer,ed,at):
        key=layer['audio'];rate=layer.get('rate',1);clip=audio[key]
        return dict(audio=key,sourceSha256=clip['sha256'],time=at+ed.get('offsetMs',0)/1000-(0 if layer.get('align')=='onset' else clip['peakTime']/rate),
            gain=layer.get('gain',1)*ed.get('volume',100)/100,rate=rate,duration=clip['duration']/rate)
    for aid,a in actions.items():
        if a.get('isBasicReview'):continue
        confirmations=a.get('confirmedCandidates',[])
        numbers=sorted(set(a.get('favorites',[])+[c['number'] for c in confirmations]))
        if aid in ('dagger_range','base_sword_follow_up','whip_follow_up'):numbers=[1]
        assert numbers,aid
        for number in numbers:
            c=next((c for c in confirmations if c['number']==number),{})
            revision=c.get('candidateRevision',a.get('candidateRevision',1) or 1)
            rec=copy.deepcopy(a['candidates'][number-1]);layers=copy.deepcopy(c.get('layers',rec['layers']))
            recipeid=c.get('recipeId',f'{aid}-v{revision}-{number:02}')
            edits=copy.deepcopy(c.get('settings') or c.get('legacySettingsByRecipe',{}).get(recipeid,{}) or a.get('settings',{}).get(rec['id'],{}))
            if aid=='base_sword_follow_up':layers=copy.deepcopy(a['resolvedLink']['layers']);edits=copy.deepcopy(a['resolvedLink']['settings'])
            if aid=='dagger_range':layers=[dict(id='smoke',audio=smoke_key,times=[0],gain=1,anchor='activation',align='onset')];edits={}
            # The star explicitly refers to revision 3; do not substitute the
            # later unapproved revision-5 shotgun found in the current list.
            if aid=='launcher_move_special' and revision==3:
                layers=[dict(id='shotgun',audio='r3/75bbfe1bef12bf384e7e',times=[.15],gain=.95,anchor='absolute',align='peak')]
            attack=[];eventsets={}
            for l in layers:
                group=l.get('group')
                if group:
                    g=groups[group];v=g['variants'][0 if g.get('fixed') else selection['sharedChoices'].get(group,0)]
                    l.update(audio=v['audio'],gain=v['gain'])
                if aid in ('bomb_range_special','bomb_followup_special') and l['id']=='throw':l['gain']=1.9
                ed=edits.get(l['id'],dict(offsetMs=0,volume=100))
                trigger=None
                if aid=='shield_default':trigger='parry'
                elif aid=='shield_follow_up':trigger='placed'
                elif aid=='dagger_range':trigger='activated'
                elif l['id']=='explosion':trigger='explosion'
                elif l['id']=='teleport':trigger='teleport'
                elif aid=='shuriken_move_special' and l['id'] in ('hit','return'):trigger=l['id']
                if trigger:
                    # Explosion timings and return delays in HTML are illustrative.
                    # Real events own their time; no future collision is baked in.
                    times=l['times'] if trigger=='placed' else [0]
                    for at in times:eventsets.setdefault(trigger,[]).append(event(l,ed,at))
                    continue
                anchor=a.get('scenarioDefinition',{});sustain=anchor.get('sustain',{}).get('default',2)
                when=a.get('impact',0) # export omits impact; resolve authoritative audition metadata below
                actual=next(v for v in p['actions'] if v['id']==aid);when=actual['impact']
                origin=when if l.get('anchor') in ('activation','release') else 0
                times=np.arange(0,sustain,l['repeatEvery']).tolist() if l.get('repeatEvery') else l['times']
                for at in times:
                    e=event(l,ed,origin+at)
                    if l.get('loop'):e.update(loop=True,duration=sustain,fadeSeconds=l.get('fadeSeconds',.035))
                    attack.append(e)
            actual=next(v for v in p['actions'] if v['id']==aid)
            emit(aid,a['weapon'],attack,number,actual['impact'],actual['duration'])
            for trigger,events in eventsets.items():
                emit(aid+'__'+trigger,a['weapon'],events,number,
                    verbatim=smokeraw if aid=='dagger_range' and trigger=='activated' else None)
            resolved.append(dict(id=aid,number=number,approvedRevision=revision,layers=layers,settings=edits,
                eventTriggers=list(eventsets),override='latest explicit common sound / louder throw / smoke directions take precedence'))
    # Existing explosion entry points retain their ids and actual collision time.
    for id,key,weapon,gain in [('explosion_bomb','approved/bomb-B3','bomb',.8),('explosion_large','r11/380a9a7cdb2eb0736ec9','bomb',.8),
        ('explosion_grenade','r3/1143dfcb80b098da443e','launcher',.8)]:
        emit(id,weapon,[event(dict(audio=key,gain=gain,align='onset'),{},0)])
    # The approved grenade explosion already contains the successive bursts.
    # Do not add the old crackling fragment recording on top of that cluster.
    emit('explosion_fragment','launcher',[])
    manifest=dict(format='selected-skill-audio-v1',selectionSha256=m.sha((source/'selections/user-skill-selections-revision12.json').read_bytes()),
        smokeSha256=m.sha(smokeraw),variantPolicy='round-robin approved stars per action',resolved=resolved,outputs=outputs)
    m.dump(out/'source/render.json',manifest)
    m.register_asset(out,'audio.gamemaster-skills-selected','user-confirmed skill render; local deployment' if args.apply_local else 'user-confirmed skill render')
    m.dump(m.MAIN/'data/audio/skill-selections.json',manifest)
    if args.apply_local:
        dest=m.MAIN/'unity/Assets/Deviant/Audio/Resources/SelectedSkillAudio';dest.mkdir(exist_ok=True)
        contractpath=m.MAIN/'docs/assets/manifests/unity-deployment.json';contract=m.load(contractpath);bytarget={f['target']:f for f in contract['files']}
        template=(m.MAIN/'unity/Assets/Deviant/Audio/SFX/Weapons/sfx_weapon_bomb_range_special_01_v01.wav.meta').read_text(encoding='utf8')
        for output in outputs:
            target=dest/output['file'];target.write_bytes((runtime/output['file']).read_bytes())
            meta=Path(str(target)+'.meta')
            if not meta.exists():
                import re
                meta.write_text(re.sub(r'guid: [0-9a-f]+','guid: '+uuid.uuid5(uuid.NAMESPACE_URL,'deviant-selected-skills/'+output['file']).hex,template),encoding='utf8')
            rel=target.relative_to(m.MAIN).as_posix();record=bytarget.get(rel)
            if record is None:record=dict(target=rel);contract['files'].append(record)
            record.update(bytes=output['bytes'],sha256=output['sha256'],source=dict(asset='audio.gamemaster-skills-selected',path='audio/gamemaster-skills-selected/runtime/'+output['file']))
        m.dump(dest/'Catalog.json',dict(entries=catalog))
        m.dump(contractpath,contract)
    print(json.dumps(dict(skills=len({r['id'] for r in resolved}),variants=len(resolved),clips=len(outputs),appliedLocal=args.apply_local)))


if __name__=='__main__':main()
