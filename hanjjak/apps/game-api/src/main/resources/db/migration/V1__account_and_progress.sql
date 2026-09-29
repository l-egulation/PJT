create table account (
  id uuid primary key,
  email varchar(320) not null unique,
  password_hash varchar(100) not null,
  state_version bigint not null check (state_version >= 1),
  created_at timestamptz not null
);
create table character (
  id uuid primary key,
  account_id uuid not null unique references account(id),
  level integer not null check (level between 1 and 500),
  experience bigint not null check (experience >= 0),
  rice bigint not null check (rice >= 0)
);
create table auth_session (
  id uuid primary key,
  account_id uuid not null references account(id),
  expires_at timestamptz not null,
  revoked_at timestamptz
);
create table stage_progress (
  account_id uuid not null references account(id),
  stage_id varchar(32) not null,
  unlocked boolean not null,
  first_cleared_at timestamptz,
  highest_clear_count bigint not null default 0,
  content_version varchar(64) not null,
  primary key(account_id, stage_id)
);
create table command_record (
  command_id uuid primary key,
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  fingerprint varchar(128) not null,
  status varchar(16) not null,
  result_json jsonb,
  expires_at timestamptz not null,
  unique(account_id, idempotency_key)
);
