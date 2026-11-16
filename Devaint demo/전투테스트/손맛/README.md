# 손맛 HTML 테스트

Unity 에 넣기 전에 타격 손맛(히트스톱·흰 점멸·섬광·파티클·피해 숫자·흔들림·줌)을 브라우저에서 켜고 끄며 비교하는 시험장이다.
현재 게임 화면(1Stage 마을 스케치, 960×540, PPU32)과 이 저장소의 확정 에셋을 그대로 쓴다. 게임 규칙·수치의 원본이 아니다.

- 빌드: `node build.cjs` → 같은 폴더에 `손맛테스트.html`(에셋 내장 단독 파일, Git 제외). 더블클릭으로 연다
- 입력: CharacterV2 카탈로그(검·망치, 확정 검 궤적), `Deviant/Stage1Monsters/Data/import.json`(5종, 엘리트 04_well-warden),
  `Stage1VillageExperiment/Resources/Village`(배경·ReferenceGround), `data/combat/enemy_hit_reaction.json`, `data/combat/presentation/hit_feedback.json`
- 조작: A D 이동, Shift 대시, J·클릭 칼 연타, K 강타, L 불씨, I 광역 폭발, F6 손맛 켜기/끄기, T 느리게, R 자동 시연, Backspace 몹 되살리기
- 프리셋: 제안 v1 / 조사 메모 기준 / Unity 1차(hit_feedback.json) / 손맛 없음. 효과 층 16개와 공격별 수치 표를 바로 고칠 수 있고 JSON(Unity 단위 포함)으로 내보낸다

알려진 제한: 불씨·폭발의 캐릭터 동작은 검 모션을 빌린 임시 동작이다. 몬스터는 공격하지 않는 연습 대상이라 주인공 피격감은 다루지 않는다.
