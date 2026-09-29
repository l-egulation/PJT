create table stage_first_clear_reward (
  reward_id uuid primary key,
  account_id uuid not null references account(id) on delete cascade,
  stage_id varchar(32) not null,
  reward_version varchar(80) not null,
  source_id uuid not null,
  rice_granted bigint not null check (rice_granted >= 0),
  unlocked_skill_id varchar(80),
  item_status varchar(16) not null check (item_status in ('CLAIMED','PENDING')),
  items_json jsonb not null,
  required_slots integer not null check (required_slots >= 0),
  claimed_at timestamptz,
  created_at timestamptz not null,
  unique(account_id,stage_id,reward_version)
);

create index stage_first_clear_reward_pending_idx
  on stage_first_clear_reward(account_id,created_at,reward_id)
  where item_status='PENDING';

create table first_clear_reward_claim_command (
  account_id uuid not null references account(id) on delete cascade,
  idempotency_key uuid not null,
  reward_id uuid not null references stage_first_clear_reward(reward_id),
  fingerprint varchar(128) not null,
  result_json jsonb not null,
  created_at timestamptz not null,
  primary key(account_id,idempotency_key)
);
