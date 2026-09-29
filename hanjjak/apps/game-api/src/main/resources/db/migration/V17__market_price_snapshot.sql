create table if not exists market_price_snapshot (
  item_id varchar(120) primary key,
  last_unit_price bigint not null check (last_unit_price > 0),
  average_unit_price numeric(19,2) not null check (average_unit_price >= 0),
  trade_count bigint not null default 0 check (trade_count >= 0),
  traded_quantity bigint not null default 0 check (traded_quantity >= 0),
  trade_amount bigint not null default 0 check (trade_amount >= 0),
  fee_amount bigint not null default 0 check (fee_amount >= 0),
  settlement_amount bigint not null default 0 check (settlement_amount >= 0),
  last_trade_at timestamptz not null,
  updated_at timestamptz not null default now()
);

create table if not exists processed_kafka_event (
  event_id uuid not null,
  consumer_name varchar(120) not null,
  processed_at timestamptz not null default now(),
  primary key (event_id, consumer_name)
);

create index if not exists market_price_snapshot_updated_idx on market_price_snapshot(updated_at desc);
