create table if not exists market_anomaly_alert (
  alert_id uuid primary key,
  item_id varchar(120) not null,
  trade_id uuid not null unique,
  alert_type varchar(64) not null,
  unit_price bigint not null,
  reference_price numeric(19,2) not null,
  deviation_ratio numeric(19,6) not null,
  occurred_at timestamptz not null,
  created_at timestamptz not null default now()
);

create index if not exists market_anomaly_alert_item_idx on market_anomaly_alert(item_id, occurred_at desc);
