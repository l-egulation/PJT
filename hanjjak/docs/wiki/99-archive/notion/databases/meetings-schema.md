---
doc_kind: archive
source_system: Notion
source_type: database-or-page
notion_id: '10806c87-81b0-83e5-98c6-814c0446e683'
notion_title: '📝 프로젝트 회의록'
source_url: 'https://app.notion.com/p/10806c8781b083e598c6814c0446e683?pvs=204'
snapshot_date: '2026-08-28'
---

# 📝 프로젝트 회의록

> 원문 보관본. 현재 정책은 도메인 SSOT와 결정 로그를 따른다.

## 원문 전사

<database url="{{https://app.notion.com/p/10806c8781b083e598c6814c0446e683}}" inline="true" icon="📝">
The title of this Database is: 📝 프로젝트 회의록
<ancestor-path>
<parent-page url="https://app.notion.com/p/c5e06c8781b083fdb40d8184c9ad00be" title="회의록"/>
<ancestor-2-page url="https://app.notion.com/p/3b006c8781b083eb94e80158c8cdd110" title=""/>
<ancestor-3-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-4-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
Here are the Database's Data Sources:
You can use the "fetch" tool on the URL of any Data Source to see its full schema configuration.
<data-sources>
<data-source url="{{collection://15606c87-81b0-8379-950d-070673c193d6}}">
The title of this Data Source is: 📝 프로젝트 회의록

Here is the database's configurable state:
Properties with `readOnly: true` are synced or system-managed. Do not try to update their values with page update tools.
<data-source-state>
{"default_page_template":"https://app.notion.com/p/ec406c8781b083b3b4ee8174e0a0221b","icon":"📝","name":"프로젝트 회의록","schema":{"날짜":{"description":"","name":"날짜","querySqlColumns":{"columns":[{"name":"date:날짜:start","sqlType":"TEXT"},{"name":"date:날짜:end","sqlType":"TEXT"},{"name":"date:날짜:is_datetime","sqlType":"INTEGER"}],"usage":"For connections.notion.querySql. Main schema name not queryable."},"type":"date"},"이름":{"description":"","name":"이름","type":"title"},"참석자":{"description":"","name":"참석자","type":"person"},"태그":{"description":"","name":"태그","options":[{"color":"red","description":"","name":"긴급회의","url":"collectionPropertyOption://15606c87-81b0-8379-950d-070673c193d6/aUtWRA/YWVkODhmNDgtY2M0ZS00YjZmLThlMDEtMWUyNmRjMDBkZjI1"},{"color":"yellow","description":"","name":"정기회의","url":"collectionPropertyOption://15606c87-81b0-8379-950d-070673c193d6/aUtWRA/N2E5ODM3MzktYTgzMC00MGNkLTg5MWYtNTY4YjU1OWZmZjM0"},{"color":"green","description":"","name":"멘토링","url":"collectionPropertyOption://15606c87-81b0-8379-950d-070673c193d6/aUtWRA/YTM3OTllMDYtZTdkMS00YzAzLWI2MmItZjI5MGI2YWZiMWQ4"}],"type":"select"},"해야할 일":{"dataSourceUrl":"collection://2254e077-662a-8370-b18e-078ab5aa4848","description":"","name":"해야할 일","type":"relation"}},"url":"collection://15606c87-81b0-8379-950d-070673c193d6"}
</data-source-state>

Here is the SQLite table definition for this data source.
<sqlite-table>
CREATE TABLE IF NOT EXISTS "collection://15606c87-81b0-8379-950d-070673c193d6" (
	url TEXT UNIQUE,
	createdTime TEXT, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"해야할 일" TEXT, -- JSON array of page URLs relating to {{collection://2254e077-662a-8370-b18e-078ab5aa4848}} data source, you must "view" {{collection://2254e077-662a-8370-b18e-078ab5aa4848}} to query this column
	"참석자" TEXT, -- JSON array of zero or more user IDs
	"date:날짜:start" TEXT, -- ISO-8601 date or datetime string. Use the expanded property (date:<column_name>:start) to set this value.
	"date:날짜:end" TEXT, -- ISO-8601 date or datetime string, can be empty. Must be NULL if the date is a single date, and must be present if the date is a range. Use the expanded property (date:<column_name>:end) to set this value.
	"date:날짜:is_datetime" INTEGER, -- 1 if the date is a datetime, 0 if it is a date, NULL defaults to 0. Use the expanded property (date:<column_name>:is_datetime) to set this value.
	"태그" TEXT, -- one of ["긴급회의", "정기회의", "멘토링"]
	"이름" TEXT
)
</sqlite-table>

<templates>
<template id="ec406c87-81b0-83b3-b4ee-8174e0a0221b" name="회의록" default="true"/>
</templates>
</data-source>
</data-sources>
Here are the Database's Views:
You can use the "fetch" tool on the URL of any View to see its full configuration.
<views>
<view url="{{view://39206c87-81b0-836e-bfdf-0865430c356c}}">
{"dataSourceUrl":"{{collection://15606c87-81b0-8379-950d-070673c193d6}}","defaultPageTemplate":"https://app.notion.com/p/26e027c7597d80cf816ac06dcde1fb84","displayProperties":["태그","이름","참석자","날짜"],"name":"목록","sorts":[{"direction":"descending","property":"날짜"}],"type":"list"}
</view>
</views>
</database>
