"""Natural-tail re-renders of truncated hammer/greatsword skill cues and a quieter hammer floor slam.

The selected skill renders were mixed from pre-cut layers that kept only 0.18-0.36 s after the peak
and then ended with a 25-30 ms linear fade, so slam rumbles stopped while still audible. This tool
re-cuts each layer from the SAME start sample of the same purchased recording with the SAME causal
filters and level, but keeps the recording's own decay (to -54 dB below the layer peak or the end of
the recording). Event times, layer gains and the render safety gain are taken unchanged from
gamemaster-skills-selected/source/render.json, so everything before the old fade is reproduced.

Run from the main repository root: python resources/tools/build_natural_tail_fixes.py
"""
import json
import math
import zipfile
import numpy as np
import render_basic_attack_audio as m

ARCHIVE = 'C:/Users/SSAFY/Downloads/Gamemaster Audio - Pro Sound Collection.zip'
OUT = m.RES / 'audio/skill-natural-tails'
AUDITION = m.RES / 'audio/gamemaster-skill-audition/source'
SELECTED = m.RES / 'audio/gamemaster-skills-selected'
BASIC = m.RES / 'audio/gamemaster-selected/runtime'
R = m.RATE
TARGETS = ['hammer_default_10.wav', 'hammer_range_10.wav', 'hammer_follow_up_03.wav',
           'greatsword_default_06.wav', 'greatsword_default_07.wav',
           # Same truncation found by the pre-fade level scan; fixed on user request ("같이 고쳐줘").
           'launcher_followup_special_02.wav', 'rapier_default_01.wav', 'rapier_range_01.wav', 'rapier_movement_01.wav',
           'rapier_follow_up_01.wav', 'rapier_movement__teleport_01.wav', 'rapier_follow_up__teleport_01.wav',
           'shuriken_move_special__teleport_02.wav', 'shuriken_move_special__return_02.wav', 'greatsword_movement_04.wav']
END_DB = 54.
END_FADE = .04
MAX_NATURAL = 1.5  # seconds from layer peak; longer recordings are continuous beds, not a single impact
LONG_FADE = .4
SOURCE_END_FADE = .08


def one_pole(x, low, high=0):
    """Exact numpy port of the original causal one-pole low-pass (+ optional high-pass)."""
    al = 1-math.exp(-2*math.pi*low/R)
    ah = 1-math.exp(-2*math.pi*high/R) if high else 0
    lo = hi = 0.
    out = np.empty(len(x))
    for i, v in enumerate(x):
        lo += al*(v-lo)
        if high:
            hi += ah*(lo-hi)
            out[i] = lo-hi
        else:
            out[i] = lo
    return out


def crop_start(samples, before):
    _, _, peak = m.stats(samples)
    return max(0, int((peak-before)*R))


audio_table = {}


def extend(key, info, archive):
    """Rebuild one layer with its natural tail; return (samples, provenance)."""
    edit, src = info['edit'], info['source']
    if isinstance(src, dict) and 'composite' in src:
        entry = raw = samples = None
    else:
        entry = src if isinstance(src, str) else src['archiveEntry']
        raw = archive.read(entry)
        assert m.sha(raw) == (info.get('sourceSha256') or src['sha256']), key
        samples = m.decode(raw)
    start = None
    if key.startswith('cut/'):  # build_skill_audio_audition: crop(before=.045, lowpass 6500) -> level(-22)
        start = crop_start(samples, .045)
        y = one_pole(np.asarray(samples[start:]), 6500)
        attacks = [int(.002*R)]
        old_fade = int(.025*R)
    elif edit.get('mode') == 'composite':  # skill_audio_revision2.composite: onset-aligned parts -> fade(.004, .035) -> level(db)
        mix = np.zeros(R*6)
        parts = []
        for part in src['parts']:
            if part['reverse']:  # a reversed cut ends with its original attack: keep it as it was
                y_part = np.asarray(m.decode((AUDITION/'audio'/audio_table[part['audio']]['file']).read_bytes()))[::-1]
                parts.append(dict(part, kept='old cut reversed'))
            else:
                y_part, prov = extend(part['audio'], audio_table[part['audio']], archive)
                parts.append(dict(part, extended=prov))
            at = round(part['onset']*R)
            mix[at:at+len(y_part)] += y_part*part['gain']
        y = np.trim_zeros(mix, 'b')
        attacks = [round(.004*R)]
        old_fade = round(.035*R)
        entry, raw = None, None
    else:  # skill_audio_revision2 'burst': crop(before=.012) -> [resize(pitch)] -> filter(low, high) -> fade(.003, .03) -> level(db)
        assert edit['mode'] == 'burst', key
        start = crop_start(samples, .012)
        x = np.asarray(samples[start:])
        ramp = int(.002*R)
        x[:ramp] *= np.arange(ramp)/ramp  # the crop's 2 ms ramp comes before filtering in this recipe
        if edit['pitch'] != 1:
            # Same resampling step as the original resize() of the short crop, continued over the longer crop.
            _, _, peak = m.stats(samples)
            old_len = min(len(samples), int((peak+edit['tail'])*R))-start
            step = (old_len-1)/(max(2, round(old_len/edit['pitch']))-1)
            x = np.interp(np.arange(0, len(x)-1, step), np.arange(len(x)), x)
        y = one_pole(x, edit['lowpass'], edit['highpass'])
        attacks = [round(.003*R)]  # then fade()'s 3 ms ramp
        old_fade = round(.03*R)
    old = np.asarray(m.decode((AUDITION/'audio'/info['file']).read_bytes()))
    # The old cut is this same signal times one level gain until its end fade: fit that gain.
    a, b = int(.01*R), len(old)-int(.06*R)
    gain = float(np.dot(old[a:b], y[a:b])/np.dot(y[a:b], y[a:b]))
    residual = float(np.max(np.abs(old[a:b]-gain*y[a:b]))/np.max(np.abs(old)))
    assert residual < .01, (key, residual)
    y = y*gain
    for attack in attacks:
        y[:attack] *= np.arange(attack)/max(1, attack)
    env = np.sqrt(np.convolve(y*y, np.ones(int(.01*R))/int(.01*R), 'same'))
    peak = int(np.argmax(env))
    below = np.where(env[peak:] < env[peak]*10**(-END_DB/20))[0]
    natural = peak+int(below[0]) if len(below) else len(y)
    if natural >= len(y):
        # The purchased recording itself stops before decaying (no material left): soften its last part only.
        rule = f'recording ends before -{END_DB:g} dB: {SOURCE_END_FADE*1000:g} ms fade at recording end'
        end = len(y)
        fade = min(int(SOURCE_END_FADE*R), end//4)
    elif natural-peak <= MAX_NATURAL*R:
        rule = f'recording decay to -{END_DB:g} dB'
        end = min(len(y), natural+int(END_FADE*R))
        fade = min(int(END_FADE*R), end//4)
    else:
        # A continuous recording (landslide) has no end within the skill: keep everything up to where the
        # old cut began its fade, then replace that 25-30 ms cut with a long smooth fade.
        rule = f'continuous recording: kept to old fade start, then {LONG_FADE:g} s fade'
        fade = int(LONG_FADE*R)
        end = min(len(y), len(old)-old_fade+fade)
    y = y[:end].copy()
    y[-fade:] *= np.cos(np.linspace(0, np.pi/2, fade))**2
    return y, dict(audio=key, archiveEntry=entry, sourceSha256=m.sha(raw) if raw else None, startSample=start,
                   parts=parts if entry is None else None, levelGain=round(gain, 6),
                   reproductionResidual=round(residual, 6), endRule=rule, endFadeSeconds=round(fade/R, 4), oldSeconds=round(len(old)/R, 4), newSeconds=round(len(y)/R, 4))


def k_loud(x):
    def biquad(v, b, a):
        y = np.zeros_like(v); x1 = x2 = y1 = y2 = 0.
        for i, s in enumerate(v):
            o = b[0]*s+b[1]*x1+b[2]*x2-a[1]*y1-a[2]*y2; x2, x1, y2, y1 = x1, s, y1, o; y[i] = o
        return y
    y = biquad(np.asarray(x, float), [1.53512485958697, -2.69169618940638, 1.19839281085285], [1, -1.69065929318241, .73248077421585])
    y = biquad(y, [1, -2, 1], [1, -1.99004745483398, .99007225036621])
    n = int(.1*R)
    return float(10*np.log10(np.convolve(y*y, np.ones(n)/n, 'full').max()+1e-12)-.691)


def main():
    (OUT/'runtime').mkdir(parents=True, exist_ok=True)
    (OUT/'source').mkdir(exist_ok=True)
    audio = m.load(AUDITION/'recipes.json')['audio']
    audio_table.update(audio)
    render = m.load(SELECTED/'source/render.json')
    outputs = {o['file']: o for o in render['outputs']}
    layers, records = {}, []
    with zipfile.ZipFile(ARCHIVE) as archive:
        for name in TARGETS:
            out = outputs[name]
            old = np.asarray(m.decode((SELECTED/'runtime'/name).read_bytes()))
            events, used, old_ends = [], [], []
            for e in out['events']:
                assert e.get('rate', 1) == 1
                at = round(e['time']*R)
                if e.get('loop'):
                    # Looped bed: same tiling and fade-in; keep it to where the old 35 ms fade began,
                    # then replace that short linear cut with a long cosine fade.
                    base = np.asarray(m.decode((AUDITION/'audio'/audio[e['audio']]['file']).read_bytes()))
                    n = round(e['duration']*R)
                    fade = min(round(e.get('fadeSeconds', .035)*R), n//4)
                    extra = int(LONG_FADE*R)
                    x = np.resize(base, n+extra).astype(float)
                    x[:fade] *= np.linspace(0, 1, fade)
                    tail = fade+extra
                    x[n-fade:] *= np.cos(np.linspace(0, np.pi/2, tail))**2
                    events.append((x*e['gain'], at))
                    old_ends.append(at+n-fade)
                    used.append(dict(audio=e['audio'], loop=True, time=e['time'], gain=e['gain'], loopSeconds=e['duration'],
                                     endRule=f'loop kept to old fade start, then {LONG_FADE:g} s fade'))
                    continue
                if e['audio'] not in layers:
                    layers[e['audio']] = extend(e['audio'], audio[e['audio']], archive)
                x, prov = layers[e['audio']]
                events.append((x*e['gain'], at))
                # Compare only before the old cut's fade (r2: 25 ms crop fade + 30 ms fade) or the new longer fade.
                old_ends.append(at+min(round(prov['oldSeconds']*R)-int(.07*R), round(prov['newSeconds']*R)-round((prov['endFadeSeconds']+.005)*R)))
                used.append(dict(prov, time=e['time'], gain=e['gain']))
            mix = np.zeros(max(at+len(x) for x, at in events))
            for x, at in events:
                if at < 0:
                    x, at = x[-at:], 0
                mix[at:at+len(x)] += x
            mix *= out['safeGain']
            check = max(int(.02*R), min(len(old)-int(.07*R), min(old_ends)))  # before any layer's old end fade
            mismatch = float(np.max(np.abs(mix[:check]-old[:check]))/np.max(np.abs(old)))
            assert mismatch < .01, (name, mismatch)
            raw = m.wav_bytes(mix)
            target = name[:-7]+'_tail'+name[-7:]  # keeps the _NN variant suffix (e.g. greatsword_default_tail_06.wav)
            (OUT/'runtime'/target).write_bytes(raw)
            records.append(dict(id=name[:-7], replaces=f'audio/gamemaster-skills-selected/runtime/{name}', file='runtime/'+target,
                                sha256=m.sha(raw), bytes=len(raw), samples=len(mix), oldSeconds=round(len(old)/R, 4),
                                newSeconds=round(len(mix)/R, 4), safeGain=out['safeGain'], matchBeforeOldFade=round(mismatch, 6), matchedSeconds=round(check/R, 4),
                                layers=used))
    # Hammer basic 2nd hit: same recording, one scalar gain so its loudness equals the hammer's 1st hit.
    first = np.asarray(m.decode((BASIC/'sfx_weapon_hammer_basic_0_01_v01.wav').read_bytes()))
    slam_raw = (BASIC/'sfx_weapon_hammer_basic_1_01_v01.wav').read_bytes()
    slam = np.asarray(m.decode(slam_raw))
    db = k_loud(first)-k_loud(slam)
    quiet = m.wav_bytes(slam*10**(db/20))
    (OUT/'runtime/sfx_weapon_hammer_basic_1_01_v02.wav').write_bytes(quiet)
    basic = dict(id='hammer_basic_1', replaces='audio/gamemaster-selected/runtime/sfx_weapon_hammer_basic_1_01_v01.wav',
                 sourceSha256=m.sha(slam_raw), file='runtime/sfx_weapon_hammer_basic_1_01_v02.wav', sha256=m.sha(quiet), bytes=len(quiet),
                 gainDb=round(db, 2), rule='K-weighted 100 ms maximum equal to sfx_weapon_hammer_basic_0_01_v01 (hammer 1st hit)',
                 superseded='v03 replaces this in Unity (user: make the 2nd-hit thud the same as 횡강타)')
    # v03: keep the 2nd-hit swing layer; replace its "쿵" impact layer with the 횡강타 (hammer_default) skill sound,
    # peak-aligned to the old impact peak; same runtime rate compensation; loudness equal to v02 (= 1st hit).
    gm = m.RES/'audio/gamemaster-audition/source'
    hammer = m.load(m.SELECTED/'source/recipe.json')['weapons']['hammer']
    known = next(c for c in m.load(gm/'revision-02/recipes.json')['candidates']['hammer'] if c['recipeId'] == hammer['recipeId'])
    def stem(name):
        raw = (gm/known['layers']['attack2'][name]).read_bytes()
        assert m.sha(raw) == hammer['layers']['attack2'][name]['sha256'], name
        return np.asarray(m.decode(raw)), m.sha(raw)
    swing, swing_sha = stem('swing')
    impact, impact_sha = stem('impact')
    offsets = hammer['layerOffsetsMs']
    def peak_of(x):
        n = int(.005*R)
        return int(np.argmax(np.convolve(x*x, np.ones(n)/n, 'same')))
    impact_peak = round(offsets['impact']/1000*R)+peak_of(impact)
    sweep = next(r for r in records if r['id'] == 'hammer_default')
    sweep_raw = (OUT/sweep['file']).read_bytes()
    sweep_x = np.asarray(m.decode(sweep_raw))
    at = impact_peak-peak_of(sweep_x)
    assert at >= 0
    duration = next(f for f in m.load(m.MAIN/'unity/Assets/Deviant/Combat/Resources/MeleeCatalog.json')['forms'] if f['id'] == 'hammer')['basics'][1]['duration']
    timing = next(e for e in m.load(m.MAIN/'unity/Assets/Deviant/Audio/Resources/WeaponSounds/Timing.json')['entries'] if e['id'] == 'hammer_basic_1')
    rate = timing['nominalDuration']/duration
    mix = np.zeros(max(round(duration*R), round(offsets['swing']/1000*R)+len(swing), at+len(sweep_x)))
    s0 = round(offsets['swing']/1000*R)
    mix[s0:s0+len(swing)] += swing
    mix[at:at+len(sweep_x)] += sweep_x
    target_loud = k_loud(np.asarray(m.decode(quiet)))
    trial = np.asarray(m.decode(m.runtime_bytes(mix.tolist(), rate)))
    gain3 = 10**((target_loud-k_loud(trial))/20)
    v3 = m.runtime_bytes((mix*gain3).tolist(), rate)
    assert np.max(np.abs(np.asarray(m.decode(v3)))) < .8
    (OUT/'runtime/sfx_weapon_hammer_basic_1_01_v03.wav').write_bytes(v3)
    basic3 = dict(id='hammer_basic_1', replaces='runtime/sfx_weapon_hammer_basic_1_01_v02.wav', file='runtime/sfx_weapon_hammer_basic_1_01_v03.wav',
                  sha256=m.sha(v3), bytes=len(v3), layers=[
                      dict(role='2타 휘두름 (기존 그대로)', file=known['layers']['attack2']['swing'], sha256=swing_sha, offsetSeconds=offsets['swing']/1000),
                      dict(role='쿵 → 횡강타 소리', file='audio/skill-natural-tails/'+sweep['file'], sha256=m.sha(sweep_raw), offsetSeconds=round(at/R, 4),
                           alignedTo=f'old impact layer peak ({impact_sha[:12]}) at {impact_peak/R:.4f} s')],
                  runtimePlaybackRate=rate, mixGainDb=round(20*np.log10(gain3), 2),
                  rule='K-weighted 100 ms maximum of the runtime file equal to v02 (= hammer 1st hit)')
    m.dump(OUT/'source/recipe.json', dict(
        status='applied locally on user request 2026-11-16; not committed',
        reason=dict(skills='user: hammer skills and greatsword charged slam sound cut off mid-way; then other truncated skills found by the pre-fade scan',
                    basic='user: hammer basic floor slam too loud'),
        rule=dict(skills=f'same start sample, filters and level as the original cut; keep recording decay to -{END_DB:g} dB '
                         f'(or recording end) with a {END_FADE*1000:g} ms end fade; event times/gains/safe gain unchanged',
                  basic=basic['rule']),
        skills=records, basic=basic, basicV3=basic3))
    m.register_asset(OUT, 'audio.skill-natural-tails', 'natural-tail fixes for truncated skill cues (hammer, greatsword, launcher, rapier, shuriken); quieter hammer floor slam')
    print(json.dumps(dict(skills=[(r['id'], r['oldSeconds'], r['newSeconds'], r['matchBeforeOldFade']) for r in records],
                          basicGainDb=basic['gainDb']), ensure_ascii=False))


if __name__ == '__main__':
    main()
