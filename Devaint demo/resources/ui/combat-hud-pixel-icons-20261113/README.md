# 전투 HUD 무기·스킬 픽셀 아이콘 (2026-11-13)

전투 HUD 왼쪽 아래 무기 원(큰 원)과 스킬 원(작은 원)에 들어가는 그림을 도트 HUD(체력판·물감 얼룩 팔레트)와 같은 픽셀 화풍으로 새로 그린 세트다. 가방 스킬 설명(`data/combat/skill_descriptions.json`)의 동작이 한눈에 보이게 그렸고, 사용자가 여러 차례 시안을 보고 확정했다(2026-11-13).

## 들어 있는 것

| 경로 | 내용 |
|---|---|
| `runtime/<스프라이트 이름>.png` | 게임용 54장. 1배 그림을 최근접 2배로 키운 것(무기 96×96, 스킬 88×88). |
| `source/native/<스프라이트 이름>.png` | 1배 원본 54장(무기 48×48, 스킬 44×44). HUD 캔버스 1화소 = 그림 1칸. |
| `source/tools/` | 그리는 코드. `pixicon.py`(도형·재기 검사·서브픽셀 렌더), `weapons.py`(무기 14종), `skills.py`(스킬 40종), `export.py`(내보내기). |
| `preview/weapons.png`, `preview/skills.png` | 무기·스킬 전체 판(HUD 원판 위). |
| `preview/hud-before-after.png` | 무기 원 + 기본 스킬 원을 실제 배치로 놓은 이전 그림·새 그림 비교. |

스프라이트 이름은 메인 `ApprovedCombatHUD/bindings.json`의 `icon`·`presentationIcon`과 같다.

- 무기 14종: `weapon-sword`, `weapon-rapier`, `weapon-whip`, `weapon-dagger`, `weapon-hammer`, `weapon-greatsword`, `weapon-shield`, `weapon-launcher`, `weapon-launcher-laser`, `weapon-launcher-shotgun`, `weapon-launcher-grenade`, `weapon-shuriken`, `weapon-bomb`, `weapon-unarmed`(맨손 주먹, 새 이름).
- 스킬 40종: `special-<무기>_<default|range|movement|follow_up>` 36장(검은 `base_sword`)과 발사기 `launcher-ball`·`launcher-laser`·`launcher-shotgun`·`launcher-grenade`.

## 그리는 규칙

- 재기(jaggies) 규칙: 직선은 가로·세로·1:1·1:2·1:3 기울기만 쓴다(`pixicon.LINT`가 다른 기울기의 변을 잡는다). 곡선은 기울기가 한쪽으로만 바뀐다(채찍 줄은 칸 단위 `stroke`).
- 원·띠·초승달은 진짜 원을 4×4 서브픽셀로 그리고, 가장 많은 색에 두 번째 색을 25·50·75%로만 섞는다(메인 `HudPixelKit.Supersample`과 같은 규칙). 띠는 반지름 두 개 사이라 어느 방향이든 굵기가 같다.
- 그 밖의 도형은 선명한 1배 칸 위에, 비어 있는 가장자리 칸만 덮인 비율대로 25·50·75% 반투명으로 채운다.
- 여백: 무기 그림은 링 안쪽(반지름 29)에서 2칸, 스킬 그림은 크림 선(반지름 24.5)에서 3칸 안쪽까지만 칠한다. 넘는 그림은 중심 기준으로 0.97배씩 줄여 맞춘다. 그림의 범위 상자를 원 가운데에 둔다(방패만 2칸 아래로).
- 색: 크림(무기·몸), 금색(베기·폭발 같은 힘), 청록(이동·순간이동), 회색 동그라미(적), 방패 십자만 빨강.

## 다시 뽑기

```sh
python source/tools/export.py <다른 출력 폴더>
```

Pillow·NumPy가 필요하다. 같은 코드로 `runtime/`·`source/native/`와 같은 바이트가 나온다. `preview/`의 판 그림은 저장소 밖 HUD 편집기 원판 부품으로 합성한 것이라 이 코드로 다시 만들지 않는다.

## 게임 연결

메인 `docs/assets/manifests/unity-deployment.json`이 `runtime/`을 `unity/Assets/Deviant/TemporaryGUI/Resources/ApprovedCombatHUD/sprites/`의 같은 이름으로 배치한다. HUD는 무기 그림을 48×48, 스킬 그림을 44×44 캔버스 화소에 정수 화소로 맞춰 그린다.
