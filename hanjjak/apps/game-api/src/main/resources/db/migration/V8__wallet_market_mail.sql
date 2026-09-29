create table if not exists wallet_balance (
  account_id uuid primary key references account(id),
  balance bigint not null check (balance >= 0),
  updated_at timestamptz not null default now()
);

insert into wallet_balance(account_id, balance, updated_at)
select account_id, rice, now()
from character
on conflict(account_id) do nothing;

create table if not exists wallet_ledger (
  ledger_id uuid primary key,
  account_id uuid not null references account(id),
  delta bigint not null check (delta <> 0),
  balance_after bigint not null check (balance_after >= 0),
  source_type varchar(64) not null,
  source_id uuid not null,
  created_at timestamptz not null default now()
);

create index if not exists wallet_ledger_account_created_idx on wallet_ledger(account_id, created_at desc);

create or replace function create_wallet_balance_for_character()
returns trigger
language plpgsql
as $$
begin
  insert into wallet_balance(account_id, balance, updated_at)
  values (new.account_id, new.rice, now())
  on conflict(account_id) do nothing;
  return new;
end;
$$;

drop trigger if exists character_wallet_balance_insert on character;
create trigger character_wallet_balance_insert
after insert on character
for each row execute function create_wallet_balance_for_character();

create table if not exists market_revision (
  item_id varchar(120) primary key,
  revision bigint not null default 1 check (revision >= 1)
);

create table if not exists market_listing (
  listing_id uuid primary key,
  seller_account_id uuid not null references account(id),
  item_id varchar(120) not null,
  initial_quantity bigint not null check (initial_quantity > 0),
  remaining_quantity bigint not null check (remaining_quantity >= 0 and remaining_quantity <= initial_quantity),
  unit_price bigint not null check (unit_price > 0),
  status varchar(16) not null check (status in ('ACTIVE','FILLED','CANCELLED','EXPIRED')),
  created_at timestamptz not null,
  updated_at timestamptz not null,
  expires_at timestamptz not null
);

create index if not exists market_listing_active_item_order_idx on market_listing(item_id, unit_price, created_at, listing_id) where status = 'ACTIVE';
create index if not exists market_listing_seller_created_idx on market_listing(seller_account_id, created_at desc, listing_id desc);
create index if not exists market_listing_expiry_idx on market_listing(item_id, expires_at, listing_id) where status = 'ACTIVE';

create table if not exists market_trade (
  trade_id uuid primary key,
  listing_id uuid not null references market_listing(listing_id),
  buyer_account_id uuid not null references account(id),
  seller_account_id uuid not null references account(id),
  item_id varchar(120) not null,
  quantity bigint not null check (quantity > 0),
  unit_price bigint not null check (unit_price > 0),
  total_price bigint not null check (total_price > 0),
  fee bigint not null check (fee >= 0),
  settlement_amount bigint not null check (settlement_amount >= 0),
  filled_at timestamptz not null
);

create index if not exists market_trade_buyer_filled_idx on market_trade(buyer_account_id, filled_at desc, trade_id desc);
create index if not exists market_trade_seller_filled_idx on market_trade(seller_account_id, filled_at desc, trade_id desc);
create index if not exists market_trade_listing_idx on market_trade(listing_id);

create table if not exists mail_message (
  mail_id uuid primary key,
  account_id uuid not null references account(id),
  type varchar(64) not null,
  rice_amount bigint not null check (rice_amount >= 0),
  source_trade_id uuid references market_trade(trade_id),
  claimed boolean not null default false,
  created_at timestamptz not null default now(),
  claimed_at timestamptz
);

create index if not exists mail_message_account_created_idx on mail_message(account_id, created_at desc, mail_id desc);
create index if not exists mail_message_claimable_idx on mail_message(account_id, created_at, mail_id) where claimed = false;

create table if not exists outbox_event (
  event_id uuid primary key,
  event_type varchar(120) not null,
  aggregate_id uuid not null,
  payload jsonb not null,
  status varchar(16) not null default 'PENDING' check (status in ('PENDING','SENT','FAILED')),
  attempt_count integer not null default 0 check (attempt_count >= 0),
  next_retry_at timestamptz,
  created_at timestamptz not null default now()
);

create index if not exists outbox_event_pending_idx on outbox_event(status, next_retry_at, created_at) where status = 'PENDING';
