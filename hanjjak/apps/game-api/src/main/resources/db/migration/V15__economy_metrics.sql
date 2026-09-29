create table if not exists economy_metric_daily (
  metric_date date not null,
  item_id varchar(80) not null,
  listed_quantity bigint not null default 0 check (listed_quantity >= 0),
  cancelled_quantity bigint not null default 0 check (cancelled_quantity >= 0),
  trade_count bigint not null default 0 check (trade_count >= 0),
  traded_quantity bigint not null default 0 check (traded_quantity >= 0),
  trade_amount bigint not null default 0 check (trade_amount >= 0),
  fee_amount bigint not null default 0 check (fee_amount >= 0),
  settlement_amount bigint not null default 0 check (settlement_amount >= 0),
  dropped_quantity bigint not null default 0 check (dropped_quantity >= 0),
  consumed_quantity bigint not null default 0 check (consumed_quantity >= 0),
  rice_generated bigint not null default 0 check (rice_generated >= 0),
  rice_consumed bigint not null default 0 check (rice_consumed >= 0),
  updated_at timestamptz not null default now(),
  primary key (metric_date, item_id)
);

create index if not exists economy_metric_daily_item_idx on economy_metric_daily(item_id, metric_date);
