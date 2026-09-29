create table battle_enemy_settlement (
    settlement_id uuid primary key,
    battle_session_id uuid not null references battle_session(id) on delete cascade,
    enemy_index integer not null check (enemy_index between 1 and 21),
    boss boolean not null,
    eligible_tick integer not null check (eligible_tick >= 0),
    eligible_at timestamptz not null,
    reward_content_version varchar(64) not null,
    requested_reward_json jsonb not null,
    experience bigint not null check (experience >= 0),
    rice bigint not null check (rice >= 0),
    settled_at timestamptz,
    reward_result_json jsonb,
    progression_result_json jsonb,
    constraint battle_enemy_settlement_boss_index check ((boss and enemy_index between 1 and 21) or (not boss and enemy_index between 1 and 20)),
    constraint battle_enemy_settlement_result_complete check (
        (settled_at is null and reward_result_json is null and progression_result_json is null)
        or (settled_at is not null and reward_result_json is not null and progression_result_json is not null)
    ),
    unique (battle_session_id, enemy_index)
);

create index battle_enemy_settlement_pending_idx
    on battle_enemy_settlement(battle_session_id, enemy_index)
    where settled_at is null;

update battle_session
set status = 'ABORTED', closed_at = now()
where status = 'ACTIVE';
