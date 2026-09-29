create table account_active_session (
  account_id uuid primary key references account(id) on delete cascade,
  session_id varchar(128) not null unique,
  activated_at timestamptz not null
);

create index account_active_session_activated_at_idx on account_active_session(activated_at);
