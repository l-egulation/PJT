alter table raid_session
  add column settlement_phase varchar(32) not null default 'ROTATE_SESSION',
  add column completed_at timestamptz;

update raid_session
set settlement_phase = case
      when status in ('SETTLED_SUCCESS', 'SETTLED_FAILURE') then 'SETTLED'
      when status = 'SETTLING' then 'FREEZE_WORKSET'
      else 'ROTATE_SESSION'
    end,
    completed_at = case
      when status in ('SETTLED_SUCCESS', 'SETTLED_FAILURE') then updated_at
      else null
    end;

alter table raid_session
  add constraint raid_session_phase_ck check (settlement_phase in ('ROTATE_SESSION','FREEZE_WORKSET','FINALIZE_ACCOUNTS','MATERIALIZE_RANKS','SETTLED')),
  add constraint raid_session_completion_ck check ((status in ('SETTLED_SUCCESS','SETTLED_FAILURE')) = (settlement_phase = 'SETTLED')),
  add constraint raid_session_completed_at_ck check ((settlement_phase = 'SETTLED') = (completed_at is not null));

create index raid_session_phase_due on raid_session(status, settlement_phase, settles_at, id);
