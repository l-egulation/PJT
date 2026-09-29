---
doc_kind: archive
source_system: Notion
source_collection: project-ideas
notion_id: 'ad206c87-81b0-82d7-8654-012ab095004c'
notion_title: 'AI 에이전트와 인간이 함께 참여하는 대규모 게임이론 실험 플랫폼'
idea_status: '기각'
source_url: 'https://app.notion.com/p/ad206c8781b082d78654012ab095004c?pvs=204'
snapshot_date: '2026-08-28'
---

# AI 에이전트와 인간이 함께 참여하는 대규모 게임이론 실험 플랫폼

> 원문 보관본. 현재 제품 범위는 제품 SSOT와 결정 로그를 따른다.

## 원문 전사

Here is the result of "fetch" for the Page with URL https://app.notion.com/p/ad206c8781b082d78654012ab095004c as of 2026-08-28T00:23:13.180Z:
<page url="https://app.notion.com/p/ad206c8781b082d78654012ab095004c" icon="icons/stars_gray">
<ancestor-path>
<parent-data-source url="collection://dc806c87-81b0-8387-8766-87c78b6ac794" name="프로젝트 아이디어"/>
<ancestor-2-database url="https://app.notion.com/p/1cc06c8781b0828a938701e3d4217d39" title=""/>
<ancestor-3-page url="https://app.notion.com/p/f7806c8781b082e3a1c1017e8cd50222" title="아이디어"/>
<ancestor-4-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-5-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-6-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
<properties>
{"url":"https://app.notion.com/p/ad206c8781b082d78654012ab095004c","상태":"기각","생성 일시":"2026-08-28T00:23:13.192Z","이름":"AI 에이전트와 인간이 함께 참여하는 대규모 게임이론 실험 플랫폼"}
</properties>
<content>
<span color="gray">*⬆️ 피드백은 댓글로 작성해주세요.*</span>
## **1. 아이디어 설명**
---
사람과 AI가 함께 참여하는 **게임이론 실험 플랫폼**입니다.
죄수의 딜레마, 소수결 게임, 공유지의 비극 등 여러 게임이론 콘텐츠를 진행하며, 참가자들은 주어진 상황에서 자신의 선택을 합니다.
실제 사용자가 부족한 부분은 **AI 플레이어**로 채웁니다. AI도 전체 상황을 알고 움직이는 것이 아니라, 실제 사용자처럼 **자신에게 주어진 정보만 보고 판단**합니다.
이를 통해 많은 참가자가 동시에 선택했을 때 어떤 집단 행동이 나타나는지 관찰합니다.
<empty-block/>
## 2. 기대 효과
---
- AI 플레이어를 이용해 **수천\~수만 명 규모의 참여 상황을 재현**할 수 있습니다.
- 많은 참가자가 동시에 선택하면서 발생하는 **대량 트래픽을 분산 처리**할 수 있습니다.
- 단순 더미 데이터가 아니라 AI가 직접 판단해서 만든 **행동 데이터를 수집**할 수 있습니다.
- 인간과 AI의 선택 차이, 협력률, 배신률 등 **집단 행동을 분석**할 수 있습니다.
- 실제 사용자가 직접 참여할 수 있어 **재미있는 시연이 가능**합니다.
<empty-block/>
## **3. 구현 방법**
---
- 죄수의 딜레마, 소수결 게임 등 간단한 게임이론 콘텐츠를 구현합니다.
- 실제 사용자와 함께 참여할 **AI 플레이어를 생성**합니다.
- AI에게 현재 점수, 이전 결과 등 제한된 정보만 제공하고 스스로 선택하게 합니다.
- 사용자와 AI가 발생시키는 대량의 선택 데이터를 **Kafka를 통해 전달**합니다.
- 여러 서버와 Worker가 데이터를 나누어 처리하도록 구성합니다.
- Redis와 DB를 활용해 결과와 통계를 저장합니다.
- 협력률, 배신률, 선택 변화 등을 **실시간 대시보드로 시각화**합니다.
- AI 플레이어 수를 늘려가며 서버의 처리량과 응답속도를 측정하고 **분산처리 적용 전후의 성능을 비교**합니다.
<empty-block/>
<empty-block/>
</content>
</page>
