# 주제별 SSOT 지도

차기 `progression-rebalance-v1`의 버전 경계는 [설계 진입점](../../70-plans/progression-rebalance/README.md)과 [승인 설계](../../superpowers/specs/2026-09-11-progression-rebalance-v1-design.md)에서 찾는다. 최초 클리어 지급 수량의 정본은 [차기 최초 보상](../../60-content/stages/first-clear-rebalance.md)이며 owner_domain은 first-clear-content다. 레벨 목표시간·진행·장비 비용·재료 공급·최초 스킬 해금은 각 기존 SSOT의 차기 절이 소유한다.


아래 표의 책임 문서가 해당 주제의 유일한 정본이다. 보조 문서는 정본을 복사하지 않고 출처·영향·검증 결과만 기록한다.

| 주제 | 책임 문서 | 보조·출처 |
| --- | --- | --- |
| 일일 공동 봉인 레이드·개인 도전·등급·재시도·봉인 기여·순위·보상 | [레이드 SSOT](../../30-domain/raid/ssot.md) | [레이드 콘텐츠 v1](../../60-content/raids/mvp-v1.md), [L-01 작업](../06-delivery/tasks/L-raid/l-01-raid-content-simulation.md), 2026-09-10~13 사용자 기획 대화, [일일 공동 봉인 레이드 승인 설계](../../superpowers/specs/2026-09-13-daily-seal-raid-design.md), [레이드 기획 검토](../../70-plans/raid/README.md) |
| 제품 정의·경험 의도 | [Game Vision](../../10-game/vision.md) | 게임 설계 보고서 원문 |
| 세계관·주인공·핵심 서사 | [Story](../../10-game/story.md) | 사용자 제공 스토리 초안 |
| 핵심 플레이 루프 | [Core Loop](../../20-design/core-loop.md) | Game Vision, 게임 설계 보고서 원문 |
| MVP·배포 범위 | [MVP 범위](../../70-plans/mvp-release/requirements.md) | A-01~A-04 |
| 미래 범위 | [미래 범위](../../70-plans/future-content/brief.md) | 원문 설계 보고서 |
| 계정·캐릭터 식별·서버 진행·반복 대상·최근 전투 기록·로컬 성장·실행 체크포인트·복원 | [계정·저장 SSOT](../../30-domain/player/ssot.md) | B-01~B-08, 데이터 권한 설계, [스테이지 화면 계획](../../70-plans/stage-screen/README.md) |
| 주력 재료 선택·드롭 비율 | [주력 재료 SSOT](../../30-domain/character/ssot.md) | C-01~C-07 |
| 챕터·스테이지·진행·반복 조건 | [스테이지 SSOT](../../30-domain/world/ssot.md) | D-01~D-07, [스테이지 화면 계획](../../70-plans/stage-screen/README.md), 스테이지 설계 원문 |
| 전투 사이클·스탯·피해·실패 | [전투 SSOT](../../30-domain/combat/ssot.md) | E-01~E-06, [스테이지 화면 계획](../../70-plans/stage-screen/README.md), 구형 스탯 원문 |
| 공개 종합전투력·전문별 랭킹 | [전투 SSOT](../../30-domain/combat/ssot.md) | J-15, `apps/game-api` ranking API, `apps/web` ranking 화면 |
| 레벨·경험치·쌀 성장 | [성장 SSOT](../../30-domain/progression/ssot.md) | 게임 설계 보고서, B-02 |
| 아이템 분류·드롭·인벤토리 | [아이템 SSOT](../../30-domain/items/ssot.md) | F-01, F-03~F-06 |
| 주력 재료 드롭 기회·지급 수량·세대 가중치 | [MVP 주력 재료 드롭표 v1](../../60-content/items/material-drops-mvp-v1.md) | 아이템 SSOT, F-02, 2026-09-01 사용자 승인 초안 |
| `progression-rebalance-v1` 최초 클리어 재료·쌀·보석함·스킬·승급 스타터 | [차기 최초 보상](../../60-content/stages/first-clear-rebalance.md) | [승인 설계](../../superpowers/specs/2026-09-11-progression-rebalance-v1-design.md), A-05 |
| 주력 재료 표시명·설명 문구와 장비 표시명 | [MVP 아이템·장비 표시 콘텐츠 v1](../../60-content/items/mvp-v1.md) | 2026-09-02 콘텐츠 바이블 결정, 2026-09-11 사용자 승인 UI 카피 |
| 장비·제작·강화 | [장비 SSOT](../../30-domain/items/equipment/ssot.md) | G-01~G-09, 장비 설계 원문 |
| 스킬·스킬 성장·자동 사용 | [스킬 SSOT](../../30-domain/character/skills/ssot.md) | H-01~H-05, 스킬 설계 원문 |
| 스킬·스킬북 표시명·아이콘·액티브 전투 VFX 확정 시안 | [MVP 스킬 표시 콘텐츠 v1](../../60-content/abilities/mvp-v1.md) | 2026-09-02 콘텐츠 바이블 결정, 2026-09-09 사용자 아이콘 선택, 2026-09-10 사용자 VFX 선택 |
| 보석 장착·합성·옵션·입장권·일일 보스 던전 | [보석 SSOT](../../30-domain/gems/ssot.md) | 2026-08-31 사용자 승인 설계, 보석 콘텐츠 계획 |
| 보석 옵션 값·보석함·던전 보상·기준 빌드·표시명 | [MVP 보석·던전 수치 v1](../../60-content/gems/mvp-v1.md) | 보석 성장·던전 콘텐츠 설계, 2026-09-02 콘텐츠 바이블 결정 |
| 치장 뽑기·등록 재고·성급·도감·세트·외형 착용·200회 선택 상자 | [치장 SSOT](../../30-domain/cosmetics/ssot.md) | [캐릭터·치장·도감 통합 계획](../../70-plans/character-cosmetics-collection/integration-plan.md), 치장 뽑기 요구사항 명세 |
| 치장 배너 비용·확률·활성 풀·부위·성급 임계값·세트 누적 효과 | [MVP 치장 뽑기 콘텐츠 v1](../../60-content/cosmetics/mvp-v1.md) | 치장 SSOT, 통합 계획, 2026-09-02 콘텐츠 바이블 결정 |
| 치장 뽑기·등록·외형 착용 구현 범위·API·검증 | [치장 뽑기 시스템 요구사항 명세서](../../70-plans/cosmetic-gacha/requirements.md) | 치장 SSOT, 통합 계획, MVP 치장 뽑기 콘텐츠 v1 |
| 거래소·시장 품목·주문·수수료·체결·자산 보관·유동성 | [거래소 SSOT](../../30-domain/economy/ssot.md) | I-01~I-08, [거래소 호가창 계획](../../70-plans/market-order-book/README.md), 경제 설계 원문 |
| 웹 클라이언트·챕터/스테이지 화면·설정·관리 UI·저피로 UX | [클라이언트 UX SSOT](../../30-domain/player/ux/ssot.md) | K-01~K-20, [챕터 화면 계획](../../70-plans/chapter-screen/README.md), [스테이지 화면 계획](../../70-plans/stage-screen/README.md), [웹 PIP 계획](../../70-plans/picture-in-picture/README.md), 디자인 자료 |
| 이벤트·Outbox·분석 데이터 | [데이터·이벤트 SSOT](../../40-systems/event-system/ssot.md) | J-01~J-03·J-12, Kafka 실험 |
| 운영·배포·모니터링 | [배포·운영 SSOT](../../50-architecture/operations.md) | J-08~J-10, 배포 준비 회의 |
| 여러 도메인을 가로지르는 인수·검증 | [인수 기준](../../70-plans/mvp-release/acceptance.md) | J-04~J-07·J-11 |
| 모노레포·클라이언트·서버·시뮬레이션 코드 구조와 의존성 경계 | [모노레포 설계](../../50-architecture/monorepo.md) | [코드베이스 기반 결정](../../80-decisions/architecture/codebase-foundation.md), [기반 구축 계획](../../70-plans/codebase-foundation/README.md) |
| API·콘텐츠 계약 생성 | [Content Pipeline](../../50-architecture/content-pipeline.md) | TypeSpec, OpenAPI, JSON Schema, codegen |
| 코드 빌드·CI·관측성·패키징 | [배포·운영 SSOT](../../50-architecture/operations.md) | GitLab CI, Docker Compose, OpenTelemetry |
| 데이터 소유권과 저장 경계 | [데이터 권한과 저장 경계](../../50-architecture/persistence.md) | 기존 저장·권한 설계 요약 |
| 결정 상태·충돌 | [결정 로그](../../80-decisions/README.md) | 날짜별 회의 원문 |
| 작업 상태·완료 증거 | [A–K 작업 색인](../06-delivery/tasks/_index.md) | Notion 작업 페이지 |
| 콘텐츠 파일 배치·버전 | [콘텐츠 구조](../../60-content/schema.md) | 자료실·스테이지 설계 |
| 챕터 1~4 적·보스 수치 | [MVP 적 수치 v1](../../60-content/enemies/mvp-v1.md) | 공통 적 방어력 규칙 반영 재산출 중인 working 콘텐츠, 전투·스킬·스테이지 SSOT |
