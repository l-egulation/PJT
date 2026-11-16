"""Validate fixed/link contracts, source isolation and soft short-event endings."""
import numpy as np
import render_basic_attack_audio as m

root=m.RES/'audio/gamemaster-skill-audition';p=m.load(root/'source/recipes.json');assert p['revision']==5
a={x['id']:x for x in p['actions']};audio=p['audio'];cache={}
def pcm(key):
    if key not in cache:
        v=audio[key];raw=(root/'source/audio'/v['file']).read_bytes();assert m.sha(raw)==v['sha256'];cache[key]=np.asarray(m.decode(raw))
    return cache[key]
def rms(x):return np.sqrt(np.mean(x*x))
def band(key,low,high):
    x=pcm(key);f=np.fft.rfftfreq(len(x),1/m.RATE);power=abs(np.fft.rfft(x))**2
    return sum(power[(f>=low)&(f<high)])/sum(power)
def walk(key,seen=None):
    seen=set() if seen is None else seen
    if key in seen:return
    seen.add(key);s=audio[key]['source'];assert 'userReference' not in str(s) and '.mp4' not in str(s),key
    if isinstance(s,dict):
        if s.get('processedSource') in audio:walk(s['processedSource'],seen)
        for part in s.get('parts',[]):walk(part['audio'],seen)
for action in a.values():
    for r in action['candidates']:
        for l in r['layers']:walk(l['audio'])
assert a['base_sword_follow_up']['linkedAction']=='base_sword_default'
for i in range(10):
    whip=a['whip_follow_up']['candidates'][i]['layers'];assert whip==[p['fixedWhipLayer']]
    echo=a['base_sword_follow_up']['candidates'][i]['layers'];strong=a['base_sword_default']['candidates'][i]['layers']
    for e,s in zip(echo,strong):
        assert e['audio']==s['audio'] and e['gain']==s['gain']
        assert all(abs(te-ts-.045)<1e-8 for te,ts in zip(e['times'],s['times']))
        assert max(e['times'])<=.2000001
    triple=a['bomb_basic_special']['candidates'][i]['layers'];assert triple[:-1]==p['restoredTripleThrowLayers'][i]
    assert triple[0]['times']==[.2,.35,.5] and triple[0]['gain']==1
    assert 'whoosh_swish_high_big_' in str(audio[triple[0]['audio']]['source'])
    if i>=5:assert triple[1]['times']==[.2] and triple[1]['gain']==.42
    v=p['sharedGroups']['triple_explosion']['variants'][i];x=pcm(v['audio'])
    assert .19<=len(x)/m.RATE<=.26;assert x[0]==0 and x[-1]==0
    assert rms(x[-480:])<rms(x)*.04,'short explosion must fade to silence'
    for group in ['normal_explosion','large_explosion']:
        v=p['sharedGroups'][group]['variants'][i];origin=audio[v['audio']]['source'];assert 'ink-water' in origin['composite'];walk(v['audio'])
    for aid in ['dagger_range','launcher_basic_review','launcher_move_special','shield_follow_up','launcher_range_special']:
        key=a[aid]['candidates'][i]['layers'][0]['audio'];x=pcm(key)
        assert max(abs(x))<.9;assert abs(x[-1])<.00004
        assert band(key,5000,24000)<.003,(aid,i,'high-frequency residue')
    laser=a['launcher_range_special']['candidates'][i]['layers'][0]['audio'];assert band(laser,0,600)>.45
current=0
for key,v in audio.items():
    if key.startswith('current/'):
        assert (root/'source/audio'/v['file']).read_bytes()==(m.MAIN/v['source']).read_bytes();current+=1
for v in m.load(m.RES/'generated-assets.json')['files']:
    if v['path'].startswith('audio/gamemaster-skill-audition/'):
        raw=(m.RES/v['path']).read_bytes();assert len(raw)==v['bytes'] and m.sha(raw)==v['sha256'],v['path']
print(dict(fixedWhipVariants=10,linkedStrongVariants=10,restoredTripleThrows=10,softShortExplosions=10,cleanSignalChecks=50,waterFamilies=2,currentFilesUnchanged=current,externalRecordedSamples=0))
