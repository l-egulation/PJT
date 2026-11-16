# 리소스 저장소의 역할과 구조

[분야 개요](README.md)

`resources/`는 비공개 Git LFS 저장소 `h0422ys/deviant-resources`의 서브모듈이다. 이미지·음원·폰트·VFX 원본·게임용 출력·미디어 포함 HTML 검토 자료·미리보기·원본 패키지·색인·리소스 도구를 소유한다. 메인은 외부 커밋을 고정하고 사용할 에셋·세트·Unity 목적지를 [배치 계약](manifests/unity-deployment.json)으로 선택한다. [받기·배치](README.md)는 이 두 저장소만 사용한다.

## 대상별 원본

- `characters/ink/`: 공통 몸체, 이동 모션, 무기별 본체·전용 효과·편집 원본.
- `monsters/stage-01/`: 일반·엘리트·보스와 공통 제작 자료. 이전 들판 종은 `monsters/legacy/field/`에서 보존하며 현행 종으로 임의 재배정하지 않는다.
- `environments/`: 숲·마을·시계탑 공간. 보스 본체와 공간 미술을 구분한다.
- `effects/`, `sequences/`, `ui/`: 공용 효과, 포탈·연출, 화면별 미술.
- `audio/approved-combat/`, `fonts/game/`, `effects/wallcoeur/`: 기준·런타임·편집 입력 음원, 게임 폰트, 보유 VFX 텍스처. `imports/main-media/mapping.json`으로 원래 메인 경로·커밋·바이트·해시와 Unity 메타데이터 해시를 연결한다.
- `monsters/stage-01/bosses/clock-executioner/runtime/stationary/`, `authoring/map-editor/runtime/`: 보스 PNG TextAsset과 현재·이전 지형용 단일 HTML 원본. `imports/main-boss-map-editor/mapping.json`에 메인 출처·해시와 보스 메타데이터 해시를 보존한다. 편집기 템플릿·JS·맵 데이터는 메인, 내장 변환기는 `resources/tools/build_map_editor.py`가 소유한다.
- `authoring/workbench/source/`: 공통 제작 도구·편집 데이터·미리보기의 교차 상대 경로를 보존한 원본. `WeaponReference`, `MonsterReference`, `ForestReference`, `StoryReference`, `StoryPreview`, `아트`의 제작 내용을 찾는다. 기존 배포 안내·복원 프로그램을 현재 사용 절차로 보관하지 않는다.
- `imports/`: 원본 패키지와 원래 경로·크기·SHA256·원본 Git 커밋을 연결하는 이관 매핑. 신규 제작물의 일상 출력 폴더로 사용하지 않는다.

각 대상의 `asset.json`과 전체 `catalog.json`으로 파일·출처·세트를 찾는다. `source/`는 편집 기준본, `runtime/`는 출력, `preview/`는 열람 자료다. 일반 배포본·원본 패키지 등 같은 파일명에 다른 바이트가 있으면 별도 세트로 보존하고 자동 병합하지 않는다. `runtime/`라는 이름은 최종 승인이나 현재 게임 통합 완료를 뜻하지 않는다.

## 도구와 메인 경계

`resources/tools/assets.py verify`는 원본·색인을 검사한다. `export`는 소비자가 명시한 선택 계약만 출력하고 로컬 수정·경로 이탈·누락 LFS·버전 불일치를 거부한다. JSON 내장 PNG는 재인코딩 없이 추출한다. 제작 변환기는 `resources/tools/`에서 새 검토 디렉터리에 결과를 만든다.

메인의 `tools/assets/update_assets.py`와 `에셋 업데이트.bat`은 고정 gitlink·작업 상태·Unity 목적지·메타데이터를 확인하는 얇은 실행기다. Unity Editor 연결과 GUID·가져오기 설정은 메인이 소유한다. 게임 수치·행동·모션 시간표의 편집 원본은 계속 `data/combat/`다. 아트 배치로 원본 게임 데이터를 재생성하지 않는다.

음원·폰트·VFX 원본은 resources에 보존하고 Unity에서는 기존 경로의 로컬 배치 사본을 사용한다. `.meta`·GUID·씬·프리팹·재질·셰이더·게임 카탈로그와 오디오 편집 설정은 메인에 유지한다. 비공개 저장은 라이선스 검증이나 외부 공개·제3자 전달 허가를 뜻하지 않는다.

## 미디어 포함 HTML 자료

이미지·음원·폰트·VFX를 포함한 HTML 보고서·비교 화면·제작 검토 자료는 해당 에셋의 `preview/` 또는 공통 `authoring/`에 저장한다. Base64/data URI 내장과 상대 파일 참조 모두 해당한다. 본문·스크립트·스타일과 필요한 미디어를 함께 등록하고 링크를 검사한다. 내장 미디어가 있는 HTML·JS·JSON은 `.lfs-payloads.json`에 등록해 LFS 속성을 생성한다. 메인 문서에는 원본 경로와 사용 방법을 안내하고 미디어를 중복 내장하지 않는다. 일회성 로그·캡처·작업/검증 이력 보고서는 별도 커밋하지 않고 Git 제외 `outputs/`에 둔다.

## 변경과 검증

두 저장소의 실제 브랜치와 미커밋 작업을 먼저 확인한다. 리소스 내부 변경은 외부 저장소에서 커밋·push하고 LFS 복원을 확인한 다음, 메인에서 배치 계약·연결 코드·gitlink를 함께 갱신한다. `git submodule update --remote`로 최신 세트를 자동 채택하지 않는다.

원본 패키지·편집 프레임·미채택 자료는 현재 게임에서 호출되지 않는다는 이유로 삭제하지 않는다. 이관 매핑의 해시를 편집 결과로 덮어쓰지 않는다. 원본 바이트·GUID·승인 상태를 유지한다. 현재 유효한 작업 절차만 문서에 두고 변경·검증 이력은 Git 커밋으로 관리한다.

파일·배치 검사는 Unity 실행·브라우저 상호작용·미술 재승인을 대신하지 않는다. Unity 검증은 **6000.6.5f1**과 메인의 원본 패키지 버전으로 수행한다.

## GUI 디자인 검토 자료

`ui.journal-preview` 그룹은 `ui/journal-preview/`의 HTML 미리보기, 채택 시안, 개별 부품과 생성 원본을 관리한다. `preview/index.html`을 직접 열며 `source/`의 원본은 보존한다. HTML 검토 단계이므로 Unity 배치 계약에는 추가하지 않는다. 채택되지 않은 로컬 탐색 시안은 이 그룹에 등록하지 않는다.
