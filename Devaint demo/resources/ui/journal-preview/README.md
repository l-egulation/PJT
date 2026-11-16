# 수첩 GUI 미리보기

preview/index.html을 브라우저에서 직접 연다. compare.html은 채택 시안과 현재 HTML을 비교한다. Unity 적용 전 검토본이며 거래와 보상은 예시 상태로만 작동한다.

HUD는 960×540 기준 작은 아이콘과 숫자로 구성한다. 일반 전투의 우측 보스 패널은 없고 보스전에서만 중앙 전용 체력을 표시한다. 일시정지는 패널 없이 버튼 세 개를 표시한다.

지도는 현재 main의 Chapter1ProceduralMap.cs에서 사용하는 15개 방의 좌표 및 연결 구조를 재현한 방향 고정 검토 fixture다. 라이브 실행의 난수 seed 또는 장면에 연결한 상태가 아니다. applyMapSnapshot({cols,rows,rooms,passages,visited,current})가 실제 ForestData와 탐색 상태를 받는 경계다. 방 이동 재현 도구는 연결된 방으로만 이동하며 방문 전 정비소와 보스 마커는 숨긴다. 전체 보기 버튼은 탐색 상태를 바꾸지 않는다.

원본 그림은 source/, 실제 사용하는 부품과 채택 시안은 preview/assets/에 있다. source의 원본을 보존한다. Windows 글씨는 시스템 Malgun Gothic을 사용하고 페이지 탐색용 Galmuri는 기존 프로젝트 글꼴이다.

독립적인 버튼과 실제 텍스트로 구성했으며 시안 이미지를 게임 UI로 사용하는 부분은 없다. 원본 재분리는 preview/export-refined.cjs로 실행한다(sharp 필요). 전체 이미지를 잘라 쓰는 과정은 없다.

검증은 메인 tools/gui-preview/verify-journal.cjs로 수행한다. Node와 playwright가 필요하며 PLAYWRIGHT_MODULE, CHROME_PATH 환경변수로 설치 경로를 지정할 수 있다.
