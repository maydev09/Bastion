import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { Route, Routes } from "react-router";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { OfferList } from "./OfferList";

// Inside its route, as in the app: links to an offer are relative to the list.
const renderList = (path: string) =>
  renderWithProviders(
    <Routes>
      <Route path="/housing/offers" element={<OfferList />} />
    </Routes>,
    path,
  );

const t = i18n.getFixedT("pl", "housing");
const common = i18n.getFixedT("pl", "common");

const emptyList = { status: 200, body: { items: [], page: 1, pageSize: 50, totalCount: 0 } };

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

describe("OfferList", () => {
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

    renderList("/housing/offers");

    expect(await screen.findByRole("link", { name: "Small flat" })).toHaveAttribute(
      "href",
      "/housing/offers/1",
    );
    expect(screen.getByRole("button", { name: common("table.next") })).toBeDisabled();
  });

  it("sorts by a column header: cheapest first, then dearest", async () => {
    const requests = stubApi({ "/api/housing/offers": longList });
    renderList("/housing/offers");

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
    renderList("/housing/offers");

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

    renderList(
      "/housing/offers?page=3&pageSize=25&sort=AreaM2&direction=Ascending&size=SmallTown&priceMax=3000&feature=Balcony&priceCut=true",
    );

    await screen.findByRole("link", { name: "Flat" });
    expect(lastQuery(requests)).toEqual({
      page: "3",
      pageSize: "25",
      sort: "AreaM2",
      direction: "Ascending",
      size: "SmallTown",
      priceMax: "3000",
      feature: "Balcony",
      priceCut: "true",
    });
    expect(screen.getByRole("checkbox", { name: t("place.size.SmallTown") })).toBeChecked();
  });

  it("narrows the list by a ticked filter, from the first page", async () => {
    const requests = stubApi({ "/api/housing/offers": longList });
    renderList("/housing/offers?page=3");

    await userEvent.click(
      await screen.findByRole("checkbox", { name: t("place.size.Metropolis") }),
    );
    expect(lastQuery(requests)).toMatchObject({ size: "Metropolis", page: "1" });

    await userEvent.click(screen.getByRole("button", { name: t("filters.clear") }));
    expect(lastQuery(requests)).not.toHaveProperty("size");
  });

  it("picks a whole category of amenities with one click", async () => {
    const requests = stubApi({
      "/api/housing/offers": longList,
      "/api/housing/features": {
        status: 200,
        body: [
          { feature: "Balcony", category: "Outdoor" },
          { feature: "Stove", category: "Appliances" },
          { feature: "Fridge", category: "Appliances" },
        ],
      },
    });
    renderList("/housing/offers");
    const category = await screen.findByRole("checkbox", {
      name: t("featureCategory.Appliances"),
    });

    await userEvent.click(category);
    const picked = () =>
      requests
        .filter((r) => r.pathname === "/api/housing/offers")
        .at(-1)!
        .searchParams.getAll("feature");
    expect(picked()).toEqual(["Stove", "Fridge"]);

    await userEvent.click(category);
    expect(picked()).toEqual([]);
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

    renderList("/housing/offers");

    const row = (await screen.findByRole("link", { name: "Flat in Pruszków" })).closest("tr")!;
    expect(row).toHaveTextContent(
      t("place.name", { name: "Pruszków", voivodeship: t("voivodeship.Mazowieckie") }),
    );
    expect(row).toHaveTextContent(t("place.size.LargerTown"));
    expect(row).toHaveTextContent(t("place.role.CountySeat"));
    expect(row).toHaveTextContent(t("place.nearestCity", { city: "Bydgoszcz", km: "89" }));
  });

  it("tells the user when nothing is stored yet", async () => {
    stubApi({ "/api/housing/offers": emptyList });

    renderList("/housing/offers");

    expect(await screen.findByText(t("list.empty"))).toBeInTheDocument();
  });
});
