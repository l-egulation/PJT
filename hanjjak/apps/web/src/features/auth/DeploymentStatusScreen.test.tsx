import { renderToStaticMarkup } from "react-dom/server";
import { describe, expect, it } from "vitest";
import { DeploymentStatusScreen } from "./DeploymentStatusScreen";

describe("DeploymentStatusScreen", () => {
  it("explains that the service is being deployed", () => {
    const html = renderToStaticMarkup(<DeploymentStatusScreen />);

    expect(html).toContain("배포중입니다");
    expect(html).toContain("잠시 후 다시 접속해 주세요.");
    expect(html).toContain('role="status"');
  });
});
