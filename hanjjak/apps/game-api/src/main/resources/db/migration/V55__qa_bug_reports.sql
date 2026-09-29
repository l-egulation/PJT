create table qa_bug_report (
  report_id uuid primary key,
  reporter_account_id uuid not null references account(id),
  description varchar(2000) not null check (char_length(btrim(description)) between 1 and 2000),
  page_url varchar(1000) not null,
  user_agent varchar(1000) not null,
  viewport_width integer not null check (viewport_width > 0),
  viewport_height integer not null check (viewport_height > 0),
  image_media_type varchar(32),
  image_data bytea,
  idempotency_key uuid not null,
  created_at timestamptz not null default now(),
  unique (reporter_account_id, idempotency_key),
  check ((image_media_type is null) = (image_data is null))
);

create index qa_bug_report_created_idx on qa_bug_report(created_at desc, report_id desc);
