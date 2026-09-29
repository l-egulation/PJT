# 문서 정보 구조

---
doc_kind: ssot
owner_domain: governance
authority_level: applied
---

현재 문서의 canonical 구조와 각 경로의 책임을 정의한다. 기존 `docs/wiki`의 일반 문서는 새 계층으로 이동했고, 이 디렉터리에는 운영 규칙과 보존 경로만 남긴다.

## 계층

```text
docs/
├─ 10-game/                         Game Vision
├─ 20-design/                       Game Loop / Player Experience
├─ 30-domain/                       게임 규칙과 개념 SSOT
│  └─ <domain>/
│     ├─ ssot.md                    해당 책임의 유일한 규칙 정본
│     └─ features/                  기능 경로·작업 연결
├─ 40-systems/                      논리 Gameplay Systems
├─ 50-architecture/                 Runtime Architecture·저장·운영
├─ 60-content/                      Content model·버전·배치
├─ 70-plans/                        제안·출시·기능 계획
├─ 80-decisions/                    GDR / ADR 결정 상태와 전파
├─ 90-reference/                    용어·원문 참고·실험
└─ wiki/
   ├─ 00-meta/                      운영 규칙과 SSOT 지도
   ├─ 05-meetings/records/          회의 원문 전사
   ├─ 06-delivery/tasks/            A–K 실행 상태·완료 증거
   └─ 99-archive/notion/            Notion 원문·스키마·첨부 메타데이터
```

## 읽기 경로

질문이 생기면 `10-game → 20-design → 30-domain → 40-systems → 50-architecture` 순서로 내려가고, 제안은 `70-plans`, 결정 상태는 `80-decisions`, 참고 자료는 `90-reference`에서 확인한다.

| 질문 | 먼저 읽을 곳 | 기록하지 않는 곳 |
| --- | --- | --- |
| 어떤 경험을 만드는가 | `10-game` | 시스템·작업 문서 |
| 플레이 루프는 무엇인가 | `20-design` | 회의록만으로 확정 |
| 게임 규칙·수치·상태 전이는 무엇인가 | `30-domain/<domain>/ssot.md` | 기능 라우트·작업 문서 |
| 논리 시스템의 책임은 무엇인가 | `40-systems` | 런타임 서비스 문서 |
| 파일·서비스·저장·운영 경계는 무엇인가 | `50-architecture` | 기능 작업 문서 |
| 콘텐츠 모델과 버전 배치는 어디에 있는가 | `60-content`와 해당 도메인 SSOT | 회의록의 임시 표 |
| 제안과 출시 계획은 무엇인가 | `70-plans` | 현재 정책 SSOT |
| 무엇이 결정되었고 무엇이 미해결인가 | `80-decisions/README.md` | 회의 원문만으로 확정 |
| 원문·실험·용어는 무엇인가 | `90-reference`와 `99-archive/notion` | archive를 현재 정책으로 사용 |

기능 경로와 작업 문서가 다른 내용을 갖게 되면 기능 문서는 링크만 남기고, 규칙은 SSOT, 실행 상태는 A–K 작업 문서로 되돌린다.
