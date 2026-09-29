create table ranking_entry (
  character_id uuid primary key references character(id),
  account_id uuid not null unique references account(id),
  nickname varchar(20) not null,
  material_type varchar(24) not null check (material_type in ('POTATO','SWEET_POTATO','CORN')),
  combat_power bigint not null check (combat_power >= 0),
  formula_version varchar(64) not null,
  source_state_version bigint not null check (source_state_version >= 1),
  updated_at timestamptz not null
);

create index ranking_entry_specialization_order_idx
  on ranking_entry(material_type, combat_power desc, updated_at asc, character_id asc);
