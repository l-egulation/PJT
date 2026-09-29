import { useEffect, useMemo, useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { authApi } from "../auth/api";
import { inventoryApi } from "../inventory/api";
import { gemsApi } from "../gems/api";
import { marketApi, type MarketOrderQuote, type MarketOrderSide, type MarketPage, type MarketTimeInForce, type MarketTrade } from "./api";
import { marketErrorMessage } from "./marketErrors";
import { MIN_MARKET_UNIT_PRICE, marketUnitPrice, positiveInteger, type NumericFieldValue } from "./marketForm";
import { MARKET_UI_STORAGE_KEY, readMarketUiState, type MarketBoardView } from "./marketUiState";
import { riceIcon } from "./marketArt";
import { MarketTradeBoard } from "./MarketTradeBoard";
import { MarketLedger } from "./MarketLedger";
import { MarketInbox } from "./MarketInbox";
import { PlazaIcon } from "./PlazaIcon";
import { TopAlert } from "../../shared/TopAlert";
import marketTitleIcon from "../../shared/assets/cozy-hud-v1/icons/nav-market.png";
import paperCloseIcon from "../../shared/assets/cozy-paper-v2/close-button.png";
import "./MarketPlaza.css";

export type MarketPlazaTab = "trade" | "ledger" | "inbox";
export type MarketMode = "buy" | "sell" | "history";
export type MarketOrderRequest = { request: Parameters<typeof marketApi.createOrder>[0]; idempotencyKey: string };
export type TradeConfirmation = MarketOrderRequest & { quote: MarketOrderQuote };
/*
 * 잘된 일은 알리지 않는다. 산 물건은 가방에, 받은 쌀은 지갑에, 취소한 거래는 받기에
 * 곧바로 나타나므로 초록 띠는 화면만 밀어 올릴 뿐이었다. 남는 것은 잘못된 일뿐이다.
 */
type PlazaMessage = { text: string };

const TABS: Array<{ id: MarketPlazaTab; label: string; icon: "cart" | "ledger" | "parcel" }> = [
  { id: "trade", label: "거래하기", icon: "cart" },
  { id: "ledger", label: "내 거래", icon: "ledger" },
  { id: "inbox", label: "받기", icon: "parcel" },
];
const MAX_TRADE_PAGES = 8;
const REFRESH_KEYS = [["market-instruments"], ["market-order-book"], ["market-order-quote"], ["market-orders"], ["market-deliveries"], ["market-summary"], ["market-trades"], ["market-mails"], ["inventory"], ["gems"], ["auth", "session"]] as const;

export function MarketPlaza({ onClose, initialMode = "buy", pollingEnabled = true }: { onClose?: () => void; initialMode?: MarketMode; pollingEnabled?: boolean } = {}) {
  const client = useQueryClient();
  const [stored] = useState(() => readMarketUiState(window.sessionStorage));
  const [tab, setTab] = useState<MarketPlazaTab>(() => initialMode === "history" ? "ledger" : "trade");
  const [selectedId, setSelectedId] = useState<string | null>(null);
  const [side, setSide] = useState<MarketOrderSide>(initialMode === "sell" ? "SELL" : "BUY");
  const [timeInForce, setTimeInForce] = useState<MarketTimeInForce>(initialMode === "sell" ? "GTC" : "IOC");
  const [quantity, setQuantity] = useState<NumericFieldValue>(initialMode === "sell" ? 1 : 10);
  const [unitPrice, setUnitPrice] = useState<NumericFieldValue>("");
  const [message, setMessage] = useState<PlazaMessage | null>(null);
  const [confirmation, setConfirmation] = useState<TradeConfirmation | null>(null);
  const [checking, setChecking] = useState(false);
  /* 시세 보기와 간단 목록 중 무엇을 쓰는지는 취향이다. 한 번 고르면 다음에도 그대로 연다. */
  const [boardView, setBoardView] = useState<MarketBoardView>(() => stored.boardView);

  const session = useQuery({ queryKey: ["auth", "session"], queryFn: authApi.session, retry: false, refetchInterval: pollingEnabled ? 3_000 : false });
  const instruments = useQuery({ queryKey: ["market-instruments"], queryFn: marketApi.instruments, refetchInterval: pollingEnabled ? 3_000 : false });
  const catalog = useMemo(() => instruments.data ?? [], [instruments.data]);
  const selected = catalog.find(item => item.instrumentId === selectedId)
    ?? catalog.find(item => item.itemId === stored.selectedItemId)
    ?? catalog.find(item => item.itemId === "POTATO_M1") ?? catalog[0] ?? null;
  const book = useQuery({ queryKey: ["market-order-book", selected?.instrumentId], queryFn: () => marketApi.orderBook(selected!.instrumentId, 20), enabled: Boolean(selected), refetchInterval: pollingEnabled ? 3_000 : false });
  const inventory = useQuery({ queryKey: ["inventory", "market", selected?.itemId], queryFn: () => inventoryApi.detail(selected!.itemId), enabled: Boolean(selected), refetchInterval: pollingEnabled ? 3_000 : false });
  const gems = useQuery({ queryKey: ["gems", "market"], queryFn: gemsApi.state, enabled: selected?.category === "GEM", refetchInterval: pollingEnabled ? 3_000 : false });
  const saleGems = (gems.data?.gems ?? []).filter(gem => selected?.category === "GEM" && gem.level === Number(selected.attributes.level)
    && gem.option === (selected.attributes.option === "attack_speed" ? "HASTE" : selected.attributes.option?.toUpperCase())
    && gem.value === Number(selected.attributes.value) && !gem.locked && !gem.reservedForSale && gem.equippedPresets.length === 0);
  const availableQuantity = selected?.category === "GEM" ? gems.data ? saleGems.length : undefined : inventory.data?.availableQuantity;
  const quote = useQuery({
    queryKey: ["market-order-quote", selected?.instrumentId, side, timeInForce, positiveInteger(quantity), marketUnitPrice(unitPrice)],
    queryFn: () => marketApi.quote(selected!.instrumentId, side, timeInForce, positiveInteger(quantity)!, marketUnitPrice(unitPrice)!),
    enabled: Boolean(selected) && positiveInteger(quantity) !== null && marketUnitPrice(unitPrice) !== null && !confirmation,
    refetchInterval: pollingEnabled ? 3_000 : false, retry: false,
  });
  const activeOrders = useQuery({ queryKey: ["market-orders", "active"], queryFn: () => marketApi.orders("ACTIVE"), refetchInterval: pollingEnabled ? 3_000 : false });
  const closedOrders = useQuery({ queryKey: ["market-orders", "closed"], queryFn: () => marketApi.orders("CLOSED"), enabled: tab === "ledger", refetchInterval: pollingEnabled ? 5_000 : false });
  /*
   * 지난 거래 하나가 여러 번에 나뉘어 체결되면 그 기록이 한 쪽에 다 들어오지 않는다.
   * 한 쪽만 읽던 예전에는 큰 거래의 내역이 영영 "불러오는 중"에 머물렀다.
   */
  const myTrades = useQuery({
    queryKey: ["market-trades", "me"],
    queryFn: async () => {
      const items: MarketTrade[] = [];
      let cursor: string | null = null;
      for (let page = 0; page < MAX_TRADE_PAGES; page += 1) {
        const response: MarketPage<MarketTrade> = await marketApi.trades([], true, cursor);
        items.push(...response.items);
        cursor = response.nextCursor;
        if (!cursor) break;
      }
      return items;
    },
    enabled: tab === "ledger", refetchInterval: pollingEnabled ? 15_000 : false,
  });
  /* 거래소는 자기 매물을 자기가 사지 못하게 막는다. 어느 물품에 내 매물이 걸려 있는지 알아 둔다. */
  const mySellInstrumentIds = useMemo(() => new Set((activeOrders.data?.items ?? [])
    .filter(order => order.side === "SELL" && order.remainingQuantity > 0)
    .map(order => order.instrumentId)), [activeOrders.data]);
  const deliveries = useQuery({ queryKey: ["market-deliveries"], queryFn: () => marketApi.deliveries(true), refetchInterval: pollingEnabled ? 3_000 : false });
  const settlements = useQuery({ queryKey: ["market-mails"], queryFn: () => marketApi.settlementMails(), refetchInterval: pollingEnabled ? 3_000 : false });
  const summary = useQuery({ queryKey: ["market-summary"], queryFn: marketApi.summary, refetchInterval: pollingEnabled ? 3_000 : false });

  useEffect(() => { if (selected && selected.instrumentId !== selectedId) setSelectedId(selected.instrumentId); }, [selected, selectedId]);
  useEffect(() => {
    window.sessionStorage.setItem(MARKET_UI_STORAGE_KEY, JSON.stringify({ ...stored, selectedItemId: selected?.itemId ?? stored.selectedItemId, boardView }));
  }, [selected?.instrumentId, boardView]);
  useEffect(() => { setConfirmation(null); }, [selected?.instrumentId, side, timeInForce, quantity, unitPrice]);
  /*
   * 값 칸의 첫 값은 거래 방식마다 뜻이 다르다. 바로 거래하는 값은 "지금 오갈 수 있는 값"
   * 이라 상대 쪽 최우선가여야 하고, 값을 걸어 두는 거래는 "얼마에 걸까"라 시세를 따라가는
   * 편이 낫다. 상대가 없는 물품도 시세나 마지막 거래가로는 값을 적을 수 있다.
   */
  useEffect(() => {
    if (!selected) return setUnitPrice("");
    const market = selected.bestAskUnitPrice ?? selected.lastTradeUnitPrice ?? selected.bestBidUnitPrice;
    /* 아무도 사지도 팔지도, 거래한 적도 없는 물품이면 기댈 시세가 없다. 그래도 값 칸은
       비워 두지 않는다. 빈 칸은 곧 잠긴 단추이고, 그러면 첫 거래를 아무도 시작할 수 없다. */
    if (timeInForce === "GTC") return setUnitPrice(market ?? MIN_MARKET_UNIT_PRICE);
    setUnitPrice((side === "BUY" ? selected.bestAskUnitPrice : selected.bestBidUnitPrice) ?? "");
  }, [selected?.instrumentId, side, timeInForce]);
  /*
   * 간단 목록은 판매 게시판이다. 사는 쪽은 올라온 것 중 가장 싼 값에 그대로 사므로 값을
   * 묻지 않고, 파는 쪽만 얼마에 올릴지 적는다.
   */
  useEffect(() => { if (boardView === "list") setTimeInForce(side === "BUY" ? "IOC" : "GTC"); }, [boardView, side]);

  const refresh = () => Promise.all(REFRESH_KEYS.map(key => client.invalidateQueries({ queryKey: key })));
  const fail = (fallback: string) => (error: unknown) => setMessage({ text: marketErrorMessage(error, fallback) });
  const create = useMutation({
    mutationFn: (order: MarketOrderRequest) => marketApi.createOrder(order.request, order.idempotencyKey),
    onSuccess: async () => {
      setConfirmation(null);
      await refresh();
    },
    onError: fail("거래를 넣지 못했어요."),
  });
  const update = useMutation({ mutationFn: ({ orderId, request }: { orderId: string; request: { quantity?: number; limitUnitPrice?: number } }) => marketApi.updateOrder(orderId, request), onSuccess: () => refresh(), onError: fail("거래를 수정하지 못했어요.") });
  const cancel = useMutation({ mutationFn: marketApi.cancelOrder, onSuccess: () => refresh(), onError: fail("거래를 취소하지 못했어요.") });
  const claim = useMutation({ mutationFn: marketApi.claimDelivery, onSuccess: () => refresh(), onError: fail("물품을 받지 못했어요.") });
  const claimAll = useMutation({ mutationFn: marketApi.claimAllDeliveries, onSuccess: () => refresh(), onError: fail("물품을 모두 받지 못했어요.") });
  const claimRice = useMutation({ mutationFn: marketApi.claimSettlement, onSuccess: () => refresh(), onError: fail("쌀을 받지 못했어요.") });
  const claimAllRice = useMutation({ mutationFn: marketApi.claimAllSettlements, onSuccess: () => refresh(), onError: fail("쌀을 모두 받지 못했어요.") });
  const busy = [create, update, cancel, claim, claimAll, claimRice, claimAllRice].some(mutation => mutation.isPending);
  const locked = busy || checking || confirmation !== null;

  const buildRequest = () => {
    const parsedQuantity = positiveInteger(quantity), parsedPrice = marketUnitPrice(unitPrice);
    if (!selected || !parsedQuantity || !parsedPrice || locked) return null;
    return {
      instrumentId: selected.instrumentId, side, timeInForce, quantity: parsedQuantity, limitUnitPrice: parsedPrice,
      ...(side === "SELL" && selected.category === "GEM" ? { instanceIds: saleGems.slice(0, parsedQuantity).map(gem => gem.gemId) } : {}),
    };
  };
  const requestConfirmation = async () => {
    const request = buildRequest();
    if (!request) return;
    setChecking(true);
    setMessage(null);
    try {
      setConfirmation({ request, quote: await marketApi.quote(request.instrumentId, side, timeInForce, request.quantity, request.limitUnitPrice), idempotencyKey: crypto.randomUUID() });
    } catch (error) {
      setMessage({ text: marketErrorMessage(error, "지금 시세를 불러오지 못했어요.") });
    } finally {
      setChecking(false);
    }
  };
  /*
   * 간단 목록은 한 번 더 묻지 않는다. 고를 것이 수량과 값뿐이고 둘 다 화면에 적혀 있어,
   * "이대로 거래할까요"는 이미 아는 것을 다시 보여 줄 뿐이었다. 시세 보기는 여러 가격대를
   * 훑어 체결되므로 무엇이 얼마에 오갈지 미리 보여 주는 확인 단계를 남긴다.
   */
  const submit = () => {
    if (confirmation) return create.mutate(confirmation);
    if (boardView !== "list") return void requestConfirmation();
    const request = buildRequest();
    if (request) create.mutate({ request, idempotencyKey: crypto.randomUUID() });
  };

  const inboxCount = (summary.data?.claimableDeliveries ?? 0) + (summary.data?.claimableSettlements ?? 0);
  const tabCount = (id: MarketPlazaTab) => id === "ledger" ? summary.data?.activeOrders ?? 0 : id === "inbox" ? inboxCount : 0;

  /* 서버가 준 문제는 창 안이 아니라 화면 맨 위에 뜬다. 거래 칸 배치가 밀리지 않는다. */
  const alert = message?.text ?? (!confirmation && quote.error ? marketErrorMessage(quote.error, "예상 금액을 불러오지 못했어요.") : null);

  return <section className="market-plaza" aria-labelledby="market-plaza-title">
    {alert && <TopAlert message={alert} action={<button type="button" onClick={() => { setMessage(null); void quote.refetch(); }}>다시 확인</button>} />}
    <div className="market-plaza-paper">
      <header className="market-plaza-heading">
        <h2 id="market-plaza-title"><img className="market-plaza-title-icon" src={marketTitleIcon} alt="" aria-hidden="true" /><span>거래소</span></h2>
        <nav className="market-plaza-tabs" aria-label="거래소 메뉴">{TABS.map(entry => <button
          key={entry.id} type="button" className={tab === entry.id ? "active" : ""}
          aria-current={tab === entry.id ? "page" : undefined} disabled={locked && tab !== entry.id}
          onClick={() => { setTab(entry.id); setMessage(null); }}
        ><PlazaIcon name={entry.icon} /><span>{entry.label}</span>{tabCount(entry.id) > 0 && <em>{tabCount(entry.id)}</em>}</button>)}</nav>
        <p className="market-plaza-rice"><img src={riceIcon} alt="" /><span>보유 쌀</span><strong>{session.data?.account?.rice.toLocaleString() ?? "—"}</strong></p>
        {onClose && <button type="button" className="paper-close" aria-label="거래소 닫기" onClick={onClose}><img src={paperCloseIcon} alt="" aria-hidden="true" /></button>}
      </header>

      <div className="market-plaza-body">
        {tab === "trade" && <MarketTradeBoard
          catalog={catalog} loading={instruments.isLoading} error={instruments.error} selected={selected} onSelect={setSelectedId}
          book={book.data} bookLoading={book.isLoading} availableQuantity={availableQuantity} availableRice={session.data?.account?.rice}
          side={side} setSide={setSide} timeInForce={timeInForce} setTimeInForce={setTimeInForce}
          quantity={quantity} setQuantity={setQuantity} unitPrice={unitPrice} setUnitPrice={setUnitPrice}
          quote={confirmation?.quote ?? quote.data} quotePending={checking || (!confirmation && quote.isFetching)}
          quoteError={!confirmation && quote.error ? marketErrorMessage(quote.error, "예상 금액을 불러오지 못했어요.") : null}
          confirmation={confirmation} busy={busy} locked={locked}
          onCancelConfirmation={() => setConfirmation(null)} onSubmit={submit}
          boardView={boardView} setBoardView={setBoardView} mySellInstrumentIds={mySellInstrumentIds} />}
        {tab === "ledger" && <MarketLedger
          active={activeOrders.data?.items ?? []} closed={closedOrders.data?.items ?? []} trades={myTrades.data ?? []}
          loading={activeOrders.isLoading || closedOrders.isLoading} busy={busy}
          onUpdate={(orderId, request) => update.mutate({ orderId, request })} onCancel={orderId => cancel.mutate(orderId)} />}
        {tab === "inbox" && <MarketInbox
          deliveries={deliveries.data?.items ?? []} settlements={settlements.data?.items ?? []}
          loading={deliveries.isLoading || settlements.isLoading} busy={busy}
          onClaimDelivery={id => claim.mutate(id)} onClaimSettlement={id => claimRice.mutate(id)}
          onClaimAll={() => { claimAll.mutate(); claimAllRice.mutate(); }} />}
      </div>
    </div>
  </section>;
}
