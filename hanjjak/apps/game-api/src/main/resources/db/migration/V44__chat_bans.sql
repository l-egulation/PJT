create table chat_ban (
  account_id uuid primary key references account(id),
  banned_until timestamptz,
  reason varchar(240) not null,
  created_at timestamptz not null default now()
);
