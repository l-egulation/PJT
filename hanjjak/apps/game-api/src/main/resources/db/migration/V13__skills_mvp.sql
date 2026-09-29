create table skill_state (
    account_id uuid not null references account(id) on delete cascade,
    skill_id varchar(48) not null,
    grade varchar(16) not null check (grade in ('NORMAL','RARE','EPIC','LEGENDARY')),
    level integer not null check (level between 1 and 10),
    failure_bonus_basis_points integer not null default 0 check (failure_bonus_basis_points between 0 and 10000),
    primary key(account_id, skill_id)
);

create table skill_loadout (
    account_id uuid not null references account(id) on delete cascade,
    slot_index integer not null check (slot_index between 1 and 4),
    skill_id varchar(48) not null,
    primary key(account_id, slot_index),
    unique(account_id, skill_id)
);

create table skill_command_record (
    command_id uuid primary key,
    account_id uuid not null references account(id) on delete cascade,
    idempotency_key uuid not null,
    fingerprint varchar(64) not null,
    result_json jsonb not null,
    created_at timestamptz not null default now(),
    unique(account_id, idempotency_key)
);
