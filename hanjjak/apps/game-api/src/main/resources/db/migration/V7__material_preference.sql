create table material_preference (
  account_id uuid primary key references account(id),
  material_type varchar(24) not null check (material_type in ('POTATO','SWEET_POTATO','CORN')),
  selected_at timestamptz not null default now()
);

create table material_preference_command (
  command_id uuid primary key,
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  fingerprint varchar(64) not null,
  created_at timestamptz not null default now(),
  unique(account_id, idempotency_key)
);
