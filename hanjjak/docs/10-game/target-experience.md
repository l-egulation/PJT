---
doc_kind: ssot
owner_domain: player-experience
authority_level: working
---

# Target Experience

## 목표 경험

- 사용자가 다른 일을 하는 동안에도 백그라운드 탭의 메인 전투·성장·보상은 서버 기준 경과시간 또는 서버 확정 결과로 이어진다.
- 플레이어는 반복 전투를 직접 조작하기보다 성장 선택과 진행 속도를 관리한다.
- 탭 discard·브라우저 종료·기기 절전·네트워크 단절 중에는 메인 전투·성장·보상이 중단된다.

## 경험의 경계

단일 브라우저 탭은 펫의 존재감, 메인 전투와 선택적 관리 개입을 함께 제공한다. Picture-in-Picture는 지원 브라우저의 선택 기능이며, 미지원·거부 시 단일 탭 UI가 기준이다. 화면 상태 공유와 표시 책임은 [플레이어 UX 도메인](../30-domain/player/ux/README.md), 전체 진행은 [Core Loop](../20-design/core-loop.md)와 [Session Flow](../20-design/session-flow.md)를 따른다.
