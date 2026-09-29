create table gem_dungeon_progress (
    account_id uuid not null references account(id) on delete cascade,
    boss_type varchar(16) not null check (boss_type in ('SURVIVAL','BERSERK','ARMORED')),
    highest_cleared_stage integer not null default 0 check (highest_cleared_stage between 0 and 10),
    primary key (account_id, boss_type)
);

create table gem_dungeon_first_clear (
    account_id uuid not null references account(id) on delete cascade,
    boss_type varchar(16) not null check (boss_type in ('SURVIVAL','BERSERK','ARMORED')),
    stage integer not null check (stage between 1 and 10),
    claimed_at timestamptz not null,
    challenge_id uuid not null,
    primary key (account_id, boss_type, stage)
);

create table gem_dungeon_challenge (
    challenge_id uuid primary key,
    account_id uuid not null references account(id) on delete cascade,
    boss_type varchar(16) not null check (boss_type in ('SURVIVAL','BERSERK','ARMORED')),
    stage integer not null check (stage between 1 and 10),
    kst_date date not null,
    status varchar(16) not null check (status in ('ACTIVE','SUCCEEDED','FAILED','ABORTED','EXPIRED')),
    seed bigint not null,
    content_version varchar(64) not null,
    preset_snapshot jsonb not null,
    player_snapshot jsonb not null,
    battle_result jsonb not null,
    reward_gem_boxes bigint not null check (reward_gem_boxes >= 0),
    started_at timestamptz not null,
    minimum_complete_at timestamptz not null,
    expires_at timestamptz not null,
    completed_at timestamptz,
    check (minimum_complete_at >= started_at),
    check (expires_at > minimum_complete_at)
);

create unique index gem_dungeon_one_active_per_account
    on gem_dungeon_challenge(account_id)
    where status = 'ACTIVE';

alter table gem_dungeon_first_clear
    add constraint gem_dungeon_first_clear_challenge_fk
    foreign key (challenge_id) references gem_dungeon_challenge(challenge_id);

create table inventory_capacity_reservation (
    reservation_id uuid primary key,
    challenge_id uuid not null unique references gem_dungeon_challenge(challenge_id) on delete cascade,
    account_id uuid not null references account(id) on delete cascade,
    item_id varchar(128) not null,
    reservation_kind varchar(16) not null check (reservation_kind in ('STACK_RIGHT','EMPTY_SLOT')),
    status varchar(16) not null check (status in ('ACTIVE','CONSUMED','RELEASED')),
    created_at timestamptz not null,
    resolved_at timestamptz
);

create index inventory_capacity_reservation_account_active_idx
    on inventory_capacity_reservation(account_id)
    where status = 'ACTIVE';
