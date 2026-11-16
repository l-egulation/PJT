"""Apply a verified hammer audition export, preserving the chosen stem offsets."""
import argparse
import math
from pathlib import Path
import render_basic_attack_audio as audio


def shifted_mix(layers, minimum):
    n=max([round(minimum*audio.RATE)]+[max(0,round(offset*audio.RATE)+len(samples)) for samples,offset in layers])
    result=[0.0]*n
    for samples,offset in layers:
        start=round(offset*audio.RATE)
        for i,value in enumerate(samples):
            if 0<=start+i<n:result[start+i]+=value
    return result


def main():
    parser=argparse.ArgumentParser(description=__doc__);parser.add_argument('--selection',required=True,type=Path);args=parser.parse_args()
    user=audio.load(args.selection);w=next(w for w in user['weapons'] if w['weapon']=='hammer')
    choice=w.get('choice')
    number=choice['number'] if choice else w['selected']+1
    if not choice:assert w.get('favorites')==[number], 'An unconfirmed choice must match the sole favorite.'
    candidate=next(c for c in w['candidates'] if c['number']==number)
    assert candidate.get('recipeId','').startswith('hammer-r5-')
    global_ms=choice.get('offsetMs',0) if choice else w['offsets'][number-1]
    offsets=(choice or {}).get('layerOffsetsMs') or w['layerOffsets'][number-1]
    assert all(math.isfinite(x) for x in [global_ms,*offsets.values()])
    assert -200<=global_ms<=200 and all(-400<=x<=400 for x in offsets.values())
    revision=audio.load(audio.AUDITION/'source/revision-02/recipes.json')
    known=next(c for c in revision['candidates']['hammer'] if c['recipeId']==candidate['recipeId'])
    def checked(record,key):
        raw=(audio.AUDITION/'source'/key).read_bytes()
        assert audio.sha(raw)==record['sha256']==revision['audio'][key]['sha256'],key
        assert Path(key).name==record['file']
        return audio.decode(raw)
    forms=audio.load(audio.MAIN/'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json')['forms']
    form=next(f for f in forms if f['id']=='hammer')
    timings={e['id']:e for e in audio.load(audio.MAIN/'unity/Assets/Deviant/Audio/Resources/WeaponSounds/Timing.json')['entries']}
    trial=next(t['scale'] for t in audio.load(audio.MAIN/'data/combat/playtest_environment.json')['basicSpeedTrials'] if t['form']=='hammer')
    assert trial==1, 'Re-evaluate real-time global offset if default speed changes.'
    mixes=[]
    for hit,basic in enumerate(form['basics'],1):
        key=f'attack{hit}'
        if hit==1:layers=[(checked(candidate['variants'][key],known['variants'][key]),global_ms/1000)]
        else:layers=[(checked(record,known['layers'][key][name]),(global_ms+offsets[name])/1000) for name,record in candidate['layers'][key].items()]
        mixes.append(shifted_mix(layers,basic['duration']))
    peak=max(abs(s) for mix in mixes for s in mix)
    # SelectedAudioClips has 0.8 runtime gain. Preserve the balance with common headroom.
    headroom=min(1,.795/peak) if peak else 1
    recipe=audio.load(audio.SELECTED/'source/recipe.json')
    selection={'number':number,'file':candidate['recipeId'],'recipeId':candidate['recipeId'],'offsetMs':global_ms,
        'layerOffsetsMs':offsets,'status':'applied-locally','sourceChoice':'exported selected candidate and sole favorite' if not choice else 'confirmed export',
        'inputSha256':audio.sha(args.selection.read_bytes()),'runtimeHeadroomGain':headroom,'layers':candidate['layers'],'variants':candidate['variants']}
    recipe['weapons']['hammer']=selection;recipe['pending']=[]
    recipe['outputs']=[x for x in recipe['outputs'] if not x['actionId'].startswith('hammer_basic_')]
    deployment_path=audio.MAIN/'docs/assets/manifests/unity-deployment.json';deployment=audio.load(deployment_path)
    bytarget={x['target']:x for x in deployment['files']}
    for i,(basic,mix) in enumerate(zip(form['basics'],mixes)):
        action=f'hammer_basic_{i}';timing=timings[action];rate=timing['nominalDuration']/basic['duration']
        raw=audio.runtime_bytes([s*headroom for s in mix],rate)
        name=f'sfx_weapon_{action}_01_v01.wav';target='unity/Assets/Deviant/Audio/SFX/Weapons/'+name
        (audio.SELECTED/'runtime'/name).write_bytes(raw);(audio.MAIN/target).write_bytes(raw)
        bytarget[target].update({'bytes':len(raw),'sha256':audio.sha(raw),'source':{'asset':'audio.gamemaster-selected','path':'audio/gamemaster-selected/runtime/'+name}})
        recipe['outputs'].append({'actionId':action,'file':name,'sha256':audio.sha(raw),'bytes':len(raw),'playbackRate':rate,'nominalDuration':timing['nominalDuration'],'source':candidate['recipeId'],'layerOffsetsMs':offsets if i==1 else {}})
    (audio.SELECTED/'source/hammer-input-selection.json').write_bytes(args.selection.read_bytes())
    audio.dump(audio.SELECTED/'source/recipe.json',recipe);audio.dump(audio.MAIN/'data/audio/basic-attack-selections.json',recipe);audio.dump(deployment_path,deployment)
    selections=audio.load(audio.AUDITION/'source/selections.json');selections['hammer']=selection;audio.dump(audio.AUDITION/'source/selections.json',selections)
    audio.register_asset(audio.SELECTED,'audio.gamemaster-selected','user-selected; local Unity deployment')
    print({'hammer':number,'offsetMs':global_ms,'layerOffsetsMs':offsets,'runtimeHeadroomGain':headroom,'appliedClips':2})


if __name__=='__main__':main()
