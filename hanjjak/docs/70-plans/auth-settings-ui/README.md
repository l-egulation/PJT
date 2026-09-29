# 인증·사용자 설정 UI 계획

로그인·회원가입과 후속 사용자 설정 화면의 시각 설계와 구현용 이미지 자산 분리를 관리한다. 화면 동작은 [클라이언트 UX SSOT](../../30-domain/player/ux/ssot.md), 계정 규칙은 [계정·저장 SSOT](../../30-domain/player/ssot.md), 실행 상태는 [K-04](../../wiki/06-delivery/tasks/K-client-ux/k-04-auth-first-entry-ui.md)와 [B-01](../../wiki/06-delivery/tasks/B-account-storage/b-01-signup-login.md)을 따른다.

새 아늑한 픽셀 화면에 필요한 신규·재사용 자산은 [아늑한 픽셀 온보딩 에셋 제작 명세](./cozy-pixel-asset-plan.md)에서 관리한다.

## 2026-09-09 아늑한 픽셀 온보딩 적용

사용자 확인을 거친 로그인, 나라 선택 v6와 확인 모달 v2를 현재 구현에 연결했다. 공통 목조 길드 배경·종이 셸·버튼 셸은 `apps/web/src/shared/assets/onboarding-cozy-pixel`, 인증 전용 로고·입력 자산은 `apps/web/src/features/auth/assets-cozy-pixel`, 나라 삽화·선택 도장·인구 아이콘은 `apps/web/src/features/material-preference/assets-cozy-pixel`에서 사용한다. 이전 빈티지 구현과 자산은 변경 이력으로 보존하지만 현재 온보딩 화면에서는 참조하지 않는다.

나라별 인구는 선택 화면에 표시 자리를 구현했지만 현재 API 계약에는 값이 없다. 값이 없을 때는 `집계 준비 중`으로 표시하며, 임의의 인구 수를 제품 데이터로 사용하지 않는다.

## 아늑한 픽셀 UI 후보 초안

2026-09-09 제공된 캐주얼 픽셀 RPG 레퍼런스의 화풍을 적용한 [로그인 후보 초안](./concepts/login-cozy-pixel-draft-v1.png), [주력 나라 선택 초기 초안](./concepts/material-country-cozy-pixel-draft-v1.png), [단순화한 주력 나라 선택 초안 v2](./concepts/material-country-cozy-pixel-draft-v2.png), [로그인 배경 화풍과 통일한 선택 초안 v3](./concepts/material-country-cozy-pixel-draft-v3.png), [최종 확인 모달 초안](./concepts/material-country-confirm-modal-draft-v1.png), [큰 잉크 도장을 적용한 선택 초안 v4](./concepts/material-country-cozy-pixel-draft-v4.png), [잉크 도장 상태를 유지한 확인 모달 v2](./concepts/material-country-confirm-modal-draft-v2.png), [주력 비율과 선택 인구를 강조한 추천 초안 v5](./concepts/material-country-population-draft-v5.png), [나라 이름과 인구를 이미지 위로 옮긴 추천 초안 v6](./concepts/material-country-population-draft-v6.png)를 보존한다.

- 따뜻한 픽셀 아트 마을·길드 배경, 밝은 크림색 종이 패널, 짙은 갈색 외곽선, 코랄 강조색을 공통 시각 언어로 사용한다.
- 로그인 초안은 로그인·회원가입 탭, 이메일·비밀번호 입력, 주요 제출 동작의 기존 구조를 유지한다.
- 나라 선택 초안은 [주력 재료 SSOT](../../30-domain/character/ssot.md)의 선택지와 서버 제공 비율을 표시하고, 영구 선택 고지는 최종 명령 전에 제공한다.
- 나라 선택 v2는 상세 배경과 중첩 카드 구조를 제거하고, 넓은 단일 종이 위에 나라별 대표 건물 하나와 재료 하나만 배치한다. 선택 상태는 카드 테두리 대신 코랄 바닥 강조와 확인 인장으로 표시한다.
- 나라 선택 v3는 로그인 화면과 같은 목조 길드 배경 화풍을 사용하되 배경 소품을 줄이고, 실제 재료 아이템 에셋과 비율 숫자를 묶어 표시한다. 제목 위 장식과 상단 부제는 제거하고 영구 선택 고지는 어두운 오버레이 위 최종 확인 모달로 이동한다.
- 나라 선택 v4는 작은 왁스 배지를 제거하고 선택한 나라 그림 위에 반투명한 대형 원형 잉크 도장을 직접 겹쳐 선택 상태를 표시한다. 확인 모달 v2의 어두운 배경에서도 같은 도장 상태를 유지한다.
- 나라 선택 v5는 주력 재료 아이콘을 보조 재료보다 크게 표시하고 나라 이름과 재료 비율 사이에 사람 아이콘과 선택 인구를 배치한다. 이미지의 인구 값은 시각 검토용 예시이며 현재 주력 재료 API는 집계 인구를 제공하지 않으므로 구현 전 집계 범위와 갱신 시점이 포함된 별도 계약이 필요하다.
- 나라 선택 v6는 각 나라 이름과 선택 인구를 나라 이미지 위의 헤더 묶음으로 이동하고 이미지 아래에는 재료 아이콘과 비율만 남긴다.
- 로그인 초안, 나라 선택 v6와 확인 모달 v2는 2026-09-09 사용자 확인을 거쳐 현재 구현 기준으로 사용한다. 나머지 중간 초안은 변경 기록으로 보존한다.

## 승인된 인증 화면

2026-09-08 사용자 확인을 기준으로 [클립 없는 빈티지 로그인 시안 v2](./concepts/login-vintage-final-v2.png)를 로그인·회원가입 화면의 구현 기준으로 사용한다. 회원가입은 같은 제목·탭·입력 종이·붉은 제출 종이 구성을 유지하고 닉네임 입력 행만 추가한다.

시각 기준은 오래된 동네 주방의 타일 벽과 나무 조리대, 클립이나 금장 장식이 없는 낡은 종이 UI다. 인증 화면에서는 젓가락 캐릭터와 금장 티켓을 사용하지 않는다. 기존 [로그인 v1](./concepts/login-final-v1.png)과 [회원가입 v1](./concepts/signup-final-v1.png)은 이전 승인안 기록으로 보존한다.

## 승인된 국가 선택 화면

2026-09-08 사용자 확인을 기준으로 [빈티지 국가 선택 시안 v2](./concepts/material-selection-vintage-v2.png)를 최초 국가 선택 화면의 구현 기준으로 사용한다.

- 감자·고구마·옥수수 국가 풍경 v2와 기존 문구·배치는 유지한다.
- 개별 국가 카드와 금장 명패는 사용하지 않고 풍경을 타일 벽에 직접 배치한다.
- 대표 재료와 획득 비율은 하나의 공용 종이 띠, 선택 후 질문과 버튼은 바로 이어지는 같은 계열의 종이 띠에 표시한다.
- 선택 상태는 평면 붉은 잉크 도장과 비선택 풍경의 명도 차이로 표시한다.

## 마이페이지 화면 변경 기록

[마이페이지 시안](./concepts/profile-concept-v1.png)의 큰 종이 패널과 좌우 분할 구성은 2026-09-08 승인안 기록으로 보존한다. 2026-09-09 사용자 변경으로 캐릭터 요약과 오른쪽 상태 정보의 중복이 확인되어 좌측 패널은 현재 구현 기준에서 제외했다. 현재 정보 구조와 반응형 기준은 [클라이언트 UX SSOT](../../30-domain/player/ux/ssot.md)만 따른다.

- 계정 정보, 캐릭터 상태, 닉네임 변경을 하나의 넓은 세로 흐름으로 배치한다.
- 화면 문구와 값은 HTML로 표시하고 현재 계정·캐릭터 계약에서 제공하는 값만 사용한다.
- 전투 능력치처럼 현재 계약에서 제공하지 않는 값은 임의로 추가하지 않는다.

## 이미지 자산 분리 계획

완성 시안을 잘라서 사용하지 않는다. 배경이 섞이지 않도록 각 자산을 독립 원본 또는 투명 PNG로 준비한다.

| 자산 | 형식 | 구현 방식 |
| --- | --- | --- |
| 주방 배경 | WebP 또는 PNG | 글자 없는 16:9 배경 한 장 |
| 공통 티켓 외곽 프레임 | 투명 PNG | 로그인·회원가입이 같은 프레임을 사용하고 반응형 크기는 9-slice 또는 `border-image`로 처리 |
| 종이 내부 질감 | WebP 또는 PNG | CSS 배경으로 반복하거나 늘림 |
| 한짝 로고 명판 | 투명 PNG | 화면 상단 장식 이미지 |
| 환영 자세 젓가락 | 투명 PNG | 로그인·회원가입이 [선택된 한 자산](./assets-v1/auth-mascot-greeting-selected-v1.png)을 함께 사용 |
| 우측 티켓 장식 | 투명 PNG | 장식 전용, 상호작용 없음 |
| 활성·비활성 탭 판 | 투명 PNG | 실제 탭 텍스트와 상태는 HTML로 표시 |
| 입력창 프레임 | 투명 PNG | 실제 `<input>` 위에 배경 또는 테두리로 적용 |
| 주요 버튼 판 | 투명 PNG 세트 | 기본·hover·pressed·disabled 상태를 준비하고 텍스트는 HTML로 표시 |
| 비밀번호 보기 아이콘 | SVG 또는 투명 PNG | 실제 버튼의 접근성 이름은 HTML로 제공 |

## 분리 순서

1. 글자가 없는 주방 배경을 확정한다.
2. 빈 티켓 프레임과 종이 질감을 분리한다.
3. 원본 비율의 환영 자세 젓가락을 투명 배경으로 만든다.
4. 탭·입력창·버튼의 빈 판과 상태별 이미지를 만든다.
5. 실제 한글, 입력값, 포커스, 오류, 로딩 상태는 React와 CSS로 올린다.
6. 로그인과 회원가입 양쪽에 동일한 좌표·크기 규칙을 적용하고 16:9와 16:10에서 확인한다.

텍스트를 이미지 안에 굽지 않는다. 입력값·오류·로딩·키보드 포커스가 동적으로 바뀌고 접근성 트리에 노출되어야 하기 때문이다.

## 1차 자산 추출 결과

- [글자 없는 주방 배경](./assets-v1/auth-kitchen-background-v1.png)은 조립용 배경 후보로 사용할 수 있다.
- [빈 티켓 프레임](./assets-v1/auth-ticket-shell-draft-v2.png) 하나를 로그인·회원가입에서 함께 사용한다. 로그인은 두 입력칸 묶음을 프레임 안에서 세로 중앙에 두고, 회원가입은 세 입력칸 묶음을 같은 상단 기준선에서 배치한다.
- [선택된 환영 자세 젓가락](./assets-v1/auth-mascot-greeting-selected-v1.png) 하나를 로그인·회원가입에서 함께 사용한다.

티켓과 선택된 캐릭터는 2026-09-06 배경 제거를 마쳐 실제 알파 채널을 가진 구현 후보 자산으로 정리했다.

## 2차 조립 자산

2026-09-06 기준 로그인·회원가입 화면을 조립하는 데 필요한 공통 제어 자산을 추가했다.

| 자산 | 파일 | 배경 처리 | 사용 기준 |
| --- | --- | --- | --- |
| 입력창 프레임 | [auth-input-frame-v1.png](./assets-v1/auth-input-frame-v1.png) | 실제 알파 채널 포함 | 이메일·닉네임·비밀번호에 공통 사용 |
| 활성 탭 | [auth-tab-active-v1.png](./assets-v1/auth-tab-active-v1.png) | 실제 알파 채널 포함 | 현재 선택한 로그인 또는 회원가입 탭 |
| 비활성 탭 | [auth-tab-inactive-v1.png](./assets-v1/auth-tab-inactive-v1.png) | 실제 알파 채널 포함 | 선택하지 않은 탭 |
| 주요 버튼 | [auth-primary-button-v1.png](./assets-v1/auth-primary-button-v1.png) | 실제 알파 채널 포함 | 로그인·계정 만들기 버튼에 공통 사용 |
| 종이 질감 | [auth-paper-texture-v1.png](./assets-v1/auth-paper-texture-v1.png) | 불투명 반복 배경 | 티켓과 폼 내부 바탕에 사용 |
| 공통 폼 패널 | [auth-form-panel-v1.png](./assets-v1/auth-form-panel-v1.png) | 실제 알파 채널 포함 | 로그인·회원가입 입력 영역의 공통 바탕 |
| 비밀번호 표시 | [auth-password-visible-v1.png](./assets-v1/auth-password-visible-v1.png) | 실제 알파 채널 포함 | 비밀번호가 보이는 상태 |
| 비밀번호 숨김 | [auth-password-hidden-v1.png](./assets-v1/auth-password-hidden-v1.png) | 실제 알파 채널 포함 | 비밀번호가 가려진 상태 |

탭과 입력창에는 글자를 굽지 않는다. 주요 버튼의 hover는 밝기 상승, pressed는 `translateY`와 밝기 감소, disabled는 채도와 명도 감소로 구현한다. 오른쪽 티켓의 점 장식은 CSS로 처리한다. 이 구성으로 인증 화면에 필요한 별도 이미지 자산은 충족한다.

## 빈티지 v2 전환

2026-09-08 승인 변경으로 v1의 금장 티켓·캐릭터·이미지 탭·입력 프레임·버튼 판은 현재 인증 화면 구성에서 제외했다. 이전 자산 파일은 마이페이지 등 기존 참조와 변경 이력 보존을 위해 유지한다.

- 공통 주방 배경: `apps/web/src/features/auth/assets/auth-background-vintage.png`
- 제목·탭 종이: `apps/web/src/features/auth/assets/auth-vintage-title-paper.png`
- 로그인·회원가입 공용 폼 종이: `apps/web/src/features/auth/assets/auth-vintage-form-paper.png`
- 입력창 프레임: `apps/web/src/features/auth/assets/auth-vintage-input-frame.png`
- 붉은 제출 버튼 판: `apps/web/src/features/auth/assets/auth-vintage-primary-button.png`
- 한짝 그릇 도장: `apps/web/src/features/auth/assets/auth-vintage-bowl-stamp.png`
- 문구·선·선택 밑줄·동작 상태 배치: `apps/web/src/features/auth/AuthGate.css`
- 국가 선택 배경: `apps/web/src/features/material-preference/assets/material-country-background-vintage.png`
- 국가 풍경과 픽셀 재료 아이콘: `apps/web/src/features/material-preference/assets/*-country-v2.png`, `*-item-pixel-192.png`

모든 문구와 입력값은 계속 HTML로 표시하며 로그인·회원가입 탭, 비밀번호 표시, 오류·로딩·키보드 포커스 동작도 기존 React 구현을 유지한다.

로그인·회원가입 폼 행은 공용 종이 패널의 상하·좌우 중앙에 배치하고 오류·상태 문구는 입력 행의 중앙 정렬 계산에서 분리한다. 패널의 빨간 세로선은 종이 안쪽에서 끝나며 비밀번호 표시 버튼은 입력 프레임의 오른쪽 테두리와 간격을 둔다. 이미 국가를 확정한 계정은 정책에 따라 선택 화면을 건너뛰므로 시각 검토가 필요하면 새 로컬 테스트 계정으로 회원가입해 미선택 첫 진입 상태를 사용한다.

## 아늑한 픽셀 온보딩 전환

현재 로그인·회원가입은 주력 나라 화면과 같은 공통 길드 배경, 양피지 셸, 붉은 버튼 셸을 사용한다. 인증 전용 자산은 `apps/web/src/features/auth/assets-cozy-pixel`의 목재 질감 `한짝 / 젓가락 키우기` 로고, 이메일·닉네임·비밀번호 아이콘, 입력창 셸이다. 입력값·탭·오류·로딩 문구는 이미지에 굽지 않고 기존 React 폼과 접근성 트리에서 유지한다.

입력창과 버튼 자산은 투명 캔버스의 광학 여백을 고려해 배경 표시 높이를 보정한다. 로그인 종이는 화면 중앙에 두고 로고, 탭, 이메일·비밀번호, 제출 버튼이 위에서 아래로 한 번에 읽히도록 배치하며 회원가입은 같은 구조에 닉네임 필드만 추가한다.

## 팀원 로컬 인증 QA 실행

저장소 루트에서 의존성을 설치한 뒤 세 터미널을 사용한다.

```powershell
corepack pnpm install --frozen-lockfile
```

첫 번째 터미널에서 PostgreSQL을 실행하고 healthy 상태를 확인한다.

```powershell
docker compose -f infra/local/compose.yaml up -d postgres
docker compose -f infra/local/compose.yaml ps postgres
```

두 번째 터미널에서 실제 game-api를 실행한다.

```powershell
.\gradlew.bat :apps:game-api:bootRun
```

세 번째 터미널에서 Vite 웹 클라이언트를 실행한다. Vite의 `/api` 프록시는 `http://127.0.0.1:8080`의 game-api를 사용한다.

```powershell
corepack pnpm --filter @hanjjak/web dev
```

브라우저에서 `http://127.0.0.1:5173`을 열고 회원가입, 국가 선택, 로그아웃, 같은 계정 재로그인을 차례로 확인한다. 재로그인 후에는 저장된 국가가 복원되어 국가 선택 화면을 건너뛰어야 한다.

Docker Desktop에서 `dockerInference` 또는 `docker-secrets-engine` AF_UNIX 소켓 오류가 발생하면 공장 초기화를 실행하지 않는다. Docker를 완전히 종료한 뒤 `%LOCALAPPDATA%\Docker\run`과 `%LOCALAPPDATA%\docker-secrets-engine`을 같은 상위 폴더의 백업 이름으로 옮기고 다시 시작한다. 이 조치는 컨테이너 볼륨을 삭제하지 않는다.
