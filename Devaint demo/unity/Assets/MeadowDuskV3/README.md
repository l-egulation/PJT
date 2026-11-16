# 1챕터 · 들판 배경과 기존 숲 타일

활성 프로젝트: 저장소 루트 `unity/`.
씬: `Assets/MeadowDuskV3/Scenes/Chapter1MeadowCombat.unity`.
메뉴: `Tools > Deviant > Meadow Dusk > Open Applied Chapter1 Scene`.

- 기존 숲의 32×32 잔디·부분 스케치 타일 826종과 맵 배치를 복구했습니다.
- 하늘·태양·구름·산/들판·중앙 나무 5개 배경을 유지합니다. 구름은 따로 이동하고 배경의 수직 이동은 제한합니다.
- 캐릭터 PNG와 애니메이션 카탈로그는 병합 전 승인본과 같습니다.
- 전투는 `fix/forest-mouse-combat-pilot`에서 병합한 `Deviant.Combat` 공통 구현을 사용합니다. 일반 이동 속도는 7.5칸/초이며 기존 점프 높이와 대시 설정을 사용합니다.
- `ForestCombatV2`는 이 맵의 지형·배경 어댑터입니다. 새 공통 카메라 출력에 부드러운 추적 위치를 공급합니다.

←/→ 이동, Space 2단 점프, ↓+Space 발판 내려가기, Shift 대시, Q/W 슬롯별 기본 공격, E/R 슬롯별 스킬, X 무기 순서 교환, F3 구간 이동.

이 씬의 몬스터는 기존 배치 테스트 적입니다. 들쥐 행동과 원본 PNG 22장 복원은 [들쥐 행동 안내](../../../docs/development/gameplay/enemies/field-mouse-pilot.md)를 따릅니다.

Windows 빌드 진입점: `ForestCombatV2.Editor.MeadowDuskBuilder.BuildWindows`.
`-forestBuildOutput`으로 실행 파일 경로를 지정합니다.
실행본의 `--verify-forest OUTPUT_DIRECTORY`는 실제 컨트롤러로 지형·배경·전투·시작점부터 출구까지의 이동을 검사합니다. 검사 결과는 실행 후 생성된 JSON을 확인합니다.

이미지와 실행 빌드는 별도 배포하며 Git에는 코드·씬·데이터·메타데이터를 보관합니다. 취소한 흙·모듈 타일은 이 씬에서 사용하지 않습니다.
