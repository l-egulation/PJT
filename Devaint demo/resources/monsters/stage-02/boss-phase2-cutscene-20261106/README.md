# 2페이즈 종료·흡수 컷씬 검토

로즈 선격파·블루 선격파 두 분기의 컷씬 에셋과 실행 가능한 HTML 검토본이다. 실제 게임 캡처나 Unity 통합 결과가 아니다. 전투 QA 수치는 `source/references.json`에 기록한 메인 커밋 기준이며, 게임 규칙을 새로 확정하지 않는다.

- `preview/index.html`: 2페이즈 전투와 두 종료 분기를 선택해 재생.
- `preview/bridge/index.html`: 로즈 앞에 착지·회복한 블루가 손을 뻗는 연결 모션 18프레임 검토.
- `preview/handoff/index.html`: 제작용 시작·끝 이미지와 프롬프트.
- `preview/motions.json`: 실제 재생할 프레임·시퀀스·시간·바닥 기준.
- `preview/assets/motions/blue-absorb-bridge-v2/`: 배경제거와 외곽을 정리한 현재 채택본. `blue-absorb-bridge/`는 이전 보정본 보존용이며 현재 재생 목록에서는 사용하지 않는다.
- `source/blue-absorption-bridge/`: 새 연결 모션의 Aether 원본 보존. 시작·끝 이미지 및 프롬프트는 handoff와 함께 보존한다.
- 기타 모션의 사용자 배율 적용본은 `../boss-motion-size-review-20261106/runtime/`, 무배율 승인본은 같은 그룹의 `preview/assets/`에 있다. 이 컷씬은 배율 적용본을 자체 동봉해 독립 실행한다.

리소스 루트에서 `python -m http.server 8155 --bind 127.0.0.1` 실행 후 `http://127.0.0.1:8155/monsters/stage-02/boss-phase2-cutscene-20261106/preview/`로 연다. `node --test monsters/stage-02/boss-phase2-cutscene-20261106/preview/engine.test.mjs`로 전투 QA 검사를 실행한다.
