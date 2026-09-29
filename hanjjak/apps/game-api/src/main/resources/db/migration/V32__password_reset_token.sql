create table password_reset_token (
  id uuid primary key,
  account_id uuid not null references account(id) on delete cascade,
  token_hash char(64) not null unique,
  created_at timestamptz not null,
  expires_at timestamptz not null,
  consumed_at timestamptz,
  check (expires_at > created_at)
);

create index password_reset_token_account_idx
  on password_reset_token(account_id, created_at desc);

create index password_reset_token_expiry_idx
  on password_reset_token(expires_at)
  where consumed_at is null;
