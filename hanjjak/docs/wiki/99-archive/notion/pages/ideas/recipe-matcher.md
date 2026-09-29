---
doc_kind: archive
source_system: Notion
source_collection: project-ideas
notion_id: '61706c87-81b0-83c3-8016-815b84dc6bcb'
notion_title: '냉장고 속 재료 기반 레시피 유사도 매처 (오늘 뭐 먹지?)'
idea_status: '새 아이디어'
source_url: 'https://app.notion.com/p/61706c8781b083c38016815b84dc6bcb?pvs=204'
snapshot_date: '2026-08-28'
---

# 냉장고 속 재료 기반 레시피 유사도 매처 (오늘 뭐 먹지?)

> 원문 보관본. 현재 제품 범위는 제품 SSOT와 결정 로그를 따른다.

## 원문 전사

Here is the result of "fetch" for the Page with URL https://app.notion.com/p/61706c8781b083c38016815b84dc6bcb as of 2026-08-28T00:23:15.448Z:
<page url="https://app.notion.com/p/61706c8781b083c38016815b84dc6bcb" icon="icons/stars_gray">
<ancestor-path>
<parent-data-source url="collection://dc806c87-81b0-8387-8766-87c78b6ac794" name="프로젝트 아이디어"/>
<ancestor-2-database url="https://app.notion.com/p/1cc06c8781b0828a938701e3d4217d39" title=""/>
<ancestor-3-page url="https://app.notion.com/p/f7806c8781b082e3a1c1017e8cd50222" title="아이디어"/>
<ancestor-4-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-5-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-6-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
<properties>
{"url":"https://app.notion.com/p/61706c8781b083c38016815b84dc6bcb","상태":"새 아이디어","생성 일시":"2026-08-28T00:23:15.457Z","이름":"냉장고 속 재료 기반 레시피 유사도 매처 (오늘 뭐 먹지?)"}
</properties>
<content>
<span color="gray">*⬆️ 피드백은 댓글로 작성해주세요.*</span>
## **1. 아이디어 설명**
---
> 이용자가 냉장고에 남은 식재료를 선택하면, 대용량 레시피 데이터베이스와 비교하여 재료 구성과 조리법이 가장 유사한 가성비 요리 레시피를 추천해 주는 웹 서비스입니다. {color="gray_bg"}
- **주요 기능**
	- 보유 재료 multi-select 선택 기능
	- 유사도 높은 추천 레시피 Top 5 카드 UI 제공
	- 대체 가능한 부족한 재료 및 가성비 추천 포인트 안내
<empty-block/>
## 2. 기대 효과
---
- **사용자 관점**
	- 남은 식재료 활용도를 높여 식비 절감 및 잔반 처리 고민 해결
- **프로젝트 관점**
	- 일상적인 도메인으로 시연 시 직관적인 이해와 몰입도 확보
	- 복잡한 AI 모델 없이 자카드 유사도(Jaccard Similarity) 기반 연산으로 짧은 기간 내 높은 완성도 구현 가능
<empty-block/>
## **3. 구현 방법**
---
- **데이터셋**
	- 공공데이터포털 또는 Kaggle의 오픈 레시피 데이터(CSV/JSON) 활용
- **분산 처리 (Hadoop/Spark)**
	- HDFS에 레시피 데이터셋 저장
	- 사용자가 선택한 재료 집합과 수십만 개 레시피 재료 집합 간의 Jaccard Similarity를 Spark/MapReduce로 병렬 계산 (유사 조인)
- **웹 서비스**
	- 백엔드(FastAPI/Spring Boot)에서 분산 처리 연산 결과를 받아 REST API로 제공
	- 프론트엔드(React/Vue)에서 요리 카드로 깔끔하게 렌더링
<empty-block/>
<empty-block/>
</content>
</page>
