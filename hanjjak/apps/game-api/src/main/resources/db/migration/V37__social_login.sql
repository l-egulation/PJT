alter table account alter column password_hash drop not null;
alter table account drop constraint if exists account_email_key;

alter table account add column login_email varchar(320);
update account set login_email = email where password_hash is not null;

create unique index account_local_email_unique
  on account (lower(login_email))
  where login_email is not null and deleted_at is null;

create table social_identity (
  provider varchar(32) not null,
  provider_subject varchar(255) not null,
  account_id uuid not null references account(id),
  email varchar(320),
  linked_at timestamptz not null,
  last_login_at timestamptz not null,
  primary key (provider, provider_subject),
  unique (account_id, provider)
);

create index social_identity_account_idx on social_identity(account_id);
