---
doc_kind: reference
owner_domain: cosmetics
status: partial-verified
verified_at: 2026-09-12
---

# 캐릭터·치장·도감 구현 증거

규칙 정본은 [치장 SSOT](../../30-domain/cosmetics/ssot.md), 콘텐츠 정본은 [MVP 치장 콘텐츠 v2 임시 카탈로그](../../60-content/cosmetics/mvp-v1.md)다. 이 문서는 구현 위치와 실제 수행한 검증만 기록한다.

## 구현 위치

- 콘텐츠·스키마: `packages/game-content/versions/v1/cosmetics/cosmetics.json`, `packages/game-content/schema/cosmetics.schema.json`
- 콘텐츠 검증: `tools/content-validator/src/validate.ts`
- 서버 도메인·저장·API: `modules/cosmetics`
- 캐릭터 능력치: `apps/game-api/src/main/kotlin/com/hanjjak/gameapi/character`
- DB migration: `apps/game-api/src/main/resources/db/migration/V3__cosmetics.sql`, `V4__cosmetic_audit.sql`
- TypeSpec: `packages/contracts/cosmetics.tsp`, `packages/contracts/character.tsp`
- 웹 UI: `apps/web/src/features/cosmetics`, `apps/web/src/features/character`
- 실행 상태: [K-21](../../wiki/06-delivery/tasks/K-client-ux/k-21-cosmetics-vertical-slice.md), [K-22](../../wiki/06-delivery/tasks/K-client-ux/k-22-character-window.md)

## 2026-09-07 구현 범위

- 기존 30종 개발 초안을 제거하고 `cosmetic-001`~`cosmetic-066`, 11세트, 노말·희귀·영웅·전설 세트 수 `4:3:3:1`로 clean cutover했다.
- 실제 표시명과 이미지는 nullable로 두고 웹에서 `치장 #NNN`, `세트 #NN`, `이미지 준비 중` fallback을 제공한다.
- 1회 5,000쌀, 10회 50,000쌀, 등급 확률 `50:35:14:1`, 등급별 성급 임계값을 콘텐츠에 적용했다.
- 도감 단계·도감 전투 효과와 legacy 고정 3축 계약을 제거하고 stat ID·정수 값·단위의 세트 효과로 바꿨다.
- 캐릭터 능력치와 전투 입력이 같은 계산을 사용하도록 레벨·장비·보석·패시브·치장 효과를 정렬했다.
- 웹에 캐릭터 능력치·치장·도감 dialog, 정보 카드 미리보기·착용, 성급 등록, 200회 마일스톤 1개/전체 수령과 전설 선택 상자 사용을 연결했다.

## 2026-09-07 검증

- `corepack pnpm --filter @hanjjak/content-validator test`: 2 files, 3 tests passed.
- `corepack pnpm --filter @hanjjak/content-validator validate`: 성공, manifest checksum 갱신.
- `corepack pnpm --filter @hanjjak/contracts build`: TypeSpec·OpenAPI 생성 성공.
- `corepack pnpm --filter @hanjjak/web test`: 13 files, 30 tests passed.
- `corepack pnpm --filter @hanjjak/web typecheck`: 성공.
- `corepack pnpm --filter @hanjjak/web build`: Vite production build 성공.
- `C:/hanjjak-main`에서 inventory·cosmetics·game-api·sim-core를 clean한 뒤 `:modules:cosmetics:test :apps:game-api:test :packages:sim-core:test --no-configuration-cache`: 성공.

## 2026-09-11 승인 치장 보관함 시안 구현

- 캐릭터 치장 탭을 중앙 착용 캐릭터와 좌우 3개씩의 큰 부위 슬롯, 오른쪽 6부위 탭과 2열×2행 착용 프리뷰 카드, 하단 선택 요약·액션 행으로 재구성했다.
- 선택한 치장의 세트 완성 프리뷰가 있으면 왼쪽 캐릭터에 우선 표시하고, 오른쪽 카드명은 별도 하단 띠에 배치했다. 짧은 화면에서도 4개 카드가 한 화면에 들어오며 가로 스크롤이 생기지 않도록 행 높이와 오버플로를 보정했다.
- 하단에는 기존 `해제`를 보조 동작으로 유지하고 `취소`·`착용`을 나란히 배치했다. `취소`는 서버 명령 없이 현재 부위의 임시 선택만 지워 원래 착용 상태로 되돌린다.
- `BOTTOM`과 정적 프리뷰 `WEAPON` 차이는 운영 계약을 변경하지 않고 UI 호환 매핑으로 처리했다. 확정되지 않은 슬롯 정책 충돌은 [결정 로그](../../80-decisions/README.md)에 `unresolved`로 등록했다.
- `npm test -- src/features/character/CosmeticPanels.test.tsx src/features/character/CharacterWindow.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 35개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. Vite production build에서 263개 모듈을 변환했으며 기존 500kB 초과 chunk 경고는 남아 있다.
- local `battle-hud-preview.html?character-tab=cosmetics`에서 2열×2행 카드의 가로 스크롤 없는 배치와 `붕어빵 모자 선택 → 취소 → 거북이 모자 복원`을 실제 포인터 입력으로 확인했다.

## 2026-09-11 치장 보관함 레이아웃 재정돈

- 왼쪽 미리보기 폭을 넓히고 부위 슬롯 카드를 `106×112px`로 줄여 중앙 캐릭터와 슬롯이 겹치지 않게 했다. 슬롯에서 반복되던 `착용 중` 문구는 시각적으로 숨기되 접근성 이름에는 상태를 유지했다.
- 오른쪽은 부위 탭, 2열×2행 치장 목록, 선택 요약·액션 행의 세 구역으로 간격을 다시 맞췄다. 공통 폭 규칙 때문에 길게 늘어나던 등급 표시는 작은 배지 너비로 복원했다.
- local `battle-hud-preview.html?character-tab=cosmetics`에서 미리보기 469px, 슬롯 `106×112px`, 보관함 550px 배치와 가로 오버플로 없음, `붕어빵 모자 선택 → 취소 → 거북이 모자 복원`을 실제 포인터 입력으로 확인했다.
- `npm test -- src/features/character/CosmeticPanels.test.tsx src/features/character/CharacterWindow.test.tsx src/features/battle/battle-hud-preview.test.tsx` — 3개 파일, 35개 테스트 통과. 로컬 API proxy `ECONNREFUSED :3000` 진단 로그가 있었지만 실패 테스트는 없었다.
- `npm run typecheck`과 `npm run build` 성공. Vite production build에서 263개 모듈을 변환했으며 기존 500kB 초과 chunk 경고는 남아 있다.

## 2026-09-12 도감 재구성 시안 구현용 에셋 준비

- 승인된 도감 시안의 `세트 목록 → 6부위 구성품 → 선택 세트 효과 → 누적 도감 효과` 구조에 필요한 신규 장식만 `apps/web/src/features/character/assets-cozy-pixel/collection-v1`에 준비했다.
- 신규 파일은 글자를 런타임에서 올릴 수 있는 투명 명패 `set-title-plaque.png`, 도감 헤더용 `collection-book-icon.png`, 누적 효과 헤더용 `collection-effect-box-icon.png`, 방어력 행용 `defense-shield-icon.png`, 활성 성급용 `set-effect-star-active.png`, 잠금 성급용 `set-effect-star-locked.png`, 수집 수량용 `collection-piece-icon.png`, 하단 장식용 `collection-sign-decoration.png`다.
- 종이 모달·행·버튼 프레임, 새싹·검·하트 아이콘과 치장별 이미지는 기존 에셋을 재사용한다. 성급 진행선과 상태 전환은 CSS가 맡되 활성·잠금 표식 자체는 신규 PNG를 사용하도록 준비했다.
- 여덟 PNG가 모두 `Format32bppArgb`이고 모서리 알파가 0임을 확인했다. 명패는 투명 여백을 잘라내고 `1020×153`으로 줄였고(원본 `2172×724`), 하단 장식은 `1536×1024`, 나머지 아이콘 6종은 각각 `1254×1254`다.
- 준비한 여덟 PNG를 `GalleryPanel`에 연결하고 실제 치장 아트로 브라우저 시각 검증까지 마쳤다. 세트 목록 썸네일은 `preview` 전신 프레임을, 6부위 카드는 `item-icons` 부위 아이콘을 쓴다.

## 2026-09-12 치장 성급 임계값 재조정

- 사용자 1,000회 뽑기 체감과 제한적인 뽑기권 공급을 반영해 `packages/game-content/versions/v1/cosmetics/cosmetics.json`의 콘텐츠 버전을 `cosmetics-v3-balanced-placeholder`로 올렸다.
- 누적 등록 임계값은 노말 `1/3/6/10/18`, 희귀 `1/3/6/12/24`, 영웅 `1/2/4/8/16`, 전설 `1/2/3/5/10`으로 확정했다. 등급 확률과 66종 풀 기준 특정 치장 기대 획득 간격은 각각 약 48회·51회·129회·600회다.
- `corepack pnpm --filter @hanjjak/content-validator validate` 성공, 콘텐츠 manifest checksum을 갱신했다.
- `corepack pnpm --filter @hanjjak/content-validator build`와 `test` 성공 — 2개 파일, 14개 테스트 통과.
- `gradlew.bat :modules:cosmetics:test --no-configuration-cache` 성공. 실제 JSON 로더와 등급별 성급 계산 회귀 테스트를 포함한다.
- `npm test -- src/features/character/CosmeticPanels.test.tsx` 성공 — 1개 파일, 5개 테스트 통과. `npm run typecheck`과 `npm run build`도 성공했으며 production build는 297개 모듈을 변환했다. 기존 500kB 초과 chunk 경고는 남아 있다.
- `CosmeticsRulesTest`의 자체 임계값 픽스처와 `CosmeticPanels.test.tsx`의 `nextStarThreshold` 픽스처가 옛 수치로 남아 있어 새 표에 맞췄다. 두 곳 모두 콘텐츠 파일을 읽지 않는 합성 값이라 실패하지는 않았지만, 읽는 사람이 옛 표를 현행으로 오해할 수 있었다.
- 등급별 성급 계산 회귀 검증을 네 등급 모두로 넓혔다. 같은 누적 10개에서 노말 4성, 희귀 3성, 영웅 4성, 전설 5성이다.
- 재검증: `:modules:cosmetics:test`, `:apps:game-api:test`, `:modules:admin:test` 성공. 웹 전체 60개 파일 345개 테스트 통과, content-validator 14개 테스트 통과.

## 2026-09-13 매끈한 치장 모달 종이 에셋 준비

- 사용자 제공 현재 스킬 화면과 목표 치장 화면을 비교해, 고주파 종이 입자 대신 넓고 고른 아이보리 면을 가진 `apps/web/src/features/character/assets-cozy-pixel/modal-paper-smooth-v3.png`를 생성했다.
- 에셋은 `1672×941` PNG이며 얇은 픽셀 찢김 테두리와 아래 양끝의 작은 새싹만 포함한다. 글자·아이콘·버튼·카드·구분선·배경 장면·외부 그림자는 넣지 않아 런타임 UI를 올릴 수 있게 했다.
- 최초 생성본의 바깥 체크무늬가 실제 픽셀이어서 후속 배경 추출로 교체했다. 최종 파일은 `Format32bppArgb`, 네 모서리 알파 0, 중앙 알파 253으로 확인했다.
- 이번 요청은 에셋 1개 준비까지이며 실제 화면의 `modal-paper-decorated-v2.png` 교체는 수행하지 않았다.

## 남은 범위

- PostgreSQL migration과 인증된 Spring Boot HTTP E2E는 실행하지 않았다.
- 2026-09-07 변경에 대한 실제 Chromium E2E는 실행하지 않았다.
- 마일스톤·선택 상자·등록·착용의 동시 요청 검증은 실행하지 않았다.
- 실제 치장·세트 명칭, 이미지와 캐릭터 외형 합성 렌더러는 미구현이다.
- 미등록 중복 치장의 거래소 예약·체결 연결과 상세 감사 기록은 미구현이다.
- 지속 자동전투 session에서 변경 효과를 다음 사이클에 적용하는 전체 수명주기 인수는 미검증이다.
