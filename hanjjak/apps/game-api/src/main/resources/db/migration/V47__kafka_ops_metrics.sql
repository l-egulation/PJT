create table if not exists kafka_ops_metric (
  metric_name varchar(120) primary key,
  value bigint not null default 0,
  updated_at timestamptz not null default now()
);

create table if not exists kafka_ops_consumer_metric (
  consumer_name varchar(120) primary key,
  events bigint not null default 0,
  duplicates bigint not null default 0,
  failures bigint not null default 0,
  batches bigint not null default 0,
  batch_size bigint not null default 0,
  processing_millis bigint not null default 0,
  updated_at timestamptz not null default now()
);
