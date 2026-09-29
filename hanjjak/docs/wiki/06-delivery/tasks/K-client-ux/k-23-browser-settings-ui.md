---
doc_kind: task
owner_domain: delivery
task_code: 'K-23'
task_area: 'K 클라이언트 UI·UX'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '부분 완료'
development_status: '부분 구현'
verification_status: '부분 검증'
source: 'repository-implementation-sync'
created_at: '2026-09-08'
snapshot_date: '2026-09-14'
---

# K-23 브라우저 환경 설정 UI 구현

> 작업 상태와 완료 증거의 SSOT. 화면 동작과 저장 경계는 [클라이언트 UX SSOT](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

웹에서 지원하는 환경 설정을 조회·변경하고 브라우저 재진입 뒤 복원할 수 있으며, 설정 화면에서 기존 인증 명령으로 로그아웃할 수 있다.

## 선행 작업

B-01, K-03, K-15

## 비고

소리 켜기·끄기, 볼륨, 절전 모드를 브라우저 로컬 저장소에 버전형 값으로 보존하고 문서 루트에 적용 상태를 노출했다. 설정 화면은 기존 인증 로그아웃 명령을 사용한다. 실제 오디오 재생기 연결, 절전 모드에 따른 렌더링 소비자 연결, 프레임 제한 허용 목록과 구체적인 렌더링 정책은 아직 남아 있다.

## 증거 링크

- 웹 화면: `apps/web/src/features/settings/SettingsScreen.tsx`, `apps/web/src/features/settings/SettingsScreen.css`, `apps/web/src/main.tsx`
- 로컬 설정: `apps/web/src/features/settings/browserSettings.ts`
- 단위 검증: `apps/web/src/features/settings/browserSettings.test.ts`
- 2026-09-08 검증: `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web test`(9개 파일·25개 테스트), `corepack pnpm --filter @hanjjak/web build` 통과.
- 2026-09-08 브라우저 QA: 1280×720과 좁은 창에서 설정 화면 배치를 확인하고 소리, 볼륨 35%, 절전 모드 값을 변경한 뒤 새로고침해 동일 값이 복원되는 것을 확인했다. 검토 후 기본값으로 되돌렸다.
- 2026-09-08 빈티지 관리 UI 반영: 설정을 좌측 환경·계정 메뉴와 우측 종이 본문 구조로 재배치하고, 계정 화면에 현재 닉네임·이메일, 마이페이지 이동, 기존 로그아웃 명령을 모았다. 좁은 창에서는 메뉴가 상단 탭으로 전환된다.
- 2026-09-08 브라우저 UI QA: 로컬 모의 API로 783×717에서 환경·계정 탭 전환, 소리·볼륨·절전 제어, 계정 표시와 마이페이지 이동을 확인했다.
- 2026-09-08 검증: `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web test`(27개 파일·65개 테스트), `corepack pnpm --filter @hanjjak/web build`, `git diff --check` 통과.
- 2026-09-09 마이페이지 설정 통합: 별도 전체 화면 `SettingsScreen` 셸과 환경·계정 내부 탭을 제거하고 마이페이지 종이 모달의 `설정` 책갈피 안에 소리·볼륨·절전·로그아웃을 한 화면으로 옮겼다. 기존 브라우저 로컬 저장과 인증 로그아웃 명령은 유지하며, 상단 설정 진입점도 별도 관리 화면 대신 같은 모달의 설정 탭을 바로 연다.
- 2026-09-09 설정 본문 단순화: `내 정보`와 동일한 폭·안쪽 시작선을 적용하고 부가 제목과 설명을 제거해 `전체 소리`, `볼륨`, `절전 모드`, `로그아웃` 네 행만 남겼다. 설정 행과 조작부는 단색 표면으로 정리해 그라데이션을 사용하지 않는다. 848×731 실제 미리보기에서 책갈피와 본문 정렬, 네 설정 행, 종이 하단의 무스크롤 노출을 확인했으며 웹 전체 테스트 30개 파일·82개 테스트, `typecheck`, 프로덕션 빌드와 `git diff --check`가 통과했다.
- 2026-09-14 QA 신고 접수 초안: 설정의 `버그 신고` 행에서 텍스트만 또는 PNG/JPG/WebP 사진 1장을 첨부해 접수할 수 있다. `POST /api/v1/qa/reports`는 인증 계정, 현재 URL·user agent·viewport, 선택 사진과 멱등 키를 `qa_bug_report`에 저장하며 사진은 4MB로 제한한다. 구현 위치는 `apps/web/src/features/settings/SettingsScreen.tsx`, `apps/web/src/features/settings/qaReportApi.ts`, `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/feedback/QaBugReportController.kt`, `apps/game-api/src/main/resources/db/migration/V55__qa_bug_reports.sql`이다.
- 2026-09-14 검증: 신고 요청 단위 테스트, 사진 data URL 검증, Flyway migration 버전 유일성 테스트와 game-api Kotlin compile이 통과했다. 1280×720 브라우저에서 설정→QA 신고 창, 텍스트 입력·선택 사진 UI와 자동 환경 안내를 확인했다. 접수 목록을 열람·분류·상태 변경하는 운영 화면과 실제 운영 DB 접수 인수는 남아 있어 부분 구현·부분 검증 상태를 유지한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 부분 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
