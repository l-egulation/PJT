"""Close, dry ink explosions: direct splash attack, no added room or echo."""
import copy
import json
import re
import zipfile
import numpy as np
import render_basic_attack_audio as m

FAMILIES = ('triple_explosion', 'normal_explosion', 'large_explosion')


def revise(p, archive, source):
    assert p['revision'] == 6
    audio = p['audio']; groups = p['sharedGroups']; rate = m.RATE
    before = copy.deepcopy(groups)

    def filt(x, low, high=70):
        pad = rate // 5; size = len(x)
        y = np.pad(x, (pad, pad)); f = np.fft.rfftfreq(len(y), 1 / rate)
        shape = (1 + (f / low) ** 8) ** -.5
        shape *= (1 + (high / np.maximum(f, .001)) ** 8) ** -.5
        return np.fft.irfft(np.fft.rfft(y) * shape, n=len(y))[pad:pad+size]

    def unit(x):
        return x / max(np.max(abs(x)), 1e-12)

    def burst(water, i, family):
        large = family == 'large_explosion'; triple = family == 'triple_explosion'
        speed = .88 if large else 1.07 if triple else 1
        liquid = np.interp(np.arange(0, len(water)-1, speed), np.arange(len(water)), water)
        # Retain the direct recording's quiet tail. No convolution, delayed copy,
        # diffuse noise, or room response is added to fill the end.
        duration = max(len(liquid), round(1.25 * rate))
        t = np.arange(duration) / rate
        liquid = np.pad(liquid, (0, duration-len(liquid)))
        decay = (.060 if triple else .095 if large else .080) + .002 * (i % 3)
        liquid *= np.exp(-np.maximum(0, t-.012) / decay)
        hz = (112 if large else 185 if triple else 160) + 5*i
        frequency = hz * (1 + .7*np.exp(-t/.012))
        phase = 2*np.pi*np.cumsum(frequency)/rate
        body = (np.sin(phase) + .23*np.sin(phase*2+.15))
        body *= (1-np.exp(-t/.0008)) * np.exp(-t/(.067 if large else .038 if triple else .047))
        # Water supplies the crisp near-field attack; the brief rounded body
        # supplies size without a long bass rumble or a separate delayed hit.
        y = .83*unit(liquid) + (.51 if large else .34)*unit(body)
        y[:round(.001*rate)] *= np.sin(np.linspace(0, np.pi/2, round(.001*rate)))**2
        peak = np.max(abs(y)); step = round(.01*rate)
        bins = np.array([np.max(abs(y[j:j+step])) for j in range(0,len(y),step)])
        end = len(y)
        for j in range(30, len(bins)-6):
            if np.max(bins[j:j+6]) < peak*10**(-68/20):
                end = (j+6)*step; break
        y = y[:end]
        # Only fade samples already below -68 dB; no audible hard cut.
        n = round(.025*rate); y[-n:] *= np.cos(np.linspace(0,np.pi/2,n))**2
        target = .28 if triple else .44 if large else .34
        return y*target/np.max(abs(y)), dict(
            mode='close-dry-ink-burst', addedReverb=False, addedEcho=False,
            directAttackSeconds=.001, liquidDecaySeconds=decay,
            liquidRate=speed, bodyHz=hz, highpass=70, lowpass=6200,
            endThresholdDb=-68, bodyOnset=0, waterOnset=0)

    with zipfile.ZipFile(archive) as z:
        for i in range(10):
            # Recover the purchase-pack recording, not the muffled r6 render.
            old_normal = audio[before['normal_explosion']['variants'][i]['audio']]
            entry = old_normal['source']['restoredOriginal']['archiveEntry']
            raw = z.read(entry); original = np.asarray(m.decode(raw))
            first = original[:round(.25*rate)]
            hit = round(m.sliding_peak(first.tolist())*rate)
            start = max(0,hit-round(.004*rate))
            water = filt(original[start:],6200)
            for family in FAMILIES:
                old = before[family]['variants'][i]
                y, edit = burst(water,i,family)
                raw_out = m.wav_bytes(y); key = 'r7/'+m.sha(raw_out)[:20]
                file = key.replace('/','-')+'.wav'; (source/'audio'/file).write_bytes(raw_out)
                samples = m.decode(raw_out); step=round(.01*rate)
                audio[key] = {**m.builder.audio_payload(raw_out,file),'gain':1,
                    'peak':max(map(abs,samples)), 'peakTime':m.sliding_peak(samples),
                    'peakEnvelopeStep':.01,
                    'peakEnvelope':[max(map(abs,samples[j:j+step])) for j in range(0,len(samples),step)],
                    'source':{'archiveEntry':entry,'sha256':m.sha(raw),
                        'leadTrimSamples':start,'previousCandidate':old['audio'],
                        'proceduralBody':'short rounded pressure pulse; no sampled reference audio'},
                    'edit':dict(edit,family=family)}
                label = '짧고 선명한 잉크 파열' if family=='triple_explosion' else '묵직한 근접 잉크 파열' if family=='large_explosion' else '가까운 잉크 파열'
                groups[family]['variants'][i] = dict(audio=key,gain=.72 if family=='triple_explosion' else .8,name=f'{label} {i+1:02}')
    for family in FAMILIES:
        groups[family]['revision']=7
    groups['triple_explosion']['scope']='3연속 전용 · 짧은 근접 파열 · 투척은 02 시안 유지'
    preserved={}; changed=[]
    for a in p['actions']:
        if not any(l.get('group') in FAMILIES for r in a['candidates'] for l in r['layers']): continue
        changed.append(a['id']); a['candidateRevision']=7
        a['reviewReason']='인공 잔향 제거 · 가까운 물 파열과 빠른 타격 · 조용한 자연 감쇠'
        a['note']=a['reviewReason']+' · 투척과 폭발 시점은 유지'
        for i,r in enumerate(a['candidates']):
            for l in r['layers']:
                if l.get('group') in FAMILIES:
                    v=groups[l['group']]['variants'][i]
                    l.update(audio=v['audio'],gain=v['gain'],name='가까이서 터지는 잉크')
                else: preserved[f'{a["id"]}/{i+1}/{l["id"]}']=copy.deepcopy(l)
            r['id']=a['id']+f'-v7-{i+1:02}'
            r['name']=f'{i+1:02} · '+' → '.join(l['name'] for l in r['layers'])
    assert preserved==p['preservedExplosionCompanions']
    p.update(revision=7,reviewedActions=changed)


if __name__=='__main__':
    import argparse
    parser=argparse.ArgumentParser(description=__doc__)
    parser.add_argument('--archive',required=True)
    args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition'; source=asset/'source'; page=asset/'preview/index.html'
    p=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',page.read_text(encoding='utf8'))[1])
    revise(p,args.archive,source)
    html=(source/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(p,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(source/'app.js').read_text(encoding='utf8'))
    page.write_text(html,encoding='utf8')
    provenance=dict(p,frames={k:{n:v[n] for n in ['source','sha256']} for k,v in p['frames'].items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in p['audio'].items()})
    m.dump(source/'recipes.json',provenance)
    m.register_asset(asset,'audio.gamemaster-skill-audition','40 skills and 2 basic reviews; revision 7 dry explosions; star confirmations preserved; not applied')
    print('Updated 30 close explosion variants; preserved all throwing layers and event bindings.')
