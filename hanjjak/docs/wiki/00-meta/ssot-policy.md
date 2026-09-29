# SSOT 운영 규칙

## 목적

같은 규칙이 게임 기획, 도메인, 시스템, 계획, 작업 문서에 반복되는 문제를 막는다. 하나의 사실은 하나의 책임 문서에만 정의하고, 나머지는 링크·상태·질문만 기록한다.

## 강제 규칙

1. 주제마다 owner_domain과 SSOT 경로를 하나만 둔다.
2. SSOT가 아닌 문서는 숫자, 상태, 허용 목록, 성공 조건을 재서술하지 않는다. 필요한 경우 SSOT 링크만 둔다.
3. 회의록은 사실의 출처지만 자동으로 현재 정책이 되지 않는다. [결정 로그](../../80-decisions/README.md)의 전파 상태가 confirmed인 경우에만 현재 기준으로 사용한다.
4. 실행 상태와 완료 증거는 [A–K 작업 문서](../06-delivery/tasks/_index.md)가 소유한다. 계획·도메인 문서에 작업 상태를 복사하지 않는다.
5. Notion 원문은 archive/reference다. 원문에 있는 미정·구버전·충돌 내용을 현재 규칙으로 해석하지 않는다.
6. 충돌을 발견하면 한쪽을 임의로 삭제하거나 합치지 않는다. [결정 로그](../../80-decisions/README.md)에 unresolved 항목으로 등록하고 양쪽 출처를 링크한다.
7. 구현 문서가 정책을 재정의하지 않는다. 정책은 `10-game`·`20-design`·`30-domain`·`60-content`에서, 논리 책임은 `40-systems`에서, 기술 배치는 `50-architecture`에서 관리한다.
8. 도메인 간 문서는 상대 도메인의 필드를 소유하지 않는다. 상대 SSOT를 참조한다.

## 경로와 문서 유형

```text
10-game/<문서>.md                         경험·의도·최상위 제약
20-design/<문서>.md                       루프·플레이 경험·규칙 탐색 경로
30-domain/<domain>/ssot.md                 게임 규칙·수치·상태 전이의 유일한 정본
30-domain/<domain>/features/<기능>.md      기능 탐색 경로와 책임 연결만 기록
40-systems/<system>/                      논리 시스템의 책임·경계·상태·이벤트 연결
50-architecture/<문서>.md                 런타임·저장·서비스·운영 구조
60-content/<문서>.md                      콘텐츠 모델·버전·배치
70-plans/<feature>/                       제안·요구사항·위험·롤아웃 계획
80-decisions/README.md                    결정 상태와 SSOT 전파의 정본
wiki/06-delivery/tasks/<그룹>/<작업>.md   실행 상태·완료 기준·증거의 정본
wiki/05-meetings/records/<날짜>.md        회의 원문 전사
wiki/99-archive/notion/...                Notion 원문·스키마·속성 보관
```

`README.md`와 `features/` 문서는 탐색을 돕는 라우팅 문서다. 이 문서들에는 도메인 규칙, 수치, 허용 목록, 작업 상태를 복사하지 않는다.

## 우선순위

1. confirmed 상태의 결정 로그와 그 결정이 갱신한 SSOT
2. 현재 배포 범위를 명시한 A–K P0 작업의 완료 기준
3. 도메인 SSOT의 working 규칙
4. reference 자료
5. archive 원문과 이전안

우선순위만으로 충돌이 해소되지 않으면 결론은 unresolved다.

## 문서 갱신 절차

1. 변경하려는 주제의 SSOT 경로를 [주제별 SSOT 지도](source-map.md)에서 찾는다.
2. SSOT 한 곳을 수정하고, 영향을 받는 결정·작업·계약 문서에는 링크와 영향만 기록한다.
3. 기존 값과 새 값을 비교해야 하면 [결정 로그](../../80-decisions/README.md)에 출처, 날짜, 상태를 남긴다.
4. 원문은 삭제하지 않는다. 새 내용이 원문과 달라진 이유를 결정 로그에 남긴다.
5. 링크와 중복 문장을 검사하고 unresolved 표기가 사라지지 않았는지 확인한다.

## LLM 응답 규칙

- 질문에 답하기 전에 이 문서와 source-map.md를 읽는다.
- 답변의 근거가 되는 SSOT 경로를 함께 제시한다.
- unresolved, working, legacy를 확정 사실처럼 표현하지 않는다.
- 작업 문서의 제목만 보고 도메인 규칙을 추론하지 않는다. 완료 기준과 해당 도메인 SSOT를 함께 확인한다.
