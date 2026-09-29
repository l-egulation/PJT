import pg from "pg";

const { Client } = pg;
const client = new Client({
  connectionString: process.env.DATABASE_URL ?? "postgres://hanjjak:local-only@localhost:5432/hanjjak",
});

const SYSTEM_ACCOUNT_ID = "00000000-0000-4000-8000-000000000001";
const SYSTEM_CHARACTER_ID = "00000000-0000-4000-8000-000000000002";
const now = new Date();
const instrumentIds = new Map();
// The same timestamped trade history is intentionally reusable for 5m, 15m, and 30m server-side aggregation.

function id(suffix) {
  return `00000000-0000-4000-8000-${String(Number(suffix)).padStart(12, "0")}`;
}

function timestamp(minutesAgo) {
  return new Date(now.getTime() - minutesAgo * 60_000);
}

async function query(text, values = []) {
  return client.query(text, values);
}

async function ensureSystemAccount() {
  await query(
    `insert into account(id,email,password_hash,state_version,created_at)
     values ($1,$2,null,1,$3) on conflict (id) do nothing`,
    [SYSTEM_ACCOUNT_ID, "system-market@internal.hanjjak", now],
  );
  await query(
    `insert into character(id,account_id,level,experience,rice,nickname)
     values ($1,$2,1,0,0,'거래소 시스템') on conflict (account_id) do nothing`,
    [SYSTEM_CHARACTER_ID, SYSTEM_ACCOUNT_ID],
  );
  await query(
    `insert into wallet_balance(account_id,balance,updated_at)
     values ($1,0,$2) on conflict (account_id) do nothing`,
    [SYSTEM_ACCOUNT_ID, now],
  );
}

async function instruments(itemIds) {
  const rows = await query(
    `select instrument_id,item_id,display_name
       from market_instrument
      where item_id = any($1::varchar[]) and status = 'ACTIVE'`,
    [itemIds],
  );
  for (const row of rows.rows) instrumentIds.set(row.item_id, row);
  if (instrumentIds.size !== itemIds.length) {
    throw new Error(`Missing active instruments: ${itemIds.filter((itemId) => !instrumentIds.has(itemId)).join(", ")}`);
  }
}

async function seedSellOrders() {
  const levels = [
    ["POTATO_M1", 95, 180], ["POTATO_M1", 110, 260], ["POTATO_M1", 125, 420], ["POTATO_M1", 140, 310],
    ["POTATO_M2", 180, 90], ["POTATO_M2", 205, 140], ["POTATO_M2", 230, 210],
    ["CORN_M1", 70, 240], ["CORN_M1", 85, 330], ["CORN_M1", 100, 500],
    ["SWEET_POTATO_M1", 80, 200], ["SWEET_POTATO_M1", 105, 280], ["SWEET_POTATO_M1", 130, 360],
  ];
  for (const [index, [itemId, price, quantity]] of levels.entries()) {
    const orderId = id(`1${index}`);
    const instrument = instrumentIds.get(itemId);
    const createdAt = timestamp(index + 1);
    await query(
      `insert into market_order(
         order_id,account_id,instrument_id,side,time_in_force,initial_quantity,filled_quantity,remaining_quantity,
         limit_unit_price,reserved_rice,status,priority_at,created_at,updated_at,expires_at,display_name_snapshot
       ) values ($1,$2,$3,'SELL','GTC',$4,0,$4,$5,0,'ACTIVE',$6,$6,$6,$7,$8)
       on conflict (order_id) do update set account_id=excluded.account_id,instrument_id=excluded.instrument_id,initial_quantity=excluded.initial_quantity,filled_quantity=0,remaining_quantity=excluded.remaining_quantity,limit_unit_price=excluded.limit_unit_price,reserved_rice=0,status='ACTIVE',priority_at=excluded.priority_at,created_at=excluded.created_at,updated_at=excluded.updated_at,expires_at=excluded.expires_at,display_name_snapshot=excluded.display_name_snapshot`,
      [orderId, SYSTEM_ACCOUNT_ID, instrument.instrument_id, quantity, price, createdAt, new Date(now.getTime() + 8 * 60 * 60_000), instrument.display_name],
    );
  }
}

async function seedTrades() {
  const trades = [
    ["POTATO_M1", 90, 38, 11], ["POTATO_M1", 100, 52, 27], ["POTATO_M1", 115, 76, 44], ["POTATO_M1", 130, 61, 68],
    ["POTATO_M1", 105, 48, 93], ["POTATO_M1", 122, 85, 121], ["POTATO_M1", 98, 32, 149], ["POTATO_M1", 145, 64, 181],
    ["POTATO_M2", 175, 30, 16], ["POTATO_M2", 205, 46, 51], ["POTATO_M2", 220, 58, 112],
    ["CORN_M1", 68, 80, 24], ["CORN_M1", 84, 120, 76], ["CORN_M1", 99, 95, 145],
    ["SWEET_POTATO_M1", 78, 55, 31], ["SWEET_POTATO_M1", 104, 71, 89], ["SWEET_POTATO_M1", 128, 92, 173],
  ];
  for (const [index, [itemId, price, quantity, minutesAgo]] of trades.entries()) {
    const tradeId = id(`2${index}`);
    const makerOrderId = id(`3${index}`);
    const takerOrderId = id(`4${index}`);
    const instrument = instrumentIds.get(itemId);
    const filledAt = timestamp(minutesAgo);
    const total = price * quantity;
    const fee = Math.floor(total / 10);
    await query(
      `insert into market_order(
         order_id,account_id,instrument_id,side,time_in_force,initial_quantity,filled_quantity,remaining_quantity,
         limit_unit_price,reserved_rice,status,priority_at,created_at,updated_at,expires_at,display_name_snapshot
       ) values ($1,$2,$3,'SELL','GTC',$4,$4,0,$5,0,'FILLED',$6,$6,$6,$7,$8)
       on conflict (order_id) do nothing`,
      [makerOrderId, SYSTEM_ACCOUNT_ID, instrument.instrument_id, quantity, price, filledAt, new Date(filledAt.getTime() + 8 * 60 * 60_000), instrument.display_name],
    );
    await query(
      `insert into market_order(
         order_id,account_id,instrument_id,side,time_in_force,initial_quantity,filled_quantity,remaining_quantity,
         limit_unit_price,reserved_rice,status,priority_at,created_at,updated_at,expires_at,display_name_snapshot
       ) values ($1,$2,$3,'BUY','IOC',$4,$4,0,$5,0,'FILLED',$6,$6,$6,null,$7)
       on conflict (order_id) do nothing`,
      [takerOrderId, SYSTEM_ACCOUNT_ID, instrument.instrument_id, quantity, price, filledAt, instrument.display_name],
    );
    await query(
      `insert into market_trade(
         trade_id,listing_id,buyer_account_id,seller_account_id,item_id,quantity,unit_price,total_price,fee,settlement_amount,
         filled_at,maker_order_id,taker_order_id,instrument_id,display_name_snapshot
       ) values ($1,null,$2,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13)
       on conflict (trade_id) do nothing`,
      [tradeId, SYSTEM_ACCOUNT_ID, itemId, quantity, price, total, fee, total - fee, filledAt, makerOrderId, takerOrderId, instrument.instrument_id, instrument.display_name],
    );
  }
}

async function updateRevisions() {
  await query(
    `update market_instrument i
        set revision = greatest(revision, coalesce((select count(*) from market_order o where o.instrument_id=i.instrument_id and o.status in ('ACTIVE','PARTIALLY_FILLED')), 0) + 1),
            updated_at = now()
      where item_id = any($1::varchar[])`,
    [[...instrumentIds.keys()]],
  );
}

await client.connect();
try {
  await client.query("begin");
  await ensureSystemAccount();
  const itemIds = ["POTATO_M1", "POTATO_M2", "CORN_M1", "SWEET_POTATO_M1"];
  await instruments(itemIds);
  await seedSellOrders();
  await seedTrades();
  await updateRevisions();
  await client.query("commit");
  console.log(`Seeded ${itemIds.length} instruments with representative sell depth and trade history.`);
} catch (error) {
  await client.query("rollback");
  console.error(error);
  process.exitCode = 1;
} finally {
  await client.end();
}
