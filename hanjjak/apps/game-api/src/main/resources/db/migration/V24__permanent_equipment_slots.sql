drop table equipment_loadout;
drop table equipment_instance;
drop table equipment_command_record;

create table equipment_slot_state (
    account_id uuid not null references account(id) on delete cascade,
    slot varchar(16) not null check (slot in ('WEAPON','GLOVES','ARMOR','HELMET','CAPE','SHOES')),
    grade varchar(16) not null check (grade in ('NORMAL','RARE','EPIC','LEGENDARY')),
    enhancement_level integer not null check (enhancement_level between 1 and 30),
    unlocked_at timestamptz not null default now(),
    updated_at timestamptz not null default now(),
    primary key(account_id, slot)
);

create table equipment_command_record (
    command_id uuid primary key,
    account_id uuid not null references account(id) on delete cascade,
    idempotency_key uuid not null,
    fingerprint varchar(64) not null,
    result_json jsonb not null,
    created_at timestamptz not null default now(),
    unique(account_id, idempotency_key)
);
