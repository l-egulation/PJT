---
doc_kind: archive
source_system: Notion
notion_id: '42c06c87-81b0-83a5-80eb-818964dc099a'
notion_title: 'Stage'
source_url: 'https://app.notion.com/p/42c06c8781b083a580eb818964dc099a?pvs=204'
snapshot_date: '2026-08-28'
---

# Stage

> 이 파일은 Notion 원문 보관본이다. 현재 정책은 도메인 SSOT와 결정 로그를 따른다.

## 원문 전사

<database url="{{https://app.notion.com/p/42c06c8781b083a580eb818964dc099a}}" inline="true">
The title of this Database is: Stage
<ancestor-path>
<parent-page url="https://app.notion.com/p/05606c8781b083f9820701a99375c320" title="디자인 레퍼런스"/>
<ancestor-2-data-source url="collection://4f806c87-81b0-8303-ad48-0755c71f61f1" name="프로젝트 자료실"/>
<ancestor-3-database url="https://app.notion.com/p/fe806c8781b08319832901d6a6520179" title=""/>
<ancestor-4-page url="https://app.notion.com/p/1dd06c8781b083669ab681e49cefffdd" title="자료실"/>
<ancestor-5-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-6-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-7-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
Here are the Database's Data Sources:
You can use the "fetch" tool on the URL of any Data Source to see its full schema configuration.
<data-sources>
<data-source url="{{collection://d1d06c87-81b0-8358-9bde-875409315d0a}}">
The title of this Data Source is: Stage

Here is the database's configurable state:
Properties with `readOnly: true` are synced or system-managed. Do not try to update their values with page update tools.
<data-source-state>
{"name":"Stage","schema":{"이름":{"description":"","name":"이름","type":"title"}},"url":"collection://d1d06c87-81b0-8358-9bde-875409315d0a"}
</data-source-state>

Here is the SQLite table definition for this data source.
<sqlite-table>
CREATE TABLE IF NOT EXISTS "collection://d1d06c87-81b0-8358-9bde-875409315d0a" (
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
<view url="{{view://d7406c87-81b0-83cf-8f88-081f0174600e}}">
{"dataSourceUrl":"{{collection://d1d06c87-81b0-8358-9bde-875409315d0a}}","displayProperties":["이름"],"name":"","type":"table"}
</view>
</views>
</database>
