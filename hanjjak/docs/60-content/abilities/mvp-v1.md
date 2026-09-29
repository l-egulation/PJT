# MVP 스킬 표시 콘텐츠 v1

---
doc_kind: content-data
owner_domain: skills-content
status: approved
approved_at: 2026-09-10
authority_level: applied
content_version: skill-display-mvp-v1
---

## 책임

스킬 6종과 등급별 스킬북의 플레이어 표시명, MVP 스킬 아이콘과 액티브 전투 VFX의 확정 시안을 소유한다. 해금, 성장, 드롭, 자동 사용과 전투 효과 수치는 [스킬 SSOT](../../30-domain/character/skills/ssot.md)가 소유한다.

## 스킬 표시명

| 역할 ID | 표시명 | 효과 설명 |
|---|---|---|
| `active_heavy` | 한짝의 일격 | 힘을 모아 한 번에 큰 피해를 준다. |
| `active_dot` | 마! 쫄이나 | 전투 장판을 5초 유지해 매초 현재 몬스터를 졸이듯 피해를 준다. |
| `active_haste` | 잘게 더 잘게! | 5초 동안 기본공격 속도를 높인다. |
| `active_basic_amp` | 화력 최대로! | 5초 동안 기본공격 피해를 높인다. |
| `passive_critical` | 회심의 간 | 치명타 확률을 높인다. |
| `passive_all_damage` | 오늘의 특선 | 모든 주는 피해를 높인다. |

## 스킬북 표시명 규칙

스킬북 표시명은 `<등급> <스킬명> 비법서` 형식이다. 드롭, 인벤토리, 거래소와 스킬 성장 action은 같은 품목 ID의 카탈로그 표시명을 사용한다.

| 등급 | 예시 |
|---|---|
| 노말 | 노말 한짝의 일격 비법서 |
| 희귀 | 희귀 마! 쫄이나 비법서 |
| 영웅 | 영웅 잘게 더 잘게! 비법서 |
| 전설 | 전설 화력 최대로! 비법서 |

모든 스킬은 네 등급의 전용 스킬북 품목 ID를 가진다. 품목 ID는 표시명과 분리한 안정 문자열 키로 관리한다.

인벤토리 런타임 아이콘은 공용 비법서 이미지 위에 같은 `skillId`의 확정 스킬 아이콘을 작은 배지로 합성한다. 등급 차이는 이름 안의 접두사만으로 반복하지 않고 노말·희귀·영웅·전설 등급 배지를 함께 사용한다. 이 표현은 품목 ID와 카탈로그 표시명 규칙을 바꾸지 않는다.

## 스킬 아이콘 확정 시안

2026-09-09 사용자 선택 `3·3·3·1·1·1`을 스킬 표시 순서에 적용한다. 선택된 시각 방향은 확정이며, 원본 생성 후보와 비교 설명은 [정체성·효과 기반 초안](../../90-reference/art/skill-icons/2026-09-09-identity-effect-drafts/README.md)에 보존한다.

| 역할 ID | 표시명 | 선택안 | 확정 시안 |
|---|---|---:|---|
| `active_heavy` | 한짝의 일격 | C안(3) | [active-heavy.png](./icons/mvp-v1/active-heavy.png) |
| `active_dot` | 마! 쫄이나 | C안(3) | [active-dot.png](./icons/mvp-v1/active-dot.png) |
| `active_haste` | 잘게 더 잘게! | C안(3) | [active-haste.png](./icons/mvp-v1/active-haste.png) |
| `active_basic_amp` | 화력 최대로! | A안(1) | [active-basic-amp.png](./icons/mvp-v1/active-basic-amp.png) |
| `passive_critical` | 회심의 간 | A안(1) | [passive-critical.png](./icons/mvp-v1/passive-critical.png) |
| `passive_all_damage` | 오늘의 특선 | A안(1) | [passive-all-damage.png](./icons/mvp-v1/passive-all-damage.png) |

확정 PNG는 1254×1254 시각 원본이다. 런타임용 실제 투명 배경, 목표 픽셀 크기 리사이즈, 압축과 UI 연결은 이 선택에 포함하지 않으며 구현 작업에서 별도 검증한다.

## 액티브 전투 VFX 확정 시안

2026-09-10 사용자가 아래 네 미리보기를 액티브 스킬별 최종 시각 방향으로 확정했다. 각 미리보기는 실제 고유 프레임 8장을 반복 재생하며, 프레임 캔버스·중심·외곽 여백을 통일한다. `active_dot`에는 이전 눈 모양 표식을 사용하지 않는다.

| 역할 ID | 표시명 | 확정 연출 | 8프레임 시트 | 확정 미리보기 |
|---|---|---|---|---|
| `active_heavy` | 한짝의 일격 | 황금 고리와 교차 섬광이 한 번에 터지는 단발 일격 | [active-heavy-8f-concept-v2.png](../../../outputs/skill-vfx-concepts-v2/active-heavy-8f-concept-v2.png) | [active-heavy-preview-fixed-v3.gif](../../../outputs/skill-vfx-concepts-v2/active-heavy-preview-fixed-v3.gif) |
| `active_dot` | 마! 쫄이나 | 붉은 소스가 고리로 맺혀 끓고 응축되는 지속 피해 표식 | [active-dot-joligi-quality-8f-v7.png](../../../outputs/skill-vfx-concepts-v2/active-dot-joligi-quality-8f-v7.png) | [active-dot-joligi-quality-preview-v7.gif](../../../outputs/skill-vfx-concepts-v2/active-dot-joligi-quality-preview-v7.gif) |
| `active_haste` | 잘게 더 잘게! | 청록색 상승 화살표가 반복되는 공격속도 버프 | [haste-caster-8f.png](../../../apps/web/public/assets/effects/skills-v1/haste-caster-8f.png) | [haste-caster-8f-preview.gif](../../../apps/web/public/assets/effects/skills-v1/previews/haste-caster-8f-preview.gif) |
| `active_basic_amp` | 화력 최대로! | 가스레인지 화구와 푸른 불꽃이 강해지는 기본공격 강화 버프 | [active-basic-amp-8f-concept-v2.png](../../../outputs/skill-vfx-concepts-v2/active-basic-amp-8f-concept-v2.png) | [active-basic-amp-preview-fixed-v3.gif](../../../outputs/skill-vfx-concepts-v2/active-basic-amp-preview-fixed-v3.gif) |

이 표는 시각 시안 확정을 소유한다. 현재 런타임 에셋 교체·재생 타이밍·전투 화면 연결과 검증 상태는 [H-05 액티브·버프·패시브 전투 검증](../../wiki/06-delivery/tasks/H-skills/h-05-skill-combat-validation.md)에서 관리한다.
