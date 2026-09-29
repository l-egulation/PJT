# MVP 아이템·장비 표시 콘텐츠 v1

---
doc_kind: content-data
owner_domain: items-content
status: approved
approved_at: 2026-09-02
authority_level: working
content_version: item-display-mvp-v1
---

## 책임

주력 재료 15종의 플레이어 표시명·설명 문구와 장비 6부위×4등급의 플레이어 표시명을 소유한다. 재료 드롭 기회·수량·세대 가중치는 [MVP 주력 재료 드롭표 v1](./material-drops-mvp-v1.md), 주력 선택과 80:10:10 계열 비율은 [전문 SSOT](../../30-domain/character/ssot.md), 장비 제작·강화 규칙은 [장비 SSOT](../../30-domain/items/equipment/ssot.md)가 소유한다.

## 주력 전문 표시명

| 내부 계열 | 표시명 | 대표 재료 |
|---|---|---|
| A | 감자 전문 | 감자 |
| B | 고구마 전문 | 고구마 |
| C | 옥수수 전문 | 옥수수 |

## 재료 표시명

| itemId | 등급 | 표시명 | 이미지 |
|---|---:|---|---|
| `POTATO_M1` | F | 감자 한 조각 | `potato-fragment-f.png` |
| `POTATO_M2` | D | 미니 감자 | `potato-mini-d.png` |
| `POTATO_M3` | C | 감자 | `potato-c.png` |
| `POTATO_M4` | B | 황금 감자 | `potato-golden-b.png` |
| `POTATO_M5` | A | 전설 감자 | `potato-legendary-a.png` |
| `SWEET_POTATO_M1` | F | 고구마 한 조각 | `sweet-potato-fragment-f.png` |
| `SWEET_POTATO_M2` | D | 미니 고구마 | `sweet-potato-mini-d.png` |
| `SWEET_POTATO_M3` | C | 고구마 | `sweet-potato-c.png` |
| `SWEET_POTATO_M4` | B | 황금 고구마 | `sweet-potato-golden-b.png` |
| `SWEET_POTATO_M5` | A | 전설 고구마 | `sweet-potato-legendary-a.png` |
| `CORN_M1` | F | 옥수수 한 알 | `corn-kernel-f.png` |
| `CORN_M2` | D | 미니 옥수수 | `corn-mini-d.png` |
| `CORN_M3` | C | 옥수수 | `corn-c.png` |
| `CORN_M4` | B | 황금 옥수수 | `corn-golden-b.png` |
| `CORN_M5` | A | 전설 옥수수 | `corn-legendary-a.png` |

이미지는 `apps/web/src/features/equipment/assets/material-ranks`의 `manifest.json`과 같은 파일명을 사용한다. 내부 M1~M5 식별자는 F·D·C·B·A 표시 등급에 각각 대응하며 드롭 풀은 [아이템 SSOT](../../30-domain/items/ssot.md) 및 [MVP 주력 재료 드롭표 v1](./material-drops-mvp-v1.md)을 따른다.

인벤토리 재료 설명은 `장비 제작과 강화에 사용하는 {세대}세대 재료` 형식을 사용한다. 표시명을 반복하지 않으며 문장을 `~입니다`가 아닌 명사형 `재료`로 끝낸다.

## 장비 표시명

| 장비 슬롯 | 등급 | 표시명 |
|---|---|---|
| 무기 | 노말 | 무딘 식칼 |
| 무기 | 희귀 | 잘 벼린 식칼 |
| 무기 | 영웅 | 명인의 식칼 |
| 무기 | 전설 | 전설의 식칼 |
| 장갑 | 노말 | 낡은 조리장갑 |
| 장갑 | 희귀 | 튼튼한 조리장갑 |
| 장갑 | 영웅 | 명인의 조리장갑 |
| 장갑 | 전설 | 전설의 조리장갑 |
| 갑옷 | 노말 | 해진 앞치마 |
| 갑옷 | 희귀 | 질긴 앞치마 |
| 갑옷 | 영웅 | 명인의 앞치마 |
| 갑옷 | 전설 | 전설의 앞치마 |
| 투구 | 노말 | 구겨진 조리모 |
| 투구 | 희귀 | 빳빳한 조리모 |
| 투구 | 영웅 | 명인의 조리모 |
| 투구 | 전설 | 전설의 조리모 |
| 망토 | 노말 | 빛바랜 식탁보 망토 |
| 망토 | 희귀 | 윤나는 식탁보 망토 |
| 망토 | 영웅 | 명인의 식탁보 망토 |
| 망토 | 전설 | 전설의 식탁보 망토 |
| 신발 | 노말 | 미끄러운 주방화 |
| 신발 | 희귀 | 든든한 주방화 |
| 신발 | 영웅 | 명인의 주방화 |
| 신발 | 전설 | 전설의 주방화 |

전투 장비 표시명은 장비 인스턴스의 외형 착용 규칙을 만들지 않는다. 치장 외형과 전투 장비 능력치는 [치장 SSOT](../../30-domain/cosmetics/ssot.md)와 [장비 SSOT](../../30-domain/items/equipment/ssot.md)에 따라 분리한다.
