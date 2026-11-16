# Deviant

팀 공유 문서의 입구는 [docs/README.md](docs/README.md)입니다. [게임 개요](docs/overview/overview.md) → [플레이 흐름](docs/overview/player-flow.md) → [기획·구현 현황](docs/overview/project-status.md) 순서로 읽으면 전체 방향과 현재 적용 범위를 확인할 수 있습니다. 작업 전에는 [미정·명세 충돌](docs/overview/open-questions.md)과 [개발 연결 안내](docs/development/team-guide.md)를 확인합니다.

기획·개발 문서, Unity 공통 게임, 전투 시험장과 스토리·아트 제작 자료를 관리합니다. 스토리·접수실 제작본의 승인과 전체 게임 적용 상태는 구분해서 확인합니다.

- [리소스 원본·제작 자료](resources/README.md) · [게임 이미지 배치](docs/assets/README.md)
- 현재 들판 배경·기존 숲 잔디/부분 스케치 타일: `unity/Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity`.
- 들쥐 파일럿과 공통 게임은 아래 실행 안내를 따릅니다.

잉크 컨셉의 세계관을 가진 2D 액션 로그라이트입니다. Unity 공통 게임은 전투·숲 지형·몬스터 표현·HUD를 한 실행 씬에서 사용합니다. 무기·배경 제작 자료와 비교 도구는 별도 영역에서 관리합니다.

## 시작하기

1. [환경 설정](docs/getting-started/setup.md)에 따라 저장소와 Unity를 준비합니다.
2. [에셋 복원](docs/assets/README.md)을 진행합니다.
3. Unity Hub에서 루트의 `unity/` 프로젝트를 열고 [게임 실행](docs/getting-started/playtest.md)에 따라 `Assets/ForestCombatV2/Scenes/Chapter1Title.unity`를 엽니다.

[전체 문서](docs/README.md)에서 기획, 개발, 협업 안내를 찾을 수 있습니다. 변경 이유와 검증 결과는 Git 커밋 본문에서 확인합니다.

들쥐 행동과 이미지 복원은 [들쥐 행동 안내](docs/development/gameplay/enemies/field-mouse-pilot.md)를 따른다.

## 공유 자료

- [현재 마을 몬스터 제작·적용](docs/development/presentation/visuals/stage1-monster-motions.md)
- [외부 리소스 폴더 구조·보존 범위](docs/assets/resource-repository.md): `resources/` 서브모듈의 무기·스테이지별 몬스터·배경·제작 원본. 받기·갱신은 [환경 설정](docs/getting-started/setup.md#외부-리소스-서브모듈)을 따른다.

- [무기·몬스터·배경·편집 원본](resources/catalog.json)
- [제작·공유 규칙](docs/workflow/asset-production.md)

코드·씬·설정·Unity `.meta`는 메인 Git으로, 이미지·편집 원본은 고정된 `resources` 서브모듈과 Git LFS로 받습니다. [리소스 배치 안내](docs/assets/README.md)에 따라 `에셋 업데이트.bat`을 실행합니다. 외부 폴더 선택이나 별도 패키지 다운로드는 필요 없습니다.
