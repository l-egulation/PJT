# 거래소 호가 거래 전환 계획

이 계획은 기존 개별 판매 매물·즉시 구매·구매 예약을 단일 품목 기준 양방향 주문장 경험으로 통합한 2026-09-11 Office Hours 결과와 구현 전환 범위를 연결한다.

- 설계 입력과 선택 근거: [Office Hours 설계](./design.md)
- 간편 거래·호가 거래 역할 분리 제안: [거래 모드 개선 계획](./trading-modes-plan.md)
- 거래 규칙 정본: [거래소·경제 SSOT](../../30-domain/economy/ssot.md)
- 화면 규칙 정본: [클라이언트 UX SSOT](../../30-domain/player/ux/ssot.md)
- API·저장 경계: [Networking](../../50-architecture/networking.md#거래소주문장-계약), [Persistence](../../50-architecture/persistence.md)
- 결정 상태: [결정 로그](../../80-decisions/README.md)
- 저장·동시성 결정: [거래소 주문장 아키텍처 결정](../../80-decisions/architecture/market-order-book.md)
- 실행 상태: [I 거래소·경제 작업](../../wiki/06-delivery/tasks/I-marketplace-economy/_index.md), [K 클라이언트 UI·UX 작업](../../wiki/06-delivery/tasks/K-client-ux/_index.md)

이 문서는 탐색 진입점이다. 현재 정책은 연결된 SSOT, 구현·검증 상태는 연결된 작업 문서에서 확인한다.
