/*
 * Small line icons for the market.  They are drawn inline so the market can
 * share the cozy paper palette without waiting on new sprite work; item and
 * currency artwork still comes from the pixel assets in `marketArt`.
 */
import type { ReactElement } from "react";

export type PlazaIconName =
  | "sprout" | "cart" | "ledger" | "parcel" | "close" | "search" | "clock"
  | "pencil" | "trash" | "calendar" | "chevron" | "claim" | "bag" | "check" | "cross" | "hourglass" | "half"
  | "chart" | "list";

const PATHS: Record<PlazaIconName, ReactElement> = {
  sprout: <><path d="M12 21v-8" /><path d="M12 13c0-3.3-2.4-6-5.5-6C6.5 10.7 8.9 13 12 13Z" /><path d="M12 13c0-4 2.7-7 6-7 0 4-2.7 7-6 7Z" /></>,
  cart: <><circle cx="9.5" cy="19" r="1.4" /><circle cx="17.5" cy="19" r="1.4" /><path d="M3 4h2.2l2.4 10.2A2 2 0 0 0 9.5 16h8.2a2 2 0 0 0 2-1.6L21 8H6" /></>,
  ledger: <><rect x="5" y="3" width="14" height="18" rx="2" /><path d="M9 3v18" /><path d="M12.5 8h4M12.5 12h4M12.5 16h2.5" /></>,
  parcel: <><path d="m12 3 9 4.5v9L12 21l-9-4.5v-9L12 3Z" /><path d="M3 7.5 12 12l9-4.5M12 12v9" /></>,
  close: <><path d="m5 5 14 14M19 5 5 19" /></>,
  search: <><circle cx="10.5" cy="10.5" r="6" /><path d="m15 15 5 5" /></>,
  clock: <><circle cx="12" cy="12" r="8.5" /><path d="M12 7.5V12l3 2" /></>,
  pencil: <><path d="M4 20h4l10-10a2.8 2.8 0 0 0-4-4L4 16v4Z" /><path d="m13.5 6.5 4 4" /></>,
  trash: <><path d="M4 7h16" /><path d="M9 7V5h6v2" /><path d="M6.5 7 7.5 20h9L17.5 7" /><path d="M10.5 11v5M13.5 11v5" /></>,
  calendar: <><rect x="3.5" y="5" width="17" height="15" rx="2" /><path d="M3.5 10h17M8 3v4M16 3v4" /></>,
  chevron: <><path d="m9.5 5 7 7-7 7" /></>,
  claim: <><path d="M12 3.5v10" /><path d="m7.5 9 4.5 4.5L16.5 9" /><path d="M4.5 17v2.5h15V17" /></>,
  bag: <><path d="M4.5 8h15l-1.2 12H5.7L4.5 8Z" /><path d="M8.5 8V6a3.5 3.5 0 0 1 7 0v2" /></>,
  check: <><circle cx="12" cy="12" r="8.5" /><path d="m8 12 3 3 5-6" /></>,
  cross: <><circle cx="12" cy="12" r="8.5" /><path d="m9 9 6 6M15 9l-6 6" /></>,
  hourglass: <><path d="M7 3.5h10M7 20.5h10" /><path d="M8 3.5v3.2c0 2 4 3.6 4 5.3 0 1.7-4 3.3-4 5.3v3.2M16 3.5v3.2c0 2-4 3.6-4 5.3 0 1.7 4 3.3 4 5.3v3.2" /></>,
  half: <><circle cx="12" cy="12" r="8.5" /><path d="M12 3.5a8.5 8.5 0 0 1 0 17Z" fill="currentColor" stroke="none" /></>,
  chart: <><path d="M4 4v16h16" /><path d="m7.5 14.5 3.5-4.5 3 2.5 4.5-6.5" /></>,
  list: <><path d="M9 6h11M9 12h11M9 18h11" /><circle cx="5" cy="6" r="1.1" /><circle cx="5" cy="12" r="1.1" /><circle cx="5" cy="18" r="1.1" /></>,
};

export function PlazaIcon({ name, className }: { name: PlazaIconName; className?: string }) {
  return <svg className={`plaza-icon${className ? ` ${className}` : ""}`} viewBox="0 0 24 24" fill="none" stroke="currentColor" strokeWidth="1.9" strokeLinecap="round" strokeLinejoin="round" aria-hidden="true">{PATHS[name]}</svg>;
}
