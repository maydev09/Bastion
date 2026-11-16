import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { FlatOffersPage } from "./FlatOffersPage";

const t = i18n.getFixedT("pl", "flatOffers");

const emptyList = { status: 200, body: { items: [], page: 1, pageSize: 50, totalCount: 0 } };

describe("FlatOffersPage", () => {
  it("lists stored offers with a link to the portal", async () => {
    stubApi({
      "/api/flat-offers/offers": {
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

    renderWithProviders(<FlatOffersPage />);

    expect(await screen.findByRole("link", { name: "Small flat" })).toHaveAttribute(
      "href",
      "https://www.otodom.pl/pl/oferta/small-ID1",
    );
    expect(screen.getByRole("button", { name: t("list.next") })).toBeDisabled();
  });

  it("tells the user when nothing is stored yet", async () => {
    stubApi({ "/api/flat-offers/offers": emptyList });

    renderWithProviders(<FlatOffersPage />);

    expect(await screen.findByText(t("list.empty"))).toBeInTheDocument();
  });

  it("shows what the import saved", async () => {
    stubApi({
      "/api/flat-offers/offers": emptyList,
      "/api/flat-offers/import": {
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
    renderWithProviders(<FlatOffersPage />);

    await userEvent.click(screen.getByRole("button", { name: t("import.run") }));

    expect(await screen.findByRole("status")).toHaveTextContent(
      t("import.result", { source: "OtoDom", added: 70, updated: 1, unchanged: 1 }),
    );
  });

  it.each([
    { status: 503, code: "scraping.vpnRequired", ns: "common" },
    { status: 502, code: "flatOffers.parseFailed", ns: "flatOffers" },
  ] as const)("shows the translated import error for $code", async ({ status, code, ns }) => {
    stubApi({
      "/api/flat-offers/offers": emptyList,
      "/api/flat-offers/import": { status, body: { title: "Import failed", code } },
    });
    renderWithProviders(<FlatOffersPage />);

    await userEvent.click(screen.getByRole("button", { name: t("import.run") }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      i18n.getResource("pl", ns, `errors.${code}`) as string,
    );
  });
});
