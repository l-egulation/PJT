alter table economy_metric_daily add column if not exists item_family varchar(80);
alter table economy_metric_daily add column if not exists generation integer;

update economy_metric_daily
set item_family = case
    when item_id = 'RICE' then 'RICE'
    when item_id like '%\_M%' escape '\' then split_part(item_id, '_M', 1)
    when item_id like '%:%' then split_part(item_id, ':', 1)
    else item_id
  end,
  generation = case
    when item_id ~ '_M[0-9]+$' then substring(item_id from '_M([0-9]+)$')::integer
    else null
  end
where item_family is null;

alter table economy_metric_daily alter column item_family set not null;
create index if not exists economy_metric_daily_family_idx on economy_metric_daily(item_family, generation, metric_date);
