"""Ten distinct purchased-source hit directions, without the rejected wet-hit recipe."""
import base64
import json
import re
import zipfile
import numpy as np
import render_basic_attack_audio as m

OUT = m.RES / 'audio/ink-hit-directions'
OLD = m.RES / 'audio/ink-hit-audition'
SPECS = [
    ('물방울 파열', '포닥', '짧고 둥근 물방울이 터지는 방향', 'Water/water_drop_drip_single_12.wav', .19, .78, 150, 4200, False),
    ('얇은 막 베기', '스착', '무거운 충격보다 얇게 찢어지는 마찰감', 'Foley/foley_cloth_light_fast_movement_08.wav', .18, 1.05, 280, 6200, False),
    ('부드러운 몸통 타격', '퍽', '액체 꼬리 없이 살짝 눌리는 둥근 타격', 'Punches/punch_general_body_impact_06.wav', .21, .90, 180, 3600, False),
    ('공기압 타격', '푸슉', '압축된 공기가 짧게 퍼지는 방향', 'Explosion_Fire_Gas/gas_leak_short_burst_02.wav', .22, .92, 320, 4600, False),
    ('가루 흩어짐', '파사', '잉크가 작은 입자로 부서져 흩어지는 방향', 'Footsteps/footstep_sand_land_03.wav', .23, 1.12, 400, 5700, False),
    ('기포 터짐', '뽀글팍', '부풀었던 기포가 찌그러지며 터지는 방향', 'Comedy_Cartoon/bubble_effect_03.wav', .24, .82, 140, 4000, False),
    ('먹물 흡입', '쑤압', '안쪽으로 빨려 들어갔다가 사라지는 방향', 'Magic_Spells/dark_wind_suck_conjure_01.wav', .26, 1.10, 240, 4200, True),
    ('짧은 잉크 물결', '촤', '앞쪽 펀치 없이 넓게 번지는 물결', 'Magic_Spells/water_spell_wave_crash_02.wav', .28, .95, 300, 5300, False),
    ('납작한 눌림', '팟', '단단한 쿵 대신 부드럽게 납작해지는 타격', 'Foley/foley_cloth_sports_glove_catch_03.wav', .17, .88, 220, 4400, False),
    ('탄성 파열', '뽁', '짧게 튕겼다가 끊김 없이 사라지는 방향', 'Comedy_Cartoon/cork_pop_open_bottle_boing_01.wav', .20, .76, 180, 3400, False),
]


def main():
    for directory in ('runtime', 'source/originals', 'preview'):
        (OUT/directory).mkdir(parents=True, exist_ok=True)
    old = (OLD/'preview/index.html').read_text(encoding='utf8')
    payload = json.loads(re.search(r'<script[^>]*id="audition-data"[^>]*>(.*?)</script>', old, re.S).group(1))
    reference = next(a['currentHit'] for a in payload['actions'] if a['weapon']=='hammer')
    candidates, recipes = [], []
    with zipfile.ZipFile('C:/Users/SSAFY/Downloads/Gamemaster Audio - Pro Sound Collection.zip') as archive:
        for number, (name, voice, description, entry, duration, rate, highpass, lowpass, reverse) in enumerate(SPECS, 1):
            raw = archive.read(entry); (OUT/'source/originals'/entry.split('/')[-1]).write_bytes(raw)
            x = np.asarray(m.decode(raw)); window=round(.008*m.RATE)
            energy=np.convolve(x*x,np.ones(window)/window,'valid')
            start=max(0,int(np.argmax(energy))-round(.012*m.RATE))
            x=x[start:start+round((duration*rate+.02)*m.RATE)]
            if reverse: x=x[::-1]
            y=np.interp(np.arange(0,max(1,len(x)-1),rate),np.arange(len(x)),x)
            count=round(duration*m.RATE); y=np.pad(y,(0,max(0,count-len(y))))[:count]
            size=1 << (count*2-1).bit_length(); hz=np.fft.rfftfreq(size,1/m.RATE)
            band=(1-np.exp(-(hz/highpass)**2))/np.sqrt(1+(hz/lowpass)**4)
            y=np.fft.irfft(np.fft.rfft(y,size)*band,size)[:count];y-=np.mean(y)
            attack=round((.018 if reverse else .004)*m.RATE);tail=round(min(.075,duration*.4)*m.RATE)
            y[:attack]*=np.sin(np.linspace(0,np.pi/2,attack))**2
            y[-tail:]*=np.cos(np.linspace(0,np.pi/2,tail))**2
            y*=np.exp(-np.arange(count)/m.RATE/(duration*.95))
            # Modest comparable levels. Gain only: no clipping, saturation or common bass thud.
            gain=min(.045/np.sqrt(np.mean(y*y)),.25/max(abs(y)));y*=gain
            rendered=m.wav_bytes(y);path=OUT/'runtime'/f'hit-{number:02}.wav';path.write_bytes(rendered);key=m.sha(rendered)
            payload['audio'][key]=dict(src='data:audio/wav;base64,'+base64.b64encode(rendered).decode(),duration=duration,sha256=key,
                source=path.relative_to(m.MAIN).as_posix(),wave=[round(float(max(abs(v))),4) for v in np.array_split(y,100)])
            candidates.append(dict(number=number,name=name,voice=voice,description=description,audio=key,duration=duration))
            recipes.append(dict(number=number,name=name,archiveEntry=entry,sourceSha256=m.sha(raw),startSeconds=start/m.RATE,
                rate=rate,highpassHz=highpass,lowpassHz=lowpass,reverse=reverse,duration=duration,gain=float(gain),
                rms=float(np.sqrt(np.mean(y*y))),peak=float(max(abs(y))),sha256=key))
    payload['candidates']=candidates
    for action in payload['actions']:
        action['currentHit']=dict(reference)
        action['currentHitNote']='참고음 · 기존 망치 피격음 · 모든 무기에서 같은 참고음으로 비교'
    used={reference['audio'],*(c['audio'] for c in candidates)}
    for a in payload['actions']:
        used.update(e['audio'] for e in a['events'])
        for t in a.get('triggered',[]):used.update(e['audio'] for e in t['events'])
    for events in payload['extras'].values():used.update(e['audio'] for e in events)
    payload['audio']={k:v for k,v in payload['audio'].items() if k in used}
    payload['scope']='Ten distinct hit studies; rejected wet-hit family excluded; not applied to game'
    m.dump(OUT/'source/recipes.json',dict(status='awaiting user selection; not applied',rejectedFamily='ink-hit-wet-revision; original wet-hit candidate 03',candidates=recipes))
    template=(OUT/'source/template.html').read_text(encoding='utf8');app=(OUT/'source/app.js').read_text(encoding='utf8')
    (OUT/'preview/index.html').write_text(template.replace('__DATA__',json.dumps(payload,ensure_ascii=False).replace('</','<\\/')).replace('__APP__',app),encoding='utf8')
    lfs=m.load(m.RES/'.lfs-payloads.json');entry='audio/ink-hit-directions/preview/index.html'
    if entry not in lfs:lfs.append(entry);m.dump(m.RES/'.lfs-payloads.json',sorted(lfs))
    m.register_asset(OUT,'audio.ink-hit-directions','10 distinct new hit directions; wet-hit family rejected; awaiting selection')
    print(json.dumps(dict(candidates=len(candidates),actions=len(payload['actions']),uniqueSources=len({r['sourceSha256'] for r in recipes}))))


if __name__=='__main__':main()
