"""Explosion re-synthesized from the measured time-frequency contour of the user's reference recording.

User (2026-11-16): every earlier candidate rejected; "make it almost exactly like the reference video".
The reference is another 2D pixel game's explosion from a screen recording. Its audio is NOT stored here.
`--analyze <wav>` measures only a contour: band power in 1/6-octave bands every 5 ms, minus the recording's
background (measured before the event), median-smoothed over 35 ms (drops an unrelated 10 ms click).
That contour (dB numbers, not playable audio) is saved to source/reference-contour.json. The build shapes fresh
seeded noise to that contour in the STFT domain with smooth, log-frequency-interpolated gains, so the result
follows the reference's attack, colour and decay while containing none of its samples.

Run from the main repository root:
  python -X utf8 resources/tools/build_explosion_reference_resynth.py --analyze <reference.wav>   (once)
  python -X utf8 resources/tools/build_explosion_reference_resynth.py
"""
import argparse
import json
import numpy as np
import build_explosion_candidates as base
import render_basic_attack_audio as m

R = m.RATE
OUT = m.RES/'audio/explosion-reference-resynth'
CONTOUR = OUT/'source/reference-contour.json'
N, HOP = 2048, 240
EVENT = (.95, 2.65)        # seconds in the extracted reference audio (first explosion)
BACKGROUND = (.30, .90)    # quiet part before it


def bands():
    edges = [31.5*2**(k/6) for k in range(0, 60)]
    return [e for e in edges if e < 20000]


def stft(x):
    win = np.hanning(N)
    pad = np.pad(x, (N, N))
    return np.array([np.fft.rfft(pad[i:i+N]*win) for i in range(0, len(pad)-N, HOP)]), len(x)


def istft(frames, length):
    win = np.hanning(N)
    out = np.zeros((len(frames)-1)*HOP+N); norm = np.zeros(len(out))
    for j, spec in enumerate(frames):
        i = j*HOP
        out[i:i+N] += np.fft.irfft(spec, N)*win; norm[i:i+N] += win**2
    return (out/np.maximum(norm, 1e-8))[N:N+length]


def band_index():
    freqs = np.fft.rfftfreq(N, 1/R); e = bands()
    idx = np.digitize(freqs, e)-1
    return freqs, e, idx


def analyze(path):
    import soundfile as sf
    from scipy.ndimage import median_filter
    x, sr = sf.read(path, always_2d=True); x = x.mean(1)
    assert sr == R, 'convert the reference to 48 kHz first'
    freqs, edges, idx = band_index()
    def band_power(seg):
        S, _ = stft(seg); P = np.abs(S)**2
        out = np.zeros((len(P), len(edges)-1))
        for b in range(len(edges)-1):
            sel = idx == b
            if sel.any():
                out[:, b] = P[:, sel].mean(1)
        return out
    ev = band_power(x[int(EVENT[0]*R):int(EVENT[1]*R)])
    bg = band_power(x[int(BACKGROUND[0]*R):int(BACKGROUND[1]*R)]).mean(0)
    # Gentle background subtraction: keep at least 10% of each frame's power so quiet bands never hit zero
    # (zeroed bands made patchy 'islands' in the tail).
    clean = np.maximum(ev-bg, .1*ev)
    clean = median_filter(clean, size=(7, 1))
    db = 10*np.log10(clean+1e-20)
    db = np.maximum(db, db.max()-80)
    # Smooth only the tail over time (from 200 ms after the loudest frame): 75 ms moving average in dB.
    from scipy.ndimage import uniform_filter1d
    peak = int(np.argmax(db.max(1)))
    start = min(len(db)-1, peak+40)
    db[start:] = uniform_filter1d(db, 15, axis=0)[start:]
    CONTOUR.parent.mkdir(parents=True, exist_ok=True)
    m.dump(CONTOUR, dict(note='Measured contour of the user reference explosion (another game, screen recording). '
                              'dB band power per 5 ms frame; no audio samples. Background subtracted (kept >=10%), '
                              '35 ms median; tail (from 200 ms after the peak) 75 ms smoothed.',
                         rate=R, n=N, hop=HOP, bandEdgesHz=[round(e, 2) for e in edges], eventSeconds=EVENT,
                         backgroundSeconds=BACKGROUND, db=[[round(float(v), 1) for v in row] for row in db]))
    print('frames', db.shape)


def resynth(seed, soft_start=False, tail=1., shift=0, steady_end=False):
    """tail < 1 shortens everything after the loudest frame; shift > 0 moves the contour up by `shift`
    1/6-octave bands (a smaller body sounds higher). Both 1/1 reproduce the reference."""
    def make():
        c = m.load(CONTOUR)
        db = np.array(c['db']); edges = np.array(c['bandEdgesHz'])
        if tail != 1:
            pk = int(np.argmax(db.max(1)))
            after = db[pk:]
            n_new = max(2, int(round(len(after)*tail)))
            src = np.linspace(0, len(after)-1, n_new)
            db = np.vstack([db[:pk], np.array([np.interp(src, np.arange(len(after)), after[:, b]) for b in range(after.shape[1])]).T])
        centers = np.sqrt(edges[:-1]*edges[1:])
        frames = len(db); length = (frames-1)*HOP
        rng = np.random.default_rng(seed)
        S, _ = stft(rng.standard_normal(length))
        S = S[:frames]
        S[np.abs(S).sum(1) == 0] = 1e-9  # padded all-zero frames would divide by zero
        freqs, _, idx = band_index()
        # Only bands that contain STFT bins carry data (low 1/6-octave bands can be narrower than one bin).
        valid = np.array([(idx == b).any() for b in range(len(centers))])
        vc = centers[valid]
        ratio = 2**(shift/6)  # frequency scaling over bands that carry data (empty low bands never move in)
        out = []
        for j in range(frames):
            row = db[j][valid]
            if shift:
                row = np.interp(np.log(vc/ratio), np.log(vc), row, left=row[0]-6*shift, right=row[-1])
            target = 10**(row/10)
            mag2 = np.abs(S[j])**2
            got = np.array([mag2[idx == b].mean() for b in np.where(valid)[0]])
            gain_db = 10*np.log10(target/np.maximum(got, 1e-30))  # dB is the same for power and amplitude ratios
            # smooth gains across frequency: interpolate band gains on a log-frequency axis
            g = np.interp(np.log(np.maximum(freqs, 1)), np.log(vc), gain_db, left=gain_db[0]-60, right=gain_db[-1])
            out.append(S[j]*10**(g/20))
        y = istft(np.array(out), length)
        info = dict(synthesized=True, method='seeded noise shaped to the measured reference contour (1/6-octave, 5 ms)',
                    seed=seed, contour='source/reference-contour.json', tailScale=tail, bandShiftSixthOctaves=shift)
        if soft_start:
            # Remove the reference's bright 60 ms flash crack ('딸깍') and open the body softly over 20 ms.
            env = np.sqrt(np.convolve(y*y, np.ones(int(.005*R))/int(.005*R), 'same'))
            on = int(np.argmax(env > env.max()*.05))
            hi = base.highpass(base.highpass(y, 1500), 1500)
            k = np.arange(len(y))
            duck = np.where((k >= on) & (k < on+int(.07*R)), 10**(-15/20), 1.)
            duck = np.convolve(duck, np.ones(int(.01*R))/int(.01*R), 'same')
            y = y-hi+hi*duck
            ramp = np.clip((k-on)/(.02*R), 0, 1)
            y *= np.where(k < on, 0, np.sin(np.pi/2*ramp)**2)
            info.update(softStart='bright flash above 1.5 kHz -15 dB for the first 70 ms; 20 ms raised-cosine swell')
        if steady_end:
            # The measured contour flattens at about -40 dB (reference background left over), which then ends
            # abruptly. From the -30 dB point, keep the decay slope the sound had from its peak to -30 dB:
            # the envelope is capped by that straight dB line until it is below -70 dB, so it fades, never stops.
            from scipy.ndimage import uniform_filter1d
            env = np.sqrt(np.convolve(y*y, np.ones(int(.01*R))/int(.01*R), 'same'))+1e-12
            edb = uniform_filter1d(20*np.log10(env), int(.03*R))
            pk = int(np.argmax(edb)); rel = edb-edb[pk]
            t30 = pk+int(np.argmax(rel[pk:] < -30))
            slope = 30/max(1, t30-pk)                       # dB per sample, same as the natural decay so far
            k = np.arange(len(y))
            ceiling = np.where(k < t30, 0., -30-slope*(k-t30))
            cut = np.minimum(0, ceiling-rel)
            cut = uniform_filter1d(cut, int(.02*R))          # smooth gain, no zipper
            y = y*10**(cut/20)
            stop = min(len(y), t30+int(45/slope))            # -75 dB on the line
            y = y[:stop]
            info.update(steadyEnd=f'from -30 dB keep the peak-to--30 dB decay slope ({slope*R:.0f} dB/s) down to -75 dB')
        # trim to the event and let it end naturally
        env = np.sqrt(np.convolve(y*y, np.ones(int(.005*R))/int(.005*R), 'same'))
        on = max(0, int(np.argmax(env > env.max()*.01))-int(.005*R))
        pk = int(np.argmax(env)); below = np.where(env[pk:] < env[pk]*10**(-60/20))[0]
        if steady_end:
            end, f = len(y), int(.01*R)   # already faded to -75 dB on its own slope; a 5 ms dip must not end it early
        else:
            end, f = (pk+int(below[0]) if len(below) else len(y)), int(.05*R)
        y = y[on:end].copy(); y[-f:] *= np.cos(np.linspace(0, np.pi/2, f))**2
        y[:int(.002*R)] *= np.linspace(0, 1, int(.002*R))
        return y, info
    return make


PICKS = [
    ('참고 영상 그대로', '퍼엉', 'synth/reference-contour', '참고 영상의 폭발을 5 ms·1/6 옥타브 단위로 측정한 모양 그대로 새 잡음을 깎아 만든 소리. 앞의 밝은 섬광음까지 포함. 끝은 -40 dB에 머물지 않고 같은 기울기로 끝까지 줄어든다(사용자 선택 후 적용).',
     dict(synth=resynth(1116, steady_end=True))),
    ('참고 영상 · 딸깍 없이', '퍼엉', 'synth/reference-contour-soft', '같은 소리에서 시작의 밝은 섬광음(약 0.07초)만 낮추고 몸통을 0.02초에 걸쳐 부드럽게 열었다.',
     dict(synth=resynth(1116, soft_start=True))),
    ('참고 영상 그대로 · 다른 잡음', '퍼엉', 'synth/reference-contour-b', '1번과 같은 모양에 다른 잡음 씨앗. 결이 조금 다르게 들리는지 비교용.',
     dict(synth=resynth(2232))),
]
SLOTS = {
    'basic': dict(name='기본 폭발', uses='폭탄 평타 · 폭탄 3연투 · 폭발 수리검', current='explosion_bomb_01.wav', candidates=PICKS),
    'large': dict(name='큰 폭발', uses='폭탄 원거리 · 후속 · 이동 스킬', current='explosion_large_01.wav', candidates=PICKS),
    'grenade': dict(name='유탄 3연쇄', uses='발사기 후속 특수기', current='explosion_grenade_01.wav',
                    candidates=[(n+' 3연쇄', v, e, d, o) for n, v, e, d, o in PICKS]),
}

if __name__ == '__main__':
    p = argparse.ArgumentParser(); p.add_argument('--analyze'); a = p.parse_args()
    if a.analyze:
        analyze(a.analyze)
    else:
        base.build(OUT, SLOTS, 'audio.explosion-reference-resynth',
                   're-synthesized from the measured reference contour; awaiting user selection; not applied',
                   title='참고 영상 재현 폭발음',
                   intro='보내 주신 참고 영상의 폭발을 0.005초 · 1/6 옥타브 단위로 측정하고(배경 잡음은 빼고), 그 모양대로 새 잡음을 깎아 만든 소리입니다. 원래 게임의 소리를 잘라 쓴 것이 아니라 측정한 모양만 따랐습니다. 「참고 파일 불러오기」로 영상과 바로 A/B 해 보세요.',
                   store='deviant-explosion-resynth-v1', reference_loader=True)
