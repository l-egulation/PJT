"""Checks audible-discontinuity risks and provenance; not a listening approval."""
import json
import math
import wave
from pathlib import Path
import numpy as np
import render_basic_attack_audio as m

root=m.RES/'audio/gamemaster-skill-audition';data=m.load(root/'source/recipes.json')
assert data['revision']==3
def pcm(key):
    a=data['audio'][key];raw=(root/'source/audio'/a['file']).read_bytes()
    assert m.sha(raw)==a['sha256'],key
    return np.asarray(m.decode(raw))

current=0
for key,a in data['audio'].items():
    x=pcm(key);assert max(abs(x))<.999,key
    if key.startswith('current/'):
        assert (root/'source/audio'/a['file']).read_bytes()==(m.MAIN/a['source']).read_bytes(),key
        current+=1
actions={a['id']:a for a in data['actions']}
worst_gap=0
for aid,part in [('shield_range','field'),('shield_movement','push'),('dagger_range','smoke')]:
    for recipe in actions[aid]['candidates']:
        l=next(l for l in recipe['layers'] if l['id']==part);a=data['audio'][l['audio']];x=pcm(l['audio'])
        assert l['loop'] and a['edit']['mode']=='continuous-spectrum'
        blocks=x.reshape(-1,2400);rms=np.sqrt(np.mean(blocks**2,axis=1))
        gap=20*math.log10(max(rms)/min(rms));worst_gap=max(worst_gap,gap)
        assert gap<5.5,(aid,recipe['number'],'50ms amplitude hole',gap)
        assert abs(x[0]-x[-1])<np.quantile(abs(np.diff(x)),.999)*1.5,(aid,'seam exceeds ordinary waveform slope')
        if aid=='shield_range':assert gap<3.0,('hum must be steady',gap)
for recipe in actions['launcher_range_special']['candidates']:
    x=pcm(recipe['layers'][0]['audio']);assert len(x)/m.RATE>.5
    assert np.sqrt(np.mean(x[-480:]**2))<10**(-60/20),'Laser must end softly'
for aid,part in [('shield_default','parry'),('shield_follow_up','place'),('greatsword_range','impact'),('launcher_followup_special','explosion')]:
    for recipe in actions[aid]['candidates']:
        x=pcm(next(l for l in recipe['layers'] if l['id']==part)['audio']);power=abs(np.fft.rfft(x))**2;freq=np.fft.rfftfreq(len(x),1/m.RATE)
        assert sum(power[freq>4000])/sum(power)<.002,(aid,'sharp high-frequency residue')
assert len(data['reference']['clips'])==4
assert m.sha((root/data['reference']['file']).read_bytes())==data['reference']['sha256']
registered=[e for e in m.load(m.RES/'generated-assets.json')['files'] if e['path'].startswith('audio/gamemaster-skill-audition/')]
for e in registered:
    raw=(m.RES/e['path']).read_bytes();assert m.sha(raw)==e['sha256'] and len(raw)==e['bytes'],e['path']
print(json.dumps(dict(currentFilesUnchanged=current,audioHashes=len(data['audio']),registeredFiles=len(registered),maxBed50msRangeDb=round(worst_gap,2),laserSoftEnds=10,roundedEventHighBandChecks=40,referenceClips=4)))
