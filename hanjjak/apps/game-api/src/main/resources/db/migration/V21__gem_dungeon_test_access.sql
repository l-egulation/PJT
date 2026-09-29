create table gem_dungeon_test_access (
    account_id uuid primary key references account(id) on delete cascade,
    granted_at timestamptz not null default now()
);
