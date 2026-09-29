const MARKET_TIME_ZONE = "Asia/Seoul";
const MARKET_DATE_TIME_FORMAT = new Intl.DateTimeFormat("en-CA", {
  year: "numeric",
  month: "2-digit",
  day: "2-digit",
  hour: "2-digit",
  minute: "2-digit",
  hourCycle: "h23",
  timeZone: MARKET_TIME_ZONE,
});

export function formatMarketDateTime(value: string | number | Date | null): string {
  if (value === null) return "없음";
  const date = value instanceof Date ? value : new Date(typeof value === "number" ? value : value);
  if (Number.isNaN(date.getTime())) return "날짜 확인 중";
  const parts = Object.fromEntries(MARKET_DATE_TIME_FORMAT.formatToParts(date).map(part => [part.type, part.value]));
  return `${parts.year}-${parts.month}-${parts.day} ${parts.hour}:${parts.minute}`;
}
