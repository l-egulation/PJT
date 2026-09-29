alter table account add column deleted_at timestamptz;
alter table account drop constraint account_email_key;
create unique index account_active_email_unique on account(email) where deleted_at is null;
