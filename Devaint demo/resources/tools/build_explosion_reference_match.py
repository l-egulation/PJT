"""Explosion candidates chosen to match the user's reference recording (not applied to the game).

The user shared a screen recording of a 2D pixel game explosion as "the feel I wanted" (2026-11-16). Measured
reference character: a tiny bright click, then a soft body that swells over ~125 ms; energy almost entirely
below 500 Hz (0.5-6 kHz about 13 dB lower, almost nothing above 6 kHz); smooth decay of -20 dB at 0.4 s,
-30 dB at 0.75 s, -40 dB at ~1.05 s. Purchased sources were ranked by envelope + band-shape distance to that
reference, as-is and with a gentle low-pass; the best match of each sound family is used here. Original speed,
no tail cut, 30 Hz rumble cut, optional 12 dB/oct low-pass to reach the reference's darkness, level equal to
the slot's current file. The reference recording itself is NOT embedded; the page can load it locally.

Run from the main repository root: python -X utf8 resources/tools/build_explosion_reference_match.py
"""
import build_explosion_candidates as base
import render_basic_attack_audio as m

S = 'Sci-Fi Weapons/'
G = 'Magic_Spells/'
E = 'Explosion_Fire_Gas/'
PICKS = [  # name, voice, entry, description, options; final distance to the reference is in README/recipes
    ('어두운 신스 펑', '푸웅', S+'sci-fi_weapon_blaster_laser_boom_zap_02.wav',
     '측정상 참고와 가장 가까움. 합성 폭발음을 1.5 kHz 저역 통과로 어둡게 했다. 앞의 레이저 결이 조금 남을 수 있다.', dict(lowpass=1500)),
    ('부드러운 신스 붐', '푸엉', S+'sci-fi_weapon_blaster_laser_boom_03.wav',
     '감쇠 모양이 참고와 가장 비슷함. 합성 폭발음을 3 kHz 저역 통과로 다듬었다.', dict(lowpass=3000)),
    ('얼음 마법 폭발', '퍼엉', G+'ice_blast_projectile_spell_02.wav',
     '마법 폭발 중 가장 가까움. 얼음 파편 결을 3 kHz 저역 통과로 눌렀다.', dict(lowpass=3000)),
    ('덩굴 마법 폭발', '쿠웅', G+'nature_spell_vines_blast_impact3.wav',
     '가공 없이 가까운 것 중 1위. 둔탁하고 흙먼지 같은 마법 폭발.', {}),
    ('작은 실제 폭발 · 어둡게', '펑', E+'explosion_small_02.wav',
     '실제 폭발 중 감쇠가 가장 비슷함. 3 kHz 저역 통과로 파편 소리를 눌러 덜 위협적으로 했다.', dict(lowpass=3000)),
    ('둥근 에너지 폭발', '퐁', G+'energy_blast_small_02.wav',
     '에너지 마법 중 가장 가까움. 가공 없이 둥근 몸통.', {}),
]
SLOTS = {
    'basic': dict(name='기본 폭발', uses='폭탄 평타 · 폭탄 3연투 · 폭발 수리검', current='explosion_bomb_01.wav', candidates=PICKS),
    'large': dict(name='큰 폭발', uses='폭탄 원거리 · 후속 · 이동 스킬', current='explosion_large_01.wav', candidates=PICKS),
    'grenade': dict(name='유탄 3연쇄', uses='발사기 후속 특수기', current='explosion_grenade_01.wav',
                    candidates=[(n+' 3연쇄', v*2 if len(v) < 3 else v, e, d, o) for n, v, e, d, o in PICKS]),
}

if __name__ == '__main__':
    base.build(m.RES/'audio/explosion-candidates-reference-match', SLOTS, 'audio.explosion-candidates-reference-match',
               'candidates matched to the user reference recording; awaiting user selection; not applied',
               title='참고 영상에 맞춘 폭발음 후보',
               intro='보내 주신 영상의 폭발음을 측정해 고른 후보입니다. 그 소리는 앞에 작은 딸깍 뒤 몸통이 약 0.13초에 걸쳐 부풀고, 거의 500 Hz 아래에 에너지가 모여 있으며(파편·지글거림이 거의 없음), 1초쯤 부드럽게 사라집니다. 구매팩의 폭발 계열을 이 모양과 가까운 순서로 줄 세워, 계열마다 가장 가까운 것을 골랐습니다. 속도를 바꾸거나 꼬리를 자르지 않았고, 필요한 경우만 부드러운 저역 통과로 어둡게 했습니다. 아래 「참고 파일 불러오기」로 영상을 열면 같은 크기로 맞춰 A/B 할 수 있습니다.',
               store='deviant-explosion-refmatch-v1', reference_loader=True)
