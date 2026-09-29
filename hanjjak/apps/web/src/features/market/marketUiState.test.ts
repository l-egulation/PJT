import { describe, expect, it } from "vitest";
import { readMarketUiState } from "./marketUiState";

describe("marketplace UI persistence", () => {
  it("restores selected material and command inputs after refresh", () => {
    const storage = { getItem: () => JSON.stringify({ selectedItemId: "POTATO_M1", sellQuantity: 5, sellUnitPrice: 230, purchaseQuantity: 9 }) };
    expect(readMarketUiState(storage)).toEqual({ selectedItemId: "POTATO_M1", sellQuantity: 5, sellUnitPrice: 230, purchaseQuantity: 9, boardView: "list" });
  });

  it("uses safe defaults for corrupt or invalid values", () => {
    expect(readMarketUiState({ getItem: () => "broken" })).toEqual({ selectedItemId: null, sellQuantity: 1, sellUnitPrice: 10, purchaseQuantity: 1, boardView: "list" });
    expect(readMarketUiState({ getItem: () => JSON.stringify({ selectedItemId: 1, sellQuantity: 0, sellUnitPrice: -2, purchaseQuantity: 1.5 }) })).toEqual({ selectedItemId: null, sellQuantity: 1, sellUnitPrice: 10, purchaseQuantity: 1, boardView: "list" });
  });

  it("opens on the plain list unless the player left it on the chart", () => {
    expect(readMarketUiState({ getItem: () => JSON.stringify({ boardView: "chart" }) }).boardView).toBe("chart");
    expect(readMarketUiState({ getItem: () => JSON.stringify({ boardView: "ladder" }) }).boardView).toBe("list");
  });

  it("keeps cleared inputs empty while the user replaces them", () => {
    expect(readMarketUiState({ getItem: () => JSON.stringify({ selectedItemId: "POTATO_M1", sellQuantity: "", sellUnitPrice: "", purchaseQuantity: "" }) })).toEqual({ selectedItemId: "POTATO_M1", sellQuantity: "", sellUnitPrice: "", purchaseQuantity: "", boardView: "list" });
  });
});
