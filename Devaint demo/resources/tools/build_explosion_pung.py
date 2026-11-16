"""'펑' explosion candidates after user feedback on explosion-six-fix (not applied to the game).

Feedback (2026-11-16): six-fix 2/3 still sound like a dull knock ('탕'), synth 4/5 sound like a bomb far away
outside, remove the click ('딸깍'), make something that fits our game.
Decisions:
- Stop patching candidate 6. Its siblings energy_blast_small_04 / _05 (same synthesized magic-boom family, round
  low body) show no laser chirp or comb lines in their spectrograms, so they are used as they are, bright
  enough to stay close (no low-pass).
- 'Far away' cues removed from the synth: no 500 Hz darkening, no long low rumble; a dry body with mid presence
  and a short tail. 'Knock'/click cues removed everywhere: no added click, every candidate starts with a 12 ms
  raised-cosine fade-in, and the synth swells over 20 ms.
Loudness equals the slot's current file like the earlier pools.

Run from the main repository root: python -X utf8 resources/tools/build_explosion_pung.py
"""
import numpy as np
import build_explosion_candidates as base
import render_basic_attack_audio as m

R = m.RATE
G = 'Magic_Spells/'
SOFT_ATTACK = .012


def soft_attack(x):
    x = np.asarray(x, float).copy()
    n = int(SOFT_ATTACK*R)
    x[:n] *= np.sin(np.linspace(0, np.pi/2, n))**2
    return x, dict(softAttackSeconds=SOFT_ATTACK, softAttack='raised-cosine fade-in so no onset click')


def close_pung(seed, presence_hz, body_hz, decay40):
    """Close, dry '펑' from seeded noise: low 'whump' body + mid presence puff, 20 ms swell, no click."""
    def make():
        rng = np.random.default_rng(seed)
        n = int(1.0*R); t = np.arange(n)/R
        whump = base.lowpass(base.lowpass(rng.standard_normal(n), body_hz), body_hz)      # round low body
        whump = base.highpass(whump, 45)                                                  # no sub rumble (distance cue)
        puff = base.lowpass(base.lowpass(rng.standard_normal(n), presence_hz), presence_hz)
        puff = base.highpass(base.highpass(puff, 300), 300)                               # mid 'air' of the puff
        attack = .02
        swell = np.where(t < attack, np.sin(np.pi/2*t/attack)**2, 1.)
        after = np.maximum(0, t-attack)
        body_db = np.where(after < .12, -6*after/.12, -6-34*(after-.12)/(decay40-.12))   # -40 dB at decay40
        puff_db = -45*after/(decay40*.7)                                                   # the puff clears sooner
        y = (whump/np.std(whump))*10**(body_db/20) + .55*(puff/np.std(puff))*10**(puff_db/20)
        y *= swell
        end = min(n, int((decay40+.08)*R)); y = y[:end]
        f = int(.06*R); y[-f:] *= np.cos(np.linspace(0, np.pi/2, f))**2
        return y, dict(synthesized=True, seed=seed, bodyLowpassHz=body_hz, bodyHighpassHz=45, presenceBandHz=[300, presence_hz],
                       swellSeconds=attack, minus40dbAtSeconds=decay40, click=False, pitchedComponents=False)
    return make


PICKS = [
    ('6번 원본 (비교용)', '퐁', G+'energy_blast_small_02.wav', '고르셨던 6번. 레이저 결이 섞여 있다. 비교 기준.', dict(process=soft_attack)),
    ('6번 형제 A', '펑', G+'energy_blast_small_04.wav', '6번과 같은 마법 폭발 묶음 중 레이저 줄무늬가 보이지 않는 것. 가공 없이 둥근 몸통 그대로, 0.9초.',
     dict(process=soft_attack)),
    ('6번 형제 B', '퍼엉', G+'energy_blast_small_05.wav', '같은 묶음에서 레이저 줄무늬가 없고 꼬리가 조금 더 긴 것(1.2초).',
     dict(process=soft_attack)),
    ('가까운 펑', '펑', 'synth/close-pung', '코드로 만든 소리. 둥근 저음 몸통 + 중음 「훅」 공기감, 0.02초에 부풀고 0.7초 안에 사라진다. 딸깍·먼 울림 없음.',
     dict(synth=close_pung(20261118, 2600, 260, .7))),
    ('가까운 펑 · 둥글게', '퐁', 'synth/close-pung-round', '같은 합성에서 공기감을 줄이고(1.6 kHz) 몸통을 조금 더 둥글게(200 Hz), 0.55초로 짧게.',
     dict(synth=close_pung(20261119, 1600, 200, .55))),
]
SLOTS = {
    'basic': dict(name='기본 폭발', uses='폭탄 평타 · 폭탄 3연투 · 폭발 수리검', current='explosion_bomb_01.wav', candidates=PICKS),
    'large': dict(name='큰 폭발', uses='폭탄 원거리 · 후속 · 이동 스킬', current='explosion_large_01.wav', candidates=PICKS),
    'grenade': dict(name='유탄 3연쇄', uses='발사기 후속 특수기', current='explosion_grenade_01.wav',
                    candidates=[(n+' 3연쇄', v, e, d, o) for n, v, e, d, o in PICKS]),
}

if __name__ == '__main__':
    base.build(m.RES/'audio/explosion-pung', SLOTS, 'audio.explosion-pung',
               'pung candidates after six-fix feedback; awaiting user selection; not applied',
               title='「펑」 폭발음 후보',
               intro='6번을 고쳐 쓰는 대신, 같은 마법 폭발 묶음에서 레이저 줄무늬가 없는 형제 2개를 그대로 쓰고, 「멀리서 터진 소리」의 원인(지나치게 어둡게 깎기·긴 저음 울림)과 「딸깍」을 뺀 가까운 합성 「펑」 2개를 새로 만들었습니다. 모든 후보는 시작을 0.012초 부드럽게 열어 딸깍이 없습니다.',
               store='deviant-explosion-pung-v1', reference_loader=True)
