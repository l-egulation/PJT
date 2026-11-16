"""Less threatening, stylized explosion candidates for a 2D pixel side-scroller (not applied to the game).

User feedback 2026-11-16: the real explosion recordings feel too realistic and threatening; trimming and
speeding them up was not what was wanted. This set changes the SOURCE instead: magic smoke poofs, round
energy blasts, 8-bit retro explosions and sparkle pops. Same processing as the first pool: original speed,
30 Hz rumble cut, the recording's own decay, loudness equal to the slot's current file.

Run from the main repository root: python -X utf8 resources/tools/build_explosion_stylized.py
"""
import build_explosion_candidates as base
import render_basic_attack_audio as m

M = 'Magic_Spells/'
X = 'Retro_Classic/Explosion/'
SLOTS = {
    'basic': dict(name='기본 폭발', uses='폭탄 평타 · 폭탄 3연투 · 폭발 수리검', current='explosion_bomb_01.wav', candidates=[
        ('연기 펑', '펑!', M+'vanish_spell_flash_potion_01.wav', '마법 연기가 퐁 하고 터지는 소리. 만화 폭탄이 연기를 내며 사라지는 느낌이다.'),
        ('둥근 마법 펑', '퐁', M+'energy_blast_small_01.wav', '에너지 마법 폭발. 날카로운 파편 소리 없이 둥글게 퍼진다.'),
        ('픽셀 폭발', '치익쾅', X+'retro_explosion_general_03.wav', '8비트 게임의 노이즈 폭발. 고전 픽셀 게임 그대로의 질감이다.'),
        ('픽셀 폭발 · 긴 꼬리', '치이익', X+'retro_explosion_general_09.wav', '8비트 폭발 중 꼬리가 부드럽게 줄어드는 것.'),
        ('반짝 팝', '뽕', M+'magic_pop_open_03.wav', '반짝이며 터지는 마법 팝. 가장 귀엽고 가볍다. 반짝임 꼬리가 길게 남는다.'),
    ]),
    'large': dict(name='큰 폭발', uses='폭탄 원거리 · 후속 · 이동 스킬', current='explosion_large_01.wav', candidates=[
        ('큰 마법 펑', '푸왕', M+'energy_blast_large_02.wav', '큰 에너지 마법 폭발. 무겁지만 실제 폭탄의 파편·잔향은 없다.'),
        ('둥근 마법 펑 · 큰', '쿠퐁', M+'energy_blast_small_04.wav', '작은 에너지 폭발 중 몸통이 큰 것. 큰 폭발 자리에 가볍게 쓰는 안.'),
        ('픽셀 큰 폭발', '콰콰쾅', X+'retro_explosion_general_01.wav', '8비트 큰 폭발. 꼬리가 1초가량 이어진다.'),
        ('픽셀 깊은 폭발', '쿵쾅', X+'retro_explosion_deep_06.wav', '8비트 깊은 폭발. 저음이 조금 더 있다.'),
        ('긴 연기 펑', '퍼어엉', M+'vanish_spell_flash_potion_02.wav', '연기가 크게 피어오르는 마법 소리. 반짝임과 함께 길게 퍼진다.'),
    ]),
    'grenade': dict(name='유탄 3연쇄', uses='발사기 후속 특수기', current='explosion_grenade_01.wav', candidates=[
        ('연기 펑 3연쇄', '펑펑펑', M+'vanish_spell_flash_potion_01.wav', '마법 연기 펑을 지금과 같은 간격으로 세 번.'),
        ('마법 펑 3연쇄', '퐁퐁퐁', M+'energy_blast_small_03.wav', '짧은 에너지 마법 폭발 세 번.'),
        ('픽셀 펑 3연쇄', '팡팡팡', X+'retro_explosion_general_07.wav', '8비트 폭발 세 번.'),
        ('픽셀 짧은 펑 3연쇄', '탕탕탕', X+'retro_explosion_short_02.wav', '8비트 짧은 폭발 세 번. 가장 짧다.'),
        ('반짝 팝 3연쇄', '뽕뽕뽕', M+'magic_pop_open_01.wav', '반짝 마법 팝 세 번. 가장 귀엽다.'),
    ]),
}

if __name__ == '__main__':
    base.build(m.RES/'audio/explosion-candidates-stylized', SLOTS, 'audio.explosion-candidates-stylized',
               'stylized less-threatening explosion candidates; awaiting user selection; not applied',
               title='덜 위협적인 폭발음 후보',
               intro='실제 폭탄 녹음 대신, 2D 픽셀 횡스크롤에 어울리는 덜 위협적인 원음으로 다시 고른 후보입니다. 마법 연기 펑, 둥근 에너지 폭발, 8비트 픽셀 폭발, 반짝 팝 계열입니다. 속도를 바꾸거나 꼬리를 자르지 않았고, 30 Hz 아래 들리지 않는 울림만 걷어냈습니다.',
               store='deviant-explosion-stylized-v1')
