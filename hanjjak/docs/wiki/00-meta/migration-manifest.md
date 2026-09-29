# Notion 마이그레이션 범위

## 기준

- 원본: Notion의 특화 프로젝트 페이지와 그 하위 링크·데이터베이스
- 스냅샷: 2026-08-28
- 대상: 기획·설계·회의·작업·자료실·일정에 포함된 콘텐츠
- 원문 보관: [Notion 원문 보관소](../99-archive/notion/_index.md)

## 회수 수량

| 유형 | 수량 | 보관 위치 |
| --- | ---: | --- |
| 프로젝트 허브·상위 페이지 | 10 | 99-archive/notion/pages/project |
| 시스템 설계 하위 페이지 | 7 | 99-archive/notion/pages/design |
| 프로젝트 자료실 행 | 9 | 99-archive/notion/pages/materials |
| 아이디어 행 | 13 | 99-archive/notion/pages/ideas |
| 디벨롭 행 | 6 | 99-archive/notion/pages/develop |
| 프로젝트 회의록 행 | 5 | 05-meetings/records 및 원문 보관소 |
| 컨설팅 미팅 행 | 1 | 99-archive/notion/pages/project/consulting-2026-08-19.md |
| A–K 작업 페이지 | 95 | 06-delivery/tasks/A ~ K |
| 일정표 행 | 22 | 99-archive/notion/databases/schedule.md |
| 간트 차트 행 | 6 | 99-archive/notion/databases/gantt.md |
| 데이터베이스 스키마·구조 파일 | 19 | 99-archive/notion/databases |

페이지·행 원문은 146개(프로젝트 10 + 설계 7 + 자료 9 + 아이디어 13 + 디벨롭 6 + 회의 5 + 컨설팅 1 + 작업 95)이며, 데이터베이스 행 뷰와 스키마는 별도 파일로 보존했다.

## 첨부 자료 처리

Notion MCP가 반환한 본문·속성·첨부 참조는 모두 기록했다. 첨부 참조 8개 중 기존 다른 통합에서 업로드된 파일은 현재 연결의 download_attachment 권한으로 직접 다운로드할 수 없었으므로 바이너리 자체는 복사하지 않고 파일명·첨부 ID·원문 위치를 [첨부 목록](../99-archive/notion/attachments.md)에 보존했다. 서명된 이미지 URL은 만료될 수 있으므로 원문 링크를 참고용으로만 사용한다.
