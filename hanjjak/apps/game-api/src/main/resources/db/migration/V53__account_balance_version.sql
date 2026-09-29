create table account_balance_state (
  account_id uuid primary key references account(id) on delete cascade,
  balance_version varchar(80) not null,
  applied_at timestamptz not null,
  retroactive_completed_at timestamptz,
  backfill_last_attempted_at timestamptz,
  backfill_failure_count integer not null default 0 check (backfill_failure_count >= 0),
  provisioned_during_rollout boolean not null default false
);


create function provision_account_balance_state() returns trigger
language plpgsql
as $$
begin
  insert into account_balance_state(account_id,balance_version,applied_at,provisioned_during_rollout)
  values (new.id,'enemy-v1-applied',now(),true)
  on conflict(account_id) do nothing;
  return new;
end;
$$;

create trigger account_balance_state_after_account_insert
after insert on account
for each row execute function provision_account_balance_state();
insert into account_balance_state(account_id,balance_version,applied_at)
select id,'enemy-v1-applied',now() from account
on conflict(account_id) do nothing;

create index account_balance_state_backfill_idx
  on account_balance_state(backfill_last_attempted_at nulls first,account_id)
  where retroactive_completed_at is null;
