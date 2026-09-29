# 책임 경계

| 책임 | 소유 | 소유하지 않는 것 |
| --- | --- | --- |
| 제품 범위 | `70-plans/mvp-release` | 전투 공식·DB 구현 |
| 게임 규칙 | `30-domain`과 `60-content` | 화면 배치·작업 상태 |
| 계약 | `packages/contracts` (예정) | 각 런타임의 내부 모델 |
| 스테이지 진행 권한 | `game-api`의 progression 경계 | 브라우저 로컬 성장·실행 체크포인트·화면 캐시 |
| 로컬 성장·실행 상태 | `web`의 브라우저 저장 계층 | 스테이지 클리어·챕터 해금·거래 자산의 최종 권한 |
| 거래 자산·정산 | `game-api`의 market/economy 경계 | 펫 창의 표시 상태 |
| 분석 이벤트 | Outbox와 event contract | 거래 정합성의 최종 판정 |
| 작업 상태 | `wiki/06-delivery/tasks` | 도메인 규칙 |
| 원문·이력 | `wiki/99-archive/notion`과 `wiki/05-meetings/records` | 현재 정책 |

세부 저장 권한은 [데이터 권한과 저장 경계](./persistence.md)와 [계정·저장 SSOT](../30-domain/player/ssot.md), 이벤트 경계는 [데이터·이벤트 SSOT](../40-systems/event-system/ssot.md)를 따른다.
