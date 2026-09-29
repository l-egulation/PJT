// @vitest-environment node

import { readFileSync } from "node:fs";
import { resolve } from "node:path";
import { describe, expect, it } from "vitest";

describe("ChatPanel scalable paper frame", () => {
  it("keeps ornamental corners at a fixed size while only the center stretches", () => {
    const css = readFileSync(resolve(process.cwd(), "src/features/chat/ChatPanel.css"), "utf8");
    const frameRule = css.match(/\.chat-panel\.is-open::before \{[^}]+\}/)?.[0] ?? "";
    expect(frameRule).toContain("border-image-source");
    expect(frameRule).toContain("border-image-slice: 132 130 142 fill");
    expect(frameRule).not.toContain("background-size: 100% 100%");
  });

  it("renders desktop nicknames at one-and-a-half times the previous size", () => {
    const css = readFileSync(resolve(process.cwd(), "src/features/chat/ChatPanel.css"), "utf8");
    const nicknameRule = css.match(/\.chat-message-list strong \{[^}]+\}/)?.[0] ?? "";
    expect(nicknameRule).toContain("font-size: 15px");
  });
});
