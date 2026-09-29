-- Preserve individual identities while grouping only gems with known attributes.
alter table inventory_instance add column stack_key varchar(160);

do $$
begin
  if exists (
    select 1 from gem_instance g join inventory_instance i on i.instance_id=g.gem_id
    where i.account_id<>g.account_id or i.item_id<>('gem:' || g.level || ':' || lower(g.option))
  ) then
    raise exception 'V21: gem inventory identity mismatch';
  end if;
end $$;

insert into inventory_instance(instance_id,account_id,item_id,reserved_for_sale,acquired_sequence)
select g.gem_id,g.account_id,'gem:' || g.level || ':' || lower(g.option),false,
  coalesce((select max(seq) from (
    select acquired_sequence seq from inventory_stack where account_id=g.account_id
    union all select acquired_sequence from inventory_instance where account_id=g.account_id
  ) owned),0) + row_number() over (partition by g.account_id order by g.created_at,g.gem_id)
from gem_instance g
where not exists (select 1 from inventory_instance i where i.instance_id=g.gem_id);

update inventory_instance i
set stack_key=g.level || ':' || g.option || ':' || g.value
from gem_instance g where i.instance_id=g.gem_id;

do $$
begin
  if exists (
    select 1 from account a
    where (select coalesce(sum(case when s.item_id ~ '^(POTATO|SWEET_POTATO|CORN)_M[1-4]$'
             then (s.quantity-1)/999+1 else 1 end),0)
           from inventory_stack s where s.account_id=a.id and s.quantity>0)
      + (select count(*) from inventory_instance i where i.account_id=a.id and i.stack_key is null)
      + (select coalesce(sum((g.quantity-1)/99+1),0) from (
           select count(*) quantity from inventory_instance i
           where i.account_id=a.id and i.stack_key is not null group by i.item_id,i.stack_key
         ) g)
      + (select count(distinct r.item_id) from inventory_capacity_reservation r
         where r.account_id=a.id and r.status='ACTIVE' and not exists (
           select 1 from inventory_stack s where s.account_id=a.id and s.item_id=r.item_id and s.quantity>0)) > 200
  ) then
    raise exception 'V21: gem backfill would exceed the 200 inventory slots';
  end if;
end $$;

create index inventory_instance_stack_group on inventory_instance(account_id,item_id,stack_key);
