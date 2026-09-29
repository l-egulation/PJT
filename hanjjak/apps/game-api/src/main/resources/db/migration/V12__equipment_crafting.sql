create table equipment_instance (
    equipment_id uuid primary key,
    account_id uuid not null references account(id) on delete cascade,
    slot varchar(16) not null check (slot in ('WEAPON','GLOVES','ARMOR','HELMET','CAPE','SHOES')),
    grade varchar(16) not null check (grade in ('NORMAL','RARE','EPIC','LEGENDARY')),
    enhancement_level integer not null check (enhancement_level between 1 and 30),
    locked boolean not null default false,
    created_at timestamptz not null default now()
);

create index equipment_instance_account_idx on equipment_instance(account_id, slot, created_at desc);

create table equipment_loadout (
    account_id uuid not null references account(id) on delete cascade,
    slot varchar(16) not null check (slot in ('WEAPON','GLOVES','ARMOR','HELMET','CAPE','SHOES')),
    equipment_id uuid not null references equipment_instance(equipment_id) on delete cascade,
    primary key(account_id, slot),
    unique(account_id, equipment_id)
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
