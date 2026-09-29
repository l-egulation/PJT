---
doc_kind: archive
source_system: Notion
source_type: database-or-page
notion_id: 'fe806c87-81b0-8319-8329-01d6a6520179'
notion_title: '🗂️ 프로젝트 자료실'
source_url: 'https://app.notion.com/p/fe806c8781b08319832901d6a6520179?pvs=204'
snapshot_date: '2026-08-28'
---

# 🗂️ 프로젝트 자료실

> 원문 보관본. 현재 정책은 도메인 SSOT와 결정 로그를 따른다.

## 원문 전사

<database url="{{https://app.notion.com/p/fe806c8781b08319832901d6a6520179}}" inline="true" icon="🗂️">
The title of this Database is: 🗂️ 프로젝트 자료실
<ancestor-path>
<parent-page url="https://app.notion.com/p/1dd06c8781b083669ab681e49cefffdd" title="자료실"/>
<ancestor-2-page url="https://app.notion.com/p/88306c8781b0821aa3cf016226b4de57" title=""/>
<ancestor-3-page url="https://app.notion.com/p/a7406c8781b082f5b3c381993d7952bf" title=""/>
<ancestor-4-page url="https://app.notion.com/p/df006c8781b0827a8bea01780f925f3f" title="특화 프로젝트"/>
</ancestor-path>
Here are the Database's Data Sources:
You can use the "fetch" tool on the URL of any Data Source to see its full schema configuration.
<data-sources>
<data-source url="{{collection://4f806c87-81b0-8303-ad48-0755c71f61f1}}">
The title of this Data Source is: 🗂️ 프로젝트 자료실

Here is the database's configurable state:
Properties with `readOnly: true` are synced or system-managed. Do not try to update their values with page update tools.
<data-source-state>
{"default_page_template":"https://app.notion.com/p/aa706c8781b082bd8cf701accdb92b19","icon":"🗂️","name":"프로젝트 자료실","schema":{"링크":{"description":"","name":"링크","type":"url"},"생성일":{"description":"","name":"생성일","type":"created_time"},"이름":{"description":"","name":"이름","type":"title"},"카테고리":{"description":"","name":"카테고리","options":[{"color":"orange","description":"","name":"기획","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/QENAfA/OWU5ZDAwZjktYjg3OS00MTE5LTgyMzEtNTYxNDcwMTRkN2Ix"},{"color":"yellow","description":"","name":"개발","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/QENAfA/YzU3NDgwOTUtOTFjYS00NjE1LWEyOWYtZjNlZmQxNjA4NDU3"},{"color":"green","description":"","name":"디자인","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/QENAfA/M2U4NmI3MWQtNzQ1MS00ZmUzLWJmOGMtNzZmY2YxNDY3NWM0"},{"color":"blue","description":"","name":"템플릿","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/QENAfA/MmUwZTk3MGMtODhlMy00YjU5LWI2YTItNWM1ZTczZWU2OWY5"},{"color":"default","description":"","name":"기타","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/QENAfA/ZGIzOWZkNGMtOWJhYS00NWVkLWI2YTctN2ZjZTZhNmJiZThm"}],"type":"select"},"파일":{"description":"","name":"파일","type":"file"},"파일형태":{"description":"","name":"파일형태","options":[{"color":"orange","description":"","name":"압축파일","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/e3lkUA/OGJmMzlmMTAtNjViZC00ODMzLTk4NmMtZmVmODYzM2IzMWMz"},{"color":"yellow","description":"","name":"이미지","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/e3lkUA/MzkzODM2YzMtZmE0NS00Mjc3LWE0MWEtMTdkNGZhYjU2MjUw"},{"color":"green","description":"","name":"문서","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/e3lkUA/ZGU0ODU4MjItZmUyMC00YTJlLWFkYTctMTIyYzQyZjM4YTg4"},{"color":"blue","description":"","name":"영상","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/e3lkUA/NzdiMDQ5ZTUtNjY2Ny00Y2NkLTk1YzItNWU3NWEzNTZiMTcx"},{"color":"purple","description":"","name":"링크","url":"collectionPropertyOption://4f806c87-81b0-8303-ad48-0755c71f61f1/e3lkUA/NzcwNmZlMzEtZTk4Yi00NGZmLWFiNmQtMjU0ODc4MWFkZjZm"}],"type":"select"}},"url":"collection://4f806c87-81b0-8303-ad48-0755c71f61f1"}
</data-source-state>

Here is the SQLite table definition for this data source.
<sqlite-table>
CREATE TABLE IF NOT EXISTS "collection://4f806c87-81b0-8303-ad48-0755c71f61f1" (
	url TEXT UNIQUE,
	createdTime TEXT, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"카테고리" TEXT, -- one of ["기획", "개발", "디자인", "템플릿", "기타"]
	"링크" TEXT,
	"파일" TEXT, -- JSON array of zero or more file IDs, Notion Folder URLs, or <folder> tags copied from fetch output. Folders are stored as native Folder references.
	"생성일" TEXT NOT NULL, -- ISO-8601 datetime string, automatically set. This is the canonical time for when the page was created.
	"파일형태" TEXT, -- one of ["압축파일", "이미지", "문서", "영상", "링크"]
	"이름" TEXT
)
</sqlite-table>

<templates>
<template id="aa706c87-81b0-82bd-8cf7-01accdb92b19" name="관련 자료" default="true"/>
</templates>
</data-source>
</data-sources>
Here are the Database's Views:
You can use the "fetch" tool on the URL of any View to see its full configuration.
<views>
<view url="{{view://d8106c87-81b0-8371-9c5a-0857403881b9}}">
{"dataSourceUrl":"{{collection://4f806c87-81b0-8303-ad48-0755c71f61f1}}","defaultPageTemplate":"https://app.notion.com/p/26e027c7597d804d95bde75a89d0076d","displayProperties":["이름","카테고리","파일형태"],"name":"목록","type":"list"}
</view>
</views>
</database>
