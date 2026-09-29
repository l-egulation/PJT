# 한짝 — 쓰레기통 아래 이야기

Windows 바탕화면 한편에 띄워 두는 방치형 웹 펫 게임입니다. 기획·설계 문서를 게임 경험 중심의 계층형 문서 체계로 관리하면서, 현재는 계정·스테이지 전투·치장 수직 슬라이스와 코드베이스 기반을 부분 구현하고 있습니다.

## 문서 시작점

[게임 중심 문서 체계](docs/README.md)

## 빠른 링크

- [SSOT 운영 규칙](docs/wiki/00-meta/ssot-policy.md)
- [주제별 SSOT 지도](docs/wiki/00-meta/source-map.md)
- [Game Vision](docs/10-game/README.md)
- [Core Loop](docs/20-design/core-loop.md)
- [Game Domain](docs/30-domain/README.md)
- [Gameplay Systems](docs/40-systems/README.md)
- [Runtime Architecture](docs/50-architecture/README.md)
- [Content](docs/60-content/README.md)
- [MVP Release Plan](docs/70-plans/mvp-release/README.md)
- [결정 로그와 충돌 목록](docs/80-decisions/README.md)
- [A–K 작업 문서](docs/wiki/06-delivery/tasks/_index.md)
- [Notion 원문 보관소](docs/wiki/99-archive/notion/_index.md)

## 현재 상태

- pnpm·Gradle 모노레포, React·Vite 웹 앱, Kotlin·Spring Boot 서버, PostgreSQL/Flyway, TypeSpec·OpenAPI, 버전 콘텐츠와 GitLab CI 기반이 구축되어 있습니다. 상세 상태와 증거는 [J-13 코드베이스 기반 작업](docs/wiki/06-delivery/tasks/J-delivery-operations/j-13-codebase-foundation.md)이 소유합니다.
- 계정 인증, 주력 재료 최초 선택·진입 차단·80:10:10 재료 드롭, 40스테이지 전투 콘텐츠·결정론 시뮬레이션, 치장 뽑기·도감·착용의 일부가 구현되어 있습니다. 기능별 완료·부분 구현 상태는 [A–K 작업 문서](docs/wiki/06-delivery/tasks/_index.md)를 확인합니다.
- 현재 배포 기준은 [MVP 범위 SSOT](docs/70-plans/mvp-release/requirements.md)를 따르며, 계정 생성부터 4-10 클리어와 거래까지의 전체 인수 흐름은 아직 완성되지 않았습니다.
- Notion에서 확인한 충돌은 삭제하거나 임의로 합치지 않고 [결정 로그](docs/80-decisions/README.md)에 보류 상태로 남겼습니다.
- `docs/wiki`에는 운영 규칙·회의 원문·A–K 작업·Notion 원문만 보존하고, 새 canonical 문서는 `docs/10-game`부터 `docs/90-reference`에 추가합니다.
