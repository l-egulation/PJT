# 콘텐츠 구조와 버전

---
doc_kind: ssot
owner_domain: content
authority_level: applied
---

정적 게임 콘텐츠는 코드와 분리하고 JSON Schema로 검증되는 JSON 파일을 버전 단위로 관리한다.

## 확정된 스키마 원칙

- MVP 전체 콘텐츠를 JSON + JSON Schema로 관리한다.
- `chapters`, `stages`, `monsters`, `items`, `equipment`, `skills`, `gems`, `gem-dungeons`, `drops`, `economy`, `cosmetics`, `localization` 등 도메인별 파일을 분리하고 manifest가 버전 패키지를 묶는다.
- 콘텐츠 ID는 DB UUID와 구분되는 안정적 문자열 키를 사용한다. 예를 들어 스테이지 키는 `stage.01-01` 형식이다.
- manifest는 `contentVersion`, schema version, 파일 목록과 전체 checksum을 가진다. 서버는 immutable 파일을 로드할 때 checksum과 참조를 검증한다.
- 확률은 기본 10,000분율 basis point 정수로 저장하되, [치장 SSOT](../30-domain/cosmetics/ssot.md)처럼 도메인 SSOT가 별도 정밀도를 지정하면 해당 정밀도를 따른다. 게임 수치는 명시된 최소 단위의 정수로 저장한다.
- 중간 계산은 도메인이 정한 정밀도를 유지하고, SSOT가 지정한 최종 계산 지점에서만 round half-up을 적용한다.
- 파일 간 참조는 안정적 문자열 키로 표현한다. loader와 검증 도구는 누락·중복·순환·도달 불가 참조를 전체 검증하며 DB FK로 정적 콘텐츠를 복제하지 않는다.
- manifest와 각 데이터 항목은 `authority` 상태를 `working` 또는 `applied`로 표시한다. working 수치는 applied 기준으로 오인하지 않으며, 운영 허용 여부는 배포 환경이 통제한다.
- 서버 실행 세션·메인 전투 세션·보석 던전 도전은 시작 시 콘텐츠 버전을 고정한다. 새 버전은 새 세션·새 도전부터 적용한다.
- 스테이지 화면 프레젠테이션은 일반 몬스터 ID 목록 `normalMonsterIds`, 보스 ID `bossMonsterId`, 배경 ID `backgroundId`로 관리한다. 바이너리 에셋은 웹 공개 에셋 경로에 두되 스테이지 배치는 콘텐츠 JSON만이 소유한다.

## 파일 배치

~~~text
packages/game-content/
├─ schema/
├─ versions/
│  └─ v1/
│     ├─ manifest.json
│     ├─ chapters/
│     ├─ stages/
│     ├─ monsters/
│     ├─ items/
│     ├─ equipment/
│     ├─ skills/
│     ├─ gems/
│     ├─ gem-dungeons/
│     ├─ drops/
│     ├─ economy/
│     ├─ cosmetics/
│     └─ localization/
└─ README.md
~~~

## 데이터 책임

- 챕터·스테이지 구조·표시 이름·해금 조건: [스테이지 SSOT](../30-domain/world/ssot.md)
- 챕터·스테이지 물리 콘텐츠 배치와 버전: 이 문서의 `versions/v1/chapters`·`versions/v1/stages`
- `progression-rebalance-v1` 최초 클리어 보상: [차기 최초 보상 콘텐츠](./stages/first-clear-rebalance.md)
- 전투 수치: [전투 SSOT](../30-domain/combat/ssot.md)
- 아이템·드롭 규칙: [아이템 SSOT](../30-domain/items/ssot.md)
- 주력 재료 표시명·설명 문구와 장비 표시명: [아이템 표시 콘텐츠 v1](./items/mvp-v1.md)
- 장비·제작식 규칙: [장비 SSOT](../30-domain/items/equipment/ssot.md)
- 스킬 규칙: [스킬 SSOT](../30-domain/character/skills/ssot.md)
- 스킬·스킬북 표시명·아이콘 확정 시안: [스킬 표시 콘텐츠 v1](./abilities/mvp-v1.md)
- 보석 해금·합성·중복·입장권·진행: [보석 SSOT](../30-domain/gems/ssot.md)
- 보석 옵션 값·보석함·던전 단계·보상·표시명: [보석 콘텐츠 v1](./gems/mvp-v1.md)
- 승인된 보석 던전 보스 절대 수치·15초 패턴: `packages/game-content/versions/v1/gem-dungeons/gem-dungeons.json`
- 치장 해금·뽑기·등록·도감·세트 규칙: [치장 SSOT](../30-domain/cosmetics/ssot.md)
- 치장 비용·확률·카탈로그·`starThresholds`·세트 누적 효과: [치장 콘텐츠 v1](./cosmetics/mvp-v1.md)
- 쌀 보상률·스킬 성장 정수 비용: [성장 SSOT](../30-domain/progression/ssot.md)와 [스킬 SSOT](../30-domain/character/skills/ssot.md)
- 거래 수수료: [거래소 SSOT](../30-domain/economy/ssot.md)

현재 승인된 챕터 1~4 적·보스 수치는 `packages/game-content/versions/v1/stages/stages.json`의 `enemy-v1-applied` 콘텐츠로 이관됐고, seed 1,000개 검증 결과는 [enemy-v1-applied 검증 결과](../70-plans/mvp-release/verification/enemy-v1-applied.csv)에 보존한다. 차기 `progression-rebalance-v1`은 [승인 설계](../superpowers/specs/2026-09-11-progression-rebalance-v1-design.md)에 따라 별도 manifest에서 경험치·적·최초 보상·장비 비용·드롭 의존 버전을 묶으며 현재 v1을 덮어쓰지 않는다. 치장 승인 수치·카탈로그는 `packages/game-content/versions/v1/cosmetics/cosmetics.json`에 이관됐다. 스테이지 표시 이름·해금 조건·주요 보상은 `world/ssot.md`가 정책과 필드 책임을 소유하지만 현재 JSON 스키마에는 아직 없다. 보석 옵션·보석함·던전 기준 빌드 초안은 `gems/mvp-v1.md`가 소유하고, 보석 포함 3-1~4-10 적 수치도 공통 방어력을 포함한 별도 콘텐츠 버전으로 산출한다. 보석 콘텐츠의 승격 상태는 [Gems Content](./gems/README.md)에서 확인한다. 문서와 런타임 데이터가 서로 다른 숫자를 소유하지 않도록 승인 콘텐츠를 버전 패키지의 단일 데이터 원본으로 이관한다.
