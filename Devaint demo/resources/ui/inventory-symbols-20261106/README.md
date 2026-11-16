# 인벤토리·정비소 문양 시안

색상 파츠 54종과 무기 스탬프 7종·공통 스킬 스탬프 3종의 초기 디자인 검토 자료와 개별 제작본을 함께 보존한다. 현재 게임용 PNG는 아래의 `runtime/items/`와 `runtime/items.json`을 사용한다. 초기 검토 보드는 보존 자료이며 잘라서 게임 에셋으로 사용하지 않는다.

- `preview/index.html`: 전체 보드와 아이템별 효과·문양 의도.
- `source/icon-manifest.json`: 실제 데이터 ID와 문양의 대응 64개.
- `source/prompts.json`: 기본 imagegen 도구에 전달한 전체 요청문 6개.
- `source/generation-files.json`: 보존된 생성 원본 경로와 사본 해시.

색상 파츠의 기준 원본은 메인의 `data/combat/ink_color_parts_proposal.json`이다. 스탬프 목록은 `unity/Assets/ForestCombatV2/Runtime/Chapter1Rewards.cs`의 `StampForms`와 `SkillStampName`을 따른다. 이전 실험용 50종·59종과 구분한다. 원거리 레이저·샷건·유탄은 무기 형태 스탬프를 추가하는 방식이 아니라 공통 스킬 스탬프 선택에 따른 실제 스킬이다.

문양은 큰 주효과와 최소한의 보조 표시로 구성한다. 보호막·회복, 범위·사거리, 속박·기절·독을 서로 다른 실루엣으로 나타낸다. 파츠 색은 역할 분류이며 등급 표현이 아니다. 무기 스탬프는 팔각형, 스킬 스탬프는 청록색 마름모를 사용한다. 실제 48px 가독성과 게임 화면 내 배치는 개별 에셋 제작 시 검증해야 한다.

## 단순 문양 시안

`preview/index-v2.html`에서 54종의 단색 문양과 기본 도면·발사기 스킬 외형을 포함한 무기 표 16종을 비교한다. `source/prompts-v2.json`과 `source/generation-files-v2.json`에 생성 요청과 보존 원본을 기록한다. 무기 스탬프의 실제 아이템 수는 7종이며, 나머지 보충 외형을 신규 스탬프로 정의하지 않는다. v2 문양 디자인은 사용자 확정 상태이며 개별 게임용 에셋 제작·Unity 연결은 별도다.

## 획득 아이템 바탕

`preview/backgrounds.html`은 잉크·형태 스탬프·스킬 스탬프 바탕 A/B/C를 비교한다. 빈 바탕·문양 적용·다섯 잉크 색상 슬롯을 함께 표시한다. 문양은 v2로 확정됐고 바탕 디자인은 검토 중이다. `source/background-prompts.json`에 생성 요청을, `source/background-generation-files.json`에 보존 원본과 해시를 기록한다. 검토 보드를 잘라 런타임 에셋으로 사용하지 않는다.

## 잉크 바탕과 스탬프 재시안

잉크 바탕은 기존 게임에 적용된 물방울 실루엣으로 정한다. 문양은 확정 v2를 얹는다. `preview/stamp-backgrounds-v2.html`은 형태·스킬 스탬프의 D(손잡이 도장), E(낙관 인장), F(판형) 비교다. 스탬프 바탕은 사용자 검토 중이다. 생성 원본은 보존하며 검토 보드를 잘라 게임에 적용하지 않는다.

## 개별 Unity용 제작본

확정된 v2 문양·기존 물방울·D 손잡이 도장 바탕으로 각각 새로 생성한 PNG 71개를 `runtime/items/`에 보존한다. 아이템 64개와 문양 없는 바탕 7개다. `preview/production-items.html`에서 크기·명암 배경을 바꿔 비교하고 원본 PNG를 열 수 있다. ID·SHA256·알파 내용 영역은 `runtime/items.json`, 원본 생성 경로는 `source/production-generation-files.json`에 기록한다. 바탕과 문양을 포함한 완성 이미지이며, 검토 보드에서 크롭하지 않았다. Unity 미디어 배치와 실제 인벤토리·정비소 코드의 이미지 선택 연결은 구분한다.

## 게임 표시 연결

빈 색상 잉크 바탕은 물방울 내부에 문양과 투명 얼룩이 없는 민무늬다. 초록 빈 바탕은 파랑 바탕의 실루엣을 기준으로 새로 생성하며, 수정 전·생성 원본과 요청문은 [빈 초록 바탕 제작본](source/blank-green-20261109/generation.json)에 보존한다. `tools/finalize_inventory_symbols.py`는 다섯 빈 바탕의 중앙 알파가 240 이상인지 검사해 지운 문양의 투명 흔적을 검출한다. 게임 적용본은 생성 PNG의 바이트를 그대로 사용한다.

초록 잉크의 바탕과 문양 내부는 초록색·먹색으로 통일하며 종이색 원판·밝은 채움·번짐을 두지 않는다. `ink3_green_01`, `ink3_green_04`, `ink3_green_06`, `ink3_green_10`의 수정 전 그림·수정 생성 원본·정확한 요청문은 [초록 아이콘 편집 기록](source/green-palette-review-20261108/generation-edits.json)에 연결한다. [첫 갑옷 깁기 수정본](source/corrections-20261108/edit-request.json)은 중간 제작본으로 보존하며, 게임은 `runtime/items/`의 최종본을 사용한다. 파일명·1254×1254 투명 캔버스·문양 역할은 유지한다.

공통 게임 GUI의 인벤토리·정비소에서 개별 제작본을 사용한다. 색상 파츠는 파츠 ID, 형태 스탬프는 FormId, 스킬 스탬프는 범위·이동·후속타로 연결한다. 인벤토리의 보관·장착·상세 보기와 정비소의 보관 목록·상세 보기가 같은 그림을 쓴다. 메인의 `docs/development/inventory-item-icons.md`가 구현 범위를 설명한다.
