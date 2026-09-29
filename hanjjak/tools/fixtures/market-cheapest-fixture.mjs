/*
 * Stacks several sellers on the same item at different prices so you can watch a
 * purchase take the cheapest one first.  The cheap levels hold small amounts, so
 * buying more than one level's worth shows plainly what an immediate purchase
 * does: it fills the cheapest and stops, and the next price becomes the new best.
 *
 *   node tools/fixtures/market-cheapest-fixture.mjs
 *
 * Fixed uuids, so a second run refreshes the same rows.  Local databases only —
 * these orders were never paid for, and cancelling them would mint rice.
 */
import pg from "pg";

const { Client } = pg;

const CANDIDATE_URLS = process.env.DATABASE_URL
  ? [process.env.DATABASE_URL]
  : ["postgres://hanjjak:local-only@localhost:5432/hanjjak", "postgres://hanjjak:local-only@localhost:5432/hanjjak_ui"];

const now = new Date();
const at = minutesAgo => new Date(now.getTime() - minutesAgo * 60_000);
const EIGHT_HOURS = 8 * 60 * 60_000;
const SELLER_RICE = 500_000_000;

/** Three separate shopkeepers, so the cheapest row is plainly somebody else's. */
const SELLERS = [
  { id: "00000000-0000-4000-8000-0000000000b1", characterId: "00000000-0000-4000-8000-0000000000b2", email: "cheap-seller-1@fixture.hanjjak", nickname: "싼값 상인" },
  { id: "00000000-0000-4000-8000-0000000000b3", characterId: "00000000-0000-4000-8000-0000000000b4", email: "cheap-seller-2@fixture.hanjjak", nickname: "이웃 상인" },
  { id: "00000000-0000-4000-8000-0000000000b5", characterId: "00000000-0000-4000-8000-0000000000b6", email: "cheap-seller-3@fixture.hanjjak", nickname: "느긋한 상인" },
];

/*
 * Each row is one order.  Two sellers sit on the same cheapest price on purpose —
 * duplicates at one price are what a busy market actually looks like, and the
 * purchase should sweep both before it ever touches the dearer rows.
 */
const LADDERS = [
  { itemId: "POTATO_M1", orders: [[0, 31, 4], [1, 31, 3], [2, 34, 40], [0, 38, 120], [1, 44, 300]] },
  { itemId: "CORN_M2", orders: [[1, 120, 5], [2, 120, 5], [0, 141, 30], [1, 168, 90], [2, 190, 250]] },
  { itemId: "gem:1:flat_attack", orders: [[2, 250, 1], [0, 250, 2], [1, 305, 6], [2, 360, 15]] },
];

let client;
const run = (text, values = []) => client.query(text, values);

/** Fixed uuids so a second run updates the same rows instead of piling up new ones. */
const uuid = (group, index) => `00000000-0000-4000-8000-${String(group).padStart(4, "0")}${String(index).padStart(8, "0")}`;

async function connect() {
  for (const url of CANDIDATE_URLS) {
    const candidate = new Client({ connectionString: url });
    try {
      await candidate.connect();
      const { rowCount } = await candidate.query("select 1 from information_schema.tables where table_name = 'market_order'");
      if (rowCount) {
        client = candidate;
        console.log(`데이터베이스: ${url.replace(/:[^:@]*@/, ":***@")}`);
        return;
      }
      console.warn(`거래소 표가 없어 건너뜁니다: ${url.replace(/:[^:@]*@/, ":***@")}`);
      await candidate.end();
    } catch (error) {
      console.warn(`연결 실패: ${url.replace(/:[^:@]*@/, ":***@")} (${error.message})`);
    }
  }
  throw new Error("거래소 표가 있는 데이터베이스를 찾지 못했습니다.");
}

async function ensureSellers() {
  for (const seller of SELLERS) {
    await run(
      `insert into account(id, email, password_hash, state_version, created_at)
       values ($1, $2, null, 1, $3) on conflict (id) do nothing`,
      [seller.id, seller.email, now],
    );
    await run(
      `insert into character(id, account_id, level, experience, rice, nickname)
       values ($1, $2, 1, 0, $3, $4) on conflict (account_id) do nothing`,
      [seller.characterId, seller.id, SELLER_RICE, seller.nickname],
    );
    await run(
      `insert into wallet_balance(account_id, balance, updated_at)
       values ($1, $2, $3) on conflict (account_id) do update set balance = excluded.balance, updated_at = excluded.updated_at`,
      [seller.id, SELLER_RICE, now],
    );
  }
}

async function putOrder({ orderId, accountId, instrument, unitPrice, quantity, minutesAgo }) {
  const createdAt = at(minutesAgo);
  await run(
    `insert into market_order(
       order_id, account_id, instrument_id, side, time_in_force, initial_quantity, filled_quantity, remaining_quantity,
       limit_unit_price, reserved_rice, status, priority_at, created_at, updated_at, expires_at, display_name_snapshot
     ) values ($1,$2,$3,'SELL','GTC',$4,0,$4,$5,0,'ACTIVE',$6,$6,$6,$7,$8)
     on conflict (order_id) do update set
       account_id = excluded.account_id, instrument_id = excluded.instrument_id,
       initial_quantity = excluded.initial_quantity, filled_quantity = 0,
       remaining_quantity = excluded.remaining_quantity, limit_unit_price = excluded.limit_unit_price,
       status = 'ACTIVE', priority_at = excluded.priority_at, created_at = excluded.created_at,
       updated_at = excluded.updated_at, expires_at = excluded.expires_at`,
    [orderId, accountId, instrument.instrument_id, quantity, unitPrice, createdAt, new Date(createdAt.getTime() + EIGHT_HOURS), instrument.display_name],
  );
}

async function main() {
  await connect();
  await ensureSellers();

  const itemIds = LADDERS.map(entry => entry.itemId);
  const { rows } = await run(
    `select instrument_id, item_id, display_name from market_instrument
      where item_id = any($1::varchar[]) and status <> 'INACTIVE'`,
    [itemIds],
  );
  const instruments = new Map(rows.map(row => [row.item_id, row]));

  let placed = 0;
  const report = [];
  for (const [ladderIndex, ladder] of LADDERS.entries()) {
    const instrument = instruments.get(ladder.itemId);
    if (!instrument) {
      console.warn(`거래소에 없는 품목은 건너뜁니다: ${ladder.itemId}`);
      continue;
    }
    const sorted = [...ladder.orders].sort((a, b) => a[1] - b[1]);
    for (const [index, [sellerIndex, unitPrice, quantity]] of sorted.entries()) {
      await putOrder({
        orderId: uuid(20 + ladderIndex, index + 1),
        accountId: SELLERS[sellerIndex].id,
        instrument, unitPrice, quantity,
        minutesAgo: 90 - index * 5,
      });
      placed += 1;
    }
    const cheapest = sorted[0];
    const samePrice = sorted.filter(entry => entry[1] === cheapest[1]);
    report.push(`${instrument.display_name}: ${sorted.map(entry => `${entry[1]}쌀×${entry[2]}`).join(", ")}`
      + `  → 가장 싼 ${cheapest[1]}쌀 ${samePrice.reduce((sum, entry) => sum + entry[2], 0)}개부터 나갑니다`);
  }

  console.log(`상인 ${SELLERS.length}명이 매물 ${placed}건을 올렸습니다.`);
  for (const line of report) console.log(`  ${line}`);
  console.log("쌀과 가방은 건드리지 않았습니다. 낸 적 없는 쌀이 도는 자료이니 로컬 확인용으로만 쓰세요.");
}

main()
  .catch(error => { console.error(error.message); process.exitCode = 1; })
  .finally(async () => { if (client) await client.end(); });
