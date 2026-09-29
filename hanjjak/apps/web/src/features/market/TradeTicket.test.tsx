// @vitest-environment happy-dom
import { fireEvent, render, screen } from "@testing-library/react";
import { useState } from "react";
import { expect, it } from "vitest";
import { TradeTicket } from "./MarketScreen";
import type { NumericFieldValue } from "./marketForm";
import type { MarketInstrument } from "./api";

const instrument: MarketInstrument = {
  instrumentId: "potato-1",
  canonicalKey: "material:potato_m1",
  itemId: "POTATO_M1",
  displayName: "감자 한 조각",
  category: "MATERIAL",
  attributes: {},
  status: "ACTIVE",
  bestBidUnitPrice: 90,
  bestAskUnitPrice: 95,
  lastTradeUnitPrice: 90,
  marketRevision: 1,
};

function SellTicket() {
  const [quantity, setQuantity] = useState<NumericFieldValue>("");
  const [unitPrice, setUnitPrice] = useState<NumericFieldValue>(90);
  return <TradeTicket
    selected={instrument}
    side="SELL"
    setSide={() => undefined}
    view="book"
    timeInForce="GTC"
    setTimeInForce={() => undefined}
    quantity={quantity}
    setQuantity={setQuantity}
    unitPrice={unitPrice}
    setUnitPrice={setUnitPrice}
    availableQuantity={1_000}
    availableRice={0}
    quote={undefined}
    quotePending={false}
    quoteError={null}
    busy={false}
    locked={false}
    confirmation={null}
    onCancel={() => undefined}
    onSubmit={() => undefined}
  />;
}

it("adds the same 10, 100, and 500 quantity shortcuts for sell orders", () => {
  render(<SellTicket />);

  expect(screen.queryByRole("button", { name: "1개" })).toBeNull();
  for (const label of ["10개", "100개", "500개"]) expect(screen.getByRole("button", { name: label })).toBeTruthy();

  fireEvent.click(screen.getByRole("button", { name: "10개" }));
  fireEvent.click(screen.getByRole("button", { name: "10개" }));
  fireEvent.click(screen.getByRole("button", { name: "100개" }));
  fireEvent.click(screen.getByRole("button", { name: "500개" }));

  expect((screen.getByRole("textbox", { name: "주문 수량" }) as HTMLInputElement).value).toBe("620");
});
