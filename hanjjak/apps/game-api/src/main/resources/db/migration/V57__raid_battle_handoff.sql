alter table battle_session
  add column raid_handoff_consumed_at timestamptz;

alter table raid_attempt
  add column resume_pending boolean not null default false;

create index battle_session_raid_handoff
  on battle_session(account_id, status, closed_at)
  where status='ABORTED' and raid_handoff_consumed_at is null;

create index raid_attempt_resume_pending
  on raid_attempt(id)
  where resume_pending;
