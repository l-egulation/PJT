---
doc_kind: archive
source_system: Notion
source_type: database-or-page
notion_id: '1cc06c87-81b0-828a-9387-01e3d4217d39'
notion_title: '💡 프로젝트 아이디어'
source_url: 'https://app.notion.com/p/1cc06c8781b0828a938701e3d4217d39?pvs=204'
snapshot_date: '2026-08-28'
---

# 💡 프로젝트 아이디어

> 원문 보관본. 현재 정책은 도메인 SSOT와 결정 로그를 따른다.

## 원문 전사

<database url="{{https://app.notion.com/p/1cc06c8781b0828a938701e3d4217d39}}" inline="true" icon="💡">
The title of this Database is: 💡 프로젝트 아이디어
<ancestor-path>
<parent-page url="https://app.notion.com/p/f7806c8781b082e3a1c1017e8cd50222" title="아이디어"/>
<ancestor-2-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-3-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-4-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
Here are the Database's Data Sources:
You can use the "fetch" tool on the URL of any Data Source to see its full schema configuration.
<data-sources>
<data-source url="{{collection://dc806c87-81b0-8387-8766-87c78b6ac794}}">
The title of this Data Source is: 💡 프로젝트 아이디어

Here is the database's configurable state:
Properties with `readOnly: true` are synced or system-managed. Do not try to update their values with page update tools.
<data-source-state>
{"default_page_template":"https://app.notion.com/p/47a06c8781b082f6a2370124418193ab","icon":"💡","name":"프로젝트 아이디어","schema":{"상태":{"description":"","groups":{"complete":[{"color":"red","description":"","name":"기각","url":"collectionPropertyOption://dc806c87-81b0-8387-8766-87c78b6ac794/UEBReQ/QFdpPQ"},{"color":"green","description":"","name":"채택","url":"collectionPropertyOption://dc806c87-81b0-8387-8766-87c78b6ac794/UEBReQ/MjVlZDE0NjAtZjc4Yy00ZDUxLTk2NjgtMTE5MTNjOTI3ZGY3"}],"current":[],"future":[],"in_progress":[{"color":"blue","description":"","name":"보류","url":"collectionPropertyOption://dc806c87-81b0-8387-8766-87c78b6ac794/UEBReQ/ZTlhNzVhODItYTA3Mi00OGNmLTkwZTctYTk0OTVjZTc2YWY5"}],"to_do":[{"color":"yellow","description":"","name":"새 아이디어","url":"collectionPropertyOption://dc806c87-81b0-8387-8766-87c78b6ac794/UEBReQ/YjE3Y2Q5NDItNGYzYS00YTc4LTkzNTktMzkxMGM0YTliODE1"}]},"name":"상태","type":"status"},"생성 일시":{"description":"","name":"생성 일시","type":"created_time"},"이름":{"description":"","name":"이름","type":"title"}},"url":"collection://dc806c87-81b0-8387-8766-87c78b6ac794"}
</data-source-state>

Here is the SQLite table definition for this data source.
<sqlite-table>
CREATE TABLE IF NOT EXISTS "collection://dc806c87-81b0-8387-8766-87c78b6ac794" (
	url TEXT UNIQUE,
	createdTime TEXT, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"생성 일시" TEXT NOT NULL, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"상태" TEXT, -- one of ["새 아이디어", "보류", "채택", "기각"]
	"이름" TEXT
)
</sqlite-table>

<templates>
<template id="47a06c87-81b0-82f6-a237-0124418193ab" name="아이디어" default="true"/>
</templates>
</data-source>
</data-sources>
Here are the Database's Views:
You can use the "fetch" tool on the URL of any View to see its full configuration.
<views>
<view url="{{view://33106c87-81b0-83fd-abdb-8866f772c331}}">
{"cardLayoutMode":"default","cardSize":"medium","dataSourceUrl":"{{collection://dc806c87-81b0-8387-8766-87c78b6ac794}}","defaultPageTemplate":"https://app.notion.com/p/26e027c7597d80f6b451c7fc4f2ce67d","displayProperties":["이름"],"fullWidthProperties":[],"groupBy":{"groupBy":"option","hideEmptyGroups":false,"property":"상태","propertyType":"status","sort":{"type":"ascending"}},"name":"목록","type":"board"}
</view>
</views>
</database>
