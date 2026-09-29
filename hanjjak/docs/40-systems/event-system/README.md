# Event System

게임·경제 이벤트의 공통 계약과 전달 경계를 논리 시스템으로 관리한다. 이벤트 envelope, Outbox, 재시도와 소비 경계의 정본은 [Event System SSOT](ssot.md)가 소유한다.

- [이벤트 기능 경로](features/_index.md)
- [Combat Events](../combat-system/events.md)
- [Runtime Networking](../../50-architecture/networking.md)
- [J 실행 작업](../../wiki/06-delivery/tasks/J-delivery-operations/_index.md)

도메인 상태의 최종 판정과 거래 정합성은 각 도메인·런타임 경계가 소유하며, 이 시스템이 대신 결정하지 않는다.
