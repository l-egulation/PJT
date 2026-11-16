# 2챕터 지하수로 실행과 미술

[분야 개요](README.md)

정식 씬은 `Assets/Chapter2/Scenes/Chapter2Procedural.unity`다. Unity 6000.6.5f1에서 `Tools > deviant > Chapter2`로 열고 `Tools > deviant > Play Chapter2`로 직접 실행한다. 공통 게임에서는 1챕터 클리어 결과의 「2챕터로」가 같은 씬으로 연결된다.

## 에셋 복원

main을 갱신한 뒤 [고정 리소스 복원·Unity 배치](../README.md)를 수행한다. Unity를 닫고 메인이 지정한 resources 커밋을 받은 다음 `python tools/assets/update_assets.py`로 배치한다. 서브모듈 다운로드만으로 Unity 사본이 갱신되지는 않는다.

실제 적용 경로는 `Assets/Chapter2/Resources/Chapter2Waterway/`다. `Chapter2WaterwayArt`가 `WaterwayRuntimeAtlas.png`에서 기본 돌·드문 이끼·2/3칸 이끼와 주변 지형 패턴에 맞는 모서리를 선택한다. 지형은 32px/unit, 내부 명암은 사용자 승인 40px 기준이다. 배경 벽·아치·안쪽 통로·배관·창틀·도시·근경은 `Chapter2WaterwayBackground`가 같은 Resources 경로에서 읽는다. 필터는 Point, 압축·밉맵 없음이다.

원본은 resources의 `environments/waterway-tile-variants/`와 `environments/waterway/`이며, Unity에서 사용할 파일은 [배치 계약](../manifests/unity-deployment.json)이 결정한다. 제작 원본과 HTML 예시는 Unity 실행 씬이 아니다. 상세 배치·카메라·게임 연결은 [2챕터 런타임](../../development/gameplay/world/chapter2-runtime.md)을 따른다.

## 검증

`Deviant.Chapter2.Tests.Chapter2Tests.ApprovedWaterwayAtlasUsesNativePixels`는 실제 Resources 이미지와 Point·밉맵 설정을 확인한다. 실제 게임 캡처는 `Chapter2Procedural.unity`의 Play Mode 카메라로 확인한다. 레이아웃이 다른 HTML 조립 예시를 Unity 캡처라고 표시하지 않는다.
