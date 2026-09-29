create table admin_operator (
  operator_id uuid primary key,
  username varchar(80) not null unique,
  display_name varchar(120) not null,
  password_hash varchar(100) not null,
  enabled boolean not null default true,
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table admin_operator_role (
  operator_id uuid not null references admin_operator(operator_id) on delete cascade,
  role varchar(32) not null check (role in ('VIEWER','CS_OPERATOR','GAME_OPERATOR','ENGINEER','ADMIN')),
  primary key(operator_id, role)
);

create table admin_session (
  session_token_hash varchar(64) primary key,
  operator_id uuid not null references admin_operator(operator_id) on delete cascade,
  created_at timestamptz not null,
  last_seen_at timestamptz not null,
  expires_at timestamptz not null,
  revoked_at timestamptz
);

create index admin_session_operator_active_idx
  on admin_session(operator_id, expires_at)
  where revoked_at is null;

create table admin_audit_event (
  audit_id uuid primary key,
  occurred_at timestamptz not null,
  operator_id uuid references admin_operator(operator_id),
  username varchar(80) not null,
  action varchar(120) not null,
  target_type varchar(80) not null,
  target_id varchar(160),
  outcome varchar(16) not null check (outcome in ('SUCCEEDED','FAILED')),
  request_id uuid not null,
  remote_address varchar(64) not null
);

create index admin_audit_occurred_idx on admin_audit_event(occurred_at desc, audit_id desc);
create index admin_audit_operator_idx on admin_audit_event(operator_id, occurred_at desc);

create or replace function reject_admin_audit_mutation()
returns trigger
language plpgsql
as $$
begin
  raise exception 'admin_audit_event is append-only';
end;
$$;

create trigger admin_audit_event_reject_update
before update on admin_audit_event
for each row execute function reject_admin_audit_mutation();

create trigger admin_audit_event_reject_delete
before delete on admin_audit_event
for each row execute function reject_admin_audit_mutation();
