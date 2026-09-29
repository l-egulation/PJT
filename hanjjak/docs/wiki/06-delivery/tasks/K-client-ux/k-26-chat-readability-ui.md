---
doc_kind: task
owner_domain: delivery
task_code: 'K-26'
task_area: 'K 클라이언트 UI·UX'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
source: '2026-09-12-user-request'
created_at: '2026-09-12'
---

# K-26 광장 채팅 가독성 UI 보정

> 작업 상태와 완료 증거의 SSOT. 화면 규칙은 [클라이언트 UX SSOT](../../../../30-domain/player/ux/ssot.md), 전문 표시명은 [주력 재료 SSOT](../../../../30-domain/character/ssot.md)를 따른다.

## 완료 기준

- 메시지 사이의 세로 간격과 구분을 넓혀 연속된 채팅을 쉽게 식별한다.
- 시각을 오전·오후 표기 없이 24시간제 `HH:mm` 형식으로 표시한다.
- 차단 목록 진입을 역할과 펼침 상태가 명확한 `차단 관리` 버튼으로 제공한다.
- 전문 아이콘을 추가하지 않고 감자·고구마·옥수수 전문에 따라 닉네임을 각각 갈색·적보라색·노란색으로 표시한다.
- 닉네임 텍스트를 기존 크기의 1.5배로 확대하고, 나라 선택 화면의 재료 이름에도 같은 전문 색상을 적용한다.
- 조회·전송·WebSocket 메시지 계약에 주력 재료 유형을 포함하고, 색상 외 접근성 이름으로도 전문을 식별한다.

## 선행 작업

C-04, K-03

## 증거 링크

- 웹 구현: `apps/web/src/features/chat/ChatPanel.tsx`, `apps/web/src/features/chat/ChatPanel.css`, `apps/web/src/features/chat/api.ts`
- 서버·계약 구현: `modules/chat/src/main/kotlin/com/hanjjak/chat`, `packages/contracts/main.tsp`, `packages/contracts/generated/openapi/openapi.yaml`
- 2026-09-12 자동 검증: 채팅 전용 Vitest 3개 파일·10개 테스트, 웹 TypeScript typecheck, TypeSpec OpenAPI 생성, 웹 production build 통과.
- 2026-09-12 시각 검증: `battle-hud-preview.html`을 로컬 `5173`에서 렌더링해 메시지 간 여백·옅은 구분선, 24시간제 시각, 감자·고구마·옥수수 닉네임 색상, 상단 `차단 관리` 버튼을 확인했다.
- 2026-09-12 후속 보정: 데스크톱 닉네임을 10px에서 15px로 확대하고 모바일 닉네임을 8px에서 12px로 확대했다. 전문 색상을 공용 CSS 변수로 승격해 나라 선택 화면의 `감자`, `고구마`, `옥수수` 텍스트와 채팅 닉네임이 같은 색상을 사용하도록 연결했다.
- 2026-09-12 후속 검증: 채팅·나라 선택 전용 Vitest 4개 파일·14개 테스트, 웹 TypeScript typecheck와 production build를 통과했다. 로컬 `5173` 전투 HUD에서 확대된 닉네임의 크기·줄바꿈과 전문별 색상을 다시 확인했다.
- 2026-09-12 색상 대비 후속: 기존 옥수수 황갈색 `#a56f00`을 감자에 적용하고 옥수수는 더 밝고 노란 `#ad8500`으로 분리했다. 재료별 표현을 동일하게 유지하기 위해 옥수수에만 적용됐던 글자 그림자는 제거했다.
- 2026-09-12 색상 대비 시각 검증: 로컬 `5173` 전투 HUD의 동일한 채팅 목록에서 감자 황갈색과 옥수수 황금노랑이 서로 구분되고, 그림자 없는 옥수수 닉네임이 밝은 종이색 배경에서도 읽히는 것을 확인했다.
- 2026-09-12 사용자 이미지 팔레트 반영: 첨부 이미지의 글자 내부 대표색을 추출해 감자 `#E4AD56`, 고구마 `#982A59`, 옥수수 `#F0C712`를 공용 토큰에 적용했다. 채팅 닉네임과 나라 선택 화면이 같은 팔레트를 사용한다.
- 2026-09-12 사용자 이미지 팔레트 시각 검증: 로컬 `5173` 전투 HUD의 채팅 목록에서 세 전문의 추출색과 그림자 미적용 상태를 확인했다.
- 2026-09-12 팔레트 탐색: 고구마 `#982A59`를 고정하고 감자·옥수수의 명도와 색상 범위를 넓힌 후보 5개를 실제 채팅 형태로 비교하는 `apps/web/chat-color-palette-preview.html`을 추가했다. 사용자가 후보를 선택하기 전까지 실제 공용 색상 토큰은 변경하지 않는다.
- 서버 채팅 모듈은 main/test Kotlin 컴파일을 통과했으나 Gradle 테스트 실행기가 컴파일된 테스트 클래스를 찾지 못하는 기존 환경 문제로 테스트 완료 판정은 보류한다.
- 2026-09-13 후속 UX: 새 메시지(WebSocket·조회·자신의 전송 결과)가 등록되어도 사용자가 채팅 목록 최하단을 보고 있을 때만 맨 아래로 자동 이동하고, 과거 메시지를 읽기 위해 위로 스크롤한 상태에서는 현재 위치를 유지하도록 `ChatPanel`의 하단 고정 추적을 보정했다.
- 2026-09-13 후속 회귀 검증: `ChatPanel.interactions.regression.test.tsx`에 30개 메시지 상한으로 목록 길이가 유지되는 새 메시지 시나리오를 추가해 하단 고정 시 이동, 비하단 시 위치 유지를 검증했다. 의존성 설치 후 해당 회귀 테스트 5개, 채팅 API·컴포넌트·프레임 테스트 10개, 웹 typecheck와 production build를 통과했다. 채팅 컴포넌트 테스트 중 백엔드가 없는 환경에서 발생한 `localhost:3000` 연결 거부 로그는 기존 비차단 로그다. `battle-hud-preview.html`을 로컬 `5173`에서 열어 채팅 패널·메시지 6개·입력창 렌더링도 확인했다.
