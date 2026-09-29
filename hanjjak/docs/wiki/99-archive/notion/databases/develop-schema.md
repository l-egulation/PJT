---
doc_kind: archive
source_system: Notion
source_type: database-or-page
notion_id: '84b06c87-81b0-82d2-a3df-81a7aec81cbb'
notion_title: '디벨롭'
source_url: 'https://app.notion.com/p/84b06c8781b082d2a3df81a7aec81cbb?pvs=204'
snapshot_date: '2026-08-28'
---

# 디벨롭

> 원문 보관본. 현재 정책은 도메인 SSOT와 결정 로그를 따른다.

## 원문 전사

<database url="{{https://app.notion.com/p/84b06c8781b082d2a3df81a7aec81cbb}}" inline="true">
The title of this Database is: 디벨롭
<ancestor-path>
<parent-page url="https://app.notion.com/p/f7806c8781b082e3a1c1017e8cd50222" title="아이디어"/>
<ancestor-2-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-3-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-4-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
Here are the Database's Data Sources:
You can use the "fetch" tool on the URL of any Data Source to see its full schema configuration.
<data-sources>
<data-source url="{{collection://6a606c87-81b0-8254-b209-87a2d636a6d5}}">
The title of this Data Source is: 디벨롭

Here is the database's configurable state:
Properties with `readOnly: true` are synced or system-managed. Do not try to update their values with page update tools.
<data-source-state>
{"name":"디벨롭","schema":{"이름":{"description":"","name":"이름","type":"title"}},"url":"collection://6a606c87-81b0-8254-b209-87a2d636a6d5"}
</data-source-state>

Here is the SQLite table definition for this data source.
<sqlite-table>
CREATE TABLE IF NOT EXISTS "collection://6a606c87-81b0-8254-b209-87a2d636a6d5" (
	url TEXT UNIQUE,
	createdTime TEXT, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"이름" TEXT
)
</sqlite-table>
</data-source>
</data-sources>
Here are the Database's Views:
You can use the "fetch" tool on the URL of any View to see its full configuration.
<views>
<view url="{{view://9f906c87-81b0-8340-bd8d-88c59dba1781}}">
{"dataSourceUrl":"{{collection://6a606c87-81b0-8254-b209-87a2d636a6d5}}","displayProperties":["이름"],"name":"","type":"table"}
</view>
</views>
</database>
