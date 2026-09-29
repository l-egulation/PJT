---
doc_kind: task
owner_domain: delivery
task_code: 'K-18'
task_area: ''
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/2a506c8781b0823c9957815bdd3f380a'
notion_id: '2a506c87-81b0-823c-9957-815bdd3f380a'
snapshot_date: '2026-08-28'
---

# K-18 단일 탭 펫 UI·관리 UI 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

단일 브라우저 탭·단일 문서에서 펫 UI와 관리 UI를 전환한다. 지원 브라우저의 PIP 선택 기능과 미지원·거부 시 단일 탭 대체 UI를 제공하며, 트레이·자동 시작·항상 위·클릭 통과·창 위치 복원은 제공하지 않는다.

## 선행 작업

B-06, K-02, K-03

## 비고

한짝 설계 브리핑 v2와 확정 A안 레이아웃을 반영했다. 지원 브라우저에서 Document Picture-in-Picture 포털로 전투·정산 요약을 표시하고 미지원·거부 시 인라인 표시를 유지한다. PIP 열기·닫기·로그아웃 정리, 단일 store 공유, 확정 프레임·12프레임 캐릭터 자산, 서버 확정 클리어·지급 수량 누적은 구현했다. K-19의 실제 백그라운드·discard·종료·절전·네트워크 중단 검증은 남아 있다.

## 증거 링크

- 2026-09-08 [웹 Picture-in-Picture 요구사항과 구현 계획](../../../../70-plans/picture-in-picture/requirements.md)
- 2026-09-08 웹 구현: `apps/web/src/features/picture-in-picture`, `apps/web/src/features/battle/PetBattleSurface.tsx`, `apps/web/src/features/battle/runtimeStore.ts`, `apps/web/src/main.tsx`, `apps/web/src/styles.css`
- 2026-09-08 자동 검증: `corepack pnpm --filter @hanjjak/web test` 9 files·24 tests 통과, `typecheck`, `build` 통과.
- 2026-09-08 Chromium 브라우저 스모크: 실제 Document PIP 창 생성, PIP 포털 단독 렌더링, 전투 결과 동기화, 브라우저 닫기 후 인라인 복귀, 로그아웃 시 PIP 종료·보호 화면 제거 확인. API 미지원 환경에서 비활성 컨트롤·이유 안내·인라인 대체 표시 확인.
- 2026-09-10 HUD 진입 조작 개선: `apps/web/src/features/picture-in-picture/PictureInPictureController.tsx`, `apps/web/src/app-shell.css`, `apps/web/src/features/battle/BattleHud.css`, `apps/web/src/features/battle/battle-hud-preview.tsx`에서 큰 텍스트 버튼을 관련 아이콘과 짧은 상태명이 있는 소형 종이 버튼으로 교체하고 우측 상단 관리 내비게이션과 획득 패널 사이에 배치했다. 전체 동작명은 접근성 이름과 툴팁에 유지했다.
- 2026-09-10 HUD 정렬 후속 보정: 화면 폭별 `한켠` 버튼 크기와 오른쪽 여백을 상단 관리 내비게이션의 마지막 버튼에 맞추고, 두 버튼 행 사이의 세로 간격을 축소했다. 일반 전투 미리보기에서 오른쪽 끝선과 획득 패널 비중첩을 확인했다.
- 2026-09-10 자동 검증: `pnpm --filter @hanjjak/web typecheck`, `pnpm --filter @hanjjak/web test` 44 files·174 tests, `pnpm --filter @hanjjak/web build` 통과. 빌드의 기존 500 kB 초과 번들 경고는 유지된다.
- 2026-09-13 확정 A안 구현: `PetBattleSurface.tsx`·`PetBattleSurface.css`, `pictureInPictureSessionStore.ts`, `trackedItems.ts`, `AutoBattleRuntime.tsx`, `PictureInPictureController.tsx`와 `features/picture-in-picture/assets`에 고정 비율 프레임, 무이펙트 12프레임 전투, 서버 확정 쌀·클리어·정산 수량, 최대 4개 로컬 추적 설정을 연결했다.
- 2026-09-13 반응형 브라우저 검증: 폭 `300/420/560px`에서 가로 넘침 `0px`, 글자 겹침 없음. `300px`에서는 등급 칩 숨김을 확인했다. 설정 전후 표면 높이는 `524.8125px`로 동일했고 설정 목록 `scrollHeight 1123px > clientHeight 198px`, 5번째 선택 뒤에도 `4/4` 유지와 안내 문구 표시를 확인했다. 500ms 프레임 비교에서 프레임 아트와 전투 개구부 좌표·배경 위치가 동일해 카메라·배경 이동이 없음을 확인했다.
- 2026-09-13 12프레임 접지 검증: 실제 Chromium 구현 화면에서 F1~F12를 각각 정지해 역보정 후 접지 앵커를 기준 프레임으로 환산했다. 요리사와 몬스터 모두 모든 프레임에서 y `454.9765px`(확정 좌표 `455px`의 서브픽셀 환산값)였으며 최대-최소 편차는 각각 `0px`였다.
- 2026-09-13 자동 검증: 최신 `origin/main` 통합 후 웹 전체 75 files·434 tests, `typecheck`, `build` 통과. `PictureInPictureController.test.tsx`에서 열기 성공·pagehide·수동 복귀·열기 실패에 따른 세션 카운터 수명주기를, `AutoBattleRuntime.pip.test.tsx`에서 서버 확정 정산·완료 콜백과 PiP 누적 store 연결을 검증했다. 빌드의 기존 500 kB 초과 번들 경고는 유지된다.
- 2026-09-13 프레임 외곽 투명화: `PetBattleSurface.css`의 PIP 스테이지와 Document PIP 문서 루트에서 짙은 단색 배경 및 12px 패딩을 제거했다. 나무 프레임과 내부 전투·장부 레이어는 유지했으며 `pip-preview.html`의 체크 패턴이 프레임 알파 외곽으로 드러나는 것을 확인했다. 최신 `origin/main` 통합 후 웹 전체 Vitest 75개 파일·434개 테스트가 통과했고 로컬 API 3000 미실행에 따른 `ECONNREFUSED` 로그는 기존 모의 API fallback 이후 결과에 영향을 주지 않았다.
- 2026-09-13 플로팅 오버레이 재구성: `PetBattleSurface`의 상시 어두운 카드·텍스트·별도 정보 버튼을 제거하고, 투명 배경의 착용 외형 캐릭터 자체를 열기 조작으로 사용했다. 선택 시 캐릭터 위에 닉네임·전투 상태·스테이지·HP·레벨·경험치·쌀·최근 진행 정보창을 표시하며 같은 캐릭터 선택·닫기·`Esc`로 접는다. PIP 문서의 `html`·`body` 배경도 투명 처리하고 낮은 창 높이에서 캐릭터와 정보창을 함께 축소한다. `PetBattleSurface`·Document PIP adapter 2 files·6 tests, 웹 typecheck와 프로덕션 build가 통과했다. 로컬 API `localhost:3000` 미기동 로그는 query 실패 fallback이며 테스트 결과에는 영향을 주지 않았다. 실제 운영 브라우저의 PIP 투명 합성·리사이즈 시각 검증은 남아 있어 상태는 `부분 검증`으로 유지한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
