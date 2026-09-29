create table market_buy_order (
  buy_order_id uuid primary key,
  buyer_account_id uuid not null references account(id),
  item_id varchar(120) not null,
  initial_quantity bigint not null check (initial_quantity between 1 and 999),
  remaining_quantity bigint not null check (remaining_quantity between 0 and initial_quantity),
  max_unit_price bigint not null check (max_unit_price between 10 and 999990 and max_unit_price % 10 = 0),
  reserved_rice bigint not null check (reserved_rice >= 0),
  status varchar(16) not null check (status in ('ACTIVE','FILLED','CANCELLED','EXPIRED')),
  created_at timestamptz not null,
  updated_at timestamptz not null,
  expires_at timestamptz not null,
  check (
    (status = 'ACTIVE' and remaining_quantity > 0 and reserved_rice = remaining_quantity * max_unit_price)
    or (status <> 'ACTIVE' and reserved_rice = 0)
  )
);

create index market_buy_order_match_idx
  on market_buy_order(item_id, max_unit_price desc, created_at, buy_order_id)
  where status = 'ACTIVE';
create index market_buy_order_buyer_created_idx
  on market_buy_order(buyer_account_id, created_at desc, buy_order_id desc);
create index market_buy_order_expiry_idx
  on market_buy_order(expires_at, buy_order_id)
  where status = 'ACTIVE';

alter table market_trade add column buy_order_id uuid references market_buy_order(buy_order_id);
create index market_trade_buy_order_idx on market_trade(buy_order_id) where buy_order_id is not null;

create table market_purchase_delivery (
  delivery_id uuid primary key,
  buy_order_id uuid not null references market_buy_order(buy_order_id),
  trade_id uuid not null unique references market_trade(trade_id),
  buyer_account_id uuid not null references account(id),
  item_id varchar(120) not null,
  quantity bigint not null check (quantity > 0),
  created_at timestamptz not null,
  claimed_at timestamptz
);

create index market_purchase_delivery_buyer_claimable_idx
  on market_purchase_delivery(buyer_account_id, created_at desc, delivery_id desc)
  where claimed_at is null;
