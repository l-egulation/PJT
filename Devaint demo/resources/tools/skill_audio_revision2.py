"""Event-specific second review: charge, sustain, conditional events and shared families."""
import math
import json
import wave
import zipfile
from pathlib import Path
import render_basic_attack_audio as m


def seq(prefix,n=4):return [prefix+f'{i:02}.wav' for i in range(1,n+1)]


def resize(samples,count):
    if count<=1:return samples[:1]
    ratio=(len(samples)-1)/(count-1)
    out=[]
    for i in range(count):
        at=i*ratio;j=min(len(samples)-2,int(at));f=at-j;out.append(samples[j]*(1-f)+samples[j+1]*f)
    return out


def filter_audio(samples,low=7000,high=0):
    lo=0;hi=0;al=1-math.exp(-2*math.pi*low/m.RATE);ah=1-math.exp(-2*math.pi*high/m.RATE) if high else 0;out=[]
    for v in samples:
        lo+=al*(v-lo);hi+=ah*(lo-hi);out.append(lo-hi if high else lo)
    return out


def fade(samples,attack=.004,release=.035):
    out=list(samples);a=min(len(out)//3,round(attack*m.RATE));b=min(len(out)//3,round(release*m.RATE))
    for i in range(a):out[i]*=i/max(1,a)
    for i in range(b):out[-i-1]*=i/max(1,b)
    return out


def revise(payload,archive_path,source):
    audio=payload['audio'];actions={a['id']:a for a in payload['actions']};cache={};pcm={}
    specs={s['id']:s for f in m.load(m.MAIN/'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json')['forms'] for s in f['specials']}
    z=zipfile.ZipFile(archive_path)

    def register(samples,provenance,edit):
        raw=m.wav_bytes(samples);key='r2/'+m.sha(raw)[:20];name=key.replace('/','-')+'.wav';(source/'audio'/name).write_bytes(raw)
        decoded=m.decode(raw);pcm[key]=decoded
        audio[key]={**m.builder.audio_payload(raw,name),'peakTime':m.sliding_peak(decoded),'peak':max(map(abs,decoded)),
                    'gain':1,'source':provenance,'edit':edit}
        return key

    def cut(entry,tail=.3,pitch=1,low=6500,high=60,db=-20,mode='burst',length=None):
        sig=(entry,tail,pitch,low,high,db,mode,length)
        if sig in cache:return cache[sig]
        if entry in audio:
            raw=(source/'audio'/audio[entry]['file']).read_bytes();provenance={'processedSource':entry,'sha256':m.sha(raw),'origin':audio[entry]['source']}
        else:
            raw=z.read(entry);provenance={'archiveEntry':entry,'sha256':m.sha(raw)}
        samples=m.decode(raw)
        if mode=='burst':samples=m.crop_transient(samples,after=tail,before=.012)
        else:
            threshold=max(map(abs,samples))*.012;start=next((i for i,x in enumerate(samples) if abs(x)>threshold),0)
            samples=samples[max(0,start-round(.008*m.RATE)):]
            samples=samples[:min(len(samples),round((tail if mode=='full' else max(1.5,tail))*m.RATE))]
        if pitch!=1:samples=resize(samples,max(2,round(len(samples)/pitch)))
        samples=filter_audio(samples,low,high)
        if mode=='charge':
            samples=resize(samples,round((length or .8)*m.RATE))
            samples=[v*(.12+.88*(i/max(1,len(samples)-1))**1.3) for i,v in enumerate(samples)]
        elif mode=='loop':
            samples=resize(samples,round((length or 1)*m.RATE))
            # Equal-length endpoints with a 50 ms overlap; no silence at the wrap.
            n=min(round(.05*m.RATE),len(samples)//6)
            for i in range(n):
                t=i/max(1,n-1);samples[i]=samples[-n+i]*(1-t)+samples[i]*t
            samples=samples[:-n]
        if mode!='loop':samples=fade(samples,.015 if mode=='charge' else .003,.03)
        samples=m.level(samples,db)
        key=register(samples,provenance,{'mode':mode,'tail':tail,'pitch':pitch,'lowpass':low,'highpass':high,'activeRmsDb':db,'length':length});cache[sig]=key;return key

    def samples(key):
        if key not in pcm:pcm[key]=m.decode((source/'audio'/audio[key]['file']).read_bytes())
        return pcm[key]

    def composite(parts,label,db=-20):
        # Parts are onset-aligned, optionally reversed. Reverbs/tails remain part of the designed event.
        prepared=[(list(reversed(samples(k))) if reverse else samples(k),at,gain) for k,at,gain,reverse in parts]
        size=max(round(at*m.RATE)+len(s) for s,at,gain in prepared);out=[0.0]*size
        for s,at,gain in prepared:
            start=round(at*m.RATE)
            for j,v in enumerate(s):out[start+j]+=v*gain
        out=m.level(fade(out),db)
        return register(out,{'composite':label,'parts':[{'audio':k,'onset':at,'gain':gain,'reverse':reverse} for k,at,gain,reverse in parts]}, {'mode':'composite','activeRmsDb':db})

    def variant(bank,i,**kw):return cut(bank[i%len(bank)],pitch=kw.pop('pitch',1)*(1+(.055*(i//len(bank)))),**kw)
    air=seq('Whooshes/whoosh_swish_high_fast_');deep=seq('Whooshes/whoosh_low_deep_',5)
    charge=seq('Magic_Spells/casting_charge_matter_grow_')+[f'Magic_Spells/casting_charge_whoosh_buildup{i}.wav' for i in range(1,7)]
    ring=['Guns_Weapons/Knife_Sword_Pick/sword_hit_impact_ringing_01.wav','Guns_Weapons/Knife_Sword_Pick/knife_unsheathe_04_with_ring.wav']+seq('Metal/metal_hit_small_',8)
    blade=seq('Guns_Weapons/Knife_Sword_Pick/sword_hit_impact_',4)+seq('Guns_Weapons/Knife_Sword_Pick/sword_hit_impact_heavy_',5)
    earth=seq('Impacts_Smashable/rock_impact_heavy_slam_')
    rumble=seq('Impacts_Smashable/rock_earthquake_impact_',2)+seq('Impacts_Smashable/rock_avalanche_landslide_debris_',3)
    wet=seq('Water/water_splash_small_item_',5)
    fabric=seq('Foley/foley_leather_stretch_couch_chair_',6)
    breeze=seq('Magic_Spells/wind_blizzard_storm_spell_light_',3)+seq('Magic_Spells/dark_portal_wind_effect_',5)
    spins=seq('Whooshes/fan_spin_whoosh_',4)
    conjure=seq('Magic_Spells/magic_conjure_charge1_',5)+seq('Magic_Spells/magic_conjure_charge2_',5)
    shot=seq('Guns_Weapons/Guns/gun_shotgun_shot_')+seq('Guns_Weapons/Guns/gun_shotgun_sawed_off_shot_')
    cannon=seq('Guns_Weapons/Guns/weapon_cannon_shot_')+seq('Guns_Weapons/Guns/gun_tank_cannon_turret_shot_')
    grenade=seq('Guns_Weapons/Guns/gun_grenade_launcher_shot_')
    small=seq('Explosion_Fire_Gas/explosion_small_no_tail_')
    large=seq('Explosion_Fire_Gas/explosion_large_no_tail_',6)+seq('Explosion_Fire_Gas/explosion_large_',4)
    portal=seq('Magic_Spells/dark_portal_wind_effect_',5)
    groups={}

    # Runtime basic bomb explosion is the ApprovedCombatAudio event, not the similarly named unused file.
    catalog=m.load(m.MAIN/'unity/Assets/Deviant/Audio/Resources/ApprovedCombatAudio/Catalog.json')
    entry=next(e for e in catalog['entries'] if e['id']=='explosion_bomb')
    path=m.MAIN/'unity/Assets/Deviant/Audio/Resources'/(entry['clip']+'.wav');raw=path.read_bytes()
    deployed=next(e for e in m.load(m.MAIN/'docs/assets/manifests/unity-deployment.json')['files'] if e['target']==path.relative_to(m.MAIN).as_posix())
    assert m.sha(raw)==deployed['sha256'] and raw==(m.RES/deployed['source']['path']).read_bytes()
    basic_key='current/explosion_bomb';filename='current-basic-bomb-explosion.wav';(source/'audio'/filename).write_bytes(raw)
    audio[basic_key]={**m.builder.audio_payload(raw,filename),'source':path.relative_to(m.MAIN).as_posix(),'gain':.8,'peak':max(map(abs,m.decode(raw))),'peakTime':0,'edit':{'unchangedRuntimeEvent':True}}
    groups['basic_explosion']={'label':'기본 폭탄 · 폭발 수리검 공통 폭발','fixed':True,'variants':[{'audio':basic_key,'gain':.8}], 'scope':'기본 폭탄과 같은 현재 게임 폭발 WAV'}
    groups['teleport']={'label':'공통 순간이동','scope':'흘려찌르기 · 섬광 연침 · 수리검 뒤이동','variants':[]}
    groups['large_explosion']={'label':'대형 폭탄 공통 폭발','scope':'대형 폭탄 · 착지 시한폭탄 · 미사일','variants':[]}
    groups['shuriken_throw']={'label':'공통 수리검 투척','scope':'폭발 수리검 · 회전 수리검','variants':[]}
    basic_peak,basic_rms,_=m.stats(m.decode(raw));basic_peak*=.8;basic_rms*=.8
    for i in range(10):
        rush=variant(air if i<5 else portal,i,tail=.16,high=280,db=-20)
        ink=variant(wet,i,tail=.075,high=260,low=4300,db=-26)
        teleport=composite([(rush,0,.8,True),(ink,.10,.35,False)],'ink teleport',-20)
        groups['teleport']['variants'].append({'audio':teleport,'gain':.9,'name':f'순간이동 {i+1:02} · '+('짧은 흡입' if i<5 else '공간 찢김')})
        blast=variant(large,i,tail=.7,low=4200,high=65,db=-19)
        low=variant(rumble,i,tail=.6,low=600,high=45,db=-25)
        combo=composite([(blast,0,1,False),(low,.05,.25,False)],'large ink bomb',-18)
        peak,rms,_=m.stats(samples(combo))
        blast_gain=min(.85,basic_rms*10**(4.5/20)/rms,basic_peak*2/peak)
        groups['large_explosion']['variants'].append({'audio':combo,'gain':blast_gain,'name':f'대형 폭발 {i+1:02} · 절제된 저음'})
        toss=variant(seq('Whooshes/whoosh_swish_high_big_',5),i,tail=.22,high=110,db=-21)
        groups['shuriken_throw']['variants'].append({'audio':toss,'gain':1,'name':f'수리검 투척 {i+1:02}'})

    def layer(id,name,key,times,gain=1,anchor='absolute',align='peak',**extra):
        return dict(id=id,name=name,audio=key,times=times,gain=gain,anchor=anchor,align=align,**extra)
    def shared(id,name,group,times,anchor='impactDemo',**extra):
        v=groups[group]['variants'][0]
        return layer(id,name,v['audio'],times,v['gain'],anchor,'onset',group=group,**extra)
    changed={
      'base_sword_follow_up','whip_follow_up','rapier_default','rapier_movement','rapier_follow_up','dagger_range','hammer_follow_up',
      'shield_default','shield_range','shield_movement','shield_follow_up','greatsword_range','greatsword_movement',
      'launcher_basic_special','launcher_range_special','launcher_move_special','launcher_followup_special',
      'bomb_range_special','bomb_followup_special','bomb_move_special','shuriken_range_special','shuriken_move_special','shuriken_followup_special'}
    reasons={
      'base_sword_follow_up':'직접 베기는 날카롭게, 잔상은 공기와 잉크가 스치는 소리로 분리',
      'whip_follow_up':'처음 감는 휘두름 뒤에 섬유가 조여 드는 짧은 압착음',
      'rapier_default':'힘이 차오르는 상승음을 충전 전체에 배치한 뒤 찌르기',
      'rapier_movement':'공통 순간이동 후 한 번 찌르기', 'rapier_follow_up':'공통 순간이동 후 서로 끊기는 5연속 찌르기',
      'dagger_range':'연막탄의 짧은 파열 뒤에 밀도 있는 분출음',
      'hammer_follow_up':'첫 강타는 충격, 세 번의 여진은 낮게 퍼지는 진동',
      'shield_default':'짧고 얇은 금속의 팅 · 방어 성공 청취 예시',
      'shield_range':'두꺼운 공명과 잉크 막의 전개 · 전자음 제외',
      'shield_movement':'밀어붙이는 마찰·공기 소리가 이동 중 계속 이어짐',
      'shield_follow_up':'잉크 시전과 방패가 자리 잡는 소리를 분리',
      'greatsword_range':'검의 소환 공명 뒤에 금속 칼날이 꽂히는 소리',
      'greatsword_movement':'2초간 연속 회전 + 칼날 악센트 · 반복 횟수로 음량을 나누지 않음',
      'launcher_basic_special':'충전 상승음 + 짧고 덜 무거운 포 발사음',
      'launcher_range_special':'기존 후보의 질감을 유지하며 저음과 꼬리를 줄임',
      'launcher_move_special':'실제 산탄총 원음을 사용한 묵직한 단발',
      'launcher_followup_special':'유탄의 뽕 + 절제된 퍼퍼벙 연쇄폭발',
      'bomb_range_special':'무거운 투척 + 공통 대형 폭발',
      'bomb_followup_special':'무거운 투척 + 착지 후 신관 1.5초 + 공통 대형 폭발',
      'bomb_move_special':'액체 잉크의 몸 변형 + 연속 비행 + 공통 대형 폭발',
      'shuriken_range_special':'공통 수리검 투척 + 기본 폭탄과 같은 폭발',
      'shuriken_move_special':'공통 순간이동 + 수리검 투척',
      'shuriken_followup_special':'공통 수리검 투척 + 2초 연속 회전'}
    for aid in changed:
        a=actions[aid];s=specs[aid];old=a['candidates'];a['candidateRevision']=2;a['reviewReason']=reasons[aid];a['note']=reasons[aid]
        a['scenario']={};scenario=a['scenario']
        if aid in ['rapier_default','launcher_basic_special']:
            scenario['charge']={'min':s['chargeMin'],'max':s['chargeMax'],'default':s['chargeMax'],'label':'충전 시간'}
        if aid in ['greatsword_movement','shield_range','shield_movement','bomb_move_special','shuriken_followup_special']:
            if aid=='greatsword_movement':actual=s['movement']['duration']
            elif aid=='shield_range':actual=s['effect']['duration']
            elif aid=='shield_movement':actual=s['movement']['duration']
            elif aid=='bomb_move_special':actual=s['missile']['distance']/s['missile']['speed']
            else:actual=s['ranged']['distance']/s['ranged']['speed']
            scenario['sustain']={'min':.3,'max':10,'default':3 if aid in ['shield_movement','bomb_move_special'] else actual,'actual':actual,'label':'청취용 지속 시간'}
            if aid=='shield_movement':a['note']+=' · 게임 이동은 0.3초. 기본 청취 예시는 3초이며 게임 시간 버튼으로 비교할 수 있습니다.'
            if aid=='bomb_move_special':a['note']+=' · 최대 비행 10초, 기본 청취 예시는 3초입니다.'
        if aid in ['launcher_followup_special','bomb_range_special','bomb_followup_special','shuriken_range_special']:
            scenario['impactDelay']={'min':.2,'max':3,'default':1,'label':'충돌·착지까지 청취 시간'}
            a['note']+=' · 폭발은 청취용 충돌 시나리오입니다. 실제 게임에서는 충돌·착지 사건에 연결합니다.'
        if aid=='shield_default':scenario['impactDelay']={'min':.02,'max':.19,'default':.12,'label':'방어 성공 청취 시점'}
        recipes=[]
        for i in range(10):
            original=old[i]['layers'][0]['audio'];ls=[];activation=a['impact']
            if aid=='base_sword_follow_up':
                first=old[i]['layers'][0]['audio'];airy=variant(breeze,i,tail=.27,high=550,low=7500,db=-23)
                wetkey=variant(wet,i,tail=.08,db=-26);echo=composite([(airy,0,1,False),(wetkey,.035,.22,False)],'ink afterimage',-22)
                ls=[layer('slash','직접 베기',first,[a['markers'][0]]),layer('echo','잔상 효과',echo,[a['markers'][1]],.9)]
            elif aid=='whip_follow_up':
                squeeze=variant(fabric,i,tail=.18,pitch=.9,low=5000,db=-20)
                vine=variant(seq('Magic_Spells/nature_spell_vines_tree_impact_',3),i,tail=.1,low=4200,db=-23)
                tight=composite([(squeeze,0,1,False),(vine,.02,.35,False)],'ink knot tightening',-20)
                ls=[layer('wrap','처음 감기',original,[a['markers'][0]]),layer('tighten','매듭 조이기',tight,[a['markers'][1]],.9)]
            elif aid in ['rapier_default','launcher_basic_special']:
                rise=variant(charge,i,mode='charge',length=.8,high=90,low=6500,db=-19)
                hit=original if aid=='rapier_default' else variant(cannon,i,tail=.30,pitch=1.25,high=150,low=5200,db=-18)
                ls=[layer('charge','기 모으기',rise,[0],.85,'absolute','onset',fit='charge',duration='charge'),layer('release','찌르기' if aid=='rapier_default' else '가벼운 포 발사',hit,[0],1,'release')]
            elif aid in ['rapier_movement','rapier_follow_up','shuriken_move_special']:
                ls=[shared('teleport','순간이동','teleport',[max(0,activation-.09)],anchor='absolute'),layer('attack','찌르기' if aid.startswith('rapier') else '투척',original,a['markers'])]
            elif aid=='dagger_range':
                pop=variant(small,i,tail=.07,high=210,low=4200,db=-21)
                hiss=variant(['Explosion_Fire_Gas/gas_leak_long_burst_01.wav']+seq('Explosion_Fire_Gas/gas_leak_short_burst_',3),i,mode='full',tail=.7,high=350,low=5500,db=-22)
                ls=[layer('burst','연막탄 파열',pop,[activation],.85),layer('smoke','연막 분출',hiss,[activation+.035],.85,align='onset')]
            elif aid=='hammer_follow_up':
                hit=variant(earth,i,tail=.26,db=-18);wavekey=variant(rumble,i,tail=.36,low=750,high=45,db=-20)
                ls=[layer('slam','첫 강타',hit,[a['markers'][0]]),layer('aftershock','땅속 여진',wavekey,a['markers'][1:],.9)]
            elif aid=='shield_default':
                ting=variant(ring,i,tail=.20,pitch=1.15,high=900,low=11000,db=-24)
                ls=[layer('parry','받아치기 · 팅',ting,[0],.8,'impactDemo')]
            elif aid=='shield_range':
                form=variant(conjure,i,mode='full',tail=.5,high=65,low=2200,db=-19)
                field=variant(portal,i,mode='loop',length=1,high=55,low=1200,db=-24)
                ls=[layer('deploy','두꺼운 보호막 전개',form,[activation],.95,align='onset'),layer('field','보호막 공명',field,[0],.7,'activation','onset',loop=True,duration='sustain')]
            elif aid=='shield_movement':
                friction=variant(seq('Metal/metal_scrape_deep_grind_squeak_',7),i,mode='loop',length=.9,high=120,low=2300,db=-21)
                body=variant(deep,i,mode='loop',length=.8,high=90,low=1800,db=-21)
                ls=[layer('scrape','방패 밀기 마찰',friction,[0],.7,'activation','onset',loop=True,duration='sustain'),layer('push','밀어붙이는 공기',body,[0],.85,'activation','onset',loop=True,duration='sustain')]
            elif aid=='shield_follow_up':
                casting=variant(conjure,i,mode='charge',length=.2,low=3300,db=-21)
                place=variant(blade,i,tail=.18,pitch=.85,low=3800,db=-22)
                ls=[layer('cast','잉크 시전',casting,[0],.85,align='onset'),layer('place','미끼 방패 설치',place,[activation],.9)]
            elif aid=='greatsword_range':
                summon=variant(['Guns_Weapons/Knife_Sword_Pick/unsheathe_sword_with_ringout.wav']+conjure,i,mode='charge',length=max(.2,activation),high=150,low=6000,db=-21)
                metal=variant(blade,i,tail=.4,pitch=.9,high=240,low=7500,db=-19)
                ls=[layer('summon','검 소환',summon,[0],.85,align='onset'),layer('blade','금속 칼날 내려꽂기',metal,[activation],1)]
            elif aid=='greatsword_movement':
                turn=variant(spins,i,mode='loop',length=.5,high=90,low=6500,db=-17)
                edge=variant(seq('Whooshes/whoosh_swish_high_big_',5),i,tail=.16,high=320,db=-21)
                ls=[layer('spin','연속 회전',turn,[0],1,'activation','onset',loop=True,duration='sustain'),layer('blade','칼날 회전 악센트',edge,[0],.65,'activation',repeatEvery=.25,duration='sustain')]
            elif aid=='launcher_range_special':
                light=cut(original,tail=.16 if i<5 else .11,high=500 if i<5 else 700,low=9500 if i<5 else 10500,db=-23)
                ls=[layer('laser','가벼운 레이저',light,[activation],.85)]
            elif aid=='launcher_move_special':
                blast=variant(shot,i,tail=.35,high=60,low=6500,db=-18)
                ls=[layer('shotgun','산탄총 발사',blast,[activation],1)]
            elif aid=='launcher_followup_special':
                launch=variant(grenade,i,tail=.25,pitch=.94,high=80,low=4500,db=-19)
                pop=variant(small,i,tail=.18,low=3500,db=-24)
                cluster=composite([(pop,0,.65,False),(pop,.12,.45,False),(pop,.25,.85,False)],'quiet grenade cluster',-24)
                ls=[layer('launch','유탄 · 뽕',launch,[activation]),layer('explosion','연쇄폭발 · 퍼퍼벙',cluster,[0],.68,'impactDemo','onset')]
            elif aid in ['bomb_range_special','bomb_followup_special']:
                toss=variant(deep,i,tail=.36,pitch=.82,high=45,low=4200,db=-19)
                ls=[layer('throw','무거운 폭탄 투척',toss,[activation],.95),shared('explosion','공통 대형 폭발','large_explosion',[specs[aid].get('bomb',{}).get('fuseSeconds',0)],anchor='impactDemo')]
            elif aid=='bomb_move_special':
                liquid=variant(wet,i,tail=.23,pitch=.72,high=100,low=3800,db=-20)
                stretch=variant(fabric,i,tail=.17,pitch=.8,low=2500,db=-24)
                transform=composite([(liquid,0,1,False),(stretch,.04,.4,True)],'liquid ink morph',-19)
                flight=variant(['Sci-Fi/sci-fi_vehicle_spaceship_jet_engine_loop1.wav','Sci-Fi/sci-fi_vehicle_spaceship_jet_engine_loop2.wav','Sci-Fi/sci-fi_vehicle_spaceship_jet_engine_loop3.wav']+portal,i,mode='loop',length=1,high=100,low=3000,db=-22)
                ls=[layer('morph','액체 잉크 변신',transform,[0],.9,align='onset'),layer('flight','미사일 연속 비행',flight,[0],.85,'activation','onset',loop=True,duration='sustain'),shared('explosion','공통 대형 폭발','large_explosion',[0],anchor='sustainEnd')]
            elif aid=='shuriken_range_special':
                ls=[shared('throw','공통 수리검 투척','shuriken_throw',[activation],anchor='absolute'),shared('explosion','기본 폭탄과 같은 폭발','basic_explosion',[0])]
            elif aid=='shuriken_followup_special':
                spin=variant(spins,i,mode='loop',length=.4,pitch=1.2,high=360,low=7500,db=-20)
                ls=[shared('throw','공통 수리검 투척','shuriken_throw',[activation],anchor='absolute'),layer('spin','수리검 연속 회전',spin,[0],.9,'activation','onset',loop=True,duration='sustain')]
            assert ls,aid
            for part in ls:
                if part.get('group'):
                    part['audio']=groups[part['group']]['variants'][i%len(groups[part['group']]['variants'])]['audio']
            recipes.append({'number':i+1,'id':aid+f'-v2-{i+1:02}','name':f'{i+1:02} · '+ ' → '.join(l['name'] for l in ls),
                'layers':ls,'mixed':len(ls)>1,'reason':reasons[aid]})
        a['candidates']=recipes
    for aid,a in actions.items():a.setdefault('candidateRevision',1)
    payload.update(revision=2,sharedGroups=groups,defaultWeapon='shield',reviewedActions=sorted(changed),basicExplosion=basic_key)
    z.close()
