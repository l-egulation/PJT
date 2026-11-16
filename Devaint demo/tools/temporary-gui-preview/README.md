# 실제 캡처 기반 임시 GUI 미리보기

실제 마을 캡처의 배경·캐릭터·지형 픽셀을 다시 그리지 않고, 별도 HTML/CSS UI를 겹쳐 960×540 화면 6개를 렌더링한다. 게임 실행 캡처에 새로운 UI를 조합한 미리보기이며 Unity 게임에서 새 UI가 실행된 결과는 아니다. 메뉴·가방 선택만 로컬 미리보기로 동작하고 게임 거래·효과와 연결하지 않는다.

## 생성

Node.js와 Playwright의 Chromium이 필요하다. 외부 서버·API·이미지 생성 호출을 사용하지 않는다.

```powershell
node tools/temporary-gui-preview/render.cjs "C:/Users/SSAFY/Desktop/자율 프로젝트" "C:/Users/SSAFY/Desktop/자율 프로젝트/output/gui/temporary-gui-v3"
```

Playwright 브라우저가 별도 위치에 설치되어 있으면 `PLAYWRIGHT_BROWSERS_PATH`, 기존 Chromium 실행 파일을 쓸 때는 `GUI_BROWSER_PATH`를 지정한다. 라이브러리 위치는 실행 환경의 `NODE_PATH`로 지정할 수 있다.

출력 `index.html`은 별도 서버 없이 연다. `six-screens.png`는 6분할 보드, `screen-01.png`~`screen-06.png`는 개별 화면이다. 05번의 사망·정지·타이틀 상태도 개별 PNG로 생성한다. 전체 지도·미니맵·아이템 아이콘은 코드로 만든 기능·배치 예시이며 실제 런의 지도·소유 아이템 데이터가 아니다. 숫자·상품·가격도 미리보기용이며 런타임 원본을 변경하지 않는다.

## Unity용 UI 부품

`unity-sprites/`에는 프레임·버튼·슬롯·게이지·아이콘 등 텍스트 없는 PNG 23개와 `import-settings.json`을 내보낸다. 아이콘은 투명 배경이고 사각 프레임은 불투명 채움이다. HTML 버튼·슬롯과 같은 코드를 사용하며 PNG에 이름·수치·가격을 굽지 않는다. 폰트는 기존 프로젝트의 Galmuri11을 사용하며 복사본과 OFL을 미리보기에 보존한다.

Unity에서 사용하는 방법:

1. PNG를 `Assets/Deviant/TemporaryGUI/Sprites/`로 복사한다.
2. 이 폴더의 `TemporaryGuiSpriteImporter.cs`를 `Assets/Deviant/TemporaryGUI/Editor/`에 둔다.
3. Sprite Single / Point / 무압축 / PPU32 설정이 적용된다. 아이콘·게이지 채움을 제외한 부품은 테두리 4px의 9-slice다.
4. uGUI Image에서 프레임·버튼·슬롯·트랙은 Sliced, 아이콘·게이지 채움은 Simple로 연결한다. 게이지 비율은 기존 `PreservedGauge` 등 런타임 표시 코드에서 조절한다.
5. 텍스트는 기존 한글 폰트와 실제 상태 공급자로 별도 구성하고, 기능은 기존 Presenter·ScreenRouter·정비소 서비스에 연결한다.

이 패키지는 부품과 레이아웃 참고용이다. 실제 씬·프리팹 배선과 Unity 컴파일·실행 검증은 별도다. 기존 테마·씬·프리팹·공유 아트는 수정하지 않는다.
