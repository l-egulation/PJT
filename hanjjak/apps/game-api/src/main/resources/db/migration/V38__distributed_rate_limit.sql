create table request_rate_limit (
  rate_key varchar(160) primary key,
  reset_at timestamptz not null,
  request_count integer not null check (request_count > 0)
);

create index request_rate_limit_reset_at_idx on request_rate_limit(reset_at);
