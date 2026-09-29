---
doc_kind: archive
source_system: Notion
source_collection: project-ideas
notion_id: '71e06c87-81b0-837f-a4f7-81ad9148f43a'
notion_title: '유명 핫플 대체 장소 추천 서비스 (숨은 핫플 찾기)'
idea_status: '새 아이디어'
source_url: 'https://app.notion.com/p/71e06c8781b0837fa4f781ad9148f43a?pvs=204'
snapshot_date: '2026-08-28'
---

# 유명 핫플 대체 장소 추천 서비스 (숨은 핫플 찾기)

> 원문 보관본. 현재 제품 범위는 제품 SSOT와 결정 로그를 따른다.

## 원문 전사

Here is the result of "fetch" for the Page with URL https://app.notion.com/p/71e06c8781b0837fa4f781ad9148f43a as of 2026-08-28T00:23:13.146Z:
<page url="https://app.notion.com/p/71e06c8781b0837fa4f781ad9148f43a" icon="icons/stars_gray">
<ancestor-path>
<parent-data-source url="collection://dc806c87-81b0-8387-8766-87c78b6ac794" name="프로젝트 아이디어"/>
<ancestor-2-database url="https://app.notion.com/p/1cc06c8781b0828a938701e3d4217d39" title=""/>
<ancestor-3-page url="https://app.notion.com/p/f7806c8781b082e3a1c1017e8cd50222" title="아이디어"/>
<ancestor-4-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-5-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-6-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
<properties>
{"url":"https://app.notion.com/p/71e06c8781b0837fa4f781ad9148f43a","상태":"새 아이디어","생성 일시":"2026-08-28T00:23:13.160Z","이름":"유명 핫플 대체 장소 추천 서비스 (숨은 핫플 찾기)"}
</properties>
<content>
<span color="gray">*⬆️ 피드백은 댓글로 작성해주세요.*</span>
## **1. 아이디어 설명**
---
> 이용자가 특정 인스타그램 핫플이나 유명 관광지/카페를 검색하면, 리뷰 및 분위기 키워드 유사도가 90% 이상 일치하는 전국의 대체 숨은 장소를 추천해 주는 웹 서비스입니다. {color="gray_bg"}
- **주요 기능**
	- 유명 장소 검색 및 기준 장소 설정
	- 키워드/분위기 유사도 기반 대체 장소 추천 (지도 히트맵 & 장소 카드 UI)
	- 장소별 분위기 레이더 차트 및 주요 연관 키워드 비교 제공
<empty-block/>
## 2. 기대 효과
---
- **사용자 관점**
	- 혼잡한 핫플 방문 부담 완화 및 개인 취향에 맞는 대체 여행지/카페 발견
- **프로젝트 관점:**
	- 지도(Kakao Map API) 및 차트 UI를 활용하여 시연 시 뛰어난 시각적 효과 전달
	- 다차원 키워드 벡터 연산을 통해 '빅데이터 분산 유사 조인'의 기술적 명분 확보
<empty-block/>
## **3. 구현 방법**
---
- **데이터셋**
	- 공공데이터포털 관광지 데이터 또는 네이버/카카오 지도 리뷰 키워드 오픈 데이터
- **분산 처리 (Hadoop/Spark)**
	- 전국 수십만 개 장소의 리뷰 키워드 및 분위기 속성을 고차원 특징 벡터로 변환하여 HDFS에 저장
	- 검색된 기준 장소와 전국 장소 간의 Cosine Similarity를 Spark/MapReduce로 병렬 연산 (Similarity Join)
- **웹 서비스**
	- 분산 연산 결과로 선별된 Top 3 장소 및 좌표를 백엔드가 수집
	- 프론트엔드에서 지도 위에 마커/히트맵을 찍고, 스파이더 차트로 속성 비교 시각화
<empty-block/>
<empty-block/>
</content>
</page>
