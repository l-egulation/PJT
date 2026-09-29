-- Clean cutover from listings and buy reservations to one price-time ordered book.
create table market_instrument (
  instrument_id uuid primary key,
  canonical_key varchar(200) not null unique,
  item_id varchar(120) not null,
  display_name varchar(160) not null,
  category varchar(32) not null,
  attributes jsonb not null default '{}'::jsonb,
  status varchar(24) not null default 'ACTIVE' check (status in ('ACTIVE','CANCELLING','INACTIVE')),
  revision bigint not null default 1 check (revision >= 1),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);
create unique index market_instrument_item_active_idx on market_instrument(item_id) where status <> 'INACTIVE';

create table market_order (
  order_id uuid primary key,
  account_id uuid not null references account(id),
  instrument_id uuid not null references market_instrument(instrument_id),
  side varchar(4) not null check (side in ('BUY','SELL')),
  time_in_force varchar(3) not null check (time_in_force in ('GTC','IOC')),
  initial_quantity bigint not null check (initial_quantity > 0),
  filled_quantity bigint not null default 0 check (filled_quantity >= 0 and filled_quantity <= initial_quantity),
  remaining_quantity bigint not null check (remaining_quantity >= 0 and remaining_quantity <= initial_quantity),
  limit_unit_price bigint not null check (limit_unit_price between 10 and 999999),
  reserved_rice bigint not null default 0 check (reserved_rice >= 0),
  status varchar(32) not null check (status in ('ACTIVE','FILLED','CANCELLED','EXPIRED','PARTIALLY_FILLED','PARTIALLY_FILLED_CLOSED','RECOVERY_REVIEW')),
  priority_at timestamptz not null,
  created_at timestamptz not null,
  updated_at timestamptz not null,
  expires_at timestamptz,
  display_name_snapshot varchar(160) not null,
  instance_ids uuid[] not null default '{}',
  legacy_listing_id uuid unique references market_listing(listing_id),
  legacy_buy_order_id uuid unique references market_buy_order(buy_order_id),
  check (filled_quantity + remaining_quantity <= initial_quantity),
  check ((side='BUY' and time_in_force='GTC' and reserved_rice=remaining_quantity*limit_unit_price) or (side='BUY' and time_in_force='IOC' and reserved_rice=0) or (side='SELL' and reserved_rice=0)),
  check ((time_in_force='GTC' and expires_at is not null) or (time_in_force='IOC' and expires_at is null)),
  check (time_in_force='GTC' or status not in ('ACTIVE','PARTIALLY_FILLED'))
);
create index market_order_sell_match_idx on market_order(instrument_id,limit_unit_price,priority_at,order_id) where side='SELL' and status in ('ACTIVE','PARTIALLY_FILLED');
create index market_order_buy_match_idx on market_order(instrument_id,limit_unit_price desc,priority_at,order_id) where side='BUY' and status in ('ACTIVE','PARTIALLY_FILLED');
create index market_order_account_created_idx on market_order(account_id,created_at desc,order_id desc);
create index market_order_expiry_idx on market_order(expires_at,order_id) where status in ('ACTIVE','PARTIALLY_FILLED');

create table market_order_mutation_history (
  mutation_id uuid primary key,
  account_id uuid not null references account(id),
  instrument_id uuid not null references market_instrument(instrument_id),
  order_id uuid not null references market_order(order_id),
  side varchar(4) not null check (side in ('BUY','SELL')),
  mutation_type varchar(16) not null check (mutation_type in ('CREATE','UPDATE','CANCEL')),
  command_id uuid not null,
  created_at timestamptz not null
);
create index market_order_mutation_rate_idx on market_order_mutation_history(account_id,created_at desc);
create index market_order_mutation_pattern_idx on market_order_mutation_history(account_id,instrument_id,side,created_at desc);

create table market_account_control (
  account_id uuid primary key references account(id),
  created_at timestamptz not null default now(),
  updated_at timestamptz not null default now()
);

create table market_delivery (
  delivery_id uuid primary key,
  account_id uuid not null references account(id),
  instrument_id uuid not null references market_instrument(instrument_id),
  order_id uuid not null references market_order(order_id),
  trade_id uuid references market_trade(trade_id),
  source varchar(24) not null check (source in ('BUY_FILL','SELL_CANCEL_RETURN','SELL_EXPIRE_RETURN')),
  item_id varchar(120) not null,
  instance_ids uuid[] not null default '{}',
  quantity bigint not null check (quantity > 0),
  display_name_snapshot varchar(160) not null,
  created_at timestamptz not null,
  claimed_at timestamptz,
  source_account_id uuid references account(id),
  legacy_purchase_delivery_id uuid unique references market_purchase_delivery(delivery_id)
);
create index market_delivery_account_claimable_idx on market_delivery(account_id,created_at,delivery_id) where claimed_at is null;

create table market_unread_event (
  account_id uuid not null references account(id),
  stream varchar(16) not null check (stream in ('FILLS','DELIVERIES','SETTLEMENTS','EXPIRATIONS')),
  sequence bigint not null check (sequence > 0),
  source_id uuid not null,
  created_at timestamptz not null,
  primary key(account_id,stream,sequence),
  unique(account_id,stream,source_id)
);
create table market_unread_cursor (
  account_id uuid not null references account(id),
  stream varchar(16) not null check (stream in ('FILLS','DELIVERIES','SETTLEMENTS','EXPIRATIONS')),
  latest_sequence bigint not null default 0 check (latest_sequence >= 0),
  read_sequence bigint not null default 0 check (read_sequence >= 0 and read_sequence <= latest_sequence),
  issued_sequence bigint not null default 0 check (issued_sequence >= 0 and issued_sequence <= latest_sequence),
  primary key(account_id,stream)
);

create table market_migration_discrepancy (
  discrepancy_id uuid primary key,
  source_type varchar(32) not null,
  source_id uuid not null,
  account_id uuid not null references account(id),
  item_id varchar(120) not null,
  expected_quantity bigint not null,
  actual_quantity bigint not null,
  expected_rice bigint not null,
  actual_rice bigint not null,
  status varchar(24) not null default 'RECOVERY_REVIEW',
  resolution varchar(32),
  rationale text,
  resolved_by uuid,
  created_at timestamptz not null default now(),
  resolved_at timestamptz,
  unique(source_type,source_id)
);

alter table market_trade alter column listing_id drop not null;
alter table market_trade add column maker_order_id uuid references market_order(order_id);
alter table market_trade add column taker_order_id uuid references market_order(order_id);
alter table market_trade add column instrument_id uuid references market_instrument(instrument_id);
alter table market_trade add column display_name_snapshot varchar(160);
create index market_trade_instrument_filled_idx on market_trade(instrument_id,filled_at desc,trade_id desc);
-- Runtime catalog synchronization creates deterministic market_instrument rows before the following
-- application-startup conversion runs. Active listings retain their external escrow and created_at
-- priority. Active legacy buy orders are cancelled and refunded; their completed order shells and
-- purchase deliveries are retained so old rows remain reachable from the unified history.
