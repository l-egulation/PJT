# 추가 필요 에셋의 적용안

[분야 개요](README.md)

## 범위와 현재 상태

> **2026-10-31 적용(사용자 지시, 선택은 추천안):** 아래 [적용한 선택](needed-assets-application.md#적용한-선택)의 105장을 배치 계약에 넣고 게임에 연결했다. 선택하지 않은 46장(제단 24px 호환본, 출구 닫힘, 종이 포함 도면 카드, F 외 키보드 키캡 20장, 게임패드 문양 20장)은 리소스에만 있다. 최종 미술 승인은 여전히 별도다. 이 아래의 "변경하지 않았다"는 제작 시점의 설명이다.

[임시 표현과 필요한 에셋](temporary-and-needed-assets.md)에 대응하는 **작업본**이다. 이름별 PNG와 출처·규격·피벗·프레임 순서를 리소스에 저장했으며, 이 문서는 이후 적용 담당자의 작업 방법을 설명한다. **게임 코드·씬·프리팹·Unity 배치 계약은 변경하지 않았고, 새 그림을 게임에 적용하지 않았다.**

- 원화는 실제 마을 집의 컬러/연필 짝, 현재 잉크 주인공, 시계탑 보스방의 PNG 바이트를 참조해 제작했다.
- 새 PNG는 **151개**다. 같은 파일명의 다른 버전이나 기존 승인 아트를 덮어쓰지 않았다.
- 외형·출구 표현·새 연출 시간은 사용자 승인 전 후보다. 인물 정지 그림과 희생용 마스크를 인물 걷기·사망 모션 전체로 취급하지 않는다.
- 원본·프롬프트·미리보기는 `resources/`가 소유한다. 실제 적용을 선택하는 계약, Unity 목적지·`.meta`·GUID와 호출부는 메인이 소유한다.
- 목록의 임시 표현은 **승인과 실제 적용이 끝나기 전까지 미해결로 유지**한다. 파일 생성만으로 원래 목록에서 지우지 않는다.

규격 원본은 [output-layout.json](../../../resources/authoring/needed-assets/source/output-layout.json), 생성 출처는 [provenance.json](../../../resources/authoring/needed-assets/source/provenance.json), 원화·프레임 검토는 [에셋 갤러리](../../../resources/authoring/needed-assets/preview/index.html)를 따른다.

## 이름별 파일 목록

아래 경로의 기준은 `resources/`다. 번호가 붙은 프레임은 한 폴더에서 이름 오름차순으로 읽는다.

| 목록의 대상 | 파일·수량 | 규격과 의미 |
|---|---|---|
| 보스 색상 잉크 제단 | `environments/clock-tower/runtime/needed-art/ink-altar/altar-before.png`, `altar-after.png` | 48×40, PPU32, 바닥 중앙. 사용 전 빨강·노랑·초록·파랑·보라 5보석, 사용 후 보석이 어두워짐 |
| 제단 호환 크기 | 같은 폴더 `compat24/altar-before.png`, `altar-after.png` | 24×20, PPU16. 위 48px 본과 **택일**하며 함께 적용하지 않음 |
| 제단 등장·사용 | 같은 폴더 `appear/altar-appear-01.png`~`08.png`, `use/altar-use-01.png`~`08.png` | 각 8프레임, 48×40. 등장 첫 장은 투명·마지막은 사용 전, 사용 마지막은 사용 후 상태 |
| 키보드 키캡 | `ui/keycaps/runtime/keyboard/keycap-*.png` 21개 | F/E/Q/W/R/A/S/D/I/M/C/V, Space/Shift/Esc/Enter/Tab, 방향키. 높이 18px, 단일 키 17px 폭, 긴 표기는 더 넓음 |
| 게임패드 문양 | `ui/keycaps/runtime/gamepad/keycap-*.png` 20개 | A/B/X/Y, LB/RB/LT/RT, L3/R3, Menu/View, D-pad 4방향, cross/circle/square/triangle |
| 챕터 출구 | `environments/clock-tower/runtime/needed-art/chapter1-exit/exit-open.png`, `exit-sealed.png`, `open/exit-open-01.png`~`08.png` | 96×128, PPU32. 전용 아치 2상태와 열림 8장. 닫힘 상태의 사용 여부는 출구 연출 설계 후 선택 |
| 검·둔기·원거리 큰 그림 | `ui/blueprints/runtime/needed-art/illustrations/blueprint-{sword,blunt,ranged}-art.png` | 96×128, 투명 배경 3개. 기존 선택 버튼 프레임과 함께 쓰는 기본안 |
| 종이 포함 도면 카드 | `ui/blueprints/runtime/needed-art/blueprint-{sword,blunt,ranged}.png` | 128×160 3개. 종이 카드까지 한 장인 다른 배치용. 큰 그림과 중복 배치하지 않음 |
| 건물 영역별 채색 | `environments/village/runtime/effects/needed-art/color-spread/{palette-atelier,supply-office,reclaim-workshop}/color-spread-01.png`~`08.png` | 256×256, 건물별 8장·총 24개. 기존 컬러 건물의 실제 픽셀·알파에 클립한 오버레이 |
| 가방 잉크 파괴 | `effects/ink-destruction/runtime/needed-art/ink-destruction-01.png`~`08.png` | 64×64, 중심 피벗. 마지막은 투명 |
| 제단 후보 획득·폐기 | `environments/clock-tower/runtime/needed-art/ink-altar/choice-acquire/altar-choice-acquire-01.png`~`08.png`, `choice-discard/altar-choice-discard-01.png`~`08.png` | 각 8장, 64×64, 중심 피벗. 획득은 상승, 폐기는 하강·소멸 방향 |
| 마법안료 획득 대비 보강 | `effects/hub-unlock/runtime/pigment-pickup-contrast/pigment-pickup-contrast-01.png`~`08.png` | 64×64 8장. 짙은 잉크 외곽과 청록/올리브 계열로 종이 배경 대비 보강. 기존 8장을 덮어쓰지 않음 |
| 인간 시기 주인공 | `characters/story/human-clerk/runtime/human-clerk.png` | 192×160, PPU32, 실제 인물 높이 약 64px. 크림 후드·청록 스카프·인간 피부/눈 |
| 은인 | `characters/story/benefactor/runtime/benefactor.png`, `benefactor-erased.png` | 192×160 2상태, 같은 자세·알파. 외형 검토 후보이며 상점 NPC가 아님 |
| 조력자 | `characters/story/ink-helper/runtime/ink-helper.png`, `sacrifice/ink-helper-sacrifice-01.png`~`08.png` | 192×192, PPU32. 큰 잉크 존재 1장과 같은 자세의 소실 마스크 8장. 보행·리깅 모션 아님 |
| 누락 검 접촉광의 대체 후보 | `effects/basic-sword-contact/runtime/needed-art-candidate/00.png`~`05.png` | 64×64 6장. **기존 승인 6장의 원본 복원이 아닌 새 그림 후보** |

## 적용한 선택

| 대상 | 선택 | Unity 목적지(`unity/Assets/`) | 연결 |
|---|---|---|---|
| 보스 제단 | 48px 사용 전/후, 등장 8·사용 8, 후보 획득 8·폐기 8 | `ForestCombatV2/Resources/Chapter1Art/InkAltar/` | `Chapter1InkAltar`: 등장은 보스 처치로 처음 나타날 때, 사용은 획득·폐기 확정 뒤 1회. 폐기는 제단 위에 후보 수만큼 효과. 획득 효과는 제단 화면 카드 위 |
| 키캡 | 키보드 `keycap-f` 1장 | `ForestCombatV2/Resources/InputGlyphs/Keyboard/` | `KeycapMarker.SpriteFor`. 코드 그림 생성부 삭제, 1초·4px 움직임은 코드 유지 |
| 1챕터 출구 | `exit-open`, 열림 8장 | `ForestCombatV2/Resources/Chapter1Art/Exit/` | `Chapter1BossEncounter`: 빌린 포탈 프레임 대신 전용 아치. 출구가 열릴 때 열림 8장 뒤 `exit-open` |
| 출발 도면 | 96×128 큰 그림 3장 | `Deviant/Hub/Resources/Hub/blueprints/` | 출발 준비대 카드(패널 744×480, 카드 높이 176, 그림 네이티브 크기). HUD 무기 아이콘은 그대로 |
| 건물 채색 | 건물별 8장×3 | `Deviant/Hub/Resources/Hub/effects/color-spread/<건물>/` | `StartingVillageWorld`: 플레이 중 새로 칠해질 건물에 화면이 닫힌 뒤 1회 재생, 끝나면 영역을 컬러로 확정 |
| 가방 파괴 | 8장 | `Deviant/TemporaryGUI/Resources/TemporaryGUI/Effects/ink-destruction/` | 가방 '파괴' 두 번째 누름 성공 시 그 칸 위 |
| 안료 획득 | 대비 보강 8장(현재 미사용) | `Deviant/Hub/Resources/Hub/effects/pigment-pickup-contrast/` | 파일은 보존한다. 현재 `Chapter1PigmentPickupEffect`는 [바닥 팔레트 아이콘·실제 지급량 표시](../../development/gameplay/equipment/inventory-and-loot.md#데이터에셋-교체)를 사용한다 |
| 이야기 인물 | 인간 행정원, 은인·지워진 은인, 조력자·소실 8장 | `Deviant/Hub/Resources/Hub/story/characters/` | `data/story/starting_village_opening.json`의 줄별 `cast`(위치·2배·좌우 반전, 소실은 1회 재생 후 마지막 장 유지) |
| 검 접촉광 | 대체 후보 6장 | `ForestCombatV2/Resources/BasicSwordHit/00~05.png` | 기존 `.meta`·GUID·재생 시간 유지. 원본이 복구되면 원본 우선 |
| 효과음 | 기존 WAV 5종(8사건 중 7사건) | `ForestCombatV2/Resources/PresentationSfx.asset`(GUID 참조, 복제 없음) | 해금·안료·제단 등장/획득/폐기·가방 파괴·출구 열림 각 1회. 키캡 표시음은 연결하지 않음 |

스프라이트 `.meta`는 출력 규격의 PPU·피벗으로 만들었다(Point·무압축·mipmap 없음·FullRect·Clamp). `HubArtImporter`는 허브 색인에 없는 Hub 그림의 `.meta` 피벗·PPU를 덮어쓰지 않는다.

## 공통 임포트·선택 순서

1. 갤러리에서 종이/어두운 배경과 네이티브 확대를 비교해 적용할 후보를 확정한다. 최종 미술 승인을 별도 기록한다.
2. 해당 리소스 파일의 에셋 ID·경로·SHA256·크기를 확인한다. 단순히 최신 파일명이나 같은 이름이라는 이유로 선택하지 않는다.
3. 적용 담당자가 `docs/assets/manifests/unity-deployment.json`에 **선택한 파일만** 추가한다. 이번 작업은 이 계약을 수정하지 않았다.
4. Unity 목적지 `.meta`·GUID·임포트 설정은 메인에서 준비한다. 기존 GUID를 재생성하거나 `.meta` 유실을 전체 초기화로 해결하지 않는다.
5. Unity를 닫은 상태에서 고정 리소스 실행기의 `--check`를 먼저 실행한 뒤 승인된 계약으로 배치한다. 별도 경로 선택·공유 드라이브·수동 복사를 공용 진입점으로 되살리지 않는다.
6. Unity **6000.6.5f1**에서 연결·실행·화면 검수를 수행한다. 파일/해시 검사와 실제 게임 검증을 구분한다.

스프라이트는 기본적으로 Point, 무압축, mipmap 없음, FullRect, Wrap Clamp다. UI 그림은 `Image.Type.Simple`·`preserveAspect`를 사용하고 종이·인물·컬러 효과를 어두운 텍스트색으로 통째로 tint하지 않는다. 전체 투명 캔버스를 다시 자르지 않는다. 피벗은 항목별 메타데이터를 읽는다.

새 8프레임 연출의 **80ms/장**은 검토용 표현 기본값이다. 이를 상호작용 대기시간·보상 확정 시점·전투 시간표로 자동 채택하지 않는다. 검 후보는 아래의 기존 재생 시간과 구분한다.

## 보스 제단 적용안

수정 위치는 `unity/Assets/ForestCombatV2/Runtime/Chapter1InkAltar.cs`의 `Pedestal()`과 표시 상태 선택 부분이다.

- 48×40 본은 PPU32·pivot `(0.5,0)`으로, 24×20 호환 본은 PPU16·같은 피벗으로 가져온다. 둘의 스프라이트 전체 월드 크기는 1.5×1.25로 같다. 48px 본에 기존 PPU16을 그대로 주어 두 배 커지게 하지 않는다.
- `Pedestal(!Finished)`에 대응해 사용 전/후 그림을 선택한다. 색 선택만 했을 때 사용 후로 바꾸지 않는다. 현재 `Finished`는 `Claimed != null || Discarded`다.
- 등장은 보스 처치 후 기존 `Available` 조건이 성립한 시점에 표현한다. 그림 연출을 이유로 제단 위치·접근 거리·후보 생성·획득 규칙을 바꾸지 않는다.
- 사용 연출은 획득 또는 폐기가 실제로 확정된 뒤 한 번 재생한다. 끝에는 정적인 사용 후 그림을 유지한다. 반복 Draw/LateUpdate마다 연출을 재시작하지 않는다.
- 키캡은 실제 불투명 윗선을 기준으로 두는 것이 안전하다. 그림 캔버스의 여백을 키 위치로 오인하지 말고 `KeycapMarker.OpaqueTop()`과 기존 간격을 이용한다.

제안 목적지는 `Resources/Chapter1Art/InkAltar/`다. 이는 이후 계약 작성 시 사용할 경로안이며 현재 게임에 폴더를 배치한 상태가 아니다.

## 공용 키캡 적용안

수정 위치는 `KeycapMarker.SpriteFor()`의 그림 공급 부분이다. 기존 F 생성 그림을 승인된 F PNG로 교체하되 누락 이미지 처리와 기존 호출부를 먼저 확인한 뒤 코드 생성부를 정리한다.

- 단일 키 F의 17×18, PPU32, pivot `(0.5,0)`, 월드 scale 2를 유지한다. 기존 **1초 주기·4 월드 픽셀** bob은 코드에 남긴다. PNG 프레임에 같은 이동을 굽지 않아 두 번 움직이지 않게 한다.
- 긴 키 이름은 파일에 기록된 폭을 사용한다. 단일 키 크기로 강제 압축해 글자를 뭉개지 않는다.
- 기존 `char F` 호출은 호환 경로를 유지한다. 여러 글자와 게임패드는 플랫폼/문양 ID 조회를 추가해야 하며 캐시 키에 플랫폼을 포함한다.
- 게임패드 그림의 존재는 게임패드 입력 매핑 구현을 뜻하지 않는다. 실제 바인딩에서 표시 문양을 선택하며 이 에셋을 이유로 액션 키를 임의 변경하지 않는다.
- 사용 가능 대상 변경·일시정지·화면 열림 등의 기존 가시성 조건을 유지한다. 표시음은 선택 사항이며 매 프레임/매 bob에 재생하지 않는다.

제안 목적지는 `Resources/InputGlyphs/Keyboard/`, `Gamepad/`다. 제단·출구·마을 작업대가 같은 공급자를 공유하도록 한다.

## 1챕터 출구 적용안

수정 위치는 `Chapter1BossEncounter.ResolvePortal()`의 `ApprovedTraversal.portalDraw` 마지막 프레임을 빌려 오는 부분이다. **출구의 그림 공급만** 전용 출구로 분리하고 플레이어 C/V 귀환 연출 원본은 유지한다.

96×128 본은 PPU32, pivot `(0.5,0.0625)`다. `ExitPosition`과 바닥 탐색을 유지한다. 키캡 높이는 `OpaqueTop()`에 맞춘다. 기존 `ExitOpen` 가시성·상호작용·클리어 판정은 바꾸지 않는다. `exit-sealed`와 열림 표현은 설계 검토 후 선택하며, 새 문 잠금·방 이동 능력·시간 지연을 자의적으로 추가하지 않는다.

열림 8장의 끝은 `exit-open`이다. 재생이 취소되거나 화면을 다시 구성해도 실제 보스/출구 상태에 맞는 정적 그림을 표시한다. 임시 `exitIsPortal` 가드의 의미를 확인해 전용 그림이 이후 프레임에 빌린 포탈로 다시 덮이지 않게 한다.

## 출발 도면 카드 적용안

수정 위치는 `TemporaryGameGui.Hub.cs`의 `BlueprintIcons`와 `BuildDeparture()`다. 기존 `icon-sword`·`icon-hammer`·`icon-launcher`는 HUD와 다른 GUI도 쓰므로 덮어쓰지 않는다. 출발 화면 전용으로 큰 그림 3개를 따로 로드한다.

기본안은 **96×128 투명 무기 그림**을 기존 선택 버튼 프레임 안에 넣는 방식이다. `Image.color = Color.white`, Simple, preserveAspect를 사용한다. 128×160 전체 종이 카드는 선택 프레임과 중복 테두리를 피할 다른 배치를 택할 때만 쓴다.

960×540 화면에서의 배치 검토안은 다음과 같다. 실제 적용 시 모든 표시 문자열·선택 배지·팔레트·버튼을 합성해 검수한다.

| 요소 | 검토안 |
|---|---|
| 패널 | 744×480, 위치 `(108,30)` |
| 도면 선택 버튼 | 기존 3열·폭 220 유지, y=92·높이 176 |
| 큰 무기 그림 | 버튼 안 `(62,8)`, **96×128 네이티브 표시** |
| 이름·선택 배지 | 이름은 아래쪽, 배지는 그림과 겹치지 않는 모서리. Galmuri 12/24 사용 |
| 팔레트 미리보기 | y=284, 높이 76 검토 |
| 정보·골드·출발 | 아래 여백으로 재배치, 선택·포커스·메시지 잘림 확인 |

도면 ID·선택 순서·휴대 2개·팔레트 생성·출발 골드 지급은 그대로다. 큰 그림을 해금 항목이나 시작 장비 추가 지급으로 연결하지 않는다.

## 건물 채색 전환 적용안

염색공방·보급소·회수공방의 해금 성공 후 표시를 선택하는 지점에 **표현 전용 오버레이**를 둔다. 원래 스케치 그림은 아래에 두고 같은 위치·PPU32·pivot `(0.5,0.03125)`·크기 256×256의 마스크 프레임을 위에 재생한다.

마스크는 실제 컬러 건물의 재질 영역·알파를 이용한다. 첫 장은 투명, 마지막은 원래 컬러 PNG와 같은 픽셀이다. 마지막에 본체를 컬러로 확정하고 오버레이를 제거하면 끊김 없이 이어진다. 바닥·이웃 건물·캐릭터를 함께 칠하거나 넓은 화면 그라데이션·이중 윤곽을 만들지 않는다.

이미 컬러인 건물의 두 번째 해금에 스케치를 다시 씌우지 않는다. 해금 거래·저장은 먼저 기존 모델에서 확정하고 연출이 끝나기를 기다려 지급하지 않는다. 중단·언로드·불러오기 때도 프로필 상태에 맞는 최종 건물 그림을 복구한다. 기록 농가·출발 농로의 채색 조건은 [시작 마을 채색 규칙](../../design/world/village/starting-village.md#마을-채색과-해금)을 따르며 기존 컬러/스케치 짝만 쓴다. 우물의 미정 채색 조건은 새로 확정하지 않는다.

## 파괴·획득·폐기·안료 효과 적용안

- **가방 파괴:** 실제 소유 잉크의 파괴 명령이 성공한 뒤 선택 슬롯/아이콘 위치에 64×64 효과를 한 번 표시한다. 판매·거절·취소에 같은 파괴 효과를 붙이지 않는다. +20G 지급은 기존 모델의 영수증 로직이 소유한다.
- **제단 획득:** `TryClaim()` 성공 결과에서 선택한 카드 아이콘 중심에 획득 효과를 표시한다. 색을 고른 순간이나 실패한 인덱스에는 재생하지 않는다.
- **제단 폐기:** `DiscardUnclaimed()`로 실제 미선택 후보가 폐기될 때 그 카드에 폐기 효과를 표시한다. **이 후보는 플레이어 소유 잉크가 아니므로 잉크 파괴 +20G와 연결하지 않는다.** 반복 닫기·출구 콜백으로 효과/소리를 중복 재생하지 않는다.
- **안료 대비:** 기존 `pigment-pickup`과 `pigment-pickup-contrast`는 보존하되 현재 처치 보상에서는 재생하지 않는다. 실제 지급 성공 뒤 바닥 팔레트가 착지하고 사라진 자리에 지급량이 떠오르는 [현재 연출](../../development/gameplay/equipment/inventory-and-loot.md#데이터에셋-교체)을 사용한다.

기존 표현 수명·정지 시계를 유지하며 UI 닫힘·오브젝트 파괴·씬 전환 때 해당 연출만 정리한다. 새 효과 때문에 피해·아이템 소유권·보상·입력 제한을 추가하지 않는다.

## 초반 인물 그림 적용안

인물은 **외형 검토용 정지 스프라이트**다. 2026-11-01 이야기에서 은인·인간 행정원을 뺐으므로 이 두 그림과 정지 배경 연출(`starting_village_opening.json`)은 게임에서 쓰지 않는다. 조력자 1장과 소실 8장은 [플레이형 오프닝](../../design/story/opening/opening-playable.md)에서 쓴다. 현행 전투 주인공·무기 모션·충돌체를 교체하는 용도가 아니다. 인간 주인공과 은인은 실제 그림 높이 약 64px, 조력자는 약 128px로 현재 잉크 주인공과 크기를 비교할 수 있게 했다.

인간 주인공/은인은 PPU32·pivot `(0.5,0.05)`, 조력자는 PPU32·pivot `(0.5,0.041667)`를 기준으로 한다. 투명 캔버스 바닥을 잘라 별도 피벗을 만들지 않는다. 서술 화면 또는 별도 이야기 Actor 뷰에 넣고 현재 스토리 순서를 유지한다.

은인은 발견한 농가·우물 자리에서 숨을 거두고 다른 장소로 옮기지 않는다. 조력자의 희생은 같은 자리에서 일어난다. 이 그림을 살아 있는 마을 상점 NPC로 배치하지 않는다. `benefactor-erased`는 색 소실 상태이며 별도 사망 자세가 아니다. 조력자 8장은 같은 자세의 소실 마스크이며 보행·팔 동작·잉크 이동 전체 모션을 제공한 것이 아니다. 전체 장면 움직임·서술 시간은 이야기 담당자가 명세에 맞춰 따로 연결한다.

## 누락 파일과 기존 원본

### 검 접촉광

현재 리소스에서 승인된 `BasicSwordHit/00.png`~`05.png`의 원본을 특정하지 못했다. `effects/hit-variants`의 128px 6장은 서로 다른 피격 변주이므로 검 64px 6프레임 애니메이션의 원본이라고 바꿔치기하지 않는다.

새로 만든 `effects/basic-sword-contact/.../00.png`~`05.png`는 **대체 후보**다. 원본이 복구되면 승인 원본을 우선한다. 새 후보를 승인한 경우에만 같은 Unity 목적지의 기존 `.meta`·GUID를 보존해 계약을 갱신한다. PPU32·중심 피벗, `data/combat/presentation/base_sword_hit.json`의 프레임 끝 시각 `[0.03,0.06,0.09,0.13,0.18,0.24]`를 유지한다. 수명·명중 시점·공격 데이터는 바꾸지 않는다.

### 픽셀 가방 그림

아래 7개는 새로 그릴 필요가 없는 기존 원본이다. 에셋 ID는 `ui.inventory`, 경로는 `ui/inventory/runtime/Art/`다.

`ink-cooldown.png`, `ink-follow.png`, `ink-move.png`, `ink-power.png`, `ink-range.png`, `ink-special.png`, `ink-speed.png`.

적용 담당자가 각 원본의 해시를 확인해 기존 `unity/Assets/Deviant/Inventory/Resources/PixelInventory/Art/` 목적지와 추적 `.meta`를 명시하는 배치 계약에 추가한다. 실제 코드 경로와 GUID를 먼저 확인하며 다른 색상 잉크 20종 아이콘의 대용으로 재분류하지 않는다. 이번 작업은 이를 복원·배치하지 않았다.

`ClockExecutioner/Stationary.png`는 현재 `StationaryBytes`가 사용되므로 새 그림을 만들지 않았다. 사용되지 않는 메타데이터 정리는 참조 검사를 거치는 별도 작업이다. 2챕터는 테마·구성 설계가 없으므로 임의 배경·몬스터를 추가하지 않는다.

## 기존 효과음 재사용안

원본은 메인 소유 `unity/Assets/Deviant/Audio/SFX/`다. [재사용 목록](../../../resources/authoring/needed-assets/source/audio-reuse.json)에 해시·길이·형식을 기록했다. 8개 사건이 6개 기존 WAV를 참조하며 새 복제 WAV를 만들거나 원본을 자르지 않았다. 파일 무결성 확인은 새 사건의 청취 적합성 승인이나 실제 연결을 뜻하지 않는다.

| 사건 | 기존 WAV 상대 경로 | 재생 시점 |
|---|---|---|
| 마을 해금 | `Items/sfx_items_stamp_01_v01.wav` | 구매 성공·저장 확정 뒤 1회 |
| 안료 획득 | `Items/sfx_items_ink_pickup_01_v01.wav` | 실제 획득 성공 뒤 1회 |
| 제단 등장 | `Items/sfx_items_draw_01_v01.wav` | 사용 가능 상태로 처음 나타날 때 |
| 제단 후보 획득 | `Items/sfx_items_upgrade_01_v01.wav` | `TryClaim()` 성공 뒤 1회 |
| 제단 후보 폐기 | `Items/sfx_items_palette_break_01_v01.wav` | 실제 미선택 후보 폐기 사건 |
| 가방 잉크 파괴 | `Items/sfx_items_palette_break_01_v01.wav` | 소유 잉크 파괴 확정 뒤 1회 |
| 키캡 표시 | `UI/sfx_ui_navigate_01_v01.wav` | 선택 사항. 대상이 새로 유효해진 전환만, bob 반복 재생 금지 |
| 출구 열림 | `Items/sfx_items_draw_01_v01.wav` | 실제 출구 개방 전환 1회 |

기존 오디오 사건이 이미 같은 소리를 재생한다면 같은 순간에 새 경로를 추가하지 않는다. 반복 Draw를 사건으로 사용하지 말고 현재 CuePlayer·출력 수명·중복 방지 경계를 재사용한다. 믹서·볼륨·루프는 담당자가 실제 장치에서 확인하며 이번 파일 목록을 재생 완료로 기록하지 않는다.

## 승인 후 적용 검수

- 새 그림만 계약에 선택하고 기존 미선택 세트·원본을 보존한다.
- 제단 5색·사용 후 상태, 출구 위치·F 조건, 도면 2개 선택·포커스·팔레트 표시가 유지되는지 확인한다.
- 키캡 1초/4px 동작, 긴 키·패드 표시, 종이/보스방 배경에서의 대비를 확인한다.
- 채색 마스크가 건물 밖으로 새지 않고 마지막 색·피벗·접지가 일치하는지 확인한다.
- 획득/폐기/파괴 소유권과 +20G 지급이 섞이지 않고 화면 종료·실패·재로드로 중복 연출이 생기지 않는지 확인한다.
- 960×540·1280×720·1920×1080에서 실제 Game 뷰로 확인한다. 인물 외형·출구 표현·연출 시간은 최종 승인과 별도 검토한다.

파일 검사는 `python resources/tools/verify_needed_assets.py --main-root .`, 전체 리소스 검사는 `python resources/tools/assets.py verify --check-links`다. 이후 실제 적용 담당자는 Unity 6000.6.5f1에서 관련 제단·키캡·검·GUI 회귀 검사를 실행해야 한다. **이번 제작은 그 Unity 실행·입력·게임 플레이 검증을 수행한 결과가 아니다.**
