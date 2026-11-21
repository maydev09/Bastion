import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { HousingPage } from "./HousingPage";

const t = i18n.getFixedT("pl", "housing");
const common = i18n.getFixedT("pl", "common");

const emptyList = { status: 200, body: { items: [], page: 1, pageSize: 50, totalCount: 0 } };

const openTools = () => userEvent.click(screen.getByRole("button", { name: t("tools.title") }));

// One offer on the page of a list with 260 offers: six pages of 50.
const longList = {
  status: 200,
  body: {
    items: [
      {
        id: 7,
        source: "OtoDom",
        title: "Flat",
        url: "https://www.otodom.pl/pl/oferta/flat-ID7",
        createdAt: "2026-11-16T10:00:00+00:00",
        updatedAt: "2026-11-16T10:00:00+00:00",
      },
    ],
    page: 1,
    pageSize: 50,
    totalCount: 260,
  },
};

const lastQuery = (requests: URL[]) =>
  Object.fromEntries(
    requests.filter((r) => r.pathname === "/api/housing/offers").at(-1)!.searchParams,
  );

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
    expect(screen.getByRole("button", { name: common("table.next") })).toBeDisabled();
  });

  it("sorts by a column header: cheapest first, then dearest", async () => {
    const requests = stubApi({ "/api/housing/offers": longList });
    renderWithProviders(<HousingPage />);

    await userEvent.click(await screen.findByRole("button", { name: t("list.price") }));
    expect(lastQuery(requests)).toMatchObject({ sort: "Price", direction: "Ascending", page: "1" });

    await userEvent.click(screen.getByRole("button", { name: t("list.price") }));
    expect(lastQuery(requests)).toMatchObject({ sort: "Price", direction: "Descending" });
    expect(screen.getByRole("columnheader", { name: t("list.price") })).toHaveAttribute(
      "aria-sort",
      "descending",
    );
  });

  it("jumps to a typed page and to the last page", async () => {
    const requests = stubApi({ "/api/housing/offers": longList });
    renderWithProviders(<HousingPage />);

    const pageNumber = await screen.findByRole("textbox", { name: common("table.page") });
    await userEvent.clear(pageNumber);
    await userEvent.type(pageNumber, "4{Enter}");
    expect(lastQuery(requests)).toMatchObject({ page: "4" });

    await userEvent.click(screen.getByRole("button", { name: common("table.last") }));
    expect(lastQuery(requests)).toMatchObject({ page: "6" });
    expect(screen.getByRole("button", { name: common("table.first") })).toBeEnabled();
  });

  it("opens the view a copied link describes", async () => {
    const requests = stubApi({ "/api/housing/offers": longList });

    renderWithProviders(<HousingPage />, "/?page=3&pageSize=25&sort=AreaM2&direction=Ascending");

    await screen.findByRole("link", { name: "Flat" });
    expect(lastQuery(requests)).toEqual({
      page: "3",
      pageSize: "25",
      sort: "AreaM2",
      direction: "Ascending",
    });
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

    await openTools();
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

    await openTools();
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

    await openTools();
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

    await openTools();
    await userEvent.click(screen.getByRole("button", { name: t("reparse.run") }));

    expect(await screen.findByRole("status")).toHaveTextContent(t("reparse.failed", { count: 1 }));
  });
});
