---
doc_kind: task
owner_domain: delivery
task_code: 'K-06'
task_area: ''
task_type: '개발'
priority: 'P0'
target_chapters: '챕터 1, 챕터 2, 챕터 3'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/39a06c8781b08353877b0110608ec84f'
notion_id: '39a06c87-81b0-8353-877b-0110608ec84f'
snapshot_date: '2026-08-28'
---

# K-06 자동전투 HUD·전투 피드백 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [클라이언트 UX](../../../../30-domain/player/ux/ssot.md)를 따른다.

## 완료 기준

항상 자동인 전투에서 레벨·HP·EXP, 적 이름, 1~9스테이지의 현재 처치 수/20 또는 10스테이지의 최종 보스 진행, 피해, 치명타, 보스 이름·남은 제한시간을 즉시 식별할 수 있다. 공격·이동·스킬 직접 조작은 노출하지 않고 자동 진행·스테이지 반복 전환만 상단 이미지 토글로 제공한다.

## 선행 작업

E-01, E-04, K-02

## 비고

전투 화면은 실행 버튼이나 별도 AUTO 표시 없이 서버 전투 사이클을 자동 시작한다. 챕터 1은 성심당, 챕터 2는 쿠키 선물세트, 챕터 3은 스시집 배경과 5행×4프레임 몬스터 시트를 실제 메인 전투에 사용한다. 서버 논리 tick 사건에서 접근·공격·피격·사망·피해·치명타·처치 진행과 양쪽 HP를 갱신한다. 일반 구간에는 처치 수/20만 표시하고, 보스 제한시간이 설정된 전투는 보스 등장 연출이 끝난 뒤 해당 제한시간을 정수 초로 표시한다.

## 증거 링크
- 2026-09-13 보스 제한시간 표시 보정: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleHud.tsx`, `apps/web/src/features/battle/BattleHud.test.tsx`. 전체 전투 세션 완료 예정 시각을 일반 구간부터 `남은 시간`으로 표시하던 연결을 제거했다. 일반 구간은 처치 수/20만 표시하고, `bossTimeLimitTicks`가 있는 보스가 실제 등장해 인트로를 마친 동안에만 `남은 제한시간`을 표시한다. 관련 Vitest 2개 파일·21개 테스트와 TypeScript typecheck가 통과했다.
- 2026-09-13 기본공격 대상 VFX 런타임 연결: `apps/web/src/features/gems/skillVfx.tsx`, `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleScreen.css`. 서버의 `PLAYER_ATTACK_IMPACT` 사건마다 A안 `백금 교대베기`를 현재 대상의 실제 몸 중앙에 표시하고, 중복 사건 ID를 제거한 전체 기본공격 순서에 따라 우상향·좌상향을 교대한다. 일반 몬스터와 보스가 공유하는 `ChapterOneMonster` 경로에서 불투명 픽셀 너비·높이와 표시 배율로 이펙트 크기를 산출하므로 현재 메인 전투의 모든 몬스터에 같은 규칙이 적용된다. 이펙트는 240ms 동안 자체 레이어만 재생하며 몬스터 스프라이트의 위치·피격 모션은 변경하지 않는다. `skillVfx.test.ts` 회귀 테스트 16건, 웹 전체 Vitest 380건, `tsc --noEmit`, production `vite build`를 통과했다.
- 2026-09-12 일반 몬스터 교체 복원: 40% HP 이하에서 다음 몬스터를 미리 표시하던 2마리 동시 연출과 관련 승격 상태는 사용하지 않는다. 첫 일반 몬스터만 기존 접근 연출을 거치고, 이후 일반 몬스터는 직전 `ENEMY_DEFEATED` 표시 프레임에 즉시 교체해 공격 공백을 만들지 않는다. `BOSS_SPAWNED`도 마지막 일반 처치 표시보다 앞서지 않게 보정하며, 보스에만 기존 2.8초·최종 4.2초 인트로 지연을 유지한다. `autoBattleClient.test.ts`가 일반 즉시 교체, 보스 스폰 clamp와 두 보스 인트로를 검증한다.

- 2026-09-03 구현: `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`, `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/api.ts`, `apps/web/src/main.tsx`, `apps/web/src/styles.css`
- 2026-09-03 검증: `./gradlew.bat test`, `corepack pnpm --filter @hanjjak/web test`, `corepack pnpm --filter @hanjjak/web typecheck`, `corepack pnpm --filter @hanjjak/web build`, 브라우저 스모크에서 전투 탭 진입, 1-1 전투 결과 표시, 서버 챕터 자동 진행 중단/누적 결과 표시 확인.
- 2026-09-08 실행 세션 연결: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/api.ts`, `modules/battle/src/main/kotlin/com/hanjjak/battle/api/BattleController.kt`, `packages/contracts/main.tsp`; 웹 전체 Vitest 39개, typecheck, build와 game-api Kotlin 컴파일 통과.
- 2026-09-08 지속 전투 UI: `apps/web/src/features/battle/AutoBattleRuntime.tsx`, `apps/web/src/features/battle/autoBattleClient.ts`, `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/PetBattleSurface.tsx`; 실행 버튼 제거, 서버 완료 시각 기반 진행 표시와 다음 사이클 자동 시작을 연결했다.
- 2026-09-08 전투 HUD 개편: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleScreen.css`, `apps/web/src/features/battle/battlePresentation.ts`; 스테이지·체크포인트·음식 몬스터 장면·HP/EXP·접이식 획득 패널을 한 화면에 연결했다.
- 2026-09-08 브라우저 QA: 실제 전투 세션에서 일반 처치 수 증가에 따라 5·10·15 체크포인트가 순서대로 완료되고 HP·EXP·현재 스테이지 상태가 갱신되는 것을 확인했다.
- 2026-09-09 최신 `main` 재기반 후 실제 3-7 전투 세션에서 0/20부터 진행 수와 체크포인트가 다시 갱신되는 것을 확인했고, 웹 테스트 76개, typecheck와 production build가 통과했다.
- 2026-09-09 초밥집 실전 화면: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleScreen.css`, `apps/web/src/features/battle/battleVisuals.ts`, `apps/web/public/assets/chapters/chapter-01-sushi`. 로컬 브라우저 1-1과 혼합 편성 1-3에서 마스터·광어/새우초밥 교대·아침 배경, 사건 기반 피해 숫자·HP 감소·처치 증가를 확인했다. 웹 85개 테스트·타입 검사·프로덕션 빌드 통과.
- 2026-09-09 전투 가독성 개선: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleScreen.css`, `apps/web/src/features/battle/runtimeStore.ts`, `apps/web/src/features/battle/autoBattleClient.ts`. 캐릭터·적 접근 뒤 근접 전투, 일반 몬스터 50% 축소와 보스 크기 유지, 양쪽 머리 위 HP 바, 적중 피해의 몬스터 상단 표시, 실패 분석 패널을 구현했다. 로컬 1-3에서 19/20 일반 구간 사망을 재현해 실패 구간·33.3초·남은 HP 0·강화 방향 표시와 자동 재도전 중 유지까지 확인했다. 웹 87개 테스트·타입 검사·프로덕션 빌드 통과.
- 2026-09-09 타격 연출 보정: 좁은 화면의 접근 거리를 무기 끝 기준으로 재조정하고 몬스터·이름표를 위로 올려 축소했다. 공격 시작·4번째 프레임·적 HP와 직접 피해 숫자를 순서대로 재생하며, 값이 생략된 공격 시작 사건에서 적 HP가 `NaN`으로 보이지 않도록 회귀 테스트를 추가했다.
- 2026-09-09 메인 전투 연출 보정: 메인 전투에 액티브 4종 VFX를 연결하고 지속 버프 문양을 캐릭터 머리 위에 겹치지 않게 배치했다. 교전 간격은 기존보다 캐릭터 가로 폭만큼 넓히고 캐릭터 레이어를 몬스터보다 앞에 두었다. 첫 조우 뒤에는 다음 몬스터가 등장해도 캐릭터 위치를 유지한 채 `rest`로 대기하고 몬스터만 진입한다. 브라우저 1-4 실제 전투, Vitest 4개 파일·19개 테스트, typecheck와 프로덕션 빌드를 통과했다.
- 2026-09-09 전투 간격 재검수: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleScreen.css`, `apps/web/src/features/battle/battleVisuals.ts`, `apps/web/src/features/battle/battleStageEntry.regression-1.test.ts`. 실제 캐릭터 픽셀 영역에 HP·버프를 붙이고, 넓은 화면의 교전 열을 중앙 고정 폭으로 제한했으며, 지속 피해 표식을 몬스터 중앙에서 기존 대비 1/3 크기로 표시한다. 스테이지 첫 몬스터에는 `run2` 진입을 재생하고 이후 몬스터 교대에는 기존 대기를 유지한다. 로컬 1-3 브라우저에서 첫 진입과 교전 간격을 확인했고 웹 90개 테스트, typecheck와 production build를 통과했다.
- 2026-09-09 공격 위치 고정: `apps/web/src/features/battle/BattleScreen.tsx`, `apps/web/src/features/battle/BattleScreen.css`. 공격 이미지에 중복 적용되던 38px 돌진을 제거하고 `strike1`·`thrust1` 네 프레임의 발 중심을 동일한 X축 기준점에 맞췄다. 로컬 1-3 실제 공격 프레임, typecheck와 production build를 통과했다.
- 2026-09-09 지속 피해 눈 이펙트 정렬: `apps/web/src/features/battle/BattleScreen.css`. `thrust1` 네 프레임의 캐릭터 위치 보정을 눈 불꽃 레이어에도 동일하게 적용하고 프레임 전환 지연을 제거해 화면 기준 오른쪽 눈을 즉시 따라가도록 맞췄다. 로컬 1-4 실제 전투에서 네 프레임의 캐릭터·이펙트 X 변환값이 각각 일치함을 확인했고 웹 91개 테스트, typecheck와 production build를 통과했다.
- 2026-09-09 GitLab #3 HUD 동기화: `apps/web/src/features/battle/runtimeStore.ts`, `BattleScreen.tsx`, `PetBattleSurface.tsx`, `battlePresentation.ts`; 실패 코드·HP 0·부활 대기·같은 스테이지 0/20 재시작 상태를 단일 Zustand 상태에서 메인 화면과 Document PIP가 함께 구독한다.
- 2026-09-09 검증: 실패 완료 직후 공유 상태에 HP 0과 재도전 예약이 반영되고 새 세션 시작 시 최대 HP·0/20으로 초기화되는 회귀 테스트가 통과했다.
- 2026-09-09 `hyw` 로컬 후보 검증: 공격 action ID마다 마스터 렌더를 새로 시작하고 시간 기반 단발 프레임 계산을 적용해 `strike1`이 항상 1→2→3→4 순서로 한 번 재생되도록 수정했다. 공격 시트를 선로드해 프레임 교체 요청 취소를 줄였고, 직접 피해 숫자는 별도 표시 상태에서 약 0.9초 유지한다. 사용자 참고 캡처에 맞춰 넓은 화면 교전 간격도 축소했다. 실제 로컬 API·웹 전투에서 연속 10회 공격의 1→2→3→4 순서, 피해 표시 0.88~0.91초, 콘솔·HTTP 오류 없음과 Vitest 3개 파일·21개 테스트, typecheck, production build를 확인했다. 아직 `main`에는 적용하지 않은 `hyw` 검증 후보이며 기존 상태는 유지한다.
- 2026-09-09 피해 숫자 단발 보정: 동일 적의 후속 전투 사건마다 몬스터 컴포넌트가 재마운트돼 같은 피해 숫자 애니메이션이 반복되던 원인을 제거했다. 몬스터 렌더 key는 적 인덱스와 보스 여부가 바뀔 때만 변경하며, 실제 로컬 전투 MutationObserver 검증에서 같은 피해 DOM은 한 번 추가되어 약 0.9초 유지된 뒤 한 번 제거됐다. 연속 공격에서 보이는 다음 숫자는 서로 다른 `PLAYER_*_IMPACT` 사건이다. `hyw` 후보 상태와 K-06 기존 상태는 유지한다.
- 2026-09-10 코지 픽셀 HUD 조립: `BattleHud.tsx`의 동일 폭 HP·EXP 진행 바와 `BattleScreen.tsx`의 Zustand 전투 값을 연결했다. 별도 AUTO 표시는 추가하지 않았고 진행 바·수치·레벨은 기존 서버/런타임 값만 표시한다. 웹 전체 149개 테스트, typecheck, production build와 1265×712 시각 검수가 통과했다. 보스 제한시간 표시는 기존과 같이 남아 있다.
- 2026-09-10 방치 모드 직접 조작 보정: 스테이지 선택 팝오버 안에 있던 자동 진행·스테이지 반복을 상단의 독립 이미지 토글로 이동하고 기존 서버 저장 콜백을 유지했다. 정상 상태의 중복 설명 문구는 제거하되 저장 중·오류·다음 사이클 예약 안내는 유지했다. `BattleStageControls.test.tsx`를 포함한 웹 43개 파일·150개 테스트, typecheck, production build가 통과했다.
- 2026-09-10 메인 HUD API 연결 확인: 프로필은 `/api/v1/auth/session`, 스테이지 목록은 `/api/v1/stages`, 전투 기록은 `/api/v1/battle-history`를 조회한다. 자동 진행·스테이지 반복은 `PATCH /api/v1/battle/repeat-stage`, 실제 전투 사이클은 `/api/v1/battles/sessions`와 하위 heartbeat·settlements·complete·abort를 사용한다. 레거시 `/api/v1/battles/cycles`와 챕터 `/auto-run`은 운영 비활성화 경로다. HP·처치 수·레벨·EXP·쌀·체류 획득 보상은 `useBattleRuntimeStore`가 서버 세션과 완료 결과를 반영한 값을 `BattleScreen.tsx`에서 각 HUD 컴포넌트로 전달하며, 미리보기의 고정 예시 값은 메인 화면 데이터 경로에 사용하지 않는다.
- 2026-09-10 지속 버프 VFX 위치 보정: `apps/web/src/features/battle/BattleScreen.css`에서 가속·기본 공격 증폭 이펙트의 기준점을 캐릭터 스프라이트 상단의 투명 여백이 아닌 실제 젓가락 머리 바로 위로 내렸다. 캐릭터 이동·공격 모션 중에도 `master-hero` 상대 좌표를 유지한다.
- 2026-09-10 위치 보정 검증: `battle-hud-preview.html?skill-vfx=buffs`에서 두 이펙트가 캐릭터 머리 위에 따라붙는 것을 확인했고, 웹 TypeScript typecheck·production build와 `git diff --check`를 통과했다.
- 2026-09-10 보스 등장 연출·전투 공간 보정: `apps/web/src/features/battle/BattleScreen.tsx`, `BattleScreen.css`, `BattleHud.css`에서 서버 `BOSS_SPAWNED` 사건을 감지해 기존 몬스터 스프라이트와 동적 이름을 중앙 경고에 조합한다. 일반 스테이지는 붉은 오라와 `WARNING`, 각 챕터의 10단계는 금빛 문장과 `FINAL BOSS`로 구분했다. 경고 중 전투 장면만 어둡게 처리하고 HUD는 유지하며, 하단 HP·EXP와 메뉴 바를 축소하고 캐릭터·몬스터 전투선을 위로 올렸다. 1265×712 로컬 브라우저에서 일반 보스 `게살군함`, 최종 보스 `후토마키`, 기본 전투 화면을 각각 확인했다.
- 2026-09-10 기본 나무검 교체: 사용자 제공 `기본나무검.zip`의 합성 완료 20프레임을 `apps/web/public/assets/chapters/chapter-01-sushi/hero`의 `rest`, `run2`, `strike1`, `thrust1`, `death1` 전체에 적용했다. 기존 928×672 캔버스와 정렬을 유지하며, `basic-wooden-sword-manifest.json`에 제작·검증 메타데이터를 보존했다. 전투 미리보기에서 기본 자세의 나무검 표시를 확인했다.
- 2026-09-10 검증: 새 보스 경고 단위 테스트 2건을 포함한 웹 테스트에서 해당 테스트는 통과했다. 웹 typecheck와 production build가 통과했다. 전체 웹 테스트 169건 중 기존 스킬 미리보기 문구 기대값 1건은 별도 변경과 충돌해 실패했으므로 전체 검증 상태는 부분 검증으로 유지한다.
- 2026-09-10 보스 HUD 후속 조정: 등장 경고 리본에서 `BOSS APPEARS`와 챕터·스테이지 보조 문구를 제거하고, 리본의 가로 왜곡을 줄이면서 높이와 보스 이름 크기를 확대했다. 캐릭터와 보스 머리 위 HP 바는 제거하고 일반 몬스터만 머리 위 HP를 유지한다. 보스 HP는 `BattleStageProgress`의 세 조작 버튼 아래 전용 막대로 옮겨 런타임 `enemyHp/enemyMaxHp`와 연결했으며 경고가 닫힌 뒤에만 표시한다. 일반 전투·일반 경고·최종 경고·보스전 미리보기를 실제 브라우저에서 확인했고 전투 HUD 테스트 18건, typecheck와 production build를 통과했다.
- 2026-09-10 보스 경고 재생 미리보기: `battle-hud-preview.html?boss-warning=regular&animate=1`과 `boss-warning=final&animate=1`에서 캡처용 정지 프레임 대신 실제 2.8초·4.2초 애니메이션을 재생한다. 일반 보스 경고가 어두워짐과 함께 등장하고 사라진 뒤 보스 전용 HP 바로 전환되는 흐름을 로컬 브라우저에서 확인했다. 이 미리보기는 시각 전환 검수용이며 서버 전투 타임라인의 19마리 전환·10단계 입장 시 정지는 별도 구현 범위다.
- 2026-09-10 보스 경고 중앙 정렬 보정: 넓은 화면에서 미리보기의 1600px 최대 폭 때문에 경고 중심이 왼쪽으로 치우치던 제한을 제거해 전체 전투 뷰포트 중앙을 기준으로 배치했다. 보스 이름은 리본의 정중앙에 절대 배치하고 일반·최종 보스 모두 글자 크기를 한 단계 줄여 가장자리 여백을 확보했다.
- 2026-09-10 보스 이름 세로 정렬 보정: `apps/web/src/features/battle/BattleScreen.css`에서 일반·최종 보스 이름을 리본 중앙 기준 8px 아래로 이동해 글자의 시각 중심과 리본의 내부 여백을 맞췄고 최종 보스 미리보기에서 확인했다.
- 2026-09-10 사용자 픽셀 보정 배경 반영: `apps/web/public/assets/chapters/chapter-01-sushi/backgrounds`의 `morning.png`, `lunch.png`, `evening.png`, `predawn.png` 수정본 네 장을 기존 스테이지 시간대 매핑에서 직접 사용한다. 네 파일 모두 1672×940으로 일치하며 전투 HUD 미리보기와 프로덕션 빌드에서 수정된 픽셀 배경 표시를 확인했다.
- 2026-09-10 스테이지 남은 시간·버프 기준점 보정: `BattleStageProgress`의 스테이지명 옆에 서버가 계산한 현재 전투 총 재생시간에서 `logicalTickBasis` 이후의 렌더 사건 tick을 빼 남은 시간을 표시한다. 표시는 1초 미만 소수점을 버리고 0초 아래로 내려가지 않는다. 기본 나무검 합성본 928px 캔버스에서 무기를 제외한 젓가락 두 짝의 정수리 중앙(39.3%)을 `hero-crown-vfx` 기준점으로 두어 가속·기본 공격 증폭 이펙트 묶음을 중앙 정렬했다.
- 2026-09-10 스테이지 시간·정수리 앵커 검증: 웹 Vitest 44파일·184테스트, TypeScript typecheck, production build와 `git diff --check`를 통과했다. `battle-hud-preview.html?stage=stage.01-01&skill-vfx=buffs`에서 스테이지명 옆 남은 시간과 젓가락 정수리 중앙에 놓인 두 버프 문양을 1200×731 화면으로 확인했다.
- 2026-09-10 캐릭터 접지 그림자 보정: 몬스터 그림자는 유지하고 기본 나무검 캐릭터의 그림자만 8px 위로 이동해 발 위치와 그림자의 시각적 간격을 줄였다.
- 2026-09-10 1-10 보스 단독 HUD: `/api/v1/stages`의 `bossOnly`과 전투 기록의 `normalCount` 스냅샷을 사용해 메인 HUD·PIP에는 보스 진행 상태, 실패 기록에는 `진행: 보스전`을 표시한다. 메인 HUD 브라우저 미리보기에서 일반 `0/20` 미표시, 반복 비활성, 레이아웃 무초과와 콘솔 오류 없음을 확인했고 PIP·전투 기록 회귀 테스트를 추가했다.
- 2026-09-10 최종 검증: 웹 45개 파일·174개 테스트, TypeScript typecheck, production build, TypeSpec compile과 콘텐츠 검증 통과.
- 2026-09-10 메인 HUD API 연결 확인: 프로필은 `/api/v1/auth/session`, 스테이지 목록은 `/api/v1/stages`, 전투 기록은 `/api/v1/battle-history`를 조회한다. 자동 진행·스테이지 반복은 `PATCH /api/v1/battle/repeat-stage`, 메인 전투는 `POST /api/v1/battles/sessions`와 하위 settlements·complete·abort를 사용한다. HP·처치 수·레벨·EXP·쌀·체류 획득 보상은 `useBattleRuntimeStore`가 서버 세션·처치 정산·완료 결과를 반영한 값을 `BattleScreen.tsx`에서 각 HUD 컴포넌트로 전달하며, 미리보기의 고정 예시 값은 메인 화면 데이터 경로에 사용하지 않는다.
- 2026-09-10 몬스터별 즉시 반영: `AutoBattleClient`가 일반 적 `ENEMY_DEFEATED` 재생 때 정산을 직렬 요청하고 `runtimeStore`가 `settlementId`로 중복을 제거해 레벨·EXP·쌀·체류 획득 아이템을 완료 전 갱신한다. 웹 170개 테스트, typecheck와 production build가 통과했다. 실제 브라우저/API 스모크는 PostgreSQL 런타임 부재로 미실행했다.
- 2026-09-10 전투 정보·접지선 후속 보정: 하단 성장 메뉴의 선택 장식 막대를 제거하고 스테이지 남은 시간을 코랄 레드 배경·확대 수치로 강조했다. 전투 장면의 중복 몬스터 이름표는 제거하되 일반 몬스터 HP 영역과 보스 전용 HP 영역의 이름은 유지했다. 일반 몬스터만 약 20% 축소하고 일반·보스 스프라이트의 발끝을 기본 나무검 캐릭터 발끝 기준선에 맞췄다. 2-1 일반 전투와 2-4 보스 전투를 로컬 브라우저에서 확인했고 웹 185개 테스트, typecheck와 production build를 통과했다.
- 2026-09-10 몬스터 시각 앵커 보정: `apps/web/src/features/battle/battleVisuals.ts`, `BattleScreen.tsx`, `BattleScreen.css`, `BattleHud.css`에 몬스터별 실제 불투명 픽셀의 머리·발 경계를 기록했다. 일반 몬스터 HP는 각 스프라이트 머리 10px 위, 그림자는 발 중심, 대상 액티브 VFX는 실제 몸 중심을 따라가며 몬스터 크기에 맞춰 한 단계 확대된다. 보스 전용 HP·이름은 맵 명암과 관계없이 읽히도록 반투명 암갈색 명패와 밝은 테두리 위에 표시한다. 1-7·2-1 일반 전투와 2-4 보스 전투를 로컬 브라우저에서 확인했고 웹 44개 파일·186개 테스트, typecheck, production build와 `git diff --check`를 통과했다.
- 2026-09-10 챕터 3 스시집 전투 연결: `apps/web/src/features/battle/battleVisuals.ts`와 `apps/web/public/assets/chapters/chapter-03-sushi`에 스시 일반몹 7종·보스 4종, 아침·점심·저녁·새벽 배경 매핑을 연결했다. 새 ZIP의 5행×4프레임 시트를 사용하며 게살군함 이동 교체본과 발 없는 후토마끼 최종본의 마젠타 키를 투명 알파로 변환했다. 로컬 미리보기의 3-1 일반전·3-7 보스전·3-10 최종 보스 경고에서 실제 표시를 확인했고 웹 44개 파일·187개 테스트, typecheck, production build와 `git diff --check`를 통과했다.
- 2026-09-10 보스 경고 실제 픽셀 중앙 보정·전수 매핑 검증: `BattleScreen.tsx`에서 몬스터 시트의 투명 캔버스가 아니라 첫 프레임의 실제 불투명 픽셀 경계를 계산해 문장 원형의 중앙에 맞췄다. `BattleScreen.css`는 몬스터를 원형 포트레이트로 자른 아래 레이어에 두고 금색 문장·오라를 위에 덮으며, `WARNING`·`FINAL BOSS` 제목은 포트레이트와 겹치지 않는 독립 상단 영역으로 분리한다. 3-10 최종 보스 미리보기에서 경고 영역과 연출 묶음의 X/Y 중심 오차 0px 및 화면 내 비잘림을 확인했다. 챕터 1~3의 30개 스테이지 편성과 12개 보스 경고 이름·일반/최종 구분을 회귀 테스트로 고정했으며 웹 44개 파일·227개 테스트, typecheck, content validation, production build가 통과했다.
- 2026-09-10 최종 보스 포트레이트 확대·10스테이지 단독 보스 보정: 가로로 넓은 보스는 원 안에서 양옆 몸체를 자연스럽게 크롭하고 얼굴이 더 크게 보이도록 실제 픽셀 종횡비 기반 확대를 적용했다. 1-10 딸기 시루, 2-10 로열 어소트먼트 선물 골렘, 3-10 후토마끼 왕의 경고 화면을 로컬 브라우저에서 확인했다. 10스테이지 HUD·패배 기록은 `0/20` 대신 `최종 보스`·`일반 몬스터 없음`으로 표시한다. 콘텐츠 검증, 웹 44개 파일·228개 테스트, typecheck, production build와 Kotlin `StageBattleServiceTest`가 통과했다.
- 2026-09-10 보스 얼굴·전투 VFX 최종 보정: 보스 경고 포트레이트는 실제 불투명 픽셀 맞춤 배율에 최소 1.3배, 가로형 최대 1.7배 확대를 적용해 원형 문장 안을 얼굴이 확실히 채우도록 했다. 메인 전투와 보석 던전에는 확정 액티브 4종의 공통 8프레임 런타임 매핑을 연결했고, 큰 몬스터 원본 캔버스에서도 HP·그림자·피해·대상 VFX가 `monster-visual-anchor`의 실제 몸 중심을 공유하도록 수정했다. 로컬 3-10 최종 보스 경고와 2-4 강타·마! 쫄이나·두 버프 미리보기를 확인했으며 웹 Vitest 44개 파일·227개 테스트, typecheck, production build, 네 VFX 에셋 HTTP 200, 새 브라우저 세션 warning/error 0개를 확인했다. 세부 스킬 증거는 [H-05](../H-skills/h-05-skill-combat-validation.md)를 따른다.
- 2026-09-10 남은 시간 강조 검증: `BattleSessionStarted.durationMilliseconds`에서 현재 논리 tick 경과분을 차감해 정수 초로 내림한 `남은 시간`을 표시하고, 진한 코랄 배경·밝은 글자·18px 굵은 수치로 대비를 높였다. 748px 폭의 2-4 전투 미리보기에서 `남은 시간 21초`가 진행 바·상단 메뉴와 겹치지 않는 것을 확인했다. 웹 Vitest 44개 파일·228개 테스트, TypeScript typecheck와 production build를 통과했다.
- 2026-09-10 `화력 최대로!` 런타임 시트 보정: 2172×724 확정 합본을 CSS 정사각 셀에 직접 맞추며 가로·세로 배율이 달라지던 문제를 4096×512(512×512 8셀) 투명 시트 재패킹으로 해결했다. 메인 전투와 보석 던전은 기존 공통 `skillVfx.tsx` 매핑과 서버 사건 타임라인을 그대로 사용한다. 웹 Vitest 44개 파일·230개 테스트, TypeScript typecheck, production build 및 2-4 `skill-vfx=all` 브라우저 시각 검수를 통과했다. 세부 증거는 [H-05](../H-skills/h-05-skill-combat-validation.md)를 따른다.
- 2026-09-10 지속 버프 외곽 정렬 후속: 실제 불투명 픽셀 크기를 비교해 `화력 최대로!`를 약 12% 확대하고 6% 위로 올렸다. 메인 전투와 보석 던전 모두 두 버프의 시각적 아래 기준선과 전체 외곽 크기가 비슷하게 보이도록 동일한 오버라이드를 적용했다. 관련 Vitest 3개 파일·46개 테스트, TypeScript typecheck, production build와 2-4 브라우저 시각 검수를 통과했다.
- 2026-09-10 로컬 API 연결 점검: Vite `/api` 프록시는 `http://127.0.0.1:8080`을 향하고, 메인 전투는 `/api/v1/battles/sessions` 계열의 `CombatRenderingEvent`, 보석 던전은 `/api/v1/gem-dungeons` 결과 사건을 공통 VFX 매핑에 전달하고 있음을 확인했다. API 클라이언트 포함 관련 회귀 테스트는 통과했다. 다만 이 작업 환경에서는 PostgreSQL과 game-api가 미기동 상태이며 Docker Desktop 서비스 시작이 Windows 권한으로 거부되어, 실제 4175 프록시 호출은 502인 상태다. 따라서 코드 연결은 확인했지만 로컬 실서버 왕복 검증은 Docker/PostgreSQL 기동 후 남은 검증으로 유지한다.
- 2026-09-10 배포 전 회귀 보강: `BattleScreen.bossWarning.test.tsx`에서 서버 `BOSS_SPAWNED` 사건 뒤 일반 보스 경고가 2.8초, 최종 보스 경고가 4.2초 후 한 번만 해제되는 흐름을 검증했다. `battleVisuals.test.ts`는 챕터 1~3의 30개 스테이지가 참조하는 11개 배경과 30개 몬스터 PNG·manifest의 존재, 4×5 배열, 20프레임, 셀·시트 치수를 검사한다. 웹 Vitest 48파일·248테스트, TypeScript typecheck, production build와 `git diff --check`를 통과했다.
- 2026-09-10 배포 CI 전투 기록 보정: `BattleSessionService`가 완료 전투의 실제 `CombatRenderingEvent` 타임라인을 `BattleHistoryService`에 전달하도록 변경했다. 기록 계층의 재시뮬레이션을 제거하고 패배 HP는 완료 결과를 권위값으로 사용해, 입력과 결과가 독립적으로 주어지는 E2E에서도 남은 HP 0을 보존한다. `:modules:battle:test`와 `:apps:game-api:compileTestKotlin`이 통과했다.
- 2026-09-11 전투 화면 후속 보정: `BattleScreen.tsx`, `BattleScreen.css`, `battleVisuals.ts`, `BattleHud.tsx`, `BattleHud.css`, `ChatPanel.css`에서 피해 숫자를 몬스터 몸체 가까이 내려 보스 HP 영역과의 겹침을 제거하고, 일반·최종 보스 경고 포트레이트를 실제 프레임별 얼굴 기준점으로 원 중앙에 맞췄다. 채팅은 좌측 하단으로 이동했으며 코스튬 동선은 하단 보석 메뉴에서 제거하고 상단 던전 왼쪽의 동일 형식 버튼으로 옮겼다. 챕터 1~3 대표 보스 5종과 강타 미리보기를 브라우저에서 확인했다.
- 2026-09-11 검증: 웹 Vitest 50개 파일·288개 테스트, TypeScript typecheck와 Vite production build를 통과했다. 로컬 API 미기동에 따른 테스트 연결 거부 로그는 있었으나 실패는 없었다.
- 2026-09-11 보스 인트로 전투 재생 정지: `apps/web/src/features/battle/autoBattleClient.ts`의 사건 재생 큐가 `BOSS_SPAWNED` 뒤 일반 보스 2.8초·최종 보스 4.2초 동안 후속 공격·피격·정산 사건을 보류하도록 수정했다. `BattleScreen.tsx`의 인트로 해제 타이머도 같은 시간 함수를 사용해 화면이 닫히는 순간부터 전투 재생이 이어진다. 이는 클라이언트 표현 순서만 조정하며 서버가 이미 확정한 논리 tick·피해·결과는 변경하지 않는다.
- 2026-09-11 하단 배치·검증: `apps/web/src/features/chat/ChatPanel.css`에서 넓은 화면 채팅창 하단을 성장 메뉴와 같은 14px 기준선에 맞췄고, 전체 폭 하단 메뉴를 사용하는 720px 이하 배치는 기존 상단 여유를 유지했다. 일반·최종 인트로 동안 후속 사건이 각각 2.8초·4.2초 이전에는 전달되지 않는 회귀 테스트를 추가했다. 웹 Vitest 50개 파일·291개 테스트, TypeScript typecheck, Vite production build와 `battle-hud-preview.html?stage=stage.02-04&boss-warning=regular&animate=1`의 인트로 종료 전후 화면 검수를 통과했다. 로컬 API 미기동에 따른 테스트 연결 거부 로그는 있었으나 실패는 없었다.
- 2026-09-11 배포 캐시·모드 토글 후속: 운영 `/assets/`가 `Cache-Control: public, max-age=31536000, immutable`인 상태에서 기존 나무검 파일을 같은 URL로 교체해 배포 브라우저가 구 프레임을 재사용할 수 있음을 확인했다. 메인 전투의 모든 영웅 프레임에 `basic-wooden-sword-v1-20260911` 버전 쿼리를 붙여 새 요청을 보장했다. 자동 진행·스테이지 반복은 서버가 다음 사이클 적용을 반환한 즉시 `pendingIdleMode`를 버튼의 선택 상태에 반영해 첫 클릭 결과를 보이게 했다. 보스 인트로 중에는 후속 논리 tick과 HUD 남은 시간이 일반 2.8초·최종 4.2초 동안 함께 고정되고, 인트로 이후에만 감소하는 회귀 테스트를 추가했다. 웹 Vitest 50개 파일·293개 테스트, TypeScript typecheck, Vite production build와 `git diff --check`를 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
