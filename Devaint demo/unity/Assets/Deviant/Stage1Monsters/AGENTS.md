# 1스테이지 몬스터 Unity 적용 기준

- 원본은 `resources/authoring/workbench/source/MonsterReference/approved/manifest.json`과 외부 색인의 승인 프레임이다. 일반 배치는 메인의 `tools/assets/update_assets.py`로 수행한다. 제작 재생성은 `resources/tools/export_stage1_monster_motions.py --destination <새 검토 디렉터리>`로 분리한다. 원본을 Unity 결과에서 역생성하거나 이미지 배치로 게임 시간표를 다시 쓰지 않는다.
- 현재 게임은 Chapter1Title → StartingVillage → Chapter1Procedural로 연결된다. 이 몬스터 모듈은 기존 Chapter1Procedural의 선택적 콘텐츠를 유지하며 시작 마을의 게임 규칙을 소유하지 않는다. 구 모션 확인·숲·시험 씬을 Unity Assets에 재생성하지 않는다. 다른 모듈의 몬스터·전투·전리품·공통 피격 경직 정책을 변경하지 않는다.
- 마을 공격은 data/combat/stage1_village_monsters.json의 시험용 수치와 tools/combat/export_stage1_village_monsters.py를 따른다. 보스는 생성하지 않는다. 기존 승인 그림·시간은 변경하지 않는다.
- 4종은 대기·이동·공격·피격·죽음, 보스는 기본·피격만 있다. 누락된 보스 클립을 임의로 만들거나 완료로 표기하지 않는다.
- Point·PPU32·mipmap 없음·Full Rect를 유지한다. 사용자 지정 게임 미디어 압축은 메인의 `data/assets/media-compression.json`과 `docs/development/delivery/media-compression.md`를 따르며 BC1·BC7 플랫폼 설정을 적용한다. 편집 원본은 resources에 보존한다. 네이티브 프레임을 확대·잘라내기·보간하지 않는다. 모션별 발 피벗은 import.json을 따른다.
- Unity PPtr 클립은 마지막 키 뒤에 한 샘플을 더한다. frameRate1000에서 마지막 중복 키를 원본 종료 1ms 전에 둬서 실제 clip.length를 원본 시간과 같게 유지한다.
- Editor에서 SpriteRenderer의 PPtr 클립을 샘플링할 때 검증 대상에 해당 컨트롤러를 가진 Animator가 있어야 한다. `Validate Imported Motions`로 실제 프레임과 클립 길이를 검사한다.
- 생성물 갱신 시 기존 파일의 GUID를 보존한다. 검증 로그·캡처는 저장소 루트의 Git 제외 outputs/stage1-monsters에 둔다.
