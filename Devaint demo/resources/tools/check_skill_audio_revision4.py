"""Signal/provenance regression checks; not a listening approval."""
import math
import numpy as np
import render_basic_attack_audio as m

root=m.RES/'audio/gamemaster-skill-audition';p=m.load(root/'source/recipes.json');assert p['revision']==4
actions={a['id']:a for a in p['actions']};audio=p['audio'];cache={}
def pcm(key):
    if key not in cache:
        a=audio[key];raw=(root/'source/audio'/a['file']).read_bytes();assert m.sha(raw)==a['sha256'],key
        cache[key]=np.asarray(m.decode(raw))
    return cache[key]
def rms(x):return float(np.sqrt(np.mean(x*x)))
def energy(key,lo,hi):
    x=pcm(key);f=np.fft.rfftfreq(len(x),1/m.RATE);s=abs(np.fft.rfft(x))**2
    return float(sum(s[(f>=lo)&(f<hi)])/sum(s))
def layer(a,i,id):return next(l for l in actions[a]['candidates'][i]['layers'] if l['id']==id)
assert 'reference' not in p
assert not any(a.get('edit',{}).get('mode')=='reference-excerpt' for a in audio.values())
def provenance(key,seen=None):
    seen=set() if seen is None else seen
    if key in seen:return
    seen.add(key);origin=audio[key]['source'];assert 'userReference' not in str(origin) and 'user-reference.mp4' not in str(origin),key
    if isinstance(origin,dict):
        if origin.get('processedSource') in audio:provenance(origin['processedSource'],seen)
        for part in origin.get('parts',[]):provenance(part['audio'],seen)
for a in actions.values():
    for r in a['candidates']:
        for l in r['layers']:provenance(l['audio'])
for group in p['sharedGroups'].values():
    for v in group['variants']:provenance(v['audio'])
for key,contract in p['frozenLayers'].items():
    aid,num,id=key.split('/');l=layer(aid,int(num)-1,id)
    assert l==contract['layer'];assert audio[l['audio']]['sha256']==contract['sha256'];pcm(l['audio'])
pop_gaps=[];laser_levels=[]
for i in range(10):
    slash=layer('base_sword_follow_up',i,'echo');assert energy(slash['audio'],0,400)<.04
    assert 'foley_cloth' in str(audio[slash['audio']]['source'])
    knot=layer('whip_follow_up',i,'pop');assert 'Explosion_Fire_Gas/' in str(audio[knot['audio']]['source'])
    pop=layer('dagger_range',i,'burst');bed=layer('dagger_range',i,'smoke')
    gap=20*math.log10(rms(pcm(pop['audio']))/rms(pcm(bed['audio'])));pop_gaps.append(gap);assert gap>12,(i,gap)
    assert energy(bed['audio'],1000,24000)<.005
    for aid in ['shield_range','shield_movement']:
        ls=actions[aid]['candidates'][i]['layers'];assert len(ls)==1 and not ls[0].get('loop');assert len(pcm(ls[0]['audio']))/m.RATE<.7
    assert [l['id'] for l in actions['shield_follow_up']['candidates'][i]['layers']]==['place']
    assert [l['id'] for l in actions['launcher_basic_special']['candidates'][i]['layers']]==['release']
    laser=layer('launcher_range_special',i,'laser');x=pcm(laser['audio']);level=20*math.log10(m.stats(x.tolist())[1]);laser_levels.append(level)
    assert level>-18;assert energy(laser['audio'],0,250)<.002;assert rms(x[:144])<rms(x)*.06;assert rms(x[-480:])<rms(x)*.04
    spin=layer('shuriken_followup_special',i,'spin');assert spin['audio'].startswith('r2/') and spin['rate']==1.12 and spin['previousRevision']==2
current=0
for key,a in audio.items():
    if key.startswith('current/'):
        assert (root/'source/audio'/a['file']).read_bytes()==(m.MAIN/a['source']).read_bytes();current+=1
    if key.startswith('r4/'):assert max(abs(pcm(key)))<.9
for entry in m.load(m.RES/'generated-assets.json')['files']:
    if entry['path'].startswith('audio/gamemaster-skill-audition/'):
        raw=(m.RES/entry['path']).read_bytes();assert m.sha(raw)==entry['sha256'] and len(raw)==entry['bytes'],entry['path']
print(dict(frozenLayers=len(p['frozenLayers']),currentFilesUnchanged=current,candidateReferenceRecordingUses=0,minimumPopToBedRmsDb=round(min(pop_gaps),1),minimumLaserActiveRmsDb=round(min(laser_levels),1),spinRateVsRevision2=1.12))
