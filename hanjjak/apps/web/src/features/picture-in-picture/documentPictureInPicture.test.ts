import { describe, expect, it, vi } from "vitest";
import { getPictureInPictureAvailability, openPictureInPictureDocument } from "./documentPictureInPicture";

function fakeWindow(overrides: Record<string, unknown> = {}): Window {
  return { isSecureContext: true, ...overrides } as unknown as Window;
}

describe("Document Picture-in-Picture adapter", () => {
  it("distinguishes insecure and unsupported environments", () => {
    expect(getPictureInPictureAvailability(fakeWindow({ isSecureContext: false }))).toEqual({ supported: false, reason: "insecure" });
    expect(getPictureInPictureAvailability(fakeWindow())).toEqual({ supported: false, reason: "unsupported" });
  });

  it("opens one document and prepares the portal mount", async () => {
    const mountNode = { id: "" } as HTMLElement;
    const appendBody = vi.fn();
    const targetDocument = {
      documentElement: { lang: "", className: "" },
      title: "",
      body: { className: "", append: appendBody },
      head: { append: vi.fn() },
      createElement: vi.fn(() => mountNode),
    } as unknown as Document;
    const pipWindow = { document: targetDocument } as Window;
    const requestWindow = vi.fn().mockResolvedValue(pipWindow);
    const sourceDocument = {
      documentElement: { lang: "ko" },
      title: "한짝",
      head: { querySelectorAll: vi.fn(() => []) },
    } as unknown as Document;
    const sourceWindow = fakeWindow({ document: sourceDocument, documentPictureInPicture: { window: null, requestWindow } });

    await expect(openPictureInPictureDocument(sourceWindow, { width: 320, height: 420 })).resolves.toEqual({ window: pipWindow, mountNode });
    expect(requestWindow).toHaveBeenCalledWith({ width: 320, height: 420, disallowReturnToOpener: false, preferInitialWindowPlacement: false });
    expect(targetDocument.documentElement.lang).toBe("ko");
    expect(targetDocument.body.className).toBe("picture-in-picture-document");
    expect(targetDocument.documentElement.className).toBe("picture-in-picture-root-document");
    expect(mountNode.id).toBe("picture-in-picture-root");
    expect(appendBody).toHaveBeenCalledWith(mountNode);
  });
});
