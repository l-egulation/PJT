---
doc_kind: task
owner_domain: delivery
task_code: 'B-12'
task_area: 'B 계정·저장'
task_type: '개발'
priority: 'P1'
target_chapters: '공통'
planning_status: '완료'
development_status: '완료'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://lab.ssafy.com/s15-bigdata-dist-sub1/S15P21B107/-/work_items/11'
snapshot_date: '2026-09-09'
---

# B-12 Google·Kakao·Naver·SSAFY GitLab 소셜 로그인 구현

> 작업 상태와 완료 증거의 SSOT. 도메인 규칙은 [계정·저장](../../../../30-domain/player/ssot.md), 전송 규칙은 [Networking](../../../../50-architecture/networking.md)을 따른다.

## 완료 기준

서버가 활성화된 공급자의 Authorization Code 로그인을 시작·검증하고, 외부 식별자를 기존 계정 로그인 또는 닉네임 확정 뒤 신규 계정 생성에 연결한다. 사용자는 인증된 마이페이지에서 여러 공급자를 연결·해제할 수 있으며 마지막 로그인 수단은 제거할 수 없다.

## 선행 작업

B-01, K-04

## 비고

Google·Kakao·SSAFY GitLab OIDC와 Naver OAuth 사용자 정보 흐름, 서버 세션의 state·nonce·PKCE verifier, RS256/JWK·issuer·audience·만료 검증, `(provider, providerSubject)` 복합 식별자를 구현했다. 공급자 이메일은 자동 병합 키로 사용하지 않고 로컬 로그인 이메일과 외부 표시 이메일을 분리했다. Kakao는 비즈니스 앱 등록 없이도 동작하도록 개인 정보 scope를 요청하지 않고 `openid`의 `sub`만 식별에 사용하며 닉네임은 서비스 화면에서 확정한다. 공급자 token은 인증 요청 메모리에서만 사용하고 DB·세션·클라이언트에 저장하지 않는다. 공급자별 enable flag와 자격 증명이 모두 있는 경우에만 독립 활성화한다.

## 증거 링크
- 서버: `modules/account/src/main/kotlin/com/hanjjak/account/application/SocialLoginService.kt`, `modules/account/src/main/kotlin/com/hanjjak/account/infrastructure/HttpSocialProviderClient.kt`, `modules/account/src/main/kotlin/com/hanjjak/account/api/SocialLoginController.kt`
- 저장: `apps/game-api/src/main/resources/db/migration/V37__social_login.sql`, `modules/account/src/main/kotlin/com/hanjjak/account/infrastructure/JdbcSocialIdentityRepository.kt`
- 계약·웹: `packages/contracts/main.tsp`, `apps/web/src/features/auth/SocialLoginPanel.tsx`, `apps/web/src/features/profile/SocialConnections.tsx`
- 검증: `modules/account/src/test/kotlin/com/hanjjak/account/application/SocialLoginServiceTest.kt`에서 PKCE S256·state 선검증·동일 이메일 비병합·마지막 로그인 수단 보호를 확인했다.
- 2026-09-09 `:modules:account:test`와 `:apps:game-api:test`, 웹 116개 테스트·typecheck·production build, TypeSpec/OpenAPI 생성을 통과했다. 1280×900 브라우저에서 설정이 완전한 공급자만 표시되는 로그인 화면과 공급자 인증 뒤 닉네임 확정 화면을 확인했다. Kakao authorization URL이 `openid`만 요청하고 `account_email`·`profile_nickname`을 포함하지 않는 회귀 검증을 추가했다. 실제 공급자 client credential이 저장소에 없으므로 Google·Kakao·Naver·SSAFY GitLab의 실계정 consent/callback은 아직 검증하지 않았다.
- 2026-09-09 운영 `game-api`가 internal `backend` 네트워크에만 연결되어 공급자 token·JWK endpoint를 DNS 조회하지 못하는 장애를 확인했다. `infra/deploy/compose.yaml`에 `game-api` 전용 `api-egress`를 추가했고, 실행 컨테이너에서도 Google·Kakao·Naver·SSAFY GitLab HTTPS endpoint 연결을 확인했다. SSAFY GitLab OAuth application의 callback도 운영 주소 `https://hanjjak.verte.kr/api/v1/auth/social/ssafy-gitlab/callback`으로 교정했다.
- 2026-09-09 운영이 K3s로 전환된 뒤 `allow-game-api` NetworkPolicy가 PostgreSQL 5432만 허용해 Google·Kakao·SSAFY GitLab callback의 token 교환이 `SOCIAL_PROVIDER_RESPONSE_INVALID`로 실패했다. 동일 자격 증명을 외부 HTTPS가 허용된 진단 Pod에서 사용하면 Google과 GitLab 모두 의도한 `invalid_grant` 공급자 응답까지 도달함을 확인해 credential 문제가 아님을 분리했다. `infra/k8s/base.yaml`에서 `game-api`의 외부 TCP 443 egress를 허용했다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 완료 |
| 개발 | 완료 |
| 검증 | 부분 검증 |
