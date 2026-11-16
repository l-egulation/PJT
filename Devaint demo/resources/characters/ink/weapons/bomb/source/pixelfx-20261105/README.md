# 폭탄 스킬 이펙트 원본 (pixelfx, 2026-11-05)

`runtime/skill-fx-20261105` 의 프레임을 만든 코드다. pixelfx(patch-gg/pixelfx, MIT) 의 `src/fx/explosions/` 에 넣고
`bake.mjs` 로 다시 구울 수 있다. 불덩이·불티·고리 방식은 pixelfx `big-explosion` 과 Sprite Fusion explosion(MIT, Hugo Duprez)을 따른다.

| 클립 | 이펙트 | 스킬 |
|---|---|---|
| basic | bomb-pop-a3 (A3 더 몽글) | 평타 |
| triple_0·1·2 | bomb-triple-hit size 1, 주황 둥글게 · 분홍 솟구침 · 하늘 옆으로 (C2-2) | 기본 특수 3연속 투척 |
| range | bomb-big-c (R3 버섯 없이) | 범위 대형 폭탄 |
| timed_fuse · timed | bomb-timed-fuse heat (F2 달아오름) · bomb-big-b (T1 버섯 기둥) | 후속타 착지 시한폭탄 |
| missile_launch·body·trail·hit | bomb-missile-launch · comet · trail fire 64px · hit-b (S2 불꽃 혜성) | 이동 미사일 변신 |

게임 1유닛 = 32px(PPU32), 초당 30장. 피벗은 이펙트 원점(폭탄 중심, ground 클립은 그 아래 땅).
