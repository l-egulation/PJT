"""Verify water-only bomb ingredients and softened, unshortened smoke pops."""
import json
import re
import numpy as np
import render_basic_attack_audio as m

root=m.RES/'audio/gamemaster-skill-audition';p=m.load(root/'source/recipes.json');assert p['revision']==10
audio=p['audio'];actions={a['id']:a for a in p['actions']}
def pcm(k):
    a=audio[k];raw=(root/'source/audio'/a['file']).read_bytes();assert m.sha(raw)==a['sha256']
    return np.asarray(m.decode(raw))
counts={};ratios=[]
for k,a in audio.items():
    if not k.startswith('r10/'):continue
    x=pcm(k);mode=a['edit']['mode'];counts[mode]=counts.get(mode,0)+1
    assert x[0]==0 and x[-1]==0 and max(abs(x))<.5
    assert np.sqrt(np.mean(x[-960:]**2))/max(abs(x))<.001
    assert np.allclose(a['peakEnvelope'],[max(abs(x[j:j+480])) for j in range(0,len(x),480)])
    if mode=='water-balloon-rupture':
        assert a['source']['waterBurst']['archiveEntry'].startswith('Magic_Spells/water_')
        assert a['source']['membraneBubble']['archiveEntry'].startswith('Comedy_Cartoon/bubble_effect_')
        assert a['source']['waterSpread']['archiveEntry'].startswith('Water/water_splash_object_body_')
        assert a['edit']['explosionRecordingUsed'] is False and a['edit']['impactRecordingUsed'] is False
    else:
        old=pcm(a['source']['processedSource']);assert len(old)==len(x)
        f=np.fft.rfftfreq(len(x),1/m.RATE)
        hi=lambda v:sum(abs(np.fft.rfft(v))[f>2500]**2)
        ratio=hi(x)/hi(old);assert ratio<.1,'Sharp upper band must actually be reduced'
        assert sum(x[:1440]**2)/sum(old[:1440]**2)<.5,'Initial snap must actually be softened'
        ratios.append(ratio)
assert counts=={'water-balloon-rupture':30,'rounded-long-smoke-pop':10}
for tag,original in p['revision10PreservedLayers'].items():
    aid,num,lid=tag.split('/')
    assert next(l for l in actions[aid]['candidates'][int(num)-1]['layers'] if l['id']==lid)==original
for aid,a in p['revision10UnchangedActions'].items():assert actions[aid]==a
assert {'shield_follow_up','launcher_range_special'}<=p['revision10UnchangedActions'].keys()
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
print(dict(newFiles=counts,unchangedActions=len(p['revision10UnchangedActions']),
    currentFilesUnchanged=current,smokeHighBandEnergyRatio=[round(min(ratios),3),round(max(ratios),3)]))
