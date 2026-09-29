alter table admin_audit_event
  add column mutation boolean not null default false,
  add column reason varchar(500),
  add column before_summary text,
  add column after_summary text,
  add column idempotency_key uuid;

create index admin_audit_mutation_occurred_idx
  on admin_audit_event(mutation, occurred_at desc, audit_id desc);

create table admin_user_command (
  operator_id uuid not null references admin_operator(operator_id),
  idempotency_key uuid not null,
  account_id uuid not null references account(id),
  action varchar(120) not null,
  fingerprint varchar(64) not null,
  result_json text not null,
  created_at timestamptz not null default now(),
  primary key(operator_id, idempotency_key)
);

create index admin_user_command_account_created_idx
  on admin_user_command(account_id, created_at desc);
