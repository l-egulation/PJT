# 조사 수첩 오프닝

사용자가 Unity 적용을 요청한 HTML 연출과 같은 텍스처 세트다. 제목 없는 가죽 표지, 글씨가 있는 종이, 마을·주민·지도 3개 펼침면을 포함한다.

- `preview/index.html`: 승인된 표지·책장 넘김·그림 등장 연출. 폴더를 HTTP로 열면 재생·일시정지·시점 이동으로 확인할 수 있다.
- `preview/export.html`: 같은 Canvas2D 원본에서 종이·글씨·draft/final 펼침면·배경을 PNG data URL이 든 JSON으로 내보내는 제작용 화면이다. 표지는 `preview/output/imagegen/journal-cover.png`의 원본 바이트를 사용한다.
- `runtime/`: Unity 소비용 PNG 15개. `page-N-draft`와 `page-N-final`의 종이와 위아래 글씨는 같고 중앙 글씨·그림 영역만 다르다. 게임에서는 두 텍스처를 그라데이션으로 합성한다.
- `preview/cover-prompt.txt`: 표지 생성 요청문. `preview/assets/`는 기존 마을·주민·지도 그림과 미리보기 글꼴이다.

Unity 목적지와 GUID는 메인 저장소가 관리한다. 메인의 고정 `resources` 커밋과 배치 계약을 이용해 `python tools/assets/update_assets.py --asset ui.notebook-opening`으로 배치한다. 런타임은 웹 브라우저나 HTML 실행기를 사용하지 않는다.
