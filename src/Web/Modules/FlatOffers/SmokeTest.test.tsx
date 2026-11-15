import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { SmokeTest } from "./SmokeTest";

async function runSmokeTest(response: { status: number; body: unknown }) {
  stubApi({ "/api/flat-offers/smoke-test": response });
  renderWithProviders(<SmokeTest />);
  await userEvent.click(screen.getByRole("button"));
}

describe("SmokeTest", () => {
  it("shows how many offers were read", async () => {
    await runSmokeTest({
      status: 200,
      body: { source: "OtoDom", offerCount: 73, skippedCount: 0 },
    });

    expect(await screen.findByRole("status")).toHaveTextContent(
      i18n.t("smokeTest.result", { ns: "flatOffers", source: "OtoDom", count: 73 }),
    );
  });

  it.each([
    { status: 503, code: "scraping.vpnRequired", ns: "common" },
    { status: 502, code: "flatOffers.parseFailed", ns: "flatOffers" },
  ] as const)("shows the translated message for $code", async ({ status, code, ns }) => {
    await runSmokeTest({ status, body: { title: "Smoke test failed", code } });

    expect(await screen.findByRole("alert")).toHaveTextContent(
      i18n.getResource("pl", ns, `errors.${code}`) as string,
    );
  });
});
