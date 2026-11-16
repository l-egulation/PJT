"""Five quieter, lower studies derived only from ink-hit-audition candidate 03."""
import base64
import json
import re
import numpy as np
import render_basic_attack_audio as m

OUT = m.RES / 'audio/ink-hit-wet-revision'
ORIGINAL = m.RES / 'audio/ink-hit-audition'


def main():
    for folder in ('source', 'runtime', 'preview'):
        (OUT / folder).mkdir(parents=True, exist_ok=True)
    raw = (ORIGINAL / 'runtime/ink-hit-03.wav').read_bytes()
    assert m.sha(raw) == 'a50410dfac3d6d4b16f15f44241eab810c4384ccb809ace896f87a55f56b6b5e'
    x = np.asarray(m.decode(raw))
    original_rms = float(np.sqrt(np.mean(x*x)))
    html = (ORIGINAL / 'preview/index.html').read_text(encoding='utf8')
    payload = json.loads(re.search(r'<script[^>]*id="audition-data"[^>]*>(.*?)</script>', html, re.S).group(1))
    reference = next(c for c in payload['candidates'] if c['number'] == 3)['audio']
    specs = [
        ('원형에 가까운 저음', '퍽촥', '원본 질감을 가장 많이 남기고 조금 낮춘 소리', .93, .70, 1.5, 5600),
        ('낮고 둥근 타격', '퍽촥', '음높이를 더 낮추고 날카로운 튐을 부드럽게', .87, .65, 2.5, 4700),
        ('묵직한 젖은 타격', '푸걱촥', '저음의 몸통을 강조한 중간 무게감', .81, .60, 3.5, 4100),
        ('가장 낮은 덩어리', '푸걱', '다섯 후보 중 가장 낮은 음높이와 두꺼운 저음', .77, .56, 4.5, 3500),
        ('조용한 중저음', '퍽쫩', '낮은 타격과 액체감을 유지하면서 가장 작은 음량', .84, .50, 3.0, 4800),
    ]
    candidates, recipes = [], []
    for number, (name, voice, description, rate, level, bass, cutoff) in enumerate(specs, 1):
        # Playback-rate pitch shift preserves adjacent source samples and lowers the
        # liquid texture as a whole; no generated knocks, resonators or extra layers.
        y = np.interp(np.arange(0, len(x)-1, rate), np.arange(len(x)), x)
        size = 1 << (len(y)*2-1).bit_length()
        hz = np.fft.rfftfreq(size, 1/m.RATE)
        shelf = 1 + (10**(bass/20)-1)/(1+(hz/320)**2)
        lowpass = 1/np.sqrt(1+(hz/cutoff)**4)
        y = np.fft.irfft(np.fft.rfft(y, size)*shelf*lowpass, size)[:len(y)]
        y -= np.mean(y)
        y[:144] *= np.sin(np.linspace(0, np.pi/2, 144))**2
        tail = round(.022*m.RATE)
        y[-tail:] *= np.cos(np.linspace(0, np.pi/2, tail))**2
        y *= original_rms*level/np.sqrt(np.mean(y*y))
        # Preserve v1 as a reproducible reference. Reduce only the front low band:
        # liquid highs and the later tail keep their previous levels, with no
        # loudness normalization after this cut.
        baseline = y.copy()
        low = np.fft.irfft(np.fft.rfft(y, size)/(1+(hz/1000)**4), size)[:len(y)]
        age = np.arange(len(y))/m.RATE
        hold, end = .065/rate, .110/rate
        blend = np.clip((age-hold)/(end-hold), 0, 1)
        reduction = .78 * (1 - blend*blend*(3-2*blend))
        # Avoid introducing a new edge at the beginning of the sample.
        reduction[:144] *= np.sin(np.linspace(0, np.pi/2, 144))**2
        y -= low*reduction
        previous = OUT / 'source/baseline-v1' / f'wet-hit-{number:02}.wav'
        previous.parent.mkdir(parents=True, exist_ok=True)
        previous.write_bytes(m.wav_bytes(baseline))
        assert np.isfinite(y).all() and max(abs(y)) < .9
        path = OUT / 'runtime' / f'wet-hit-{number:02}.wav'
        rendered = m.wav_bytes(y); path.write_bytes(rendered)
        key = m.sha(rendered)
        payload['audio'][key] = dict(src='data:audio/wav;base64,'+base64.b64encode(rendered).decode(),
            duration=len(y)/m.RATE, sha256=key, source=path.relative_to(m.MAIN).as_posix(),
            wave=[round(float(np.max(np.abs(v))),4) for v in np.array_split(y,100)])
        actual_rms = float(np.sqrt(np.mean(y*y)))
        db = round(20*np.log10(actual_rms/original_rms),1); semitones=round(12*np.log2(rate),1)
        candidates.append(dict(number=number, name=name, voice=voice,
            description=f'{description} · 앞쪽 쿵 억제 · 원본 대비 약 {db} dB / {semitones} 반음', audio=key, duration=len(y)/m.RATE))
        recipes.append(dict(number=number, name=name, rate=rate, semitones=semitones,
            rmsRatio=actual_rms/original_rms, rmsDb=db, bassShelfDb=bass, lowpassHz=cutoff,
            frontLowCut=dict(cutoffHz=1000, lowBandGain=.22, holdSeconds=hold, releaseEndsSeconds=end, renormalized=False),
            duration=len(y)/m.RATE, rms=float(np.sqrt(np.mean(y*y))), peak=float(max(abs(y))), sha256=key))
    payload['candidates'] = candidates
    for action in payload['actions']:
        # The comparison buttons consistently mean the old candidate 03 here,
        # never the weapon-dependent current game hit sound.
        action['currentHit'] = dict(id='reference-wet-hit-03', audio=reference, gain=1)
        action['currentHitNote'] = '기준 원본 · 이전 3번 젖은 타격 (퍽촥) · 모든 무기에서 같은 소리'
    used = {reference, *(c['audio'] for c in candidates)}
    for action in payload['actions']:
        used.update(e['audio'] for e in action['events'])
        for trigger in action.get('triggered', []): used.update(e['audio'] for e in trigger['events'])
    for events in payload['extras'].values(): used.update(e['audio'] for e in events)
    payload['audio'] = {k:v for k,v in payload['audio'].items() if k in used}
    payload['scope'] = 'Five studies derived from candidate 03; audition only; no game changes'
    m.dump(OUT / 'source/recipes.json', dict(revision=2, status='user-rejected entire wet-hit family; not applied',
        source='../ink-hit-audition/runtime/ink-hit-03.wav', sourceSha256=m.sha(raw), originalRms=original_rms,
        referenceAudio=reference, candidates=recipes))
    template = (OUT / 'source/template.html').read_text(encoding='utf8')
    app = (OUT / 'source/app.js').read_text(encoding='utf8')
    result = template.replace('__DATA__', json.dumps(payload, ensure_ascii=False).replace('</', '<\\/')).replace('__APP__', app)
    (OUT / 'preview/index.html').write_text(result, encoding='utf8')
    lfs = m.load(m.RES / '.lfs-payloads.json'); entry='audio/ink-hit-wet-revision/preview/index.html'
    if entry not in lfs: lfs.append(entry); m.dump(m.RES / '.lfs-payloads.json', sorted(lfs))
    m.register_asset(OUT, 'audio.ink-hit-wet-revision', 'user-rejected wet hit family; preserved only; not applied')
    print(json.dumps(recipes, ensure_ascii=False, indent=2))


if __name__ == '__main__': main()
