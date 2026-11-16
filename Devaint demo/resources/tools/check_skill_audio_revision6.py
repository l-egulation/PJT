"""Validate natural tails, unchanged companion sounds and exact peak envelopes."""
import numpy as np
import render_basic_attack_audio as m
root=m.RES/'audio/gamemaster-skill-audition';p=m.load(root/'source/recipes.json');assert p['revision']==6
audio=p['audio'];actions={a['id']:a for a in p['actions']}
def pcm(key):
    a=audio[key];raw=(root/'source/audio'/a['file']).read_bytes();assert m.sha(raw)==a['sha256'];return np.asarray(m.decode(raw))
durations={};tail_energy={}
for group in ['triple_explosion','normal_explosion','large_explosion']:
    lengths=[];fractions=[]
    for v in p['sharedGroups'][group]['variants']:
        a=audio[v['audio']];x=pcm(v['audio']);old=pcm(a['source']['previousCandidate']);peak=max(abs(x));n=480
        assert len(x)>len(old),group
        assert np.sqrt(np.mean(x[-960:]**2))<peak*.001,'Tail must end well below audible peak'
        fraction=sum(x[len(old):]**2)/sum(x*x);assert fraction>1e-6,'Do not append only silence'
        assert abs(peak-max(abs(old)))<.00007,'Preserve former peak'
        assert peak<.9 and x[-1]==0
        expected=[max(abs(x[j:j+n])) for j in range(0,len(x),n)]
        assert np.allclose(expected,a['peakEnvelope'],atol=1e-10)
        assert a['peakEnvelopeStep']==.01
        lengths.append(len(x)/m.RATE);fractions.append(fraction)
    durations[group]=[round(min(lengths),3),round(max(lengths),3)];tail_energy[group]=round(min(fractions),6)
for id,original in p['preservedExplosionCompanions'].items():
    aid,num,part=id.split('/');l=next(l for l in actions[aid]['candidates'][int(num)-1]['layers'] if l['id']==part);assert l==original
for i,r in enumerate(actions['bomb_basic_special']['candidates']):assert r['layers'][:-1]==p['restoredTripleThrowLayers'][i]
for r in actions['whip_follow_up']['candidates']:assert r['layers']==[p['fixedWhipLayer']]
assert actions['base_sword_follow_up']['linkedAction']=='base_sword_default'
current=0
for key,a in audio.items():
    if key.startswith('current/'):
        assert (root/'source/audio'/a['file']).read_bytes()==(m.MAIN/a['source']).read_bytes();current+=1
for v in m.load(m.RES/'generated-assets.json')['files']:
    if v['path'].startswith('audio/gamemaster-skill-audition/'):
        raw=(m.RES/v['path']).read_bytes();assert m.sha(raw)==v['sha256'] and len(raw)==v['bytes'],v['path']
print(dict(naturalTailSeconds=durations,minimumRestoredTailEnergyFraction=tail_energy,preservedCompanions=len(p['preservedExplosionCompanions']),currentFilesUnchanged=current))
