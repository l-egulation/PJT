---
doc_kind: archive
source_system: Notion
source_collection: project-ideas
notion_id: '08606c87-81b0-832e-bdc8-0155b1bf4403'
notion_title: '실종자 수색 범위 확인 서비스'
idea_status: '기각'
source_url: 'https://app.notion.com/p/08606c8781b0832ebdc80155b1bf4403?pvs=204'
snapshot_date: '2026-08-28'
---

# 실종자 수색 범위 확인 서비스

> 원문 보관본. 현재 제품 범위는 제품 SSOT와 결정 로그를 따른다.

## 원문 전사

Here is the result of "fetch" for the Page with URL https://app.notion.com/p/08606c8781b0832ebdc80155b1bf4403 as of 2026-08-28T00:23:13.302Z:
<page url="https://app.notion.com/p/08606c8781b0832ebdc80155b1bf4403" icon="icons/stars_gray">
<ancestor-path>
<parent-data-source url="collection://dc806c87-81b0-8387-8766-87c78b6ac794" name="프로젝트 아이디어"/>
<ancestor-2-database url="https://app.notion.com/p/1cc06c8781b0828a938701e3d4217d39" title=""/>
<ancestor-3-page url="https://app.notion.com/p/f7806c8781b082e3a1c1017e8cd50222" title="아이디어"/>
<ancestor-4-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-5-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-6-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
<properties>
{"url":"https://app.notion.com/p/08606c8781b0832ebdc80155b1bf4403","상태":"기각","생성 일시":"2026-08-28T00:23:13.322Z","이름":"실종자 수색 범위 확인 서비스"}
</properties>
<content>
<span color="gray">*⬆️ 피드백은 댓글로 작성해주세요.*</span>
## **1. 아이디어 설명**
---
현재 수색 인원의 GPS 이동 경로만 기록하면 이런 문제가 생김
> 이 길을 지나가긴 했지만, 건물이나 수풀 때문에 오른쪽 구역은 실제로 보지 못했다.
각 수색자의 휴대폰에서 다음 데이터를 실시간으로 보낸다.
- GPS 위치
- 이동 방향
- 휴대폰이 바라보는 방향
- 카메라 시야각
- 촬영 여부
- 발견물 신고
서버는 단순 이동 경로가 아니라 **수색자가 실제로 볼 수 있었던 부채꼴 영역**을 계산해 지도에 표시한다
- 초록색: 충분히 확인한 구역
- 노란색: 지나갔지만 제대로 보지 못한 구역
- 빨간색: 아무도 확인하지 않은 구역
- 줄무늬: 여러 팀이 중복 수색한 구역
관제자는 미수색 구역을 다른 팀에 바로 배정할 수 있음. 통신이 끊기면 휴대폰에 저장했다가 연결 후 동기화함
<empty-block/>
**빅데이터·분산 기술**
- 여러 수색자의 위치 스트림 수집
- 구역별 수색 상태 실시간 집계
- 중복·누락 구역 계산
- 통신 재연결 시 이벤트 병합
- 대규모 모의 위치 데이터 처리
## 2. 기대 효과
---
<empty-block/>
<empty-block/>
## **3. 구현 방법**
---
<empty-block/>
<empty-block/>
<empty-block/>
</content>
</page>
