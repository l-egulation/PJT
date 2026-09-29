create table account_runtime_state (
  account_id uuid primary key references account(id),
  current_stage_id varchar(32) not null default 'stage.01-01'
);

create table game_session (
  id uuid primary key,
  account_id uuid not null references account(id),
  status varchar(16) not null check (status in ('ACTIVE','REPLACED','CLOSED','EXPIRED')),
  created_at timestamptz not null,
  last_heartbeat_at timestamptz not null,
  closed_at timestamptz
);

create unique index game_session_one_active_per_account
  on game_session(account_id)
  where status='ACTIVE';

create table game_session_command (
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  fingerprint varchar(128) not null,
  game_session_id uuid not null references game_session(id),
  result_status varchar(16) not null,
  result_heartbeat_at timestamptz not null,
  created_at timestamptz not null,
  primary key(account_id,idempotency_key)
);
