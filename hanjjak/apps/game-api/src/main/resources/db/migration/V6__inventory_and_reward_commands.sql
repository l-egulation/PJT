create table if not exists inventory_stack (
  account_id uuid not null references account(id),
  item_id varchar(120) not null,
  quantity bigint not null check (quantity >= 0),
  reserved_quantity bigint not null default 0 check (reserved_quantity >= 0 and reserved_quantity <= quantity),
  acquired_sequence bigint not null default 1 check (acquired_sequence >= 1),
  primary key(account_id, item_id)
);

alter table inventory_stack add column if not exists reserved_quantity bigint not null default 0;
alter table inventory_stack add column if not exists acquired_sequence bigint not null default 1;
alter table inventory_stack drop constraint if exists inventory_stack_reserved_quantity_check;
alter table inventory_stack add constraint inventory_stack_reserved_quantity_check check (reserved_quantity >= 0 and reserved_quantity <= quantity);

create table inventory_instance (
  instance_id uuid primary key,
  account_id uuid not null references account(id),
  item_id varchar(120) not null,
  reserved_for_sale boolean not null default false,
  acquired_sequence bigint not null check (acquired_sequence >= 1)
);

create index inventory_instance_account_item_idx on inventory_instance(account_id, item_id);

create table inventory_reward_command (
  command_id uuid primary key,
  account_id uuid not null references account(id),
  idempotency_key uuid not null,
  fingerprint varchar(64) not null,
  result_json jsonb not null,
  created_at timestamptz not null default now(),
  expires_at timestamptz not null default (now() + interval '7 days'),
  unique(account_id, idempotency_key)
);
