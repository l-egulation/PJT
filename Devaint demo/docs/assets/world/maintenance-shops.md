# 챕터별 정비소 채택 미술

[분야 개요](README.md)

리소스 ID는 `environments.maintenance-shops`이며 제작 기준본은 `resources/environments/maintenance-shops/`다. 두 상인과 정비소 구성은 사용자 채택 상태이고, Unity 정비소 방 중앙에 런타임 배치하며 최초 방문 대화와 실제 정비 서비스를 연결한다. 실제 런의 최초 기록은 프로필에 챕터별로 저장하고, RunSession이 없는 단독 시험 씬은 씬 생명주기 동안 한 번 재생한다.

`preview/index.html`을 웹으로 열면 실제 저장된 Unity 캡처 위에서 배치·대기·간판·불빛과 첫 방문 대화를 확인할 수 있다. 캡처 위 조립 결과를 실제 Unity 적용 화면으로 간주하지 않는다. 게임 표시 960×540, 정수 2배 비교를 제공한다.

`runtime/`의 NPC·뒤 구조물·작업대·간판·전등 PNG는 서로 독립적이다. 1상인은 크림 뿔의 잉크 장인, 2상인은 기존 남색·올리브색 도롱뇽이다. 2정비소는 넓은 어두운 청회색 벽감, 수정지를 덧댄 도면, 정렬된 도구와 잉크병·접힌 천·수평계를 사용한다. NPC에 추가했던 뿔 윤곽선은 제거했다. 생성 원본은 `source/generated/`, 생성 요청과 표시용 변환 범위는 `source/`에 보존한다.

2정비소의 뒤 구조물·작업대·전등·간판(`stage2-backdrop` 480×270, `stage2-desk` 166×72, `lantern` 20×38, `sign` 52×66)은 `environments.maintenance-shops-stage2-pixel-20261113`의 도트판이다(사용자 2026-11-13). 큰 생성 원화를 줄여 그리던 예전 그림 대신 화면 크기 그대로(1px = 게임 1점) 격자에 다시 그렸다. 관은 2챕터 맵 관과 같은 방식(밝기 단계 → 수로 램프 + 베이어 디더)으로 칠하고, 상호작용 장소임이 드러나도록 랜턴·전구 줄·작업 공간·간판 둘레의 따뜻한 빛을 맵 등불 방식으로 구웠으며 바깥 벽은 한 단 어둡다. 전구 줄과 빨강·파랑·노랑 물감 선반을 더했다. 그림 전체를 자르지 않고 1:1로 쓴다(`MaintenanceDialogue.json`의 crop = 그림 전체).

방 안 진열(사용자 결정·시안 승인 2026-11-14)부터 방에는 뒤 구조물 대신 `environments.maintenance-shop-shelves-20261114`의 그림을 놓는다. `stage1-shop-3`~`-6`(922~1156×330, 끝 숫자는 한 줄 칸 수)은 1챕터 정비소 건물 한 채를 정비소 그림 8색·2×2 점으로 다시 그린 것으로, 줄 맞추지 않은 큰 돌, 윗돌의 풀·꽃, 짧은 천막과 그 아래 공구걸이, 문 안쪽 오른편의 진열 선반 판이 들어 있다. `stage2-shop-3`~`-6`(732~960×241)은 2챕터 방 벽을 판 벽감 하나에 정비공 자리와 진열칸을 함께 담고 관·밸브·전구 줄·포스터·이끼·금·렌치·간판 사슬·선반 판을 그렸다. 2챕터 간판은 1챕터 간판과 같은 모양에 2챕터 색을 칠한 `sign-stage2`(60×60)로 바꿨고, 공용 새로고침 해금 런의 줄 끝 보급 궤짝은 `stage1-reroll-crate`·`stage2-reroll-crate`(40×36)다. 상점 진열 확장으로 칸이 늘면 같은 간격으로 진열칸이 길어진 그림을 쓴다(사용자 선택 2026-11-14 A안). 장인·작업대·등불·1챕터 간판은 기존 그림을 그대로 쓰며, 그림 중심·선반 판·등불·간판 자리는 리소스의 `runtime/layout.json`과 `MaintenanceRoomPresentation`·`MaintenanceShelfDisplay` 상수가 같다. 예전 `stage1-backdrop`·`stage2-backdrop`·`sign`은 배치에 남아 있으나 방에는 놓지 않는다.

NPC 표시 크기는 1상인94×120, 2상인141×120이다. 1상인 눈은3×8px 두 개, x34·47/y38이다. 대기 호흡은 1상인1.1초/최대4px, 2상인1.5초/최대3px이고 발 아래20행을 고정한다. 손그림 프레임 애니메이션이 아닌 절차적 모션이며 HTML과 Unity에 적용한다. 생성 원화를 축소한 승인 디자인이며 Unity에서는 PPU32·Point·mipmap 없음, 실제 그림 영역 하단 중앙 피벗으로 조립한다. NPC 아래20행은 고정하고 위100행만 호흡시킨다. 책상은 NPC보다 앞에, 플레이어는 두 오브젝트보다 항상 앞에 표시한다. NPC는 플레이어가 오른쪽에 있으면 좌우 반전하고 왼쪽이면 원래 방향으로 돌아온다. 얼굴 중심 ±0.3unit에서는 방향을 유지하며 호흡·발 접지·작업대는 반전하지 않는다. 손그림 프레임 제작·최종 도트 정리는 별도 범위다.

말풍선은 Galmuri11 Bold12px, 폭184px, 띄어쓰기 줄바꿈, 2px 테두리·꼬리·화자 이름표를 사용한다. Galmuri11 은 정해 둔 글자만 그려 넣는 정적 글꼴이다. 글자 목록은 `unity/Assets/Deviant/Inventory/Resources/PixelInventory/Fonts/Glyphs.txt`이고, 두 `.ttf.meta` 의 `customCharacters` 에 같은 목록이 들어 있다. 목록에 없는 글자는 화면에서 소리 없이 빠진다. 그래서 KS X 1001 한글 2,350자를 모두 넣어 두었다. 대사에 쓴 글자는 `StartingVillageFrontTests.TheBubbleFontHoldsEveryLetterOfTheVillageDialogue` 가 확인한다. 스케치 쪽은 검은 바탕, 컬러 쪽은 밝은 바탕이며 30자/초로 출력한다. Space/F/Enter는 글자 완성 후 다음 대사로 진행한다. 대사·연출 흐름은 [첫 만남 구성](../../design/story/tutorials/maintenance-introductions.md)을 따른다. HTML 소개 패널은 실제 정비 UI가 아니며 C/V 안내가 실제 포탈 설치·귀환을 실행하지 않는다.

8개 PNG를 메인 배치 계약 `docs/assets/manifests/unity-deployment.json`에 등록하며 `tools/assets/update_assets.py`로 `unity/Assets/Deviant/Game/Resources/MaintenanceShops/`에 배치한다. 원본은 resources에 보존한다. Unity 연출은 `MaintenanceRoomPresentation`, 표시부는 `MaintenanceBubbleView`가 맡고 기존 정비 서비스의 가격·구매·장비 규칙을 재사용한다. 1챕터의 작별 인사 뒤 `PortalNotebookView`가 기존 수첩 종이와 포탈 스프라이트로 C/V 설치·재설치와 F 왕복을 안내한다. 수첩을 닫으면 포탈 조작을 해금하며, 실제 실습 성공으로 기록하지 않는다.
