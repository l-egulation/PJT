/*
 * 차트와 호가창은 읽을 줄 아는 사람에게만 친절하다. 이 화면은 그 반대편이다.
 * 무엇이 얼마에 나와 있는지만 한 줄씩 적어 둔, 옛날 거래소 게시판 같은 목록이다.
 *
 * 한 줄은 품목이 아니라 매물이다. 같은 감자 한 조각이라도 31쌀에 내놓은 것과 38쌀에
 * 내놓은 것은 서로 다른 줄로 선다. 품목마다 가장 싼 값 하나만 적던 예전에는, 뒤에
 * 무엇이 얼마에 줄 서 있는지 보려면 시세 보기로 넘어가야 했다.
 *
 * 분류와 검색은 왼쪽 품목 찾기가 이미 쥐고 있으므로 여기서 두 번 묻지 않는다.
 */
import { useMemo, useState } from "react";
import { useQueries } from "@tanstack/react-query";
import { valueText } from "../gems/GemManagement";
import { filterMarketItems, InstrumentIcon, type FilterState } from "./MarketScreen";
import { focusMarketItems, type MarketFocus } from "./MarketTradeBoard";
import { instrumentShortName, instrumentTagTone, instrumentVariant, riceIcon } from "./marketArt";
import { marketApi, type MarketInstrument, type MarketOrderBook } from "./api";

type Sort = "name" | "price";
type Listing = { item: MarketInstrument; unitPrice: number; quantity: number | null; mine: boolean; cheapest: boolean };

/*
 * 매물을 펼치려면 품목마다 주문장을 하나씩 물어봐야 한다. 분류 전체를 편 채로 마흔
 * 몇 번을 물으면 화면이 아니라 서버가 고생하므로, 좁혀서 보고 있을 때만 펼친다.
 * 값을 견주는 일은 계열이나 등급까지 좁힌 다음에 하는 일이라 이 선에서 충분하다.
 */
const EXPAND_LIMIT = 20;
const BOOK_LEVELS = 20;

/*
 * 파는 물건 목록이니 실제로 나와 있는 것만 싣는다. 값이 비어 있다는 것은 지금 그
 * 물품을 파는 사람이 없다는 뜻인데, 그런 줄까지 보여 주면 살 수 없는 것을 고르게 된다.
 * 팔 때는 왼쪽 품목 찾기에서 고르므로 이 목록이 좁아도 막히지 않는다.
 */
function onSale(item: MarketInstrument): boolean {
  return item.bestAskUnitPrice != null;
}

/** 이름·레벨·수치 순. 같은 품목의 매물끼리는 싼 값이 앞이다. */
function byName(a: Listing, b: Listing): number {
  return instrumentShortName(a.item).localeCompare(instrumentShortName(b.item), "ko")
    || Number(a.item.attributes.level ?? 0) - Number(b.item.attributes.level ?? 0)
    || Number(a.item.attributes.value ?? 0) - Number(b.item.attributes.value ?? 0)
    || a.item.instrumentId.localeCompare(b.item.instrumentId)
    || a.unitPrice - b.unitPrice;
}

export function MarketSimpleList({ catalog, filters, focus, selected, onSelect, toggle, mine }: {
  catalog: MarketInstrument[];
  filters: FilterState;
  focus: MarketFocus;
  /** 내 매물이 걸려 있는 품목. 주문장이 오기 전까지는 이것으로 표시한다. */
  mine: ReadonlySet<string>;
  selected: MarketInstrument;
  onSelect: (instrumentId: string) => void;
  toggle: React.ReactNode;
}) {
  const [sort, setSort] = useState<Sort>("name");
  const visible = useMemo(() => focusMarketItems(filterMarketItems(catalog, filters).filter(onSale), focus), [catalog, filters, focus]);
  const expanding = visible.length <= EXPAND_LIMIT;

  /* 주문장 조회 열쇠는 시세 보기가 쓰는 것과 같다. 고른 품목은 한 번만 물어보게 된다. */
  const books = useQueries({
    queries: (expanding ? visible : []).map(item => ({
      queryKey: ["market-order-book", item.instrumentId],
      queryFn: () => marketApi.orderBook(item.instrumentId, BOOK_LEVELS),
      refetchInterval: 5_000,
    })),
  });

  const listings = useMemo(() => {
    const rows: Listing[] = [];
    for (const [index, item] of visible.entries()) {
      const book = expanding ? (books[index]?.data as MarketOrderBook | undefined) : undefined;
      const asks = book?.asks ?? [];
      if (!asks.length) {
        /* 주문장이 아직 오지 않았으면 품목이 알려 준 가장 싼 값 한 줄로 버틴다. */
        rows.push({ item, unitPrice: item.bestAskUnitPrice!, quantity: null, mine: mine.has(item.instrumentId), cheapest: true });
        continue;
      }
      for (const [level, ask] of asks.entries()) {
        rows.push({ item, unitPrice: ask.unitPrice, quantity: ask.totalQuantity, mine: ask.myQuantity > 0, cheapest: level === 0 });
      }
    }
    return rows.sort((a, b) => (sort === "price" ? a.unitPrice - b.unitPrice : 0) || byName(a, b));
  }, [visible, books, expanding, mine, sort]);

  return <div className="market-simple">
    <header className="market-simple-heading">
      <div className="market-simple-sort" role="group" aria-label="줄 세우는 기준">
        <button type="button" aria-pressed={sort === "name"} onClick={() => setSort("name")}>이름순</button>
        <button type="button" aria-pressed={sort === "price"} onClick={() => setSort("price")}>싼 값순</button>
      </div>
      <h3>판매목록</h3>
      {toggle}
    </header>
    <div className="market-simple-columns" aria-hidden="true"><span>물품</span><span>수량</span><span>파는 값</span></div>
    {listings.length === 0
      ? <p className="market-simple-empty">{filters.query.trim() ? "그런 이름으로 파는 물건이 없어요." : "지금 이걸 파는 사람이 없어요."}</p>
      : <ul className="market-simple-rows">{listings.map(listing => <li key={`${listing.item.instrumentId}:${listing.unitPrice}`}>
        <button
          type="button" aria-pressed={listing.item.instrumentId === selected.instrumentId}
          className={listing.cheapest ? "is-cheapest" : undefined}
          onClick={() => onSelect(listing.item.instrumentId)}
        >
          <span className="market-simple-art"><InstrumentIcon item={listing.item} /></span>
          <span className="market-simple-label">
            <strong className="market-simple-name">{instrumentShortName(listing.item)}</strong>
            <em className={`market-simple-tag ${instrumentTagTone(listing.item)}`}>{instrumentVariant(listing.item, valueText)}</em>
            {listing.mine && <em className="market-simple-mine">내 매물</em>}
          </span>
          <small className="market-simple-stock">{listing.quantity == null ? "" : `${listing.quantity.toLocaleString()}개`}</small>
          <b className="market-simple-ask"><img src={riceIcon} alt="쌀" />{listing.unitPrice.toLocaleString()}</b>
        </button>
      </li>)}</ul>}
  </div>;
}
