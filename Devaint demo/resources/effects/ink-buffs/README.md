# 잉크 버프·상태이상 픽셀 FX (2026-11-12 확정)

런 잉크(노랑·초록·보라·빨강·파랑)가 발동할 때 화면에 보여야 하는 효과를 픽셀 FX(1배 32px = 1u, 디더 반투명)로 그렸다. 효과 종류별 후보 중 사용자가 고른 안을 게임용 시트(`runtime/`)로 내보낸다. 게임은 `runtime/`만 쓴다.

## 확정 선택

| 효과 | 선택 | 게임 시트 |
|---|---|---|
| 이동속도 증가 | A. 바람 줄 | `speed-wind` |
| 방어막(일반) | A. 잉크 거품막 | `shield-front`(유예·감소), `shield-gain`, `shield-hit` |
| 대쉬 방어막 | A. 돌진 초승달 | `dash-shield`, `dash-shield-break` |
| 체력 회복 | A1. 밝은 초록 · 큰 십자(A를 더 밝은 초록·큰 십자로 수정) | `heal-crosses` |
| 기본 공격력 증가(빨강) | 스킬 피해 증가 C와 같은 모양을 빨간색으로(C형. 붉은 불꽃 어깨) | `red-flames-back`, `red-flames-front` |
| 스킬 쿨타임 감소(파랑) | A. 되감기는 시계 | `cdr-clock` |
| 스킬 피해 증가(파랑) | C. 푸른 불꽃 어깨 | `blue-flames-back`, `blue-flames-front` |
| 기절 | A. 별 고리 — `effects/ink-status`의 확정 시트 `stun-stars`와 같은 계열이라 그 시트를 그대로 쓴다 | (없음) |
| 속박 | B. 잉크 사슬 + 자물쇠 | `chain-front`, `chain-back`, `chain-in`, `chain-snap` |
| 슬로우 | D. 머리 위 달팽이 | `slow-snail` |
| 중독 | 게임에서 빼기로 해 제외(사용자 결정) | (없음) |
| 상태 조건 강화 타격 표시 | 넣지 않음 | (없음) |

미리보기 HTML에는 반려한 후보도 비교용으로 남아 있고, 확정안은 테두리와 `✓확정`으로 표시한다. 수치(지속시간·강화 수치)는 메인 `docs/design/equipment/inks/effects.md`의 플레이 테스트 시작값을 재생 시간에만 썼으며 이 자료가 게임 규칙을 정하지 않는다.

## 시트 형식

`runtime/sheets.json`에 시트마다 셀 크기·프레임 수·변형(행)·피벗·프레임 시간·반복 여부·용도를 적는다.

- 열 = 프레임, 행 = 변형(`base`/`enhanced` 강화 수치, `steady`/`decaying`, 몸 폭 `small`/`large` 등).
- 피벗은 셀 왼쪽 위 기준 px. `anchor: feet`는 발밑 가운데, `head`는 머리 꼭대기 가운데다.
- `facing: true`는 오른쪽을 보는 그림이라 왼쪽을 볼 때 좌우를 뒤집는다.
- 플레이어 시트는 공통 캐릭터 대기 첫 프레임(`characters/ink/common/runtime/forest-character/idle/01.png`)의 키·폭·윤곽을 기준으로 그렸다. 사슬은 몸 폭 2u 미만(`small`, 1.2u×2.1u 기준)과 이상(`large`, 2.5u×3.1u 기준) 두 크기다.

## 파일

| 경로 | 내용 |
|---|---|
| `runtime/*.png`, `runtime/sheets.json` | 게임용 시트 17장과 시트 정보. |
| `preview/ink_buff_fx_candidates.html` | 후보 비교 미리보기(그림 내장). 기본/강화 수치, 배율, 디더/알파, 배경, 재생 속도를 바꾸고 카드로 후보를 고른다. |
| `source/preview_template.html` | 효과 코드 원본. 미리보기와 시트 내보내기가 같은 코드를 쓴다. |
| `source/tools/build_preview.py` | 원본에 캐릭터·빗자루 추적자 대기 PNG를 재인코딩 없이 넣어 미리보기를 쓴다. |
| `source/tools/export_runtime.mjs` | 원본의 효과 코드를 Node에서 실행해 확정안을 `runtime/`으로 내보낸다. 결정적 난수만 써서 다시 내면 바이트 그대로 같은 시트가 나온다. |

## 다시 만들기

리소스 저장소 루트에서:

```sh
python effects/ink-buffs/source/tools/build_preview.py
node effects/ink-buffs/source/tools/export_runtime.mjs
```
