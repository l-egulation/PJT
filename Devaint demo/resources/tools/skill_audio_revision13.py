"""Ten recorded-air smoke alternatives and the user's imported star confirmations."""
import argparse
import copy
import io
import json
import re
import wave
import zipfile
import numpy as np
import render_basic_attack_audio as m


def revise(p, source, archive, selection):
    assert p['revision'] == 12
    imported_raw = selection.read_bytes()
    imported = json.loads(imported_raw)
    assert imported['kind'] == 'skill-audio-selection' and imported['revision'] == 12
    selections = source / 'selections'
    selections.mkdir(exist_ok=True)
    imported_path = selections / 'user-skill-selections-revision12.json'
    imported_path.write_bytes(imported_raw)
    seed = dict(actions={}, starConfirmations={}, shared=imported.get('sharedChoices', {}))
    for a in imported['actions']:
        seed['actions'][a['id']] = {k:copy.deepcopy(a[k]) for k in ('selected','settings','favorites','choice','candidateRevision','scenario') if k in a}
        seed['starConfirmations'][a['id']] = copy.deepcopy(a.get('confirmedCandidates', []))
    p['importedReview'] = dict(sha256=m.sha(imported_raw), file='source/selections/'+imported_path.name, state=seed)
    p['reopenedReviews'] = {'dagger_range':13}
    old_actions = copy.deepcopy(p['actions'])
    out = source / 'smoke-release-09'
    out.mkdir(exist_ok=True)
    rate = m.RATE
    total = round(rate * 1.1)
    entries = {
        'short2':'Explosion_Fire_Gas/gas_leak_short_burst_02.wav',
        'short3':'Explosion_Fire_Gas/gas_leak_short_burst_03.wav',
        'med2':'Explosion_Fire_Gas/gas_leak_med_burst_02.wav',
        'gas4':'Explosion_Fire_Gas/gas_leak_04_loop.wav',
        'strut4':'Doors/hydraulic_strut_air_gas_shock_door_04.wav',
        'strut5':'Doors/hydraulic_strut_air_gas_shock_door_05.wav',
        'wind1':'Magic_Spells/wind_blizzard_storm_spell_light_01.wav',
        'airy2':'Magic_Spells/wind_blizzard_storm_spell_airy_02.wav',
        **{f'spray{i}':f'Miscellaneous/Spray Bottle/spray_bottle_{i:02}.wav' for i in [3,5,11,14]},
    }
    # Different recorded gestures, not ten EQ settings on the rejected hiss.
    specs = [
        ('낮은 압력 방출', [('short3',.07,.77,1.1,1)], 2200, .045),
        ('넓게 빠지는 공기', [('strut4',.10,.83,1.1,1)], 2400, .055),
        ('작고 부드러운 분출', [('short2',.06,.47,1.1,1)], 2600, .045),
        ('느린 공기 방출', [('strut5',.045,.50,1.1,1)], 2100, .055),
        ('분사 뒤 낮은 퍼짐', [('spray3',.045,.28,.60,.60),('med2',.18,1.45,1.1,.65)], 2500, .040),
        ('얇은 분사와 공기', [('spray5',.055,.50,.76,.65),('strut4',.13,.80,1.1,.55)], 2900, .045),
        ('둥근 분사와 퍼짐', [('spray11',.04,.33,.64,.55),('short3',.08,.79,1.1,.70)], 2300, .050),
        ('앞이 선명한 분출', [('spray14',.065,.39,.68,.60),('gas4',.16,1.40,1.1,.55)], 2800, .040),
        ('공기 흐름 중심', [('wind1',.48,1.90,1.1,.75),('short2',.075,.47,.65,.35)], 2500, .065),
        ('옅은 바람과 분사', [('airy2',1.10,2.60,1.1,.75),('spray5',.06,.49,.70,.35)], 2200, .065),
    ]
    def lowpass(x, cutoff):
        pad = rate // 4
        y = np.pad(x, (pad,pad))
        f = np.fft.rfftfreq(len(y), 1/rate)
        response = (1+(f/cutoff)**8)**-.5 * (1+(160/np.maximum(f,.001))**6)**-.5
        return np.fft.irfft(np.fft.rfft(y)*response, n=len(y))[pad:pad+len(x)]
    def soften(x, attack, release):
        x = x.copy()
        n = min(round(attack*rate),len(x)//3)
        k = min(round(release*rate),len(x)//2)
        x[:n] *= np.sin(np.linspace(0,np.pi/2,n))**2
        x[-k:] *= np.cos(np.linspace(0,np.pi/2,k))**2
        x[0] = x[-1] = 0
        return x
    items, clips = [], []
    with zipfile.ZipFile(archive) as z:
        for number,(label,parts,cutoff,attack) in enumerate(specs,1):
            mixed = np.zeros(total)
            origins = []
            for name,start,end,duration,gain in parts:
                original = z.read(entries[name])
                x = np.asarray(m.decode(original))
                segment = x[round(start*rate):round(end*rate)]
                assert len(segment) > rate*.15
                shifted = np.interp(np.linspace(0,len(segment)-1,round(duration*rate)),np.arange(len(segment)),segment)
                # Smooth continuous colour change, no modulation, repetitions,
                # synthetic noise, pressure bang, or auto-levelled static bed.
                bright = lowpass(shifted,cutoff)
                dark = lowpass(shifted,cutoff*.55)
                t = np.linspace(0,1,len(shifted))
                blend = t*t*(3-2*t)
                y = bright*(1-blend)+dark*blend
                y = soften(y,attack,.32)
                y /= max(np.sqrt(np.mean(y*y)),1e-8)
                mixed[:len(y)] += gain*y
                origins.append(dict(archiveEntry=entries[name],sha256=m.sha(original),cropSeconds=[start,end],
                    outputSeconds=duration,playbackRate=len(segment)/len(shifted),gain=gain))
            t = np.linspace(0,1,total)
            mixed *= np.exp(-1.2*t)
            mixed = soften(mixed,attack,.45)
            mixed *= min(.055/max(np.sqrt(np.mean(mixed*mixed)),1e-8), .36/max(abs(mixed)))
            raw = m.wav_bytes(mixed)
            filename = f'smoke09-{number:02}.wav'
            (out/filename).write_bytes(raw)
            # The main audition's source/audio contract uses a local basename.
            (source/'audio'/filename).write_bytes(raw)
            key = f'r13/smoke-{number:02}'
            y = np.asarray(m.decode(raw))
            meta = dict(m.builder.audio_payload(raw,filename),gain=1,peak=float(max(abs(y))),
                peakTime=m.sliding_peak(y.tolist()),peakEnvelopeStep=.01,
                peakEnvelope=[float(max(abs(y[j:j+480]))) for j in range(0,len(y),480)],
                source=dict(recordings=origins),edit=dict(mode='recorded-air-smoke',burst=False,repeat=False,
                    syntheticNoise=False,attackSeconds=attack,tailFadeSeconds=.45,lowpassStart=cutoff,lowpassEnd=cutoff*.55))
            p['audio'][key] = meta
            items.append(dict(id=f'S{number:02}',number=number,label=label,file=filename,audio=key,
                sha256=m.sha(raw),duration=1.1,status='pending',sources=origins,edit=meta['edit']))
            with wave.open(io.BytesIO(raw),'rb') as w:
                params=w.getparams();clips.append(w.readframes(w.getnframes()))
    def comparison(name, group):
        with wave.open(str(out/name),'wb') as w:
            w.setparams(params);w.writeframes((b'\0'*(rate*2)).join(group))
    comparison('smoke09-all-10.wav',clips)
    comparison('smoke09-01-05.wav',clips[:5])
    comparison('smoke09-06-10.wav',clips[5:])
    a = next(a for a in p['actions'] if a['id']=='dagger_range')
    a.update(candidateRevision=13,reviewReason='기존 연막 기각 · 구매팩 공기 방출/분사 기반 10개 재검토',note='팡 없이 1.1초 공기 분출 한 번 · 반복·지속 루프 없음 · 새 후보 미정')
    for r,item in zip(a['candidates'],items):
        r.update(id=f'dagger_range-v13-{item["number"]:02}',name=f'{item["number"]:02} · '+item['label'],reason=a['reviewReason'],mixed=False,
            layers=[dict(id='smoke',name=item['label'],audio=item['audio'],times=[0],gain=1,anchor='activation',align='onset')])
    p.update(revision=13,reviewedActions=['dagger_range'],revision13UnchangedActions={a['id']:a for a in old_actions if a['id']!='dagger_range'})
    decisions = m.load(source/'focused-selections.json')
    smoke = decisions['smoke']
    smoke.setdefault('previousConfirmations',[]).append(copy.deepcopy({k:v for k,v in smoke.items() if k!='previousConfirmations'}))
    for k in ['confirmedRevision','confirmedFiles','confirmationScope','direction']:
        smoke.pop(k,None)
    smoke.update(status='pending; reopened by user',instruction='텔레비 지직거리는 소리 같아 후보 열 개 다시',
        currentCandidates=[i['id'] for i in items],currentAudition='preview/smoke-release-09.html',
        basedOn='new purchased recordings; previous candidates rejected',structure=dict(burstSeconds=0,totalSeconds=1.1))
    smoke.setdefault('rejected',[]).extend(['smoke-release-08/S16','smoke-release-08/S17'])
    decisions['importedSkillSelection'] = dict(file='source/selections/'+imported_path.name,sha256=m.sha(imported_raw),revision=12,
        rule='all stars confirmed; revisions and multiple stars preserved; only smoke explicitly reopened')
    m.dump(source/'focused-selections.json',decisions)
    m.dump(out/'mixes.json',dict(kind='smoke-release-revision9',status='pending; not applied',items=items,comparisonGapSeconds=1))
    rows=[]
    for action in imported['actions']:
        nums=sorted(set(action.get('favorites',[])+[c['number'] for c in action.get('confirmedCandidates',[])]+([action['choice']['number']] if action.get('choice') else [])))
        fixed=bool(action.get('fixedCandidate'))
        linked=action.get('linkedAction')
        linked_done=bool(linked and next(x for x in imported['actions'] if x['id']==linked).get('confirmedCandidates'))
        pending=action['id']=='dagger_range' or not (nums or fixed or linked_done)
        rows.append(dict(id=action['id'],name=action['name'],weapon=action['weapon'],basic=bool(action.get('isBasicReview')),confirmedNumbers=nums,pending=pending))
    m.dump(source/'selection-status.json',dict(sourceSha256=m.sha(imported_raw),rows=rows,
        pendingSkills=[r for r in rows if r['pending'] and not r['basic']],pendingBasics=[r for r in rows if r['pending'] and r['basic']]))
    cards=''.join(f'<article><h2>{i["number"]:02} · {i["label"]}</h2><audio controls preload="none" src="../source/smoke-release-09/{i["file"]}"></audio><button data-number="{i["number"]}">☆ 확정 표시</button></article>' for i in items)
    page='''<!doctype html><html lang="ko"><meta charset="utf-8"><meta name="viewport" content="width=device-width,initial-scale=1"><title>연막탄 새 후보 10개</title><style>
body{margin:0;background:#142123;color:#f2f0e8;font:16px/1.6 'Malgun Gothic',sans-serif}main{max-width:1000px;margin:auto;padding:28px}h1{margin:0}h2{font-size:18px}.grid{display:grid;grid-template-columns:repeat(auto-fit,minmax(280px,1fr));gap:16px}article{padding:16px;background:#213335;border:1px solid #46605d;border-radius:10px}audio{width:100%;margin:8px 0}button,a{font:inherit;color:#a3e1c9}button{background:#213335;border:1px solid #a3e1c9;border-radius:6px;padding:8px 12px;cursor:pointer}.selected{color:#f1c281;border-color:#f1c281}p{color:#c2d0cd}</style><main><h1>연막탄 · 새 후보 10개</h1><p>팡 없이 1.1초 · 구매팩 공기 방출·분사 편집/조합 · 미정</p><p>전달한 다른 스킬의 별표는 확정으로 보존했습니다. 기존 연막은 기각했습니다.</p><a href="index.html">모션과 함께 듣기 · 단검 → 먹안개</a><h2>01 → 05 (사이 1초)</h2><audio controls preload="none" src="../source/smoke-release-09/smoke09-01-05.wav"></audio><h2>06 → 10 (사이 1초)</h2><audio controls preload="none" src="../source/smoke-release-09/smoke09-06-10.wav"></audio><div class="grid">__CARDS__</div><p id="status"></p><button id="save">연막 선택 파일 저장</button></main><script>
const KEY='lost-words-smoke-release-09',buttons=[...document.querySelectorAll('[data-number]')],players=[...document.querySelectorAll('audio')];let stars=[];try{stars=JSON.parse(localStorage.getItem(KEY)||'[]')}catch(_){}if(!Array.isArray(stars))stars=[];
for(const a of players){a.volume=.7;a.onplay=()=>players.forEach(b=>{if(a!==b)b.pause()});}
function render(){buttons.forEach(b=>{const on=stars.includes(Number(b.dataset.number));b.textContent=on?'★ 확정':'☆ 확정 표시';b.classList.toggle('selected',on);b.setAttribute('aria-pressed',String(on))});document.getElementById('status').textContent=stars.length?'확정: '+stars.join(', ')+'번':'연막 미정';}
buttons.forEach(b=>b.onclick=()=>{const n=Number(b.dataset.number);stars=stars.includes(n)?stars.filter(x=>x!==n):[...stars,n];localStorage.setItem(KEY,JSON.stringify(stars));render()});
document.getElementById('save').onclick=()=>{const data={kind:'focused-smoke-choice',revision:9,action:'dagger_range',candidateRevision:13,favorites:stars,files:__FILES__.filter(x=>stars.includes(x.number)),status:'user-confirmed; not applied'};const u=URL.createObjectURL(new Blob([JSON.stringify(data,null,2)],{type:'application/json'})),a=document.createElement('a');a.href=u;a.download='smoke-sound-selections-09.json';a.click();setTimeout(()=>URL.revokeObjectURL(u),1000)};render();
</script></html>'''.replace('__CARDS__',cards).replace('__FILES__',json.dumps([dict(number=i['number'],file=i['file'],sha256=i['sha256']) for i in items]))
    (source.parent/'preview/smoke-release-09.html').write_text(page,encoding='utf8')
    return p


if __name__ == '__main__':
    from pathlib import Path
    parser=argparse.ArgumentParser();parser.add_argument('--archive',required=True);parser.add_argument('--selection',required=True);args=parser.parse_args()
    asset=m.RES/'audio/gamemaster-skill-audition';source=asset/'source';page=asset/'preview/index.html'
    p=json.loads(re.search(r'<script id="skill-data" type="application/json">([\s\S]*?)</script>',page.read_text(encoding='utf8'))[1])
    revise(p,source,args.archive,Path(args.selection))
    page.write_text((source/'template.html').read_text(encoding='utf8').replace('__DATA__',json.dumps(p,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',(source/'app.js').read_text(encoding='utf8')),encoding='utf8')
    m.dump(source/'recipes.json',dict(p,frames={k:{n:v[n] for n in ['source','sha256']} for k,v in p['frames'].items()},audio={k:{n:v for n,v in a.items() if n not in ['src','waveform']} for k,a in p['audio'].items()}))
    m.register_asset(asset,'audio.gamemaster-skill-audition','revision 13: 10 recorded-air smoke alternatives pending; imported user stars preserved; not applied')
    print('Generated 10 smoke alternatives, updated motion audition, preserved selection snapshot and other skills.')
