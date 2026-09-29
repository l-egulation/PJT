alter table admin_operator
  drop column password_hash,
  alter column username type varchar(255),
  add column gitlab_user_id bigint;

alter table admin_audit_event
  alter column username type varchar(255);

update admin_session
set revoked_at = now()
where revoked_at is null;

create unique index admin_operator_gitlab_user_id_unique
  on admin_operator(gitlab_user_id)
  where gitlab_user_id is not null;

create table admin_gitlab_oauth_flow (
  flow_id uuid primary key,
  state_token_hash varchar(64) not null unique,
  code_verifier varchar(128) not null,
  consumed boolean not null default false,
  created_at timestamptz not null,
  expires_at timestamptz not null
);

create index admin_gitlab_oauth_flow_expiry_idx
  on admin_gitlab_oauth_flow(expires_at);
