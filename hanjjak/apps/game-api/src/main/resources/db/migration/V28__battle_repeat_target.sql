alter table account_runtime_state
  add column repeat_stage_id varchar(32);

alter table account_runtime_state
  add constraint account_runtime_state_repeat_stage_format
  check (repeat_stage_id is null or repeat_stage_id ~ '^stage\.(0[1-4])-(0[1-9])$');

create table repeat_stage_command (
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  fingerprint varchar(64) not null,
  result_json jsonb not null,
  created_at timestamptz not null,
  primary key(account_id,idempotency_key)
);
