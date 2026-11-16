"""Check blast body, extended smoke, installation timing and preserved choices."""
import json
import re
import numpy as np
import render_basic_attack_audio as m

root=m.RES/'audio/gamemaster-skill-audition'; p=m.load(root/'source/recipes.json')
assert p['revision']==9
audio=p['audio']; actions={a['id']:a for a in p['actions']}
def pcm(key):
    a=audio[key];raw=(root/'source/audio'/a['file']).read_bytes();assert m.sha(raw)==a['sha256']
    return np.asarray(m.decode(raw))

counts={}; smoke=[]
for key,a in audio.items():
    if not key.startswith('r9/'):continue
    x=pcm(key); mode=a['edit']['mode'];counts[mode]=counts.get(mode,0)+1
    assert x[0]==0 and x[-1]==0 and max(abs(x))<.5
    assert np.sqrt(np.mean(x[-960:]**2))/max(abs(x))<.001
    assert np.allclose(a['peakEnvelope'],[max(abs(x[j:j+480])) for j in range(0,len(x),480)])
    if mode=='spreading-ink-explosion':
        power=abs(np.fft.rfft(x))**2;f=np.fft.rfftfreq(len(x),1/m.RATE)
        assert sum(power[(f>500)&(f<5000)])/sum(power)>.5,'Broad midrange release must outweigh the low thump'
        assert sum(x[2880:]**2)/sum(x*x)>.35,'Explosion needs body beyond the first 60 ms'
    if mode=='fuller-smoke-grenade':
        old=pcm(a['source']['previousCandidate']); assert len(x)>len(old)*1.2
        assert sum(x[4800:]**2)/sum(x*x)>sum(old[4800:]**2)/sum(old*old)
        smoke.append(len(x)/m.RATE)
assert counts['spreading-ink-explosion']==30 and counts['fuller-smoke-grenade']==10
assert counts['layered-decoy-contact']==10 and counts['decoy-ink-settling']==5
decoy=actions['shield_follow_up'];assert decoy['installationEvent']['time']==decoy['impact']==.2
for r in decoy['candidates']:
    assert len(r['layers'])==2
    assert r['layers'][0]['times']==[0] and r['layers'][1]['times']==[.018]
    assert all(l['anchor']=='activation' and l['align']=='onset' for l in r['layers'])
for tag,original in p['revision9PreservedLayers'].items():
    aid,num,lid=tag.split('/')
    assert next(l for l in actions[aid]['candidates'][int(num)-1]['layers'] if l['id']==lid)==original
for aid,a in p['revision9UnchangedActions'].items():assert actions[aid]==a
assert 'launcher_range_special' in p['revision9UnchangedActions']
for i,r in enumerate(actions['bomb_basic_special']['candidates']):assert r['layers'][:-1]==p['restoredTripleThrowLayers'][i]
current=0
for key,a in audio.items():
    if key.startswith('current/'):
        assert (root/'source/audio'/a['file']).read_bytes()==(m.MAIN/a['source']).read_bytes();current+=1
html=(root/'preview/index.html').read_text(encoding='utf8')
embedded=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',html)[1])
assert embedded['actions']==p['actions'] and embedded['sharedGroups']==p['sharedGroups']
assert (root/'source/app.js').read_text(encoding='utf8') in html
for v in m.load(m.RES/'generated-assets.json')['files']:
    if v['path'].startswith('audio/gamemaster-skill-audition/'):
        raw=(m.RES/v['path']).read_bytes();assert len(raw)==v['bytes'] and m.sha(raw)==v['sha256'],v['path']
print(dict(newFiles=counts,smokeSeconds=[min(smoke),max(smoke)],unchangedActions=len(p['revision9UnchangedActions']),currentFilesUnchanged=current))
