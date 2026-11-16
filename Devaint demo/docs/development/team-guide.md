# 개발 연결과 작업 진입 안내

[분야 개요](README.md)

## 목적과 기준

기획자가 정의한 규칙이 어떤 원본·코드·씬으로 이어지는지 안내한다. 현재 파일 구조는 [아키텍처](architecture/architecture.md), 앞으로의 구조는 [목표 모듈·계약](architecture/project-structure.md)이다. 목표 설계 문서를 현재 구현 완료 상태로 읽지 않는다.

## 분야별 연결 지도

| 바꿀 내용 | 먼저 확인할 원본 | 구현·연결 위치 | 확인 방법 |
|---|---|---|---|
| 이동·점프·대시 | `data/combat/locomotion/player.json`, 공통 전투 명세 | `Assets/Deviant/Combat/Runtime` | 평지·벽·공중·발판·낙사 검사 |
| 검 행동 | `data/combat/actions/`, 검 관련 JSON | 전투 정의·플레이어·Cast·표현 | 원본 재생성, 행동·모션 동기화 |
| 무기·잉크 수치 | `data/combat/`의 근접·원거리·발사기 원본 | 전투 카탈로그·장비·기술 실행 | [데이터 파이프라인](data/data-pipeline.md) |
| 몬스터 | `data/monsters/chapter1_approved.json` | `FieldMouseEnemy`와 공통 전투 | 파일럿·나머지 종 범위 구분 |
| 챕터·배치 | 런 구조·맵 기획·레이아웃 계약 | World·Integration·Unity 모듈 연결 | 공급·연결도와 실제 이동 별도 검사 |
| HUD·정지 | [HUD 구현](presentation/ui/hud.md) | UI·Control·`GameComposition` | 실제 HP·중첩 제한·정지 복귀 |
| NPC·대화 | [대화 계약](presentation/narrative/narrative-dialogue.md) | Narrative·UI·호스트 | 후보·세션·표시 토큰·종료 수명 |
| 순차 연출 | [시퀀스 계약](presentation/narrative/sequence-execution.md) | 순차 실행·챕터 바인딩 | 완료·취소·실패·스킵 |
| 아트·이펙트 | 승인 패키지·[표현 구현](presentation/visuals/combat-presentation.md) | CharacterPresenter·표현 바인딩 | 피벗·프레임·발동·수명 확인 |

`Assets/...`는 저장소의 `unity/` 프로젝트 기준 경로다. 제품 콘텐츠 원본이 아직 없는 영역에서 테스트 C# 문자열·fixture를 제품 원본으로 승격하지 않는다.

## 실행 씬과 조립 책임

- 현재 실행은 `Assets/ForestCombatV2/Scenes/Chapter1Title.unity`에서 시작해 `Assets/MeadowDuskV3/MapVer2/Scenes/Chapter1Procedural.unity`로 이어진다. 최신 팀 스케치 지형·발판과 선택적 몬스터 콘텐츠를 사용한다.
- Unity Assets와 Build Settings에는 이 두 씬만 둔다. 예전 숲·들판·무기·몬스터 시험 씬은 실행 메뉴에서 제외하며 `unity/Tests/Fixtures/LegacyScenes/`의 텍스트 fixture로만 보존한다.
- NPC·챕터·시퀀스의 격리 검사는 실제 초반 이야기·정식 챕터 조립의 대체물이 아니다.

## 수치 변경의 경로

원본 JSON·명세 확인 → 원본 편집 → 관련 생성기 실행 → 생성 문서·Unity 카탈로그 diff 확인 → Unity 컴파일·대상 테스트 → 실제 씬의 기대 결과 확인 순서다. 정확한 명령과 생성물은 [데이터 파이프라인](data/data-pipeline.md)에 모은다. 문서만 편집하거나 생성 카탈로그만 고쳐 원본과 불일치시키지 않는다. 에셋 가져오기 도구는 일반 수치 재생성에 섞어 실행하지 않는다.

## 시스템 간 경계

전투는 실제 HP·행동·피해를 소유하고 UI는 공급받은 상태를 표시한다. 입력 차단·정지는 소유자별 요청으로 합성한다. 대화·시퀀스는 자신의 표시·제어 수명을 반환하며 다른 시스템의 제한을 해제하지 않는다. 맵 공급기는 배치 결과를 공급하고 제품 전환·보상 정책을 임의로 수행하지 않는다. 구체 경계는 각 계약 문서를 따른다.

## 검증과 인계

[검증 안내](testing/testing.md)에 따라 변경된 계약과 동작에 맞는 검사를 수행한다. 테스트 파일 존재, 컴파일 성공, 자동 테스트 통과, 화면 확인은 서로 다른 증거다. 에셋이 없어서 검사하지 못한 범위를 명시한다. 실행 로그는 Git 제외 `outputs/`, 실행 명령·환경·범위·결과·제한은 커밋 본문에 남긴다.
