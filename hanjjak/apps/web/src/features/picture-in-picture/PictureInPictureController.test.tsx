// @vitest-environment happy-dom
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { afterEach, beforeEach, describe, expect, it, vi } from "vitest";
import { usePictureInPictureSessionStore } from "./pictureInPictureSessionStore";

const adapter = vi.hoisted(() => ({
  availability: vi.fn(() => ({ supported: true as const })),
  open: vi.fn(),
}));

vi.mock("./documentPictureInPicture", () => ({
  getPictureInPictureAvailability: adapter.availability,
  openPictureInPictureDocument: adapter.open,
}));
vi.mock("../battle/PetBattleSurface", () => ({ PetBattleSurface: () => <div>PiP 전투</div> }));

import { PictureInPictureController } from "./PictureInPictureController";

function pipWindow() {
  const events = new EventTarget();
  return Object.assign(events, { closed: false, close: vi.fn() }) as unknown as Window;
}

beforeEach(() => {
  usePictureInPictureSessionStore.getState().stop();
  adapter.availability.mockReturnValue({ supported: true });
  adapter.open.mockReset();
});

afterEach(() => {
  cleanup();
  usePictureInPictureSessionStore.getState().stop();
});

describe("PictureInPictureController session lifecycle", () => {
  it("starts on open and stops on pagehide", async () => {
    const targetWindow = pipWindow();
    adapter.open.mockResolvedValue({ window: targetWindow, mountNode: document.createElement("div") });
    render(<PictureInPictureController />);

    fireEvent.click(screen.getByRole("button", { name: "화면 한켠에서 보기" }));
    await waitFor(() => expect(usePictureInPictureSessionStore.getState().active).toBe(true));

    targetWindow.dispatchEvent(new Event("pagehide"));
    await waitFor(() => expect(usePictureInPictureSessionStore.getState().active).toBe(false));
  });

  it("closes the document and resets counters on manual return", async () => {
    const targetWindow = pipWindow();
    adapter.open.mockResolvedValue({ window: targetWindow, mountNode: document.createElement("div") });
    render(<PictureInPictureController />);

    fireEvent.click(screen.getByRole("button", { name: "화면 한켠에서 보기" }));
    await screen.findByRole("button", { name: "메인 화면으로 돌아오기" });
    usePictureInPictureSessionStore.setState({ clearCount: 3 });
    fireEvent.click(screen.getByRole("button", { name: "메인 화면으로 돌아오기" }));

    expect(targetWindow.close).toHaveBeenCalledOnce();
    expect(usePictureInPictureSessionStore.getState()).toMatchObject({ active: false, clearCount: 0 });
  });

  it("does not start the session when opening fails", async () => {
    adapter.open.mockRejectedValue(new DOMException("denied", "NotAllowedError"));
    render(<PictureInPictureController />);

    fireEvent.click(screen.getByRole("button", { name: "화면 한켠에서 보기" }));
    await screen.findByRole("alert");

    expect(usePictureInPictureSessionStore.getState().active).toBe(false);
  });
});
