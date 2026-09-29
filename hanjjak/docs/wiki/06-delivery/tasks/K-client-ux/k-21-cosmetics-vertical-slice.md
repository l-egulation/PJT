---
doc_kind: task
owner_domain: delivery
task_code: 'K-21'
task_area: 'K 클라이언트 UI·UX'
task_type: '개발'
priority: 'P0'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
source: 'repository-implementation-sync'
created_at: '2026-09-02'
---

# K-21 치장 뽑기·도감 수직 슬라이스 구현

> 작업 상태와 완료 증거의 SSOT. 규칙은 [치장 SSOT](../../../../30-domain/cosmetics/ssot.md), 구현 범위는 [치장 뽑기 요구사항](../../../../70-plans/cosmetic-gacha/requirements.md)을 따른다.

## 완료 기준

승인된 치장 콘텐츠, 서버 저장·API, 전투 입력 연결과 웹 획득·등록·도감·착용 흐름을 구현하고 요구사항 추적성에 맞는 통합·UI 검증 증거를 남긴다.

## 선행 작업

B-01, B-02, D-07, E-02

## 비고

66종 번호형 치장·11세트 콘텐츠와 스키마, 서버 권한 보유 상태, 전설 상시 배너 1개와 추첨·결제, 신규 자동 등록·중복 재고, 등급별 성급·도감·세트 효과, 200회 마일스톤·선택 상자, 6부위 착용 API와 웹 화면을 구현했다. 미등록 중복 거래, 정식 battle session의 다음 사이클 스냅샷, 상세 감사 필드, 실제 명칭·이미지·외형 렌더러와 실DB·실브라우저 검증은 남아 있다.

2026-09-08 선택 상자 이관 브랜치를 최신 main과 통합했다. 마일스톤 상자 수량 조회도 인벤토리를 사용하도록 변경했다. 상자 이관과 검증 상태는 [F-01](../F-items-inventory/f-01-item-classification-tradeability.md)이 소유한다.

## 증거 링크
- 2026-09-12 전투 외형 적용 범위: 메인 전투와 보석 던전은 공용 `PlayerBattleCharacter`로 계정의 현재 6부위 착용 ID를 읽는다. 완성 전투 프레임이 있는 완전한 지원 세트만 해당 프레임을 표시하고, 현재 콘텐츠의 nullable 이미지·부분 착용·혼합·미착용은 기본 캐릭터 fallback을 사용한다. 고정 천사 테스트 세트로 대체하지 않는다. 실제 적용 카탈로그의 완성 프레임 지원은 `cosmetic-061`~`066` 거북이 수호자 완전 세트이며, 그 밖의 제공 프레임은 정적 검수 전용 ID에만 연결된다. 테스트는 완전 세트·다른 지원 세트·부분 착용·미착용과 천사 DOM 부재를 포함한다.

- 2026-09-08 V19 기존 데이터에서 V20 이관 경계 테스트 5건과 상자 통합·카탈로그 테스트 3건 통과. 범위와 기존 V15 적용 DB의 제한은 [F-01](../F-items-inventory/f-01-item-classification-tradeability.md)에 기록했다.
- 2026-09-08 별도 로컬 PostgreSQL에서 실제 HTTP 상자 수령·조회·개봉 및 중복 요청 검증 성공. 조건 준비와 검증 한계는 [F-01](../F-items-inventory/f-01-item-classification-tradeability.md)에 기록했다. 전체 치장 UI·플레이 흐름의 검증 완료를 의미하지 않는다.
- 2026-09-07 66종·11세트 clean cutover와 캐릭터 창·마일스톤·선택 상자 변경: [K-22](./k-22-character-window.md)
- [구현 증거](../../../../70-plans/character-cosmetics-collection/implementation-evidence.md)
- 서버·저장·API: `modules/cosmetics`, `apps/game-api/src/main/resources/db/migration/V3__cosmetics.sql`, `V4__cosmetic_audit.sql`
- 웹: `apps/web/src/features/cosmetics`, `apps/web/src/features/character`
- 계약·콘텐츠: `packages/contracts/cosmetics.tsp`, `packages/game-content/versions/v1/cosmetics/cosmetics.json`
- 상자 저장: `apps/game-api/src/main/resources/db/migration/V22__cosmetic_selector_box_to_inventory.sql`, `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/JdbcCosmeticsWallet.kt`
- 2026-09-02 `pnpm build`, `pnpm test`, `./gradlew.bat test`, `pnpm content:validate` 통과. PostgreSQL 통합·브라우저 시나리오는 이번 동기화에서 실행하지 않았다.
- 2026-09-04 `./gradlew :modules:inventory:test :modules:market:test :modules:cosmetics:test`와 `./gradlew :apps:game-api:build -x test` 성공. 마이그레이션 실행과 상자 수령·개봉 종단 검증은 실행하지 않았다.
- 2026-09-07 콘텐츠 테스트 3건, 웹 테스트 30건, TypeSpec 생성, 웹 typecheck·build, `:modules:cosmetics:test :apps:game-api:test :packages:sim-core:test` 성공. PostgreSQL·실제 HTTP·실제 Chromium·동시 요청 검증은 실행하지 않았다.

### 2026-09-10 main 기반 cosmetics web rebuild 현재 증거

- 최신 `origin/main`(`5070d6b3`) 위로 rebase한 구현 반영 커밋은 Task 1 `5ca3b8b6`(navigation), Task 2 `86c3519a`/`9e982a1e`(client contract·실제 서버 오류 매핑), Task 3 `52daa65b`(cosmetics web source 최종 review-fix series), Task 4 `1537c60b`(character entry 보존 테스트), 통합 수정 `645d05ac`(battle/shared 직접 진입·잠금 게이트), `fffc8cb4`(static preview 직접 진입), 후속 수정 `c5094064`/`f71fc440`/`ea5ac45b`(동기식 명령 직렬화·결정적 오류 실제 재조회·오류 refetch 중 명령 차단), 최종 navigation follow-up `392f30fe`/`59b7ddd5`/`445cdff0`/`f1c22bf5`(shared·legacy header·active cosmetics menu·CharacterWindow 재진입 origin 보존), 최종 MR blocker 수정 `010670e1`(draw-time outage·확률 상세 retry)다.
- 실제 변경 경로는 `apps/web/src/main.tsx`, `apps/web/src/navigationState.ts`, `apps/web/src/navigationState.test.ts`, `apps/web/src/features/battle/BattleHud.tsx`, `apps/web/src/features/battle/BattleHud.css`, `apps/web/src/features/battle/BattleStageControls.test.tsx`, `apps/web/src/features/battle/battle-hud-preview.tsx`, `apps/web/src/features/battle/battle-hud-preview.test.tsx`, `apps/web/src/features/character/CosmeticPanels.test.tsx`, `apps/web/src/features/cosmetics/api.ts`, `apps/web/src/features/cosmetics/api.test.ts`, `apps/web/src/features/cosmetics/presentation.ts`, `apps/web/src/features/cosmetics/presentation.test.ts`, `apps/web/src/features/cosmetics/CosmeticsScreen.tsx`, `apps/web/src/features/cosmetics/CosmeticsScreen.test.tsx`, `apps/web/src/features/cosmetics/CosmeticGachaBoard.tsx`, `apps/web/src/features/cosmetics/CosmeticGachaBoard.test.tsx`, `apps/web/src/features/cosmetics/CosmeticProbabilityDialog.tsx`, `apps/web/src/features/cosmetics/CosmeticDrawReveal.tsx`, `apps/web/src/features/cosmetics/CosmeticManagementPanel.tsx`, `apps/web/src/features/cosmetics/cosmetics.css`, `docs/superpowers/specs/2026-09-10-cosmetic-gacha-main-rebuild-design.md`, `docs/superpowers/plans/2026-09-10-cosmetic-gacha-main-rebuild.md`, `docs/wiki/06-delivery/tasks/K-client-ux/k-21-cosmetics-vertical-slice.md`다.
- 최종 source는 웹 상단·battle/shared 성장 메뉴의 `치장 뽑기` 직접 진입, 기존 캐릭터 창 진입, 배너·상세·확률·뽑기 전 확인·결과·도감 관리, 뽑기 영역 장애 격리, 불확실한 요청의 동일 요청 재확인 보존, static preview 직접 진입, 동기식 명령 직렬화와 결정적 오류 후 실제 상태 재조회, 반응형 CSS를 포함한다. 이는 구현 증거이며 규칙·정책 SSOT를 재정의하지 않는다.
- `corepack pnpm --filter @hanjjak/web exec vitest run src/navigationState.test.ts src/features/battle/BattleStageControls.test.tsx` — 2개 파일, 16개 테스트 통과.
- `corepack pnpm --filter @hanjjak/web exec vitest run src/features/cosmetics/api.test.ts src/features/cosmetics/presentation.test.ts` — 2개 파일, 8개 테스트 통과.
- `corepack pnpm --filter @hanjjak/web exec vitest run src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx` — 2개 파일, 20개 테스트 통과.
- `corepack pnpm --filter @hanjjak/web exec vitest run src/features/character/CharacterWindow.test.tsx src/features/character/CosmeticPanels.test.tsx` — 2개 파일, 3개 테스트 통과.
- `corepack pnpm --filter @hanjjak/web exec vitest run src/features/battle/BattleStageControls.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx` — 2개 파일, 21개 테스트 통과.
- `corepack pnpm --filter @hanjjak/web exec vitest run src/features/battle/battle-hud-preview.test.tsx` — 1개 파일, 11개 테스트 통과.
- `corepack pnpm --filter @hanjjak/web typecheck` — `tsc --noEmit` 성공.
- 최신 rebase 소스에서 `corepack pnpm --filter @hanjjak/web test` — 49개 파일, 216개 테스트 통과. 테스트 중 로컬 API proxy `ECONNREFUSED 127.0.0.1:3000` 진단 로그가 발생했지만 실패 테스트는 없었다.
- 최신 rebase 소스에서 `corepack pnpm --filter @hanjjak/web build` — Vite production build 성공, 228개 모듈 변환.
- 최신 rebase 소스에서 `corepack pnpm --filter @hanjjak/contracts build` — TypeSpec/OpenAPI 생성 성공.
- 최신 rebase 소스에서 `corepack pnpm --filter @hanjjak/content-validator validate` — 콘텐츠 검증 성공.
- 최신 final-source Chromium preview smoke를 1280×720에서 실행했다. local Vite 앱의 `battle-hud-preview`에서 `치장 뽑기` 클릭 후 `치장 뽑기 미리보기`가 렌더링됐고 console error는 없었다. 해당 preview에서는 `/api/v1/cosmetic*` 요청이 발생하지 않았으며 `document.documentElement.scrollWidth <= window.innerWidth`가 `true`였다.
- production/authenticated `CosmeticsScreen`의 배너·확률·뽑기·캐릭터 복귀 흐름은 local `game-api`의 `/api/v1/auth/session` 502로 인증 단계가 중단되어 Chromium에서 실행하지 않았다.
- PostgreSQL migration·Spring HTTP 종단·동시 요청 검증은 실행하지 않았다. 검증 상태는 `부분 검증`으로 유지한다.
- 2026-09-11 캐릭터 창의 치장 보관함 화면은 기존 치장 조회·미리보기·착용 계약을 유지한 채 아늑한 픽셀 UI로 재구성했다. 구현·시각 검수 증거와 실제 이미지·외형 합성의 남은 범위는 [K-22](./k-22-character-window.md)에 기록했다.

### 2026-09-11 치장 뽑기·보관함 cozy-pixel UI 개선 증거

- 기존 뽑기·확률 상세·결과 확인·마일스톤 상자·보관함 등록·착용 명령 계약은 변경하지 않고 `apps/web/src/features/cosmetics`의 화면 계층과 시각 표현만 재구성했다.
- 캐릭터·장비 화면과 같은 `modal-paper-decorated-v2.png`, `primary-button-9slice.png`, 캐릭터 대기 이미지, 쌀·뽑기권 이미지를 재사용했다. 서버 카탈로그에 실제 치장 이미지가 없는 항목은 콘텐츠 정본의 미확정 상태를 숨기지 않고 공용 플레이스홀더를 유지한다.
- production 치장 화면을 공용 성장 HUD의 콘텐츠 영역 안에 배치하고, `battle-hud-preview`도 별도 샘플 마크업 대신 실제 `CosmeticGachaBoard`·`CosmeticManagementPanel` 컴포넌트를 정적 데이터로 렌더링하도록 맞췄다.
- 1280×720 local Vite preview에서 종이 모달, 보유 재화, 배너, 뽑기 버튼과 고정 성장 메뉴의 시각 계층을 확인했다. 정적 preview는 실제 API 명령을 보내지 않는다.
- `corepack pnpm --filter @hanjjak/web test` — 51개 파일, 302개 테스트 통과. 로컬 API proxy의 `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `corepack pnpm --filter @hanjjak/web typecheck` — `tsc --noEmit` 성공.
- `corepack pnpm --filter @hanjjak/web build` — Vite production build 성공, 249개 모듈 변환. 기존 번들 크기 경고는 남아 있다.
- 실제 치장 명칭·이미지·외형 렌더러 및 인증된 game-api 종단 시나리오는 남아 있으므로 `부분 검증`을 유지한다.

### 2026-09-11 치장 뽑기 전체화면·결과 연출 후속 증거

- 치장 뽑기를 랭킹과 동일한 공용 HUD 전체화면 경로로 변경했다. 기존 종이 모달 테두리를 제거하고 밝은 종이 질감을 화면 전체에 적용했으며, 상·하단 HUD 사이의 독립 스크롤 영역에서 배너와 보관함을 제공한다.
- 보유 쌀 표시는 장비 화면의 `currency-rice-gold-v2.png`로 통일했다. 치장 뽑기권은 기존 픽셀 에셋 화풍을 참고해 생성한 256×256 투명 ARGB `apps/web/src/features/cosmetics/assets-cozy-pixel/cosmetic-draw-ticket-v1.png`를 사용한다.
- 서버 뽑기 성공 응답을 받으면 배너 아래에 결과를 추가하지 않고 전용 결과 화면으로 전환한다. 결과 카드는 서버 순서를 유지한 채 1장 또는 최대 10장이 순차 등장하며, 등급별 테두리·광원과 `prefers-reduced-motion` 대체 동작을 제공한다. 결과 확인 후 원래 뽑기 화면으로 복귀할 수 있다.
- 정적 `battle-hud-preview`에서도 실제 API 호출 없이 동일한 결과 전환과 등장 화면을 확인할 수 있게 했다.
- `corepack pnpm --filter @hanjjak/web test` — 51개 파일, 303개 테스트 통과. 로컬 API proxy의 `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `corepack pnpm --filter @hanjjak/web typecheck` — `tsc --noEmit` 성공.
- `corepack pnpm --filter @hanjjak/web build` — Vite production build 성공, 250개 모듈 변환. 기존 번들 크기 경고는 남아 있다.
- 1201×731 local Chromium preview에서 전체화면 배너, 새 뽑기권, 쌀 골드, 결과 전환·복귀를 확인했다. `documentElement.scrollWidth === innerWidth`이며 가로 넘침은 없었다.

### 2026-09-11 결과 카드·200회 선택 상자 후속 증거

- 결과 화면의 별도 제목·수량 요약·소비 비용 문구를 제거하고, 각 결과를 `이름 → 세로형 이미지 → 등급` 순서로 재구성했다. 최초 획득만 이미지 우측 상단에 `NEW`를 표시하며 중복 표식은 노출하지 않는다.
- 결과 아래에 동일 크기의 `다시 1회/10회 뽑기`와 `돌아가기` 버튼을 배치했다. 다시 뽑기 버튼에는 현재 배너 조회값의 뽑기권·쌀 잔액을 작은 글씨로 표시하고, 같은 횟수의 서버 뽑기 명령을 새 멱등 키로 실행한다.
- 200회 선택 상자 진행도를 `progress`로 표시하는 공용 `CosmeticMilestoneProgress`를 배너와 결과 화면에서 재사용한다. 수령 가능 시 선택 상자 아이콘이 활성화되고 기존 마일스톤 수령 API를 호출하며, 성공 후 상자 팝·획득 문구 애니메이션을 제공한다. 수령 전에는 남은 횟수와 실행 가능한 빠른 뽑기 동작을 제공한다.
- 선택 상자 표식은 기존 치장 뽑기권 화풍에 맞춘 256×256 투명 ARGB `apps/web/src/features/cosmetics/assets-cozy-pixel/cosmetic-selector-box-v1.png`를 새로 사용한다. 실제 치장 콘텐츠 이미지는 계속 nullable 카탈로그와 플레이스홀더를 따른다.
- 정적 `battle-hud-preview`는 199/200 상태에서 1회 뽑기 후 상자 수령 가능, 수령 애니메이션, 0/200 갱신까지 API 호출 없이 재현한다. 1201×731 local Chromium에서 세로 카드·`NEW` 위치·동일 크기 버튼·상자 수령 모션을 확인했다.
- `corepack pnpm --filter @hanjjak/web test` — 52개 파일, 307개 테스트 통과. 로컬 API proxy의 `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `corepack pnpm --filter @hanjjak/web typecheck` — `tsc --noEmit` 성공.
- `corepack pnpm --filter @hanjjak/web build` — Vite production build 성공, 252개 모듈 변환. 기존 번들 크기 경고는 남아 있다.

### 2026-09-11 선택 상자 전체화면 흐름 후속 증거

- 선택 상자의 진행도와 수령·열기 진입점을 공용 `CosmeticMilestoneProgress`로 통합했다. 상자 수령 뒤에는 별도 `CosmeticSelectorScreen`으로 전환하고, 대상 전설 세트의 치장 카드 가운데 하나를 선택해 기존 상자 사용 API로 확정한다.
- 배너·결과 화면의 반복 설명과 선택 드롭다운을 제거하고, 선택 가능한 치장은 상자를 열었을 때만 노출한다. 실제 치장 이미지가 없는 항목은 콘텐츠 정본의 nullable 상태에 따라 플레이스홀더를 유지한다.
- 정적 `battle-hud-preview`에서도 200회 달성, 상자 수령, 치장 선택 화면 전환을 실제 API 호출 없이 재현한다.
- `corepack pnpm --filter @hanjjak/web typecheck` — `tsc --noEmit` 성공.
- `corepack pnpm --filter @hanjjak/web test src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 52개 테스트 통과. 로컬 API proxy의 `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `corepack pnpm --filter @hanjjak/web build` — Vite production build 성공, 253개 모듈 변환. 기존 번들 크기 경고는 남아 있다.

### 2026-09-11 생성 에셋 런타임 연결 후속 증거

- 별도 정적 시안 페이지 대신 실제 `battle-hud-preview.html?route=cosmetics`가 사용하는 `CosmeticGachaBoard`·`CosmeticDrawReveal`·`CosmeticSelectorScreen`에 생성 에셋을 연결했다. 메인과 선택 화면은 인벤토리와 같은 `modal-paper-decorated-v2.png` 종이 배경을 재사용한다.
- 메인에는 대형 치장 티켓과 비용 영역을 포함한 뽑기 버튼 셸을 적용했고, 결과·선택 화면에는 노말·희귀·영웅·전설 카드 셸과 NEW 리본을 적용했다. 희귀·영웅 카드 원본의 바깥 비알파 영역은 CSS 카드 윤곽 클리핑으로 화면에 노출하지 않는다.
- 정적 전투 프리뷰의 전설 세트 구성은 콘텐츠 정본의 `cosmetic-061`~`cosmetic-066` 여섯 부위로 맞췄다. 로컬 브라우저에서 치장 메인 → 1회 결과 → 200회 선택 상자 → 전설 6종 선택 화면 전환을 확인했다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 52개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공.
- `npm run build` — Vite production build 성공, 255개 모듈 변환. 기존 번들 크기 경고는 남아 있다.

### 2026-09-11 치장 메인 배경 레이어·에셋 비율 보정 후속 증거

- 치장 메인 화면의 전체 배경에는 `cosmetic-reveal-background-v1.png`를 비율 유지 `cover`로 배치하고, 그 위에 인벤토리와 같은 `modal-paper-decorated-v2.png`를 별도 레이어의 비율 유지 `contain`으로 올렸다. 좁은 화면에서는 종이 프레임을 늘리지 않고 `cover`로 전환해 가장자리만 잘리도록 했다.
- 메인 티켓 일러스트는 고정 가로·세로 동시 지정 대신 원본 고유 비율을 사용한다. 뽑기 버튼은 생성 에셋의 원본 `3:1` 비율과 `contain` 배경을 사용해 화면 크기에 따라 늘어나도 프레임이 왜곡되지 않는다.
- 후속 시각 조정에서 종이 배경의 화면 안쪽 여백을 데스크톱 기준 최대 `64px`로 늘리고, 티켓 일러스트·두 뽑기 버튼·확률 버튼·선택 상자 진행 영역을 함께 확대했다. 짧은 데스크톱 화면은 별도 높이 규칙으로 한 화면 구성을 유지한다.
- local `battle-hud-preview.html?route=cosmetics`에서 치장 메인과 1회 뽑기 결과 전환을 확인했다. 메인 티켓과 버튼의 렌더링 비율은 각각 원본 비율과 일치하며 기존 뽑기 동작은 유지된다.

### 2026-09-11 전설 세트·부위 2단계 선택 후속 증거

- 선택 상자 화면은 활성 카탈로그의 모든 전설 세트를 먼저 카드로 나열하고, 선택한 세트의 `HEAD`, `TOP`, `BOTTOM`, `GLOVES`, `SHOES`, `CAPE` 순서로 6부위 치장을 펼치는 2단계 흐름으로 변경했다. 현재 콘텐츠에는 전설 세트가 하나지만 추가 세트는 같은 카탈로그 응답에서 자동으로 늘어난다.
- 서버 선택 상자 검증은 지급 배너의 세트 구성원 한정에서 활성 콘텐츠의 모든 `LEGENDARY` 치장 허용으로 변경했다. 전설이 아닌 치장과 존재하지 않는 치장은 상자를 소비하기 전에 거절한다.
- 웹 단위·프리뷰 테스트는 전설 세트 2개 노출, 비전설 세트 제외, 세트별 부위 필터, 최종 선택 전달, 정적 치장 화면의 세트→부위 전환과 `INVALID_SELECTOR_COSMETIC` 안내를 검증했다. `npm test -- src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/cosmetics/presentation.test.ts src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 56개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `./gradlew.bat :modules:cosmetics:test --rerun-tasks --console=plain` 실행 결과 `CosmeticGachaServiceContractTest` 5건을 포함한 모듈 테스트가 통과했다. 신규 계약 테스트는 다른 전설 세트 치장 허용과 비전설 선택 시 무소비 거절을 검증한다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 255개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?route=cosmetics`에서 선택 상자 수령 후 `전설 세트 선택` 카드와 `받을 부위 선택` 6카드 전환을 확인했다. 실제 치장 이미지가 nullable인 현재 preview는 부위 이니셜·공용 이미지 플레이스홀더를 유지한다.

### 2026-09-11 치장 메인 정보 밀도 후속 증거

- 치장 메인에서 별도 `등급별 확률 보기` 진입점을 제거하고 뽑기 동작만 남겼다. 서버가 제공하는 확률 데이터와 뽑기 확인 단계의 계약은 변경하지 않았다.
- 제목 아래 장식선을 제거하고 보유 뽑기권·쌀을 테두리 카드가 아닌 인라인 재화 정보로 단순화했다. 두 뽑기 버튼의 간격을 줄이고 제목·비용 문구를 원본 `3:1` 버튼 셸 내부 상·하단에 고정해 화면 크기가 달라도 셸 밖으로 밀리지 않게 했다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 50개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 254개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?route=cosmetics`에서 확률 버튼이 사라지고, 뽑기 비용이 버튼 셸 안에 표시되며, 보유 재화가 박스 없이 노출되는 것을 확인했다.

### 2026-09-11 뽑기 버튼·10회 결과·전설 세트 카드 후속 증거

- 메인 뽑기 버튼은 원본 `3:1` 비율을 유지하면서 데스크톱 최대 폭을 키우고 두 버튼 사이 간격을 줄였다. 제목과 비용 문구는 셸 내부의 상·하 영역에 고정했으며, 좁은 화면에서도 두 버튼을 한 줄에 유지해 선택 상자 진행도와 하단 성장 메뉴가 겹치지 않게 했다.
- 종이 프레임의 좌우·하단 인셋을 늘려 화면 배경보다 작게 보이게 했고, 하단 프레임이 성장 메뉴 뒤로 들어가지 않게 조정했다.
- 정적 치장 프리뷰의 10회 뽑기를 실행 가능 상태로 두고 결과 카드 10장을 데스크톱 5×2, 좁은 화면 2열 스크롤로 표시한다. 결과 화면은 기존 등급 카드·NEW 리본·마일스톤·재뽑기 흐름을 그대로 재사용한다.
- 선택 상자의 첫 단계는 전설 카드 셸을 사용한 세트 카드로 변경했다. 각 카드에는 젓가락 캐릭터와 세트별 색상 미리보기를 배치하고, 정적 프리뷰에서는 레이아웃 확인용 전설 세트 4개를 제공한다. 세트 선택 후에는 해당 세트의 6부위 선택 화면으로 전환한다. 실제 콘텐츠 응답에는 존재하는 전설 세트만 표시한다.
- `npm test -- src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 51개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 254개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?route=cosmetics`에서 메인 버튼·하단 메뉴, 10회 결과 10장, 전설 세트 카드 4장, 세트 선택 후 6부위 전환을 확인했다.

### 2026-09-11 치장 메인 압축·선택 상자 안전 뽑기 후속 증거

- 치장 뽑기 제목과 본문 폭을 줄이고 보유 뽑기권·쌀을 제목 오른쪽에 정렬했다. 대형 티켓, 두 뽑기 버튼, 선택 상자 진행도는 종이 프레임 안에서 끝나도록 높이와 간격을 함께 압축했으며, 버튼 셸의 투명 여백까지 고려해 두 버튼의 시각 간격을 추가로 줄였다.
- 선택 상자까지 남은 횟수가 1회인 상태에서 빠른 뽑기를 누르면 10회가 아니라 1회 뽑기만 실행하도록 수정했다. 남은 횟수가 10회 이상이고 10회 뽑기가 실행 가능한 경우에만 10회 명령을 사용한다.
- 10회 결과 카드는 데스크톱에서 5장씩 2행을 유지하고 행 간격을 별도로 넓혔다. 선택 상자의 첫 화면은 보이는 `전설 세트 선택` 제목을 제거하고 전설 세트 카드 4장을 2열×2행으로 배치하며, 세트를 고른 뒤에는 기존 6부위 선택 단계로 전환한다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 52개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 254개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?route=cosmetics`에서 종이 프레임 안 재화·티켓·버튼·진행도 배치와 제목 없는 2열×2행 전설 세트 목록을 확인했다.

### 2026-09-11 치장 비용 아이콘·결과 간격 후속 증거

- 메인 화면의 자동 늘어나는 세로 여백을 제거하고 제목·티켓·뽑기 버튼·선택 상자 진행 영역을 위쪽부터 고정된 리듬으로 배치해 종이 프레임 안에서 끝나게 했다. 제목과 버튼 크기도 함께 줄였다.
- 보유 재화는 `뽑기권`, `장`, `보유 쌀` 문구를 화면에서 제거하고 각 에셋과 숫자만 표시한다. 뽑기 버튼의 하단 비용은 실제 실행 가능 여부 계약을 유지하면서 `뽑기권 에셋 + 1/10`, 구분 기호, `쌀 골드 에셋 + 5,000/50,000`의 대체 비용 안내로 단순화했다.
- 10회 결과 화면에서는 선택 상자 진행 영역을 노출하지 않고 카드 5장씩 2행 사이의 간격을 확대했다. 좁은 화면의 2열 결과도 별도 행 간격을 사용한다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 52개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 254개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?route=cosmetics`에서 아이콘·숫자만 남은 재화와 비용, 축소된 버튼, 결과 화면에서 제거된 선택 상자 진행 영역을 확인했다.
- 후속 위치 조정에서 제목과 보유 재화는 종이 상단으로부터 `18px` 아래로 이동하고, 티켓 일러스트 행의 최대 높이를 `390px`에서 `340px`로 줄였다. 이에 따라 두 뽑기 버튼과 선택 상자 진행 영역은 함께 위로 이동하며 서로의 간격은 유지한다.

### 2026-09-11 결과·선택 카드 배치 후속 증거

- 10회 결과 카드의 등급 문구를 카드 하단 안쪽으로 내리고, 결과 하단 버튼은 폭·높이를 줄여 카드와 겹치지 않게 아래로 분리했다. 실행 가능한 버튼은 선명한 기본 색을 유지하고 실제 실행 조건을 충족하지 못한 버튼에만 비활성 색과 투명도를 적용한다.
- 메인 화면의 뽑기 버튼과 선택 상자 사이 구분선을 제거하고 선택 상자 이미지를 `114×82px`까지 확대했다.
- 선택 상자 첫 단계의 화면용 제목·보유 개수 문구는 제거하되 접근성 이름은 유지했다. 데스크톱에서는 전설 세트 카드 4장을 `1행×4열`로 위쪽에 배치하고, 좁은 화면에서는 `2행×2열` 반응형 배치를 유지한다.
- 세트 선택 뒤에는 별도 `받을 부위 선택` 제목을 노출하지 않고 선택한 세트명만 표시한다. 6부위 카드의 간격을 줄여 위로 모으고, 하단 수령 버튼도 함께 위로 이동시켰다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 52개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 260개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?route=cosmetics`에서 부위 카드 선택 전후 수령 버튼의 비활성·활성 색상 전환과 제목 없는 선택 흐름을 확인했다.

### 2026-09-11 치장 화면 여백·부위 그리드 후속 증거

- 메인 상단 제목과 보유 재화를 함께 아래로 이동하고, 우측 재화 영역은 종이 프레임 안쪽으로 `48px` 당겼다. 두 뽑기 버튼은 제목 글자 크기를 유지한 채 셸 폭만 소폭 확대했다.
- 선택 상자 진행 묶음을 오른쪽으로 옮기고 진행 막대와 상자 이미지 사이 여백을 제거해 한 묶음으로 보이게 했다.
- 10회 결과 묶음은 아래로 이동하고 카드 영역과 결과 버튼 사이 여백을 늘렸다. 전설 세트 목록은 카드 폭과 간격을 줄인 고정 폭 4열을 화면 중앙에 배치한다.
- 세트 선택 뒤의 부위 화면에서는 종이 프레임 레이어를 제거하고 6부위 카드를 `3열×2행`으로 전환했다. 좁은 로컬 프리뷰에서도 같은 3열 구조와 선택 버튼 상태 전환을 확인했다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 52개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공. 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 티켓·결과 등급·부위 행 간격 후속 증거

- 메인 티켓 일러스트를 기존 확대 크기에서 추가로 아래 `12px` 이동했다.
- 결과 카드 하단의 등급 문구는 `18px`로 키우고 하단 프레임 중앙보다 위쪽에 배치했다.
- 후속 조정에서 등급 문구를 하단 프레임 안쪽으로 `5px` 더 내렸다. 부위 카드 묶음은 추가로 `16px` 위로 이동하고 두 행 간격은 `40px`에서 `30px`로 줄였다.
- 부위 선택의 세트명은 흰색과 짙은 그림자로 대비를 높였다. 데스크톱에서는 6부위 카드를 `190×285px`까지 확대해 `1행×6열`로 배치하고, 좁은 화면에서는 `3열×2행` 반응형 배치를 유지한다.
- 후속 조정에서 데스크톱 부위 카드를 최대 `220×330px`로 확대하고 6장 묶음을 중앙에서 아래로 이동했다. 각 카드 상단에는 `세트명 + 부위`, 하단에는 등급 `전설`만 중앙 정렬하며 실제 치장 항목명은 접근성 이름으로 보존한다.
- 부위 카드 묶음은 수령 버튼 위치를 유지한 채 이전 위치보다 추가로 `54px` 아래로 이동했다. 짧은 데스크톱 화면에서도 같은 방향으로 `52px` 추가 이동한다.
- 부위 카드 상단은 부위명만 표시해 긴 세트명으로 인한 줄바꿈을 제거했다. 하단의 이미지 준비 문구를 숨기고 진한 노란 띠 중앙에 `전설` 등급을 고정했으며, 화면 세트명과 카드 행 사이에는 `24px` 간격을 둔다.
- `세트 다시 선택` 동작은 카드 상단에서 제거하고 하단 액션 영역으로 옮겼다. 왼쪽 `세트 다시 선택`, 오른쪽 `이 치장 받기`를 동일한 폭의 2버튼 구성으로 배치한다.
- `npm test -- src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx` — 2개 파일, 22개 테스트 통과. `npm run typecheck`과 `npm run build`도 성공했으며 기존 500kB 초과 chunk 경고는 남아 있다.
- 부위 선택 화면은 화면용 빈 헤더 행을 접고 카드 상세 영역만 위로 이동했다. 3열×2행의 행 높이는 카드 비율에 맞춘 고정값으로 두고 행 간격을 확대해 그리드 압축 시 카드가 서로 침범하지 않게 했다.
- 짧은 화면에서는 카드 묶음만 내부 스크롤되고 하단 수령 버튼은 별도 행에 유지되어 카드와 겹치지 않는다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 53개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공. 기존 500kB 초과 chunk 경고는 남아 있다.
- 후속 치장 단위 테스트 `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx` — 3개 파일, 24개 테스트 통과. 병합 뒤 전체 웹 Vitest도 59개 파일·336개 테스트가 통과했다.

### 2026-09-11 티켓·선택 카드 가독성 후속 증거

- 메인 티켓 일러스트는 원본 비율을 유지한 채 최대 폭을 `460px`, 최대 높이를 `205px`로 확대하고 아래로 `14px` 이동했다.
- 전설 세트 선택 화면도 종이 프레임 레이어를 제거했다. 데스크톱 세트 카드는 `220×330px` 비율로 확대하고 4장 사이의 좁은 간격과 중앙 정렬을 유지한다.
- 부위 선택의 3열×2행 구조는 카드 비율을 유지하도록 폭과 행 높이를 맞추고 행 간격을 `30px`로 늘렸다. 전체 카드 높이를 줄이고 화면 상단 여백도 줄여 하단 수령 버튼과 겹치지 않게 했다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 53개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공. 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 사용자 제공 치장 에셋 프리뷰 연결 후속 증거

- 사용자 제공 `equip.zip` 원본은 수정하지 않고, 정적 치장 프리뷰 검증에 필요한 `거북이수호자`, `붕어빵`, `요리사`, `무지개떡`의 완성 프레임과 부위별 레이어만 `apps/web/src/features/cosmetics/assets-equip-preview`에 복사했다. 출처와 레이어 대체 규칙은 같은 디렉터리의 `README.md`에 기록했다.
- 선택 상자 첫 단계의 세트 카드에는 실제 완성 착용 프레임을, 세트 선택 뒤 6부위 카드에는 모자·몸통·장갑·신발·등 장식 레이어를 표시한다. 별도 하의 또는 몸통 레이어가 없는 항목은 프리뷰에서만 완성 프레임으로 대체하며, 운영 콘텐츠·치장 정책은 변경하지 않았다.
- 하단 액션 영역을 최대 `640px`로 줄여 두 버튼을 짧게 중앙 정렬했다. `세트 다시 선택`은 베이지·갈색 보조 버튼, `이 치장 받기`는 기존 빨간 확정 버튼으로 구분했다.
- `npm test -- src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 52개 테스트 통과. 테스트는 실제 프레임·부위 이미지 URL 연결, 보조 버튼 색상 클래스, 치장 선택 전후 확정 버튼 활성화를 포함한다. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공. 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 5부위 착용 프리뷰 후속 증거

- 정적 디자인 프리뷰의 부위 선택 목록에서 별도 이미지가 없는 하의를 제외하고 모자·상의·장갑·신발·망토 5종을 1행 5열로 배치했다. 운영 치장 계약과 6부위 SSOT는 이번 디자인 프리뷰 변경으로 수정하지 않았다.
- `거북이수호자`, `붕어빵`, `요리사`는 장비 단독 레이어 대신 사용자 제공 ZIP의 `worn/rest_01-*` 이미지를 연결해 기본 젓가락이 해당 부위를 착용한 모습을 표시한다. `worn` 결과가 없는 `무지개떡`은 완성 착용 프레임을 유지한다.
- 카드 폭은 최대 `240px`, 행 높이는 `360px`로 확대하고 카드 내부 캐릭터 이미지를 추가 확대했다. 짧은 데스크톱 화면도 5열 배치와 확대 비율을 별도로 유지한다.
- 후속 시각 조정에서 카드 프레임 크기는 유지하고 착용 캐릭터의 중앙 확대 비율을 `2배`로 올려 부위별 외형이 더 잘 보이게 했다.
- 추가 시각 조정에서 세트 카드의 완성 착용 모습은 `1.72배`로 확대했다. 부위 카드 착용 모습은 `2.25배`로 확대하면서 위로 이동해 짧은 화면에서도 하단 등급 띠에 캐릭터가 잘리지 않게 했다.
- 후속 피드백에서 세트 카드의 완성 착용 모습은 `2.45배`, 부위별 착용 모습은 `3.1배`로 다시 확대했다. 부위 이미지는 카드 내부 투명 영역에 가려지지 않도록 하고 위쪽으로 배치했다.
- 실제 화면 검수 후 부위별 착용 모습은 `2.75배`로 소폭 조정해 캐릭터 발끝과 신발이 카드 하단 등급 띠를 침범하지 않도록 했다.
- 추가 화면 피드백에 따라 부위별 착용 이미지는 크기를 유지한 채 조금 더 위로 정렬했다. 전설 세트 선택 카드는 최대 `190×285px`과 `2.3배` 프리뷰로 축소하고, 메인 `1회/10회 뽑기` 버튼은 각 최대 `350px`로 확대했다.
- 단일 뽑기 결과의 착용 캐릭터를 `2배`로 확대해 부위 선택 카드와 비슷한 가시성을 확보했다. 정적 선택 프리뷰에는 하의 대신 `무기`를 여섯 번째 열로 추가하고 사용자 제공 ZIP의 세트별 `worn/rest_01-weapon.png`를 연결했으며, 카드 상단에는 부위명 대신 실제 치장 이름을 확대·중앙 정렬했다. 운영 치장 슬롯 SSOT는 이번 프리뷰 변경으로 수정하지 않았다.
- 6열 전환으로 축소되어 보이던 부위별 착용 이미지는 공통 카드의 `grid-area` 상속을 해제해 본문 너비를 정상화한 뒤 `2.2배`로 확대했으며, 치장 이름은 최대 `22px`까지 확대했다.
- 세트 카드 하단은 `전설 5부위 / 고르기` 조합을 제거하고 `선택` 한 단어만 중앙 정렬했다. 부위 화면을 아래로만 옮기던 `transform`을 제거해 짧은 화면에서 카드와 하단 액션 행이 겹치며 잘리는 문제를 해소했다.
- 10회 뽑기 결과의 착용 캐릭터는 카드 프레임 크기를 유지한 채 `2배`로 확대했다. 확대된 이미지는 카드 본문 영역에서 잘라내 제목과 하단 등급 띠를 침범하지 않으면서, 이름 때문에 모자·무기 상단이 잘리지 않도록 세로 기준점을 보정했다.
- 결과 카드 하단 등급 문구는 `18px`에서 `22px`로 확대하고 아래쪽 이동값을 제거해 노란 등급 띠의 중앙보다 위쪽에서 또렷하게 보이도록 조정했다.
- 후속 피드백에 따라 결과·선택 카드의 노란 하단 띠는 등급 대신 실제 의상 이름을 표시하고, 카드 상단에는 짧은 부위명만 표시한다. 10회 결과의 하단 이름은 띠 안에서 아래로, 1회 결과는 위로 각각 별도 보정했으며 긴 이름은 한 줄 말줄임 처리한다. 선택 상자 부위 카드의 착용 프리뷰는 `2.2배`에서 `2.05배`로 소폭 축소했다.
- 10회 결과에서 첫째 행의 의상명 띠가 둘째 행 카드에 가려지지 않도록 결과 버튼 위 여백을 줄여 카드 그리드의 세로 공간을 확보했다.
- 짧은 화면에서도 둘째 행과 결과 버튼이 겹치지 않도록 10회 결과 카드만 `3:4` 비율로 조정하고 버튼 위 간격을 다시 확보했다. 압축된 카드에서도 모자부터 발끝까지 보이도록 10회 착용 프리뷰는 `1.85배`로 보정했으며, 단일 결과 카드 비율과 `2배` 프리뷰는 유지한다.
- 하단 띠에 의상명이 표시되므로 결과·선택 카드 상단의 부위명은 제거했다. 확보된 본문 공간으로 10회 결과는 `2.05배`, 1회 결과는 `2.15배`, 선택 상자 부위 프리뷰는 `2.2배`로 다시 확대하고 위쪽으로 정렬했으며, 결과 카드의 `NEW` 리본 위치도 새 본문 시작점에 맞췄다.
- 상단 문구 제거 뒤 10회 결과에서 캐릭터를 가리던 `NEW` 리본은 10장 결과에 한해 `56×36px`로 축소해 카드 우측 상단에 유지했다.
- 확대된 착용 프리뷰가 카드 상단에 치우쳐 보인다는 후속 피드백에 따라 배율은 유지하고, 10회 결과·1회 결과·선택 상자 부위 카드의 이미지만 각각 아래쪽으로 소폭 재정렬했다.
- `npm test -- src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 2개 파일, 31개 테스트 통과. 하의 미노출과 `worn` 착용 이미지 연결을 함께 검증한다.
- `npm run typecheck`과 `npm run build` 성공. 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 결과 버튼·선택 상자 결과 연출 후속 증거

- 결과 화면의 광원 장식 레이어가 포인터 입력을 가로채 키보드로는 동작하던 `다시 뽑기`·`돌아가기` 버튼을 마우스로 누를 수 없던 원인을 확인했다. 장식 레이어를 포인터 대상에서 제외하고 액션 행의 쌓임 순서를 명시했다.
- 전설 세트 선택 카드는 상단 세트명을 제거하고 하단 노란 선택 띠에 실제 세트명을 표시한다. 정적 프리뷰의 거북이 수호자 완성 착용 이미지는 다른 세트보다 소폭 축소했다.
- 선택 상자에서 부위를 확정하면 즉시 메인으로 닫지 않고, 선택한 치장 한 장을 단일 뽑기 결과 카드와 같은 연출로 보여준다. 선택 상자 결과는 재뽑기 없이 중앙 `돌아가기` 버튼만 제공한다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 55개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 263개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?route=cosmetics`에서 1회 재뽑기, 1회·10회 결과 복귀, 선택 상자 결과 복귀를 실제 포인터 입력으로 확인했다.

### 2026-09-11 뽑기 결과 전용 무대 후속 증거

- 치장 뽑기 결과가 표시되는 동안에는 좌측 프로필, 상단 관리 메뉴, 하단 성장 메뉴를 포함한 공용 내비게이션 전체를 숨겨 배경과 결과 카드·결과 액션만 보이게 했다.
- 결과 화면의 존재 여부를 기준으로 스타일을 적용하므로 `돌아가기`로 메인 치장 화면에 복귀하면 공용 내비게이션이 자동으로 다시 표시된다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 55개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?route=cosmetics`에서 실제 1회·10회 뽑기 결과의 공용 내비게이션 제거와 `돌아가기` 후 복원을 확인했다.

### 2026-09-11 세트 선택 가로 스크롤 제거 후속 증거

- 전설 세트 카드 그리드의 세로 자동 스크롤이 가로축까지 자동 스크롤 영역으로 만들던 스타일을 제거했다. 카드 확대와 그림자 표현은 유지하면서 세트 선택 화면의 불필요한 가로 스크롤바만 없앴다.
- 브라우저 계산 스타일에서 세트 그리드의 `overflow-x`·`overflow-y`가 모두 `visible`이고, 실제 너비와 스크롤 너비가 동일한 것을 확인했다.
- `npm test -- src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 2개 파일, 32개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 선택 상자 전용 무대 후속 증거

- 전설 세트 선택과 선택한 세트의 부위 선택 화면이 열려 있는 동안에도 좌측 프로필, 상단 관리 메뉴, 하단 성장 메뉴를 숨겨 선택 카드만 배경 위에 표시한다.
- 선택 화면을 닫거나 결과 화면에서 메인 치장 화면으로 복귀하면 공용 내비게이션은 자동으로 다시 표시된다.
- local `battle-hud-preview.html?route=cosmetics`에서 세트 선택과 부위 선택 양쪽에서 공용 내비게이션이 숨겨지는 것을 확인했다.
- `npm test -- src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 2개 파일, 32개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 10회 결과 NEW 리본 위치 후속 증거

- 10회 결과의 `NEW` 리본 기준점을 카드 내부 이미지 영역이 아니라 결과 카드 자체로 변경해, 반응형 카드 패딩과 무관하게 우측 상단 모서리에 일정하게 배치되도록 보정했다. 단일 뽑기 결과의 리본 위치와 크기는 유지했다.
- local `battle-hud-preview.html?route=cosmetics`에서 10회 결과 카드별 리본 정렬과 단일 결과 리본의 기존 위치 유지를 실제 화면으로 확인했다.
- `npm test -- src/features/cosmetics/CosmeticGachaBoard.test.tsx src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/cosmetics/CosmeticsScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 4개 파일, 55개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. Vite production build에서 263개 모듈을 변환했으며, 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 세트 선택 카드 비율 후속 증거

- 전설 세트 선택 카드의 하단 세트명을 노란 선택 띠 안에서 아래로 `5px` 재정렬했다. 사용자 제공 완성 착용 프리뷰는 공통 `2.15배`, 거북이 수호자 세트는 `1.9배`로 축소해 카드 프레임 안쪽 여백을 확보했다.
- local `battle-hud-preview.html?route=cosmetics`에서 세트 선택 카드의 이름 위치와 네 세트 착용 프리뷰 축소 상태를 실제 화면으로 확인했다.
- `npm test -- src/features/cosmetics/CosmeticSelectorScreen.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 2개 파일, 32개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. Vite production build에서 263개 모듈을 변환했으며, 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 캐릭터 치장 탭 왼쪽 장비형 배치 후속 증거

- 캐릭터 창의 치장 탭 왼쪽 영역을 장비 화면과 같은 중앙 캐릭터·주변 6부위 선택 카드 구조로 재구성했다. 기존 미리보기 초기화·치장 뽑기·오른쪽 보관함·착용 명령은 유지한다.
- 각 부위 카드는 현재 미리보기 외형과 착용 상태를 표시하고, 선택한 카드는 장비 화면과 같은 금색 테두리·체크 표식으로 강조한다. 부위 카드를 선택하면 오른쪽 보관함의 부위 필터와 목록이 함께 갱신된다.
- local `battle-hud-preview.html?route=cosmetics`의 캐릭터 창 치장 탭에서 6부위 카드 배치와 `머리 → 상의` 선택에 따른 보관함 갱신을 실제 포인터 입력으로 확인했다.
- `npm test -- src/features/character/CosmeticPanels.test.tsx src/features/character/CharacterWindow.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 34개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 263개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 캐릭터 치장 탭 정보 계층 후속 증거

- 왼쪽 미리보기의 6부위 슬롯을 확대하고 캐릭터와 함께 위쪽으로 재배치했다. 하단의 `미리보기 초기화`·`치장 뽑기` 버튼 영역은 제거하고, 초기화 동작만 미리보기 우측 상단의 단일 버튼으로 옮겼다.
- 오른쪽 보관함의 중복 헤더(`치장 보관함`, 현재 부위 제목, 보유 수량)를 제거하고 6개 부위 선택을 첫 번째 행의 탭 목록으로 변경했다. 선택한 부위와 아이템 목록·착용 명령의 연동은 유지한다.
- local `battle-hud-preview.html?character-tab=cosmetics`에서 확대된 슬롯·상향된 캐릭터·우측 상단 초기화 버튼과, 보관함이 부위 탭부터 시작하는 접근성 구조를 확인했다.
- `npm test -- src/features/character/CosmeticPanels.test.tsx src/features/character/CharacterWindow.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 34개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck` — `tsc --noEmit` 성공. `npm run build` — Vite production build 성공, 263개 모듈 변환. 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-11 승인 치장 보관함 시안 구현 후속 증거

- 승인 시안에 맞춰 왼쪽을 중앙 착용 캐릭터와 좌우 3개씩의 대형 슬롯 카드로, 오른쪽을 6부위 탭과 2열×2행 착용 프리뷰 카드로 재구성했다. 선택 카드명은 하단 띠, 등급·성급·현재 선택명은 하단 요약 행에 모았다.
- 짧은 창에서도 4개 카드가 모두 보이도록 두 행을 가용 높이에 맞춰 줄이고 가로 오버플로를 숨겼다. 왼쪽 캐릭터는 선택한 치장의 세트 완성 프리뷰를 우선 사용하고 슬롯 프리뷰는 부위별 초점을 확대했다.
- `착용` 바로 왼쪽에 `취소`를 추가했다. `취소`는 현재 부위의 미리보기 선택만 제거해 서버 명령 없이 원래 착용 치장으로 복원하며, 기존 `해제`는 별도 보조 버튼으로 유지했다.
- 화면은 사용자 시안에 맞춰 여섯 번째 부위를 `무기`로 표시한다. 운영 치장 계약의 `BOTTOM`은 변경하지 않고 정적 프리뷰 카탈로그의 `WEAPON`만 호환 매핑했으며, 정책 충돌은 [결정 로그](../../../../80-decisions/README.md)에 `unresolved`로 등록했다.
- `npm test -- src/features/character/CosmeticPanels.test.tsx src/features/character/CharacterWindow.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 35개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. Vite production build에서 263개 모듈을 변환했으며 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?character-tab=cosmetics`에서 2열×2행 카드와 `붕어빵 모자 선택 → 취소 → 거북이 모자 복원`을 실제 포인터 입력으로 확인했다.

### 2026-09-11 치장 보관함 레이아웃 재정돈 후속 증거

- 미리보기 열을 469px로 확보하고 좌우 부위 슬롯을 `106×112px`로 축소·정렬해 중앙 착용 캐릭터를 가리던 문제를 해소했다. 반복 상태 문구는 화면에서 제거하고 접근성 이름에는 유지했다.
- 오른쪽 보관함은 부위 탭, 2열×2행 카드, 선택 요약·액션 순서가 한눈에 읽히도록 간격을 다시 배분했다. 카드 상단에서 가로로 늘어나던 등급 표시는 작은 배지로 복원했다.
- local `battle-hud-preview.html?character-tab=cosmetics`에서 가로 오버플로가 없고, 다른 치장 선택 시 `취소`·`착용`이 활성화되며 `취소` 후 기존 착용 상태로 복원되는 것을 실제 포인터 입력으로 확인했다.
- `npm test -- src/features/character/CosmeticPanels.test.tsx src/features/character/CharacterWindow.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 35개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. Vite production build에서 263개 모듈을 변환했으며 기존 500kB 초과 chunk 경고는 남아 있다.

### 2026-09-12 도감 재구성 시안 에셋 준비 증거

- 사용자 승인 시안의 `세트 목록 → 6부위 구성품 → 선택 세트 효과 → 누적 도감 효과` 배치에 필요한 신규 투명 PNG 8종을 `apps/web/src/features/character/assets-cozy-pixel/collection-v1`에 추가했다.
- `set-title-plaque.png`, `collection-book-icon.png`, `collection-effect-box-icon.png`, `defense-shield-icon.png`에 이어 `set-effect-star-active.png`, `set-effect-star-locked.png`, `collection-piece-icon.png`, `collection-sign-decoration.png`를 추가했다. 기존 종이 프레임·새싹·검·하트·치장 이미지는 재사용 대상으로 유지했다.
- 여덟 파일의 ARGB 픽셀 포맷, 이미지 크기와 투명 모서리를 확인했다.
- 시안 배치대로 `GalleryPanel`에 연결하고 3단 비율(0.30 / 0.43 / 0.25), 세트 썸네일 확대, 부위 아이콘 넘침, 효과 패널 겹침을 브라우저에서 실측해 맞췄다. 세트 이름 `거북이수호자`는 시안대로 `거북이 수호자`로 띄웠다.
- 사용자 지시로 가운데 아래 칸은 성급 트랙만 남기고, 세트 효과는 오른쪽 패널이 맡도록 옮겼다. 오른쪽 패널 제목은 `누적 도감 효과` → `세트 효과`이며 고른 세트의 현재 효과와 다음 성급 효과를 함께 보여 준다.
- 명패는 투명 여백을 잘라 `1020×153`으로 줄이고 `border-image` 9분할로 그린다. 양 끝 나뭇잎은 원래 비율로 두고 가운데 판만 늘어난다.
- 등록 수량이 0인 부위는 도감에서 흐리게 남긴다. 뽑기만 하고 등록하지 않은 부위도 미보유로 취급한다.

### 2026-09-12 치장 성급 임계값 밸런스 조정 증거

- 사용자 1,000회 뽑기 체감과 제한적인 뽑기권 공급을 반영해 적용 콘텐츠를 `cosmetics-v3-balanced-placeholder`로 갱신했다. 누적 등록 임계값은 노말 `1/3/6/10/18`, 희귀 `1/3/6/12/24`, 영웅 `1/2/4/8/16`, 전설 `1/2/3/5/10`이다. 수치 정본은 [MVP 치장 뽑기 콘텐츠](../../../../60-content/cosmetics/mvp-v1.md)가 소유한다.
- `corepack pnpm --filter @hanjjak/content-validator validate`, `build`, `test`가 성공했다. validator 테스트는 2개 파일·14개가 통과했고 manifest checksum을 갱신했다.
- `gradlew.bat :modules:cosmetics:test --no-configuration-cache`가 성공해 서버 콘텐츠 로더가 새 버전과 네 등급 임계값을 읽고 등급별 성급 계산에 적용하는 것을 확인했다.
- `npm test -- src/features/character/CosmeticPanels.test.tsx` 5개 테스트, `npm run typecheck`, `npm run build`가 통과했다. build는 297개 모듈을 변환했으며 기존 500kB 초과 chunk 경고는 남아 있다.
- 가운데 아래 칸의 `선택 세트 효과` 제목 줄을 지워 세로를 확보했다. 오른쪽 패널은 능력치별로 현재 값과 다음 성급 값을 한 줄에 합쳐 `0.2% [잠금] 0.4%` 형태로 읽는다. 여섯 줄이 세 줄로 줄었다.
- 제목 줄을 뺀 만큼 부위 카드가 늘어져 카드 높이에 상한 252px를 뒀다. 처음에는 행 자체를 252px로 고정했는데, 창이 낮으면 칸이 줄지 못해 도감 전체가 종이 아래로 삐져나갔다. 늘어나는 행으로 되돌리고 상한만 남겨 낮은 창에서는 카드가 가로로 납작해지며 줄어든다.
- 카드 상한을 252px에서 430px 격자(카드 205px)로 더 낮추고, 가운데 칸을 격자에서 세로 흐름으로 바꿨다. 격자로 두면 남는 자리가 카드와 성급 트랙 사이에 빈칸으로 남아 트랙이 아래에 떠 있었다. 이제 제목·카드·트랙이 위에서부터 붙고 여백은 아래에 모인다.
- 완성 세트 칩 위의 점선은 패널을 기준으로 그려 칩 모서리와 좌우가 어긋나 있었다. 칩 자신을 기준으로 두어 칩의 좌우 끝과 정확히 맞춘다.
- 사용자 요청으로 카드 상한을 두 번 더 낮춰 격자 290px(카드 216×142, 아이콘 80px)로 뒀다. 카드 안의 이름·성급·수량 줄과 여백도 같이 좁혀 줄어든 높이를 아이콘이 최대한 가져가게 했다. 카드가 가로로 긴 형태가 되고 칸 아래 여백이 넓어지는 것은 사용자가 승인한 방향이다.
- 창 높이 620 / 915에서 스크롤 영역을 뺀 모든 자손의 아래 끝이 창 아래선을 넘지 않는 것을 확인했다.
- 종이 배경은 `103%`로 그려 아래쪽 톱니가 칸 안쪽까지 올라온다. 원본에서 톱니 최상단이 높이의 93.84%, 103% 확대와 가운데 정렬을 되돌리면 칸 높이의 95.2% 지점이다. `.character-body` 아래 여백을 칸 높이의 5.2%로 두어 내용이 그 선 위에서 끝나게 했다.
- 능력치·치장·도감 세 탭 모두 가장 아래 요소가 톱니 선 위에서 끝나는 것을 실측했다. 이전에는 27px 아래로 내려가 종이 밖으로 걸쳐 있었다.
- 사용자가 여유를 더 요청해 아래 여백을 칸 높이의 5.2% → 7.5% → 8.6%로 올렸다. 톱니 선까지 여유는 3px에서 30px가 됐다.
- 세트 목록 줄의 보유 수·성급 표기를 12px/19px에서 15px/23px로 키웠다.
- 세트 효과 줄은 현재 값과 다음 성급 값 사이에 화살표를 둔다. 현재 값이 없는 능력치(세트 미완성)는 화살표 없이 잠금 값만 보여 준다.
- 뽑기 결과 카드의 등급 글씨가 프레임에 그려진 띠보다 위에 떠 있었다. 원본 프레임에서 띠는 카드 높이의 82.6%~94.0%이고 가운데가 88.3%다. 1회·10회 모두 글씨 가운데가 88.2~88.4%에 오도록 맞췄다.
- 세트 목록 썸네일 바탕을 뽑기 카드 프레임에서 딴 등급 색으로 칠했다. 노말 크림, 희귀 파랑, 영웅 보라, 전설 금색이며 목록에서 등급을 바로 읽을 수 있다.
- 치장 미리보기가 레이어 하나라도 없으면 고른 부위의 세트 전신 이미지로 물러서고 있었다. 그래서 스시야 모자 하나만 걸쳐도 스시야 한 벌을 입은 것처럼 보였다. 완성 이미지는 여섯 부위를 한 세트로 채웠을 때만 쓰고, 그 밖에는 레이어가 있는 부위만 겹친다.
- 스시야는 `layers/`에 rest 포즈가 없다(death1·run2·strike1·thrust1만 분리돼 있음). rest는 `worn/`에만 있는데 이쪽은 분리 레이어가 아니라 기본 몸통에 그 부위만 얹은 전신본이다(같은 모션 hat 기준 불투명 비율 `layers/` 1.22% vs `worn/` 7.84%, 다른 세트의 분리 rest 모자는 0.58~1.82%).
- 그래서 `worn/rest_01-*.png` 여섯 장에서 분리 레이어를 유도했다. 다섯 장(무기 제외)이 모두 일치하는 픽셀은 몸통과 기본 막대기이므로 제외하고, `worn/rest_01-weapon.png`(옷 없는 몸통)와도 같은 픽셀을 제외하면 부위 그림만 남는다. 무기는 `기본나무검/worn/rest_01-weapon.png`와 차분해 뽑았다. 결과 불투명 비율은 0.89~4.12%로 다른 세트의 분리 레이어와 같은 수준이다.
- 이제 스시야도 다른 세트처럼 부위별로 조합된다. 천사 모자 + 스시야 상의·무기 조합이 어긋남 없이 겹치는 것을 확인했다.
- 등에 걸치는 부위(`CAPE`)는 맨몸보다 먼저 그린다. 예전에는 맨몸 위에 얹어 천사 날개와 가죽경갑 가방이 젓가락 몸을 뚫고 앞으로 나왔다.
- 칸이 그림보다 세로로 길어 `object-fit: contain`이 캐릭터를 가운데에 띄웠다. `object-position`으로 칸 아래에 붙이고 가로 위치를 56%로 옮겼다.
- 민소매 상의는 맨몸의 팔을 덮어 팔이 사라졌다. `frame/arms-only/rest_01.png`를 공통 팔 레이어로 만들어 그 상의 바로 뒤에 다시 올린다.
- 대상 세트는 눈대중이 아니라 팔 픽셀 2,020개를 기준으로 상의가 덮는 비율을 재어 골랐다. 간호사 50.2% · 천사 44.9% · 약과 40.7% · 악마 38.0%가 대상이고, 죽창무사 1.2% · 가죽경갑 3.0% · 고물기사 3.3% · 거북이수호자 3.7% · 요리사 5.8%는 팔이 그대로 보여 제외했다. 스시야는 27.0%지만 상의에 흰 소매가 그려져 있어 팔을 덧그리면 소매를 덮으므로 제외했다.
- 무기도 같은 증상이었다. 맨몸 베이스에는 무기가 없어, 치장을 하나라도 걸치면 기본 나무검이 사라졌다. `기본나무검/layers/rest_01-weapon.png`를 기본 무기 레이어로 만들어 무기 치장을 끼지 않았을 때 맨 위에 올린다.
- 간호사는 상의가 `body2`(몸통)와 `body1`(소매)로 나뉘어 있다. 예전 빌드가 둘을 한 장으로 합쳐 팔 아래에 깔았기 때문에 소매가 팔에 덮였다. 몸통만 `nurse-top.png`로 두고 소매는 `nurse-top-over.png`로 분리해 팔 위에 다시 올린다.
- 스시야·천사·악마 무기는 자루가 손과 겹치게 그려져 있어 손 위에 얹으면 젓가락 손을 덮었다. 세 세트의 무기는 몸통보다 먼저 그려 손이 자루를 쥔 것처럼 보이게 한다. 나머지 세트는 그대로 손 위에 둔다.
- 치장 카드의 보유 개수를 `등록분 + 중복`으로 세고 있어 승급에 써도 숫자가 그대로였다. 승급은 중복을 등록분으로 옮기는 것이라 합계가 변하지 않는다. 아직 쓰지 않은 중복(`availableUnregisteredQuantity`)만 세도록 바꿨다.
- 서버 규칙과 같은 계산으로 확인했다. 등록 1·중복 4에서 노말 2성(기준 3)으로 올리면 중복 2개가 빠져 등록 3·중복 2가 되고, 카드 표기가 ×4에서 ×2로 줄고 도감 게이지는 3/3에서 5/6으로 넘어간다.
- 그래도 `5 / 6` 같은 누적 표기는 뜻을 읽어야 알 수 있어 사용자가 이해하기 어려웠다. 사용자 선택에 따라 부위 카드에서 누적 수치를 빼고, 지금 무엇이 얼마나 필요한지를 버튼에 그대로 적는다.
- 표기는 사용자 요청대로 `가진 여분 / 다음 성급에 드는 개수`다. 미획득과 최대 성급은 글자로 적고, 그 밖에는 `2 / 2`(활성, 금색)나 `1 / 3`(비활성)처럼 두 숫자를 나란히 둔다. 자세한 설명은 title과 aria-label에 문장으로 담았다.
- 여분이 필요한 것보다 많으면 `9 / 4`처럼 그대로 보여 준다. 넉넉하다는 것도 정보라 기준치로 자르지 않았다.
- 치장 미리보기의 젓가락을 520×600에서 620×700으로 키웠다.
- 초기화 버튼을 무대 오른쪽 위에서 젓가락 아래 가운데로 내렸다. 위쪽 자리가 비면서 슬롯 칸을 104×122에서 122×142로 키우고 위·아래 칸을 가장자리에 더 붙일 수 있었다.
- 위·아래 칸을 같은 값(12px + 칸 높이 절반)으로 띄우면 가운데를 50%에 두는 것만으로 간격이 같아진다. 예전처럼 초기화 버튼을 피하려고 위 칸만 따로 내릴 필요가 없다. 실측 간격은 67 / 65px이다.

### 2026-09-13 간호사·마법사 세트 제외 증거

- 사용자 결정으로 간호사(`cosmetic-set-04`)와 마법사(`cosmetic-set-09`)를 게임에서 뺐다. 콘텐츠 파일에서 지우는 대신 세트에 `active: false`를 붙였다.
- 지우지 않은 이유는 로더가 치장 ID `cosmetic-001`~`cosmetic-066` 연속을 요구하기 때문이다. 12종을 빼면 뒤 번호가 앞으로 밀려 `cosmetic_collection_state`에 남은 보유 기록이 다른 치장을 가리키게 된다.
- 스키마 `$defs/set`에 선택 항목 `active`를 더했고(`additionalProperties: false`라 필요했다) manifest 체크섬을 갱신했다. 콘텐츠 버전은 `cosmetics-v4-active-sets-placeholder`다.
- 로더는 파일 전체에 대한 불변식(66종·11세트·등급별 세트 수)을 그대로 검사한 뒤, 런타임 콘텐츠에서만 비활성 세트와 그 구성품을 뺀다. 뽑기 풀·도감·착용 목록이 모두 `content.cosmetics`/`content.sets`에서 나오므로 이 한 곳만 거르면 된다.
- 비활성 세트가 배너나 선택 상자를 가지면 뽑기가 깨지므로 로더가 이를 막는다.
- 결과 런타임 카탈로그는 54종·9세트이고 등급별 풀은 노말 18 / 희귀 18 / 영웅 12 / 전설 6이다. 전설 배너 세트(`cosmetic-set-11`)는 그대로 남는다.
- 검증: `:modules:cosmetics:test`, `:apps:game-api:test`, content-validator 14개, 웹 354개 통과.
- 여섯 상태를 한 화면에 놓고 확인했고, 누르면 `2개 넣어 2성` → 2성 → `3개 더 필요`로 넘어가는 것까지 봤다.
- 마법사는 `layers/`에 hat·shoes만 분리돼 있고 나머지는 합쳐진 상태라 기존 판단대로 상의·장갑·망토·무기 레이어가 없다.
- 카드의 승급 수량 표기는 서버가 내려주는 `nextStarThreshold`를 그대로 쓴다. 새 임계값을 화면에서 보려면 `:apps:game-api:bootJar` 재빌드 후 API 재시작이 필요하다.



### 2026-09-13 매끈한 치장 모달 종이 에셋 준비 증거

- 자글거리는 현재 종이와 사용자 제공 목표 치장 화면을 참고해 빈 모달 배경 `apps/web/src/features/character/assets-cozy-pixel/modal-paper-smooth-v3.png` 1개를 생성했다.
- 최종 에셋은 `1672×941`, `Format32bppArgb`이며 네 모서리 알파가 0이다. 매끈한 아이보리 내부, 얇은 찢김 테두리, 아래 양끝의 작은 새싹 외에는 UI 요소를 포함하지 않는다.
- 이번 범위에서는 에셋만 준비했고 런타임 배경 교체는 하지 않았다.

### 2026-09-13 치장 초기화 동작 복구 증거

- 치장 초기화는 현재 장착된 여섯 데이터 슬롯을 중복 없이 수집한 뒤 각 슬롯에 `cosmeticId: null`인 장착 명령을 전송해 서버 상태까지 해제한다. 화면 초안도 즉시 빈 슬롯으로 맞추며, 장착 항목이 없거나 요청 중이면 버튼을 비활성화한다.
- 배치 요청은 슬롯별 안정적인 idempotency key를 유지한 채 순차 실행하고 전체 성공 뒤 치장 도감·캐릭터 능력치 캐시를 각각 한 번만 무효화한다. 응답 불확실 재시도는 같은 명령과 키를 재사용한다.
- `CosmeticPanels.test.tsx`와 `api.test.ts`에서 초기화 시 다중 장착 부위와 `WEAPON` 슬롯의 해제 명령, 빈 장착 상태 비활성화, 슬롯별 요청 순서, 배치당 캐시 무효화 횟수를 검증했다. 최신 `origin/main` 통합 후 웹 전체 75개 파일·434개 테스트, TypeScript 검사와 프로덕션 빌드가 통과했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
