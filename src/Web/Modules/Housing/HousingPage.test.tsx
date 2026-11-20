import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { HousingPage } from "./HousingPage";

const t = i18n.getFixedT("pl", "housing");

const emptyList = { status: 200, body: { items: [], page: 1, pageSize: 50, totalCount: 0 } };

describe("HousingPage", () => {
  it("lists stored offers with a link to their details", async () => {
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
              createdAt: "2026-11-16T10:00:00+00:00",
              updatedAt: "2026-11-16T10:00:00+00:00",
              externalCreatedAt: "2026-11-16T09:00:00+00:00",
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
      "/offers/1",
    );
    expect(screen.getByRole("button", { name: t("list.next") })).toBeDisabled();
  });

  it("shows the place of an offer: town or village and how big", async () => {
    stubApi({
      "/api/housing/offers": {
        status: 200,
        body: {
          items: [
            {
              id: 1,
              source: "OtoDom",
              title: "Flat in Pruszków",
              url: "https://www.otodom.pl/pl/oferta/flat-ID1",
              createdAt: "2026-11-16T10:00:00+00:00",
              updatedAt: "2026-11-16T10:00:00+00:00",
              placeName: "Pruszków",
              placeKind: "Town",
              placeSize: "LargerTown",
              placeRole: "CountySeat",
              nearby: [
                { tier: "Metropolis", name: "Poznań", km: 82.4 },
                { tier: "BigCity", name: "Bydgoszcz", km: 89.1 },
              ],
              voivodeship: "Mazowieckie",
              population: 65283,
              populationYear: 2021,
              populationSource: "Wikidata",
            },
          ],
          page: 1,
          pageSize: 50,
          totalCount: 1,
        },
      },
    });

    renderWithProviders(<HousingPage />);

    const row = (await screen.findByRole("link", { name: "Flat in Pruszków" })).closest("tr")!;
    expect(row).toHaveTextContent(
      t("place.name", { name: "Pruszków", voivodeship: t("voivodeship.Mazowieckie") }),
    );
    expect(row).toHaveTextContent(t("place.size.LargerTown"));
    expect(row).toHaveTextContent(t("place.role.CountySeat"));
    expect(row).toHaveTextContent(t("place.nearestCity", { city: "Bydgoszcz", km: "89" }));
  });

  it("shows what the place import saved", async () => {
    stubApi({
      "/api/housing/offers": emptyList,
      "/api/housing/jobs/ImportPlaces/runs/latest": {
        status: 200,
        body: {
          run: {
            id: 1,
            job: "ImportPlaces",
            trigger: "Manual",
            status: "Succeeded",
            queuedAt: "2026-11-20T10:00:00+00:00",
            startedAt: "2026-11-20T10:00:00+00:00",
            finishedAt: "2026-11-20T10:01:30+00:00",
            errorCode: null,
            summary: {
              counts: {
                places: 65000,
                added: 65000,
                updated: 0,
                linkedOffers: 25757,
                unlinkedOffers: 8,
              },
            },
          },
          serverTime: "2026-11-20T10:02:00+00:00",
        },
      },
    });

    renderWithProviders(<HousingPage />);

    expect(await screen.findByRole("status")).toHaveTextContent(
      t("places.result", { places: 65000, added: 65000, updated: 0, linked: 25757, unlinked: 8 }),
    );
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
