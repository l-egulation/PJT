---
doc_kind: ssot
owner_domain: architecture
authority_level: applied
---

# Simulation

전투·성장·경제 계산은 콘텐츠 버전과 입력을 받아 재현 가능한 결과를 만드는 논리 경계로 둔다. 계산 규칙은 [Combat](../30-domain/combat/ssot.md), [Progression](../30-domain/progression/ssot.md), [Economy](../30-domain/economy/ssot.md)가 소유하고, 시스템 책임은 [Gameplay Systems](../40-systems/README.md)가 연결한다.

UI나 거래 DB가 시뮬레이션 규칙을 대신 소유하지 않는다.

## 구현 기준

- Kotlin `sim-core`가 서버 판정 계산의 유일한 구현 정본이다. HTTP, UI, Spring과 거래 DB에 의존하지 않는다.
- 서버는 Kotlin 코어로 전투·성장·경제 결과를 최종 판정한다. TypeScript 예측기는 클라이언트 렌더링을 위해 같은 입력·seed·logical tick 계약을 별도로 구현하지만 권한 결과를 확정하지 않는다.
- 두 구현은 정수 연산, 반올림, RNG, 입력·출력 schema와 replay fixture를 명시적으로 버전 관리한다.
- golden replay가 동일 콘텐츠 버전·입력·seed에 대한 Kotlin 정본 결과와 TypeScript 예측 결과를 비교한다. 불일치는 배포 차단 오류이며 허용 오차로 숨기지 않는다.
- `balance-lab`은 Kotlin `sim-core`를 직접 사용하는 CLI로 시작한다. 결과 파일과 재현 입력을 남기며 웹 UI는 실제 분석 요구가 생긴 뒤 별도 결정한다.
- 콘텐츠 버전 변경 중 진행 세션의 고정·전환 규칙은 [Networking](./networking.md), [Persistence](./persistence.md), [콘텐츠 구조와 버전](../60-content/schema.md)을 따른다.
