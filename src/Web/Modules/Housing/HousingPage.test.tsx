import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { HousingPage } from "./HousingPage";

const t = i18n.getFixedT("pl", "housing");

const emptyList = { status: 200, body: { items: [], page: 1, pageSize: 50, totalCount: 0 } };

describe("HousingPage", () => {
  it("lists stored offers with a link to the portal", async () => {
    stubApi({
      "/api/housing/offers": {
        status: 200,
        body: {
          items: [
            {
              id: 1,
              source: "OtoDom",
              title: "Small flat",
              url: "https://www.otodom.pl/pl/oferta/small-ID1",
              price: 2200,
              priceCurrency: "PLN",
              areaM2: 30,
              rooms: 1,
              firstSeenAt: "2026-11-16T10:00:00+00:00",
              updatedAt: "2026-11-16T10:00:00+00:00",
            },
          ],
          page: 1,
          pageSize: 50,
          totalCount: 1,
        },
      },
    });

    renderWithProviders(<HousingPage />);

    expect(await screen.findByRole("link", { name: "Small flat" })).toHaveAttribute(
      "href",
      "https://www.otodom.pl/pl/oferta/small-ID1",
    );
    expect(screen.getByRole("button", { name: t("list.next") })).toBeDisabled();
  });

  it("tells the user when nothing is stored yet", async () => {
    stubApi({ "/api/housing/offers": emptyList });

    renderWithProviders(<HousingPage />);

    expect(await screen.findByText(t("list.empty"))).toBeInTheDocument();
  });

  it("shows what the import saved", async () => {
    stubApi({
      "/api/housing/offers": emptyList,
      "/api/housing/import/OtoDom": {
        status: 200,
        body: {
          source: "OtoDom",
          addedCount: 70,
          updatedCount: 1,
          unchangedCount: 1,
          skippedCount: 0,
        },
      },
    });
    renderWithProviders(<HousingPage />);

    await userEvent.click(screen.getByRole("button", { name: t("import.run") }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      t("import.result", { source: "OtoDom", added: 70, updated: 1, unchanged: 1 }),
    );
  });

  it.each([
    { status: 503, code: "scraping.vpnRequired", ns: "common", key: "errors.scraping.vpnRequired" },
    { status: 502, code: "housing.parseFailed", ns: "housing", key: "errors.parseFailed" },
  ] as const)("shows the translated import error for $code", async ({ status, code, ns, key }) => {
    stubApi({
      "/api/housing/offers": emptyList,
      "/api/housing/import/OtoDom": { status, body: { title: "Import failed", code } },
    });
    renderWithProviders(<HousingPage />);

    await userEvent.click(screen.getByRole("button", { name: t("import.run") }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      i18n.getResource("pl", ns, key) as string,
    );
  });

  it("shows what parsing the stored pages saved", async () => {
    stubApi({
      "/api/housing/offers": emptyList,
      "/api/housing/reparse": {
        status: 200,
        body: {
          captureCount: 3,
          failedCount: 1,
          addedCount: 2,
          updatedCount: 0,
          unchangedCount: 70,
          skippedCount: 0,
        },
      },
    });
    renderWithProviders(<HousingPage />);

    await userEvent.click(screen.getByRole("button", { name: t("reparse.run") }));

    expect(await screen.findByRole("status")).toHaveTextContent(t("reparse.failed", { count: 1 }));
  });
});
