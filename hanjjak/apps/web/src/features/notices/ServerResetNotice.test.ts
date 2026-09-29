import { expect, it } from "vitest";
import { markServerResetNoticeSeen, serverResetNoticeDue } from "./ServerResetNotice";

function memoryStorage(initial: Record<string, string> = {}) {
  const values = { ...initial };
  return {
    getItem: (key: string) => values[key] ?? null,
    setItem: (key: string, value: string) => { values[key] = value; },
    values,
  };
}

it("shows the notice to someone who has never closed it", () => {
  expect(serverResetNoticeDue(memoryStorage())).toBe(true);
});

/* 이번 접속에서 닫았으면 접어 둔다. 다음 접속은 빈 저장소로 시작하므로 다시 뜬다. */
it("keeps the notice away for the rest of the visit once it is closed", () => {
  const storage = memoryStorage();
  markServerResetNoticeSeen(storage, 10_000_000);

  expect(serverResetNoticeDue(storage)).toBe(false);
  expect(serverResetNoticeDue(memoryStorage())).toBe(true);
});

/* 시크릿 창은 저장소를 읽기만 해도 던진다. 그때는 안내를 못 본 것으로 보고 띄운다. */
it("shows the notice when storage cannot be read at all", () => {
  const blocked = { getItem() { throw new Error("blocked"); } };
  expect(serverResetNoticeDue(blocked)).toBe(true);
});
