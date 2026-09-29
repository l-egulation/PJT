# 악마 세트 반입 패키지 요청

악마(`cosmetic-set-05`)만 게임에 못 들어가 있습니다. 그림은 다 그려져 있고 **포장 형식만**
다릅니다. 천사·약과와 같은 형식으로 한 번 더 내보내 주시면 그대로 붙습니다.

## 왜 지금 것으로는 안 되나

`equip/악마/runtime-manifest.json` 에는 겹침 순서가 `frameDrawOrderOverrides` 두 개
(`strike1_01`, `strike1_02`)뿐입니다. 나머지 18프레임의 순서가 없습니다.

같은 폴더의 `full-set-manifest.json` 은 전신 한 장짜리라 스스로 이렇게 적어 두셨습니다.

> This full-set skin is a precomposed character image and **does not support per-slot mixing**.

게임은 부위를 섞어 입을 수 있어야 합니다(악마 뿔 + 요리사 상의 같은 조합). 그래서
**프레임마다 겹침 순서가 있는 부위별 패키지**가 필요합니다. 나머지 18프레임 순서를 저희가
추측해서 채울 수는 있지만, 그건 아트 의도를 짐작하는 것이라 요청드립니다.

## 본보기

이미 넘겨주신 **천사**와 **약과**가 정확히 그 형식입니다.

- `equip/천사/runtime-manifest.json` + `equip/천사/layers/atlases/`
- `equip/약과/runtime-manifest.json` + `equip/약과/layers/atlases/`

악마도 같은 구조로 내보내 주시면 됩니다. 같은 도구를 쓰신 것 같으니 세트 이름만 바꿔
한 번 더 돌리시면 될 것 같습니다.

## 필요한 것

### 1. 부위별 아틀라스 (`layers/atlases/*-atlas-4x5.png`)

- 4열 × 5행, 한 칸 **928 × 672** — 열이 프레임 1~4, 행이 모션
- 모션 순서 `rest, run2, strike1, thrust1, death1`
- 리사이즈·크롭 없이 지금 `layers/frames/<부위>/` 에 있는 그림을 격자로 붙이기만 하면 됩니다

### 2. `runtime-manifest.json`

`schemaVersion: "hanjjak.hero-equipment-atlas/v1"` 로, 천사 것과 같은 키 구성입니다.
그중 **`drawOrderByFrame` 20개**가 핵심입니다.

```json
"drawOrderByFrame": {
  "rest_01": ["slot.cape", "base.body", "slot.clothing", "base.arms", "..."],
  "rest_02": ["..."]
}
```

토큰은 두 가지뿐입니다.

| 토큰 | 뜻 |
| --- | --- |
| `base.<부위>` | 마네킹 레이어 (`base.body`, `base.arms`, `base.hands`, `base.feet-front`) |
| `slot.<슬롯>` | 장비 한 장 |
| `slot.<슬롯>.<하위>` | 좌우로 나뉜 장비 (`slot.glove.screen-left` 처럼) |

슬롯 이름은 `hat`, `clothing`, `cape`, `glove`, `shoe`, `weapon` 여섯 개입니다.
지금 악마 레이어 이름과 이렇게 짝지으면 됩니다.

| 지금 레이어 이름 | 토큰 |
| --- | --- |
| `head-red-devil-horns` | `slot.hat` |
| `devil-outfit` | `slot.clothing` |
| `cape-devil-wings-back` | `slot.cape` |
| `wrist-ring-screen-left` / `-right` | `slot.glove.screen-left` / `-right` |
| `ankle-ring-screen-left` / `-right` | `slot.shoe.screen-left` / `-right` |
| `weapon-01-red-trident` 외 2종 | `slot.weapon` (아이템 3개, 아틀라스 3장) |
| `original-body-face-feet` | `base.body` (+ 필요하면 `base.feet-front`) |
| `original-arm-hand-combined` | `base.arms` (+ 필요하면 `base.hands`) |

이미 적어 두신 `strike1_01` 겹침 순서를 이 토큰으로 옮기면 이렇게 됩니다.

```json
"strike1_01": [
  "slot.cape", "slot.weapon", "base.body", "slot.clothing",
  "slot.shoe.screen-right", "slot.shoe.screen-left", "base.arms",
  "slot.glove.screen-right", "slot.glove.screen-left", "slot.hat"
]
```

`strike1_01`·`strike1_02` 는 무기가 몸 뒤로 가고, 나머지 18프레임은 앞으로 오는 것으로
읽었습니다. 맞는지 확인해 주시고 18프레임 순서를 채워 주세요.

### 3. `items` 와 `defaultLoadout`

천사와 같은 모양입니다. 무기가 3종이니 `items` 에 세 개를 두고 `defaultLoadout` 에서
하나를 고르시면 됩니다 (지금 `runtime-manifest.json` 의 `defaultWeaponSlot` 은
`weapon-trident` 입니다).

## 확인 방법

넘겨주시면 저희가 이렇게 검증합니다.

- 좌표계가 928 × 672 고정인지 (다르면 로더가 거부합니다)
- 20프레임 모두 `drawOrderByFrame` 이 있는지
- 세트 한 벌을 입힌 결과가 이미 승인된 합본과 픽셀 단위로 맞는지

## 급하지 않은 경우

당장 어려우시면 **전신 스킨으로만** 넣는 선택지도 있습니다. 악마 6부위를 다 입었을 때만
보이고 섞어 입으면 안 나옵니다. 다만 다른 세트와 동작이 달라져서, 부위별 패키지 쪽을
권합니다.
