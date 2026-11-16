"""Validate revised sources, smooth endings, laser identity and unchanged timing."""
import json
import re
import numpy as np
import render_basic_attack_audio as m

root=m.RES/'audio/gamemaster-skill-audition'; p=m.load(root/'source/recipes.json')
assert p['revision']==8
audio=p['audio']; actions={a['id']:a for a in p['actions']}

def pcm(key):
    a=audio[key]; raw=(root/'source/audio'/a['file']).read_bytes()
    assert m.sha(raw)==a['sha256']
    return np.asarray(m.decode(raw))

counts={}; laser_ratios=[]
for key,a in audio.items():
    if not key.startswith('r8/'):continue
    x=pcm(key); mode=a['edit']['mode']; counts[mode]=counts.get(mode,0)+1
    peak=max(abs(x)); assert peak<.9 and x[-1]==0 and x[0]==0
    assert np.sqrt(np.mean(x[-960:]**2))/peak<.001,'Tail must be quiet at the endpoint'
    assert np.allclose(a['peakEnvelope'],[max(abs(x[j:j+480])) for j in range(0,len(x),480)])
    if mode=='brighten-existing-laser':
        old=pcm(a['source']['processedSource']); assert len(old)==len(x)
        assert abs(sum(x*x)/sum(old*old)-1)<.001,'Brightness must not just mean more volume'
        freq=np.fft.rfftfreq(len(x),1/m.RATE)
        def brightness(y):
            power=abs(np.fft.rfft(y))**2
            return sum(power[freq>650])/sum(power)
        ratio=brightness(x)/brightness(old); assert ratio>1.5
        # Dominant pitch in each 40 ms block remains in the same FFT bin.
        for at in range(1920,len(x)-4800,1920):
            window=np.hanning(1920)
            orig=abs(np.fft.rfft(old[at:at+1920]*window))
            new=abs(np.fft.rfft(x[at:at+1920]*window))
            # EQ can elevate an already present octave, but creates no new peak.
            assert orig[np.argmax(new)]>.2*max(orig)
        laser_ratios.append(ratio)
    else:
        primary=a['source']['primary']['archiveEntry']
        assert a['edit']['addedReverb'] is False
        if mode in ('recorded-ink-explosion','smoke-grenade-burst'):
            assert primary.startswith('Explosion_Fire_Gas/explosion_small_')
            assert a['edit']['tonalPressurePulse'] is False
        else:
            assert primary.startswith('Miscellaneous/Object Deep Thud/')
            assert a['peakTime']<.02
assert counts=={'recorded-ink-explosion':30,'smoke-grenade-burst':10,'close-decoy-placement':10,'brighten-existing-laser':10}
for tag,contract in p['revision8EventContracts'].items():
    aid,num,lid=tag.split('/')
    l=next(l for l in actions[aid]['candidates'][int(num)-1]['layers'] if l['id']==lid)
    assert {k:v for k,v in l.items() if k not in ('audio','gain','name')}==contract
for tag,original in p['revision8PreservedLayers'].items():
    aid,num,lid=tag.split('/')
    assert next(l for l in actions[aid]['candidates'][int(num)-1]['layers'] if l['id']==lid)==original
for aid,a in p['revision8UnchangedActions'].items():assert actions[aid]==a
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
        raw=(m.RES/v['path']).read_bytes(); assert len(raw)==v['bytes'] and m.sha(raw)==v['sha256'],v['path']
print(dict(candidates=counts,unchangedActions=len(p['revision8UnchangedActions']),
    preservedLayers=len(p['revision8PreservedLayers']),currentFilesUnchanged=current,
    laserBrightBandRatio=[round(min(laser_ratios),2),round(max(laser_ratios),2)]))
