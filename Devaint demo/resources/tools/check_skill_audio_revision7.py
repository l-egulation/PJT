"""Check dry explosion attacks, quiet endings, companion sounds and media hashes."""
import json
import re
import numpy as np
import render_basic_attack_audio as m
from skill_audio_revision7 import FAMILIES

root=m.RES/'audio/gamemaster-skill-audition'
p=m.load(root/'source/recipes.json'); assert p['revision']==7
audio=p['audio']; actions={a['id']:a for a in p['actions']}

def pcm(key):
    a=audio[key]; raw=(root/'source/audio'/a['file']).read_bytes()
    assert m.sha(raw)==a['sha256']
    return np.asarray(m.decode(raw))

result={}
for family in FAMILIES:
    durations=[]; attacks=[]
    for v in p['sharedGroups'][family]['variants']:
        a=audio[v['audio']]; y=pcm(v['audio']); old=pcm(a['source']['previousCandidate'])
        peak=max(abs(y)); step=480
        assert a['edit']['addedReverb'] is False and a['edit']['addedEcho'] is False
        assert a['peakTime']<.012, 'Direct attack must be at the start'
        early=sum(y[:2400]**2)/sum(y*y)
        assert early>.75 and early>sum(old[:2400]**2)/sum(old*old), 'Energy must move to the direct attack'
        assert np.sqrt(np.mean(y[-960:]**2))<peak*.0004, 'Ending must be quiet before it is faded'
        assert y[0]==0 and y[-1]==0 and peak<.45
        assert max(abs(y[-1200:]))<peak*.0005, 'No audible truncation at the final fade'
        assert np.allclose(a['peakEnvelope'],[max(abs(y[j:j+step])) for j in range(0,len(y),step)])
        durations.append(len(y)/m.RATE); attacks.append(early)
    result[family]={'durationRange': [round(min(durations),3),round(max(durations),3)],
                    'minimumFirst50msEnergy':round(min(attacks),3)}
for id,original in p['preservedExplosionCompanions'].items():
    aid,num,part=id.split('/')
    assert next(l for l in actions[aid]['candidates'][int(num)-1]['layers'] if l['id']==part)==original
for i,r in enumerate(actions['bomb_basic_special']['candidates']):
    assert r['layers'][:-1]==p['restoredTripleThrowLayers'][i]
for r in actions['whip_follow_up']['candidates']: assert r['layers']==[p['fixedWhipLayer']]
assert actions['base_sword_follow_up']['linkedAction']=='base_sword_default'
current=0
for key,a in audio.items():
    if key.startswith('current/'):
        assert (root/'source/audio'/a['file']).read_bytes()==(m.MAIN/a['source']).read_bytes()
        current+=1
html=(root/'preview/index.html').read_text(encoding='utf8')
embedded=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',html)[1])
assert embedded['sharedGroups']==p['sharedGroups']
assert embedded['actions']==p['actions']
assert (root/'source/app.js').read_text(encoding='utf8') in html
for v in m.load(m.RES/'generated-assets.json')['files']:
    if v['path'].startswith('audio/gamemaster-skill-audition/'):
        raw=(m.RES/v['path']).read_bytes()
        assert m.sha(raw)==v['sha256'] and len(raw)==v['bytes'],v['path']
print(dict(families=result,preservedCompanions=len(p['preservedExplosionCompanions']),currentFilesUnchanged=current))
