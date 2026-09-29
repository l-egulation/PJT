---
doc_kind: ssot
owner_domain: architecture
authority_level: applied
---

# Client

클라이언트는 단일 브라우저 탭에서 펫 UI와 관리 UI를 표시하고 명령·조회·이벤트를 연결한다. 단일 게임 상태 엔진과 화면 구조의 현재 규칙은 [Player UX SSOT](../30-domain/player/ux/ssot.md), 서버 진행 권한과 브라우저 실행 체크포인트의 구분은 [Player SSOT](../30-domain/player/ssot.md)가 소유한다.

클라이언트는 서버 DB 스키마를 직접 소유하거나 스테이지 클리어·챕터 해금·거래 결과를 최종 판정하지 않는다. 로컬 비거래 성장은 [Progression SSOT](../30-domain/progression/ssot.md)를 따른다. 모노레포와 계약 배치의 제안은 [Monorepo](./monorepo.md)를 따른다.

## 구현 구조

- `web`과 `admin-console`은 각각 React·Vite·TypeScript 애플리케이션이다. 운영 UI는 game web과 같은 `PUBLIC_ORIGIN`의 `/admin/`에서 제공하지만 게임 클라이언트 route나 bundle에 포함하지 않는다. 현재 `admin-console`은 운영자 로그인, health·배포·세션·command·Outbox 요약, 정확 검색 기반 계정 타임라인, 상태 필터형 Outbox 전달 진단, 일별 경제 지표와 append-only 감사 조회를 제공하며 위험 mutation은 제공하지 않는다.
- 앱 내부는 기능 단위 수직 슬라이스를 사용한다. feature는 자체 `api`, `model`, `ui`를 소유하고 `app`과 `shared`에는 횡단 책임만 둔다.
- TanStack Query가 생성된 client를 통한 서버 상태·조회 cache를 소유한다. Zustand는 전투 예측, 브라우저 실행 checkpoint와 장시간 로컬 runtime 상태만 소유한다.
- 일시적인 화면·폼 상태는 가장 가까운 컴포넌트가 소유한다. 서버 cache, runtime 상태, 화면 상태를 하나의 전역 store에 합치지 않는다.
- IndexedDB 접근, API client, runtime engine은 adapter 경계를 통해 feature와 연결한다. UI가 저장 schema나 전송 DTO를 도메인 모델로 사용하지 않는다.
- `stateVersion`, 세션 대체, 로그아웃과 전투 만료를 수신하면 서버 cache와 runtime 상태를 각 책임에 맞게 무효화·종료한다. 로컬 예측으로 서버 권한 상태를 덮어쓰지 않는다.
- `packages/ui`에서는 디자인 토큰, 접근성 패턴과 primitive만 공유한다. 게임·운영 기능 컴포넌트는 각 앱이 소유한다.

## 검증 기준

단위·컴포넌트 검증은 Vitest·Testing Library, 실제 브라우저 사용자 흐름과 lifecycle 검증은 Playwright를 사용한다. TypeScript 예측 계산의 동등성은 [Simulation](./simulation.md)의 golden replay가 소유한다.
