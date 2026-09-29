---
doc_kind: task
owner_domain: delivery
task_code: 'K-15'
task_area: ''
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/ca006c8781b082eb998081f83f98620c'
notion_id: 'ca006c87-81b0-82eb-9980-81f83f98620c'
snapshot_date: '2026-08-28'
---

# K-15 로딩·빈 상태·오류·확인·알림 패턴 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

모든 핵심 화면에 로딩·빈 상태·실패·재시도·위험 행동 확인과 성공 알림을 동일 규칙으로 적용하고, 성장 메뉴 레드닷과 미수령 우편 숫자 배지의 확인 상태를 일관되게 갱신한다.

## 선행 작업

K-02, K-03

## 비고

세션 확인 요청이 배포 중인 API·호스트 오류(HTTP 5xx, 네트워크 연결 실패)를 반환하면 로그인 화면 대신 배포 상태 화면을 표시한다. Nginx API 프록시도 upstream 5xx를 같은 정적 화면으로 전달한다.

## 증거 링크

- `apps/web/src/features/auth/AuthGate.tsx`, `apps/web/src/features/auth/api.ts`, `apps/web/src/features/auth/DeploymentStatusScreen.tsx`, `apps/web/src/features/auth/DeploymentStatusScreen.css`
- `apps/web/public/deployment.html`, `apps/web/nginx.conf`
- `apps/web/src/features/auth/api.test.ts`, `apps/web/src/features/auth/DeploymentStatusScreen.test.tsx`
- 2026-09-09 `pnpm --filter @hanjjak/web typecheck` 통과
- 2026-09-09 인증·배포 상태 focused test 2개 파일, 6개 테스트 통과
- 2026-09-09 `pnpm --filter @hanjjak/web build` 통과 및 `html-validate apps/web/public/deployment.html` 통과
- 2026-09-09 1568×981 브라우저 스모크에서 `deployment.html`이 `배포중입니다`와 재접속 안내를 표시하는 것을 확인

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
