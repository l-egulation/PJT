import { useEffect, useMemo, useRef, useState } from "react";
import { useInfiniteQuery } from "@tanstack/react-query";
import { marketApi, type MarketInstrument, type MarketOrderSide, type MarketPriceLevel, type MarketTimeInForce } from "./api";
import "./PriceLevelGuide.css";

const PRICE_LEVEL_PAGE_SIZE = 10;

type PriceLevelGuideProps = {
  instrument: MarketInstrument;
  itemIconSrc: string;
  side: MarketOrderSide;
  timeInForce: MarketTimeInForce;
  quantity: number | null;
  unitPrice: number | null;
  quoteRevision?: number;
  onPrice: (price: number) => void;
  disabled: boolean;
};

type GuideRow = MarketPriceLevel & {
  otherQuantity: number;
  inLimit: boolean;
  requestedQuantity: number;
};

function numberText(value: number): string {
  return value.toLocaleString("ko-KR");
}

function restingSideFor(side: MarketOrderSide, timeInForce: MarketTimeInForce): MarketOrderSide {
  if (side === "BUY") return "SELL";
  return timeInForce === "IOC" ? "BUY" : "SELL";
}

function isInLimit(unitPrice: number, restingSide: MarketOrderSide, limit: number | null): boolean {
  if (limit === null || !Number.isFinite(limit)) return false;
  return restingSide === "SELL" ? unitPrice <= limit : unitPrice >= limit;
}

export function PriceLevelGuide({ instrument, itemIconSrc, side, timeInForce, quantity, unitPrice, quoteRevision, onPrice, disabled }: PriceLevelGuideProps) {
  const restingSide = restingSideFor(side, timeInForce);
  const [page, setPage] = useState(0);
  const [reloadToken, setReloadToken] = useState(0);
  const resetRevisionRef = useRef<string | null>(null);
  const queryKey = useMemo(() => ["market-price-levels", instrument.instrumentId, restingSide, instrument.marketRevision, reloadToken] as const, [instrument.instrumentId, instrument.marketRevision, reloadToken, restingSide]);
  const priceLevels = useInfiniteQuery({
    queryKey,
    queryFn: ({ pageParam }) => marketApi.priceLevels(instrument.instrumentId, restingSide, pageParam, PRICE_LEVEL_PAGE_SIZE),
    initialPageParam: null as number | null,
    getNextPageParam: (lastPage) => lastPage.nextUnitPrice,
    enabled: Boolean(instrument.instrumentId),
    refetchInterval: 3_000,
  });
  const pages = priceLevels.data?.pages ?? [];
  const revisionSignature = pages.map((page) => page.marketRevision).join(",");
  const revisions = new Set(pages.map((page) => page.marketRevision));
  const mixedRevision = revisions.size > 1;
  const pageRevision = pages[0]?.marketRevision ?? null;
  const pageBehindInstrument = pageRevision !== null && pageRevision < instrument.marketRevision;
  useEffect(() => { setPage(0); }, [instrument.instrumentId, restingSide]);
  useEffect(() => {
    if (!mixedRevision) { resetRevisionRef.current = null; return; }
    if (resetRevisionRef.current === revisionSignature) return;
    resetRevisionRef.current = revisionSignature;
    setReloadToken(current => current + 1);
  }, [mixedRevision, revisionSignature]);
  const rawLevels = mixedRevision ? [] : (pages[page]?.items ?? []);
  const currentPage = pages[page];
  const totalLevels = Number.isFinite(currentPage?.totalLevels) ? Math.max(0, currentPage!.totalLevels) : (currentPage?.nextUnitPrice == null ? currentPage?.items.length ?? 0 : (page + 2) * PRICE_LEVEL_PAGE_SIZE);
  const totalPages = Math.max(1, Math.ceil(totalLevels / PRICE_LEVEL_PAGE_SIZE));
  const canNext = page + 1 < totalPages;
  const canPrevious = page > 0;
  const loadPage = async (target: number) => {
    if (target < 0 || target >= totalPages || target === page || priceLevels.isFetchingNextPage) return;
    if (target > page) await priceLevels.fetchNextPage();
    setPage(target);
  };
  const rows = useMemo<GuideRow[]>(() => {
    let remaining = Math.max(quantity ?? 0, 0);
    return rawLevels.map((level) => {
      const otherQuantity = Math.max(level.totalQuantity - level.myQuantity, 0);
      const inLimit = isInLimit(level.unitPrice, restingSide, unitPrice);
      const requestedQuantity = timeInForce === "IOC" && inLimit && remaining > 0 ? Math.min(otherQuantity, remaining) : 0;
      remaining -= requestedQuantity;
      return { ...level, otherQuantity, inLimit, requestedQuantity };
    });
  }, [quantity, rawLevels, restingSide, timeInForce, unitPrice]);

  const executableView = side === "BUY" || timeInForce === "IOC";
  const selfCross = side === "SELL" && executableView && rows.some((row) => row.inLimit && row.myQuantity > 0);
  const finiteLimit = unitPrice !== null && Number.isFinite(unitPrice);
  const interactionDisabled = disabled || priceLevels.isFetchingNextPage || Boolean(priceLevels.error) || mixedRevision || pageBehindInstrument;
  const showLoading = priceLevels.isPending && !priceLevels.data;
  const showEmpty = !showLoading && !priceLevels.error && !mixedRevision && rows.length === 0;
  const waitingForRevision = pageBehindInstrument && !mixedRevision;
  const hasSelectedRow = unitPrice !== null && rows.some((row) => row.unitPrice === unitPrice);
  const selectionOutsideVisibleRows = finiteLimit && !hasSelectedRow && rows.length > 0 && !priceLevels.hasNextPage;
  return (
    <section className="price-level-guide" aria-labelledby="price-level-guide-title">
      {selectionOutsideVisibleRows && (
        <p className="price-level-guide-page-hint" role="status">입력 단가와 정확히 일치하는 가격대는 현재 표시된 목록에 없습니다. 입력 단가는 그대로 유지됩니다.</p>
      )}
      {waitingForRevision && (
        <p className="price-level-guide-status" role="status">시장이 바뀌어 최신 가격대를 불러오는 중입니다.</p>
      )}
      {pageRevision !== null && quoteRevision !== undefined && pageRevision !== quoteRevision && <p className="price-level-guide-page-hint" role="status">가격대 물량과 거래 견적의 조회 시점이 다릅니다. 확인 시 최신 견적을 다시 조회합니다.</p>}
      {selfCross && (
        <p className="price-level-guide-warning" role="alert">
          허용 가격 안에 내 반대 주문이 있어 거래할 수 없습니다. 내 주문을 확인해 주세요.
        </p>
      )}
      {mixedRevision && (
        <p className="price-level-guide-status" role="status">시장 가격이 바뀌어 가격대를 처음부터 다시 불러오는 중입니다.</p>
      )}
      {showLoading && <p className="price-level-guide-status" role="status">가격대를 불러오는 중입니다.</p>}
      {priceLevels.error && (
        <div className="price-level-guide-error" role="alert">
          <span>가격 안내를 불러오지 못했습니다.</span>
          <button type="button" disabled={disabled || priceLevels.isFetching} onClick={() => void priceLevels.refetch()}>다시 시도</button>
        </div>
      )}
      {showEmpty && <p className="price-level-guide-empty">표시할 가격대가 없습니다.</p>}

      {rows.length > 0 && !mixedRevision && (
        <>
          <div className="price-level-guide-table-wrap">
            <table className={`price-level-guide-table${side === "BUY" ? " with-allocation" : ""}`} aria-label="시장 가격대">
              <caption>시장 가격대. 각 행을 선택하면 입력 단가만 바뀝니다.</caption>
              <colgroup>
                <col className="price-level-guide-col-item" />
                <col className="price-level-guide-col-stock" />
                <col className="price-level-guide-col-available" />
                {side === "BUY" && <col className="price-level-guide-col-allocation" />}
                <col className="price-level-guide-col-price" />
              </colgroup>
              <thead>
                <tr>
                  <th scope="col">품목</th>
                  <th scope="col">잔량</th>
                  <th scope="col">가용 매물(누적)</th>
                  {side === "BUY" && <th scope="col">예상 배분</th>}
                  <th scope="col">개당 가격</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const selected = unitPrice !== null && row.unitPrice === unitPrice;
                  return (
                    <tr
                      key={row.unitPrice}
                      className={`${row.inLimit ? "in-limit " : ""}${selected ? "selected " : ""}${row.requestedQuantity > 0 ? "has-request" : ""}`}
                    >
                      <th scope="row">
                        <span className="price-level-guide-item">
                          <span className="price-level-guide-item-art"><img src={itemIconSrc} alt="" aria-hidden="true" /></span>
                          <span className="price-level-guide-item-copy"><strong>{instrument.displayName}</strong></span>
                        </span>
                      </th>
                      <td>{numberText(row.totalQuantity)}개{row.myQuantity > 0 && <small className="price-level-guide-own">내 물량 {numberText(row.myQuantity)}개</small>}</td>
                      <td><strong>{numberText(row.cumulativeOtherQuantity)}개</strong></td>
                      {side === "BUY" && <td className="price-level-guide-request"><strong>{numberText(row.requestedQuantity)}개</strong></td>}
                      <td>
                        <button type="button" className="price-level-guide-price" disabled={interactionDisabled} aria-pressed={selected} aria-label={`${numberText(row.unitPrice)}쌀로 가격 입력`} onClick={() => onPrice(row.unitPrice)}>
                          <strong>{numberText(row.unitPrice)}쌀</strong>
                        </button>
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
          <div className="price-level-guide-paging">
            <button type="button" disabled={interactionDisabled || !canPrevious} onClick={() => void loadPage(page - 1)}>‹</button>
            <span><strong>{Math.min(page + 1, totalPages)}</strong> / {totalPages}</span>
            <button type="button" disabled={interactionDisabled || !canNext} onClick={() => void loadPage(page + 1)}>›</button>
          </div>
        </>
      )}
    </section>
  );
}

export type { PriceLevelGuideProps };
