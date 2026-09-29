create table offline_job (
  job_id uuid primary key,
  account_id uuid not null references account(id),
  game_session_id uuid not null references game_session(id),
  stage_id varchar(32) not null,
  content_version varchar(120) not null,
  reward_rate_snapshot_json jsonb not null,
  combat_snapshot_json jsonb,
  started_at timestamptz not null,
  last_heartbeat_at timestamptz not null,
  accrual_ended_at timestamptz,
  status varchar(16) not null check (status in ('ACTIVE','CLAIMABLE','CLAIMED','CANCELLED')),
  result_json jsonb,
  claimed_at timestamptz,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  version bigint not null default 0
);

create unique index offline_job_one_open_per_account
  on offline_job(account_id)
  where status in ('ACTIVE','CLAIMABLE');

create index offline_job_active_heartbeat_idx
  on offline_job(status,last_heartbeat_at)
  where status='ACTIVE';

create table offline_reward_command (
  command_id uuid primary key,
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  fingerprint varchar(64) not null,
  result_json jsonb not null,
  created_at timestamptz not null default now(),
  unique(account_id,idempotency_key)
);