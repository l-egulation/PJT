"""Build ten wet-ink hit studies and an offline comparison against installed attacks."""
import base64
import copy
import json
import re
import zipfile
from pathlib import Path
import numpy as np
import render_basic_attack_audio as m

OUT=m.RES/'audio/ink-hit-audition'
SOURCE=OUT/'source'
RATE=m.RATE

def filtered(x, cutoff):
    # Broad, smooth roll-off; no hard spectral gate, generated noise, or resonator.
    n=len(x); size=1 << (n*2-1).bit_length()
    hz=np.fft.rfftfreq(size,1/RATE)
    shape=1/np.sqrt(1+(hz/cutoff)**4)
    shape*=1-np.exp(-(hz/65)**2)
    return np.fft.irfft(np.fft.rfft(x,size)*shape,size)[:n]

def main():
    for name in ['source/originals','runtime','preview']: (OUT/name).mkdir(parents=True,exist_ok=True)
    archive=Path('C:/Users/SSAFY/Downloads/Gamemaster Audio - Pro Sound Collection.zip')
    z=zipfile.ZipFile(archive)
    # Each direction uses actual wet collisions/splashes, not a wooden or metal core.
    specs=[
      ('잉크 튐','찰팍','가볍게 맞고 액체가 퍼지는 질감',.24,[('Water/water_splash_small_item_02.wav',1.10,6000,1),('Guns_Weapons/Bullets/bullet_impact_water_03.wav',1,4600,.34)]),
      ('말랑한 잉크','쫩','부드러운 덩어리가 눌리는 질감',.25,[('Punches/punch_grit_wet_impact_04.wav',.88,2500,.65),('Water/water_splash_small_item_04.wav',.85,4200,1)]),
      ('젖은 타격','퍽촥','둥근 타격과 짧은 튐의 균형',.23,[('Punches/punch_grit_wet_impact_01.wav',.93,2700,.8),('Guns_Weapons/Bullets/bullet_impact_water_01.wav',1.12,6200,1)]),
      ('얇은 잉크막','촥','가볍고 넓게 찢어져 퍼지는 질감',.20,[('Water/water_splash_small_item_05.wav',1.24,6800,1),('Guns_Weapons/Bullets/bullet_impact_water_05.wav',1.2,5500,.5)]),
      ('진한 잉크 덩어리','푸걱','밀도 높은 덩어리가 찌그러지는 질감',.29,[('Punches/punch_grit_wet_impact_07.wav',.72,2000,.8),('Water/water_splash_object_body_03.wav',.83,3800,1)]),
      ('잘게 튀는 잉크','파촤','작은 방울이 빠르게 흩어지는 질감',.22,[('Guns_Weapons/Bullets/bullet_impact_water_06.wav',1.18,7200,1),('Water/water_splash_small_item_01.wav',1.28,5800,.65)]),
      ('달라붙는 잉크','쭈왁','점성 있는 젖은 눌림과 풀림',.30,[('Punches/punch_grit_wet_impact_09.wav',.82,3200,.8),('Water/water_splash_small_item_03.wav',.74,3900,1)]),
      ('통통한 잉크방울','푸팍','짧은 액체 파열이 중심인 질감',.21,[('Guns_Weapons/Bullets/bullet_impact_water_08.wav',.9,4200,1),('Water/water_splash_small_item_04.wav',1.3,6500,.65)]),
      ('묵직한 잉크 파동','푸촤','낮고 넓은 물결이 남는 질감',.34,[('Water/water_splash_object_body_05.wav',.72,2800,1),('Guns_Weapons/Bullets/bullet_impact_water_02.wav',.85,3700,.65)]),
      ('압축된 잉크 타격','팍쫩','짧고 또렷한 연타용 질감',.18,[('Punches/punch_grit_wet_impact_03.wav',1.08,3400,.65),('Guns_Weapons/Bullets/bullet_impact_water_04.wav',1.18,6000,1)])]
    audio={};frames={};candidates=[];provenance=[]
    def add_audio(path):
        raw=path.read_bytes();key=m.sha(raw)
        if key not in audio:
            x=np.asarray(m.decode(raw));audio[key]=dict(src='data:audio/wav;base64,'+base64.b64encode(raw).decode(),
                duration=len(x)/RATE,sha256=key,source=path.relative_to(m.MAIN).as_posix(),
                wave=[round(float(np.max(np.abs(v))),4) for v in np.array_split(x,100) if len(v)])
        return key
    for i,(name,onomatopoeia,description,duration,layers) in enumerate(specs,1):
        count=round(duration*RATE);result=np.zeros(count);recipe=[]
        for j,(entry,rate,cutoff,gain) in enumerate(layers):
            raw=z.read(entry);original=SOURCE/'originals'/Path(entry).name;original.write_bytes(raw)
            x=np.asarray(m.decode(raw));window=round(.008*RATE)
            energy=np.convolve(x*x,np.ones(window)/window,'valid')
            peak=int(np.argmax(energy));start=max(0,peak-round(.012*RATE))
            source=x[start:start+round((duration*rate+.03)*RATE)]
            y=np.interp(np.arange(0,len(source)-1,rate),np.arange(len(source)),source)
            y=filtered(y,cutoff);y=np.pad(y,(0,max(0,count-len(y))))[:count]
            attack=round(.003*RATE);fade=round(min(.11,duration*.48)*RATE)
            y[:attack]*=np.sin(np.linspace(0,np.pi/2,attack))**2
            y[-fade:]*=np.cos(np.linspace(0,np.pi/2,fade))**2
            y*=np.exp(-np.arange(count)/RATE/(duration*.75))
            rms=np.sqrt(np.mean(y*y));y*=.105/max(rms,1e-6)*gain
            result+=y
            recipe.append(dict(archiveEntry=entry,sourceSha256=m.sha(raw),startSeconds=start/RATE,rate=rate,lowpassHz=cutoff,gain=gain))
        result-=np.mean(result)
        # Lower narrow splash clicks with a smooth look-ahead gain envelope.
        # This lifts the liquid body without clipping or a hard limiter's grit.
        threshold=np.sqrt(np.mean(result*result))*2.0
        padded=np.pad(np.abs(result),(192,192))
        envelope=np.maximum.reduce([padded[k:k+count] for k in range(0,385,8)])
        reduction=np.minimum(1,(threshold/np.maximum(envelope,1e-9))**.85)
        reduction=np.convolve(np.pad(reduction,(96,96),mode='edge'),np.ones(193)/193,'valid')
        result*=reduction
        result[:96]*=np.linspace(0,1,96);result[-240:]*=np.linspace(1,0,240)
        scale=min(.115/np.sqrt(np.mean(result*result)),.78/np.max(np.abs(result)));result*=scale
        path=OUT/'runtime'/f'ink-hit-{i:02}.wav';path.write_bytes(m.wav_bytes(result))
        key=add_audio(path);candidates.append(dict(number=i,name=name,voice=onomatopoeia,description=description,audio=key,duration=duration))
        provenance.append(dict(number=i,layers=recipe,scale=float(scale),peak=float(np.max(np.abs(result))),rms=float(np.sqrt(np.mean(result*result))),sha256=key))

    p=m.load(m.RES/'audio/gamemaster-skill-audition/source/recipes.json')
    selected=m.load(m.MAIN/'unity/Assets/Deviant/Audio/Resources/SelectedSkillAudio/Catalog.json')['entries']
    selected={e['id']:e for e in selected}
    installed=m.MAIN/'unity/Assets/Deviant/Audio/Resources'
    meta_by_guid={}
    for meta in (m.MAIN/'unity/Assets/Deviant/Audio').rglob('*.wav.meta'):
        match=re.search(r'guid: ([0-9a-f]+)',meta.read_text(encoding='utf8'))
        if match:meta_by_guid[match[1]]=Path(str(meta)[:-5])
    selected_refs=dict(re.findall(r'- id: (\S+)\s+clip: \{fileID: \d+, guid: ([0-9a-f]+)',(installed/'SelectedAudioClips.asset').read_text(encoding='utf8')))
    timing={e['id']:e for e in m.load(installed/'WeaponSounds/Timing.json')['entries']}
    timing.update({e['id']:e for e in m.load(installed/'ApprovedCombatAudio/Catalog.json')['entries']})
    def clip_data(clip):
        if not clip:return None
        for ref in clip['frames']:
            if ref in frames:continue
            meta=p['frames'][ref];path=m.MAIN/meta['source'];raw=path.read_bytes()
            assert m.sha(raw)==meta['sha256'],ref
            frames[ref]='data:image/png;base64,'+base64.b64encode(raw).decode()
        return {k:clip[k] for k in ['frames','seconds','sourceImpact']}
    def cue(entry,at=0,index=0):
        if entry.get('silent'):return []
        paths=entry.get('variants') or [entry['clip']]
        path=installed/(paths[min(index,len(paths)-1)]+'.wav')
        # Deployed bytes must match the registered resource source.
        source=m.RES/'audio/gamemaster-skills-selected/runtime'/path.name
        assert path.read_bytes()==source.read_bytes(),path
        return [dict(audio=add_audio(source),time=at,rate=1)]
    actions=[];weapons=[]
    for w in p['weapons']:
        weapons.append(dict(id=w['id'],name=w['label']))
        segments=[];cues=[];hits=[];at=0;speed=w['defaultSpeed']
        for i,b in enumerate(w['basics']):
            aid=f'{w["id"]}_basic_{i}';path=meta_by_guid[selected_refs['sfx_weapon_'+aid]]
            key=add_audio(path);t=timing[aid];duration=b['duration']/speed;impact=b['impact']/speed
            ev=dict(audio=key,time=at,rate=t['nominalDuration']/duration)
            segment=dict(start=at,duration=duration,impact=impact,clip=clip_data(w['clips']['attack'+str(i+1)]))
            hit=at+impact;hitkind='direct'
            if w['id'] in ('launcher','shuriken'):hitkind='projectile'
            if w['id']=='bomb':hitkind='explosion'
            actions.append(dict(id=aid,weapon=w['id'],name=f'기본공격 {i+1}타',events=[dict(ev,time=0)],hits=[impact],
                hitKind=hitkind,segments=[dict(segment,start=0)],duration=duration,basic=True))
            cues.append(ev);segments.append(segment);hits.append(hit);at+=duration
        actions.append(dict(id=w['id']+'_combo',weapon=w['id'],name='기본공격 전체 콤보',events=cues,hits=hits,
            hitKind=hitkind,segments=segments,duration=at,basic=True))
    no_hit={'dagger_default','dagger_range','shield_range','shield_follow_up'}
    for a in p['actions']:
        aid=a['id']
        if aid not in selected:continue
        e=selected[aid]
        for index,path in enumerate(e['variants']):
            events=cue(e,index=index);triggered=[]
            for sid,entry in selected.items():
                if sid.startswith(aid+'__'):
                    triggered.append(dict(trigger=sid.split('__')[1],events=cue(entry,index=index)))
            hitkind='direct'
            if any(v['trigger']=='explosion' for v in triggered):hitkind='explosion'
            elif a['weapon'] in ('launcher','shuriken'):hitkind='projectile'
            if aid=='shuriken_move_special':hitkind='projectile'
            if aid=='launcher_range_special':hitkind='direct'
            label=a['name']+(f' · 확정 {int(path[-2:])}번' if len(e['variants'])>1 else '')
            actions.append(dict(id=aid+f'@{index}',actionId=aid,weapon=a['weapon'],name=label,events=events,triggered=triggered,
                hits=[] if aid in no_hit else a['markers'],hitKind=hitkind,duration=a['duration'],impact=a['impact'],
                segments=[dict(start=0,duration=a['duration'],impact=a['impact'],clip=clip_data(a['clip']))],basic=False,
                noDamage=aid in no_hit,scenario=a.get('scenario',{})))
    # Resolve existing hit sounds through the same SelectedAudioClips GUIDs used
    # by ApprovedCombatAudio.EmitSelected, with WeaponHitSounds.For routing.
    hit_ids={w['id']:'sfx_hit_'+w['id'] for w in p['weapons']}
    hit_ids.update(base_sword='sfx_hit_sword',launcher='sfx_hit_launcher_ball',bomb=None)
    for a in actions:
        aid=a.get('actionId',a['id']);hid=hit_ids[a['weapon']]
        if aid=='launcher_range_special':hid='sfx_hit_launcher_laser'
        if a.get('noDamage') or a['hitKind']=='explosion' or aid=='launcher_basic_special':hid=None
        a['currentHit']=None
        a['currentHitNote']='비공격 스킬 · 별도 피격음 없음' if a.get('noDamage') else '현재는 별도 피격음 없이 폭발음만 재생'
        if hid:
            guid=selected_refs[hid];path=meta_by_guid[guid];key=add_audio(path)
            a['currentHit']=dict(id=hid,audio=key,gain=.8,guid=guid,file=path.name)
            a['currentHitNote']=('레이저' if aid=='launcher_range_special' else next(w['label'] for w in p['weapons'] if w['id']==a['weapon']))+' · 현재 적용된 피격음'
    extras={name:cue(selected[name]) for name in ['explosion_bomb','explosion_large']}
    # Preserve the currently installed cannon impact as the game runtime does.
    old=meta_by_guid.get(selected_refs.get('sfx_weapon_explosion_bomb',''))
    if old is None:
        entry=timing['explosion_bomb'];old=installed/(entry['clip']+'.wav')
    extras['launcher_charge_impact']=[dict(audio=add_audio(old),time=0,rate=1)]
    payload=dict(schema=1,candidates=candidates,weapons=weapons,actions=actions,audio=audio,frames=frames,extras=extras,
        scope='local installed approved attack audio; hypothetical target contacts for audition only',
        selectionSha256=m.sha((m.MAIN/'data/audio/skill-selections.json').read_bytes()))
    m.dump(SOURCE/'recipes.json',dict(status='ten concepts awaiting user selection; not applied to game',candidates=provenance,
        inputs='Purchased Gamemaster Audio pack',audio={k:{n:v for n,v in x.items() if n not in ('src','wave')} for k,x in audio.items()},
        actions=[{k:v for k,v in a.items() if k!='segments'} for a in actions]))
    html=(SOURCE/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(payload,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(SOURCE/'app.js').read_text(encoding='utf8'))
    (OUT/'preview/index.html').write_text(html,encoding='utf8')
    lfs=m.load(m.RES/'.lfs-payloads.json');entry='audio/ink-hit-audition/preview/index.html'
    if entry not in lfs:lfs.append(entry);m.dump(m.RES/'.lfs-payloads.json',sorted(lfs))
    m.register_asset(OUT,'audio.ink-hit-audition','10 wet ink concepts; installed basic/skill audio comparison; pending user choice')
    print(json.dumps(dict(candidates=10,weapons=len(weapons),actions=len(actions),frames=len(frames),audio=len(audio),htmlBytes=len(html.encode()))))

if __name__=='__main__':main()
