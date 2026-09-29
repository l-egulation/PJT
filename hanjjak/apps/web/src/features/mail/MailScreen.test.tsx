// @vitest-environment happy-dom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { MailScreen, claimableRice, giftMailOnly, sortMailForReading, type MailDataSource } from "./MailScreen";
import { mailDateLabel, mailTypeLabel, type MailMessage } from "./api";

afterEach(cleanup);

const mail = (mailId: string, riceAmount: number, claimed: boolean, createdAt: string, type = "OPERATOR_GIFT"): MailMessage =>
  ({ mailId, type, riceAmount, claimed, createdAt, claimedAt: claimed ? createdAt : null });

const mails = [
  mail("old-unclaimed", 100, false, "2026-09-10T00:00:00Z"),
  mail("claimed", 999, true, "2026-09-13T00:00:00Z"),
  mail("new-unclaimed", 250, false, "2026-09-12T00:00:00Z"),
];

function renderScreen(overrides: Partial<MailDataSource> = {}) {
  const dataSource: MailDataSource = {
    list: vi.fn(async () => ({ items: mails, nextCursor: null, totalItems: mails.length })),
    claim: vi.fn(async (mailId: string) => ({ commandId: "c", idempotencyKey: "k", status: "SUCCEEDED", result: { mailId, riceAmount: 0, claimedAt: "", walletBalance: 0 } })),
    claimAll: vi.fn(async () => ({ commandId: "c", idempotencyKey: "k", status: "SUCCEEDED", result: { claimedMailIds: [], totalRiceAmount: 0, walletBalance: 0 } })),
    ...overrides,
  };
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  render(<QueryClientProvider client={client}><MailScreen dataSource={dataSource} /></QueryClientProvider>);
  return dataSource;
}

/** 메시지함은 알림 탭으로 열린다. 우편 줄을 보려면 메시지 탭으로 넘어간다. */
function openMailTab() {
  fireEvent.click(screen.getByRole("button", { name: /^메시지/ }));
}

describe("sortMailForReading", () => {
  /* 받을 것이 먼저다. 받은 것은 기록으로만 남아 아래로 내려간다. */
  it("puts what can still be claimed first, newest of those on top", () => {
    expect(sortMailForReading(mails).map((item) => item.mailId)).toEqual(["new-unclaimed", "old-unclaimed", "claimed"]);
  });
});

describe("giftMailOnly", () => {
  it("leaves market settlements to the market inbox", () => {
    const settlement = mail("settlement", 10, false, "2026-09-15T00:00:00Z", "MARKET_SETTLEMENT");
    expect(giftMailOnly([...mails, settlement]).map((item) => item.mailId)).toEqual(mails.map((item) => item.mailId));
  });
});

describe("claimableRice", () => {
  it("counts only what has not been taken yet", () => {
    expect(claimableRice(mails)).toBe(350);
  });
});

describe("mailTypeLabel", () => {
  it("never leaves the line blank for a type it does not know", () => {
    expect(mailTypeLabel("MARKET_SETTLEMENT")).toBe("거래소 판매 정산");
    expect(mailTypeLabel("SOMETHING_NEW")).toBe("보관함 지급");
  });
});

describe("mailDateLabel", () => {
  it("reads as a date, and stays empty rather than printing nonsense", () => {
    expect(mailDateLabel("2026-09-12T00:00:00Z")).toMatch(/9월 1[12]일/);
    expect(mailDateLabel("어제")).toBe("");
  });
});

describe("MailScreen", () => {
  /* 알림이 먼저다. 열자마자 우편 줄이 쏟아지지 않는다. */
  it("opens on 알림 and only shows the mail rows once 메시지 is picked", async () => {
    renderScreen();
    expect(screen.getByText("아직 도착한 알림이 없어요.")).toBeTruthy();
    expect(document.querySelector(".mail-list")).toBeNull();

    openMailTab();
    await screen.findAllByText("보관함 지급");
    expect(document.querySelector(".mail-list")).toBeTruthy();
  });

  /* 받을 것이 있으면 탭 위에 통수를 얹어 둔다. */
  it("counts what is still waiting on the 메시지 tab", async () => {
    renderScreen();
    await waitFor(() => expect(document.querySelector(".mail-sidebar em")?.textContent).toBe("2"));
  });

  it("shows what is waiting and hands each claim to the server", async () => {
    const dataSource = renderScreen();
    openMailTab();
    await screen.findAllByText("보관함 지급");

    expect(screen.getByText("350")).toBeTruthy();
    /* "모두 받기"도 받기로 끝난다. 줄마다 붙은 단추만 센다. */
    expect(document.querySelectorAll(".mail-list li button")).toHaveLength(2);
    expect(screen.getByText("받음")).toBeTruthy();

    fireEvent.click(screen.getByRole("button", { name: "보관함 지급 250쌀 받기" }));
    await waitFor(() => expect(dataSource.claim).toHaveBeenCalledWith("new-unclaimed", expect.any(String)));
  });

  it("keeps 모두 받기 out of reach when there is nothing left to take", async () => {
    renderScreen({ list: vi.fn(async () => ({ items: [mail("done", 10, true, "2026-09-10T00:00:00Z")], nextCursor: null, totalItems: 1 })) });
    openMailTab();
    await screen.findByText("받음");
    expect((screen.getByRole("button", { name: "모두 받기" }) as HTMLButtonElement).disabled).toBe(true);
  });

  it("says so plainly when the mailbox is empty", async () => {
    renderScreen({ list: vi.fn(async () => ({ items: [], nextCursor: null, totalItems: 0 })) });
    openMailTab();
    expect(await screen.findByText("아직 도착한 우편이 없어요.")).toBeTruthy();
  });

  /* 서버가 막히면 화면 맨 위 한 줄로만 알린다. 목록이 밀려나지 않는다. */
  it("reports a failed claim at the top of the screen", async () => {
    renderScreen({ claim: vi.fn(async () => { throw new Error("MAIL_NOT_CLAIMABLE"); }) });
    openMailTab();
    await screen.findAllByText("보관함 지급");

    fireEvent.click(screen.getByRole("button", { name: "보관함 지급 250쌀 받기" }));
    await waitFor(() => expect(document.querySelector(".top-alert")?.textContent).toContain("우편을 받지 못했어요."));
  });
});
