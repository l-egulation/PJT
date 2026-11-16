"""Render approved basic attacks and a second hammer/launcher audition from purchased PCM sources."""
import argparse
import array
import base64
import hashlib
import importlib.util
import io
import json
import math
from pathlib import Path
import warnings
import wave
import zipfile
warnings.filterwarnings('ignore', category=DeprecationWarning)
import audioop

RES = Path(__file__).resolve().parents[1]
MAIN = RES.parent
AUDITION = RES / 'audio/gamemaster-audition'
SELECTED = RES / 'audio/gamemaster-selected'
RATE = 48000
spec = importlib.util.spec_from_file_location('audition_builder', AUDITION / 'source/build.py')
builder = importlib.util.module_from_spec(spec)
spec.loader.exec_module(builder)
load, dump, sha = builder.read_json, builder.write_json, builder.sha


def decode(raw):
    with wave.open(io.BytesIO(raw)) as w:
        channels, rate, width = w.getnchannels(), w.getframerate(), w.getsampwidth()
        assert width == 2
        pcm = w.readframes(w.getnframes())
    if channels == 2:
        pcm = audioop.tomono(pcm, 2, .5, .5)
    else:
        assert channels == 1
    if rate != RATE:
        pcm, _ = audioop.ratecv(pcm, 2, 1, rate, RATE, None)
    return [v/32768 for v in array.array('h', pcm)]


def wav_bytes(samples):
    stream = io.BytesIO()
    pcm = array.array('h', (int(round(max(-.999, min(.999, s))*32767)) for s in samples))
    with wave.open(stream, 'wb') as w:
        w.setparams((1, 2, RATE, 0, 'NONE', 'not compressed'))
        w.writeframes(pcm.tobytes())
    return stream.getvalue()


def stats(samples):
    peak = max(abs(s) for s in samples)
    if peak == 0:
        return 0, 0, 0
    active = [s for s in samples if abs(s) > peak*.03]
    rms = math.sqrt(sum(s*s for s in active)/len(active))
    window = int(RATE*.008)
    energy = [sum(s*s for s in samples[i:i+window])/len(samples[i:i+window]) for i in range(0,len(samples),window)]
    at = min(len(samples)-1, int((max(range(len(energy)), key=energy.__getitem__)+.5)*window))
    return peak, rms, at/RATE


def level(samples, target_db):
    peak, rms, _ = stats(samples)
    if peak == 0:
        return list(samples)
    gain = min(10**(target_db/20)/rms, .85/peak)
    return [s*gain for s in samples]


def crop_transient(samples, after=.25, before=.008, lowpass=None):
    _, _, peak = stats(samples)
    start=max(0,int((peak-before)*RATE));end=min(len(samples),int((peak+after)*RATE))
    out=samples[start:end]
    if lowpass:
        alpha=1-math.exp(-2*math.pi*lowpass/RATE);previous=0
        for i,s in enumerate(out):
            previous += alpha*(s-previous);out[i]=previous
    fade=min(int(.025*RATE),len(out)//4);attack=min(int(.002*RATE),len(out)//8)
    for i in range(attack):out[i]*=i/max(1,attack)
    for i in range(fade):out[-1-i]*=i/max(1,fade)
    return out


def place(layers, minimum=0, peak_time=None):
    # Layers specify sound and desired energy peak time; negative starts are cropped at attack start.
    peak_time=peak_time or (lambda samples:stats(samples)[2])
    length=max([int(minimum*RATE)]+[max(0,int((at-peak_time(samples))*RATE))+len(samples) for samples,at in layers])
    mix=[0.0]*length
    for samples,at in layers:
        start=round((at-peak_time(samples))*RATE)
        for i,s in enumerate(samples):
            j=start+i
            if 0<=j<len(mix):mix[j]+=s
    peak=max(abs(s) for s in mix)
    limiter=min(1,.92/peak) if peak else 1
    return [s*limiter for s in mix]


def hammer_floor_contact(basic):
    catalog=load(MAIN/'unity/Assets/Deviant/Combat/Resources/CharacterV2/HeavyWeaponCatalog.json')
    art=next(s for s in catalog['sets'] if s['form']=='hammer')
    clip=next(c for c in art['clips'] if c['name']=='attack2')
    # Visually checked: 15 is the first grounded hammer/dust frame; 14 is still overhead.
    ref='CharacterBasicOutline/hammer-a/15'
    frame=clip['frames'].index(ref);source=sum(clip['seconds'][:frame])
    pivot=clip['sourceImpact'];duration=sum(clip['seconds'])
    at=source/pivot*basic['impact'] if source<pivot else basic['impact']+(source-pivot)/(duration-pivot)*(basic['duration']-basic['impact'])
    return {'frame':ref,'frameIndex':frame,'sourceSeconds':source,'seconds':at}


def sliding_peak(samples):
    # Sample-by-sample windows keep the landmark stable when audio is shifted.
    width=min(len(samples),round(.008*RATE));energy=sum(s*s for s in samples[:width])
    best=energy;index=0
    for i in range(width,len(samples)):
        energy+=samples[i]**2-samples[i-width]**2
        if energy>best:best=energy;index=i-width+1
    return (index+width/2)/RATE


def floor_mix(swing, thud, contact, duration):
    # User wants one overlapping second-hit accent, not swish then impact.
    # Keep the swish tail and align both energy peaks to the incoming swing.
    accent=max(0,contact-.04)
    layers=floor_layers(swing,thud,contact,duration)
    length=max(map(len,layers.values()))
    return [sum(values[i] if i<len(values) else 0 for values in layers.values()) for i in range(length)]


def floor_layers(swing, thud, contact, duration):
    accent=max(0,contact-.04)
    layers={name:place([(samples,accent)],minimum=duration,peak_time=sliding_peak)
            for name,samples in [('swing',swing),('impact',thud)]}
    # One shared gain keeps the balance and prevents clipping at any relative shift.
    gain=min(1,.92/sum(max(map(abs,values)) for values in layers.values()))
    return {name:[sample*gain for sample in values] for name,values in layers.items()}


def runtime_bytes(actual_mix, playback_rate, runtime_gain=.8):
    # Compensate for the existing runtime's pitch/rate and gain at the current default attack speed.
    raw=wav_bytes([s/runtime_gain for s in actual_mix])
    with wave.open(io.BytesIO(raw)) as w:pcm=w.readframes(w.getnframes())
    pcm,_=audioop.ratecv(pcm,2,1,RATE,max(1,round(RATE*playback_rate)),None)
    stream=io.BytesIO()
    with wave.open(stream,'wb') as w:w.setparams((1,2,RATE,0,'NONE','not compressed'));w.writeframes(pcm)
    return stream.getvalue()


def register_asset(folder, asset_id, status):
    relative=folder.relative_to(RES).as_posix()
    files=sorted(p.relative_to(folder).as_posix() for p in folder.rglob('*') if p.is_file() and p.name!='asset.json' and '__pycache__' not in p.parts)
    dump(folder/'asset.json',{'format':'deviant-resource-asset-v1','id':asset_id,'path':relative,'status':status,'files':files})
    catalog=load(RES/'catalog.json');catalog['assets']=[x for x in catalog['assets'] if x['id']!=asset_id]
    catalog['assets'].append({'id':asset_id,'path':relative,'fileCount':len(files)});catalog['assets'].sort(key=lambda x:x['id']);dump(RES/'catalog.json',catalog)
    manifest=load(RES/'generated-assets.json');manifest['files']=[x for x in manifest['files'] if not x['path'].startswith(relative+'/')]
    for name in files:
        raw=(folder/name).read_bytes();manifest['files'].append({'path':relative+'/'+name,'sha256':sha(raw),'bytes':len(raw)})
    dump(RES/'generated-assets.json',manifest)


def main():
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive',required=True,type=Path)
    parser.add_argument('--selection',required=True,type=Path)
    parser.add_argument('--apply-local',action='store_true')
    args=parser.parse_args()
    source=load(args.selection);source_weapons={w['weapon']:w for w in source['weapons']}
    numbers={'base_sword':5,'whip':9,'rapier':10,'dagger':9,'shield':1,'greatsword':4,'bomb':1,'unarmed':10}
    combat=MAIN/'unity/Assets/Deviant/Combat/Resources'
    forms={f['id']:f for f in load(combat/'MeleeCatalog.json')['forms']}
    speed={x['form']:x['scale'] for x in load(MAIN/'data/combat/playtest_environment.json')['basicSpeedTrials']}
    audio_root=MAIN/'unity/Assets/Deviant/Audio'
    timing={x['id']:x for x in load(audio_root/'Resources/ApprovedCombatAudio/Catalog.json')['entries']}
    for x in load(audio_root/'Resources/WeaponSounds/Timing.json')['entries']:timing.setdefault(x['id'],x)
    for path in [SELECTED/'source',SELECTED/'runtime',AUDITION/'source/revision-02']:
        path.mkdir(parents=True,exist_ok=True)
    selection={}
    recipe={'format':'deviant-basic-audio-selection-v1','scope':'basic attacks only','pending':['hammer'],'weapons':{},'outputs':[]}
    configs=load(AUDITION/'source/weapons.json')
    with zipfile.ZipFile(args.archive) as archive:
        def original(entry):return archive.read(entry)
        for weapon in [*numbers,'shuriken']:
            source_weapon='bomb' if weapon=='shuriken' else weapon
            number=2 if weapon=='shuriken' else numbers[weapon]
            candidate=next(c for c in source_weapons[source_weapon]['candidates'] if c['number']==number)
            entry=candidate['archiveEntry'];raw=original(entry)
            assert sha(raw)==candidate['sha256'], 'Selection/archive mismatch: '+entry
            (SELECTED/'source'/candidate['file']).write_bytes(raw)
            samples=level(decode(raw),-22)
            if weapon=='shuriken':
                configs[weapon]['candidates'][9]=entry
                preview_number=10
            else:preview_number=number
            selection[weapon]={'number':preview_number,'file':candidate['file'],'archiveEntry':entry,'sha256':sha(raw),'offsetMs':0,'status':'applied-locally' if args.apply_local else 'selected','sourceChoice':f'{source_weapon} {number}'}
            recipe['weapons'][weapon]=dict(selection[weapon],defaultSpeed=speed.get(weapon,1),comparisonRmsDb=-22)
            for i,basic in enumerate(forms[weapon]['basics']):
                action=f'{weapon}_basic_{i}';entry_t=timing[action];trial=speed.get(weapon,1)
                actual=place([(samples,basic['impact']/trial)],minimum=basic['duration']/trial)
                playback=entry_t['nominalDuration']/basic['duration']*trial
                output=runtime_bytes(actual,playback)
                filename=f'sfx_weapon_{action}_01_v01.wav';path=SELECTED/'runtime'/filename;path.write_bytes(output)
                recipe['outputs'].append({'actionId':action,'file':filename,'sha256':sha(output),'bytes':len(output),'playbackRate':playback,
                    'impactSeconds':basic['impact']/trial,'nominalDuration':entry_t['nominalDuration'],'source':candidate['file']})
        # Hammer: same preferred #9 swing, louder, plus a separate second-hit floor transient.
        swing_raw=original('Whooshes/whoosh_low_deep_soft_01.wav')
        floor_sets=[('rock_impact_small_hit_01',6,.22,'짧은 돌바닥 · +6 dB'),('rock_impact_heavy_slam_01',6,.28,'묵직한 돌바닥 · +6 dB'),
            ('rock_impact_heavy_slam_01',9,.28,'묵직한 돌바닥 · +9 dB'),('rock_impact_heavy_slam_02',9,.28,'낮은 바닥 충격 · +9 dB'),
            ('rock_impact_heavy_slam_03',9,.35,'넓은 바닥 충격 · +9 dB'),('rock_impact_heavy_slam_02',12,.28,'강한 내려치기 · +12 dB'),
            ('rock_smashable_hit_impact_large_01',9,.25,'돌 깨짐 · 짧게'),('rock_impact_spike_trap_01',9,.22,'단단한 바닥 · 짧게'),
            ('rock_impact_small_hit_03',12,.16,'선명한 바닥 충격 · +12 dB'),('rock_impact_heavy_slam_04',12,.2,'무거운 바닥 충격 · 짧게')]
        contact=hammer_floor_contact(forms['hammer']['basics'][1])
        revision={'revision':5,'candidateRevisions':{'hammer':5,'launcher':3},'pending':['hammer'],'audio':{},'candidates':{'hammer':[],'launcher':[]}}
        for i,(floor,gain,tail,label) in enumerate(floor_sets,1):
            floor_entry='Impacts_Smashable/'+floor+'.wav';floor_raw=original(floor_entry)
            (AUDITION/'source/audio'/Path(floor_entry).name).write_bytes(floor_raw)
            swing=level(decode(swing_raw),-22+gain)
            thud=level(crop_transient(decode(floor_raw),after=tail,before=0,lowpass=4500),-17+gain*.25)
            variants={}
            for hit,basic in enumerate(forms['hammer']['basics'],1):
                mixed=floor_mix(swing,thud,contact['seconds'],basic['duration']) if hit==2 else place([(swing,basic['impact'])],minimum=basic['duration'])
                raw=wav_bytes(mixed)
                key=f'revision-02/hammer-{i:02}-hit-{hit}.wav';(AUDITION/'source'/key).write_bytes(raw)
                revision['audio'][key]={**builder.audio_payload(raw,Path(key).name),'nominalDuration':basic['duration'],'runtimeGain':1,'recipe':{'swing':'Whooshes/whoosh_low_deep_soft_01.wav','boostDb':gain,'floor':floor_entry if hit==2 else None,'floorTailSeconds':tail,'floorContact':contact if hit==2 else None,'floorAlignment':'shared-energy-peak','accentSeconds':max(0,contact['seconds']-.04) if hit==2 else basic['impact'],'swingEndsAtContact':False}}
                variants[f'attack{hit}']=key
            stems={}
            for name,samples in floor_layers(swing,thud,contact['seconds'],forms['hammer']['basics'][1]['duration']).items():
                raw=wav_bytes(samples);key=f'revision-02/hammer-{i:02}-hit-2-{name}.wav';(AUDITION/'source'/key).write_bytes(raw)
                revision['audio'][key]={**builder.audio_payload(raw,Path(key).name),'nominalDuration':forms['hammer']['basics'][1]['duration'],'runtimeGain':1,'accentSeconds':max(0,contact['seconds']-.04)}
                stems[name]=key
            revision['candidates']['hammer'].append({'number':i,'name':label,'authored':True,'fixedMix':True,'variants':variants,'layers':{'attack2':stems},'recipeId':f'hammer-r5-{i:02}'})
        # Launcher: one source only, short transient, no stacked layers or added tail.
        launcher_sources=[f'gun_silenced_pistol1_shot_{i:02}.wav' for i in range(1,5)]+[f'gun_silenced_pistol2_shot_{i:02}.wav' for i in range(1,5)]+[f'gun_silenced_semi_sub_shot_{i:02}.wav' for i in [1,2]]
        for i,name in enumerate(launcher_sources,1):
            entry='Guns_Weapons/Guns_Silenced/'+name;raw=original(entry)
            (AUDITION/'source/audio'/name).write_bytes(raw)
            tail=[.10,.14,.18,.12,.10,.14,.18,.12,.10,.14][i-1]
            sample=level(crop_transient(decode(raw),after=tail,before=.004,lowpass=5000),-24)
            raw=wav_bytes(sample);key=f'revision-02/launcher-{i:02}.wav';(AUDITION/'source'/key).write_bytes(raw)
            revision['audio'][key]={**builder.audio_payload(raw,Path(key).name),'number':i,'name':f'단일 발사음 {i:02} · {round(tail*1000)}ms 꼬리','fixedMix':True,
                'recipe':{'source':entry,'sourceSha256':sha(original(entry)),'tailSeconds':tail,'lowpassHz':5000,'layers':1,'rmsDb':-24}}
            revision['candidates']['launcher'].append(key)
    launcher_file='sfx_weapon_launcher_basic_0_01_v01.wav'
    launcher_choice={'number':11,'file':launcher_file,'sha256':sha((audio_root/'SFX/Weapons'/launcher_file).read_bytes()),'offsetMs':0,'status':'approved-current','sourceChoice':'current-runtime'}
    selection['launcher']=launcher_choice
    recipe['weapons']['launcher']=launcher_choice
    existing_path=SELECTED/'source/recipe.json'
    if existing_path.exists():
        existing=load(existing_path)
        if existing.get('weapons',{}).get('hammer',{}).get('status')=='applied-locally':
            selection['hammer']=existing['weapons']['hammer'];recipe['weapons']['hammer']=selection['hammer']
            recipe['pending']=[]
            recipe['outputs'] += [x for x in existing['outputs'] if x['actionId'].startswith('hammer_basic_')]
    dump(AUDITION/'source/revision-02/recipes.json',revision)
    dump(AUDITION/'source/selections.json',selection)
    dump(AUDITION/'source/weapons.json',configs)
    dump(SELECTED/'source/recipe.json',recipe)
    (SELECTED/'source/input-selection.json').write_bytes(args.selection.read_bytes())
    (MAIN/'data/audio/basic-attack-selections.json').write_text(json.dumps(recipe,ensure_ascii=False,indent=2)+'\n',encoding='utf-8')
    register_asset(SELECTED,'audio.gamemaster-selected','user-selected; local Unity deployment' if args.apply_local else 'user-selected')
    if args.apply_local:
        deployment_path=MAIN/'docs/assets/manifests/unity-deployment.json';deployment=load(deployment_path)
        bytarget={x['target']:x for x in deployment['files']}
        for output in recipe['outputs']:
            target='unity/Assets/Deviant/Audio/SFX/Weapons/'+output['file'];record=bytarget[target]
            record.update({'sha256':output['sha256'],'bytes':output['bytes'],'source':{'asset':'audio.gamemaster-selected','path':'audio/gamemaster-selected/runtime/'+output['file']}})
            (MAIN/target).write_bytes((SELECTED/'runtime'/output['file']).read_bytes())
        dump(deployment_path,deployment)
    print(json.dumps({'selectedWeapons':list(selection),'runtimeClips':len(recipe['outputs']),'hammerCandidates':10,'launcherCandidates':10,'appliedLocal':args.apply_local}))


if __name__=='__main__':main()
