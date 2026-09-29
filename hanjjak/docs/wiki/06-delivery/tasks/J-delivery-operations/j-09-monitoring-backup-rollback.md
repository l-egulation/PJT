---
doc_kind: task
owner_domain: delivery
task_code: 'J-09'
task_area: 'J 데이터·테스트·배포'
task_type: '배포'
priority: 'P1'
target_chapters: '공통'
planning_status: '미확인'
development_status: '부분 구현'
verification_status: '부분 검증'
deadline: ''
source_url: 'https://app.notion.com/p/20c06c8781b0832ab5ff01fbd57f78bc'
notion_id: '20c06c87-81b0-832a-b5ff-01fbd57f78bc'
snapshot_date: '2026-08-28'
---

# J-09 모니터링·백업·롤백 구성

> 작업 상태와 완료 증거의 SSOT. 이벤트 규칙은 [데이터·이벤트](../../../../40-systems/event-system/ssot.md), 운영 규칙은 [배포·운영](../../../../50-architecture/operations.md)을 따른다.

## 완료 기준

오류·성능·경제 지표를 확인하고 데이터 백업과 이전 버전 롤백을 실행할 수 있다.

## 선행 작업

J-08

## 비고

K3s backup·application rollback·수동 DB restore를 수정하고 격리 환경에서 실행했다. Registry 보존 기록은 당시 이력이며 현재 운영 경로에서는 Registry를 사용하지 않는다. 실제 운영 외부 backup disk·모니터링 backend 인수는 남아 있다. 정책은 배포·운영 SSOT를 따르며 아래에는 실행 결과만 기록한다.

## 증거 링크

- 기존 Compose 기준선: `infra/deploy/backup-postgres.*`, `restore-postgres.*`, `rollback.*`, `smoke.*`, `compose.yaml`; 2026-09-08 임시 환경 backup/restore smoke 통과
- K3s 구현: `infra/k8s/base.yaml`의 backup CronJob, `deploy.sh` pre-deploy backup, `rollback.sh` application-only rollback, `restore-postgres.sh` explicit DB restore
- 2026-09-08 당시 Bash syntax·Compose config 통과 이후, 2026-09-09 실제 격리 K3s/PostgreSQL에서 아래 drill을 추가 수행했다. 환경과 배포 증거는 [J-08](./j-08-production-build-cicd.md)을 참조한다.
- [restore-postgres.sh](../../../../../infra/k8s/restore-postgres.sh): 손상 archive를 exit 1로 거부했고 live DB의 `after-backup` 값이 유지됐다. 유효 dump는 별도 scratch DB에 먼저 복원한 뒤 실제 DB를 복원했고 값이 `before-backup`으로 돌아왔다.
- migration 라벨의 활성 Job을 둔 복원 시도는 `Active migration/backup jobs exist; refusing destructive restore`로 실패했다. 검증용 Job 종료 후 재시도는 통과했다. Job을 스크립트가 임의 삭제하지 않았다.
- 가동 중인 API·web·tunnel을 대상으로 복원한 뒤 해당 Pod 조회 결과는 `[]`, 모든 앱 Deployment replica는 0, `postgres-backup.spec.suspend`는 true였다. 명시적 `deploy.sh` 실행으로 앱 재개를 확인했다. backup CronJob은 자동 재개되지 않는다.
- [maintenance-lock.sh](../../../../../infra/k8s/maintenance-lock.sh)를 deploy·rollback·restore가 공유한다. 파일 검사·archive 검사·scratch 복원 실패는 실제 DB 파괴 전에 종료하고 원본 dump를 보존한다. 최종 restore I/O 실패·디스크 장애 후 복구는 아직 별도 fault-injection 대상이다.
- 다음 Registry prune·보존 검증은 2026-09-09 당시 Registry 기반 설계의 이력이다. 현재 직접 K3s containerd build 기준에는 적용하지 않는다.
- 당시 `prune-registry.sh`는 격리 TLS Registry에서 생성 시각 기반 보존·삭제와 보호 이미지 검사를 통과했다.
- 현재 rollback image 보존은 K3s containerd tag와 실제 Deployment·Service annotation을 기준으로 별도 확인한다.
- 2026-09-09 실제 운영 K3s에서 pre-deploy backup Job이 완료되고 `/var/backups/hanjjak`에 custom-format dump가 생성됨을 확인했다. Compose에서 이동한 dump도 보존했다. 다만 현재 backup 경로는 VM system disk 안이며 별도 물리 disk가 아니므로 disaster-recovery 완료 근거는 아니다.
- 실제 운영 migration 충돌 배포는 새 workload 전환 전에 실패했고 Service selector와 기존 API/web image가 유지되어 외부 HTTP 200을 계속 제공했다. 이후 충돌을 V31로 이동한 배포가 통과했다. application rollback과 파괴적 production restore drill은 사용자 데이터 위험 때문에 수행하지 않았고 격리 K3s 검증만 유지한다.
- 최종 Compose→K3s 전환 dump는 16,109,074 bytes, SHA-256 `0a656ee48beebcbb3200394b70535c856f70969b2cacc0db95abeeb5b67a0b06`이며 Windows와 VM에서 checksum이 일치했다. restore 뒤 public 57개 base table의 row count 목록 checksum은 source와 target 모두 `92f13b246a51eef7834bfd4d863afd715409643888555bee5191b05464d0d45a`로 일치했다. 원본 Compose volume과 `/var/backups/hanjjak/compose-to-k3s-final-20260909.dump`를 보존한다.
- Kafka 운영 지표는 `event-consumers`의 `KafkaOpsMetrics`가 기록하고, 2026-09-12 변경에서 두 consumer listener와 관리자 Kafka 조회를 연결했다. 운영 배포와 외부 metrics backend 연결은 아직 수행하지 않았다.
- Kafka broker·`event-consumers` 운영 rollout, 실제 거래 smoke, 외부 backup disk·monitoring backend·restore drill은 배포 후 확인 대상이다.

## 운영 환경에서 남은 확인

- 외부 backup disk·모니터링 backend·실제 데이터 규모의 복원 시간과 장애 시 복구를 확인한다. scratch 복원에는 추가 DB 공간이 필요하다.
- K3s containerd의 현재·rollback commit SHA image 보존과 disk GC 기준을 별도 확인한다.
- 실제 운영 restore drill은 유지보수 창과 사용자 승인 뒤 수행한다.

## 상태

| 항목 | 값 |
| --- | --- |
| 기획 | 미확인 |
| 개발 | 부분 구현 |
| 검증 | 부분 검증 |
