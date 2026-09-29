# 한짝 문서

한짝의 게임 기획·설계와 부분 구현 현황을 게임 경험에서 기술 구현으로 내려가는 계층으로 관리한다. 이 문서는 문서 체계의 진입점이다.

## 읽기 순서

1. [Game Vision](./10-game/README.md)
2. [Game Loop / Player Experience](./20-design/README.md)
3. [Game Domain](./30-domain/README.md)
4. [Gameplay Systems](./40-systems/README.md)
5. [Runtime Architecture](./50-architecture/README.md)
6. [Content](./60-content/README.md)
7. [Plans](./70-plans/README.md)
8. [GDR / ADR](./80-decisions/README.md)
9. [Reference](./90-reference/README.md)

마이그레이션 경로는 [문서 체계 마이그레이션 기록](./90-reference/migration.md)에서 확인한다.

## 문서 책임

- 게임 의도와 경험은 `10-game`, 플레이 루프는 `20-design`이 소유한다.
- 게임 규칙은 `30-domain`의 각 `ssot.md`가 소유한다.
- 논리 게임 시스템은 `40-systems`, 실행 토폴로지와 저장·운영 경계는 `50-architecture`가 소유한다.
- 콘텐츠 모델과 버전 배치는 `60-content`가 소유한다.
- 제안·출시 계획은 `70-plans`, 결정의 상태와 전파는 `80-decisions`가 소유한다.
- 원문·실험·용어 참고는 `90-reference`와 기존 Notion 보관소가 소유한다.

## 보존된 운영·원문 경로

기존 LLM Wiki의 운영 규칙, 회의 원문, A–K 실행 상태, Notion 원문은 호환·보존 경로로 남긴다.

- [문서 운영 규칙](./wiki/00-meta/ssot-policy.md)
- [구조 호환 안내](./wiki/00-index.md)
- [회의 원문](./wiki/05-meetings/records/_index.md)
- [A–K 실행 작업](./wiki/06-delivery/tasks/_index.md)
- [Notion 원문 보관소](./wiki/99-archive/notion/_index.md)

`docs/docs system.txt`는 이 계층으로의 마이그레이션 입력 문서다. 실제 구현 상태와 완료 증거는 [A–K 실행 작업](./wiki/06-delivery/tasks/_index.md), 정책은 각 책임 SSOT에서 확인한다.
