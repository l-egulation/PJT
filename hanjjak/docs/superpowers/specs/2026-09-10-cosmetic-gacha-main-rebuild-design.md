# main 기반 치장 뽑기 재구현 설계

## 상태

- 설계 상태: 사용자 승인 방향 반영, written spec 검토 대기
- 기준 브랜치: `origin/main`
- 구현 브랜치: `feat/cosmetic-gacha-main-rebuild`

## 목표

현재 `main`에 남아 있는 치장 서버 계약·도메인·콘텐츠와 캐릭터 내부 진입을 보존하면서, 사용자가 상단 메뉴에서 직접 접근할 수 있는 기능 완결형 치장 뽑기 화면을 웹 클라이언트에 다시 연결한다.

## 범위

### 포함

- 상단 관리 메뉴의 `치장 뽑기` 직접 진입
- 캐릭터 창 내부 `치장 뽑기` 버튼의 동일 화면 진입 유지
- `1-5 최초 클리어` 전 잠금 안내
- 서버 배너 목록과 티켓·쌀 잔액 표시
- 1회·10회 뽑기 예상 결제 표시 및 실행 전 확인
- 서버 제공 등급 확률과 치장별 분자·분모·표시 확률 표시
- 서버 응답 순서를 보존한 신규·중복 결과 표시
- 결과 후 배너·도감·캐릭터 능력치 상태 갱신
- 도감·미등록 중복 재고·성급 등록·외형 착용·선택 상자 관리
- 확정 오류와 네트워크/5xx 불확실 오류의 구분 및 동일 idempotency key 재확인
- 현재 `main`의 코지 전투 HUD·공용 관리 네비게이션 회귀 방지
- 웹 focused 테스트, typecheck, build, 실제 브라우저 smoke
- K-21 작업 문서의 구현·검증 증거 갱신

### 제외

- 치장 서버 도메인·DB migration·API 계약의 재작성
- 치장 실제 이미지·외형 합성 렌더러 제작
- 티켓 반복 공급 정책 변경
- 일반 배너·기간제 배너·천장·확률 보정 추가
- URL router 도입 또는 앱 전체 네비게이션 재설계
- 치장 정책 SSOT의 규칙·수치 변경

## 현재 기준과 책임 경계

- 규칙: `docs/30-domain/cosmetics/ssot.md`
- 구현 범위·검증: `docs/70-plans/cosmetic-gacha/requirements.md`
- 비용·확률·카탈로그: `docs/60-content/cosmetics/mvp-v1.md`
- MVP 배포 범위: `docs/70-plans/mvp-release/requirements.md`
- 클라이언트 구조: `docs/50-architecture/client.md`
- 배포·운영: `docs/50-architecture/operations.md`
- 실행 상태·증거: `docs/wiki/06-delivery/tasks/K-client-ux/k-21-cosmetics-vertical-slice.md`

서버가 이미 제공하는 안정 API를 클라이언트가 재계산하거나 대체하지 않는다. UI는 API adapter와 서버 상태 cache를 통해서만 데이터를 읽고 명령을 보낸다.

## 구조

```text
상단 치장 뽑기 / 캐릭터 내부 치장 뽑기
                    │
                    ▼
              AppScreen = cosmetics
                    │
                    ▼
              CosmeticsScreen
        ┌───────────┼───────────┐
        ▼           ▼           ▼
     banners      detail     collection/catalog
        │           │           │
        └─────── server API ────┘
                    │
          confirmation → command
                    │
                    ▼
       draw / claim / open / register / equip
                    │
                    ▼
  query cache: banners, detail, collection, catalog, character stats
```

### 앱 진입

`apps/web/src/navigationState.ts`에서 `AppScreen`에만 존재하는 `cosmetics`를 상단 `PRIMARY_NAV_ITEMS`에 추가한다. `MANAGEMENT_SCREENS`에는 추가하지 않아 새로고침 시 저장되는 일반 관리 화면 목록과 독립 화면 상태를 분리한다.

`apps/web/src/main.tsx`는 현재 `CozySharedNavigation`의 공용 화면 분기와 전투 HUD를 보존한다. `screen === "cosmetics"`일 때 `CosmeticsScreen`을 렌더링하며, 캐릭터 창의 `onGacha` callback은 같은 상태로 이동한다. 치장 화면을 떠날 때 `cosmetic-collection`과 `character-stats`를 무효화한다.

### 기능 컴포넌트

`apps/web/src/features/cosmetics/`는 다음 책임을 가진다.

- `api.ts`: 치장 조회·명령 endpoint와 DTO, 공통 오류 해석
- `CosmeticsScreen.tsx`: query/mutation orchestration, 선택 배너, 확인·재시도 상태
- 배너 UI: 활성 배너, 잔액, 1/10회 비용, 확률 진입
- 확률 UI: 서버 authoritative probability 표시
- 결과 UI: 서버 결과 순서·`isNew` 표시
- 관리 UI: 등록·성급·착용·마일스톤·선택 상자
- `presentation.ts`: 안정 오류 코드·표시 문자열·결과 요약
- `cosmetics.css`: 치장 화면 전용 스타일

기존 `apps/web/src/features/character/`의 치장 탭과 `onGacha` 계약은 유지한다. 공통 UI나 server DTO를 임의의 전역 store로 옮기지 않는다.

## 데이터 흐름과 상태 규칙

1. 화면이 열리면 banners, collection, catalog를 서버에서 조회한다.
2. 선택 배너가 정해지면 해당 banner detail을 조회한다.
3. 뽑기 버튼은 확인 상태만 열고, 확인 전에는 draw 명령을 보내지 않는다.
4. 명령 body에는 서버가 요구하는 최소 입력만 넣는다. 뽑기에는 `count`, registration에는 `mode`, equipment에는 `cosmeticId`, 상자 사용에는 `cosmeticId`만 보낸다.
5. idempotency key는 명령 생성 시 한 번 만들고 불확실한 오류의 재확인에 같은 키를 사용한다.
6. 성공 응답은 서버 commit 뒤에만 화면 상태에 반영한다.
7. 결과의 신규·중복은 서버 `isNew`를 사용한다. 클라이언트가 보유 상태를 보고 재판정하지 않는다.
8. 성공 후 관련 query를 무효화하거나 서버 응답으로 갱신해 잔액·마일스톤·도감·세트 효과·캐릭터 능력치를 최신화한다.
9. `COSMETIC_SYSTEM_LOCKED`와 `GACHA_CONTENT_UNAVAILABLE`은 서버 오류 의미를 그대로 표시한다. 뽑기 장애가 도감·등록·착용까지 막지 않도록 영역을 분리한다.
10. 네트워크 단절 또는 5xx는 새 key를 생성하지 않고 동일 명령 재확인만 허용한다. 확정 4xx는 오류 안내 후 최신 상태를 조회한다.

## 화면 인수 기준

- 인증·재료 선택 게이트를 통과한 사용자는 상단에 `치장 뽑기`를 볼 수 있다.
- 상단 버튼을 누르면 기존 코지 공용 관리 화면을 훼손하지 않고 치장 화면이 열린다.
- 캐릭터 → 치장 → `치장 뽑기`도 동일 화면을 연다.
- 1-5 전 계정은 뽑기 요청 없이 잠금 안내를 본다.
- 활성 배너는 서버 목록과 동일한 순서·식별자로 표시된다.
- 티켓 우선 예상 비용, 쌀 비용, 실행 가능 여부가 서버 값과 일치한다.
- 확인 취소는 draw POST를 발생시키지 않는다.
- 확률 dialog는 서버가 준 확률 정수·분자·분모·표시 문자열을 그대로 보여준다.
- draw 성공 후 결과 개수, 결과 순서, 등급, 신규·중복, 실제 차감 비용이 응답과 일치한다.
- 신규·중복 결과 후 도감·잔액·마일스톤·캐릭터 stats가 갱신된다.
- 등록·성급·착용·해제·선택 상자 명령은 기존 API와 idempotency 규칙을 따른다.
- 뽑기 HMAC 장애가 발생해도 관리 영역은 계속 표시된다.
- 현재 main의 전투 HUD, 전투 기록, 장비, 아이템, 스킬, 보석, 거래소, 랭킹 화면 진입과 렌더링이 회귀하지 않는다.

## 검증 계획

### 자동 검증

- `apps/web/src/navigationState.test.ts`: `cosmetics` 직접 진입 항목과 일반 저장 화면 분리
- `apps/web/src/features/cosmetics/*test.tsx`: 배너·확률·확인·취소·결과·오류·관리 흐름
- 기존 character tests: 캐릭터 내부 치장·뽑기 callback 회귀
- `pnpm --filter @hanjjak/web test`
- `pnpm --filter @hanjjak/web typecheck`
- `pnpm --filter @hanjjak/web build`

### 브라우저 smoke

실제 Chromium에서 mock API 또는 실행 가능한 로컬 API를 사용해 다음을 확인한다.

1. 로그인된 화면에서 상단 `치장 뽑기` 클릭
2. 배너·잔액·1/10회 비용 확인
3. 확률 dialog 열기·닫기
4. 1회 뽑기 확인 dialog 취소 후 요청 미발생 확인
5. 1회 뽑기 확인 후 결과 panel에서 서버 결과 확인
6. 캐릭터 창으로 돌아가 도감·착용 진입 확인
7. 1568px 데스크톱과 모바일 폭에서 가로 overflow가 없는지 확인

실제 PostgreSQL·Spring HTTP·동시성 인수는 이번 웹 MR의 증거로 과장하지 않는다. 서버 기존 검증 상태와 이번 웹 검증 결과를 K-21에 분리 기록한다.

## 문서 갱신

구현과 검증이 끝난 뒤 `docs/wiki/06-delivery/tasks/K-client-ux/k-21-cosmetics-vertical-slice.md`에 다음을 기록한다.

- 이번 MR의 변경 파일
- focused test/typecheck/build 결과
- 실제 브라우저 smoke 결과와 해상도
- 미실행한 PostgreSQL·HTTP·동시성 검증 범위

정책 SSOT와 feature-route에는 작업 상태나 규칙을 복제하지 않는다.
