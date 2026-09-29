alter table character add column cosmetics_unlocked boolean not null default false;
alter table character add column cosmetic_ticket_balance bigint not null default 0 check (cosmetic_ticket_balance >= 0);

create table cosmetic_collection_state (
  account_id uuid not null references account(id),
  cosmetic_id varchar(80) not null,
  registered_quantity integer not null check (registered_quantity >= 0),
  unregistered_quantity integer not null check (unregistered_quantity >= 0),
  reserved_quantity integer not null check (reserved_quantity >= 0 and reserved_quantity <= unregistered_quantity),
  primary key(account_id, cosmetic_id)
);

create table cosmetic_equipment (
  account_id uuid not null references account(id),
  slot varchar(16) not null check (slot in ('HEAD','TOP','BOTTOM','GLOVES','SHOES','CAPE')),
  cosmetic_id varchar(80),
  primary key(account_id, slot)
);
create table cosmetic_banner_progress (
  account_id uuid not null references account(id),
  banner_id varchar(80) not null,
  total_successful_draws bigint not null default 0 check (total_successful_draws >= 0),
  claimed_box_count bigint not null default 0 check (claimed_box_count >= 0),
  primary key(account_id, banner_id)
);

create table cosmetic_selector_box (
  account_id uuid not null references account(id),
  box_item_id varchar(80) not null,
  quantity bigint not null check (quantity >= 0),
  primary key(account_id, box_item_id)
);
