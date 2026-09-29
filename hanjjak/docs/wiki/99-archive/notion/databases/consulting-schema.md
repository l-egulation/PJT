---
doc_kind: archive
source_system: Notion
notion_id: '2e106c87-81b0-8281-ad64-01fac1d81dfe'
notion_title: '미팅'
source_url: 'https://app.notion.com/p/2e106c8781b08281ad6401fac1d81dfe?pvs=204'
snapshot_date: '2026-08-28'
---

# 미팅

> 이 파일은 Notion 원문 보관본이다. 현재 정책은 도메인 SSOT와 결정 로그를 따른다.

## 원문 전사

<database url="{{https://app.notion.com/p/2e106c8781b08281ad6401fac1d81dfe}}" inline="true">
The title of this Database is: 미팅
<ancestor-path>
<parent-page url="https://app.notion.com/p/85306c8781b082be8d068140f5e04947" title="컨설팅"/>
<ancestor-2-page url="https://app.notion.com/p/3b006c8781b083eb94e80158c8cdd110" title=""/>
<ancestor-3-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-4-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
Here are the Database's Data Sources:
You can use the "fetch" tool on the URL of any Data Source to see its full schema configuration.
<data-sources>
<data-source url="{{collection://e5b06c87-81b0-821d-960c-87ae3554e462}}">
The title of this Data Source is: 미팅

Here is the database's configurable state:
Properties with `readOnly: true` are synced or system-managed. Do not try to update their values with page update tools.
<data-source-state>
{"name":"미팅","schema":{"생성일":{"description":"","name":"생성일","type":"created_time"},"이름":{"description":"","name":"이름","type":"title"},"태그":{"description":"","name":"태그","options":[],"type":"multi_select"}},"url":"collection://e5b06c87-81b0-821d-960c-87ae3554e462"}
</data-source-state>

Here is the SQLite table definition for this data source.
<sqlite-table>
CREATE TABLE IF NOT EXISTS "collection://e5b06c87-81b0-821d-960c-87ae3554e462" (
	url TEXT UNIQUE,
	createdTime TEXT, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"생성일" TEXT NOT NULL, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"태그" TEXT, -- JSON array with zero or more of []
	"이름" TEXT
)
</sqlite-table>
</data-source>
</data-sources>
Here are the Database's Views:
You can use the "fetch" tool on the URL of any View to see its full configuration.
<views>
<view url="{{view://bcd06c87-81b0-836e-9962-88af8d6b408f}}">
{"dataSourceUrl":"{{collection://e5b06c87-81b0-821d-960c-87ae3554e462}}","displayProperties":["이름","태그"],"name":"리스트 보기","type":"list"}
</view>
</views>
</database>
