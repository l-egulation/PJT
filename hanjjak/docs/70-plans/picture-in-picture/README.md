# 웹 Picture-in-Picture 계획

메인 웹의 펫·전투 요약을 지원 브라우저의 Document Picture-in-Picture 창에 표시하는 구현 계획이다. 별도 게임 인스턴스를 만들지 않고 단일 브라우저 문서의 상태 엔진을 공유한다.

- [요구사항과 구현 계획](./requirements.md)
- 화면·대체 동작 정본: [클라이언트 UX SSOT](../../30-domain/player/ux/ssot.md)
- 단일 상태 엔진·수명주기 정본: [계정·저장 SSOT](../../30-domain/player/ssot.md)
- 프론트엔드 배치 정본: [모노레포 설계](../../50-architecture/monorepo.md)
- 관련 실행 작업: [B-06](../../wiki/06-delivery/tasks/B-account-storage/b-06-shared-game-state-window-sync.md), [K-18](../../wiki/06-delivery/tasks/K-client-ux/k-18-pet-management-tray-windows.md), [K-19](../../wiki/06-delivery/tasks/K-client-ux/k-19-hidden-window-state-sync-validation.md)
