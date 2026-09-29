-- Local beta test account seed.
--
-- Raises an existing account to a build that clears every gem dungeon stage 1~10 on all three
-- bosses under the accelerated beta content, and gives it 30 level 5 gems.
--
-- Sign up through the app first, then run:
--   psql -h localhost -U hanjjak -d hanjjak -v email=you@example.com -f tools/local/seed-beta-tester.sql
--
-- Safe to re-run: every write is keyed on deterministic ids and upserts.
-- Local development only. Never run this against a shared or production database.

\if :{?email}
\else
\set email 'tester@example.com'
\endif

\set ON_ERROR_STOP on

begin;

drop table if exists seed_target;
create temporary table seed_target as
select id from account where email = :'email';

do $$
begin
    if (select count(*) from seed_target) <> 1 then
        raise exception 'no account found for that email — sign up in the app first';
    end if;
end
$$;

-- Level 60 with the matching total experience, so the next battle reward does not roll it back.
-- ProgressionRules.totalExperienceForLevel(60) = 500 * 60 * 59.
update "character"
set level = 60,
    experience = greatest(experience, 500::bigint * 60 * 59),
    rice = greatest(rice, 100000000)
where account_id = (select id from seed_target);

-- Chapters 1~3 cleared (this is what unlocks gems at 1-5 and the 3-10 equipment promotions),
-- chapter 4 unlocked but not cleared.
insert into stage_progress(account_id, stage_id, unlocked, first_cleared_at, highest_clear_count, content_version)
select (select id from seed_target),
       'stage.' || lpad(chapter::text, 2, '0') || '-' || lpad(number::text, 2, '0'),
       true,
       case when chapter <= 3 then now() else null end,
       case when chapter <= 3 then 1 else 0 end,
       'enemy-v1-applied'
from generate_series(1, 4) chapter, generate_series(1, 10) number
on conflict (account_id, stage_id) do update
set unlocked = true,
    first_cleared_at = coalesce(stage_progress.first_cleared_at, excluded.first_cleared_at),
    highest_clear_count = greatest(stage_progress.highest_clear_count, excluded.highest_clear_count);

-- All six slots at EPIC +30. EquipmentRules.q = grade.index * 39 + enhancementLevel = 108 per slot,
-- above the Q 98 reference build the stage 10 numbers were tuned against.
insert into equipment_slot_state(account_id, slot, grade, enhancement_level)
select (select id from seed_target), slot, 'EPIC', 30
from unnest(array['WEAPON', 'GLOVES', 'ARMOR', 'HELMET', 'CAPE', 'SHOES']) slot
on conflict (account_id, slot) do update
set grade = 'EPIC', enhancement_level = 30, updated_at = now();

-- Every skill at EPIC 10, the highest grade the MVP content allows.
insert into skill_state(account_id, skill_id, grade, level, failure_bonus_basis_points)
select (select id from seed_target), skill_id, 'EPIC', 10, 0
from unnest(array['active_heavy', 'active_dot', 'active_haste', 'active_basic_amp',
                  'passive_critical', 'passive_all_damage']) skill_id
on conflict (account_id, skill_id) do update set grade = 'EPIC', level = 10;

delete from skill_loadout where account_id = (select id from seed_target);
insert into skill_loadout(account_id, slot_index, skill_id)
values ((select id from seed_target), 1, 'active_haste'),
       ((select id from seed_target), 2, 'active_basic_amp'),
       ((select id from seed_target), 3, 'active_heavy'),
       ((select id from seed_target), 4, 'active_dot');

-- 30 level 5 gems. Levels 1~5 only roll the two fixed options, so these are 18 flat attack (70)
-- and 12 flat max HP (700). Ids are derived from the account id, so re-running updates in place.
do $$
declare
    acct uuid;
    base_seq bigint;
    attack_slot uuid;
    hp_slot uuid;
    i integer;
    gem uuid;
    opt text;
    val integer;
begin
    select id into acct from seed_target;
    select coalesce(max(seq), 0) into base_seq
    from (
        select acquired_sequence as seq from inventory_stack where account_id = acct
        union all
        select acquired_sequence from inventory_instance where account_id = acct
    ) owned;

    -- V26 groups identical gems into one inventory slot keyed by the first member's id.
    attack_slot := md5('hanjjak-beta-seed:gem:' || acct::text || ':1')::uuid;
    hp_slot := md5('hanjjak-beta-seed:gem:' || acct::text || ':19')::uuid;

    for i in 1..30 loop
        if i <= 18 then
            opt := 'FLAT_ATTACK';
            val := 70;
        else
            opt := 'FLAT_HP';
            val := 700;
        end if;
        gem := md5('hanjjak-beta-seed:gem:' || acct::text || ':' || i)::uuid;

        insert into gem_instance(gem_id, account_id, level, "option", value, locked, content_version, created_at)
        values (gem, acct, 5, opt, val, false, 'gem-v1-draft', now())
        on conflict (gem_id) do update
        set level = 5, "option" = excluded."option", value = excluded.value, locked = false;

        insert into inventory_instance(instance_id, account_id, item_id, reserved_for_sale,
                                       acquired_sequence, stack_key, inventory_slot_id, locked)
        values (gem, acct, 'gem:5:' || lower(opt), false, base_seq + i,
                '5:' || opt || ':' || val,
                case when opt = 'FLAT_ATTACK' then attack_slot else hp_slot end, false)
        on conflict (instance_id) do update
        set item_id = excluded.item_id, stack_key = excluded.stack_key,
            inventory_slot_id = excluded.inventory_slot_id, locked = false, reserved_for_sale = false;
    end loop;

    delete from gem_loadout where account_id = acct;
    -- BERSERK and ARMORED take attack, SURVIVAL takes max HP, MAIN is mixed.
    -- Gems 16~18 and 28~30 stay unequipped so the inventory, lock and fusion screens have stock.
    for i in 1..6 loop
        insert into gem_loadout(account_id, preset, slot_index, gem_id)
        values (acct, 'BERSERK', i, md5('hanjjak-beta-seed:gem:' || acct::text || ':' || i)::uuid),
               (acct, 'ARMORED', i, md5('hanjjak-beta-seed:gem:' || acct::text || ':' || (i + 6))::uuid),
               (acct, 'SURVIVAL', i, md5('hanjjak-beta-seed:gem:' || acct::text || ':' || (i + 21))::uuid);
    end loop;
    for i in 1..3 loop
        insert into gem_loadout(account_id, preset, slot_index, gem_id)
        values (acct, 'MAIN', i, md5('hanjjak-beta-seed:gem:' || acct::text || ':' || (i + 12))::uuid),
               (acct, 'MAIN', i + 3, md5('hanjjak-beta-seed:gem:' || acct::text || ':' || (i + 18))::uuid);
    end loop;
end
$$;

-- Full entry tickets, and the test access row that lets this account pick any of the three bosses
-- instead of waiting for the hourly rotation.
insert into gem_account_state(account_id, tickets, last_ticket_at)
values ((select id from seed_target), 5, now())
on conflict (account_id) do update set tickets = 5, last_ticket_at = now();

insert into gem_dungeon_test_access(account_id)
values ((select id from seed_target))
on conflict (account_id) do nothing;

update account set state_version = state_version + 1 where id = (select id from seed_target);

select c.nickname,
       c.level,
       (select count(*) from equipment_slot_state e where e.account_id = c.account_id) as equipment_slots,
       (select count(*) from skill_state s where s.account_id = c.account_id) as skills,
       (select count(*) from gem_instance g where g.account_id = c.account_id and g.level = 5) as level5_gems,
       (select tickets from gem_account_state t where t.account_id = c.account_id) as tickets
from "character" c
where c.account_id = (select id from seed_target);

commit;
