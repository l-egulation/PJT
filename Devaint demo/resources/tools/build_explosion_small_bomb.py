"""Smaller-bomb versions of reference-resynth #1 for the basic explosion slot (not applied to the game).

User (2026-11-16): large and grenade explosions use reference-resynth #1; the basic explosion should feel like a
somewhat smaller bomb than #1. A smaller explosion keeps the character but decays sooner, sits a little higher
(smaller bodies resonate higher) and is a little quieter, so the measured contour of #1 is shortened after its
peak, moved up by 1/6-octave steps and set a little below the slot's current level. Same seed as #1.

Run from the main repository root: python -X utf8 resources/tools/build_explosion_small_bomb.py
"""
import build_explosion_candidates as base
import build_explosion_reference_resynth as rr
import render_basic_attack_audio as m

PICKS = [
    ('참고 1번 (큰 폭발과 같은 크기)', '퍼엉', 'synth/reference-contour', '큰 폭발·유탄에 쓰기로 한 1번 그대로. 비교 기준.',
     dict(synth=rr.resynth(1116, steady_end=True))),
    ('조금 작은 폭탄', '펑', 'synth/reference-contour-small-1', '1번의 꼬리를 0.75배로 줄이고 1/6 옥타브 높이고 1.5 dB 작게.',
     dict(synth=rr.resynth(1116, tail=.75, shift=1, steady_end=True), levelOffsetDb=-1.5)),
    ('작은 폭탄', '펑', 'synth/reference-contour-small-2', '꼬리 0.6배, 1/3 옥타브 높이고 2.5 dB 작게.',
     dict(synth=rr.resynth(1116, tail=.6, shift=2, steady_end=True), levelOffsetDb=-2.5)),
    ('더 작은 폭탄', '퐁', 'synth/reference-contour-small-3', '꼬리 0.45배, 1/2 옥타브 높이고 3.5 dB 작게.',
     dict(synth=rr.resynth(1116, tail=.45, shift=3, steady_end=True), levelOffsetDb=-3.5)),
    # User: shorter and smaller than 4 is fine, but it must never feel cut off. The whole decay curve is
    # time-scaled, and (steady_end) from -30 dB it keeps its own decay slope down to -75 dB instead of the
    # measured -40 dB plateau, so it fades out completely.
    ('아주 작은 폭탄', '퐁', 'synth/reference-contour-small-4', '꼬리 0.35배, 2/3 옥타브 높이고 4.5 dB 작게.',
     dict(synth=rr.resynth(1116, tail=.35, shift=4, steady_end=True), levelOffsetDb=-4.5)),
    ('꼬마 폭탄', '뽕', 'synth/reference-contour-small-5', '꼬리 0.28배, 5/6 옥타브 높이고 5.5 dB 작게.',
     dict(synth=rr.resynth(1116, tail=.28, shift=5, steady_end=True), levelOffsetDb=-5.5)),
    ('가장 작은 폭탄', '뽁', 'synth/reference-contour-small-6', '꼬리 0.22배, 1옥타브 높이고 6.5 dB 작게.',
     dict(synth=rr.resynth(1116, tail=.22, shift=6, steady_end=True), levelOffsetDb=-6.5)),
]
SLOTS = {'basic': dict(name='기본 폭발', uses='폭탄 평타 · 폭탄 3연투 · 폭발 수리검', current='explosion_bomb_01.wav', candidates=PICKS)}

if __name__ == '__main__':
    base.build(m.RES/'audio/explosion-small-bomb', SLOTS, 'audio.explosion-small-bomb',
               'smaller-bomb versions of reference-resynth #1 for the basic slot; awaiting user selection; not applied',
               title='기본 폭발 · 작은 폭탄 후보',
               intro='큰 폭발·유탄에 쓰기로 한 참고 영상 재현 1번을 바탕으로, 기본 폭발용으로 규모가 작은 폭탄 느낌을 세 단계로 만들었습니다. 작은 폭발일수록 꼬리가 짧고, 소리가 조금 높고, 조금 작습니다. 「지금 소리」는 현재 게임의 기본 폭발입니다.',
               store='deviant-explosion-smallbomb-v1', reference_loader=True)
