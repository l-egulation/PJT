# Deviant 문서

게임을 처음 이해하려면 [게임 개요](overview/overview.md) → [플레이 흐름](overview/player-flow.md) → [기획·구현 현황](overview/project-status.md)을 읽는다. 개발 환경을 준비하려면 [처음 실행하기](getting-started/README.md)에서 시작한다.

## 분야별 입구

| 분야 | 찾을 내용 |
|---|---|
| [전체 이해](overview/README.md) | 게임 개요·용어·현황·미정 사항 |
| [게임 기획](design/README.md) | 공통 규칙 → 하위 개념 → 개별 콘텐츠 |
| [개발·구현](development/README.md) | 공통 구조 → 기능 구현 → 모듈·데이터·검증 |
| [에셋](assets/README.md) | 복원·배치 계약 → 분야별 제작 규격 |
| [처음 실행하기](getting-started/README.md) | 설치·복원·플레이 테스트 |
| [협업 기준](workflow/README.md) | 기여·문서 작성·에셋 제작 절차 |
| [검토 중인 초안](draft/README.md) | 확정 전 경험 설계·기획 제안. 현행 명세와 구분한다. |

## 기준 원본

기획 확정·미정·현재 구현·실행 검증을 구분한다. 문서의 공통 규칙을 하위 콘텐츠가 참조하며, 수치는 [데이터 파이프라인](development/data/data-pipeline.md)이 지정한 원본을 편집한다. 미디어 원본은 별도 [resources 저장소](assets/resource-repository.md)가 소유한다. 문서 작성 규칙은 [문서 계층과 소유권](workflow/documentation.md)을 따른다. 작업·검증 이력은 Git, 일회성 출력은 outputs/에 둔다.

## 외부 제출 문서

요구사항, 데이터 구조, API, 테스트 계획과 결과를 목적별로 정리한다.

- [요구사항 명세서](submission/requirements.md)
- [ERD 및 데이터 설계서](submission/erd.md)
- [API 설계 문서](submission/api-design.md)
- [테스트 계획 및 결과서](submission/test-plan-results.md)
