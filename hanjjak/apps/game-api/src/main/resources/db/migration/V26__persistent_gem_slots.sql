alter table inventory_instance add column inventory_slot_id uuid;
alter table inventory_instance add column locked boolean not null default false;
update inventory_instance i set locked=g.locked from gem_instance g where i.instance_id=g.gem_id;
with ranked as (
  select instance_id,account_id,item_id,stack_key,locked,
    (row_number() over(partition by account_id,item_id,stack_key,locked order by acquired_sequence,instance_id)-1)/99 bucket
  from inventory_instance where stack_key is not null
), slots as (
  select *,first_value(instance_id) over(partition by account_id,item_id,stack_key,locked,bucket order by instance_id) slot_id from ranked
)
update inventory_instance i set inventory_slot_id=s.slot_id from slots s where i.instance_id=s.instance_id;
do $$
begin
  if exists (
    select 1 from account a where
      (select count(distinct coalesce(inventory_slot_id,instance_id)) from inventory_instance where account_id=a.id)
      + (select coalesce(sum(case when item_id ~ '^(POTATO|SWEET_POTATO|CORN)_M[1-4]$' then (quantity-1)/999+1 else 1 end),0) from inventory_stack where account_id=a.id and quantity>0)
      + (select count(distinct r.item_id) from inventory_capacity_reservation r where r.account_id=a.id and r.status='ACTIVE' and not exists(select 1 from inventory_stack s where s.account_id=a.id and s.item_id=r.item_id and s.quantity>0)) > 200
  ) then raise exception 'V26: preserving gem locks would exceed inventory capacity'; end if;
end $$;
create index inventory_instance_slot on inventory_instance(account_id,inventory_slot_id);
