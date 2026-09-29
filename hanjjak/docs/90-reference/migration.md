# 문서 체계 마이그레이션 기록

`docs/docs system.txt`의 게임 중심 구조를 기준으로 2026-08-28 스냅샷의 canonical 문서를 재배치했다. 내용의 책임과 미확정 상태는 유지하고, 경로와 탐색 계층만 변경했다.

| 이전 경로 | 새 canonical 경로 | 처리 |
| --- | --- | --- |
| `wiki/01-product` | `10-game`, `20-design`, `70-plans`, `90-reference/ideas` | 비전·루프·출시 계획·아이디어로 분리 |
| `wiki/02-domains` | `30-domain` | 게임 규칙 SSOT와 기능 경로를 Player/Character/World 등으로 재배치 |
| `wiki/02-domains/data-events` | `40-systems/event-system` | 이벤트·Outbox 논리 책임으로 이동 |
| `wiki/02-domains/deployment-ops` | `50-architecture/operations` | 런타임 운영 책임으로 이동 |
| `wiki/03-architecture` | `50-architecture` | 런타임·저장·서비스 경계로 재배치 |
| `wiki/04-content` | `60-content` | 콘텐츠 모델·버전·배치로 재배치 |
| `wiki/05-meetings/decisions` | `80-decisions/README.md` | 결정 상태의 정본으로 이동 |
| `wiki/06-delivery`의 계획·검증 경로 | `70-plans/mvp-release` | 계획 경로로 이동 |
| `wiki/07-references` | `90-reference` | 참고·실험·용어로 재배치 |

## 보존 경로

- `docs/wiki/00-meta`: 운영 규칙과 SSOT 지도
- `docs/wiki/05-meetings/records`: 회의 원문 전사
- `docs/wiki/06-delivery/tasks`: A–K 실행 상태·완료 기준·증거
- `docs/wiki/99-archive/notion`: Notion 원문·스키마·첨부 메타데이터

보존 경로의 원문은 현재 규칙으로 재해석하거나 삭제하지 않는다. 새 문서는 [docs/README.md](../README.md)에서 시작한다.
