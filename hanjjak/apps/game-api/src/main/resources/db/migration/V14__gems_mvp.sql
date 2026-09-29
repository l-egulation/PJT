create table gem_account_state (
    account_id uuid primary key references account(id) on delete cascade,
    tickets integer not null check (tickets between 0 and 3),
    last_ticket_at timestamptz not null
);

create table gem_instance (
    gem_id uuid primary key,
    account_id uuid not null references account(id) on delete cascade,
    level integer not null check (level between 1 and 7),
    option varchar(32) not null check (option in ('FLAT_ATTACK','FLAT_HP','ATTACK_PERCENT','FLAT_PENETRATION','CRITICAL_CHANCE','HASTE')),
    value integer not null,
    locked boolean not null,
    content_version varchar(64) not null,
    created_at timestamptz not null default now()
);

create index gem_instance_account_idx on gem_instance(account_id, level, created_at desc);

create table gem_loadout (
    account_id uuid not null references account(id) on delete cascade,
    preset varchar(16) not null check (preset in ('MAIN','SURVIVAL','BERSERK','ARMORED')),
    slot_index integer not null check (slot_index between 1 and 6),
    gem_id uuid not null references gem_instance(gem_id) on delete cascade,
    primary key(account_id, preset, slot_index),
    unique(account_id, preset, gem_id)
);

create table gem_command_record (
    command_id uuid primary key,
    account_id uuid not null references account(id) on delete cascade,
    idempotency_key uuid not null,
    fingerprint varchar(64) not null,
    result_json jsonb not null,
    created_at timestamptz not null default now(),
    unique(account_id, idempotency_key)
);
