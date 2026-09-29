/*
 * Fills a local database with enough market life to exercise the 거래소 screen:
 * two-sided depth and trade history to look at, my own orders to edit and
 * cancel, finished orders whose fills can be unfolded, and things waiting in
 * 받기.  Everything it writes is keyed off fixed uuids, so running it twice
 * refreshes the same rows instead of piling up new ones.
 *
 *   node tools/fixtures/market-plaza-fixture.mjs [--email=me@example.com]
 *
 * Without --email it picks the account that currently has a session, falling
 * back to the one holding the most items.  DATABASE_URL overrides the
 * connection; otherwise it probes the usual local databases for the one that
 * actually has the market tables.
 */
import pg from "pg";

const { Client } = pg;

const CANDIDATE_URLS = process.env.DATABASE_URL
  ? [process.env.DATABASE_URL]
  : ["postgres://hanjjak:local-only@localhost:5432/hanjjak_ui", "postgres://hanjjak:local-only@localhost:5432/hanjjak"];
const requestedEmail = process.argv.find(argument => argument.startsWith("--email="))?.slice("--email=".length) ?? null;

const MAKER_ACCOUNT_ID = "00000000-0000-4000-8000-0000000000a1";
const MAKER_CHARACTER_ID = "00000000-0000-4000-8000-0000000000a2";
const MAKER_EMAIL = "market-maker@fixture.hanjjak";
const MAKER_RICE = 500_000_000;
const EIGHT_HOURS = 8 * 60 * 60_000;

const now = new Date();
const at = minutesAgo => new Date(now.getTime() - minutesAgo * 60_000);

/** Fixed uuids so a second run updates the same rows. */
function uuid(group, index) {
  return `00000000-0000-4000-8000-${String(group).padStart(4, "0")}${String(index).padStart(8, "0")}`;
}

/*
 * Every tradeable item gets a market, not a hand-picked dozen. A list that carries
 * three of a family reads as broken — you open 스킬북 and find nothing at all.
 * The mid price is read off the item id so grades climb the way the game does.
 */
const MATERIAL_BASE = { POTATO: 52, SWEET_POTATO: 47, CORN: 58 };
const BOOK_GRADE_MULTIPLIER = { normal: 1, rare: 7, epic: 28, legendary: 110 };

function midPriceFor(itemId) {
  const material = /^([A-Z_]+)_M([1-5])$/.exec(itemId);
  if (material && MATERIAL_BASE[material[1]]) return MATERIAL_BASE[material[1]] * 4.6 ** (Number(material[2]) - 1);
  const book = /^skillbook:[a-z_]+:([a-z]+)$/.exec(itemId);
  if (book && BOOK_GRADE_MULTIPLIER[book[1]]) return 120 * BOOK_GRADE_MULTIPLIER[book[1]];
  const gem = /^gem:(\d+):[a-z_]+$/.exec(itemId);
  if (gem) return 90 * 2.4 ** (Number(gem[1]) - 1);
  return null;
}

/** Filled from the database once the instruments are known. */
const DEPTH_ITEMS = [];
/* Ticks away from the mid, so cheap items still get a readable spread. */
const ASK_STEPS = [[1, 210], [2, 120], [3, 64], [5, 35]];
const BID_STEPS = [[1, 200], [2, 180], [3, 96], [5, 48]];
/** A gentle wander so the 12h candles have a shape instead of a flat line. */
const TREND = [-.14, -.2, -.08, .04, -.02, .09, .16, .07, .12, .02, .06, .1];

const instruments = new Map();
let client;

async function run(text, values = []) {
  return client.query(text, values);
}

function clampPrice(value) {
  return Math.max(10, Math.min(999_999, Math.round(value)));
}
function tick(mid) {
  return Math.max(1, Math.round(mid * .02));
}
function price(mid, ratio) {
  return clampPrice(mid * (1 + ratio));
}

async function connect() {
  for (const connectionString of CANDIDATE_URLS) {
    const candidate = new Client({ connectionString });
    try {
      await candidate.connect();
      const { rows } = await candidate.query("select to_regclass('public.market_instrument') as table_name");
      if (rows[0].table_name) {
        client = candidate;
        console.log(`데이터베이스: ${connectionString.replace(/:[^:@/]*@/, ":***@")}`);
        return;
      }
      console.warn(`거래소 표가 없어 건너뜀: ${connectionString.replace(/:[^:@/]*@/, ":***@")}`);
      await candidate.end();
    } catch (error) {
      console.warn(`연결 실패: ${connectionString.replace(/:[^:@/]*@/, ":***@")} (${error.message})`);
      await candidate.end().catch(() => undefined);
    }
  }
  throw new Error("거래소 표가 있는 데이터베이스를 찾지 못했습니다. DATABASE_URL을 지정해 주세요.");
}

async function targetAccount() {
  if (requestedEmail) {
    const { rows } = await run("select a.id, a.email from account a where a.email = $1", [requestedEmail]);
    if (!rows.length) throw new Error(`계정을 찾지 못했습니다: ${requestedEmail}`);
    return rows[0];
  }
  const { rows } = await run(
    `select a.id, a.email,
            (select count(*) from account_active_session s where s.account_id = a.id) as signed_in,
            coalesce((select sum(quantity) from inventory_stack i where i.account_id = a.id), 0) as items
       from account a
      where a.id <> $1
      order by signed_in desc, items desc, a.created_at desc
      limit 1`,
    [MAKER_ACCOUNT_ID],
  );
  if (!rows.length) throw new Error("씨앗을 심을 계정이 없습니다. 먼저 회원가입을 해 주세요.");
  return rows[0];
}

async function ensureMaker() {
  await run(
    `insert into account(id, email, password_hash, state_version, created_at)
     values ($1, $2, null, 1, $3) on conflict (id) do nothing`,
    [MAKER_ACCOUNT_ID, MAKER_EMAIL, now],
  );
  await run(
    `insert into character(id, account_id, level, experience, rice, nickname)
     values ($1, $2, 1, 0, $3, '거래소 상인') on conflict (account_id) do nothing`,
    [MAKER_CHARACTER_ID, MAKER_ACCOUNT_ID, MAKER_RICE],
  );
  await run(
    `insert into wallet_balance(account_id, balance, updated_at)
     values ($1, $2, $3) on conflict (account_id) do update set balance = excluded.balance, updated_at = excluded.updated_at`,
    [MAKER_ACCOUNT_ID, MAKER_RICE, now],
  );
}

async function loadInstruments() {
  const { rows } = await run(
    `select instrument_id, item_id, display_name from market_instrument
      where status <> 'INACTIVE' order by category, display_name, instrument_id`,
  );
  const unpriced = [];
  for (const row of rows) {
    const mid = midPriceFor(row.item_id);
    if (mid === null) { unpriced.push(row.item_id); continue; }
    instruments.set(row.item_id, row);
    DEPTH_ITEMS.push({ itemId: row.item_id, mid: clampPrice(mid) });
  }
  if (unpriced.length) console.warn(`값을 매길 규칙이 없어 건너뜁니다: ${unpriced.join(", ")}`);
}

/** Every order the fixture owns is rewritten in place, so reruns stay clean. */
async function putOrder(order) {
  await run(
    `insert into market_order(
       order_id, account_id, instrument_id, side, time_in_force, initial_quantity, filled_quantity, remaining_quantity,
       limit_unit_price, reserved_rice, status, priority_at, created_at, updated_at, expires_at, display_name_snapshot
     ) values ($1,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$12,$13,$14,$15)
     on conflict (order_id) do update set
       account_id = excluded.account_id, instrument_id = excluded.instrument_id, side = excluded.side,
       time_in_force = excluded.time_in_force, initial_quantity = excluded.initial_quantity,
       filled_quantity = excluded.filled_quantity, remaining_quantity = excluded.remaining_quantity,
       limit_unit_price = excluded.limit_unit_price, reserved_rice = excluded.reserved_rice, status = excluded.status,
       priority_at = excluded.priority_at, created_at = excluded.created_at, updated_at = excluded.updated_at,
       expires_at = excluded.expires_at, display_name_snapshot = excluded.display_name_snapshot`,
    [
      order.orderId, order.accountId, order.instrument.instrument_id, order.side, order.timeInForce,
      order.initialQuantity, order.filledQuantity, order.remainingQuantity, order.unitPrice, order.reservedRice,
      order.status, order.createdAt, order.updatedAt, order.expiresAt, order.instrument.display_name,
    ],
  );
}

function restingOrder({ orderId, accountId, instrument, side, quantity, filled = 0, unitPrice, createdAt, status = "ACTIVE" }) {
  const remaining = quantity - filled;
  return {
    orderId, accountId, instrument, side, timeInForce: "GTC",
    initialQuantity: quantity, filledQuantity: filled, remainingQuantity: remaining, unitPrice,
    reservedRice: side === "BUY" ? remaining * unitPrice : 0,
    status, createdAt, updatedAt: createdAt, expiresAt: new Date(createdAt.getTime() + EIGHT_HOURS),
  };
}

/*
 * 주문 시각은 항목 수에 따라 끝없이 과거로 밀리면 안 된다. 예전에는 순번을 그대로 분으로
 * 썼는데, 품목이 열두 개에서 여든 개로 늘자 뒤쪽 주문이 8시간 만료보다 먼저 태어나
 * 넣자마자 서버가 만료로 걷어 갔다. 최근 한 시간 안에서만 앞뒤를 둔다.
 */
async function seedDepth() {
  let index = 0;
  for (const { itemId, mid } of DEPTH_ITEMS) {
    const instrument = instruments.get(itemId);
    if (!instrument) continue;
    const step = tick(mid);
    for (const [ticks, quantity] of ASK_STEPS) {
      await putOrder(restingOrder({
        orderId: uuid(1, index++), accountId: MAKER_ACCOUNT_ID, instrument, side: "SELL",
        quantity, unitPrice: clampPrice(mid + ticks * step), createdAt: at(30 + (index % 60)),
      }));
    }
    for (const [ticks, quantity] of BID_STEPS) {
      await putOrder(restingOrder({
        orderId: uuid(2, index++), accountId: MAKER_ACCOUNT_ID, instrument, side: "BUY",
        quantity, unitPrice: clampPrice(mid - ticks * step), createdAt: at(30 + (index % 60)),
      }));
    }
  }
}

/** Maker-to-maker trades: history for the chart, with nobody's ledger touched. */
async function seedTradeHistory() {
  let index = 0;
  for (const { itemId, mid } of DEPTH_ITEMS) {
    const instrument = instruments.get(itemId);
    if (!instrument) continue;
    for (const [step, ratio] of TREND.entries()) {
      const unitPrice = price(mid, ratio / 10);
      const quantity = 18 + ((step * 7 + index) % 40);
      const filledAt = at((TREND.length - step) * 55 + (index % 7));
      await counterpartOrder({ orderId: uuid(4, index), accountId: MAKER_ACCOUNT_ID, instrument, side: "SELL", quantity, unitPrice, filledAt });
      await counterpartOrder({ orderId: uuid(5, index), accountId: MAKER_ACCOUNT_ID, instrument, side: "BUY", quantity, unitPrice, filledAt });
      await seedTrade({
        tradeId: uuid(3, index), makerOrderId: uuid(4, index), takerOrderId: uuid(5, index),
        instrument, itemId, unitPrice, quantity, filledAt,
        sellerAccountId: MAKER_ACCOUNT_ID, buyerAccountId: MAKER_ACCOUNT_ID,
      });
      index += 1;
    }
  }
}

/** A finished order standing in for whoever was on the other side of a trade. */
async function counterpartOrder({ orderId, accountId, instrument, side, quantity, unitPrice, filledAt }) {
  await putOrder({
    orderId, accountId, instrument, side, timeInForce: "GTC", initialQuantity: quantity,
    filledQuantity: quantity, remainingQuantity: 0, unitPrice, reservedRice: 0, status: "FILLED",
    createdAt: filledAt, updatedAt: filledAt, expiresAt: new Date(filledAt.getTime() + EIGHT_HOURS),
  });
  return orderId;
}

async function seedTrade({ tradeId, makerOrderId, takerOrderId, instrument, itemId, unitPrice, quantity, filledAt, sellerAccountId, buyerAccountId }) {
  const total = unitPrice * quantity;
  const fee = Math.floor(total / 10);
  await run(
    `insert into market_trade(
       trade_id, listing_id, buyer_account_id, seller_account_id, item_id, quantity, unit_price, total_price, fee,
       settlement_amount, filled_at, maker_order_id, taker_order_id, instrument_id, display_name_snapshot
     ) values ($1,null,$2,$3,$4,$5,$6,$7,$8,$9,$10,$11,$12,$13,$14)
     on conflict (trade_id) do update set
       buyer_account_id = excluded.buyer_account_id, seller_account_id = excluded.seller_account_id,
       quantity = excluded.quantity, unit_price = excluded.unit_price, total_price = excluded.total_price,
       fee = excluded.fee, settlement_amount = excluded.settlement_amount, filled_at = excluded.filled_at`,
    [tradeId, buyerAccountId, sellerAccountId, itemId, quantity, unitPrice, total, fee, total - fee, filledAt, makerOrderId, takerOrderId, instrument.instrument_id, instrument.display_name],
  );
  return { total, fee, settlement: total - fee };
}

/*
 * The player's own ledger.  `내 거래` needs orders it can edit and cancel, and
 * finished orders whose fills add up exactly — the screen only unfolds
 * `실제로 거래된 내용` when the trades it finds match the filled quantity.
 */
async function seedMyOrders(accountId) {
  const potato = instruments.get("POTATO_M1");
  const corn = instruments.get("CORN_M2");
  const sweet = instruments.get("SWEET_POTATO_M1");
  const gem = instruments.get("gem:2:flat_attack");
  const live = [
    potato && restingOrder({ orderId: uuid(6, 1), accountId, instrument: potato, side: "SELL", quantity: 40, unitPrice: 58, createdAt: at(70) }),
    corn && restingOrder({ orderId: uuid(6, 2), accountId, instrument: corn, side: "BUY", quantity: 20, filled: 12, unitPrice: 190, createdAt: at(55), status: "PARTIALLY_FILLED" }),
    sweet && restingOrder({ orderId: uuid(6, 3), accountId, instrument: sweet, side: "BUY", quantity: 50, unitPrice: 44, createdAt: at(40) }),
    gem && restingOrder({ orderId: uuid(6, 4), accountId, instrument: gem, side: "SELL", quantity: 15, filled: 10, unitPrice: 1_180, createdAt: at(25), status: "PARTIALLY_FILLED" }),
  ].filter(Boolean);
  for (const order of live) await putOrder(order);

  let closed = 0;
  if (gem) {
    /*
     * Sold in two goes — this is the row that unfolds into two fills.  Both
     * fills sit inside [createdAt, updatedAt] so the screen can match them to
     * this order and show the breakdown as complete.
     */
    const order = {
      orderId: uuid(7, 1), accountId, instrument: gem, side: "SELL", timeInForce: "GTC",
      initialQuantity: 5, filledQuantity: 5, remainingQuantity: 0, unitPrice: 1_200, reservedRice: 0,
      status: "FILLED", createdAt: at(330), updatedAt: at(322), expiresAt: new Date(at(330).getTime() + EIGHT_HOURS),
    };
    await putOrder(order);
    for (const [index, [quantity, minutesAgo]] of [[2, 326], [3, 323]].entries()) {
      const filledAt = at(minutesAgo);
      await counterpartOrder({ orderId: uuid(9, index + 1), accountId: MAKER_ACCOUNT_ID, instrument: gem, side: "BUY", quantity, unitPrice: 1_200, filledAt });
      await seedTrade({
        tradeId: uuid(8, index + 1), makerOrderId: uuid(9, index + 1), takerOrderId: order.orderId,
        instrument: gem, itemId: gem.item_id, unitPrice: 1_200, quantity, filledAt,
        sellerAccountId: accountId, buyerAccountId: MAKER_ACCOUNT_ID,
      });
    }
    closed += 1;
  }
  if (potato) {
    const filledAt = at(1_500);
    const order = {
      orderId: uuid(7, 2), accountId, instrument: potato, side: "BUY", timeInForce: "GTC",
      initialQuantity: 10, filledQuantity: 10, remainingQuantity: 0, unitPrice: 55, reservedRice: 0,
      status: "FILLED", createdAt: at(1_510), updatedAt: at(1_498), expiresAt: new Date(at(1_510).getTime() + EIGHT_HOURS),
    };
    await putOrder(order);
    await counterpartOrder({ orderId: uuid(9, 3), accountId: MAKER_ACCOUNT_ID, instrument: potato, side: "SELL", quantity: 10, unitPrice: 55, filledAt });
    await seedTrade({
      tradeId: uuid(8, 3), makerOrderId: uuid(9, 3), takerOrderId: order.orderId,
      instrument: potato, itemId: potato.item_id, unitPrice: 55, quantity: 10, filledAt,
      sellerAccountId: MAKER_ACCOUNT_ID, buyerAccountId: accountId,
    });
    closed += 1;
  }
  if (corn) {
    await putOrder({
      orderId: uuid(7, 3), accountId, instrument: corn, side: "BUY", timeInForce: "GTC",
      initialQuantity: 20, filledQuantity: 0, remainingQuantity: 0, unitPrice: 180, reservedRice: 0,
      status: "CANCELLED", createdAt: at(2_900), updatedAt: at(2_880), expiresAt: new Date(at(2_900).getTime() + EIGHT_HOURS),
    });
  }
  if (sweet) {
    await putOrder({
      orderId: uuid(7, 4), accountId, instrument: sweet, side: "BUY", timeInForce: "GTC",
      initialQuantity: 12, filledQuantity: 0, remainingQuantity: 0, unitPrice: 40, reservedRice: 0,
      status: "EXPIRED", createdAt: at(4_400), updatedAt: at(4_320), expiresAt: at(4_320),
    });
    closed += 1;
  }
  return { live: live.length, closed: corn ? closed + 1 : closed };
}

async function seedInbox(accountId) {
  const rows = [
    { itemId: "POTATO_M1", orderId: uuid(7, 2), source: "BUY_FILL", quantity: 20, minutesAgo: 120 },
    { itemId: "SWEET_POTATO_M1", orderId: uuid(6, 3), source: "BUY_FILL", quantity: 15, minutesAgo: 200 },
    { itemId: "CORN_M2", orderId: uuid(7, 3), source: "SELL_CANCEL_RETURN", quantity: 6, minutesAgo: 260 },
  ];
  let claimable = 0;
  for (const [index, row] of rows.entries()) {
    const instrument = instruments.get(row.itemId);
    if (!instrument) continue;
    const { rowCount } = await run("select 1 from market_order where order_id = $1", [row.orderId]);
    if (!rowCount) continue;
    await run(
      `insert into market_delivery(
         delivery_id, account_id, instrument_id, order_id, trade_id, source, item_id, instance_ids, quantity,
         display_name_snapshot, created_at, claimed_at, source_account_id
       ) values ($1,$2,$3,$4,null,$5,$6,'{}',$7,$8,$9,null,$10)
       on conflict (delivery_id) do update set
         quantity = excluded.quantity, source = excluded.source, created_at = excluded.created_at, claimed_at = null`,
      [uuid(11, index + 1), accountId, instrument.instrument_id, row.orderId, row.source, row.itemId, row.quantity, instrument.display_name, at(row.minutesAgo), MAKER_ACCOUNT_ID],
    );
    claimable += 1;
  }
  const mails = [[3_395, 90], [1_425, 150]];
  for (const [index, [riceAmount, minutesAgo]] of mails.entries()) {
    await run(
      `insert into mail_message(mail_id, account_id, type, rice_amount, source_trade_id, claimed, created_at, claimed_at)
       values ($1,$2,'MARKET_SETTLEMENT',$3,null,false,$4,null)
       on conflict (mail_id) do update set rice_amount = excluded.rice_amount, claimed = false, created_at = excluded.created_at, claimed_at = null`,
      [uuid(12, index + 1), accountId, riceAmount, at(minutesAgo)],
    );
  }
  return { deliveries: claimable, mails: mails.length };
}

async function bumpRevisions() {
  await run(
    `update market_instrument i
        set revision = revision + 1, updated_at = now()
      where item_id = any($1::varchar[])`,
    [[...instruments.keys()]],
  );
}

await connect();
try {
  await run("begin");
  await ensureMaker();
  await loadInstruments();
  const account = await targetAccount();
  await seedDepth();
  await seedTradeHistory();
  const mine = await seedMyOrders(account.id);
  const inbox = await seedInbox(account.id);
  await bumpRevisions();
  await run("commit");
  console.log(`대상 계정: ${account.email}`);
  console.log(`품목 ${instruments.size}종에 사고파는 호가와 12시간치 시세를 넣었습니다.`);
  console.log(`진행 중 거래 ${mine.live}건, 지난 거래 ${mine.closed}건, 받을 물품 ${inbox.deliveries}건, 받을 쌀 ${inbox.mails}건.`);
  console.log("쌀과 가방은 건드리지 않았습니다. 이 자료로 넣은 거래를 취소하면 실제로 내지 않은 쌀이 돌아옵니다 — 로컬 확인용으로만 쓰세요.");
} catch (error) {
  await run("rollback");
  console.error(error);
  process.exitCode = 1;
} finally {
  await client.end();
}
