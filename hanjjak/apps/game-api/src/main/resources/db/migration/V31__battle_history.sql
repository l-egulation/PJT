create table battle_history_event (
    event_sequence bigint generated always as identity unique,
    event_id uuid primary key,
    account_id uuid not null references account(id) on delete cascade,
    occurred_at timestamptz not null,
    event_type varchar(32) not null check (event_type in (
        'STAGE_ENTERED',
        'STAGE_CLEARED',
        'STAGE_FAILED',
        'DUNGEON_ENTERED',
        'RETURNED'
    )),
    stage_id varchar(32),
    dungeon_id varchar(128),
    result_code varchar(64) not null,
    content_version varchar(64),
    combat_snapshot jsonb,
    source_type varchar(32) not null check (source_type in ('BATTLE_SESSION', 'GEM_DUNGEON_CHALLENGE')),
    source_id uuid not null,
    constraint battle_history_one_target check ((stage_id is null) <> (dungeon_id is null)),
    constraint battle_history_stage_event_target check (
        event_type not in ('STAGE_ENTERED', 'STAGE_CLEARED', 'STAGE_FAILED') or stage_id is not null
    ),
    constraint battle_history_dungeon_event_target check (
        event_type <> 'DUNGEON_ENTERED' or dungeon_id is not null
    ),
    constraint battle_history_failure_snapshot check (
        event_type <> 'STAGE_FAILED' or (content_version is not null and combat_snapshot is not null)
    ),
    unique (account_id, source_type, source_id, event_type, result_code)
);

create index battle_history_event_account_recent_idx
    on battle_history_event(account_id, occurred_at desc, event_sequence desc);

create index battle_history_event_account_stage_failure_idx
    on battle_history_event(account_id, stage_id, occurred_at desc, event_sequence desc)
    where event_type = 'STAGE_FAILED';
