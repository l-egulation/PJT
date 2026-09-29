create table arena_rating (
  account_id uuid primary key references account(id) on delete cascade,
  rating integer not null default 1000 check (rating between 0 and 99999),
  wins integer not null default 0 check (wins >= 0),
  losses integer not null default 0 check (losses >= 0),
  updated_at timestamptz not null default now()
);

create table arena_battle (
  battle_id uuid primary key,
  idempotency_key uuid not null,
  attacker_account_id uuid not null references account(id),
  defender_account_id uuid not null references account(id),
  status varchar(16) not null check (status in ('COMPLETED')),
  content_version varchar(64) not null,
  seed bigint not null,
  input_json jsonb not null,
  result_json jsonb not null,
  events_json jsonb not null,
  created_at timestamptz not null default now(),
  unique(attacker_account_id, idempotency_key)
);

create index arena_battle_attacker_created_idx on arena_battle(attacker_account_id, created_at desc, battle_id desc);
create index arena_battle_defender_created_idx on arena_battle(defender_account_id, created_at desc, battle_id desc);
