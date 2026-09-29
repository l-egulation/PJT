-- 치장 선택 상자를 공용 200슬롯 인벤토리로 이관한다.
-- 근거: docs/30-domain/items/ssot.md, 결정 로그 #69, 요구사항 COS-BOX-001/002/003.
-- 상자는 boxItemId별로 중첩되는 200슬롯 아이템이므로 별도 저장소를 두지 않는다.

-- 이관으로 200슬롯을 넘기는 계정이 있으면 조용히 넘어가지 않고 즉시 실패한다.
-- InventoryStatus가 usedSlots > maxSlots를 거부하므로, 넘긴 계정은 인벤토리 조회 자체가 실패한다.
do $$
declare offending bigint;
begin
  with box_new_slots as (
    -- 기존 스택이 없어 새 슬롯을 새로 써야 하는 상자만 계정별로 센다.
    select b.account_id, count(*) as new_slots
    from cosmetic_selector_box b
    where b.quantity > 0
      and not exists (
        select 1
        from inventory_stack s
        where s.account_id = b.account_id
          and s.item_id = b.box_item_id
          and s.quantity > 0
      )
    group by b.account_id
  ),
  current_usage as (
    select account_id, count(*) as used_slots
    from (
      select account_id from inventory_stack where quantity > 0
      union all
      select account_id from inventory_instance
      union all
      select account_id from inventory_capacity_reservation
      where status = 'ACTIVE' and reservation_kind = 'EMPTY_SLOT'
    ) owned
    group by account_id
  )
  select count(*)
  into offending
  from box_new_slots n
  left join current_usage c on c.account_id = n.account_id
  where coalesce(c.used_slots, 0) + n.new_slots > 200;

  if offending > 0 then
    raise exception
      'V20: % account(s) would exceed the 200 inventory slots after moving selector boxes. Resolve the affected accounts before migrating.',
      offending;
  end if;
end $$;

-- 기존 스택이 있으면 수량을 합치고, 없으면 새 슬롯 하나를 사용한다.
insert into inventory_stack(account_id, item_id, quantity, reserved_quantity, acquired_sequence)
select b.account_id,
       b.box_item_id,
       b.quantity,
       0,
       coalesce(s.max_seq, 0) + row_number() over (partition by b.account_id order by b.box_item_id)
from cosmetic_selector_box b
left join (
  select account_id, max(seq) as max_seq
  from (
    select account_id, acquired_sequence as seq from inventory_stack
    union all
    select account_id, acquired_sequence from inventory_instance
  ) owned
  group by account_id
) s on s.account_id = b.account_id
where b.quantity > 0
on conflict (account_id, item_id) do update
  set quantity = inventory_stack.quantity + excluded.quantity;

drop table cosmetic_selector_box;
