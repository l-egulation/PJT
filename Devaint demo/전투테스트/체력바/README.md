# 체력바 회복·보호막·피해 연출 시험판

전투 HUD 체력바에서 회복·보호막 생성·피해를 보여 주는 [pixelfx](https://github.com/patch-gg/pixelfx)(MIT) 이펙트 후보다. 사용자가 회복 A·보호막 B·피해 A를 붓·지우개 그림 없이 쓰기로 확정했다(2026-11-12). 게임 연결은 `HealthBarInk`가 같은 규칙을 런타임으로 다시 그린다([채택 전투 HUD 연결](../../docs/development/presentation/ui/combat-hud-approved.md)). 이 폴더의 시트는 비교용이며 Unity에 배치하지 않는다.

방향은 회복·보호막은 그리고 칠하는 느낌, 피해는 지워지고 휘발되는 느낌이다.

| 파일 | 내용 |
|---|---|
| `_bar.js` | 승인 HUD 체력판(검은 판·크림 선·평행사변형 빨간 게이지)을 게임 화소로 그리는 공용 코드 |
| `heal-a.js` · `heal-b.js` · `heal-c.js` | 회복 46→72: 붓질 채움 · 수채 번짐 · 색연필 해칭 |
| `_shield.js`, `shield-a.js` · `shield-b.js` | 보호막 12(체력 72 뒤): 펜 윤곽+빗금 · 틀 덧그리기+붓칠. 생김 → 5 막음 → 깨짐 |
| `_hit.js`, `damage-a.js` · `damage-b.js` · `damage-c.js` | 피해 72→50: 지우개 · 휘발 · 바람에 흩날림 |

## 렌더링

파일을 pixelfx 체크아웃의 `src/fx/hud/`에 복사한 뒤 렌더링한다.

```sh
node tools/render.mjs "hud/*" --zoom 3
node tools/render.mjs hud/heal-a --sheet --bg none --out <Deviant-main>/outputs/pixelfx/hud-bar/heal-a
```

## 시트 규격

- 276×60, 30fps, 원점 (16, 34). 게임 화면 960×540 기준 실제 화소 크기로, 게이지는 200×9px이다.
- 판·초상·숫자는 비교용으로 함께 그렸다. 게임에 넣을 때는 게이지 위에 겹치는 층만 따로 굽고 HUD 배율에 맞춰야 한다(미정).
- 보호막 민트는 캐릭터 보호막 캡슐(#5fe8c0) 계열, 빨강은 승인 HUD 게이지(#ff6b5e) 기준이다.

## 확인한 범위

시안은 pixelfx 렌더링과 `tools/check.mjs`로 확인했다. 보호막 A와 피해 시안 일부는 날아가는 파편이 판 밖으로 나가 잘린다는 경고가 남아 있다. Unity·실제 전투 화면·연속 회복·연속 피해는 확인하지 않았다.
