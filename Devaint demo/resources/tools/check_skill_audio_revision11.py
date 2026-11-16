"""Check one-second hiss semantics, removed attacks and preserved other actions."""
import json
import re
import numpy as np
import render_basic_attack_audio as m

root=m.RES/'audio/gamemaster-skill-audition';p=m.load(root/'source/recipes.json');assert p['revision']==11
audio=p['audio'];actions={a['id']:a for a in p['actions']}
def pcm(k):
    a=audio[k];raw=(root/'source/audio'/a['file']).read_bytes();assert m.sha(raw)==a['sha256']
    return np.asarray(m.decode(raw))
def rms(x):return np.sqrt(np.mean(x*x))
counts={}
for k,a in audio.items():
    if not k.startswith('r11/'):continue
    x=pcm(k);mode=a['edit']['mode'];counts[mode]=counts.get(mode,0)+1
    assert x[0]==0 and x[-1]==0 and max(abs(x))<.5
    assert rms(x[-960:])/max(abs(x))<.001
    assert rms(x[:480])/rms(x[1440:4800])<.2,'The first 10 ms must not be a percussive hit'
    assert np.allclose(a['peakEnvelope'],[max(abs(x[j:j+480])) for j in range(0,len(x),480)])
    if mode=='one-second-smoke-hiss':
        assert len(x)==m.RATE and a['duration']==1
        assert not a['edit']['burst'] and not a['edit']['repeat'] and not a['edit']['sustainLoop']
    else:
        water=a['source']['continuousWater']
        assert water['startSample']/m.RATE-water['strongestAttackSeconds']>=.0899
        assert water['archiveEntry'].startswith(('Water/water_splash_object_body_','Magic_Spells/water_spell_tidal_wave_drown_','Magic_Spells/water_spell_wave_crash_'))
        assert not a['edit']['bubbleUsed'] and not a['edit']['impactUsed']
assert counts=={'one-second-smoke-hiss':10,'water-release-without-impact':30}
for r in actions['dagger_range']['candidates']:
    assert len(r['layers'])==1
    l=r['layers'][0];assert l['id']=='smoke' and l['times']==[0] and l['anchor']=='activation' and l['align']=='onset'
    assert not l.get('loop') and not l.get('repeatEvery')
for tag,original in p['revision11PreservedLayers'].items():
    aid,num,lid=tag.split('/')
    assert next(l for l in actions[aid]['candidates'][int(num)-1]['layers'] if l['id']==lid)==original
for aid,a in p['revision11UnchangedActions'].items():assert actions[aid]==a
assert {'shield_follow_up','launcher_range_special'}<=p['revision11UnchangedActions'].keys()
for i,r in enumerate(actions['bomb_basic_special']['candidates']):assert r['layers'][:-1]==p['restoredTripleThrowLayers'][i]
current=0
for k,a in audio.items():
    if k.startswith('current/'):
        assert (root/'source/audio'/a['file']).read_bytes()==(m.MAIN/a['source']).read_bytes();current+=1
html=(root/'preview/index.html').read_text(encoding='utf8')
embedded=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',html)[1])
assert embedded['actions']==p['actions'] and embedded['sharedGroups']==p['sharedGroups']
assert (root/'source/app.js').read_text(encoding='utf8') in html
for v in m.load(m.RES/'generated-assets.json')['files']:
    if v['path'].startswith('audio/gamemaster-skill-audition/'):
        raw=(m.RES/v['path']).read_bytes();assert len(raw)==v['bytes'] and m.sha(raw)==v['sha256'],v['path']
print(dict(newFiles=counts,unchangedActions=len(p['revision11UnchangedActions']),currentFilesUnchanged=current))
