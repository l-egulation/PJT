create table raid_session (
  id uuid primary key,
  content_version varchar(64) not null,
  reward_version varchar(64) not null,
  settles_at timestamptz not null,
  status varchar(24) not null,
  cutoff_at timestamptz,
  settlement_cursor uuid,
  settlement_account_count bigint not null default 0,
  finalized_account_count bigint not null default 0,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint raid_session_status_ck check (status in ('OPEN','SETTLING','SETTLED_SUCCESS','SETTLED_FAILURE')),
  unique (id, cutoff_at),
  constraint raid_session_account_count_ck check (settlement_account_count >= 0),
  constraint raid_session_finalized_count_ck check (finalized_account_count >= 0 and finalized_account_count <= settlement_account_count),
  constraint raid_session_cutoff_ck check ((status = 'OPEN' and cutoff_at is null) or (status <> 'OPEN' and cutoff_at is not null)),
  constraint raid_session_cutoff_at_settlement_ck check (cutoff_at is null or cutoff_at = settles_at),
  constraint raid_session_settling_cursor_ck check (settlement_cursor is null or status <> 'OPEN')
);
create unique index raid_session_settles_at_uq on raid_session(settles_at);
create unique index raid_session_one_open on raid_session(status) where status = 'OPEN';
create index raid_session_due_open on raid_session(settles_at, id) where status = 'OPEN';
create index raid_session_settling on raid_session(settles_at, id) where status = 'SETTLING';

create table raid_account_state (
  session_id uuid not null,
  account_id uuid not null,
  current_slot_ordinal integer not null default 1,
  slots_terminal integer not null default 0,
  reward_attempts_started integer not null default 0,
  state_version bigint not null default 1,
  auto_finalized boolean not null default false,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  primary key (session_id, account_id),
  constraint raid_account_state_session_fk foreign key (session_id) references raid_session(id),
  constraint raid_account_state_account_fk foreign key (account_id) references account(id),
  constraint raid_account_state_slot_ck check (current_slot_ordinal between 1 and 3 and slots_terminal between 0 and 3 and slots_terminal <= current_slot_ordinal),
  constraint raid_account_state_attempts_ck check (reward_attempts_started between 0 and 9),
  constraint raid_account_state_version_ck check (state_version >= 1)
);
create index raid_account_state_account on raid_account_state(account_id, session_id);

create table raid_settlement_account (
  session_id uuid not null,
  account_id uuid not null,
  cutoff_at timestamptz not null,
  status varchar(16) not null default 'PENDING',
  created_at timestamptz not null default now(),
  finalized_at timestamptz,
  primary key (session_id, account_id),
  constraint raid_settlement_account_session_fk foreign key (session_id, cutoff_at) references raid_session(id, cutoff_at),
  constraint raid_settlement_account_account_fk foreign key (account_id) references account(id),
  constraint raid_settlement_account_status_ck check (status in ('PENDING','FINALIZED')),
  constraint raid_settlement_account_finalized_ck check ((status = 'PENDING' and finalized_at is null) or (status = 'FINALIZED' and finalized_at is not null))
);
create index raid_settlement_account_due on raid_settlement_account(session_id, account_id) where status = 'PENDING';

create table raid_reward_slot (
  id uuid primary key,
  session_id uuid not null,
  account_id uuid not null,
  ordinal integer not null,
  status varchar(16) not null,
  attempts_started integer not null default 0,
  current_attempt_id uuid,
  terminal_at timestamptz,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now(),
  constraint raid_reward_slot_session_account_fk foreign key (session_id, account_id) references raid_account_state(session_id, account_id),
  constraint raid_reward_slot_status_ck check (status in ('AVAILABLE','ACTIVE','RESULT_HELD','CONFIRMED','DISCARDED','EXPIRED')),
  constraint raid_reward_slot_ordinal_ck check (ordinal between 1 and 3),
  constraint raid_reward_slot_attempts_ck check (attempts_started between 0 and 3),
  constraint raid_reward_slot_terminal_ck check ((status in ('CONFIRMED','DISCARDED','EXPIRED')) = (terminal_at is not null)),
  constraint raid_reward_slot_available_ck check (status <> 'AVAILABLE' or current_attempt_id is null),
  unique (session_id, account_id, ordinal),
  unique (id, session_id, account_id)
);
create index raid_reward_slot_current on raid_reward_slot(session_id, account_id, ordinal);

create table raid_attempt (
  id uuid primary key,
  session_id uuid not null,
  account_id uuid not null,
  mode varchar(12) not null,
  slot_id uuid,
  attempt_ordinal integer not null,
  status varchar(16) not null,
  input_snapshot jsonb not null,
  result_json jsonb not null,
  timeline_json jsonb not null,
  seed bigint not null,
  started_at timestamptz not null,
  completable_at timestamptz not null,
  ended_at timestamptz,
  created_at timestamptz not null default now(),
  constraint raid_attempt_session_fk foreign key (session_id) references raid_session(id),
  constraint raid_attempt_account_fk foreign key (account_id) references account(id),
  constraint raid_attempt_mode_ck check (mode in ('REWARD','PRACTICE')),
  constraint raid_attempt_reward_ordinal_ck check ((mode = 'REWARD' and attempt_ordinal between 1 and 3) or (mode = 'PRACTICE' and attempt_ordinal >= 1)),
  constraint raid_attempt_status_ck check (status in ('RUNNING','RESULT_HELD','DISCARDED','CONFIRMED')),
  constraint raid_attempt_slot_mode_ck check ((mode = 'REWARD' and slot_id is not null) or (mode = 'PRACTICE' and slot_id is null)),
  constraint raid_attempt_end_ck check ((status = 'RUNNING' and ended_at is null) or (status <> 'RUNNING' and ended_at is not null)),
  constraint raid_attempt_completable_ck check (completable_at >= started_at),
  constraint raid_attempt_slot_fk foreign key (slot_id, session_id, account_id) references raid_reward_slot(id, session_id, account_id),
  unique (id, session_id, account_id),
  unique (id, session_id, account_id, slot_id)
);
create unique index raid_attempt_reward_ordinal on raid_attempt(slot_id, attempt_ordinal) where mode = 'REWARD';
create unique index raid_attempt_one_running_account on raid_attempt(account_id) where status = 'RUNNING';
create index raid_attempt_due on raid_attempt(completable_at, id) where status = 'RUNNING';

alter table raid_reward_slot add constraint raid_reward_slot_current_attempt_fk
  foreign key (current_attempt_id, session_id, account_id, id)
  references raid_attempt(id, session_id, account_id, slot_id);

create table raid_confirmed_result (
  id uuid primary key,
  attempt_id uuid not null,
  session_id uuid not null,
  account_id uuid not null,
  damage bigint not null,
  grade varchar(16) not null,
  seal_contribution bigint not null,
  reward_version varchar(64) not null,
  reward_tickets bigint not null,
  reward_gem_boxes bigint not null,
  reward_rice bigint not null,
  result_json jsonb not null,
  confirmed_at timestamptz not null default now(),
  constraint raid_confirmed_result_attempt_owner_fk foreign key (attempt_id, session_id, account_id) references raid_attempt(id, session_id, account_id),
  constraint raid_confirmed_result_session_fk foreign key (session_id) references raid_session(id),
  constraint raid_confirmed_result_account_fk foreign key (account_id) references account(id),
  constraint raid_confirmed_result_damage_ck check (damage >= 0),
  constraint raid_confirmed_result_grade_ck check (grade in ('PARTICIPATION','D','C','B','A','S','SS','SSS')),
  constraint raid_confirmed_result_contribution_ck check (seal_contribution >= 0),
  constraint raid_confirmed_result_tickets_ck check (reward_tickets >= 0 and reward_gem_boxes >= 0 and reward_rice >= 0),
  constraint raid_confirmed_result_identity_uq unique (id, session_id, account_id),
  unique (attempt_id)
);
create index raid_confirmed_result_account on raid_confirmed_result(session_id, account_id, confirmed_at);

create table raid_contribution (
  session_id uuid not null,
  account_id uuid not null,
  seal_contribution bigint not null default 0,
  total_damage bigint not null default 0,
  confirmed_attempts integer not null default 0,
  highest_damage bigint not null default 0,
  updated_at timestamptz not null default now(),
  primary key (session_id, account_id),
  constraint raid_contribution_session_fk foreign key (session_id) references raid_session(id),
  constraint raid_contribution_account_fk foreign key (account_id) references account(id),
  constraint raid_contribution_values_ck check (seal_contribution >= 0 and total_damage >= 0 and confirmed_attempts >= 0 and highest_damage >= 0)
);

create table raid_final_rank (
  session_id uuid not null,
  account_id uuid not null,
  competitive_rank integer not null,
  seal_contribution bigint not null,
  total_damage bigint not null,
  highest_damage bigint not null,
  created_at timestamptz not null default now(),
  primary key (session_id, account_id),
  constraint raid_final_rank_session_fk foreign key (session_id) references raid_session(id),
  constraint raid_final_rank_account_fk foreign key (account_id) references account(id),
  constraint raid_final_rank_values_ck check (competitive_rank >= 1 and seal_contribution >= 0 and total_damage >= 0 and highest_damage >= 0)
);
create index raid_final_rank_order on raid_final_rank(session_id, competitive_rank, account_id);

create table raid_reward_claim (
  id uuid primary key,
  session_id uuid not null,
  account_id uuid not null,
  source_kind varchar(16) not null,
  source_id uuid not null,
  auto_personal_result_id uuid generated always as (case when source_kind = 'AUTO_PERSONAL' then source_id else null end) stored,
  daily_rank_session_id uuid generated always as (case when source_kind = 'DAILY_RANK' then source_id else null end) stored,
  status varchar(16) not null default 'CLAIMABLE',
  reward_version varchar(64) not null,
  reward_tickets bigint not null,
  reward_gem_boxes bigint not null,
  reward_rice bigint not null,
  claimable_at timestamptz not null default now(),
  claimed_at timestamptz,
  created_at timestamptz not null default now(),
  constraint raid_reward_claim_session_fk foreign key (session_id) references raid_session(id),
  constraint raid_reward_claim_account_fk foreign key (account_id) references account(id),
  constraint raid_reward_claim_kind_ck check (source_kind in ('AUTO_PERSONAL','DAILY_RANK')),
  constraint raid_reward_claim_kind_source_ck check (source_kind = 'AUTO_PERSONAL' or source_id = session_id),
  constraint raid_reward_claim_auto_personal_source_fk foreign key (auto_personal_result_id, session_id, account_id)
    references raid_confirmed_result(id, session_id, account_id),
  constraint raid_reward_claim_daily_rank_source_fk foreign key (daily_rank_session_id, account_id)
    references raid_final_rank(session_id, account_id),
  constraint raid_reward_claim_status_ck check (status in ('CLAIMABLE','CLAIMED')),
  constraint raid_reward_claim_values_ck check (reward_tickets >= 0 and reward_gem_boxes >= 0 and reward_rice >= 0),
  constraint raid_reward_claim_claimed_ck check ((status = 'CLAIMABLE' and claimed_at is null) or (status = 'CLAIMED' and claimed_at is not null)),
  unique (source_kind, source_id, account_id)
);
create unique index raid_reward_claim_one_claimable on raid_reward_claim(source_kind, source_id, account_id) where status = 'CLAIMABLE';
create index raid_reward_claim_account_current on raid_reward_claim(account_id, claimable_at) where status = 'CLAIMABLE';

create table raid_command_record (
  command_id uuid primary key,
  account_id uuid not null,
  idempotency_key uuid not null,
  fingerprint varchar(128) not null,
  result_json jsonb,
  status varchar(16) not null,
  created_at timestamptz not null default now(),
  expires_at timestamptz,
  constraint raid_command_record_account_fk foreign key (account_id) references account(id),
  constraint raid_command_record_status_ck check (status in ('SUCCEEDED','FAILED','IN_PROGRESS')),
  unique (account_id, idempotency_key)
);
