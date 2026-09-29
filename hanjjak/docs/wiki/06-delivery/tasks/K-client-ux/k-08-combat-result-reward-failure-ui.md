---
doc_kind: task
owner_domain: delivery
task_code: 'K-08'
task_area: ''
task_type: '개발'
priority: 'P0'
target_chapters: '챕터 1, 챕터 2, 챕터 3, 챕터 4'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/7aa06c8781b0836fb9fa01bafdfb7a32'
notion_id: '7aa06c87-81b0-836f-b9fa-01bafdfb7a32'
snapshot_date: '2026-08-28'
---

# K-08 전투 결과·보상·실패 안내 UI 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

현재 체류 세션의 획득 누적과 실패 원인·재시작 결과를 메인 화면에서 확인하고, 서버가 사용자별로 보관한 최근 사건 중 스테이지 승리·패배 결과를 전체 메뉴와 HUD 기록창에서 다시 보며, 스테이지 상세에는 선택 스테이지의 최근 실패 1건만 표시한다.

## 선행 작업

E-04, F-05, K-06

## 비고

현재 스테이지 체류 동안 서버가 확정한 쌀과 아이템 지급량을 전투 화면의 접이식 획득 패널에 누적한다. 수동 스테이지 변경이나 자동 진행으로 체류 스테이지가 바뀌면 누적 표시를 초기화하고, 인벤토리 공간 부족으로 폐기·건너뛴 보상이 있으면 미지급 안내를 함께 표시한다. 실패 원인과 1초 부활 대기·같은 스테이지 0/20 재시작 결과는 메인 화면과 PIP에 함께 표시한다. 전체 메뉴와 HUD 기록창은 같은 서버 기록에서 승리·패배 결과만 표시하고, 선택 스테이지의 최근 실패 1건 또는 `실패 기록 없음`도 같은 기록에서 조회한다. 브라우저 재실행 후 체류 누적 복원은 남아 있다.

## 증거 링크

- 2026-09-03 구현: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/api.ts`
- 2026-09-03 검증: 브라우저 스모크에서 1-1 전투 완료 뒤 `사이클 완료`, 일반 20/20, 보상명과 획득량 표시, 아이템 탭 인벤토리 반영 확인. 서버 챕터 1 자동 진행에서는 누적 획득 아이템과 실패 중단 사유 표시를 확인했다.
- 2026-09-08 구현: `apps/web/src/features/battle/runtimeStore.ts`, `apps/web/src/features/battle/battlePresentation.ts`, `apps/web/src/features/battle/BattleScreen.tsx`; 스테이지 체류별 확정 쌀·아이템 집계와 공간 부족 표시를 연결했다.
- 2026-09-08 브라우저 QA: 3-7 반복 전투에서 쌀과 감자·고구마·옥수수 보상이 사이클 완료마다 누적되고 같은 스테이지 반복 중 유지되는 것을 확인했다.
- 2026-09-09 최신 `main` 재기반 후 새 3-7 체류가 보상 없음 상태에서 시작하고, 반복 사이클 동안 체류 스테이지가 유지되는 것을 실제 로그인 세션에서 다시 확인했다.
- 2026-09-09 이슈 #4 구현: `BattleHistoryScreen.tsx`, `BattleHistoryScreen.css`, `BattleScreen.tsx`, `api.ts`, `main.tsx`; 전체 메뉴에 최신순 전투 기록, 로딩·빈 상태·오류 재시도와 실패 스펙 표시를 추가하고 전투 화면 선택 스테이지에 최근 실패 1건 또는 명시적 빈 상태를 표시한다.
- 2026-09-09 검증: 웹 TypeScript typecheck, production build, Vitest 33개 파일·91개 테스트를 통과했다. 실제 Chromium 1440×1000에서 인증된 앱 surface를 열고 전투 기록 메뉴, `0 / 50`, `아직 전투 기록이 없습니다` 빈 상태와 가로 overflow 없음까지 확인했다.
- 2026-09-09 GitLab #3 실패 안내: `apps/web/src/features/battle/BattleScreen.tsx`, `BattleScreen.css`, `PetBattleSurface.tsx`, `battlePresentation.ts`; `PLAYER_DIED`와 `TIME_LIMIT`을 사용자 문구로 바꾸고 1초 부활·같은 스테이지 0/20 재시작을 양쪽 전투 표면에 표시한다.
- 2026-09-09 검증: 실패 코드별 문구와 메인/PIP가 공유하는 `retryAt` 생성·초기화 상태 테스트가 통과했다.
- 2026-09-10 획득·전투 기록 HUD 조립: 현재 체류 쌀·재료·스킬북을 실제 누적 값으로 렌더하고 한 번에 최대 4행만 표시하며 초과 항목은 페이지 버튼으로 전환한다. 전투 기록 버튼은 좌상단에서 최근 서버 기록 최대 50건 패널로 확장되고 로딩·빈 상태·오류 재시도를 유지한다. 웹 전체 149개 테스트, typecheck, production build와 로컬 확장 패널 시각 검수가 통과했다. 브라우저 재실행 후 체류 누적 복원은 기존과 같이 남아 있다.
- 2026-09-10 획득 패널 가독성 보정: 헤더의 스테이지 체류 누적 설명을 제거하고 최대 4개 보상 행의 재료 아이콘과 이름·수량 글자를 확대했다. 서버 누적 값, 5종 이상 페이지 전환, 미지급 경고 동작은 그대로 유지하며 1265×712에서 패널 높이와 전투 영역 겹침을 확인했다.
- 2026-09-10 전투 기록 결과 중심 개편: 전체 화면과 HUD 기록창에서 스테이지 입장·전투 복귀 사건 및 승패 설명 문장·전투 스펙을 숨겼다. 승리는 입장/클리어 시각·소요 시간·회복 전 남은 HP를, 패배는 입장/패배 시각·소요 시간·실패 원인·남은 HP·처치 수·마지막 상대 HP를 표시한다. 과거 스냅샷은 결과 시각과 전투 시간으로 입장 시각을 복원한다. 획득 소형창의 스킬북 ID는 현재 카탈로그와 같은 등급·스킬 표시명으로 변환한다.
- 2026-09-10 검증: 웹 Vitest 44개 파일·174개 테스트, TypeScript typecheck와 TypeSpec OpenAPI 생성을 통과했다.
- 2026-09-10 HUD 기록창 간격 보정: `apps/web/src/features/battle/BattleHud.css`에서 종이 이미지 자체의 투명 내부 여백을 고려해 프로필–전투 기록 버튼의 시각 간격과 전투 기록 버튼–확장 패널의 시각 간격을 동일하게 맞췄고 일반 전투 미리보기에서 확인했다.
- 2026-09-10 전투별 획득 요약: 전체 전투 기록과 HUD 기록창의 모든 승패 항목에 서버 스냅샷 기준 경험치·강화 재료 합계·쌀 수량을 표시하고, 실제 지급 아이템의 표시명·수량을 유지했다. 패배 기록도 처치 확정 시 이미 지급된 보상을 같은 방식으로 표시한다.
- 2026-09-10 검증: 웹 Vitest 44개 파일·184개 테스트, TypeScript typecheck, production build와 `git diff --check`를 통과했다. 테스트 환경의 API 프록시 `localhost:3000` 미기동 로그는 발생했지만 테스트 실패는 없었다.
- 2026-09-10 브라우저 확인: `battle-hud-preview.html`의 승리·패배 예시 기록에서 경험치·강화 재료·쌀 요약과 지급 아이템 상세가 각 기록 안에 함께 표시되고 기존 HUD 영역과 겹치지 않는 것을 확인했다.
- 2026-09-10 HUD 기록 정보 위계 보정: 각 기록의 첫 줄에 승패·스테이지와 클리어/패배 시각을 함께 배치하고, 입장·소요·HP·실패 상세를 동일한 정보칸으로 정렬했다. 획득 수치는 별도 3열 묶음으로 유지하되 숫자 대비를 높이고 지급 아이템 라벨을 추가해 결과 → 전투 상태 → 획득 순서로 읽히게 했다.
- 2026-09-10 가독성 검증: 753px 폭 전투 HUD 미리보기에서 승리·패배 두 기록의 제목, 결과 시각, 전투 상세, 획득 요약과 지급 아이템 순서를 확인했다. 웹 Vitest 44파일·184테스트, TypeScript typecheck, production build와 `git diff --check`를 통과했다.
- 2026-09-10 배포 전 기록 정확성 보정: `modules/battle/src/main/kotlin/com/hanjjak/battle/application/BattleHistoryService.kt`가 승리 결과의 완전 회복된 `CycleResult.remainingHp` 대신 권위 전투 타임라인에서 마지막 플레이어 피격 후 HP를 읽어 `사이클 회복 직전 남은 HP`로 저장하도록 수정했다. 패배의 플레이어 HP와 마지막 상대 HP도 같은 타임라인 기준을 유지한다. Kotlin `StageBattleServiceTest`와 모듈 컴파일이 통과했으며, Docker Desktop 기동 오류로 Testcontainers E2E는 GitLab CI 검증 대상으로 남겼다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
