export const MIN_MARKET_UNIT_PRICE = 10;
export const MAX_MARKET_UNIT_PRICE = 999_999;

export type NumericFieldValue = number | "";

export function numericInputValue(value: string, max: number): NumericFieldValue {
  if (value === "") return "";
  if (max < 1) return "";
  if (!/^\d+$/.test(value)) return "";
  const parsed = Number(value);
  return Number.isSafeInteger(parsed) ? Math.min(parsed, max) : max;
}

export function positiveInteger(value: NumericFieldValue): number | null {
  return typeof value === "number" && Number.isInteger(value) && value > 0 ? value : null;
}

export function marketUnitPrice(value: NumericFieldValue): number | null {
  const parsed = positiveInteger(value);
  return parsed !== null && parsed >= MIN_MARKET_UNIT_PRICE && parsed <= MAX_MARKET_UNIT_PRICE ? parsed : null;
}
