# 결정 로그와 미해결 항목

이 문서는 중앙 결정 상태의 정본이다. 기술 구조 결정은 [Architecture Decisions](architecture/README.md), 게임 규칙 결정은 [Game Design Decisions](game-design/README.md)에서 분류 경로를 확인한다.

---
doc_kind: decision
owner_domain: governance
authority_level: ssot-for-decision-status
---

## 판단 규칙

이 문서는 회의 내용을 재작성해 제품 규칙을 만드는 곳이 아니다. 어떤 결정이 현재 SSOT로 전파됐는지와 충돌이 남았는지를 관리한다.

## 현재 결정 상태

### 2026-09-10~11 진행·성장 리밸런싱

최신 출처: 이 작업의 사용자 브레인스토밍 승인. 합의 항목은 confirmed(차기 설계)이며 [승인 설계](../superpowers/specs/2026-09-11-progression-rebalance-v1-design.md)와 [진입점](../70-plans/progression-rebalance/README.md)의 제품 범위·성장·장비·스테이지·아이템·스킬 SSOT, 반복 드롭·최초 보상 정본에 전파했다. 실행 상태는 [A-05](../wiki/06-delivery/tasks/A-release-scope/a-05-progression-rebalance.md)가 소유한다.

`progression-rebalance-v1`은 1~4장·1~79레벨을 절대 수치 범위로 사용한다. 기존 필요 경험치와 권장 레벨 구조를 유지하며 목표시간은 현재의 1/3, P90 상한은 P50의 1.25배다. 노말·희귀·영웅 장비 비용, 20%·30%·50% 단계 배분, 승급 올림, 1~4장 최초 보상, 1장 확정 반복 드롭, 1-1~1-6 직접 스킬 해금, 거래 가능 최초 보상 재료, 쌀 즉시 지급·아이템 pending, 기존 계정 소급과 버전형 clean cutover를 확정했다. 치장 밸런스는 변경하지 않는다.

기존 SSOT 본문의 적용 버전·콘텐츠·전투 replay를 보존하며 새 절은 차기 버전에만 우선한다. 현재 배포 규칙과 차기 규칙을 같은 전투 세션이나 보상 결과에서 섞지 않는다. 영향 작업은 A-03·G-07·G-09 및 A-05에 연결했다.

대화 중 폐기된 안: 최신 세대만 최초 지급, 실제 아이템 개수만 등급별로 일정 비율 증가, 가치 보정 전 희귀 비용, 승급 상위 재료 절반 지급, 최초 보상 재료 계정 귀속, 치장권 스테이지별 추가 지급. 5~10장·80레벨 이후, 전설·신화 장비 절대 비용, M4·M5 신규 공급, 4-10 신규 보상과 레이드 보상은 unresolved다.



2026-09-04 캐릭터 창 사용자 합의는 [UX SSOT](../30-domain/player/ux/ssot.md)의 캐릭터 창 절에 전파했다(confirmed). 기술 API·계산 공유·검증 접근은 [설계 제안](../70-plans/character-window/design.md)이며 검토 전이다. 관련 작업은 구현 시작 시 전용 task와 기존 K-21에 연결한다.

2026-09-04 코드 대조에서 구형 치장 효과 DTO·고정 증가 계산과 최신 치장/전투 SSOT의 차이를 확인했다(unresolved: 구현 정합성). 양쪽 근거와 영향은 위 설계의 기존 코드와의 차이에 보존한다. 기존 구현으로 최신 정책을 대체하지 않으며, 이 문서 변경으로 구현 차이가 해소된 것은 아니다.

| 주제 | 최신 기록 | 상태 | 현재 사용 기준 |
| --- | --- | --- | --- |
| 기본공격 대상 피격 VFX | 2026-09-13 사용자 A안 선택 | confirmed | [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md)의 `백금 교대베기`를 사용한다. 실제 적중 시 몬스터의 보이는 영역 중앙에서 크기에 비례해 재생하고, 우상향·좌상향 단일 사선을 타격마다 교대하며 몬스터 자체를 흔들지 않는다. [비교 시안](../70-plans/basic-strike-target-vfx/README.md)의 B~E안은 이력으로만 보존한다. 실제 게임 연결과 검증은 후속 구현 범위다. |
| 관리자 사용자 상태 조정·감사 조회 | 2026-09-11 사용자 직접 지시 — 사용자 성장·자산·진행·치장·장비 관리와 관리자별 조작 로그 | confirmed | [배포·운영 SSOT](../50-architecture/operations.md)의 `user:manage` 경계를 사용한다. 레벨·누적 경험치, 쌀, 스택 아이템, 보석, 순차 스테이지 해금, 치장 보유, 장비 등급·강화 단계 조정을 허용하되 예약·장착·인벤토리·콘텐츠 불변 조건은 유지한다. 모든 조작은 관리자 ID·GitLab username·사유·멱등 키·전후 상태를 append-only 감사 로그로 남기고 콘솔에서 조회한다. 영향 작업은 J-16이다. |
| 관리자 콘솔 인증 | 2026-09-11 사용자 직접 지시 — GitLab 로그인과 사용자 아이디 화이트리스트 적용 | confirmed | [배포·운영 SSOT](../50-architecture/operations.md)의 SSAFY GitLab OAuth 2.0 Authorization Code + PKCE S256, `read_user` 기반 `GET /api/v4/user` 확인과 GitLab `username` 화이트리스트를 사용한다. 로컬 운영자 비밀번호 로그인은 대체하며 MFA·추가 접근 프록시·최종 세션 수명은 unresolved다. 영향 작업은 J-16이다. |
| 레이드 기본 기획 | 2026-09-13 사용자 설계 승인 | confirmed | [레이드 SSOT](../30-domain/raid/ssot.md)의 전체 서버 일일 공동 봉인, KST 17:30 정산, 고정 목표 50,000, 순차 보상 슬롯 3개·슬롯당 최대 3시도, 5분 개인 자동전투와 5초당 보스 공격력·방어력 5% 복리 강화, 참여~SSS 등급·봉인 기여·순위·개인 및 순위 보상·미수령 보상 규칙을 사용한다. [승인 설계 기록](../superpowers/specs/2026-09-13-daily-seal-raid-design.md)은 선택 근거와 검증 목표를 보존한다. 출시·구현 승인을 의미하지 않으며 구현 요청 시 전용 task를 만든다. |
| 레이드 절대 전투 수치·콘텐츠 | 2026-09-14 Task 11 1,000-seed 분포·인구 경제 투영 | working | [레이드 콘텐츠 v1](../60-content/raids/mvp-v1.md)의 보스 초기 공격력·방어력, 기준 빌드, B 절대 피해 커트라인과 D~SSS 정수표를 사용한다. 기준·공격력/HP/관통 부족 빌드 각각 1,000 seeds의 P10/P50/P90, 등급 분포, B 성공률, 5분 cap과 36/72/144/1,000명 seal·공급·rank·auto-claim 투영은 balance report에 기록했다. Docker-backed settlement latency/lock/deadlock/claim contention 및 실제 브라우저 흐름은 런타임 부재로 미검증이므로 L-04 수직 슬라이스 전까지 working을 유지한다. 보상·봉인 정책은 변경하지 않는다. 영향 작업은 L-01~L-04다. |
| 배포 서버 로컬 이미지 직접 실행 | 2026-09-09 사용자 직접 지시 — image Registry 미운영, 로컬 build·실행, 중간 import 금지 | confirmed | 기존 로컬 Registry 사용 결정과 `docker save`→`ctr import` 제안을 대체한다. 같은 Hyper-V Ubuntu VM의 BuildKit이 K3s containerd worker를 사용해 commit SHA image를 `k8s.io` namespace에 직접 빌드한다. 현재 기준은 [배포·운영 SSOT](../50-architecture/operations.md#배포-서버-로컬-이미지), 영향은 [배포 계획](../70-plans/docker-cloudflare-deployment/README.md)과 J-08·J-09·J-10에 전파한다. Registry 관련 기존 증거는 당시 이력으로만 보존한다. |
| 거래소 외부 보관·보석 칸 잠금·상세 간소화 | 2026-09-08 사용자 직접 승인 | confirmed | 기존 판매 예약의 인벤토리 포함·만료 자동 반환과 보석 개별 잠금·상태 요약 결정을 대체한다. [아이템 SSOT](../30-domain/items/ssot.md), [거래소 SSOT](../30-domain/economy/ssot.md), [보석 SSOT](../30-domain/gems/ssot.md)에 전파했다. 기존 결정과 테스트 기록은 당시 이력으로 보존한다. 영향 작업은 F-04·F-06·I-01·I-02·I-03·I-05·J-14다. |
| 1차 범위 | 2026-08-31 사용자 결정 | confirmed | [MVP 범위](../70-plans/mvp-release/requirements.md)의 챕터 1–4·40스테이지 |
| 챕터 5-10 확장 | 2026-09-14 사용자 결정 | confirmed | [설계](../superpowers/specs/2026-09-14-chapter-5-10-content-balance-design.md)의 챕터 1–10·100스테이지·플레이어 레벨 상한 200. 적 수치는 기하 배율 없이 `apps/balance-lab` 생성기를 연장해 만들고, 일반 몬스터 공격은 기준 빌드 최대 HP 비례(파밍 1.0%·소벽 0.75%·관통벽 0.6%)로 교체하며 보스:일반 공격은 2.0으로 통일한다. 5장부터 일반 HP도 기준 빌드를 따른다. 보상은 기존 공식 자연 연장이고 재료는 M1~M4를 유지한다. 레거시 `v1` 콘텐츠는 40스테이지로 동결한다. 테마·보스명·배경·스프라이트와 전설 스킬북 최초 배치는 후속 범위다. |
| 챕터 화면 | 2026-09-01 사용자 요구·Office Hours 결정 | confirmed | [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md)의 4챕터 단일 월드맵, 현재 전투·진행 목표 분리, 최초 클리어 진행률, 완료 배지, 잠금 조건 안내와 [챕터 화면 계획](../70-plans/chapter-screen/README.md) |
| 스테이지 화면 | 2026-09-01 사용자 요구·Office Hours 결정 | confirmed | [스테이지 화면 계획](../70-plans/stage-screen/README.md)의 클리어·현재·잠금·반복 상태, 주요 보상, `피해 N%`, 해당 스테이지 최근 실패 1건, 즉시 입장과 다음 사이클 반복 적용. 권장·내 스펙·성장벽 표시는 제외 |
| 스테이지 진행·기록 권한 | 2026-09-01 사용자 결정 | confirmed | [계정·저장 SSOT](../30-domain/player/ssot.md)와 [데이터 권한과 저장 경계](../50-architecture/persistence.md)의 현재·최고·최초 클리어·해금·반복 대상과 최근 전투 사건 50건 서버 최종 판정·저장, 로컬 성장·실행 체크포인트 분리 |
| 10스테이지 단독 최종 보스 | 2026-09-10 사용자 직접 지시 | confirmed | [스테이지 SSOT](../30-domain/world/ssot.md)에 따라 각 챕터의 10스테이지는 일반 몬스터 없이 최종 보스 1마리부터 시작한다. 현재 프레젠테이션이 연결된 챕터 1~3은 각각 딸기 시루·로열 어소트먼트 선물 골렘·후토마끼 왕을 사용하며, 영향 작업은 D-02~D-04·D-07·K-06이다. |
| 메인 전투 HUD 진행 표현 | 2026-09-10 사용자 제공 최종 시안·후속 지시 | confirmed | [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md)의 메인 전투 상단 진행 표현을 5개 체크포인트 대신 `현재 처치 수 / 20` 단일 바로 사용 |
| 튜토리얼 다시 보기 진입점 | 2026-09-15 사용자 직접 지시 — 게임 화면 왼쪽에 `튜토리얼 보기` 단추 | confirmed | 전투 화면 HUD 왼쪽 `전투 기록` 아래의 `튜토리얼 보기` 단추로 모험가의 장 기반 9단계 안내를 다시 연다. 다시 보기는 안내만 재생하고 첫 진입 완료 상태를 바꾸지 않는다. [점진형 온보딩 설계안](../70-plans/progressive-onboarding/README.md)의 `설정 > 도움말` 진입점은 폐기하지 않고 남은 범위로 유지한다(unresolved: 두 진입점의 최종 단일화). 실행 상태는 [K-27](../wiki/06-delivery/tasks/K-client-ux/k-27-progressive-onboarding-dialogue.md)이 소유한다. |
| 역할 장 자동 실행 트리거 | [역할 의상별 튜토리얼 장 구성](../70-plans/progressive-onboarding/tutorial-chapters-and-costumes.md)의 `시작 계기` 열과 [전체 콘텐츠 퀘스트·튜토리얼 맵](../70-plans/progressive-onboarding/content-quest-tutorial-map.md) 4.4·4.5·4.6·4.7·4.8·4.9절 | unresolved | 문서가 한 줄로 확정한 계기(최초 제작 가능, 첫 스킬북 획득, 던전 해금 뒤 첫 방문, 보석함·보석 보유, 치장 해금 뒤 첫 방문, 거래소 첫 방문)만 런타임에 연결했다. 같은 표가 `또는`으로 남긴 두 갈래 — 대장장이의 `성장 제안이 필요한 첫 실패`와 장터 상인의 `부족 재료 바로 가기` — 는 판정 기준과 안전 노출 시점이 확정되지 않아 연결하지 않았다. `부족 재료 바로 가기`는 현재 구현상 장비 화면 안의 구매 확인 창이라 거래소 화면에 도달하지 않는다는 점도 함께 확정이 필요하다. 계정 단위 가이드 상태의 서버 저장 계약도 미확정이라 현재는 브라우저 `localStorage`에 계정별로 둔다. 실행 상태는 [K-27](../wiki/06-delivery/tasks/K-client-ux/k-27-progressive-onboarding-dialogue.md)이 소유한다. |
| 설정 화면 저장 경계 | 2026-09-01 사용자 요구·Office Hours 결정 | confirmed | [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md)의 PC 환경 설정 로컬 저장, 계정 단위 설정만 서버 저장, 절전 모드 렌더링 부담 감소, 기존 인증 로그아웃 재사용 |
| 계정 탈퇴·이메일 재사용·캐릭터 카드 | 2026-09-09 사용자 결정 | confirmed | 계정은 `deleted_at` soft delete로 보존하고 이메일·닉네임을 익명화한다. 삭제 계정 이메일은 재사용하며 설정 카드에는 기존 장착 치장 이미지, 없으면 닉네임 첫 글자 fallback을 표시한다. 상세 규칙은 [계정·저장 SSOT](../30-domain/player/ssot.md)와 B-01 작업 증거를 따른다. |
| 강화 방식 | 2026-08-31 사용자 승인 설계 | confirmed | 장비는 확정 강화, 스킬은 2~10강 `70→20%` 단계별 확률과 목표별 `+5%p`, 최대 100% 실패 보정 사용 |
| 장비 거래 | 2026-08-31 최신 설계 동기화 | confirmed | 완성 장비 거래 제외, 재료와 스킬북 거래 |
| 영구 장비 성장·비용 | 2026-09-08 사용자 최종 승인 | confirmed | [장비 SSOT](../30-domain/items/equipment/ssot.md)의 계정×부위 단일 영구 상태, 최초 제작 1회, 등급별 강화, 챕터 조건 승급과 서버 계산 비용을 사용한다. [승인 설계](../superpowers/specs/2026-09-08-permanent-equipment-progression-design.md)가 2026-09-03 반복 제작·장착과 챕터 1 비용 working 기준을 대체한다. |
| 장비 능력치 단계별 성장 보장 | 2026-09-11 사용자 명세 승인 | confirmed | [장비 SSOT](../30-domain/items/equipment/ssot.md)의 모든 강화 주 능력치 최소 +1, 공격력·최대 HP 동일 단계의 `희귀 > 노말`, `영웅 > 희귀`, `전설 > 영웅`, 승급 1강의 직전 등급 30강 초과와 서버 단일 정수 계산을 사용한다. [승인 설계](../superpowers/specs/2026-09-11-equipment-stat-growth-guarantees-design.md)에 전파했다. |
| 영구 장비 이벤트 식별·승급 | 2026-09-08 사용자 충돌 해결 결정 | confirmed | 장비 해금·강화·승급 이벤트는 인스턴스 UUID가 아니라 `equipmentSlot`과 계정·부위 기반 안정 aggregate ID를 사용한다. 승급은 `EQUIPMENT_PROMOTED`로 분리하고 영구 장비 트랜잭션 안에서 Outbox에 기록한다. 상세 계약은 [데이터·이벤트 SSOT](../40-systems/event-system/ssot.md)와 `packages/contracts/events.tsp`를 따른다. |
| 스킬 성장 | 2026-08-31 사용자 승인 설계 | confirmed | [스킬 SSOT](../30-domain/character/skills/ssot.md)의 6종·4등급·각 1~10강·전용 책·쌀 비용 모델 |
| 스킬북 등급별 전체 종류 드롭 | 2026-09-14 사용자 직접 결정 | confirmed | [스킬 SSOT](../30-domain/character/skills/ssot.md)에 따라 등급이 등장하는 첫 스테이지부터 스킬 6종 전체를 동일 확률로 선택한다. 희귀는 2-1, 영웅은 4-1, 전설은 향후 6-1부터 적용한다. |
| 스킬 성장 MVP 비용 | 2026-09-03 구현 기준 | working | [스킬 SSOT](../30-domain/character/skills/ssot.md)의 노말 해금 100쌀, 노말 2~10강 `30 × 현재 강화 단계` 쌀, 같은 스킬 노말 스킬북 1개 비용을 최초 스킬 수직 슬라이스 기준으로 사용한다. 희귀 이상 승급과 자연 획득량 환산 비용표 검증 전까지 확정 밸런스로 보지 않는다. |
| 스킬 자동 사용 | 2026-08-31 사용자 승인 설계 | confirmed | 액티브 4슬롯을 사용자 순서대로 검사해 사용 가능한 첫 스킬이 행동을 대체하고, 없으면 기본공격 |
| 스킬 상태 권한 | 2026-08-31 사용자 결정 | confirmed | 해금·등급·단계·목표별 실패 보정·액티브 장착 순서는 서버가 최종 권한을 가지며 로컬은 서버 확정 상태를 캐시 |
| 기본 화폐 명칭 | 2026-08-31 사용자 결정 | confirmed | 프로젝트 전체 기본 화폐의 표시명과 현재 문서 용어를 `골드`에서 `쌀`로 변경 |
| 치장 뽑기·성장 MVP 범위 | 2026-09-01 사용자 최종 설계 승인 | confirmed | 2026-09-02 통합 결정 이후 현재 구현 기준은 [캐릭터·치장·도감 통합 계획](../70-plans/character-cosmetics-collection/integration-plan.md), [치장 SSOT](../30-domain/cosmetics/ssot.md), [치장 요구사항 명세](../70-plans/cosmetic-gacha/requirements.md)다. 기존 승인 기록은 유지하되 병렬 승인 입력과 충돌할 때 통합 계획과 SSOT를 최신 우선순위로 사용한다. |
| 캐릭터·치장·도감 통합 결정 | 2026-09-02 사용자 통합 결정 | confirmed | 2026-09-01 병렬 승인 입력과 치장 뽑기 승인 설계의 충돌은 [통합 계획](../70-plans/character-cosmetics-collection/integration-plan.md)으로 해소했다. 규칙 상세는 결정 로그에 복제하지 않고 [치장 SSOT](../30-domain/cosmetics/ssot.md)와 책임별 SSOT를 따른다. |
| 치장 뽑기 콘텐츠 수치 | 2026-09-12 사용자 1,000회 체감 기반 승급 밸런스 조정 | confirmed | [MVP 치장 뽑기 콘텐츠 v3 밸런스 임시 카탈로그](../60-content/cosmetics/mvp-v1.md)의 번호형 영구 ID 66종, 임시 11세트 배치 `노말 4·희귀 3·영웅 3·전설 1`, 1회 5,000쌀, 등급 확률 `50:35:14:1`을 사용한다. 성급 누적 임계값은 노말 `1/3/6/10/18`, 희귀 `1/3/6/12/24`, 영웅 `1/2/4/8/16`, 전설 `1/2/3/5/10`이다. 이는 2026-09-07의 비전설 대량 임계값을 대체하며 실제 명칭·이미지와 전설 고유 효과는 미정이다. 영향 작업은 K-21이다. |
| 전설 치장 선택 상자 범위 | 2026-09-11 사용자 결정 | confirmed | 선택 상자는 지급 배너의 픽업 세트로 한정하지 않고 활성 카탈로그에 존재하는 모든 `LEGENDARY` 치장 중 하나를 지급한다. 선택 화면은 전설 세트 선택 후 해당 세트의 6부위 중 하나를 고르는 2단계 흐름을 사용하며 상세 규칙은 [치장 SSOT](../30-domain/cosmetics/ssot.md)를 따른다. |
| 치장 세트 일시 제외 | 2026-09-13 사용자 결정 | confirmed | 간호사(`cosmetic-set-04`)와 마법사(`cosmetic-set-09`)는 아트가 완성본이 아니라 게임에서 뺀다. 콘텐츠 파일에서 지우지 않고 세트의 `active: false`로 표시하며, 서버가 콘텐츠를 읽을 때 해당 세트와 구성품을 제외한다. 치장 ID가 연속이어야 해 항목을 지우면 저장된 보유 기록이 어긋나기 때문이다. 수치 정본은 [MVP 치장 뽑기 콘텐츠](../60-content/cosmetics/mvp-v1.md)다. 영향 작업은 K-21이다. |
| 치장 하의·무기 슬롯 정합성 | 2026-09-11 사용자 UI 지시 — 하의 제거·무기 표시 | unresolved | 현재 [치장 SSOT](../30-domain/cosmetics/ssot.md)와 서버 계약의 정식 슬롯은 `BOTTOM`이지만 정적 에셋 프리뷰는 `WEAPON`을 사용한다. 캐릭터 치장 UI는 사용자 시안에 맞춰 `무기`로 표시하되 운영 카탈로그에서는 `BOTTOM`, 프리뷰 카탈로그에서만 `WEAPON`을 호환 매핑한다. 슬롯 계약 자체의 교체 여부는 확정하지 않았으며 영향 작업은 K-21이다. |
| 주력 재료 | 2026-09-01 사용자 명세 검토 | confirmed | [주력 재료 SSOT](../30-domain/character/ssot.md)의 감자·고구마·옥수수, 최초 로그인 직후 1회 선택, 선택 전 게임 진행 차단, 모든 세대에서 선택 재료 80%·나머지 10% 계열 추첨, 선택 후 변경 불가와 같은 재료 재요청 성공 수렴 |
| 공개 종합전투력·전문별 랭킹 | 2026-09-09 사용자 승인 설계·구현, 2026-09-10 사용자 v2 승인 | confirmed | `docs/30-domain/combat/ssot.md`의 `combat-power-v2`를 사용한다. 최대 HP와 방어력 100 표준 대상에 대한 60초 기대 DPS를 1:2 비중으로 정규화하고, 메인 전투에 장착한 액티브와 모든 영구 전투 효과를 반영한다. 서버 계산·`ranking_entry` 읽기 모델, 감자·고구마·옥수수 통합·전문별 TOP과 내 순위를 사용하며 시즌·보상·실시간·Kafka 확장은 후속 범위다. |
| 주력 재료 세대 | 2026-09-10 사용자 결정 | confirmed | 거래소·인벤토리 카탈로그는 감자·고구마·옥수수 각 M1~M5를 사용하고 표시 이름은 `미니/기본/알찬/황금/전설` 순서다. 챕터 1~4 드롭 풀은 현재 M1~M4를 유지하며 M5 공급처·장비 소비처는 unresolved다. |
| 주력 재료 드롭 수치 | 2026-09-01 사용자 승인 초안, 2026-09-11 1장 리밸런싱 승인 | conflict-aware | [재료 드롭 콘텐츠](../60-content/items/material-drops-mvp-v1.md)의 `progression-rebalance-v1`은 1장 1~9에서 일반 처치 M1 2개·보스 처치 M1 10개를 확정 지급하고 계열 80:10:10만 추첨한다. 2~4장은 현재 15% 성공률·성공당 10개와 기존 세대 가중치를 유지하되 시뮬레이션 전까지 working이다. 현재 배포 v1과 차기 1장 수치를 섞지 않는다. |
| 핵심 콘텐츠 표시명 | 2026-09-07 사용자 구현 우선 결정 | confirmed | 재료·장비·스킬·보석 표시명은 기존 [MVP 콘텐츠 작명 규칙 v1](../60-content/style-mvp-v1.md)과 책임별 콘텐츠 문서를 따른다. 치장 66종의 실제 표시명·이미지는 미정이며 [MVP 치장 뽑기 콘텐츠 v2 임시 카탈로그](../60-content/cosmetics/mvp-v1.md)의 `치장 #NNN`·`세트 #NN`·`이미지 준비 중` fallback만 사용한다. fallback은 영구 콘텐츠 이름이 아니다. |
| 콘텐츠 바이블 진행 경계 | 2026-09-02 사용자 범위 결정 | confirmed | [MVP 콘텐츠 바이블 설계](../superpowers/specs/2026-09-02-mvp-content-bible-design.md)와 책임별 SSOT·콘텐츠 문서 확정까지만 진행한다. 구현 계획, 소스 코드, 패키지 매니페스트, JSON Schema, 런타임 콘텐츠 JSON, DB migration, API, UI와 배포 파일은 사용자가 구현을 명시적으로 요청하기 전에는 만들거나 수정하지 않는다. |
| 주력 재료 요구사항 명세 | 2026-09-01 사용자 최종 승인 | confirmed | [최초 주력 재료 선택 요구사항 명세서](../70-plans/material-preference/requirements.md)의 기능 요구사항 32개와 필요한 API 목록 2개를 구현 범위·검증·추적성 기준으로 사용하며, [Notion 게시용 독립 명세](../70-plans/material-preference/material-requirements-notion.md)를 공유본으로 동기화하고 드롭 수치는 working 콘텐츠 버전을 입력으로 사용한다. API DTO·HTTP 상태·공통 인증·오류 envelope는 후속 API 계약에서 확정 |
| 보석 성장 | 2026-09-13 사용자 프리셋·일괄합성 UX 확정 | confirmed | [보석 SSOT](../30-domain/gems/ssot.md)의 1-5 최초 클리어 해금·기본 6슬롯 프리셋 3개·5,000쌀 단위 순차 해금·최대 10개·사용자 이름·전투 유형별 단일 연결, 1~7레벨·3:1 합성·목표 레벨과 옵션 필터 기반 연속 일괄합성·레벨별 옵션 풀·특수 옵션 종류별 최고값 적용, 같은 보석의 복수 프리셋 등록, 특수 옵션 보석 자동 잠금과 수동 잠금·해제, 메인 연결 프리셋 변경 시 현재 스테이지 0/20 재시작 지원 |
| 보석함·거래 | 2026-09-01 사용자 명세 검토 | confirmed | 보석함은 귀속, 결과 보석은 거래 가능, 결과 확률은 `85:10:5`, 1개·지정 수량·전체 개봉을 지원하고 일괄 개봉은 원자적으로 처리하며 판매 등록 전 사용자가 모든 프리셋과 잠금을 직접 해제 |
| 보석 인벤토리 | 2026-09-08 사용자 중첩 정책 직접 승인 | confirmed | 2026-09-01의 보석 인스턴스당 한 슬롯 결정을 동일 속성 보석 중첩으로 대체했다. 그룹 기준·상한·개별 상태 보존은 [아이템 SSOT](../30-domain/items/ssot.md#인벤토리-슬롯과-수량)에 전파했다. 기존 지시서와 단계 2 개별 슬롯 구현은 이전 기준의 기록이며 새 정책의 구현 증거가 아니다. 영향 작업은 F-04·F-05·F-06이며 구현 정합성 수정은 남아 있다. |
| 강화 재료 중첩 상한 | 2026-09-08 사용자 중첩 정책 직접 승인 | confirmed | 동일 아이템별 강화 재료 중첩 상한과 초과 수량의 추가 슬롯 점유를 [아이템 SSOT](../30-domain/items/ssot.md#인벤토리-슬롯과-수량)에 전파했다. 다른 스택 아이템의 상한은 이번 결정 범위가 아니다. 영향 작업은 F-04·F-05·F-06이며 지급·소비·거래·조회 구현과 경계값 검증은 남아 있다. |
| 보석 보스 순환 | 2026-09-12 사용자 승격 승인 | confirmed | `gem-dungeon-v2-accelerated-applied`부터 생존형→폭주형→장갑형을 KST 정각 기준 1시간마다 교체한다. 도전 시작 시 시간 슬롯·보스·단계·프리셋·콘텐츠·보상을 스냅샷해 슬롯이 바뀌어도 해당 도전까지 처리한다. 이전 `gem-dungeon-v1-applied`의 KST 3일 순환은 변경하지 않고 재현용으로 보존한다. |
| 보석 입장권 | 2026-09-13 사용자 변경 확정 | confirmed | 신규 정식 기본값은 최초 3장, 보스 교체와 같은 KST 매시 정각에 1장 충전, 최대 3장이다. 성공·소탕 차감, 실패·사용자 중단·앱 종료·연결 끊김 미차감·미보상과 도전 종료 시 보스 프리셋 잠금 해제는 유지한다. 이전 v1과 v2 승격 당시 값은 재현 기록으로 보존한다. |
| 보석 콘텐츠 요구사항 명세 | 2026-09-01 사용자 최종 승인 | confirmed | [보석 콘텐츠 요구사항 명세서](../70-plans/gem-content/implementation-requirements.md)의 기능 요구사항 68개와 필요한 API 목록 12개를 구현 범위·검증·추적성 기준으로 사용하며, API DTO·상태·오류 코드와 공통 인증·재시도 계약은 후속 API 계약에서 확정 |
| 코드베이스 기반 구조 | 2026-09-02 사용자 결정 | confirmed | [코드베이스 기반 아키텍처 결정](./architecture/codebase-foundation.md), [모노레포 설계](../50-architecture/monorepo.md)의 pnpm·Gradle Kotlin DSL 혼합 workspace, React·Vite 앱, Kotlin·Spring Boot 실행 앱, TypeSpec 계약 정본, Kotlin sim-core·TypeScript 예측기, PostgreSQL·Spring Data JDBC·Testcontainers, GitLab CI 기준 |
| 클라이언트 코드 구조 | 2026-09-02 사용자 결정 | confirmed | [Client](../50-architecture/client.md)의 기능 단위 수직 슬라이스, TanStack Query 서버 상태, Zustand runtime 상태, primitive 전용 공통 UI, Vitest·Testing Library·Playwright 기준 |
| 서버 모듈 구조 | 2026-09-02 사용자 결정 | confirmed | [Server](../50-architecture/server.md)의 Kotlin·Spring Boot, 도메인별 Gradle 모듈과 `api/application/domain/infrastructure`, ArchUnit 경계, 모듈별 Flyway migration 소유 기준 |
| 계약·생성 전략 | 2026-09-02 사용자 결정 | confirmed | [Content Pipeline](../50-architecture/content-pipeline.md)의 TypeSpec 유일 원본, OpenAPI·JSON Schema 커밋, TypeScript client와 Kotlin Spring interface·DTO 빌드 생성 기준 |
| 시뮬레이션 구현 | 2026-09-02 사용자 결정 | confirmed | [Simulation](../50-architecture/simulation.md)의 Kotlin 판정 정본, TypeScript 예측기, 정수 연산·RNG·버전형 golden replay 동등성 검증 기준 |
| 빌드·CI·관측성 | 2026-09-02 사용자 결정 | confirmed | [배포·운영 SSOT](../50-architecture/operations.md)의 LTS toolchain 고정, Docker Compose, container image, GitLab CI merge gate, OpenTelemetry·구조화 JSON 로그 기준 |
| API·인증·멱등성 계약 | 2026-09-01 사용자 결정 | confirmed | [Networking](../50-architecture/networking.md)의 HttpOnly 세션 쿠키, 동일 사이트 경로, Origin 검증, 공통 envelope, UUIDv4 멱등성 키, 7일 결과 보존, command 조회, 계정 stateVersion, MVP API 계약 범위 |
| 메인 전투 동기화 | 2026-09-10 사용자 결정 | confirmed | [Networking](../50-architecture/networking.md)의 서버 결정론 시뮬레이션, 몬스터 처치별 경험치·쌀·아이템 원자 정산, 사이클 단위 HP·스킬·입력 유지, 보스 처치 시 클리어·해금·방치모드 적용, 단일 활성 세션, 30초 heartbeat·90초 무신호 만료를 사용한다. 기존 2026-09-01 사이클 종료 일괄 정산은 이 결정으로 대체한다. |
| 저장소·트랜잭션 경계 | 2026-09-01 사용자 결정 | confirmed | [데이터 권한과 저장 경계](../50-architecture/persistence.md)의 PostgreSQL·Flyway·snake_case/UUID, IndexedDB, 유스케이스 단일 트랜잭션, 낙관적 잠금, 거래 매물 행 잠금, PostgreSQL Outbox polling, immutable 콘텐츠 파일 로드, 로컬 마이그레이션 자동 초기화 |
| DB 논리 모델·성장 권한 | 2026-09-01 사용자 결정 | confirmed | Account 1:1 Character, 스택/인스턴스 인벤토리, 서버 레벨·경험치·쌀, 계정별 스테이지 기록, 전투 세션·command·Outbox·보석 프리셋 분리 모델 |
| 콘텐츠 JSON 스키마·버전 | 2026-09-01 사용자 결정 | confirmed | [콘텐츠 구조와 버전](../60-content/schema.md)의 JSON+JSON Schema, 도메인별 파일·manifest checksum, 안정적 문자열 키, 정수 수치·basis point, loader 전체 참조 검증, working/applied 표시, 세션 시작 버전 고정 |
| 보석 수치 콘텐츠 | 2026-09-12 사용자 승격 승인 | conflict-aware | 1-5~3-7 목표의 가속 베타 보스 수치를 신규 `gem-dungeon-v2-accelerated-applied`로 승격한다. 이전 `gem-dungeon-v1-applied`와 베타 원본은 식별자·파일·수치를 변경하지 않고 보존한다. [MVP 보석·던전 수치](../60-content/gems/mvp-v1.md)의 옵션·보상과 보석 성장 분포·메인 적 재산출은 계속 working이다. |
| 스킬 표시명 | 2026-09-10 사용자 확정 | confirmed | [MVP 스킬 표시 콘텐츠 v1](../60-content/abilities/mvp-v1.md)의 액티브 표시명을 `한짝의 일격`, `마! 쫄이나`, `잘게 더 잘게!`, `화력 최대로!`로 사용한다. 안정 ID, 효과 수치와 10초 재사용시간은 변경하지 않는다. |
| MVP 스킬 아이콘 시안 | 2026-09-09 사용자 선택 `3·3·3·1·1·1` | confirmed | [MVP 스킬 표시 콘텐츠 v1](../60-content/abilities/mvp-v1.md)의 표시 순서대로 한짝의 일격 C안, 마! 쫄이나 C안, 잘게 더 잘게! C안, 화력 최대로! A안, 회심의 간 A안, 오늘의 특선 A안을 확정 시안으로 사용한다. 런타임 투명화·리사이즈·UI 연결과 검증 상태는 [K-12](../wiki/06-delivery/tasks/K-client-ux/k-12-skill-management-ui.md)가 소유한다. |
| 스킬북 인벤토리 식별·상세 위계 | 2026-09-11 사용자 직접 승인 | confirmed | 스킬북은 공용 책 실루엣에 같은 `skillId`의 확정 스킬 아이콘 배지를 합성해 스킬별로 구분한다. 인벤토리 상세는 `스킬북`과 등급 배지를 먼저 표시하고 카탈로그명의 등급 접두사를 뺀 고유 비법서 이름을 별도 제목으로 표시한다. 품목 ID와 서버 카탈로그 표시명 규칙은 유지하며 [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md)와 [MVP 스킬 표시 콘텐츠 v1](../60-content/abilities/mvp-v1.md)에 전파했다. 영향 작업은 K-09다. |
| MVP 액티브 전투 VFX 시안 | 2026-09-10 사용자 최종 선택 | confirmed | [MVP 스킬 표시 콘텐츠 v1](../60-content/abilities/mvp-v1.md)의 8프레임 시안으로 한짝의 일격은 황금 고리·교차 섬광, 마! 쫄이나는 눈 없는 붉은 소스 응축 고리, 잘게 더 잘게!는 청록 상승 화살표, 화력 최대로!는 가스 화구·푸른 불꽃 연출을 사용한다. 런타임 연결과 검증 상태는 [H-05](../wiki/06-delivery/tasks/H-skills/h-05-skill-combat-validation.md)가 소유한다. |
| 전투 스탯 | 2026-08-31 사용자 결정 | confirmed | [전투 SSOT](../30-domain/combat/ssot.md)의 공격·HP·관통 3축과 치명타 스킬 |
| 공통 적 레벨·방어력 | 2026-09-01 사용자 결정 | confirmed | [스테이지 SSOT](../30-domain/world/ssot.md)의 스테이지별 공통 적 레벨·방어력. 기존 기준 레벨과 보스 방어력을 일반·보스 모두에 적용 |
| 40스테이지 수치 | 2026-09-02 결정론 시뮬레이션 검증 | confirmed | [MVP 적 수치 v1](../60-content/enemies/mvp-v1.md)에서 공통 방어력을 반영해 재산출한 `enemy-v1-applied` 콘텐츠를 사용한다. 기준 빌드 seed 1,000개와 성장축 부족 회귀 결과는 [검증 결과](../70-plans/mvp-release/verification/enemy-v1-applied.csv)에 보존한다. |
| 보석 포함 3-1~4-10 수치 | 2026-09-01 사용자 명세 검토 | working | 메인 전용 6슬롯 프리셋의 보석 기대 공급과 공통 적 방어력을 포함한 별도 적 콘텐츠 버전을 사건 시뮬레이션해 기존 성장벽을 보존 |
| 레벨·경험치 | 2026-08-31 사용자 결정 | confirmed | [성장 SSOT](../30-domain/progression/ssot.md)의 최대 500레벨·선형 필요 경험치 |
| 쌀 획득 수치 | 2026-09-03 구현 기준 | working | [성장 SSOT](../30-domain/progression/ssot.md)의 스테이지 글로벌 인덱스 기반 일반 몬스터 `I`쌀·보스 `10I`쌀을 MVP working 공급 기준으로 사용한다. 제작·스킬 비용표와 경제 지표 검증 전까지 확정 밸런스로 보지 않는다. |
| 실행 중 진행·오프라인 보상 | 2026-09-14 사용자 시간·비율·스테이지 기준 확정 | confirmed | 새로고침은 저장한 게임 세션을 heartbeat로 재개하고, 저장값을 잃은 새 문서도 서버가 1분 미만의 기존 활성 세션을 재사용한다. 실제 오프라인 경과시간은 서버 UTC의 마지막 heartbeat 또는 로그아웃 시각과 첫 재로그인 시각 차이로 계산한다. 1분 미만은 지급하지 않고 1분 단위로 내림해 최대 480분(8시간)까지 인정한다. 보상은 복귀 시점의 현재 스테이지를 1분 자동전투했을 때 얻는 쌀·경험치·주력 재료 기대량의 50%를 인정 분 수만큼 지급하며 실제 경과시간과 보상 적용시간을 함께 표시한다. 영향 작업은 B-13이다. |
| 거래소 품목 찾기 계층화 | 2026-09-11 사용자 탐색 고도화 요청 | confirmed | 탐색 UX를 [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md#거래소)에 전파했다. 시장 품목 동일성은 [거래소 SSOT](../30-domain/economy/ssot.md#거래-대상과-시장-품목)를 유지하며 구현·검증 증거는 [K-13](../wiki/06-delivery/tasks/K-client-ux/k-13-marketplace-search-price-ui.md)이 소유한다. |
| 거래소 호가창 수량 그래프 | 2026-09-11 사용자 가격 X축·수량 Y축 그래프와 요약 카드 제거 요청 | superseded | 당시 통합 막대 그래프 결정과 검증은 [K-13](../wiki/06-delivery/tasks/K-client-ux/k-13-marketplace-search-price-ui.md)에 보존한다. 같은 날 후속 시각화 재설계 요청으로 아래 시장 깊이 차트 결정이 표현 방식을 대체하며 요약 카드 제거는 유지한다. |
| 거래소 시장 깊이 시각화 | 2026-09-11 사용자 호가창 재디자인 및 표 대신 시각화 요청 | confirmed | 막대 그래프와 표 제안 대신 [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md#거래소)의 시장 깊이 시각화를 적용한다. 거래 규칙과 API depth 계약은 유지하고 [K-13](../wiki/06-delivery/tasks/K-client-ux/k-13-marketplace-search-price-ui.md)에 영향과 검증을 기록한다. |
| 호가별 가격·잔량 상시 표시 | 2026-09-11 사용자 hover·하단 상세에 의존하지 않는 그래프 숫자 표시 요청 | superseded | 숫자 상시 표시는 유지하되 별도 라벨·연결선 배치는 아래 영역 내부 표시 결정으로 대체한다. 당시 구현·검증은 [K-13](../wiki/06-delivery/tasks/K-client-ux/k-13-marketplace-search-price-ui.md)에 보존한다. |
| 매물대 영역 내부 가격·잔량 | 2026-09-11 사용자 별도 라벨 대신 매물대 영역 내부 정보 표시 요청 | superseded | 영역 내부 숫자 표시는 유지하되 최소 폭·내부 스크롤 배치는 아래 정보 밀도 결정이 대체한다. 당시 구현·검증은 [K-13](../wiki/06-delivery/tasks/K-client-ux/k-13-marketplace-search-price-ui.md)에 보존한다. |
| 호가창 한 프레임 정보 밀도 | 2026-09-11 사용자 셀 크기·가로 스크롤 축소와 양쪽 영역 동시 표시 요청 | confirmed | [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md#거래소)에 프레임 내 양쪽 호가 표시와 압축 배치를 전파했다. 기존 영역 내부 숫자 표현·거래 규칙·API 계약은 유지하며 [K-13](../wiki/06-delivery/tasks/K-client-ux/k-13-marketplace-search-price-ui.md)에 검증 증거를 기록한다. |
| 데스크톱 호가창 방향 판정 수정 | 2026-09-11 사용자 데스크톱에서도 낮은 상하 차트가 표시된다는 보고 | confirmed | 차트 컨테이너 폭에 따라 방향을 바꾼 구현을 수정하고, [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md#거래소)에 거래소 기존 viewport 기준을 명시했다. 영역 내부 정보·거래 규칙·API 계약은 유지하며 [K-13](../wiki/06-delivery/tasks/K-client-ux/k-13-marketplace-search-price-ui.md)에 수정·검증을 기록한다. |
| 거래소 양방향 호가창 전환 | 2026-09-11 사용자 Office Hours 결정 | confirmed | 기존 개별 판매 매물 중심 화면과 판매 가격 체결 규칙을 대체하고 [거래소 SSOT](../30-domain/economy/ssot.md)에 전파했다. 간편 IOC와 단일 품목 고급 호가창, 매수·매도 8시간 지정가, maker 가격 체결, 엄격한 가격·시간 우선, 10~999,999쌀·1쌀 단위, 고정 수량 상한 없음, 매수·매도 합산 활성 주문 30개, 주문 mutation 분당 30회, 체결별 판매 수수료 10% 내림, 통합 물품 수령함과 종료 주문 30일 조회를 사용한다. 기존 판매 주문은 유지하고 활성 구매 예약은 취소·환급해 새 가격 규칙을 소급하지 않는다. 화면 근거와 전환 범위는 [호가창 Office Hours 설계](../70-plans/market-order-book/design.md), 실행 상태는 I-01~I-08·J-07·K-13·K-14가 소유한다. 자동 유동성 공급·가격 보호·차등 수수료·실시간 push는 후속 범위이고, 관리자가 직접 실행하는 운영 주문은 별도 `주문장 관리자 운영` 결정에 따른다. |
| 간편 거래·호가창 역할 분리 최초 제안 | 2026-09-11 최초 개선 계획과 후속 사용자 정정 | superseded | 최초 계획은 unresolved였으며 SSOT에 전파하지 않았다. 가격 입력 제거·최저 가격대 한정·자동 단가 선택 제안은 후속 사용자 기준으로 철회했다. 정정 근거는 [개선 계획의 사용자 근거](../70-plans/market-order-book/trading-modes-plan.md#사용자-근거와-앞선-해석의-정정)에 보존한다. |
| 간편 거래 경험 기준 | 2026-09-11 사용자 테스트 결과 보고와 가격대 물량 표현 명확화 | confirmed | 사용자가 명시한 경험 기준을 [UX SSOT](../30-domain/player/ux/ssot.md#거래소)에 전파했다. [개선 계획](../70-plans/market-order-book/trading-modes-plan.md)은 그 기준에 맞게 교체했으며 특정 목록 형식이나 신규 구현을 승인한 것은 아니다. 체결·자산 정본인 [거래소 SSOT](../30-domain/economy/ssot.md)는 유지한다. 영향 작업은 [K-13](../wiki/06-delivery/tasks/K-client-ux/k-13-marketplace-search-price-ui.md)·[K-14](../wiki/06-delivery/tasks/K-client-ux/k-14-listing-management-settlement-ui.md)다. |
| 간편 가격대 물량 표시 상세안 | 2026-09-11 사용자 기준을 반영한 계획 수정 | unresolved | [개선 계획](../70-plans/market-order-book/trading-modes-plan.md)의 가격대 행·누적 표시·입력 연동·추가 가격대 조회·모드 초안 전환의 구체 배치는 candidate다. 사용자 확정 경험 기준과 구분하며 상세 정책은 아직 SSOT에 전파하지 않았다. 영향은 UX SSOT·Networking·I-03·I-05·I-06·K-13·K-14다. |
| 거래소 주문장 저장·동시성·복구 구조 | 2026-09-11 사용자 후속 Office Hours 결정 | confirmed | [거래소 주문장 아키텍처 결정](./architecture/market-order-book.md)에 UUIDv5 시장 품목, 카탈로그 동기화 선생성·비활성화, 단일 매수·매도 주문 테이블, 시장 품목 row 잠금과 `SKIP LOCKED` 미사용, 원본 주문 SQL depth 집계, 30일 성공 mutation 이력, 계정 공용 항목별 읽음 cursor, 행 단위 delivery 이관과 불일치 주문 격리를 확정했다. 최초 hot instrument 동시 100건은 오류·timeout 0, p95 3초 이하와 엄격한 우선순위·자산 정합성을 함께 만족해야 한다. 비정상 가격 수치 기준만 30일 데이터 전까지 unresolved다. |
| 주문장 관리자 운영 | 2026-09-11 사용자 직접 지시와 호가창 전환 병합 | confirmed | [거래소 SSOT](../30-domain/economy/ssot.md)의 `market:manage` 권한으로 활성 주문 잔량 강제 취소, 사용자 매도 주문 시스템 구매, `MATERIAL`·`SKILL_BOOK` 시스템 매도 주문 등록을 허용한다. 모든 명령은 운영 사유·멱등 키·감사·주문장 이벤트를 남기며 자동 유동성 공급, 체결 소급 취소와 보석 인스턴스 생성은 허용하지 않는다. |
| 메인 전투 방치모드 | 2026-09-01·2026-09-10 사용자 결정 | confirmed | [스테이지 SSOT](../30-domain/world/ssot.md)의 자동 진행·스테이지 반복 2종, 수동 반복은 해금 1~9만 허용, 입장은 즉시·모드/반복 변경은 다음 사이클 적용. 두 모드는 메인 HUD 상단에서 직접 전환하는 이미지 토글로 표시한다. |
| 메인 전투 UX·알림 | 2026-08-31·2026-09-10·2026-09-11 사용자 결정 | confirmed | [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md)의 HUD·체류 획득·내비게이션·레드닷·우편 배지. 좌상단 프로필·우상단 관리 메뉴·하단 성장 메뉴는 전투·거래소·랭킹에서 공통 사용하고 하단 보석은 1-5 최초 클리어 전 잠금, 서버 해금 후 활성 상태로 표시한다. 랭킹 화면에서는 `치장 뽑기`를 포함한 상단 여섯 관리 메뉴 아래 `설정`과 같은 열에 `전투` 복귀 버튼을 둔다. |
| 인벤토리 아이템 세부 분류 | 2026-09-02 사용자 콘텐츠 바이블 승인 | confirmed | [아이템 SSOT](../30-domain/items/ssot.md)의 MVP 표시 분류는 강화 재료, 스킬북, 보석, 보석함, 치장 선택 상자다. 주문서와 기타 성장 아이템은 MVP에서 사용하지 않으며 미래 기능으로 추가할 때 별도 결정한다. |
| 인벤토리 용량 | 2026-09-01 사용자 명세 검토·Office Hours 확인 | confirmed | [아이템 SSOT](../30-domain/items/ssot.md)의 확장 없는 고정 200슬롯 계정 공용 인벤토리, 아이템 종류·스택 슬롯 기준 용량, 소비·지급 전 수용량 검증 사용 |
| 인벤토리 조회·초과 보상 | 2026-09-08 사용자 제공 INVENTORY-WORK-PLAN.md의 2026-09-04 팀 합의 D3와 단계 2 진행 승인 | confirmed | 기존 2026-09-01 초과분 폐기 결정을 D3로 대체하고 [아이템 SSOT](../30-domain/items/ssot.md)의 공간 부족 시 아이템 지급 건너뛰기·알림과 전투·성장 보상 지속에 전파했다. 조회 계약은 유지하며 영향 작업은 F-04·F-05·F-06이다. |
| 빠른 전투 | 2026-08-31 사용자 결정 | confirmed | 시스템과 보상이 없으며 MVP UI와 미래 예고에서도 제외 |
| 확장 창·펫 창 | 2026-09-13 사용자 플로팅 오버레이 직접 지시 | confirmed | MVP는 단일 브라우저 탭·단일 문서 UI를 유지한다. PIP는 지원 브라우저의 선택 기능이며 실패 시 단일 탭으로 대체한다. PIP 기본 화면에는 투명 배경의 전투 캐릭터만 표시하고 캐릭터 선택 시 바로 위에 현재 전투·성장 정보를 겹쳐 연다. 트레이·자동 시작·항상 위·클릭 통과·창 위치 복원은 제외한다. 상세 표현은 [클라이언트 UX SSOT](../30-domain/player/ux/ssot.md)를 따른다. |
| 던전·레이드 진입 | 2026-09-13 레이드 설계 승인 | confirmed | 현재 구현은 던전 바로가기를 1-5 서버 해금 후 활성화하고 레이드는 `준비 중`으로 유지한다. 레이드 구현·콘텐츠 활성화 이후에는 [레이드 SSOT](../30-domain/raid/ssot.md)의 서버 `unlockStageId`를 사용하며 최초 값은 `stage.01-02`다. 기획 승인만으로 현재 UI 잠금을 해제하지 않는다. |
| 보석 던전 실행 계약 | 2026-09-04 사용자 결정·2026-09-13 레이드 설계 분리 | confirmed | 보석 던전은 계정당 활성 도전 1개, 서버 결정론 전투와 순서화 사건 로그, 계산된 종료 시각부터 완료 허용, 이후 15초 만료와 서버 저장 결과만 보상 확정하는 기존 계약을 유지한다. 레이드의 일정·도전·봉인·보상 규칙은 더 이상 이 항목이 소유하지 않고 [레이드 SSOT](../30-domain/raid/ssot.md)가 소유한다. |
| 보석 던전 보스 패턴·권장 기준 | 2026-09-04 사용자 결정 | confirmed | 생존형은 2초마다 회피 불가능한 강타를 견디며 15초 생존, 폭주형은 15초 공격 성능 처치, 장갑형은 높은 방어력을 15초 내 관통·처치; 권장 빌드는 모든 seed 성공이며 최악 seed의 간신히 성공을 허용 |
| 보석 던전 밸런스 기준 빌드 | 2026-09-04 사용자 결정 | confirmed | 연결 성장 구간의 정상 기준 스킬과 보스별 특화 보석을 수치 산출에만 사용하고 실제 장착 상태는 보정하지 않음; 권장 미달 표본은 바로 아래 단계 권장 빌드 |
| 보석 던전 확장 스킬·장갑 효율 | 2026-09-04 사용자 결정 | confirmed | 8~10단계는 4-10 기준 스킬을 유지; 장갑형은 별도 방어막·전역 공식 변경 없이 권장 관통에서 약 75%, 관통이 크게 부족하면 약 50% 피해 효율로 산출 |
| Kafka | DB 병목 해결책이 아니라 이벤트 다중 소비 확장 수단 | working | Outbox와 병목 실험을 먼저 기준으로 삼는다 |
| 클라이언트 배포 방식 | 2026-09-01 사용자 결정 | confirmed | [배포·운영 SSOT](../50-architecture/operations.md)의 Electron·Tauri 데스크톱 셸 없는 웹 브라우저 배포 |

## 변경 절차

새 결정이 생기면 날짜·참석자·근거 원문·영향받는 SSOT·갱신할 작업을 이 표에 추가하고, 해당 SSOT의 authority_level을 갱신한다. unresolved를 임의로 confirmed로 바꾸지 않는다.

## 관련 원문

- [2026-08-19 회의](../wiki/05-meetings/records/2026-08-19-planning.md)
- [2026-08-21 회의](../wiki/05-meetings/records/2026-08-21-morning.md)
- [2026-08-24 회의](../wiki/05-meetings/records/2026-08-24-after.md)
- [1차 배포 전 준비](../wiki/05-meetings/records/2026-08-26-release-prep.md)
- [DB 병목·Kafka 실험](../90-reference/experiments/db-and-kafka.md)
