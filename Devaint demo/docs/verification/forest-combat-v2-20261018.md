# 1챕터 지형 v2 + 병합된 R16 전투

사용자 요청으로 `cce3e28`에 병합된 실제 `InkCombatV2` 구현을 1챕터의 새 숲 지형에 연결했다. 기존 흰 배경 `CombatV2Arena.unity`는 비교용으로 유지한다.

## 실행

- 프로젝트: `outputs/ink_forest/UnityProject`.
- 새 씬: `Assets/ForestCombatV2/Scenes/Chapter1ForestCombat.unity`. 단독 씬으로 열고 Play.
- 메뉴: `Tools > Deviant > Forest Combat > Open Applied Chapter1 Scene`.
- 원래 씬처럼 지형·캐릭터·적은 Play 시작 때 생성된다. 편집 모드의 루트만 보이는 상태는 정상이다.
- 이미지가 Git에서 제외되므로 전달된 `ForestCombatV2-UnitySource-20261018.zip`의 `Assets/ForestCombatV2`를 프로젝트에 함께 둔다. 기존 R16 캐릭터/무기 리소스도 필요하다.
- ZIP은 새 체크아웃의 `outputs/ink_forest/UnityProject` 안에 압축 해제한다. 저장소 루트에 풀지 않는다. [파일 목록과 SHA256](../assets/forest-combat-v2.json)으로 확인하고, 직접 수정한 파일이 있으면 먼저 보존한다.

| 입력 | 동작 |
|---|---|
| 방향키 | 이동 |
| Space / 다시 Space | 점프 / 2단 점프 |
| 아래+Space | 풀 발판 내려가기 |
| Shift | 대시 |
| S / D | 기본 / 특수 공격 |
| Q | 장착 무기 교체 |
| R | 장비를 유지하고 플레이/적/HP 초기화 |
| F1 | 기존 R16 장비 선택 및 적 AI 설정 |
| F2 / F3 | 판정 / 8구간 이동 메뉴 |

## 변경 범위

새 `ForestCombatV2` 어댑터가 전투장의 비교 지형을 대체하고 승인된 숲 Tilemap·일방향 발판·3개 배경 레이어·24개 테스트 적·부드러운 카메라를 생성한다. 실제 이동·점프·대시·충돌·공격·피격·특수·교체·모션 재생은 병합된 코드를 사용한다. 기존 `InkCombatV2` 코드, 승인 PNG, 애니메이션 카탈로그를 수정하지 않았다. 관련 1,114개 파일 해시가 일치했다.

숲 크기는 20,480×2,048px, 32×32 타일, 38개 연속 풀 발판이다. 고가 우회로·갈림굴·분지·상하층 회랑·S자 능선이 포함된다. 기존 웹 미리보기보다 R16의 체공 시간이 짧으므로 마지막 S자 발판의 왼쪽 끝을 4칸 연장했다. 이 조정은 새 전투 씬의 데이터/타일에만 적용했다. 전투 이동 수치와 캐릭터 크기는 유지했다.

테스트 적은 승인된 몬스터 정지 PNG에 기존 근접/원거리 테스트 AI를 연결한 것이다. 종별 최종 AI/애니메이션, 새 보스·보상·드랍·정식 챕터 진행 구현으로 취급하지 않는다. 적 HP는 기존 프로필의 근접100/원거리60을 사용한다.

## 실제 확인한 범위

Unity6000.6.4f1 Windows Development 플레이어를 빌드하고 실행했다. 프로젝트 지정 버전6000.6.5f1은 유지한다.

- 실제 실행 검사128개 통과, 오류0.
- 걷기 속도5, 첫 점프3unit, 2단 점프, 착지, 대시, 발판 내려가기.
- S 공격 피해10, D 특수 피해30, 활성 프레임 중 중복 피해 방지, Q 채찍 교체, 적 공격으로 플레이어 HP 감소.
- 8구간 착지와 실제 콜라이더를 사용하는 연속 입구→출구 이동. 걷기17단계+점프19단계로 x638unit까지 도달했으며 벽타기·대시를 요구하지 않았다.
- 실행 중 Camera.Render로 실제 캐릭터·이펙트·배경 프레임을 저장했다. 정지 합성 이미지나 브라우저 JavaScript 물리의 결과가 아니다.

로컬 결과는 작업공간 `04_테스트/숲_전투통합_v2_20261018/VerificationFinal/runtime-verification.json`, `unity-navigation.json`, `*.png`에 있다. 브라우저 빌드/실행 여부는 같은 작업공간의 최종 상태 파일을 별도로 확인한다. Windows 실행 검사를 브라우저 조작 검사로 표현하지 않는다.

커밋·푸시 전 같은 최종 Windows 실행본을 다시 실행하여 128검사 통과·오류0을 확인했다. 이 재실행의 [상세 결과](forest-combat-v2-runtime-20261018.json)와 [연속 이동 경로](forest-combat-v2-navigation-20261018.json)를 저장소에 기록한다. 재빌드나 Editor Play Mode 실행 결과로 취급하지 않는다.

## 이전 출력 정리

이전 Windows 빌드·중복 검증 화면·미완성 WebGL·자동 변경 백업·오래된 로그 및 사용하지 않는 WebGL 서버 스크립트 등 365파일, 304,263,172바이트를 삭제했다. 이들은 Git 저장소 밖의 로컬 제작 출력이므로 Git의 삭제 변경으로 나타나지 않는다. [정리 기록](forest-combat-v2-cleanup-20261018.json)에 대상 목록과 보존 확인 결과를 남긴다.

최종 Windows 실행본과 검증 화면, 제작 원본, LDtk 편집본, 기존 웹 미리보기와 제작 스크립트가 참조하는 이전 Sources는 유지했다. 정리 전후 최종 자료404파일과 기존 보호 대상2,009파일의 해시가 일치했고 공유 ZIP은 최종 소스와 일치했다. 캐릭터·모션과 기존 전투 코드에 변경을 가하지 않았다.

## 검증 재현

`ForestCombatV2.Editor.ForestCombatBuilder.BuildWindows`를 Unity batchmode의 executeMethod로 실행한다. `-forestBuildOutput`으로 결과 exe 경로를 지정할 수 있다. 플레이어를 `--verify-forest 결과폴더` 인자로 실행하면 실제 컨트롤러 검사를 수행하고 결과를 남긴 뒤 종료한다. 인자가 없는 일반 플레이에서는 자동 조작/검사를 실행하지 않는다.

`BuildWebGL`은 같은 씬을 WebGL로 빌드한다. 로컬 서버에서 `.wasm`의 MIME을 `application/wasm`으로 제공하고 Build 폴더를 함께 유지한다.
