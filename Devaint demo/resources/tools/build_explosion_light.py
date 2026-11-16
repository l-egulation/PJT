"""Lighter, arcade-scale explosion candidates from candidates 1 and 2 of each slot (not applied to the game).

User feedback 2026-11-16: candidates 1-2 sounded like a realistic 3D game; this is a 2D pixel side-scroller.
Each source gets three lightening steps. Every step does the same three things, more strongly each time:
higher playback speed (smaller, higher explosion), a low cut of the heavy body, and a shorter tail with a
smooth cosine fade. No distortion or added layers. Loudness matches the slot's current file like the first pool.

Run from the main repository root: python resources/tools/build_explosion_light.py
"""
import json
import zipfile
import numpy as np
import render_basic_attack_audio as m
import build_explosion_candidates as base

OUT = m.RES/'audio/explosion-candidates-light'
R = m.RATE
STEPS = [  # name, speed, low-cut Hz, max seconds from onset (basic slot), fade seconds
    ('가볍게', 1.15, 120, .60, .15),
    ('더 가볍게', 1.30, 180, .45, .12),
    ('아주 가볍게', 1.50, 250, .32, .10),
]
LENGTH_SCALE = {'basic': 1.0, 'large': 1.6, 'grenade': .8}


def speed_up(x, speed):
    """Band-limited resampling by FFT: plays the recording faster and higher, without aliasing."""
    n = len(x); new = max(2, int(round(n/speed)))
    spectrum = np.fft.rfft(x)
    keep = new//2+1
    return np.fft.irfft(spectrum[:keep], new)*(new/n)


def lighten(x, speed, low, max_seconds, fade):
    y = speed_up(x, speed)
    for _ in range(2):  # 4th-order (24 dB/oct) low cut from two 2nd-order sections
        y = base.highpass(y, low)
    env = np.sqrt(np.convolve(y*y, np.ones(int(.005*R))/int(.005*R), 'same'))
    peak = int(np.argmax(env))
    below = np.where(env[peak:] < env[peak]*10**(-54/20))[0]
    natural = peak+int(below[0]) if len(below) else len(y)
    end = min(len(y), natural, int(max_seconds*R))
    y = y[:end].copy()
    f = min(int(fade*R), len(y)//2)
    y[-f:] *= np.cos(np.linspace(0, np.pi/2, f))**2
    return y, dict(speed=speed, lowCutHz=low, lowCutOrder=4, keptSeconds=round(end/R, 4), fadeSeconds=round(f/R, 4),
                   endRule='natural -54 dB' if end == natural else f'capped at {max_seconds:g} s with cosine fade')


def main():
    for d in ('runtime', 'preview', 'source'):
        (OUT/d).mkdir(parents=True, exist_ok=True)
    data = dict(slots=[], audio={})
    recipes = []
    with zipfile.ZipFile(base.ARCHIVE) as archive:
        for sid, slot in base.SLOTS.items():
            cur_raw = (base.UNITY_SKILLS/slot['current']).read_bytes()
            cur = np.asarray(m.decode(cur_raw))
            target = base.loud(cur)
            data['audio']['current-'+sid] = base.payload_audio(cur_raw, cur)
            items = []
            number = 0
            for src_no, (src_name, _, entry, _) in enumerate(slot['candidates'][:2], 1):
                raw = archive.read(entry)
                x0, info0 = base.natural(m.decode(raw))
                variants = [(f'{src_name} · {name}', speed, low, mx*LENGTH_SCALE[sid], fade) for name, speed, low, mx, fade in STEPS]
                for label, speed, low, mx, fade in variants:
                    number += 1
                    x, info = lighten(x0, speed, low, mx, fade)
                    if sid == 'grenade':
                        mix = np.zeros(int(base.GRENADE_PATTERN[-1][0]*R)+len(x))
                        for at, g in base.GRENADE_PATTERN:
                            mix[int(at*R):int(at*R)+len(x)] += x*g
                        x = mix
                    gain = 10**((target-base.loud(x))/20)
                    x = x*gain
                    peak = float(np.max(np.abs(x)))
                    if peak > .89:
                        x *= .89/peak
                    rendered = m.wav_bytes(x)
                    key = f'{sid}-{number}'
                    (OUT/'runtime'/f'{key}.wav').write_bytes(rendered)
                    data['audio'][key] = base.payload_audio(rendered, x)
                    items.append(dict(key=key, number=number, name=label, voice=f'×{speed:g}', source=entry.split('/')[-1],
                                      description=f'원래 {src_no}번 「{src_name}」을 {speed:g}배 빠르게(더 높고 작게) 재생하고, {low} Hz 아래 무거운 몸통을 덜고, '
                                                  f'{info["keptSeconds"]:.2f}초 안에서 끝내 꼬리를 줄였다.',
                                      duration=round(len(x)/R, 3)))
                    recipes.append(dict(slot=sid, number=number, name=label, fromCandidate=src_no, archiveEntry=entry, sourceSha256=m.sha(raw),
                                        trim=info0, **info, grenadeCluster=base.GRENADE_PATTERN if sid == 'grenade' else None,
                                        gainDb=round(20*np.log10(gain), 2), limitedTo089=peak > .89, loudnessDb=round(base.loud(x), 2),
                                        targetDb=round(target, 2), peak=round(float(np.max(np.abs(x))), 4), file=f'runtime/{key}.wav', sha256=m.sha(rendered)))
            # The unlightened originals stay audible for reference.
            prev = m.RES/'audio/explosion-candidates/runtime'
            for src_no, (src_name, voice, entry, _) in enumerate(slot['candidates'][:2], 1):
                number += 1
                raw = (prev/f'{sid}-{src_no}.wav').read_bytes(); x = np.asarray(m.decode(raw)); key = f'{sid}-{number}'
                data['audio'][key] = base.payload_audio(raw, x)
                items.append(dict(key=key, number=number, name=f'참고 · {src_name} 원본', voice=voice, source=entry.split('/')[-1],
                                  description=f'가볍게 만들기 전의 「{src_name}」. 비교용이다.', duration=round(len(x)/R, 3)))
            contexts = []
            for label, path, times in base.CONTEXTS[sid]:
                raw = path.read_bytes(); key = 'ctx-'+path.stem
                data['audio'][key] = base.payload_audio(raw, np.asarray(m.decode(raw)))
                contexts.append(dict(label=label, audio=key, gain=.8, explosions=times))
            data['slots'].append(dict(id=sid, name=slot['name'], uses=slot['uses'], current='current-'+sid,
                                      currentFile=slot['current'], candidates=items, contexts=contexts))
    m.dump(OUT/'source/recipes.json', dict(status='direction rejected by user (wanted less threatening, not cut or sped up); kept for comparison',
                                            feedback='1,2번에서 좀 더 가벼운 소리. 지금은 3D 실사 게임 같은 사운드, 우리는 2D 픽셀 횡스크롤',
                                            levelMatch='K-weighted 400 ms maximum equal to the slot current file', candidates=recipes))
    src = m.RES/'audio/explosion-candidates/source'
    template = (src/'template.html').read_text(encoding='utf8').replace('<title>폭발음 후보</title>', '<title>가벼운 폭발음 후보</title>') \
        .replace('<h1>폭발음 후보</h1>', '<h1>가벼운 폭발음 후보</h1>') \
        .replace('폭발이 쓰이는 세 자리마다 서로 다른 성격의 후보 5개와 지금 소리를 나란히 듣는 화면입니다. 모두 구매팩의 폭발 계열 녹음을 원래 속도로 썼고, 30 Hz 아래 들리지 않는 울림만 걷어냈습니다.',
                 '이전 1·2번(실제 폭발)을 2D 픽셀 횡스크롤에 맞게 가볍게 만든 후보입니다. 원음마다 가볍게 · 더 가볍게 · 아주 가볍게 세 단계로, 더 빠르게(높고 작게) 재생하고 무거운 저음과 긴 꼬리를 줄였습니다. 마지막 두 장은 가볍게 만들기 전 원본입니다.')
    app = (src/'app.js').read_text(encoding='utf8').replace("STORE='deviant-explosion-candidates-v1'", "STORE='deviant-explosion-light-v1'") \
        .replace("kind:'explosion-candidates-selection'", "kind:'explosion-light-selection'").replace("v.kind!=='explosion-candidates-selection'", "v.kind!=='explosion-light-selection'") \
        .replace("'explosion-candidates-selection.json'", "'explosion-light-selection.json'")
    (OUT/'source/template.html').write_text(template, encoding='utf8'); (OUT/'source/app.js').write_text(app, encoding='utf8')
    (OUT/'preview/index.html').write_text(template.replace('__DATA__', json.dumps(data, ensure_ascii=False).replace('</', '<\\/')).replace('__APP__', app), encoding='utf8')
    lfs = m.load(m.RES/'.lfs-payloads.json'); entry = 'audio/explosion-candidates-light/preview/index.html'
    if entry not in lfs:
        lfs.append(entry); m.dump(m.RES/'.lfs-payloads.json', sorted(lfs))
    m.register_asset(OUT, 'audio.explosion-candidates-light', 'lighter variants of explosion candidates 1-2; direction rejected by user; kept for comparison')
    print(json.dumps([(r['slot'], r['number'], r['name'], r['keptSeconds'], r['gainDb'], r['limitedTo089'], r['loudnessDb'], r['targetDb']) for r in recipes], ensure_ascii=False))


if __name__ == '__main__':
    main()
