---
doc_kind: archive
source_system: Notion
notion_id: '96306c8781b0837387ef819763d34b92'
notion_title: '특화 프로젝트 일정표'
source_url: 'https://app.notion.com/p/96306c8781b0837387ef819763d34b92?pvs=204'
snapshot_date: '2026-08-28'
---

# 특화 프로젝트 일정표

> 이 파일은 Notion 원문 보관본이다. 현재 정책은 도메인 SSOT와 결정 로그를 따른다.

## 원문 전사

<database url="{{https://app.notion.com/p/96306c8781b0837387ef819763d34b92}}" inline="true">
The title of this Database is: 특화 프로젝트 일정표
<ancestor-path>
<parent-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
Here are the Database's Data Sources:
You can use the "fetch" tool on the URL of any Data Source to see its full schema configuration.
<data-sources>
<data-source url="{{collection://aa806c87-81b0-836d-a474-0714284c57ae}}">
The title of this Data Source is: 특화 프로젝트 일정표

Here is the database's configurable state:
Properties with `readOnly: true` are synced or system-managed. Do not try to update their values with page update tools.
<data-source-state>
{"name":"특화 프로젝트 일정표","schema":{"날짜":{"description":"","name":"날짜","querySqlColumns":{"columns":[{"name":"date:날짜:start","sqlType":"TEXT"},{"name":"date:날짜:end","sqlType":"TEXT"},{"name":"date:날짜:is_datetime","sqlType":"INTEGER"}],"usage":"For connections.notion.querySql. Main schema name not queryable."},"type":"date"},"이름":{"description":"","name":"이름","type":"title"},"태그":{"description":"","name":"태그","options":[],"type":"multi_select"}},"url":"collection://aa806c87-81b0-836d-a474-0714284c57ae"}
</data-source-state>

Here is the SQLite table definition for this data source.
<sqlite-table>
CREATE TABLE IF NOT EXISTS "collection://aa806c87-81b0-836d-a474-0714284c57ae" (
	url TEXT UNIQUE,
	createdTime TEXT, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"태그" TEXT, -- JSON array with zero or more of []
	"date:날짜:start" TEXT, -- ISO-8601 date or datetime string. Use the expanded property (date:<column_name>:start) to set this value.
	"date:날짜:end" TEXT, -- ISO-8601 date or datetime string, can be empty. Must be NULL if the date is a single date, and must be present if the date is a range. Use the expanded property (date:<column_name>:end) to set this value.
	"date:날짜:is_datetime" INTEGER, -- 1 if the date is a datetime, 0 if it is a date, NULL defaults to 0. Use the expanded property (date:<column_name>:is_datetime) to set this value.
	"이름" TEXT
)
</sqlite-table>
</data-source>
</data-sources>
Here are the Database's Views:
You can use the "fetch" tool on the URL of any View to see its full configuration.
<views>
<view url="{{view://94406c87-81b0-83f2-a35d-88f4047a25c3}}">
{"calendarBy":"날짜","dataSourceUrl":"{{collection://aa806c87-81b0-836d-a474-0714284c57ae}}","displayProperties":["이름"],"name":"캘린더 보기","type":"calendar"}
</view>
</views>
</database>
