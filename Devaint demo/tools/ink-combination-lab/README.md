# 잉크 파츠 데이터와 브라우저 시뮬레이터

## 브라우저 50종 실험

색상별 10종의 신규 트리거 + 효과를 독립 브라우저 모델에서 실행한다. 사용법과 구현 범위는 [시뮬레이터 안내](../../docs/development/tools/ink-simulator.md)를 따른다. 기존 Unity 잉크 데이터·내보내기와 분리한다.

- 파츠 원본: [ink_simulator_parts_proposal.json](../../data/combat/ink_simulator_parts_proposal.json)
- 조합 예시: [ink_simulator_builds_proposal.json](../../data/combat/ink_simulator_builds_proposal.json)
- 미정 규칙·무기·AI: [ink_simulator_proposal.json](../../data/combat/ink_simulator_proposal.json)
- 생성 문서: [50종 카탈로그](../../docs/development/data/catalogs/ink-simulator-part-catalog.md)
- 직접 편집할 [설계 작성지](../../docs/design/equipment/inks/combination-worksheet.md)

```sh
node --test tools/ink-combination-lab/simulator.test.cjs
node tools/ink-combination-lab/check-simulator-catalog.cjs
node tools/ink-combination-lab/generate-simulator.cjs --preview
node tools/ink-combination-lab/generate-simulator.cjs --check --preview
```

`outputs/ink-combination-lab/ink-parts.html`은 실행 화면, `encyclopedia.html`은 도감이다. 모두 단일 HTML이며 서버·외부 패키지 없이 브라우저로 연다. 출력물은 Git에서 제외한다. `--worksheet`는 작성지 전체를 재생성하므로 수동 편집 후 사용하지 않는다.

계산은 `simulator.js`, 입력·저장·Canvas 표시는 `simulator-app.js`, 화면은 `simulator.html`·`simulator.css`다. 숫자는 원본의 `effect.params`에서 읽으며 한국어 설명을 파싱하지 않는다. 설명과 실행 수치를 함께 갱신한다.

`node tools/ink-combination-lab/browser.test.cjs`는 Playwright/Chromium을 이용한 선택적 검사다. `INK_TEST_PLAYWRIGHT_PATH`로 모듈 경로, `INK_TEST_BROWSER_PATH`로 기존 Chromium 실행 파일을 지정할 수 있다. HTML을 메모리에서 렌더링해 장착·공격·설정·모바일 폭을 검사한다. 화면은 Git 제외 출력 폴더에 둔다.

## 색상 잉크 파츠 개편안 43종

[개편안 원본](../../data/combat/ink_color_parts_proposal.json)에서 [카탈로그](../../docs/development/data/catalogs/ink-color-parts.md)를 생성한다. Unity 런 데이터는 `python tools/combat/export_weapon_growth.py`로 내보낸다. 브라우저 시뮬레이터는 이전 50종을 쓴다.

```sh
node tools/ink-combination-lab/generate-color-parts.cjs
node tools/ink-combination-lab/generate-color-parts.cjs --check
```

## 기존 Unity 시험 파츠

`data/combat/ink_parts_proposal.json`과 `ink_part_builds_proposal.json`은 기존 Unity 시험 구현의 별도 원본이다. `node tools/ink-combination-lab/generate-doc.cjs`는 기존 `docs/development/data/catalogs/ink-part-catalog.md`를 생성하며 `--check`로 검사한다. `python tools/combat/export_training_parts.py`는 이 기존 데이터의 Unity 내보내기를 담당한다. 브라우저 실험 50종을 이 경로에 덮어쓰지 않는다.

신규 파츠의 제품 기준은 [개편 설계](../../docs/design/equipment/rules.md)를 따른다. 브라우저의 표식·연쇄·중복·CC·무기·AI는 변경 가능한 실험값이며 제품 규칙 확정이나 Unity 신규 50종 통합을 뜻하지 않는다.
