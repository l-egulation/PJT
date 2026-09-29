create table progression_reward_command (
  command_id uuid primary key,
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  fingerprint varchar(64) not null,
  result_json jsonb not null,
  created_at timestamptz not null default now(),
  unique(account_id, idempotency_key)
);
