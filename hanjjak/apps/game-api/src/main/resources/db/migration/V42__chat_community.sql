create table chat_message (
  message_id uuid primary key,
  account_id uuid not null references account(id),
  body varchar(240) not null check (char_length(btrim(body)) between 1 and 240),
  created_at timestamptz not null default now(),
  event_id uuid not null unique,
  deleted boolean not null default false
);
create index chat_message_created_idx on chat_message(created_at desc, event_id desc);

create table chat_board_post (
  post_id uuid primary key,
  account_id uuid not null references account(id),
  intent varchar(8) not null check (intent in ('SELL','BUY')),
  item_id varchar(120) not null,
  quantity bigint not null check (quantity > 0 and quantity <= 999),
  unit_price bigint check (unit_price is null or (unit_price >= 10 and unit_price <= 999990)),
  body varchar(240) not null check (char_length(btrim(body)) between 1 and 240),
  status varchar(16) not null check (status in ('ACTIVE','CLOSED','EXPIRED','DELETED')),
  created_at timestamptz not null default now(),
  expires_at timestamptz not null
);
create index chat_board_post_active_idx on chat_board_post(status, created_at desc, post_id desc);

create table chat_report (
  report_id uuid primary key,
  reporter_account_id uuid not null references account(id),
  target_type varchar(16) not null check (target_type in ('MESSAGE','BOARD_POST')),
  target_id uuid not null,
  reason varchar(64) not null,
  created_at timestamptz not null default now(),
  unique(reporter_account_id, target_type, target_id)
);

create table chat_block (
  account_id uuid not null references account(id),
  blocked_account_id uuid not null references account(id),
  created_at timestamptz not null default now(),
  primary key(account_id, blocked_account_id),
  check(account_id <> blocked_account_id)
);
