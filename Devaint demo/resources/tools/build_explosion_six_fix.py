"""Candidate 6 (energy_blast_small_02) without its laser-like parts, plus a synthesized reference-shaped option.

User (2026-11-16): picked reference-match candidate 6 but hears a laser sound mixed in; also allowed free,
commercially usable alternatives beyond the purchased pack. Two synthetic elements in the source can read as
"laser": (A) a pitched low tone falling from ~100 Hz to ~40 Hz under the boom, (B) the noise's upper edge
sliding down from ~3 kHz to ~1.5 kHz around 0.3-0.45 s (a filter sweep). The chosen fix removes narrow pitched peaks frame by frame (the chirp and all its harmonics) and keeps the noise body; a darker version adds a 2 kHz low-pass.
The synthesized options are generated here from seeded noise only (no pitched tone), shaped to the measured
reference envelope, so they carry no third-party license.

Run from the main repository root: python -X utf8 resources/tools/build_explosion_six_fix.py
"""
import numpy as np
import build_explosion_candidates as base
import render_basic_attack_audio as m

R = m.RATE
SIX = 'Magic_Spells/energy_blast_small_02.wav'


def remove_falling_tone(x):
    """Spectral notch that follows the strongest 25-140 Hz peak frame by frame (the falling pitched tone)."""
    n, hop = 4096, 256
    win = np.hanning(n)
    pad = np.pad(x, (n, n))
    frames = range(0, len(pad)-n, hop)
    out = np.zeros(len(pad)); norm = np.zeros(len(pad))
    freqs = np.fft.rfftfreq(n, 1/R)
    band = np.where((freqs >= 25) & (freqs <= 140))[0]
    removed = []
    for i in frames:
        spec = np.fft.rfft(pad[i:i+n]*win)
        mag = np.abs(spec)
        k = band[np.argmax(mag[band])]
        local = np.median(mag[max(1, k-40):k+40])
        if mag[k] > 4*local:  # a clear tonal peak (>12 dB over its neighbourhood)
            taper = np.ones(len(spec))
            for d in range(-4, 5):
                if 0 <= k+d < len(spec):
                    taper[k+d] = 10**(-30/20)+(1-10**(-30/20))*(abs(d)/5)**2
            spec *= taper
            removed.append(freqs[k])
        out[i:i+n] += np.fft.irfft(spec, n)*win
        norm[i:i+n] += win**2
    y = (out/np.maximum(norm, 1e-8))[n:n+len(x)]
    return y, dict(fallingToneNotch='-30 dB notch, +-4 bins (11.7 Hz bins) on the strongest 25-140 Hz peak when >12 dB over its neighbourhood',
                   notchedFrames=len(removed), notchedRangeHz=[round(min(removed)), round(max(removed))] if removed else None)


def remove_sweep(x):
    """Fixed 1 kHz 24 dB/oct low-pass: the sliding 3 -> 1.5 kHz edge is removed, so nothing audibly moves."""
    y = base.lowpass(base.lowpass(x, 1000), 1000)
    return y, dict(sweepRemoval='fixed 1 kHz low-pass, 4th order')


def both(x):
    y, a = remove_falling_tone(x)
    y, b = remove_sweep(y)
    return y, {**a, **b}


def remove_tonal(x, prominence_db=9., ceiling_db=4.):
    """Remove narrow pitched peaks (the falling 'pew' chirp and its harmonics) frame by frame; keep the noise body.

    STFT 2048/256. In each frame a bin is tonal when it stands more than `prominence_db` above the median of its
    +-10 neighbouring bins; such bins are lowered to `ceiling_db` above that median. Phase is kept."""
    from scipy.ndimage import median_filter
    n, hop = 2048, 256
    win = np.hanning(n)
    pad = np.pad(x, (n, n))
    out = np.zeros(len(pad)); norm = np.zeros(len(pad)); touched = 0; frames = 0
    for i in range(0, len(pad)-n, hop):
        spec = np.fft.rfft(pad[i:i+n]*win)
        mag = np.abs(spec)
        med = median_filter(mag, size=21, mode='nearest')+1e-12
        tonal = mag > med*10**(prominence_db/20)
        if tonal.any():
            spec[tonal] *= (med[tonal]*10**(ceiling_db/20))/mag[tonal]
            touched += int(tonal.sum()); frames += 1
        out[i:i+n] += np.fft.irfft(spec, n)*win
        norm[i:i+n] += win**2
    y = (out/np.maximum(norm, 1e-8))[n:n+len(x)]
    return y, dict(tonalRemoval=f'STFT 2048/256; bins >{prominence_db:g} dB over the +-10-bin median lowered to +{ceiling_db:g} dB',
                   tonalBins=touched, tonalFrames=frames)


def tonal_dark(x):
    y, a = comb_only_fix(x)
    return base.lowpass(y, 2000), {**a, 'lowpassHz': 2000}


def remove_comb(x, width=.2):
    """Remove the sweeping comb-filter ripple (flanger/jet 'laser' texture) from the noise.

    STFT 1024/128. Each frame's magnitude is replaced by its average over a window of +-`width` x frequency
    (about half an octave), so moving peaks/notches disappear while the broad spectral shape, the phase and the
    timing stay. Bins below 300 Hz are left to the pitch-tracking notch."""
    n, hop = 1024, 128
    win = np.hanning(n)
    pad = np.pad(x, (n, n))
    freqs = np.fft.rfftfreq(n, 1/R)
    k = np.arange(len(freqs)); half = np.maximum(1, np.round(k*width)).astype(int)
    lo = np.maximum(0, k-half); hi = np.minimum(len(k)-1, k+half)
    keep_low = freqs < 300
    out = np.zeros(len(pad)); norm = np.zeros(len(pad))
    for i in range(0, len(pad)-n, hop):
        spec = np.fft.rfft(pad[i:i+n]*win)
        power = np.abs(spec)**2
        c = np.concatenate([[0], np.cumsum(power)])
        smooth = np.sqrt((c[hi+1]-c[lo])/(hi-lo+1))
        mag = np.abs(spec)+1e-12
        gain = np.where(keep_low, 1., smooth/mag)
        out[i:i+n] += np.fft.irfft(spec*gain, n)*win
        norm[i:i+n] += win**2
    y = (out/np.maximum(norm, 1e-8))[n:n+len(x)]
    return y, dict(combRemoval=f'STFT 1024/128; magnitude averaged over +-{width:g} x frequency above 300 Hz (phase kept)')


def laser_free(x):
    """Superseded STFT version (user: sounded messy). Kept for the record; clean_laser_free is used."""
    y, a = remove_falling_tone(x)
    y, b = remove_comb(y)
    return y, {**a, **b}


def track_tone(x, lo=25., hi=150., hop=.005):
    """Pitch track of the falling low tone: strongest 25-150 Hz peak per 5 ms (8192-point frames, parabolic
    interpolation), kept only where it stands >12 dB over its neighbourhood, then median/mean smoothed."""
    from scipy.ndimage import median_filter, uniform_filter1d
    n = 8192; h = int(hop*R); win = np.hanning(n)
    pad = np.pad(x, (n//2, n//2))
    freqs = np.fft.rfftfreq(n, 1/R); band = np.where((freqs >= lo) & (freqs <= hi))[0]
    f0, on = [], []
    for i in range(0, len(x), h):
        mag = np.abs(np.fft.rfft(pad[i:i+n]*win, n))
        k = band[np.argmax(mag[band])]
        a, b, c = np.log(mag[k-1:k+2]+1e-12)
        delta = .5*(a-c)/(a-2*b+c) if a-2*b+c != 0 else 0
        f0.append((k+delta)*R/n)
        on.append(mag[k] > 4*np.median(mag[max(1, k-60):k+60]))
    f0 = uniform_filter1d(median_filter(np.array(f0), 9), 5); on = median_filter(np.array(on, float), 9) > .5
    return f0, on, h


def notch_track(x, f0, on, h, q=5., passes=2):
    """Time-varying RBJ notch (Q 5) following the tracked pitch; coefficients update every 32 samples.
    Plain IIR filtering in the time domain: no STFT artefacts."""
    y = np.asarray(x, float).copy()
    for _ in range(passes):
        out = np.zeros_like(y); x1 = x2 = y1 = y2 = 0.
        for s in range(0, len(y), 32):
            idx = min(len(f0)-1, s//h)
            seg = y[s:s+32]
            if on[idx]:
                w = 2*np.pi*f0[idx]/R; al = np.sin(w)/(2*q); c = np.cos(w)
                b = np.array([1, -2*c, 1])/(1+al); a1, a2 = -2*c/(1+al), (1-al)/(1+al)
            else:
                b = np.array([1., 0, 0]); a1 = a2 = 0.
            for j, v in enumerate(seg):
                o = b[0]*v+b[1]*x1+b[2]*x2-a1*y1-a2*y2
                x2, x1, y2, y1 = x1, v, y1, o
                out[s+j] = o
        y = out
    return y


def lr4(x, hz, kind):
    f = base.lowpass if kind == 'lp' else base.highpass
    return f(f(x, hz), hz)  # Linkwitz-Riley 4th order = two cascaded 2nd-order Butterworth sections


def replace_high_band(x, split=900., seed=6006, release=.06):
    """Split at 900 Hz (Linkwitz-Riley 4th order). Keep the low band. Replace the high band - where the sliding
    comb ('jet/laser' texture) lives - with fresh noise that has the high band's average spectral colour and
    follows its 4 ms loudness envelope. No comb can survive in independent noise; the attack/decay stays."""
    low = lr4(x, split, 'lp'); high = lr4(x, split, 'hp')
    env = np.sqrt(np.convolve(high*high, np.ones(int(.004*R))/int(.004*R), 'same'))
    # The source gates its bright band off abruptly at ~0.62 s; let the new band fade with a 60 ms release instead.
    fall = np.exp(-1/(release*R))
    for i in range(1, len(env)):
        env[i] = max(env[i], env[i-1]*fall)
    active = env > env.max()*.05
    spec = np.abs(np.fft.rfft(high*active))
    from scipy.ndimage import uniform_filter1d
    colour = uniform_filter1d(spec, 400)  # broad average colour only: no ripple carried over
    rng = np.random.default_rng(seed)
    noise = np.fft.irfft(np.fft.rfft(rng.standard_normal(len(x)))*colour, len(x))
    noise = lr4(noise, split, 'hp')
    nenv = np.sqrt(np.convolve(noise*noise, np.ones(int(.004*R))/int(.004*R), 'same'))+1e-9
    new_high = noise/nenv*env
    return low+new_high, dict(highBandReplacement=f'split {split:g} Hz LR4; high band replaced by seeded ({seed}) noise with the '
                                                     'same average colour and 4 ms envelope with a 60 ms release (no abrupt gate)')


def comb_only_fix(x):
    """Keep the falling low boom (it IS the '펑' body: ~75% of the energy below 150 Hz). Only the jet-like comb
    texture above 900 Hz is replaced with clean noise following the original envelope (60 ms release)."""
    y, info = replace_high_band(x)
    return y, {**info, 'lowBoomKept': True,
               'note': 'previous version also notched the falling 124->26 Hz tone; user heard a dull knock (탕), so the boom is kept'}


def clean_laser_free(x):
    f0, on, h = track_tone(x)
    y = notch_track(x, f0, on, h)
    y, info = replace_high_band(y)
    tracked = f0[on]
    return y, {**info, 'toneNotch': 'time-varying RBJ notch Q5 x2 following the tracked 25-150 Hz pitch (5 ms track)',
               'trackedRangeHz': [round(float(tracked.min())), round(float(tracked.max()))] if len(tracked) else None,
               'method': 'time-domain filtering + band replacement (no STFT magnitude editing)'}


def synth(seed, cutoff, click_db):
    """Soft explosion from seeded noise only, shaped to the measured reference (no pitched components)."""
    def make():
        rng = np.random.default_rng(seed)
        n = int(1.4*R); t = np.arange(n)/R
        body = rng.standard_normal(n)
        body = base.lowpass(base.lowpass(body, cutoff), cutoff)      # dark body, like the reference (<500 Hz focus)
        rumble = base.lowpass(base.lowpass(rng.standard_normal(n), 140), 140)
        body = body/np.std(body) + .7*rumble/np.std(rumble)
        peak_t = .125                                                  # reference: body swells over ~125 ms
        swell = np.where(t < peak_t, .15+.85*np.sin(np.pi/2*t/peak_t)**2, 1.)
        after = np.maximum(0, t-peak_t)                                # reference: -20 dB @0.4 s, -30 @0.75, -40 @~1.05
        decay_db = np.where(after < .275, -20*after/.275, -20-20*(after-.275)/.65)
        y = body*swell*10**(decay_db/20)
        click = rng.standard_normal(int(.004*R))*np.hanning(int(.004*R))
        click = base.highpass(click, 2000)
        y[:len(click)] += click/np.max(np.abs(click))*np.max(np.abs(y[:int(.2*R)]))*10**(click_db/20)
        end = int(1.3*R); y = y[:end]; f = int(.08*R); y[-f:] *= np.cos(np.linspace(0, np.pi/2, f))**2
        return y, dict(synthesized=True, seed=seed, bodyLowpassHz=cutoff, rumbleLowpassHz=140, swellSeconds=peak_t,
                       decay='-20 dB at 0.4 s, -40 dB at ~1.05 s', clickDb=click_db, pitchedComponents=False)
    return make


PICKS = [
    ('6번 원본', '퐁', SIX, '선택한 6번 그대로. 비교 기준.', {}),
    ('6번 · 레이저 결 제거', '퐁', SIX, '「펑」의 몸통인 낮은 울림은 그대로 두고, 제트기 같은 빗살 무늬(레이저 결)가 있던 900 Hz 위만 같은 크기 변화·같은 색의 깨끗한 잡음으로 바꿨다.',
     dict(process=comb_only_fix)),
    ('6번 · 레이저 결 제거 + 어둡게', '퍼엉', SIX, '2번에 2 kHz 저역 통과를 더해 참고 영상처럼 조금 더 어둡고 둥글게 했다.',
     dict(process=tonal_dark)),
    ('합성 퍼엉 · 어둡게', '퍼엉', 'synth/soft-boom-dark', '참고 영상의 측정값대로 코드에서 만든 소리. 음정 없는 잡음만 써서 레이저 결이 생길 수 없다. 외부 라이선스 없음.',
     dict(synth=synth(20261116, 500, -10))),
    ('합성 퍼엉 · 조금 밝게', '펑', 'synth/soft-boom-bright', '같은 합성에 몸통을 조금 밝게(900 Hz) 하고 앞 딸깍을 키웠다.',
     dict(synth=synth(20261117, 900, -4))),
]
SLOTS = {
    'basic': dict(name='기본 폭발', uses='폭탄 평타 · 폭탄 3연투 · 폭발 수리검', current='explosion_bomb_01.wav', candidates=PICKS),
    'large': dict(name='큰 폭발', uses='폭탄 원거리 · 후속 · 이동 스킬', current='explosion_large_01.wav', candidates=PICKS),
    'grenade': dict(name='유탄 3연쇄', uses='발사기 후속 특수기', current='explosion_grenade_01.wav',
                    candidates=[(n+' 3연쇄', v, e, d, o) for n, v, e, d, o in PICKS]),
}

if __name__ == '__main__':
    base.build(m.RES/'audio/explosion-six-fix', SLOTS, 'audio.explosion-six-fix',
               'candidate 6 without laser-like parts + synthesized option; awaiting user selection; not applied',
               title='6번 레이저 결 제거 · 합성 폭발',
               intro='고르신 6번에서 레이저처럼 들리는 부분, 즉 아래로 미끄러지는 음정과 그 배음만 골라 뺀 안(그대로·조금 어둡게)과, 참고 영상 측정값대로 코드에서 직접 만든 합성 폭발 2개입니다. 크기는 지금 소리와 같게 맞췄습니다. 「참고 파일 불러오기」로 영상과 A/B 할 수 있습니다.',
               store='deviant-explosion-sixfix-v1', reference_loader=True)
