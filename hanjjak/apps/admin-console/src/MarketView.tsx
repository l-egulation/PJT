import { FormEvent, useCallback, useEffect, useMemo, useRef, useState } from "react";
import { AdminCatalog, AdminMarketOrder, createApiClient } from "@hanjjak/client-sdk";
import { adminErrorMessage } from "./presentation";
import "./MarketView.css";

type AdminApi = ReturnType<typeof createApiClient>["admin"];
type MarketViewProps = { api: AdminApi };
type Operation = { kind: "purchase" | "cancel"; orderId: string } | null;

const numberFormat = new Intl.NumberFormat("ko-KR");
const number = (value: number) => numberFormat.format(value);
const MARKET_CATEGORIES = new Set(["MATERIAL", "SKILL_BOOK", "GEM"]);
const SYSTEM_CATEGORIES = new Set(["MATERIAL", "SKILL_BOOK"]);

function isSafePositiveInteger(value: string): boolean {
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) && parsed > 0;
}

export function MarketView({ api }: MarketViewProps) {
  const [catalog, setCatalog] = useState<AdminCatalog | null>(null);
  const [catalogError, setCatalogError] = useState<string | null>(null);
  const [catalogLoading, setCatalogLoading] = useState(true);
  const [orders, setOrders] = useState<AdminMarketOrder[]>([]);
  const [nextCursor, setNextCursor] = useState<string | null>(null);
  const [listingsLoading, setListingsLoading] = useState(true);
  const [listingsError, setListingsError] = useState<string | null>(null);
  const [filterItemId, setFilterItemId] = useState("");
  const [listingUuid, setListingUuid] = useState("");
  const [registerSearch, setRegisterSearch] = useState("");
  const [registerItemId, setRegisterItemId] = useState("");
  const [quantity, setQuantity] = useState("10000");
  const [unitPrice, setUnitPrice] = useState("100");
  const [registerReason, setRegisterReason] = useState("");
  const [operation, setOperation] = useState<Operation>(null);
  const [operationQuantity, setOperationQuantity] = useState<Record<string, string>>({});
  const [operationReason, setOperationReason] = useState<Record<string, string>>({});
  const [feedback, setFeedback] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const loadVersion = useRef(0);

  const filterItems = useMemo(
    () => (catalog?.items ?? []).filter((item) => item.tradeable && MARKET_CATEGORIES.has(item.category)),
    [catalog],
  );
  const registerItems = useMemo(
    () => (catalog?.items ?? []).filter((item) => item.stackable && item.tradeable && SYSTEM_CATEGORIES.has(item.category)),
    [catalog],
  );
  const visibleRegisterItems = useMemo(() => {
    const search = registerSearch.trim().toLocaleLowerCase();
    if (!search) return registerItems;
    return registerItems.filter((item) => `${item.displayName} ${item.itemId}`.toLocaleLowerCase().includes(search));
  }, [registerItems, registerSearch]);

  useEffect(() => {
    if (!registerItems.some((item) => item.itemId === registerItemId)) setRegisterItemId(registerItems[0]?.itemId ?? "");
  }, [registerItems, registerItemId]);

  const loadListings = useCallback(async (cursor?: string, append = false) => {
    const version = ++loadVersion.current;
    setListingsLoading(true);
    setListingsError(null);
    if (!append) setOrders([]);
    try {
      const query = listingUuid.trim() || filterItemId || undefined;
      const page = await api.marketOrders(query, cursor);
      if (version !== loadVersion.current) return;
      setOrders((current) => append ? [...current, ...page.items] : page.items);
      setNextCursor(page.nextCursor);
    } catch (failure) {
      if (version !== loadVersion.current) return;
      const message = adminErrorMessage(failure);
      setListingsError(message);
      setError(message);
    } finally {
      if (version === loadVersion.current) setListingsLoading(false);
    }
  }, [api, filterItemId, listingUuid]);

  useEffect(() => {
    let active = true;
    setCatalogLoading(true);
    setCatalogError(null);
    void api.catalog().then((value) => {
      if (!active) return;
      setCatalog(value);
    }).catch((failure) => {
      if (!active) return;
      setCatalogError(adminErrorMessage(failure));
    }).finally(() => {
      if (active) setCatalogLoading(false);
    });
    return () => { active = false; };
  }, [api]);

  useEffect(() => {
    void loadListings();
  }, [loadListings]);

  const runAction = async (action: () => Promise<string>) => {
    setOperation({ kind: "purchase", orderId: "__global__" });
    setError(null);
    setFeedback(null);
    try {
      setFeedback(await action());
      await loadListings();
    } catch (failure) {
      setError(adminErrorMessage(failure));
    } finally {
      setOperation(null);
    }
  };

  const register = async (event: FormEvent) => {
    event.preventDefault();
    const parsedQuantity = Number(quantity);
    const parsedPrice = Number(unitPrice);
    if (!registerItemId || !Number.isSafeInteger(parsedQuantity) || parsedQuantity < 1 || !Number.isSafeInteger(parsedPrice)) {
      setError("아이템, 수량과 단가를 확인해 주세요.");
      return;
    }
    await runAction(async () => {
      const result = await api.createSystemMarketOrder(registerItemId, parsedQuantity, parsedPrice, registerReason);
      return `${number(result.totalQuantity)}개를 ${number(result.orderIds.length)}개 주문으로 등록했습니다.`;
    });
  };

  const purchase = async (order: AdminMarketOrder) => {
    const raw = operationQuantity[order.orderId] ?? String(order.remainingQuantity);
    if (!isSafePositiveInteger(raw) || Number(raw) > order.remainingQuantity) {
      setError("구매 수량은 잔량 이내의 양의 정수여야 합니다.");
      return;
    }
    const reason = operationReason[order.orderId]?.trim() ?? "";
    if (reason.length < 3) {
      setError("시스템 구매 사유를 3자 이상 입력해 주세요.");
      return;
    }
    await runAction(async () => {
      const result = await api.purchaseMarketOrder(order.orderId, Number(raw), reason);
      return `${order.displayName} ${number(result.quantity)}개를 시스템이 구매했습니다. 판매자에게 ${number(result.settlementAmount)}쌀을 지급했습니다.`;
    });
  };

  const cancel = async (order: AdminMarketOrder) => {
    const reason = operationReason[order.orderId]?.trim() ?? "";
    if (reason.length < 3) {
      setError("강제 취소 사유를 3자 이상 입력해 주세요.");
      return;
    }
    await runAction(async () => {
      const result = await api.cancelMarketOrder(order.orderId, reason);
      return `${order.displayName} ${number(result.cancelledQuantity)}개 주문을 취소했습니다.`;
    });
  };

  const globalPending = operation?.orderId === "__global__";
  return <div className="content market-view">
    <section className="overview-head">
      <div><p>사용자·시스템 주문을 조회하고 명시적 운영 사유로 조치합니다.</p><span>등록 수량은 하나의 시스템 매도 주문으로 생성됩니다.</span></div>
      <button className="secondary" type="button" onClick={() => void loadListings()} disabled={Boolean(operation) || listingsLoading}>↻ 새로고침</button>
    </section>
    {(error || catalogError || listingsError) && <div className="alert error" role="alert">{error ?? catalogError ?? listingsError}</div>}
    {feedback && <div className="alert success" role="status">{feedback}</div>}
    <section className="market-command-grid">
      <form className="panel market-form" onSubmit={(event) => void register(event)}>
        <div className="panel-title"><h2>시스템 매물 등록</h2><p>보유 인벤토리 없이 운영 유동성 매물을 생성합니다.</p></div>
        <div className="form-body">
          <label>아이템 검색<input value={registerSearch} onChange={(event) => setRegisterSearch(event.target.value)} placeholder="이름 또는 ID로 검색" /></label>
          <label>아이템<select value={registerItemId} onChange={(event) => setRegisterItemId(event.target.value)} required disabled={catalogLoading || globalPending}><option value="">{catalogLoading ? "카탈로그 불러오는 중…" : "아이템을 선택하세요"}</option>{visibleRegisterItems.map((item) => <option key={item.itemId} value={item.itemId}>{item.displayName} · {item.itemId}</option>)}</select></label>
          <div className="form-row"><label>총수량<input type="number" min="1" step="1" value={quantity} onChange={(event) => setQuantity(event.target.value)} required disabled={globalPending} /></label><label>단가<input type="number" min="10" max="999999" step="1" value={unitPrice} onChange={(event) => setUnitPrice(event.target.value)} required disabled={globalPending} /></label></div>
          <label>등록 사유<textarea value={registerReason} onChange={(event) => setRegisterReason(event.target.value)} minLength={3} maxLength={500} required disabled={globalPending} placeholder="3자 이상 입력" /></label>
          <button className="primary" type="submit" disabled={globalPending || catalogLoading || !registerItemId}>매도 주문 등록</button>
        </div>
      </form>
      <section className="panel market-guide"><div className="panel-title"><h2>주문 조회</h2><p>아이템 이름으로 선택하거나 UUID를 고급 조회에 입력합니다.</p></div><div className="market-filter-body"><label>아이템 필터<select value={filterItemId} onChange={(event) => { setListingUuid(""); setFilterItemId(event.target.value); }} disabled={Boolean(operation)}><option value="">전체 거래 가능 아이템</option>{filterItems.map((item) => <option key={item.itemId} value={item.itemId}>{item.displayName} · {item.itemId}</option>)}</select></label><label>주문 UUID (선택)<input value={listingUuid} onChange={(event) => { setFilterItemId(""); setListingUuid(event.target.value); }} placeholder="고급 조회용 UUID" disabled={Boolean(operation)} /></label><p className="form-hint">이름 검색 목록은 서버 카탈로그 기준이며, 미보유 아이템도 표시됩니다.</p></div></section>
    </section>
    <section className="panel table-panel market-listings-panel"><div className="panel-title"><h2>주문 목록</h2><p>{listingsLoading && orders.length === 0 ? "불러오는 중…" : `${number(orders.length)}개 표시`}</p></div>{orders.length === 0 && !listingsLoading ? <div className="empty"><span>○</span><p>조건에 맞는 주문이 없습니다.</p></div> : <div className="market-listings">{orders.map((order) => { const pending = operation?.orderId === order.orderId; const reason = operationReason[order.orderId] ?? ""; const purchasable = order.side === "SELL" && !order.systemOrder && ["ACTIVE", "PARTIALLY_FILLED"].includes(order.status); const cancellable = ["ACTIVE", "PARTIALLY_FILLED"].includes(order.status); return <article className="market-listing" key={order.orderId}><div className="market-listing-summary"><div><strong>{order.displayName}</strong><small>{order.itemId} · {order.systemOrder ? "시스템 주문" : "사용자 주문"} · {order.side === "BUY" ? "매수" : "매도"}</small></div><dl><div><dt>잔량</dt><dd>{number(order.remainingQuantity)}</dd></div><div><dt>단가</dt><dd>{number(order.limitUnitPrice)}쌀</dd></div><div><dt>상태</dt><dd>{order.status}</dd></div></dl></div>{(purchasable || cancellable) && <div className="market-listing-actions">{purchasable && <label>시스템 구매 수량<input type="number" min="1" max={order.remainingQuantity} value={operationQuantity[order.orderId] ?? order.remainingQuantity} disabled={Boolean(operation)} onChange={(event) => setOperationQuantity((current) => ({ ...current, [order.orderId]: event.target.value }))} /></label>}<label>운영 사유<input minLength={3} maxLength={500} value={reason} disabled={Boolean(operation)} onChange={(event) => setOperationReason((current) => ({ ...current, [order.orderId]: event.target.value }))} /></label><div>{purchasable && <button type="button" className="primary" disabled={Boolean(operation)} onClick={() => { setOperation({ kind: "purchase", orderId: order.orderId }); void purchase(order).finally(() => setOperation(null)); }}>{pending && operation?.kind === "purchase" ? "처리 중…" : "시스템 구매"}</button>}<button type="button" className="danger" disabled={Boolean(operation)} onClick={() => { setOperation({ kind: "cancel", orderId: order.orderId }); void cancel(order).finally(() => setOperation(null)); }}>{pending && operation?.kind === "cancel" ? "처리 중…" : "강제 취소"}</button></div></div>}</article>; })}</div>}{nextCursor && <button className="secondary load-more" type="button" disabled={Boolean(operation) || listingsLoading} onClick={() => void loadListings(nextCursor, true)}>더 보기</button>}</section>
  </div>;
}
