-- ACTIVE listings become the sole owner of their remaining stock.
-- Legacy EXPIRED/CANCELLED listings already returned stock under the old policy.
do $$
begin
  if exists (
    select 1 from inventory_stack s full join (
      select seller_account_id account_id,item_id,sum(remaining_quantity) quantity
      from market_listing where status='ACTIVE' group by seller_account_id,item_id
    ) m using(account_id,item_id)
    where coalesce(s.reserved_quantity,0)<>coalesce(m.quantity,0)
       or coalesce(s.quantity,0)<coalesce(m.quantity,0)
  ) then
    raise exception 'V25: market reservation mismatch; manual reconciliation required';
  end if;
end $$;

update account set state_version=state_version+1 where id in (
  select account_id from inventory_stack where reserved_quantity>0
);
update inventory_stack set quantity=quantity-reserved_quantity,reserved_quantity=0
where reserved_quantity>0;
update market_listing set remaining_quantity=0 where status in ('EXPIRED','CANCELLED');
