# 마을 잉크 에셋과 Unity 연결

[분야 개요](README.md)

원본은 `resources/ui/village-ink-return-20261107/`에 보존한다. `runtime/acquisition/`의 색별 닫힌 병·열린 병·뚜껑·빈 병 20개, `runtime/drops/`의 드롭 5개, `runtime/ui/`의 제목·빈 판·방울·집행자 삽화·수첩 종이 9개를 사용한다. 생성 원본과 적용 PNG의 바이트는 같으며 외부 이미지 서버를 호출하지 않는다.

## 배치

1. `python resources/tools/register_village_ink_assets.py`로 원본·해시·미리보기 링크를 등록한다.
2. 리소스 커밋을 push하고 메인의 `resources` gitlink에 선택한다.
3. `python tools/assets/prepare_village_ink.py`로 배치 계약과 `VillageInk/Bindings.json`을 만든다.
4. `python tools/assets/update_assets.py --asset ui.village-ink-return-20261107`로 고정 원본을 적용하고 같은 명령에 `--check`를 붙여 검증한다.

Unity 경로는 `Assets/Deviant/Game/Resources/VillageInk/`다. `.meta`와 GUID는 메인이 소유한다. Point 필터·Clamp·압축 없음·밉맵 없음으로 가져온다. 획득용 병은 원본 투명 캔버스를 보존하고, 작은 드롭과 UI만 바인딩의 내용 경계로 Sprite rect를 잡는다.

## 동작

- 1챕터 첫 클리어는 붉은 병을 F로 챙기고 2챕터로 이어진다. 런이 끝나 마을로 돌아오면 장인이 우물과 잉크를 소개한다. 별도 줍기 컷씬이나 주인공 대사는 없다.
- 2챕터 첫 클리어도 전용 드롭을 F로 챙겨야 출구를 쓸 수 있다. 현재 보스 연결은 1·2챕터이며, 3~5색 귀환은 진행도 미리보기로 확인한다.
- 마을의 미사용 잉크는 주머니 빛으로 표시한다. 2~5색은 귀환만으로 사용하지 않는다. 우물에서 F를 눌러야 다음 미사용 색 하나를 붓는다.
- 캐릭터가 우물 쪽을 바라보고 짧게 걷는다. 머리 위 작은 병이 0.55초 페이드인하고, 뚜껑을 열고 기울여 잉크를 수직으로 흘린다. 병 연출은 5.2초이며 2.6초에 색이 번진다. 주머니 빛은 병을 꺼낼 때 사라진다.
- 붓기·색 복원 뒤 닫힌 병 획득 UI가 표시된다. 첫 색은 장인 작별과 공방 안내를 마친 뒤 획득 UI를 보여 주고, F로 닫으면 끝난다(예전 침식자·마을 잉크 공책은 2026-11-13에 뺐다). 후속 시설 안내는 잉크 사용·획득 확인 이후 별도로 진행하며 회수공방은 문 수리까지 닫혀 있다.
- 미사용 잉크가 없으면 우물에는 F 키캡과 상호작용이 없다. 마을 색은 보유량과 구분한 사용 완료 기록으로 결정한다.
- 사용·획득 확인은 각각 저장된다. 재입장하면 이미 사용한 잉크를 다시 붓지 않고 미확인 화면을 이어서 표시한다. 예전 저장의 완료된 장인 방문도 사용 완료로 호환한다.

대사와 첫 장면 순서는 [첫 붉은 마을 잉크](../../../design/story/village/first-red-village-ink.md), 진행 규칙은 [마을 잉크](../../../design/progression/village-palette-pieces.md)를 따른다.

## 확인

Unity 버전은 6000.6.5f1이다. `tools/preview_village_ink_review.cmd`의 장면 선택창에서 2~5색은 우물 F로 시작한다. 모든 미리보기는 메모리 프로필을 사용한다. 장면 전환은 같은 Play 세션에서 씬만 바꾼다.

관련 검사는 `VillageInkReturnTests`, `VillageInkPreviewTests`, `VillageInkNotebookTests`, `StartingVillageRecordBookTests`, `StartingVillageFrontTests`, `Chapter1BossExitHomeTests`, `StartingVillageVisitTests`, `OpeningDirectorTests`다. 실제 캡처와 결과는 Git 제외 `outputs/village-ink/`와 `outputs/hub/capture/`에 둔다.

수첩의 두 새 삽화는 `runtime/ui/eroders-notebook.png`, `runtime/ui/well-ink-notebook.png`다. 기존 원본과 새 생성 원본을 모두 보존하며 전체 요청문은 `source/notebook-pages-20261108.json`에 있다. 기록 농가의 다음 펼침은 같은 삽화·제목·설명을 재사용한다. `tools/preview_village_ink_storybook.cmd`는 실제 저장을 건드리지 않는 이야기책 미리보기다.
