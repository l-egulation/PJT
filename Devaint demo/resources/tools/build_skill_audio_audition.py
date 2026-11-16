"""Build 40 skill auditions with verified deployed basic-attack references."""
import argparse
import base64
import io
import json
import math
from pathlib import Path
import re
import zipfile
from PIL import Image
import render_basic_attack_audio as media

ROOT=media.MAIN
ASSET=media.RES/'audio/gamemaster-skill-audition'
SOURCE=ASSET/'source'
UNITY=ROOT/'unity/Assets/Deviant/Combat/Resources'


def series(prefix, count=4):return [prefix+f'{i:02}.wav' for i in range(1,count+1)]


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--archive',type=Path,required=True)
    parser.add_argument('--reference-video',type=Path);parser.add_argument('--ffmpeg',type=Path);args=parser.parse_args()
    for folder in [SOURCE/'audio',ASSET/'preview']:folder.mkdir(parents=True,exist_ok=True)
    basic_html=(media.AUDITION/'preview/index.html').read_text(encoding='utf8')
    basic=json.loads(re.search(r'<script id="audition-data" type="application/json">(.*?)</script>',basic_html,re.S)[1])
    frames=basic['frames'];audio={};weapons=basic['weapons'];actions=[]
    animation=media.load(UNITY/'CharacterV2/AnimationCatalog.json')
    sets={s['form']:s for s in animation['sets']};sets.update({s['form']:s for s in media.load(UNITY/'CharacterV2/HeavyWeaponCatalog.json')['sets']})
    forms=media.load(UNITY/'MeleeCatalog.json')['forms'];selections=media.load(ROOT/'data/audio/basic-attack-selections.json')
    assert not selections['pending'], 'Finish basic attack choices before using them as references.'
    assert all(a.get('mode',0)==0 for a in animation['specialFrames']['actions']), 'FrameTime skills need explicit mapping.'
    deployment={x['target']:x for x in media.load(ROOT/'docs/assets/manifests/unity-deployment.json')['files']}
    registry={a['actionId']:a['motion'] for a in animation['motionRegistry']['actions'] if a.get('kind')==1}
    # Runtime MotionSourceRegistry synthesizes these dedicated bindings when absent in the explicit registry.
    registry.update({k:registry.get(k,v) for k,v in {'bomb_basic_special':'bomb/attack_triple_kick','shuriken_basic_special':'shuriken/attack_pierce','launcher_basic_special':'launcher/attack_charge_shot'}.items()})
    timings={e['id']:e for e in media.load(ROOT/'unity/Assets/Deviant/Audio/Resources/ApprovedCombatAudio/Catalog.json')['entries']}
    for e in media.load(ROOT/'unity/Assets/Deviant/Audio/Resources/WeaponSounds/Timing.json')['entries']:timings.setdefault(e['id'],e)
    bindings=(ROOT/'unity/Assets/Deviant/Audio/Resources/SelectedAudioClips.asset').read_text(encoding='utf-8-sig')

    def add_frame(ref):
        if ref in frames:return
        p=UNITY/(ref+'.png');raw=p.read_bytes();meta=p.with_suffix('.png.meta').read_text(encoding='utf-8-sig')
        pivot=re.search(r'spritePivot: \{x: ([\d.eE+-]+), y: ([\d.eE+-]+)\}',meta)
        im=Image.open(io.BytesIO(raw)).convert('RGBA');w,h=im.size
        frames[ref]={'src':'data:image/png;base64,'+base64.b64encode(raw).decode(),'pivot':[float(pivot[1])*w,(1-float(pivot[2]))*h],
            'ppu':float(re.search(r'spritePixelsToUnits: ([\d.]+)',meta)[1]),'bounds':im.getbbox() or [0,0,w,h],'sha256':media.sha(raw),'source':p.relative_to(ROOT).as_posix(),'width':w,'height':h}

    def deployed(action):
        name=f'sfx_weapon_{action}_01_v01.wav';p=ROOT/'unity/Assets/Deviant/Audio/SFX/Weapons'/name
        raw=p.read_bytes();record=deployment[p.relative_to(ROOT).as_posix()]
        assert media.sha(raw)==record['sha256'] and raw==(media.RES/record['source']['path']).read_bytes(),action
        guid=re.search(r'^guid: (\w+)',p.with_suffix('.wav.meta').read_text(encoding='utf-8-sig'),re.M)[1]
        binding=re.search(r'- id: sfx_weapon_'+re.escape(action)+r'\s+clip: \{[^}]*guid: (\w+)',bindings)
        assert binding and binding[1]==guid,action
        key='current/'+action
        (SOURCE/'audio'/name).write_bytes(raw)
        audio[key]={**media.builder.audio_payload(raw,name),'gain':.8,'nominalDuration':timings[action].get('nominalDuration',0),'source':p.relative_to(ROOT).as_posix(),'guid':guid}
        return key

    for w in weapons:
        w['currentVariants']={hit:deployed(action) for hit,action in w['currentVariants'].items()}
        w['selection']=selections['weapons'][w['id']]
        for key in ['candidates','reviewCandidates','choice','candidateRevision']:w.pop(key,None)

    pools={
      'slash':series('Whooshes/whoosh_swish_high_big_'),
      'thrust':series('Whooshes/whoosh_swish_high_fast_'),
      'whip':series('Magic_Spells/nature_spell_bush_tree_whip_'),
      'heavy':series('Whooshes/whoosh_low_deep_'),
      'ground':series('Impacts_Smashable/rock_impact_heavy_slam_'),
      'veil':series('Whooshes/whoosh_low_deep_soft_',3)+['Magic_Spells/dark_portal_wind_effect_01.wav'],
      'smoke':series('Explosion_Fire_Gas/gas_leak_short_burst_',3)+['Magic_Spells/fireball_impact_magic_smoke_01.wav'],
      'guard':series('Sci-Fi/sci-fi_shield_device_small_',3)+['Magic_Spells/energy_blast_small_01.wav'],
      'metal':series('Metal/metal_impact_light_thud_'),
      'spin':series('Whooshes/fan_spin_whoosh_'),
      'shot':series('Guns_Weapons/Guns_Silenced/gun_silenced_pistol1_shot_'),
      'laser':series('Sci-Fi Weapons/sci-fi_weapon_blaster_laser_boom_small_'),
      'charge':series('Magic_Spells/casting_charge_matter_fast_',3)+['Magic_Spells/casting_charge_matter_grow_01.wav'],
    }
    profiles={
      'base_sword_default':('slash','heavy','강한 베기'), 'base_sword_range':('slash','thrust','검기 방출'),
      'base_sword_movement':('thrust','slash','돌진 찌르기'),'base_sword_follow_up':('slash','thrust','잔상 베기'),
      'whip_default':('whip','thrust','감아 묶기'),'whip_range':('whip','veil','그물 펼치기'),
      'whip_movement':('whip','thrust','매듭 발동'),'whip_follow_up':('whip','metal','매듭 조임'),
      'rapier_default':('thrust','charge','모아 찌르기'),'rapier_range':('thrust','slash','긴 찌르기'),
      'rapier_movement':('thrust','veil','이동 후 찌르기'),'rapier_follow_up':('thrust','metal','5연속 찌르기'),
      'dagger_default':('veil','smoke','은신 진입'),'dagger_range':('smoke','veil','먹안개 방출'),
      'dagger_movement':('thrust','veil','도약 한 번'),'dagger_follow_up':('thrust','slash','6연속 베기'),
      'hammer_default':('heavy','ground','무거운 횡타'),'hammer_range':('ground','heavy','지면 강타'),
      'hammer_movement':('heavy','ground','돌진 밀치기'),'hammer_follow_up':('ground','heavy','강타와 여진'),
      'shield_default':('metal','guard','방패 세우기'),'shield_range':('guard','veil','보호막 전개'),
      'shield_movement':('heavy','metal','방패 밀기'),'shield_follow_up':('guard','metal','미끼 설치'),
      'greatsword_default':('heavy','ground','모아 내려찍기'),'greatsword_range':('ground','heavy','대검 소환'),
      'greatsword_movement':('spin','heavy','반복 회전'),'greatsword_follow_up':('heavy','spin','투척 후 회전'),
      'launcher_basic_special':('shot','charge','충전 발사'),'launcher_range_special':('laser','shot','짧은 레이저'),
      'launcher_move_special':('shot','heavy','산탄 한 번'),'launcher_followup_special':('shot','metal','유탄 발사'),
      'bomb_basic_special':('slash','thrust','3연속 투척'),'bomb_range_special':('heavy','slash','큰 폭탄 투척'),
      'bomb_move_special':('heavy','charge','미사일 변신'),'bomb_followup_special':('slash','metal','시한폭탄 투척'),
      'shuriken_basic_special':('thrust','spin','관통 수리검'),'shuriken_range_special':('thrust','metal','폭발 수리검 투척'),
      'shuriken_move_special':('thrust','veil','이동 투척'),'shuriken_followup_special':('spin','thrust','회전 수리검 방출'),
    }
    notes={
      'dagger_range':'먹안개 생성음만 비교합니다. 피해 틱마다 소리를 반복하지 않습니다.',
      'dagger_movement':'충전 한 번을 쓴 도약입니다. 자동 3연타로 만들지 않았습니다.',
      'shield_default':'가드 진입음입니다. 방어 성공·반격은 상대 공격에 따라 발생하므로 별도입니다.',
      'shield_follow_up':'미끼 설치음입니다. 미끼가 공격받을 때의 보복음은 별도입니다.',
      'whip_movement':'첫 매듭 발동입니다. 재입력으로 접근하는 동작은 별도입니다.',
      'whip_follow_up':'후속 조임은 대상이 살아 있는 경우를 가정해 비교합니다.',
      'bomb_move_special':'변신·출발만 비교합니다. 최대 비행 시간과 실제 충돌 폭발은 재현하지 않습니다.',
      'greatsword_follow_up':'투척 후 검이 회전하는 3회 소리까지 비교합니다. 검의 이동은 재현하지 않습니다.',
    }
    utility={'dagger_default','dagger_range','shield_default','shield_range','shield_follow_up','hammer_range','hammer_follow_up','greatsword_range','launcher_range_special','bomb_move_special','shuriken_followup_special','greatsword_movement'}
    cache={}
    with zipfile.ZipFile(args.archive) as archive:
        def source_audio(entry,tail=.3):
            cachekey=(entry,tail)
            if cachekey in cache:return cache[cachekey]
            raw=archive.read(entry) if not entry.startswith('current/') else (ROOT/audio[entry]['source']).read_bytes()
            samples=media.crop_transient(media.decode(raw),after=tail,before=.045,lowpass=6500)
            samples=media.level(samples,-22)
            rendered=media.wav_bytes(samples);key='cut/'+media.sha(rendered)[:20]
            name=key.replace('/','-')+'.wav';(SOURCE/'audio'/name).write_bytes(rendered)
            audio[key]={**media.builder.audio_payload(rendered,name),'gain':1,'peakTime':media.sliding_peak(media.decode(rendered)),
                'source':entry,'sourceSha256':media.sha(raw),'edit':{'tailSeconds':tail,'lowpassHz':6500,'activeRmsDb':-22}}
            cache[cachekey]=key;return key

        def tint_audio(key,rate):
            source=audio[key];samples=media.decode((SOURCE/'audio'/source['file']).read_bytes())
            pitched=[]
            for i in range(int((len(samples)-1)/rate)):
                at=i*rate;n=int(at);fraction=at-n;pitched.append(samples[n]*(1-fraction)+samples[n+1]*fraction)
            raw=media.wav_bytes(media.level(pitched,-22));newkey='cut/'+media.sha(raw)[:20];name=newkey.replace('/','-')+'.wav'
            (SOURCE/'audio'/name).write_bytes(raw)
            audio[newkey]={**media.builder.audio_payload(raw,name),'gain':1,'peakTime':media.sliding_peak(media.decode(raw)),
                'source':source['source'],'sourceSha256':source['sourceSha256'],'edit':{**source['edit'],'pitchRate':rate}}
            return newkey

        for form in forms:
            weapon=form['id'];w=next(w for w in weapons if w['id']==weapon)
            for spec in form['specials']:
                if spec['end']<=0:continue
                aid=spec['id'];primary,secondary,meaning=profiles[aid]
                motion=registry[aid];mf,mc=motion.split('/')
                clip=next(c for c in sets[mf]['clips'] if c['name']==mc)
                fx=next((e for e in sets[mf].get('effects',[]) if e['attack']==mc),None)
                for ref in clip['frames']+(fx['frames'] if fx else []):add_frame(ref)
                body_end=.2 if aid=='bomb_move_special' else spec['end']
                marks=sorted(set(h['time'] for h in spec['hits'])) or [spec['activation']]
                if aid=='dagger_range':marks=[spec['activation']]
                if aid=='greatsword_follow_up':marks=[spec['activation']]+marks
                identity=selections['weapons'][weapon].get('archiveEntry')
                if weapon=='hammer':identity='Whooshes/whoosh_low_deep_soft_01.wav'
                if weapon=='launcher':identity=w['currentVariants']['attack1']
                assert identity,weapon
                identity=source_audio(identity,.18 if primary in ['thrust','shot'] else .3)
                primary_pool=[source_audio(e,.18 if primary in ['thrust','shot'] else .32) for e in pools[primary]]
                secondary_pool=[source_audio(e,.18 if weapon=='launcher' else .24) for e in pools[secondary]]
                first=source_audio(pools[primary][0],.14) if aid in utility else identity
                main_sources=list(dict.fromkeys([first,*primary_pool]))
                for rate in [.88,1.12]:
                    if len(main_sources)<5:main_sources.append(tint_audio(first,rate))
                assert len(main_sources)==5
                recipes=[]
                for i in range(10):
                    base=main_sources[i%5];is_mix=i>=5
                    main_marks=marks
                    if aid=='greatsword_follow_up':main_marks=[spec['activation']]
                    layers=[{'id':'main','name':meaning,'audio':base,'times':main_marks,'gain':1,'offsetMs':0}]
                    if is_mix:
                        detail_marks=[spec['activation']]
                        if aid=='greatsword_follow_up':detail_marks=marks[1:]
                        elif secondary in ['heavy','charge','veil'] and aid not in ['shield_range','dagger_default','dagger_range']:detail_marks=[max(0,spec['activation']-.07)]
                        elif aid in ['rapier_follow_up','dagger_follow_up']:detail_marks=marks
                        layers.append({'id':'detail','name':{'ground':'지면 충격','charge':'짧은 힘 모으기','metal':'짧은 단단한 악센트','spin':'회전','veil':'공기·전환'}.get(secondary,'보조 움직임'),
                            'audio':secondary_pool[(i-5)%4],'times':detail_marks,'gain':.42 if weapon!='launcher' else .22,'offsetMs':0})
                        if aid=='greatsword_follow_up' and i==9:layers[1]['gain']=.6
                    elif aid=='greatsword_follow_up':layers[0]['times']=marks
                    # The player limits actual overlapping intervals after edits, not all hits at once.
                    name=('기본공격 계열' if i==0 and aid not in utility else meaning+' '+str(i%5+1))
                    if is_mix:name+= ' + '+layers[1]['name']
                    recipes.append({'number':i+1,'id':aid+f'-v1-{i+1:02}','name':name,'layers':layers,'mixed':is_mix,
                        'reason':('확정 기본공격의 질감을 스킬 타이밍에 배치' if i==0 and aid not in utility else '한 음원으로 동작을 간결하게 표현') if not is_mix else '주 소리에 짧은 보조음을 더해 동작의 두 단계를 표현'})
                note=notes.get(aid,'')
                if weapon in ['bomb','shuriken'] or aid=='launcher_followup_special':note+=' 투척·발사와 실제 명중·폭발 사건은 분리합니다.'
                if spec.get('chargeMax',0)>0 or aid=='launcher_basic_special':note+=' 충전 해제 동작 기준이며 버튼을 누르고 있는 시간은 생략합니다.'
                actions.append({'id':aid,'weapon':weapon,'name':spec['name'],'summary':spec.get('summary',''),'duration':body_end,'impact':spec['activation'],
                    'markers':marks,'clip':clip,'effect':fx,'motion':motion,'note':note.strip(),'candidates':recipes,'current':deployed(aid),
                    'currentTiming':timings[aid],'previewDuration':max(body_end,max(marks)+.35)})
    assert len(actions)==40 and len(weapons)==11
    payload={'schema':1,'revision':1,'weapons':weapons,'actions':actions,'frames':frames,'audio':audio,'defaultWeapon':'hammer'}
    from skill_audio_revision2 import revise
    revise(payload,args.archive,SOURCE)
    from skill_audio_revision3 import revise as revise3
    revise3(payload,args.archive,SOURCE,args.reference_video,args.ffmpeg,include_reference=False)
    from skill_audio_revision4 import revise as revise4
    revise4(payload,args.archive,SOURCE)
    from skill_audio_revision5 import revise as revise5
    revise5(payload,args.archive,SOURCE)
    from skill_audio_revision6 import revise as revise6
    revise6(payload,args.archive,SOURCE)
    from skill_audio_revision7 import revise as revise7
    revise7(payload,args.archive,SOURCE)
    from skill_audio_revision8 import revise as revise8
    revise8(payload,args.archive,SOURCE)
    from skill_audio_revision9 import revise as revise9
    revise9(payload,args.archive,SOURCE)
    from skill_audio_revision10 import revise as revise10
    revise10(payload,args.archive,SOURCE)
    from skill_audio_revision11 import revise as revise11
    revise11(payload,args.archive,SOURCE)
    from skill_audio_revision12 import revise as revise12
    revise12(payload,SOURCE)
    from skill_audio_revision13 import revise as revise13
    revise13(payload,SOURCE,args.archive,SOURCE/'selections/user-skill-selections-revision12.json')
    from skill_audio_revision14 import revise as revise14
    revise14(payload,SOURCE,args.archive)
    template=(SOURCE/'template.html').read_text(encoding='utf8');app=(SOURCE/'app.js').read_text(encoding='utf8')
    html=template.replace('__DATA__',json.dumps(payload,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',app)
    (ASSET/'preview/index.html').write_text(html,encoding='utf8')
    provenance=dict(payload,frames={k:{n:v[n] for n in ['source','sha256']} for k,v in frames.items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in audio.items()})
    media.dump(SOURCE/'recipes.json',provenance)
    media.register_asset(ASSET,'audio.gamemaster-skill-audition','40 skills and 2 basic reviews; revision 11: one-second smoke hiss, impact-free water releases; stars preserved; not applied')
    p=media.RES/'.lfs-payloads.json';data=media.load(p)
    # Same manifest shape used by the basic audition builder.
    path=ASSET.relative_to(media.RES).as_posix()+'/preview/index.html'
    media.dump(p,sorted(set(data+[path])))
    print(json.dumps({'skills':sum(not a.get('isBasicReview') for a in actions),'reviewItems':len(actions),'candidates':len(actions)*10,'basicWeapons':len(weapons),'frames':len(frames),'audio':len(audio),'bytes':len(html.encode('utf8'))}))


if __name__=='__main__':main()
