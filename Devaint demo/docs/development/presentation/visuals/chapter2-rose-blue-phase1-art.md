# 2챕터 보스 1페이즈 승인 아트 연결

[분야 개요](README.md)

원본은 resources의 boss-siblings-lineup-20261103에 보존한다. 이 문서는 연출 연결을 기록하며 전투 규칙 SSOT를 대체하지 않는다.

## 적용

- Rose: 공중 사격 정지 자세, 고정 크기 3연사, 분홍 마법 화살. Blue: 고정 캔버스 16키 프레임, 생성 자세에서 반대 손바닥으로 구체 발사. Blue 발사 키는 전투 데이터의 Phase1ShotTell에 맞춘다.
- 양측 투사체는 기존 Rigidbody2D·BoxCollider2D·스윕 충돌·피해 계산을 사용하며 자식 렌더러만 승인 그림으로 교체한다.
- 기계: [잉크 결정](#기계장치-잉크-결정) 그림, 기존 2×3유닛 판정 유지. 중앙에서 시작하고 8초가 지나면 진행 중 패턴과 휴식을 마치고 추가 패턴 한 번과 2초 휴식 후 다른 발판으로 이동한다. HP와 피격 콜라이더, 분출 기준점이 함께 이동한다. 플레이어와 같은 투명 렌더 큐에서 플레이어보다 뒤에 그려 몸을 가리지 않는다.
- 함정·속박·저격·화살비·기계 발판 폭발·순서 폭발의 그림은 아래 [보스전 VFX](chapter2-rose-blue-phase1-art.md#보스전-vfx-다크-판타지)를 쓴다.
- 첫 전환에서는 플레이어를 투명한 청보라 마법 방울로 먼저 감싼 후 들어 올리고, 발판 안착 후 해제한다. 현재 임시 전환 구간의 표현이며 최종 입장 대화·자동 보행·상하 띠는 [컷신 구성안](../../../design/enemies/chapter2/rose-blue/chapter2-rose-blue-cutscenes.md) 단계다.
- 장치 파괴/페이즈 변경 시 남은 공격·경고·연출을 정리한다.
- 30×18u 경기장과 폭 6u 발판 7개를 사용한다. 함정은 발판 7개 모두에 발동하고, 순서 기억 폭발은 안전 발판 하나를 고른다. 1페이즈는 패턴 종료 후 2초 휴식한다. 분출 전조는 1초, 함정 전조는 0.7초, 화살비 전조는 1초, 첫 피해는 패턴 시작 후 2초다.

## 기계장치 잉크 결정

원본은 resources의 `monsters/stage-02/rose-blue-machine-crystal-20261112`다(2026-11-12 사용자 확정). 예전 정면 기계 그림 `RoseBluePhase1/machine.png`는 쓰지 않는다(배치 계약에는 남아 있다). 시트는 보스전 VFX와 같은 형식이고, `runtime/sheets.json`과 같은 값을 갖는 메인 바인딩 `Chapter2/Resources/RoseBlueMachineCrystal.json`을 VFX 시트 표에 더해 `Chapter2/Resources/RoseBlueVfx/`에서 잘라 쓴다.

| 시트 | 연결 |
|---|---|
| `machine-crystal-idle`, `machine-crystal-damage1`, `machine-crystal-damage2` | 96×128 칸 12장 85ms 반복. 체력이 2/3 이하면 damage1(두 동강), 1/3 이하면 damage2(1의 금을 두고 새 금을 더한 세 동강)를 보인다. 문턱은 연출용이며 전투 규칙이 아니다. |
| `machine-crystal-hit0·1·2` | 피해를 받은 순간 그때의 손상 단계 시트를 한 번(3장 50ms) 재생한다. 결정만 하얗게 번쩍이고 2px 밀렸다 돌아온다. |
| `machine-crystal-break` | 256×256 칸 14장 70ms 한 번. 장치가 부서질 때(바닥 전환 컷신 시작) 기계 자리에서 재생한다. 예전의 3×4 조각 날리기를 대신한다. 전환 컷신의 `machine-ink-blast`는 그대로 함께 재생한다. |

- 칸은 1:1로 그린다. 대기·손상·맞기 칸의 피벗 (48,76)과 부서지기 칸의 피벗 (128,140)은 2×3u 판정 칸의 가운데라, 그림은 판정을 모두 덮고 결정 끝과 빛 번짐이 판정 위로 1u 솟는다. 판정·체력·이동은 그림과 무관하게 전투 데이터를 따른다.
- 기계는 플레이어와 같은 정렬 층에서 플레이어보다 한 칸 뒤에 그린다. 부서지기 시트도 같은 층·순서를 쓴다.

## 보스전 VFX (다크 판타지)

원본은 resources의 `monsters/stage-02/rose-blue-boss-vfx-20261105`다. 게임은 그 `runtime/` 시트 35종과 백색화 덮개 4종만 쓴다. 회오리 곁들임 두 장만 반 해상도이고 `sheets.json`의 `scale`(2)만큼 키워 그린다. 시트마다 열·행·프레임·셀·피벗·프레임 시간은 `runtime/sheets.json`에 있고, 메인 바인딩 `Chapter2/Resources/RoseBlueVfxSheets.json`이 같은 값을 갖는다. 32px/u, 셀은 왼쪽 위부터 행 우선이며 `Chapter2/Resources/RoseBlueVfx/`에서 런타임에 잘라 쓴다. 그림만 바꾸며 히트박스·속도·피해 시점은 전투 데이터를 따른다.

| 시트 | 연결 |
|---|---|
| `rose-arrow-flight` | Rose 화살 전부(`straight`, `stun`, `duo-homing`). 오른쪽 방향 그림을 투사체 회전에 맞추고, 왼쪽으로 날 때 flipY로 밝은 면을 위에 둔다. 스턴 화살은 소멸 0.2초 전부터 흐려진다. |
| `rose-arrow-impact`, `p3-arrow-impact` | Rose 화살·3페이즈 화살이 플레이어에게 피해를 준 순간 그 자리에 2배 크기로 재생한다. |
| `rose-rain-arrow-flight`, `rose-rain-impact` | 1페이즈 패턴 3 화살비. 15발이 화살마다 다른 시작 시각·주기로, 떨어질 때마다 자리·높이·기울기를 새로 골라 발판 위에 골고루 떨어진다. |
| `snipe-hit` | 1페이즈 패턴 2 추적 저격 타격(깨진 고리). 저격 표식은 코드로 그린 조준경(눈금 고리·안쪽 끊긴 고리·빈 마름모·십자, 짙은 테두리 위 선명한 장밋빛, 고정 때 더 붉음)이며 플레이어보다 위에 그린다. |
| `blue-orb-trail`, `blue-orb-body` | 1페이즈 패턴 1 Blue 유도 구체와 2페이즈 패턴 3 Blue 교환 구체. 꼬리만 진행 방향으로 돌리고 구는 돌리지 않는다. |
| `orb-hit` | 1페이즈 패턴 1 구체가 기계에 닿을 때. |
| `trap-warn`, `trap-snap` | 1페이즈 패턴 2. 발판마다 하나씩, 발판 윗면 가운데에 전조와 사슬 속박을 둔다. |
| `dive-root` | 함정·급강하 속박이 걸린 동안 플레이어 발목 고리(출현 3프레임 → 90ms 반복 6프레임 → 해제 3프레임). |
| `machine-warn`, `machine-ink-blast`, `rain-warn` | 1페이즈 패턴 3 기계 발판 전조, 구역이 터지는 잉크 폭발(타격 순간 시작), 나머지 발판 화살비 전조. |
| `sequence-warn-rose/blue`, `sequence-blast-rose/blue` | 1페이즈 패턴 4. 순서 번호 홀수 Rose, 짝수 Blue. 번호 글자는 그리지 않는다. |
| `ink-charge`, `ink-fireball`, `ink-blast` | 2페이즈 패턴 1. Blue 손 앞 0.5초 차징, 진행 방향으로 도는 파이어볼, 4×4u 폭발. |
| `blue-vanish`, `dive-trail`, `dive-impact` | 2페이즈 패턴 2. 급강하 전·착지 직후 Blue 사라짐, 낙하 잔상(Blue 발에 붙음), 착지 파열. |
| `exchange-flash`, `*-white` | 2페이즈 패턴 3. 교환탄은 Rose 화살 2배와 Blue 구체이며, 충돌 1초 전부터 흰 덮개가 점점 진해진다. 충돌 섬광은 1.5배. |
| `rose-rain-arrow-flight`, `rose-rain-impact`, `ink-whirl-back`, `ink-whirl-front` | 2페이즈 패턴 4 양옆 압박(2026-11-05 확정). 전조 시점에 각 보스가 선 쪽 벽에 하나씩 시작한다. Rose 쪽은 마법 없는 화살 영역이다. 전조 동안 최종 경계 바닥에 표지 화살 3발이 박히고, 확장 동안 일제 사격 70발이 벽 위에서 비스듬히 내리꽂혀 앞면을 따라 박히며 울타리가 쌓이고, 압박이 끝나면 0.6초 동안 흐려진다(화살비 그림 1.6배, 승인 시뮬레이션과 같은 배치). Blue 쪽은 3P5 회오리 곁들임을 압박 크기·Blue 색·한 덩어리 띠로 그린 잉크 회오리이고, 피벗이 넓어지는 영역의 가운데를 따라간다(뒤 장은 보스 뒤, 앞 장은 플레이어 위). 오른쪽 벽은 좌우가 거울이다. 이전 `press-rose`·`press-blue`는 쓰지 않는다. |
| `p3-arrow-flight`, `p3-blast` | 3페이즈 튕기는 화살·부채꼴 화살·순차 낙하 화살과 튕기는 화살 지연 폭발. 백색화는 흰 덮개(`p3-arrow-flight-white`)를 겹쳐 표현한다. |
| `homing-form`, `homing-bolt` | 3페이즈 패턴 1 유도 화살(보라 단색). 1초 전조 동안 Blue 손 앞에서 잉크가 모여 화살이 맺히는 시트를 겨누는 방향으로 돌려 재생하고(시전자 기절 시 멈춤), 발사 뒤에는 반복 비행 그림을 투사체 회전에 맞춘다. 생성·비행·잔상 그림은 기존의 절반 크기로 표시한다. 0.05·0.1·0.15초 전 자리에 같은 그림을 옅게(0.35·0.2·0.1) 잔상으로 겹친다(최근 0.2초 궤적). |
| `p3-pillar` | 3페이즈 패턴 4 열 폭발. 폭발 구역이 생길 때 박힌 화살 바닥에서 한 줄기 전체 높이 기둥을 재생한다. |
| `aoe-whirl-back`, `aoe-whirl-front` | 3페이즈 패턴 5 확장 광역기 화살 회오리. 전조 시작부터 곁들임 두 장(뒤 장은 보스 뒤 정렬 10, 앞 장은 플레이어 위)을 재생하고, 확장이 시작되면 `p3-arrow-flight` 60발을 높이·반지름·속도·크기·기울기를 화살마다 다르게 해서 깔때기로 돌린다(뒤쪽 화살은 작고 어둡게 보스 뒤). 전조 중 기절하면 곁들임을 멈춘다. 판정 사각형은 보이지 않게 둔다. |

- 3페이즈 그림은 사용자가 HTML 시뮬레이션에서 확정했다(2026-11-05). 그림만 바꾸고 판정·속도·피해 시점은 전투 데이터를 따른다. 3페이즈 석궁 낙하 패턴은 삭제했다.
- 1페이즈 승인 그림 중 `trap`, `ink-column`, `root`는 위 시트로 대체되어 쓰지 않는다(배치 계약에는 남아 있다).
- `tools/assets/register_rose_blue_vfx.py`가 resources의 `runtime/sheets.json`을 읽어 배치 계약·`.meta`·바인딩 JSON을 기록한다.

## 에셋 배포와 재생성

resources/tools/export_phase1_column.mjs → resources/tools/build_phase1_unity_art.py로 원본을 보존한 적용본을 만든다. tools/assets/register_rose_blue_phase1.py가 적용본 선택과 Unity 메타·프레젠테이션 데이터를 기록한다. 리소스 커밋을 원격에 보존하고 메인 gitlink를 선택한 뒤 tools/assets/update_assets.py로 배포한다.

아틀라스의 DefaultTexturePlatform과 Standalone 가져오기 설정은 4096·RGBA32·무압축·Point·mipmap off다. 기본 2048 제한에 의해 기둥 픽셀이 줄어들지 않도록 고정한다. 프레임마다 바운딩 박스로 재정규화하지 않는다.

## 테스트 시작

Unity 6000.6.5f1에서 Tools/deviant/Play Rose Blue Phase1 또는 RoseBluePhase1Playtest.Play 진입점을 사용하면 보스 전용 RoseBlueBoss.unity 씬을 열고 일반 몬스터를 비활성화한 뒤 실제 B방 1페이즈 시작 시 자동 일시정지한다. 상단 Pause를 해제하면 바로 테스트한다. 실행 인자 --rose-blue-boss-test를 함께 주면 준비 완료 후 실제 Chapter2Procedural 씬의 B방으로 이동하고 보스전을 시작한다. 일반 새 게임 진입은 변경하지 않는다.

전투 규칙: tools/combat/test_rose_blue.ps1. 실제 씬: Deviant.Chapter2.Tests.RoseBlueIntegrationTests. 로그와 캡처는 outputs/rose-blue-unity에 저장한다. 전체 resources 보관함의 미복원 LFS 원본과 별개로, Unity 배포 계약의 선택된 에셋 바이트/해시는 배포기로 검증한다.

전투 규칙 검증은 무작위 안전 발판 7종과 연속 중복 없는 장치 이동을 포함한다. 씬 통합 검증은 공격 중 위치 고정, 이동 후 HP·피격 콜라이더 유지, 새 장치 위치를 따르는 분출 기준점을 확인한다. 그래픽 없는 배치 테스트는 화면 렌더링을 검증하지 않는다.

보스 전용 씬: unity/Assets/Chapter2/Scenes/RoseBlueBoss.unity. 일반 2챕터 씬 파일은 그대로 유지한다. 씬을 직접 열고 Play해도 RoseBlueBossScene 컴포넌트가 B방으로 진입시킨다. 전용 메뉴는 검토 중 공격이 진행되지 않도록 1페이즈 시작에서 일시정지한다.

전용 보스 씬 왼쪽의 1·2·3페이즈 버튼으로 해당 페이즈를 바로 시험한다. 버튼은 전용 씬을 다시 열어 플레이어 체력과 보스·공격 상태를 초기화하고 선택한 페이즈의 전장을 준비한다. 같은 페이즈 버튼은 재시작으로 동작하며 Editor 일시정지를 해제한다.
