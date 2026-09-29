alter table market_listing
  drop constraint if exists market_listing_unit_price_check;
alter table market_listing
  add constraint market_listing_unit_price_check
  check (unit_price between 1 and 999990);

alter table market_buy_order
  drop constraint if exists market_buy_order_max_unit_price_check;
alter table market_buy_order
  add constraint market_buy_order_max_unit_price_check
  check (max_unit_price between 10 and 999990);
