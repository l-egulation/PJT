"""Three hit-sound directions with different construction approaches (not yet applied to the game).

Each candidate keeps its purchased source at original speed (no pitch/rate change), removes only
low-frequency content below the attack swing band, starts at the source's own transient and ends at
the source's own decay (-54 dB) or, for floor-limited foley, where the event meets its room/handling
floor. Output loudness matches the hammer reference hit (x0.8 runtime gain) on a K-weighted
100 ms maximum, so A/B against the hammer is at equal loudness.

Run from the main repository root: python resources/tools/build_hit_three_directions.py
"""
import base64
import json
import re
import zipfile
import numpy as np
import render_basic_attack_audio as m

OUT = m.RES / 'audio/ink-hit-three-directions'
PREVIOUS = m.RES / 'audio/ink-hit-directions'
ARCHIVE = 'C:/Users/SSAFY/Downloads/Gamemaster Audio - Pro Sound Collection.zip'
R = m.RATE
REFERENCE_GAIN = .8  # runtime gain of the hammer reference hit in the preview and game default
SELECTED = 2  # user selection 2026-11-16: applied to every weapon basic-attack and skill hit
GAME_FILE = 'sfx_hit_common_01_v01.wav'

SNOW = 'Snow_Ice/snowball_hit_impact_04.wav'
BAG = 'Foley/foley_sports_bag_grab_pickup_catch_02.wav'
MUD = 'Guns_Weapons/Bullets/bullet_impact_mud_06.wav'
GRAB = 'Foley/foley_combat_fight_grab_throw_02.wav'

CANDIDATES = [
    dict(number=1, key='packed', name='뭉친 덩어리 한 방', voice='팍',
         approach='원음 하나 · 거의 무가공',
         description='단단히 뭉친 눈덩이가 부딪히는 실제 녹음 하나. 짧고 촘촘한 접촉 뒤 잔부스러기만 남는다. 물기는 없다.',
         intent='잉크를 액체가 아니라 꽉 찬 덩어리로 해석한다. 날카롭지만 금속·나무 울림이 없는 접촉으로 “맞았다”를 전달한다.',
         limits='원음의 60~70 Hz 울림을 걷어내 무게는 공격음 쪽에 맡긴다. 눈 부스러기 질감이 거칠게 들릴 수 있고, 잉크 연상은 가장 약하다.'),
    dict(number=2, key='layered', name='접촉점 + 잉크 두께', voice='퍽+척',
         approach='역할을 나눈 2레이어',
         description='속이 찬 가방을 받는 “퍽”이 접촉 순간을 정하고, 그 아래 2 ms 뒤에 진흙 덩어리의 끈적한 몸체가 -6 dB로 따라온다.',
         intent='앞의 접촉은 짧고 분명하게, 잉크 몸의 두께는 점성 질감으로만 준다. 이전 반려안과 반대로 앞부분 저음을 키우지 않는다.',
         limits='두 녹음의 공간감이 달라 붙여 들릴 수 있다. 진흙 원음 앞의 총알 바람 소리 구간은 잘라냈다(본 타격 직전에서 시작).'),
    dict(number=3, key='grab', name='움켜쥐는 흡수', voice='턱',
         approach='원음 하나 · 짧은 사건',
         description='두꺼운 옷감을 손으로 움켜쥐는 실제 녹음. 튀거나 울리지 않고 공격을 받아 무는 짧은 반응이다.',
         intent='잉크 몸이 공격을 흡수한다는 해석. 여러 마리·연타에서 가장 덜 쌓이도록 짧은 길이를 원음 그대로 쓴다.',
         limits='움켜쥔 뒤 이어지는 던지기 동작 소리는 쓰지 않으므로 끝을 바닥 잡음 위에서 0.1초 동안 서서히 줄였다. 타격감이 약하게 들릴 수 있다.'),
]


def biquad(x, b, a):
    y = np.zeros_like(x)
    x1 = x2 = y1 = y2 = 0.
    b0, b1, b2 = b
    _, a1, a2 = a
    for i, v in enumerate(x):
        o = b0*v + b1*x1 + b2*x2 - a1*y1 - a2*y2
        x2, x1, y2, y1 = x1, v, y1, o
        y[i] = o
    return y


def highpass(x, hz, q=.7071):
    """RBJ 2nd-order (12 dB/oct) causal high-pass: removes sub/room boom without pre-ringing."""
    w = 2*np.pi*hz/R
    alpha, c = np.sin(w)/(2*q), np.cos(w)
    b = np.array([(1+c)/2, -(1+c), (1+c)/2])
    a = np.array([1+alpha, -2*c, 1-alpha])
    return biquad(x, b/a[0], a/a[0])


def k_weight(x):
    x = biquad(x, [1.53512485958697, -2.69169618940638, 1.19839281085285], [1, -1.69065929318241, .73248077421585])
    return biquad(x, [1, -2, 1], [1, -1.99004745483398, .99007225036621])


def loudness(x, window=.1):
    """ITU-R BS.1770 K-weighted maximum over a sliding window (LKFS-like, short window for hits)."""
    y = k_weight(np.asarray(x, float))
    n = int(window*R)
    power = np.convolve(y*y, np.ones(n)/n, 'full')
    return float(10*np.log10(power.max()+1e-12) - .691)


def envelope(x, seconds):
    n = max(1, int(seconds*R))
    return np.sqrt(np.convolve(x*x, np.ones(n)/n, 'same'))


def onset(x, threshold=.05, pre=.002):
    e = envelope(x, .001)
    return max(0, int(np.argmax(e > e.max()*threshold)) - int(pre*R))


def natural_end(x, drop=54., floor_window=None, floor_margin=4.):
    e = envelope(x, .01)
    peak = int(np.argmax(e))
    limit = e[peak]*10**(-drop/20)
    floor = None
    if floor_window:
        a, b = (int(t*R) for t in floor_window)
        floor = float(np.median(e[a:b]))
        limit = max(limit, floor*10**(floor_margin/20))
    below = np.where(e[peak:] < limit)[0]
    end = peak + int(below[0]) if len(below) else len(x)
    return end, (20*np.log10(floor/e[peak]) if floor else None)


def fades(x, fade_in, fade_out):
    x = x.copy()
    a, b = max(1, int(fade_in*R)), max(1, int(fade_out*R))
    x[:a] *= np.sin(np.linspace(0, np.pi/2, a))**2
    x[-b:] *= np.cos(np.linspace(0, np.pi/2, b))**2
    return x


def take(x, hp, drop=54., floor_window=None, fade_out=.025):
    """Start at the source transient, keep its own decay; floor-limited foley fades across its floor."""
    y = highpass(x, hp)
    start = onset(y)
    y = y[start:]
    end, floor_db = natural_end(y, drop, floor_window)
    if floor_window:
        end = min(len(y), end + int(fade_out*R))
    return fades(y[:end], .001, fade_out), dict(startSeconds=start/R, keptSeconds=end/R, highpassHz=hp,
                                                 endRule=(f'-{drop:g} dB below peak' if not floor_window else
                                                          f'event meets floor ({floor_db:.1f} dB below peak) +4 dB, then fade'),
                                                 fadeInSeconds=.001, fadeOutSeconds=fade_out)


def band_share(x, hz):
    spectrum = np.abs(np.fft.rfft(x))**2
    freq = np.fft.rfftfreq(len(x), 1/R)
    return float(10*np.log10(spectrum[freq < hz].sum()/spectrum.sum()))


def main():
    for directory in ('runtime', 'source/originals', 'preview'):
        (OUT/directory).mkdir(parents=True, exist_ok=True)
    old = (PREVIOUS/'preview/index.html').read_text(encoding='utf8')
    payload = json.loads(re.search(r'<script[^>]*id="audition-data"[^>]*>(.*?)</script>', old, re.S).group(1))
    reference = next(a['currentHit'] for a in payload['actions'] if a['weapon'] == 'hammer')
    ref_raw = base64.b64decode(payload['audio'][reference['audio']]['src'].split(',', 1)[1])
    ref = np.asarray(m.decode(ref_raw))*REFERENCE_GAIN
    target = loudness(ref)

    sources, recipes, candidates = {}, [], []
    with zipfile.ZipFile(ARCHIVE) as archive:
        for entry in (SNOW, BAG, MUD, GRAB):
            raw = archive.read(entry)
            (OUT/'source/originals'/entry.split('/')[-1]).write_bytes(raw)
            sources[entry] = (np.asarray(m.decode(raw)), m.sha(raw))

    built = {}
    # 1. One recording, nearly untouched.
    y, info = take(sources[SNOW][0], 200)
    built['packed'] = (y, [dict(role='전체', archiveEntry=SNOW, sourceSha256=sources[SNOW][1], **info)])
    # 2. Two layers with separate roles.
    contact, c_info = take(sources[BAG][0], 140, floor_window=(.2, .4), fade_out=.04)
    mud = highpass(sources[MUD][0], 150)
    e = envelope(mud, .001)
    a, b = int(.035*R), int(.075*R)
    main_hit = a + int(np.argmax(np.diff(e[a:b]))) - int(.001*R)  # skip the bullet fly-in before the splat
    mud = mud[main_hit:]
    mud_end, _ = natural_end(mud, 54.)
    mud = fades(mud[:mud_end], .002, .03)
    mud_gain_db = -6.
    mud_gain = 10**((loudness(contact) + mud_gain_db - loudness(mud))/20)
    offset = int(.002*R)
    y = np.zeros(max(len(contact), offset+len(mud)))
    y[:len(contact)] += contact
    y[offset:offset+len(mud)] += mud*mud_gain
    built['layered'] = (y, [
        dict(role='접촉점: 맞는 순간과 형태', archiveEntry=BAG, sourceSha256=sources[BAG][1], **c_info, levelRelativeDb=0),
        dict(role='잉크 두께: 점성 질감과 꼬리', archiveEntry=MUD, sourceSha256=sources[MUD][1], startSeconds=main_hit/R,
             keptSeconds=mud_end/R, highpassHz=150, endRule='-54 dB below peak', fadeInSeconds=.002, fadeOutSeconds=.03,
             offsetSeconds=offset/R, levelRelativeDb=mud_gain_db,
             note='0~58 ms bullet fly-in removed; starts 1 ms before the splat transient'),
    ])
    # 3. One short recording, floor-limited event.
    y, info = take(sources[GRAB][0], 120, floor_window=(.1, .25), fade_out=.1)
    built['grab'] = (y, [dict(role='전체', archiveEntry=GRAB, sourceSha256=sources[GRAB][1], **info)])

    for spec in CANDIDATES:
        y, layers = built[spec['key']]
        gain = 10**((target - loudness(y))/20)
        y = y*gain
        assert np.max(np.abs(y)) < .5
        rendered = m.wav_bytes(y)
        path = OUT/'runtime'/f"hit-{spec['number']}-{spec['key']}.wav"
        path.write_bytes(rendered)
        key = m.sha(rendered)
        duration = len(y)/R
        payload['audio'][key] = dict(src='data:audio/wav;base64,'+base64.b64encode(rendered).decode(), duration=duration, sha256=key,
                                     source=path.relative_to(m.MAIN).as_posix(),
                                     wave=[round(float(max(abs(v))), 4) for v in np.array_split(y, 100)])
        stats = dict(durationSeconds=round(duration, 4), outputGainDb=round(20*np.log10(gain), 2), peak=round(float(np.max(np.abs(y))), 4),
                     kWeighted100msMax=round(loudness(y), 2), kWeighted400msMax=round(loudness(y, .4), 2),
                     below120HzShareDb=round(band_share(y, 120), 1), below200HzShareDb=round(band_share(y, 200), 1))
        candidates.append(dict(number=spec['number'], name=spec['name'], voice=spec['voice'], approach=spec['approach'],
                               description=spec['description'], intent=spec['intent'], limits=spec['limits'], audio=key,
                               duration=duration, sources=[l['archiveEntry'].split('/')[-1] for l in layers]))
        recipes.append(dict(number=spec['number'], name=spec['name'], approach=spec['approach'], layers=layers,
                            pitchOrRateChange=False, distortionOrSaturation=False, addedBass=False, stats=stats, sha256=key,
                            intent=spec['intent'], limits=spec['limits']))
        if spec['number'] == SELECTED:
            # The game plays hit cues at the weapon/skill volume (default 0.8). Scale by 1/0.8 so the default
            # in-game level equals what the user heard in the preview (candidate at 1.0 = hammer at 0.8).
            game = m.wav_bytes(y/REFERENCE_GAIN)
            (OUT/'runtime/game').mkdir(parents=True, exist_ok=True)
            (OUT/'runtime/game'/GAME_FILE).write_bytes(game)
            recipes[-1]['gameRuntime'] = dict(file='runtime/game/'+GAME_FILE, sha256=m.sha(game), bytes=len(game),
                                              scale=round(1/REFERENCE_GAIN, 4), defaultRuntimeVolume=REFERENCE_GAIN,
                                              peak=round(float(np.max(np.abs(y/REFERENCE_GAIN))), 4))

    payload['candidates'] = candidates
    for action in payload['actions']:
        action['currentHit'] = dict(reference)
        action['currentHitNote'] = '비교 기준 · 기존 망치 피격음(게임 기본 배율 0.8) · 모든 무기에서 같은 기준음'
    used = {reference['audio'], *(c['audio'] for c in candidates)}
    for a in payload['actions']:
        used.update(e['audio'] for e in a['events'])
        for t in a.get('triggered', []):
            used.update(e['audio'] for e in t['events'])
    for events in payload['extras'].values():
        used.update(e['audio'] for e in events)
    payload['audio'] = {k: v for k, v in payload['audio'].items() if k in used}
    payload['scope'] = 'Three construction approaches; level-matched to hammer reference; not applied to game'
    payload['reference'] = dict(name='기존 망치 피격음', file=reference['file'], gain=REFERENCE_GAIN,
                                kWeighted100msMax=round(target, 2), peak=round(float(np.max(np.abs(ref))), 4))
    m.dump(OUT/'source/recipes.json', dict(
        status='user-selected candidate 2 (layered) for every weapon basic attack and skill hit; candidates 1 and 3 not used',
        selected=SELECTED,
        levelMatch=dict(method='ITU-R BS.1770 K-weighting, maximum over sliding 100 ms window',
                        reference=reference['file'], referenceGain=REFERENCE_GAIN, targetDb=round(target, 2)),
        commonRules=['original speed, no pitch/rate change', 'no added bass layer, saturation or distortion',
                     '12 dB/oct causal high-pass only below the attack-swing band', 'start at source transient',
                     'end at source decay (-54 dB) or event/floor boundary with long fade', 'no common duration limit'],
        rejectedFamilies=['ink-hit-wet-revision (all)', 'ink-hit-directions 1-10 (all)'],
        candidates=recipes))
    template = (OUT/'source/template.html').read_text(encoding='utf8')
    app = (OUT/'source/app.js').read_text(encoding='utf8')
    (OUT/'preview/index.html').write_text(
        template.replace('__DATA__', json.dumps(payload, ensure_ascii=False).replace('</', '<\\/')).replace('__APP__', app), encoding='utf8')
    lfs = m.load(m.RES/'.lfs-payloads.json')
    entry = 'audio/ink-hit-three-directions/preview/index.html'
    if entry not in lfs:
        lfs.append(entry)
        m.dump(m.RES/'.lfs-payloads.json', sorted(lfs))
    m.register_asset(OUT, 'audio.ink-hit-three-directions', 'user-selected candidate 2 for all weapon hits; game copy in runtime/game')
    print(json.dumps(dict(target=round(target, 2), candidates=[dict(n=r['number'], **r['stats']) for r in recipes]), ensure_ascii=False))


if __name__ == '__main__':
    main()
