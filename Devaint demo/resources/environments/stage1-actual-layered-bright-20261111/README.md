# 1스테이지 실제 방 · 채택한 밝은 레이어 시안

**미술 채택본 / Unity 미적용.** 다음 제작의 공통 기준은 [밝은 스테이지 제작 기준](../stage1-modular-tiles-20261111/STYLE-GUIDE.md)이다.

[전체 4방](preview/four-rooms-overview.png) · [레이어 분리](preview/layer-breakdown.png) · [카메라/레이어 보기](preview/index.html)

| 방 | 원형 크기 | 전체 / 원본 크기 일부 |
|---|---|---|
| S1-04 세 단 언덕 | 48×24u | [계단형 전체](preview/S1-04-bright.png) / [1배 일부](preview/S1-04-detail-1x.png) |
| S1-05 낮은 뿌리 터널 | 48×24u | [낮은 터널 전체](preview/S1-05-bright.png) / [1배 일부](preview/S1-05-detail-1x.png) |
| S1-07 계단 탑 | 32×36u | [수직방 전체](preview/S1-07-bright.png) / [1배 일부](preview/S1-07-detail-1x.png) |
| S1-12 삼자 누각 | 32×36u | [통과형 발판방 전체](preview/S1-12-bright.png) / [1배 일부](preview/S1-12-detail-1x.png) |

## 실제 맵과 타일

메인 기준 커밋 add1e8a9b464f8acdf8c2db8b3fadf34f8362e72의 방 생성기로 만든 원형을 사용한다. 슬롯 전부 열림·미러링 없음이며 현재 실행 중인 랜덤 시드 전체 맵의 캡처는 아니다. [rooms.json](source/rooms.json)의 grid/plats/출입구는 원본을 보존했다. 생성기의 재계산 결과와 4개 방 구조가 일치한다.

타일은 environments/stage1-modular-tiles-20261111의 실제 32×32 PNG를 그대로 불러왔다. 1u=32px, 확대·재페인팅·임의 발판 추가 없음. solid 1,630칸과 기존 one-way 정의를 반영했다. 원래 y=0에 있는 아래 방 연결 발판은 방 캔버스 아래 경계에 위치한다. 방 밖은 연결된 지형으로 취급해 외곽에 장식 테두리를 새로 만들지 않는다.

## 분리된 PNG

각 layers/S1-XX/에는 **같은 캔버스 크기의 네 장**이 있다.

1. 01-far.png: 하늘·산·마을. 패럴랙스 속도 0.2는 검토용 제안.
2. 02-architecture.png: 벽·기둥·창·아치. 개구부는 알파 투명. 월드 위치 고정.
3. 03-terrain.png: 실제 타일로 조립한 solid와 목재 발판. 플레이 영역 외부는 투명.
4. 04-character.png: 기존 캐릭터를 크기 비교용으로 배치. 별도 레이어.

건축과 먼 풍경은 한 PNG에 합쳐 저장하지 않는다. preview의 밝은 전체 이미지만 열람용 합성본이다. source에는 생성 원본을 그대로 보존했고, layers는 방 크기에 맞춘 출력이다. 건축 비율은 균일 확대/크롭으로 유지했다. 수직 배경은 맨 위 장식띠 30px를 크롭하여 열린 출입구 뒤에 가짜 발판처럼 보이지 않게 했다.

배경은 시안용 두 건축 소스(넓은 홀/높은 아트리움)와 공통 원경을 사용한다. 본 게임용 배경 모듈 전체나 무한 반복 월드가 완성된 것은 아니다. 원경·건축의 네이티브 비율과 합성 결과는 실제 방에서 검토한다.

## 확인 범위

- 메인 방 생성기와 원형 4종의 grid/plats/크기/출입구 일치.
- 기존 타일 PNG와 합성 지형의 보이는 픽셀 일치.
- 배경 개구부의 투명 영역과 4레이어 분리.
- 합성 PNG 시각 확인, 파일/해시/상대 링크 검사.
- HTML은 카메라와 레이어 토글을 위한 검토 도구이며 브라우저 동작 검증·Unity 적용은 수행하지 않았다.

[생성 요청문](source/prompts.json) · [타일 선택 좌표](source/tile-placements.json)

재생성: pwsh -NoProfile -File source/build.ps1
