-- V14 created an unnamed column CHECK with the exact predicate "tickets between 0 and 3".
-- Replace only that legacy ceiling constraint. Other current or future CHECK constraints that
-- happen to mention tickets must remain intact.
do $$
declare
    constraint_name text;
begin
    for constraint_name in
        select con.conname
        from pg_constraint con
        join pg_class rel on rel.oid = con.conrelid
        join pg_namespace nsp on nsp.oid = rel.relnamespace
        where con.contype = 'c'
          and rel.relname = 'gem_account_state'
          and nsp.nspname = current_schema()
          and pg_get_constraintdef(con.oid) = 'CHECK (((tickets >= 0) AND (tickets <= 3)))'
    loop
        execute format('alter table gem_account_state drop constraint %I', constraint_name);
    end loop;
end
$$;

alter table gem_account_state add constraint gem_account_state_tickets_check check (tickets >= 0);
