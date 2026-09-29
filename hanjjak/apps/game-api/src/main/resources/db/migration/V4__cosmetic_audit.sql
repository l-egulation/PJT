create table cosmetic_audit_event (
  event_id uuid primary key,
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  operation varchar(40) not null,
  content_version varchar(64) not null,
  request_fingerprint varchar(128) not null,
  result_json jsonb not null,
  created_at timestamptz not null default now(),
  unique(account_id, idempotency_key)
);
