import { describe, expect, it } from "vitest";
import { ApiError } from "@hanjjak/client-sdk";
import { adminAuthErrorFromHash, adminErrorMessage, defaultEconomyRange } from "./presentation";

describe("admin presentation", () => {
  it("maps permission failures without exposing server details", () => {
    expect(adminErrorMessage(new ApiError(403, "ADMIN_PERMISSION_DENIED", false))).toBe("이 화면을 조회할 권한이 없습니다.");
    expect(adminErrorMessage(new ApiError(500, "INTERNAL_SECRET_DETAIL", false))).toBe("요청을 처리하지 못했습니다. (INTERNAL_SECRET_DETAIL)");
  });

  it("maps OAuth callback fragments without exposing provider details", () => {
    expect(adminAuthErrorFromHash("#admin-auth-error=ADMIN_GITLAB_ACCESS_DENIED")).toBe("허용된 GitLab 계정이 아닙니다.");
    expect(adminAuthErrorFromHash("#other=value")).toBe("");
  });

  it("starts economy lookup at a seven day inclusive range", () => {
    expect(defaultEconomyRange(new Date("2026-09-09T12:00:00Z"))).toEqual({ from: "2026-09-03", to: "2026-09-09" });
  });
});
