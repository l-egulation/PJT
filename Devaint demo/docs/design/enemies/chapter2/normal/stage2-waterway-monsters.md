# 2스테이지 지하수로 몬스터 전투

[일반 몬스터 개요](README.md)

> `python tools/stage2_monster_concepts/build_combat_plan.py`로 생성한다. 일반5종 편집 원본은 `data/combat/stage2_*.json`이다.

일반5종은 확정 수치·판정·기존 프레임 시간표를 Chapter2 Unity 전투에 연결한다. 기존 이미지와 FX를 사용하며 새 스프라이트를 만들지 않는다.

| 몬스터 | HP | 이동 u/s | 공격 시작 수평거리 u | 회복 뒤 대기 초 |
|---|---:|---:|---:|---:|
| 등짐분사수 | 70 | 2.4 | 5.5 | 1.2 |
| 쐐기보행기 | 140 | 1.6 | 7.5 | 1.8 |
| 먹침필경사 | 60 | 2.8 | 3.5 | 0.9 |
| 늘어진수의 | 60 | 1.2 | 11 | 1.8 |
| 신호투척수 | 50 | 1.8 | 10 | 1.4 |

## 일반5종 기준

- [피격·공격 박스](stage2-monster-geometry.md)
- [피해·연타·피격 반응과 공격 취소 예외](stage2-monster-damage-reaction.md)
- [공격 프레임과 시간](stage2-monster-attack-timing.md)
- [체력·이동·거리·대기](stage2-monster-stats.md)
- [조준·투사체·장판](stage2-projectiles.md)
- [첫 공격·화면·층간 접근](stage2-monster-engagement.md)
- [Unity 연결과 검사 방법](../../../../development/testing/stage2-unity-validation.md)
- [남은 결정](stage2-monster-pending.md)

몸 접촉 피해는 없다. 본체 전조·회복과 이미 발사된 탄·장판의 시계는 분리한다. 분사는 최대3회, 찌르기는3연타, 돌진은 최초접촉1회 후 실제 정지 기준 내려찍기로 이어진다.

일반5종은 배치된 바닥 구역에서만 이동한다. 원거리 탄은 발사 직전 조준한 바닥만 충돌 대상으로 고정하고 벽·천장·다른 바닥을 통과한다. 세부 예외는 해당 편집 원본과 위 명세를 따른다.

## 엘리트와 보스

일반5종 확정을 별도 콘텐츠의 미정 수치까지 확대하지 않는다. 현재 기준은 [A3 엘리트](../elite/chapter2-elite-three-patterns.md)와 [Rose/Blue 보스](../rose-blue/chapter2-rose-blue-boss.md)를 따른다.
