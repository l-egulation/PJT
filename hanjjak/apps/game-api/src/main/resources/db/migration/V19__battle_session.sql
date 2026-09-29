create table battle_session (
  id uuid primary key,
  account_id uuid not null references account(id),
  game_session_id uuid not null references game_session(id),
  token_hash varchar(64) not null,
  status varchar(16) not null check (status in ('ACTIVE','COMPLETED','ABORTED','EXPIRED')),
  stage_id varchar(32) not null,
  content_version varchar(64) not null,
  seed bigint not null,
  logical_tick_basis integer not null default 0,
  input_json jsonb not null,
  started_at timestamptz not null,
  last_heartbeat_at timestamptz not null,
  completable_at timestamptz not null,
  closed_at timestamptz,
  predicted_hash varchar(64),
  rendering_checkpoint_json jsonb,
  result_json jsonb
);

create unique index battle_session_one_active_per_account
  on battle_session(account_id)
  where status='ACTIVE';

create index battle_session_active_heartbeat
  on battle_session(last_heartbeat_at)
  where status='ACTIVE';

create table battle_session_command (
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  fingerprint varchar(64) not null,
  result_json jsonb not null,
  created_at timestamptz not null,
  primary key(account_id,idempotency_key)
);
