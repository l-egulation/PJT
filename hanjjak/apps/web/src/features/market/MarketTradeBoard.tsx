import { useState, type Dispatch, type SetStateAction } from "react";
import { valueText } from "../gems/GemManagement";
import { InstrumentIcon, InstrumentPicker, marketGroupKey, maximumAffordableQuantity, PriceTrendChart, type FilterState } from "./MarketScreen";
import { PlazaIcon } from "./PlazaIcon";
import { MarketSimpleList } from "./MarketSimpleList";
import type { MarketBoardView } from "./marketUiState";
import { MAX_MARKET_UNIT_PRICE, MIN_MARKET_UNIT_PRICE, marketUnitPrice, numericInputValue, positiveInteger, type NumericFieldValue } from "./marketForm";
import { instrumentArt, instrumentShortName, instrumentVariant, riceIcon } from "./marketArt";
import type { MarketDepthLevel, MarketInstrument, MarketOrderBook, MarketOrderQuote, MarketOrderSide, MarketTimeInForce } from "./api";
import type { TradeConfirmation } from "./MarketPlaza";

const MAX_ORDER_QUANTITY = 9_999;
const DEPTH_ROWS = 2;

export type MarketTradeBoardProps = {
  catalog: MarketInstrument[];
  loading: boolean;
  error: unknown;
  selected: MarketInstrument | null;
  onSelect: (instrumentId: string) => void;
  book: MarketOrderBook | undefined;
  bookLoading: boolean;
  availableQuantity: number | undefined;
  availableRice: number | undefined;
  side: MarketOrderSide;
  setSide: (side: MarketOrderSide) => void;
  timeInForce: MarketTimeInForce;
  setTimeInForce: (timeInForce: MarketTimeInForce) => void;
  quantity: NumericFieldValue;
  setQuantity: Dispatch<SetStateAction<NumericFieldValue>>;
  unitPrice: NumericFieldValue;
  setUnitPrice: Dispatch<SetStateAction<NumericFieldValue>>;
  quote: MarketOrderQuote | undefined;
  quotePending: boolean;
  quoteError: string | null;
  confirmation: TradeConfirmation | null;
  busy: boolean;
  locked: boolean;
  onCancelConfirmation: () => void;
  onSubmit: () => void;
  boardView: MarketBoardView;
  setBoardView: (view: MarketBoardView) => void;
  /** 내 매물이 걸려 있는 품목. 거래소가 자기 매물을 자기에게 팔지 않으므로 미리 알린다. */
  mySellInstrumentIds: ReadonlySet<string>;
};

function rice(value: number | null | undefined): string {
  return value == null ? "—" : `${value.toLocaleString()} 쌀`;
}
function variantLabel(instrument: MarketInstrument): string {
  return instrumentVariant(instrument, valueText);
}

export function MarketTradeBoard(props: MarketTradeBoardProps) {
  const { catalog, loading, error, selected, onSelect } = props;
  /*
   * 분류와 검색은 왼쪽 품목 찾기 한 곳에서만 고른다. 가운데 목록은 그 결과를 받는다.
   * 목록에도 같은 탭을 두었더니 어느 쪽이 진짜인지 알 수 없었다.
   */
  const [filters, setFilters] = useState<FilterState>(() => ({ category: (props.selected?.category ?? "MATERIAL") as FilterState["category"], query: "" }));
  /*
   * 왼쪽에서 좁힌 만큼만 가운데에 보여 준다. 분류만 고르면 그 분류 전부, 계열을
   * 펼치면 그 계열만, 등급 하나를 고르면 그 하나만. 분류·검색을 다시 고르면 푼다.
   */
  const [focus, setFocus] = useState<MarketFocus>(null);
  const narrow = (next: SetStateAction<FilterState>) => { setFocus(null); setFilters(next); };
  if (error) return <p className="market-plaza-empty" role="alert">거래소 물품을 불러오지 못했어요. 잠시 뒤 다시 열어 주세요.</p>;
  if (loading) return <p className="market-plaza-empty" role="status">거래소를 여는 중이에요…</p>;
  if (!selected) return <p className="market-plaza-empty">아직 거래할 수 있는 물품이 없어요.</p>;
  /*
   * 간단 목록이 기본이다. 차트와 호가가 있는 시세 보기는 원하는 사람만 연다.
   * 두 보기 모두 왼쪽 품목 찾기를 그대로 쓴다. 가운데 칸만 바뀐다.
   */
  const toggle = <BoardViewToggle view={props.boardView} onChange={props.setBoardView} />;
  if (props.boardView === "list") return <div className="market-plaza-trade is-list">
    <PickerColumn catalog={catalog} selected={selected} filters={filters} setFilters={narrow}
      onSelect={id => {
        /* 뺀 것을 사게 되면 안 된다. 더할 때만 거래 대상이 되고, 거래하던 것을 빼면 남은 것으로 옮긴다. */
        const next = toggleFocusInstrument(focus, id);
        const ids = next?.kind === "instruments" ? next.ids : [];
        if (ids.includes(id)) onSelect(id);
        else if (selected.instrumentId === id && ids.length) onSelect(ids[ids.length - 1]);
        setFocus(next);
      }}
      focusedIds={new Set(focus?.kind === "instruments" ? focus.ids : [])}
      onFocusGroup={key => setFocus(key ? { kind: "group", key } : null)} />
    <MarketSimpleList catalog={catalog} filters={filters} focus={focus} selected={selected} onSelect={onSelect} toggle={toggle} mine={props.mySellInstrumentIds} />
    <TradeTicket {...props} selected={selected} />
  </div>;
  return <div className="market-plaza-trade">
    <PickerColumn catalog={catalog} selected={selected} onSelect={onSelect} filters={filters} setFilters={narrow} />
    <ItemDetail selected={selected} book={props.book} bookLoading={props.bookLoading} onPickPrice={price => props.setUnitPrice(price)} toggle={toggle} />
    <TradeTicket {...props} selected={selected} />
  </div>;
}

/** 두 보기를 오가는 단추. 늘 "지금 아닌 쪽"의 이름을 달고 있다. */
function BoardViewToggle({ view, onChange }: { view: MarketBoardView; onChange: (view: MarketBoardView) => void }) {
  const next: MarketBoardView = view === "chart" ? "list" : "chart";
  return <button type="button" className="market-plaza-view-toggle" onClick={() => onChange(next)}>
    <PlazaIcon name={next === "list" ? "list" : "chart"} />{next === "list" ? "간단 목록" : "시세 보기"}
  </button>;
}

/*
 * 왼쪽 칸은 이미 배포된 품목 찾기를 그대로 쓴다. 계열 › 등급으로 접히는 그 구조가
 * 검증돼 있어서, 거래소 새 화면이라고 따로 만들 이유가 없었다.
 */
function PickerColumn({ catalog, selected, onSelect, filters, setFilters, onFocusGroup, focusedIds }: Pick<MarketTradeBoardProps, "catalog" | "onSelect"> & { selected: MarketInstrument; filters: FilterState; setFilters: Dispatch<SetStateAction<FilterState>>; onFocusGroup?: (groupKey: string | null) => void; focusedIds?: ReadonlySet<string> }) {
  return <div className="market-plaza-catalog">
    <InstrumentPicker items={catalog} selected={selected} filters={filters} setFilters={setFilters} onSelect={onSelect} onFocusGroup={onFocusGroup} focusedIds={focusedIds} />
  </div>;
}

/** 가운데 목록이 얼마나 좁혀졌는지. null 이면 왼쪽 분류·검색 결과를 그대로 본다. */
export type MarketFocus = { kind: "group"; key: string } | { kind: "instruments"; ids: string[] } | null;

/*
 * 왼쪽에서 고른 만큼만 남긴다. 남는 것이 없으면 빈 목록 그대로 둔다. 예전에는 그럴 때
 * 좁힘을 풀고 전부 보여 줬는데, 파는 사람이 없는 등급을 골랐을 때 엉뚱한 줄이 잔뜩
 * 나와 고른 것이 반영되지 않은 것처럼 보였다. 빈 목록과 그 까닭을 적는 편이 낫다.
 */
export function focusMarketItems(items: MarketInstrument[], focus: MarketFocus): MarketInstrument[] {
  if (!focus) return items;
  return focus.kind === "group"
    ? items.filter(item => marketGroupKey(item) === focus.key)
    : items.filter(item => focus.ids.includes(item.instrumentId));
}

/*
 * 등급 하나만 볼 이유는 없다. 감자 D등급과 C등급을 나란히 놓고 값을 견주는 일이
 * 잦아, 누를 때마다 더하고 다시 누르면 뺀다. 거래하는 것은 늘 마지막에 누른 하나다.
 * 마지막 하나까지 빼면 좁힘을 푼다 — 아무것도 없는 목록을 보여 줄 이유가 없다.
 */
export function toggleFocusInstrument(focus: MarketFocus, id: string): MarketFocus {
  const current = focus?.kind === "instruments" ? focus.ids : [];
  const next = current.includes(id) ? current.filter(entry => entry !== id) : [...current, id];
  return next.length ? { kind: "instruments", ids: next } : null;
}

function DepthRows({ levels, tone, emptyText, onPickPrice }: { levels: MarketDepthLevel[]; tone: "ask" | "bid"; emptyText: string; onPickPrice: (price: number) => void }) {
  const widest = Math.max(1, ...levels.map(level => level.totalQuantity));
  if (levels.length === 0) return <p className="market-plaza-depth-empty">{emptyText}</p>;
  return <ul className={`market-plaza-depth ${tone}`}>{levels.map(level => <li key={level.unitPrice}>
    <button type="button" onClick={() => onPickPrice(level.unitPrice)} aria-label={`${level.unitPrice.toLocaleString()}쌀 가격 고르기`}>
      <b>{level.unitPrice.toLocaleString()} 쌀</b>
      <i aria-hidden="true"><span style={{ width: `${Math.round((level.totalQuantity / widest) * 100)}%` }} /></i>
      <small>수량 <strong>{level.totalQuantity.toLocaleString()}</strong></small>
    </button>
  </li>)}</ul>;
}

function ItemDetail({ selected, book, bookLoading, onPickPrice, toggle }: { selected: MarketInstrument; book: MarketOrderBook | undefined; bookLoading: boolean; onPickPrice: (price: number) => void; toggle: React.ReactNode }) {
  const asks = (book?.asks ?? []).slice(0, DEPTH_ROWS).reverse();
  const bids = (book?.bids ?? []).slice(0, DEPTH_ROWS);
  return <div className="market-plaza-detail">
    <header className="market-plaza-item-heading">
      <img src={instrumentArt(selected)} alt="" />
      {/* 등급은 오른쪽에 따로 있으니 이름에서 뺀다. 설명은 읽을 사람이 없다. */}
      <h3>{instrumentShortName(selected)}</h3>
      <span className="market-plaza-variant">{variantLabel(selected)}</span>
      {toggle}
    </header>
    <div className="market-plaza-chart"><PriceTrendChart instrumentId={selected.instrumentId} /></div>
    <section className="market-plaza-ladder" aria-label="지금 나와 있는 거래">
      <div className="market-plaza-ladder-side ask">
        <h4><PlazaIcon name="bag" />다른 사람이 파는 중</h4>
        <DepthRows levels={asks} tone="ask" emptyText={bookLoading ? "시세를 확인하는 중이에요…" : "지금 파는 사람이 없어요."} onPickPrice={onPickPrice} />
      </div>
      <div className="market-plaza-ladder-side bid">
        <h4><PlazaIcon name="cart" />다른 사람이 사는 중</h4>
        <DepthRows levels={bids} tone="bid" emptyText={bookLoading ? "시세를 확인하는 중이에요…" : "지금 사는 사람이 없어요."} onPickPrice={onPickPrice} />
      </div>
      {/* "바로 구매 / 바로 판매"로는 누가 무엇을 하는 값인지 읽히지 않았다.
          지금 내가 사면 얼마, 팔면 얼마인지 그대로 적는다. */}
      <p className="market-plaza-spread">
        <span>지금 사면 <strong className="ask">{rice(book?.bestAskUnitPrice ?? selected.bestAskUnitPrice)}</strong></span>
        <i aria-hidden="true" />
        <span>지금 팔면 <strong className="bid">{rice(book?.bestBidUnitPrice ?? selected.bestBidUnitPrice)}</strong></span>
      </p>
    </section>
  </div>;
}

function TradeTicket({
  selected, book, availableQuantity, availableRice, side, setSide, timeInForce, setTimeInForce,
  quantity, setQuantity, unitPrice, setUnitPrice, quote, quotePending, quoteError, confirmation, busy, locked,
  onCancelConfirmation, onSubmit, boardView,
}: MarketTradeBoardProps & { selected: MarketInstrument }) {
  const buying = side === "BUY";
  /*
   * 간단 목록에서는 사고파는 것만 고른다. 거래 방식을 고르는 줄은 호가가 보이는 시세 보기의
   * 몫이다. 사는 것은 올라온 것 중 가장 싼 값에 그대로 사고, 파는 쪽은 값을 스스로 매겨
   * 내놓는 것이 본래 하는 일이라 값 칸을 내준다(그래서 파는 주문은 늘 기다리는 주문이 된다).
   */
  const plain = boardView === "list";
  const reserving = timeInForce === "GTC";
  const parsedQuantity = positiveInteger(quantity);
  const parsedPrice = marketUnitPrice(unitPrice);
  const bestOpposite = buying ? book?.bestAskUnitPrice ?? selected.bestAskUnitPrice : book?.bestBidUnitPrice ?? selected.bestBidUnitPrice;
  const maximum = buying
    ? reserving
      ? parsedPrice && availableRice ? Math.floor(availableRice / parsedPrice) : 0
      : maximumAffordableQuantity(book?.asks ?? [], parsedPrice ?? bestOpposite ?? 0, availableRice ?? 0)
    : availableQuantity ?? 0;
  /*
   * 견적은 "지금 당장 오갈 금액"이라 값을 걸어 두는 거래에서는 0이 돌아온다. 그 값에
   * 맞서 있는 사람이 없으면 당장 체결될 것이 없기 때문이다. 간단 보기가 묻는 것은
   * "이대로 다 오가면 얼마"라서 수량과 값을 그대로 곱한다.
   */
  const wouldTotal = parsedQuantity && parsedPrice ? parsedQuantity * parsedPrice : null;
  const estimate = plain ? wouldTotal : quote?.totalPrice ?? wouldTotal;
  /* 가진 것보다 많이 내놓으면 서버가 되돌린다. 눌러 봐야 오류만 받을 단추는 잠가 둔다. */
  const overSelling = !buying && availableQuantity !== undefined && (parsedQuantity ?? 0) > availableQuantity;
  /*
   * 거래소는 자기 매물을 자기에게 팔지 않는다. 간단 목록의 구매는 가장 싼 값 하나만
   * 훑으므로, 그 값에 걸린 것이 전부 내 매물이면 눌러도 한 개도 체결되지 않는다.
   */
  const topAsk = book?.asks?.[0];
  const onlyMyListing = plain && buying && Boolean(topAsk && topAsk.myQuantity >= topAsk.totalQuantity);
  const action = plain ? (buying ? "구매" : "판매") : `${reserving ? "예약" : "바로"} ${buying ? "구매" : "판매"}`;
  const priceLabel = plain ? "판매 가격" : "예약 가격";

  /*
   * 방향을 바꾸면 거래 방식도 그 방향의 기본으로 돌아간다. 사는 쪽은 나와 있는 값에 바로,
   * 파는 쪽은 값을 적어 내놓는 쪽이다. 간단 목록도 같은 기본을 쓴다 — 예전에 간단 목록이면
   * 무조건 "바로"로 두던 자리가 남아, 판매를 골라도 값 칸 대신 읽기 전용 줄이 잠깐 떴다.
   */
  const chooseSide = (next: MarketOrderSide) => { if (next !== side) { setSide(next); setTimeInForce(next === "BUY" ? "IOC" : "GTC"); } };
  const stepQuantity = (delta: number) => setQuantity(current => Math.max(1, Math.min(MAX_ORDER_QUANTITY, (typeof current === "number" ? current : 0) + delta)));
  const stepPrice = (delta: number) => setUnitPrice(current => Math.max(MIN_MARKET_UNIT_PRICE, Math.min(MAX_MARKET_UNIT_PRICE, (typeof current === "number" ? current : bestOpposite ?? MIN_MARKET_UNIT_PRICE) + delta)));

  const most = <button type="button" className="market-plaza-most" disabled={locked || maximum <= 0} onClick={() => setQuantity(Math.max(1, maximum))}>최대</button>;
  /*
   * +10·+50·+100 줄은 두지 않는다. 단추로 빼곡히 채우면 읽을 것만 늘어난다.
   * 한 번에 끝까지 가는 최대만 수량 칸 오른쪽에 붙인다.
   */
  const amount = <label className="market-plaza-field">
    <span>{buying ? "구매" : "판매"} 수량</span>
    <div className="market-plaza-stepper with-most">
      <button type="button" aria-label="수량 줄이기" disabled={locked} onClick={() => stepQuantity(-1)}>−</button>
      <input inputMode="numeric" aria-label="거래 수량" value={quantity} disabled={locked} onChange={event => setQuantity(numericInputValue(event.currentTarget.value, MAX_ORDER_QUANTITY))} />
      <button type="button" aria-label="수량 늘리기" disabled={locked} onClick={() => stepQuantity(1)}>+</button>
      {most}
    </div>
  </label>;

  return <form className={`market-plaza-ticket ${buying ? "buying" : "selling"}${plain ? " is-plain" : ""}`} aria-label="거래 넣기" onSubmit={event => { event.preventDefault(); onSubmit(); }}>
    <div className="market-plaza-side-toggle" role="group" aria-label="구매와 판매">
      <button type="button" className={buying ? "active buy" : ""} aria-pressed={buying} disabled={locked} onClick={() => chooseSide("BUY")}><PlazaIcon name="cart" />구매</button>
      <button type="button" className={!buying ? "active sell" : ""} aria-pressed={!buying} disabled={locked} onClick={() => chooseSide("SELL")}><PlazaIcon name="bag" />판매</button>
    </div>
    {!plain && <><div className="market-plaza-mode-toggle" role="group" aria-label="거래 방식">
      <button type="button" className={!reserving ? "active" : ""} aria-pressed={!reserving} disabled={locked} onClick={() => { setTimeInForce("IOC"); setUnitPrice(bestOpposite ?? ""); }}>바로 {buying ? "구매" : "판매"}</button>
      <button type="button" className={reserving ? "active" : ""} aria-pressed={reserving} disabled={locked} onClick={() => setTimeInForce("GTC")}>예약 {buying ? "구매" : "판매"}</button>
    </div>
    <p className="market-plaza-mode-hint"><PlazaIcon name="clock" />{reserving
      ? `원하는 가격이 되면 자동으로 ${buying ? "구매" : "판매"}해요.`
      : `지금 나와 있는 가격으로 바로 ${buying ? "사요" : "팔아요"}.`}</p></>}

    {/* 목록에서 고른 것이 무엇인지 거래 칸에도 적어 둔다. 목록은 길어서 고른 줄이
        화면 밖으로 밀려나기 쉽고, 그러면 무엇을 사는지 모른 채 단추를 누르게 된다. */}
    {plain && <div className="market-plaza-ticket-item">
      <span className="market-plaza-ticket-art"><InstrumentIcon item={selected} /></span>
      <strong>{instrumentShortName(selected)}</strong>
      <em>{variantLabel(selected)}</em>
    </div>}
    {/* 수량·가격·총액·보유와 거래 단추는 한 덩어리로 칸 맨 아래에 붙는다. 수량만
        위에 떨어뜨려 두면 가격 줄과 사이가 휑하게 벌어진다. */}
    <div className="market-plaza-ticket-foot">
    {amount}
    {/* 간단 보기의 값 칸에는 쌀 그림도 안내 줄도 두지 않는다. 옆 칸이 수량이라 그림이
        무엇을 가리키는지 되레 헷갈렸고, 무슨 일이 일어나는지 설명하는 일은 시세 보기 몫이다. */}
    {reserving ? <label className="market-plaza-field">
      {/* 그림이 옆에 붙었으니 이름에 단위를 또 적지 않는다. */}
      <span>{priceLabel}</span>
      <div className="market-plaza-stepper">
        <button type="button" aria-label="가격 내리기" disabled={locked} onClick={() => stepPrice(-1)}>−</button>
        <input inputMode="numeric" aria-label={priceLabel} value={unitPrice} disabled={locked} onChange={event => setUnitPrice(numericInputValue(event.currentTarget.value, MAX_MARKET_UNIT_PRICE))} />
        <button type="button" aria-label="가격 올리기" disabled={locked} onClick={() => stepPrice(1)}>+</button>
        {/* 무엇으로 치르는 값인지 — 쌀 — 을 칸 끝에 둔다. 수량 줄의 최대와 같은 자리다. */}
        <span className="market-plaza-stepper-unit"><img src={riceIcon} alt="쌀" /></span>
      </div>
    </label> : <p className="market-plaza-field readonly">
      <span>{buying ? "지금 사는 가격" : "지금 파는 가격"}</span>
      <strong>{rice(bestOpposite)}</strong>
    </p>}

    <p className="market-plaza-total"><span>예상 총액</span><strong><img src={riceIcon} alt="" />{estimate == null ? "—" : estimate.toLocaleString()}</strong></p>
    <p className="market-plaza-holdings">{buying
      ? `보유 쌀 ${availableRice?.toLocaleString() ?? "—"} 쌀`
      : `팔 수 있는 수량 ${availableQuantity?.toLocaleString() ?? "—"}개`}</p>
    {onlyMyListing && <p className="market-plaza-ticket-error" role="alert">이 값에 걸린 것은 내가 올린 매물이라 살 수 없어요.</p>}

    {confirmation ? <div className="market-plaza-confirm" role="group" aria-label="거래 확인">
      <p>{confirmation.quote.expectedFilledQuantity.toLocaleString()}개가 지금 거래되고 {confirmation.quote.expectedRemainingQuantity.toLocaleString()}개는 {reserving ? "예약으로 기다려요" : "거래되지 않고 끝나요"}.</p>
      <p className="market-plaza-confirm-total">{buying ? "낼 쌀" : "받을 쌀"} <strong>{rice(buying ? confirmation.quote.totalPrice : confirmation.quote.expectedSettlementAmount)}</strong></p>
      <div>
        <button type="submit" className="market-plaza-cta" disabled={busy}>{busy ? "거래하는 중…" : "이대로 거래하기"}</button>
        <button type="button" className="market-plaza-secondary" disabled={busy} onClick={onCancelConfirmation}>다시 볼게요</button>
      </div>
    </div> : <button type="submit" className="market-plaza-cta" disabled={locked || !parsedQuantity || !parsedPrice || overSelling || onlyMyListing}>
      <PlazaIcon name={buying ? "cart" : "bag"} />{quotePending ? "확인하는 중…" : `${action}하기`}
    </button>}
    </div>
  </form>;
}
