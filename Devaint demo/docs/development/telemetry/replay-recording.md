# 리플레이 기록·로컬 저장 설계안

[분야 개요](README.md)

## 문서의 범위와 현재 상태

Deviant 데모의 전체 플레이를 관찰하기 위한 클라이언트 기록 명세다. **로컬 기록기·바이너리 코덱·파일 복구·UI/전투 어댑터·Editor의 도식 관찰 창을 구현했다.** 전송 클라이언트와 Go 서버의 범위는 [서버 명세](replay-server-storage.md)를 따른다. 전체 실제 데모의 60분 플레이·배포 승인·실제 외부 운영과 구별한다. 기준 Editor/Player는 Unity 6000.6.5f1과 프로젝트 원본 패키지다. 현재 구현 범위와 사용 방법은 아래 10절을 따른다.

[데모 수집 설계](demo-telemetry.md)는 기본 통계·수집 목적·수집 정책을, 이 문서는 기록 수명·샘플링·바이너리 파일·로컬 복구를, [서버 저장 설계](replay-server-storage.md)는 전송·서버 검증·보관을 소유한다. 파일의 내용/해시 규칙은 이 문서를 단일 원본으로 삼는다. 원격 서비스 없이 로컬 기록과 디코딩부터 검증할 수 있어야 한다.

작업 순서·기존 코드 연결·용량 절감 실험·단계별 수락 조건은 [리플레이 기록 구현 계획](replay-recording-plan.md)을 따른다.

플레이 규칙은 [런 구조](../../design/run/run-structure.md), [UI·상호작용](../../design/ui/ui-and-interaction.md), [전투 규칙](../../design/combat/rules.md)을 따른다. 기록 때문에 게임 규칙·재화·판정·입력 소비 순서를 바꾸지 않는다.

## 1. 목표와 재생의 의미

- 첫 실행부터 자동으로 타이틀·마을·런·결과까지 관측한 전체 시간을 기록한다. 기존에 명시적으로 중단한 설정은 유지한다. 메뉴와 마을은 런 ID가 없어도 저장한다.
- 당시 카메라에서 플레이어·주변 적·UI·마우스를 보며 사망 원인·진행·UI 탐색을 확인한다.
- 기록한 상태/사건을 재생하며 AI·물리·피해 계산은 다시 실행하지 않는다. seed와 입력만으로 결정론적 재실행을 보장하지 않는다.
- 전체 시간은 유지하되 적의 연속 동작은 낮은 빈도로 표시한다. 프레임 단위 판정, 화면 밖 자유 카메라, 완전한 영상·이펙트·음향 복원은 목표에 포함하지 않는다.
- 압축 후 **5MiB/시간 이하를 최적화 목표**, **10MiB/시간 이내를 대표 플레이의 검토 기준**으로 둔다. 예상치·수락 보장값이나 강제 파일 절단 기준이 아니며 실제 데모의 탐험·메뉴·보스·다수 탄 상황으로 확인한다.

## 2. 기능 경계와 연결

현재 입력은 `LocalInput`이 단일 큐를 소비하고 전투 ingress가 실제 접수한다. `CombatLab/Editor/CombatInputRecorder.cs`의 live 관측은 고정 시나리오 재생 파일과 구별된다. 기존 실험용 기능을 전체 Player 녹화 구현으로 간주하지 않는다. [입력 구조](../presentation/ui/input-devices.md)와 [전투 결과 계약](../gameplay/combat/combat.md#명령-접수와-실행-결과)을 따른다.

| 제안 구성요소 | 책임 |
|---|---|
| 기록 세션 조립부 | 기록 설정·씬·런·전투 세션 수명을 관찰하고 기록 시작/종료 |
| 입력·월드·UI 어댑터 | 이미 해석된 입력, 원본 실행 결과, 표시 상태를 값으로 복사 |
| 기록기 | 순서·개체 ID·샘플 주기·변화분·누락·snapshot 생성 |
| 코덱/파일 writer | 이 문서의 제한된 바이너리 직렬화, 압축, 원자적 파일 확정 |
| 로컬 보관소 | manifest·대기 조각·용량/기간 제한·앱 재실행 복구 |
| 업로더 | 확정된 파일만 읽어 서버 계약 실행. 게임플레이 상태를 변경하지 않음 |
| 디코더·관찰 재생기 | 호환 버전 검사, snapshot 탐색, 사건·개체·UI 표시 |

명칭과 폴더는 구현 시 기존 모듈 경계에 맞춰 정한다. 새 전역 게임 이벤트 버스를 전제하지 않는다. polling cursor는 수집기 전용이며 다른 도구의 cursor를 소비하지 않는다. `LocalInput` 원본 큐를 다시 비우거나 관측 때문에 명령을 재실행하지 않는다. Unity 객체는 메인 스레드에서만 읽고 worker에는 불변 값·바이트 버퍼만 전달한다.

## 3. 식별자와 기록 수명

| 항목 | 계약 |
|---|---|
| `player_id` | 자동 전송 시작 시 생성해 유지하는 무작위 UUID. 현재는 파일 header 밖 collection binding에 저장하며 인증 비밀이 아님 |
| `session_id` | 앱 실행의 관측 세션 UUIDv7. UTC 생성 ms 포함, 지연 전송에도 원래 ID 유지 |
| `run_id` | 실제 출발 성공마다 생성. 마을·메뉴에서는 null, 같은 런의 챕터 이동에는 유지 |
| `segment_id` | 자동 기록 시작/재개, 씬·맵 변경, 전투 reset마다 새 UUIDv7 |
| `entity_id` | segment 내 증가 정수. 소멸 뒤 재사용 금지. 범위 재진입 시 같은 개체면 유지 |
| `record_seq` | 세션 내 모든 리플레이 레코드의 증가 uint64. 확정된 파일을 재전송해도 유지 |
| `chunk_index` | segment별 0부터 증가. 같은 index의 파일 내용은 확정 뒤 불변 |
| `realtime_ms` | 세션 기준 단조 실제 시간. 시계 변경·일시정지로 역행하지 않음 |
| `sim_tick`, `sim_time_ms` | 원본 전투 세션의 실제 tick/시각. 월드가 없으면 null |

기본 통계의 `event_id/event_seq`와 리플레이의 `record_seq`는 다른 순번 공간이다. 같은 사실이 두 저장 경로에 있으면 `source_event_id` 또는 원본 command/hit ID로 연결한다. snapshot은 사건을 다시 발생시키는 레코드가 아니다.

기록을 시작하면 현재 상태 snapshot부터 시작하며 과거를 소급 수집하지 않는다. 일시정지에서는 UI·실제 시간 변화만 기록하고 월드의 같은 상태를 반복하지 않는다. 창 포커스를 잃으면 포인터·게임 액션 관측을 멈추고 상태 변경을 남긴다. 복귀 시 현재 상태를 동기화한다. 씬 변경은 기존 segment를 닫고 새 segment를 시작하되 session/run ID는 해당 수명에 맞춰 유지한다.

사망은 월드 사건이며 즉시 전체 기록을 끝내지 않는다. 사망 모션·결과 화면·마을 귀환도 이어서 기록한다. 정상 종료는 남은 조각·최종 manifest를 가능한 범위에서 확정한다. 강제 종료는 열린 `.tmp` 조각을 잃을 수 있으며 종료 사건을 만들어 내지 않는다.

### 시간순 구간 ID

새 session/segment ID는 [RFC 9562 UUIDv7](https://www.rfc-editor.org/rfc/rfc9562.html#section-5.7)을 소문자 N 형식(하이픈 없는 32자리)으로 저장한다. 앞 48비트는 UTC Unix 밀리초이며 기존 UUID보다 길어지지 않는다. `ReplayUuid7`는 같은 프로세스의 동일 ms/시계 후퇴에서 74비트 suffix를 증가시켜 문자열 정렬 순서를 유지한다. suffix가 소진되면 논리 timestamp를 1ms 올린다. 새 ms에는 `Guid.NewGuid()`의 무작위 비트를 사용하며 Unity 게임 RNG를 변경하지 않는다.

UUID의 timestamp는 구간 ID 생성 시각 힌트다. 시계 후퇴 보정/서로 다른 실행과 기기의 시계 차이가 있으므로, 실제 세션 내 재생·연결은 기존 전역 record seq와 realtime_ms를 우선한다. 각 구간의 파일 번호는 여전히 0부터 시작하고 이전 해시 체인은 같은 구간 안에 적용한다. player/run ID 생성 정책은 바꾸지 않는다.

기존 UUIDv4 파일/경로/해시는 유지하고 v4/v7 모두 읽는다. UUID만으로 v4 생성 시각을 추정하지 않는다. `ReplayUuid7.TryGetUtc`로 v7 시각을 UTC로 해석할 수 있다. 관리자 API의 `recorded_from/to`는 등록된 session 시작 UTC에 해당 조각의 단조 실제 시간을 더한 값이며, UUID 생성 시각과 구별한다. 기록의 시각은 클라이언트 시계 기반이며 서버 수신 시각도 별도로 보존한다.

## 4. 기록 대상과 주기

수집 설정 `capture_profile_id`에 다음 제안값을 고정한다. 변경하면 설정 ID/revision과 `capture_profile_changed` 레코드를 남긴다.

| 대상 | 기록 내용 | 제안 주기 |
|---|---|---|
| 시작 상태 | 빌드/콘텐츠/UI 버전, 씬, 해석된 맵 레이아웃, 시작 장비·자원·표시 개체, UI | segment 처음. 필요한 참조 manifest는 불변 해시로 연결 |
| 플레이어·카메라 | 위치·방향·속도·행동/모션 기준 시각, 카메라 위치/크기 | 변할 때 최대 10Hz |
| 주변 적 | 관찰 범위 내 위치·방향·행동/모션·필요 보정 | 변할 때 최대 5Hz |
| 개체 수명 | 생성·소멸·범위 진입/이탈, ID·콘텐츠·부모/소유자 | 발생 즉시 |
| 탄 | 발사 위치·방향·속도·경로 정의, 반사·소멸, 유도 등 보정 | 사건 즉시, 필요한 보정만 최대 5Hz |
| 상태/전투 | 입력 edge/변경 값, 실제 공격 시작·필요 종료/취소/실패·확정 타격·회복·사망·phase·장비·가방·재화·포탈 | 변화/확정 시. AI 추적/이동의 반복 영수증·동일 held 입력·접수/소비 정상 영수증은 파일에서 제외. HP·쿨타임 전체를 매 샘플에 반복하지 않음 |
| UI | 열기/닫기·hover·selection·입력 포커스·활성화·탭·레이아웃·enabled/visible | 실제 변화 시 |
| 포인터 | 정규화 좌표·표시/잠금·창/viewport 내부 여부 | 움직일 때 최대 10Hz, 클릭/hover 변경은 별도 즉시 |
| 스크롤·지도 | 최종 offset·확대·이동 transform | 변화 중 최대 10Hz와 마지막 값 |
| 표시 snapshot | 현재 표시 대상 전체, 장비/가방 결과, UI·포인터·시계 상태 | 각 조각 시작과 유실/재연결 후 복구 시점. 고정 5초 간격의 전체 복사는 하지 않음 |
| 시간/제어·누락 | pause/time scale·입력 제한·창 활성, 누락 시간/개수/원인 | 변화/발견 즉시 |

관찰 범위는 카메라 rect 사방 2unit 여백을 제안한다. 화면 밖 궤적은 수집하지 않으며 재진입 snapshot으로 동기화한다. 범위 이탈을 실제 죽음으로 표시하지 않는다. teleport·낙사 복귀는 보간하지 않는다. 낮은 FPS로 생략된 샘플은 같은 상태를 복제해 채우지 않는다.

주기는 관측 후보의 상한이다. 값이 같은 필드는 생략하고 이동/포인터 후보는 고정 사건과 상태 경계를 anchor로 유지한 채 시간 기반 보간 오차 내에서 중간점을 줄인다. 조각 내 탐색은 시작 snapshot부터 delta를 적용한다. 오차·원본 사건 중복 방지·스트림별 측정은 구현 계획의 절감 정책을 따른다. 실제 gameplay 시간·판정·입력 원본을 바꾸지 않는다.

원본 결과가 유실되면 원인·해당 스트림·빠진 개수/시간 범위를 `capture_gap`으로 기록한다. 모르면 `unknown`으로 남긴다. 성공 여부를 입력이나 HP 차이로 추정하지 않는다. 치명적인 기록 오류는 게임을 유지하고 해당 기록을 `truncated`로 종료한다.

## 5. 메뉴·포인터와 개인정보 경계

hover는 커서 아래 대상, selection은 키보드/패드·코드가 선택한 대상, input focus는 입력을 받는 컨트롤이다. 창의 활성 상태와도 구별한다. 변화에는 이전/다음 target ID와 원인·입력 계열을 저장한다. 클릭은 down/up과 실제 `ui_activated`를 구분하고 장비/거래의 성공 결과와 연결한다.

목표 target은 `inventory.weapon.slot_1.palette.slot_2` 같은 안정적인 의미 ID다. 현재 공통 버튼은 작성 메서드·생성 순번, 나머지 Selectable은 자식 순번 기반의 **빌드 범위 ID**를 사용하며 표시 이름/텍스트는 저장하지 않는다. UI 구조가 달라진 빌드 간 의미 ID의 연속성을 보장하지 않는다. 화면은 marker의 open version과 segment를 함께 사용한다. 의미 기반 콘텐츠/slot ID 확대, 전체 팝업 부모/레이어 복원은 후속 범위다.

포인터는 viewport 내부 좌상단 원점 0~1 좌표를 1/4096 격자로 표현한다. 렌더 해상도·viewport pixel rect·UI scale·safe area도 보존한다. 창/viewport 밖이나 창 비활성의 좌표는 null이다. 마우스가 멈추면 직전 위치를 유지하되 포커스 상실을 화면 가장자리 좌표로 바꾸지 않는다.

문자 입력·클립보드·이메일/비밀번호 등 민감 화면·기록 설정/중단 화면의 상세 행동은 기록하지 않는다. 향후 해당 화면이 생기면 허용 목록과 수집 금지 영역을 먼저 등록한다. 이름·계정·하드웨어 serial·영상·오디오를 파일에 넣지 않는다. 기록은 기본 켜짐이며 서버 주소가 있으면 별도 전송 동의 없이 자동 업로드한다. 기록을 끄면 새 기록과 대기 전송을 중단하고 로컬 큐를 삭제한다.

## 6. 조각 파일 계약 제안 v1

### 6.1 파일·manifest

파일 확장자는 `.replay.gz`, 압축은 gzip이다. 아래의 **해제한 내용**과 **압축한 최종 바이트**를 구별한다. SHA-256은 업로드할 압축 파일 전체로 계산한다. gzip 생성 시각/옵션도 최초 확정 뒤 바꾸지 않으며 재시도 때 재압축하지 않는다.

최상위 session manifest에는 설치/세션/빌드/콘텐츠/UI/스키마/수집 설정·정책 revision, 관측 시작 UTC, segments를 둔다. segment manifest에는 run ID(null 허용), 씬/맵·레이아웃 참조, 시작/끝 실제 시간, 조각 목록·마지막 index/seq, 누락 범위·종료 이유를 둔다. 민감한 전체 세이브 파일 대신 허용 필드만 직렬화한다.

압축 해제한 조각의 구조는 다음 순서다.

1. magic 8바이트 ASCII `DVRPLY01`, container version uint16 LE(1), header 바이트 길이 uint32 LE.
2. UTF-8 JSON header. 최대 64KiB, 임의 확장 필드 금지. 공통 ID·버전·chunk index·예상 시간 범위·직전 조각 SHA-256과 필요한 콘텐츠 사전/레이아웃 참조를 포함한다.
3. 길이가 지정된 바이너리 레코드들. 첫 상태 그룹은 독립 표시 snapshot, 마지막 레코드는 footer다. snapshot보다 먼저 필요한 사전/레이아웃 레코드는 허용한다.
4. footer는 마지막 record seq·실제/논리 시각, 레코드 수, 누락 수/범위, 종료 이유를 포함한다. footer 뒤의 바이트는 허용하지 않는다.

업로드용 manifest에는 각 파일의 `segment_id`, `chunk_index`, `schema_version`, `capture_profile_id`, `previous_sha256`(최초 null), `sha256`, `compressed_bytes`, `uncompressed_bytes`, `start_realtime_ms`, `end_realtime_ms`, `first_record_seq`, `last_record_seq`, `record_count`, `gap_count`를 둔다. 종료 시간은 header의 상한 정보보다 footer의 실제 값을 우선하며 서버 제출값과 일치해야 한다. manifest revision은 증가시키고 이전 조각 항목의 해시/내용은 바꾸지 않는다.

### 6.2 바이너리 표현

레코드는 `length(varuint) + type_id(varuint) + record_seq_delta(varuint) + realtime_delta_ms(varuint) + sim_tick(ZigZag) + value_count(varuint) + signed_values(ZigZag)`다. String 레코드만 끝에 UTF-8 바이트 길이(varuint)와 text를 가진다. length는 뒤따르는 type부터 payload 끝까지의 길이이고 최대 256KiB다. 시간·순번 delta는 직전 레코드 기준이며 첫 기준은 header에 명시한다. 같은 시각 사건은 순번으로 정렬한다. 파일 순번은 후보 단순화 이후 부여하며 일반 증가값은 1이다.

정수는 unsigned LEB128(varuint, 최대 10바이트), signed 정수는 ZigZag 후 varuint, 고정 폭 숫자는 little-endian으로 제안한다. UUID/해시는 텍스트 대신 정해진 원본 바이트 순서로 payload에 넣고 header JSON에서는 표준 문자열을 사용한다. 문자열은 UTF-8 바이트 길이와 바이트, nullable은 presence byte로 구별한다. 문자열/사전 항목 수·길이와 각 payload의 필드 순서/정수 폭은 코드와 함께 버전별 registry로 확정한다. 이 문서만으로 코덱 구현·상호운용 완료를 주장하지 않는다.

현재 콘텐츠·액션·UI target 사전은 조각별로 선언한다. 외부 참조 manifest가 없는 독립 디코딩을 우선하며 맵 방 목록도 각 조각에서 압축한다. 불변 레이아웃/사전의 외부 manifest 분리는 후속 최적화다. Entity 레코드는 wire에서 `entity_id, changed_mask, changed_values`만 쓰며, 첫 상태는 11필드 전체 mask(2047)다. 디코더는 논리 12필드 상태를 복원한다. 다른 상태는 동일 payload 반복을 제외하고 변화 시 기록한다. SnapshotBegin/EntityExit에서 해당 baseline을 초기화한다.

위치는 구역 기준 1/256unit 격자로 제안한다. 일반 범위를 넘으면 확장 정수로 표현하고 절대 위치로 복귀시킬 수 있게 origin을 기록한다. 입력/판정 원본은 수정하지 않는다. 0값과 null, 미지원과 유실을 구별한다. NaN/Infinity·알 수 없는 enum은 거부한다. 신규 record type이나 payload 변경은 schema version을 올리고 지원하지 않는 재생기는 명시적으로 거부한다.

### 6.3 조각·크기 제한

30초 또는 해제한 내용 2MiB 중 먼저 도달하면 조각을 닫는다. 순간적인 대형 snapshot은 group ID·part index/count·총 크기를 가진 연속 레코드로 분할한다. snapshot 그룹은 다음 조각으로 넘기지 않는다. 제한 안에 완성할 수 없으면 기록을 중단하고 `truncated`로 남긴다. 각 조각은 완전 snapshot을 포함하거나 검증된 불변 manifest와 완성된 snapshot 그룹으로 독립 탐색 가능해야 한다.

초기 방어 상한은 압축 4MiB·해제 16MiB·레코드 256KiB다. 2MiB는 일반 조각 회전 기준이고 16MiB는 큰 시작 snapshot을 위한 별도 절대 상한이다. 임계값을 넘는 순간 레코드를 조용히 삭제하지 않는다. 압축기·직렬화 큐·사전·snapshot 메모리에도 상한을 두며 구체적 버퍼 수는 부하 시험에서 확정한다.

## 7. 로컬 저장과 업로더 인계

`Application.persistentDataPath` 아래 전용 `ReplayQueue`를 제안한다. 실제 절대 경로는 서버로 보내지 않는다. 각 session/segment 디렉터리에 manifest와 조각을 두고, 토큰은 리플레이 파일과 분리해 적절한 OS 보관 경계를 사용한다. `.tmp`는 업로드 대상이 아니다.

writer는 임시 조각 작성·footer 확정·gzip 종료·가능한 파일 flush 후 같은 볼륨에서 `.ready`로 원자적 rename한다. 그 다음 SHA/크기를 계산하고 manifest를 임시 파일+교체로 갱신한다. 물리적 디스크 보존 보장은 플랫폼에 따라 검증한다. 크래시로 ready와 manifest가 어긋나면 다음 시작에서 ready의 제한된 header/footer/해시를 검사해 인덱스를 복구한다.

로컬 파일 상태는 `writing`, `ready`, `uploading`, `awaiting_validation`, `committed`, `rejected`다. 업로드 2xx만으로 지우지 않고 서버가 검증 후 `committed`를 반환했을 때 삭제한다. 통신 중단 뒤 같은 파일·ID·해시로 재개한다. 전송 실패가 기록을 멈추게 하지 않되 오프라인 큐 상한을 적용한다.

오프라인 큐는 합계 256MiB 또는 7일 중 먼저 도달한 한도를 제안한다. 오래된 미전송 조각부터 제거하고 manifest에 `local_eviction` 누락을 남긴다. 기본 통계/최종 결과는 별도의 작은 큐를 우선 보존한다. 앱 시작 시 불완전 `.tmp`를 폐기하거나 검증 가능한 완전 레코드까지만 별도 손상 구간으로 표시하며 임의로 정상 footer를 만들지 않는다.

기록 수명과 업로드 수명은 독립이다. 다음 실행의 큐 전송이 현재 session/run에 섞이지 않아야 한다. 기록 중단은 새 기록·대기 전송을 중단하고 ready/임시 파일을 제거한다. 다시 켜도 이전 대기 기록을 복원하지 않는다. 서버에 이미 저장된 기록의 삭제는 서버 문서의 별도 요청을 사용한다.

## 8. 관찰 재생과 품질 표시

재생기는 정확한 build/content/UI/schema 호환성을 먼저 확인하고 해당 버전의 표시 자원을 사용한다. 임의 최신 카탈로그로 옛 리플레이를 해석하지 않는다. 자원/레이아웃이 없으면 `incompatible` 또는 `partial`로 표시한다.

탐색은 해당 시각 이전 완전 snapshot에서 시작해 사건·delta를 순서대로 적용한다. 위치 보간은 표시만 수행하고 피해·사망·보상 횟수는 원본 사건으로만 읽는다. 포인터 경로·hover·selection·input focus를 구분해 보여 주고, 공백·수집률 변경·관찰 범위 밖은 표시한다. 누락 snapshot과 지원하지 않는 payload를 정상 재생으로 숨기지 않는다.

## 9. 구현 수락 조건과 남은 결정

자동 검사와 실제 Player 수락 조건을 구별하며 Unity 6000.6.5f1에서 다음을 확인한다.

- 기록 중단 후 기록 0건과 명시적인 중단 설정 유지, 민감 입력·다른 앱 좌표 미수집, 입력 소비·전투 결과·게임 진행 무변경.
- 실제 수집 경로의 press/release·명령 접수/시작·피해·사망·UI 3종 포커스 구별과 원본 순서 보존.
- 일시정지·창 포커스·장치 전환·씬/챕터 이동·전투 reset·포탈·가방의 정지와 재개의 시간/상태 복원.
- 파일 round-trip, 알려진 바이너리 fixture, 버전 불일치·잘못된 길이/정수·손상·잘린 snapshot 거부.
- ready 확정 전/후 강제 종료, 디스크 부족·권한 오류·큐 초과·오프라인/재실행·검증 지연에서 손상/중복/유실의 명시.
- 탐험·메뉴·보스·다수 적/탄별 10분 이상과 대표 전체 런의 압축 바이트·주 스레드 시간·할당·메모리 측정. 5MiB/시간 최적화 목표·10MiB/시간 검토 기준과 재생 가독성을 함께 평가.

현재 schema 1의 registry와 모듈 배치는 구현과 아래 표를 기준으로 고정한다. 허용 payload별 추가 의미 검증·성능 예산·최종 sampling/양자화 품질, 다음 버전의 사전/레이아웃 최적화는 실제 플레이 검증 후 확정한다. 목표값은 운영 승인이나 검증 결과가 아니다.

## 10. 현재 구현 사용과 제한

업로드 대기열의 디렉터리 탐색·파일 읽기·SHA 검사는 데스크톱에서 읽기 전용 백그라운드 작업으로 실행한다. 게임 코루틴은 완료를 프레임마다 확인하며 동기적으로 기다리지 않는다. WebGL은 같은 탐색을 디렉터리 항목·후보 단위로 나눠 프레임당 약 2ms의 작업 예산으로 진행한다(개별 파일 작업의 시간까지 보장하는 상한은 아니다). 전송 요청·거절 표시·서버 확정 응답 뒤의 삭제는 소유 코루틴이 처리한다. 중단·재설정은 이전 탐색을 취소하고 그 결과를 버리며, 완료 전 다른 탐색을 겹쳐 시작하지 않는다. 기록·업로드 설정과 파일 형식은 이 실행 방식 때문에 바뀌지 않는다.

제품 타이틀·시작 마을·1/2챕터 씬은 첫 실행부터 기록을 자동 시작하며, 서버 주소가 설정돼 있으면 별도 전송 동의 없이 자동 업로드한다. 기록과 서버 전송은 백그라운드에서 동작하며, 제품 화면에 기록 버튼·상태 패널·기록 설정 창을 표시하지 않는다. 기록 호스트는 입력이나 게임 일시정지를 점유하지 않는다. PlayerPrefs의 `deviant.replay.local-consent.v1` 키는 기존의 명시적인 중단 값을 보존하며, 키가 없으면 활성화한다. 이전 빌드의 별도 업로드 선택은 사용하지 않는다. 무작위 player ID·수집·전송 범위는 [player_id 수집](player-data-collection.md), API/보관은 [서버 명세](replay-server-storage.md)를 따른다. 별도의 analytics-only 이벤트 API를 구현한 것은 아니다.

저장 위치는 `Application.persistentDataPath/ReplayQueue/{session}/{segment}/{index}.replay.gz.ready`다. 조각은 일반적으로 30초 또는 후보 버퍼 임계값에서 회전하고, 바이너리/Editor에서는 worker에서 경로 절감·gzip·검증·SHA 계산·파일 확정을 수행한다. Web은 C# 스레드에 의존하지 않도록 같은 bounded commit을 동기로 처리하고 `.jslib`로 브라우저 FS 동기화를 요청한다. Web의 프레임 비용·탭 종료·IndexedDB 지속성은 실제 Web Player 검증이 필요하다. `.ready.json`은 파일 메타데이터이며, `ReplayUploadClient`는 committed SHA 일치 후에만 `ReplayStore.Acknowledge`를 호출한다. 기록이 켜져 있고 서버 주소가 있을 때 확정한 새 파일에만 `.ready.collection.json` binding이 있고, 복구 과정에서 과거 로컬 파일을 전송 대상으로 바꾸지 않는다. 세션 `collection.json`/`session.json`은 원래 player/endpoint와 종료 상태를 다음 실행에서도 재시도하기 위한 메타데이터다. 누락 조각 표식과 복구 API는 파일을 정상 세션 종료로 바꾸지 않는다.

Editor 메뉴 **Tools > Deviant > Replay > Inspect recording**에서 ready 파일을 열면 세션 시간 slider로 액터/카메라 위치, UI rect, 포인터와 hover/selection/input focus·사건을 확인한다. 현재 관찰 창은 단일 조각을 표시하는 **도식 재생기**다. 실제 게임 아트·음향·모션, 전체 런 조각 자동 연결, 최종 제품 재생 UI를 구현한 상태는 아니다.

현재 파일 header의 정확한 키는 `session,segment,run,build,content,ui,profile,previous,schema,chunk,first,start`다. UUID는 N 형식 문자열, content는 현재 MeleeCatalog 텍스트의 SHA-256이며 전체 배포 매니페스트 해시는 아니다. build는 Player build GUID 또는 Editor에서는 `editor`, ui는 build 범위 버전, profile은 고정 `compact-v1`이다. schema/container는 1이며 배포한 뒤 레이아웃을 바꾸면 버전을 올려야 한다.

| type ID / 이름 | 논리 signed_values 필드 순서 |
|---|---|
| 1 String | 사전 ID; 뒤에 UTF-8 텍스트. 표시/입력 텍스트 금지 |
| 2/3 SnapshotBegin/End | 없음 |
| 4 Entity | ID,x256,y256,facing,HP bits,maxHP bits,shield bits,action ID,action start bits,weapon ID,kind,action phase. ID 0은 camera이며 action start 자리에 orthographic size bits |
| 5 EntityExit | ID,원인(1: 관찰 범위 이탈/소멸 미구분, 2: 4unit 초과 위치 변화의 표시 불연속. 실제 teleport 판정은 아님) |
| 6 Pointer | 0,x4096,y4096,cursor visible,cursor lock. 창/viewport 밖 좌표 -1 |
| 7 UiState | channel,target/value,detail. channel 0/1/2=hover/selection/focus, 3~5=mouse 버튼, 10/11=scroll x/y |
| 8 UiActivate | target ID,input 방식(현재 0=미구분) |
| 9 Input | action,phase,x256,y256,입력 계열,consumed,원본 입력 순번 |
| 10 Command | actor,kind,state,reason,원본 결과 순번,명령 순번,execution 사전 ID,sim Now bits,action ID |
| 11 Hit | source,target,원본 hit 순번,damage bits,absorbed bits,전HP bits,후HP bits,x256,y256,action ID,execution ID,원본 command 순번,sim Now bits,reflector,hit kind |
| 12 Clock | 창 활성,time scale bits,simulation paused,width,height,safe x bits,safe y bits,blocked input |
| 13 Gap | stream,유실 수,예약 값 |
| 14 Run | run 사전 ID,seed,chapter,kills,result(0 미확인,1 사망,2 클리어) |
| 15 Scene | 씬 경로 사전 ID,Unity 버전 사전 ID |
| 16 Footer | 마지막 본문 순번,본문 레코드 수 |
| 17 UiRect | target,x4096,y4096,w4096,h4096,interactable(-1 비활성),open version |
| 18 Screen | target,open version(0 닫힘) |
| 19 Layout | 방 ID,원형/종류 ID,x,y,w,h,mirror/main,source zone/챕터,seed. 1챕터는 원본 map 정수, 2챕터는 x/y/w/h를 256배 |
| 20 Weapon | slot,instance ID,form ID,special ID,capacity,available,period bits,next ready bits,active |
| 21 Growth | kind와 여섯 값. kind 0은 revision/attack level, 1은 palette/weapon/slot/color/part/level, 2는 bag ink instance/part, 3은 목록 끝 |
| 22 Economy | 재화 종류(0=gold),현재 잔액 |

bits는 IEEE754 float의 원본 int32 비트다. 런/재화·성장과 대부분 표시 상태는 관측 시점 상태이며, 거래 ID별 경제 영수증이나 모든 아이템 드롭/포탈 사건을 따로 연결한 것은 아니다. 현재 탄은 관찰 범위에서 위치를 최대 5Hz로 관측하고 경로를 단순화하며, 발사/반사/소멸의 완전한 원본 수명 계약은 후속이다. 같은 segment에서 화면 밖 개체 궤적은 수집하지 않는다.

모든 후보 값을 의미 있는 사건과 함께 조각 단위로 처리한다. 입력 event의 원본 순번, command/hit의 논리 tick/Now를 유지하지만 독립 원본 스트림 사이의 전역 발생 순서를 새로 보장하지 않는다. 상대 실제 시각은 수집 프레임의 시각이다. 런 밖 프로필 전체·영구 세이브, 네트워크 인증/업로드·보관 만료·원격 삭제, 프로필 JSON 생성기·고빈도 참조 대비 실제 60분 성능/용량 검증은 후속 범위다. 5MiB/시간 목표가 실제 게임에서 달성됐다고 주장하지 않는다.
