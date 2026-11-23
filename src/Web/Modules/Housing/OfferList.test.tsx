import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { Route, Routes } from "react-router";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { OfferList } from "./OfferList";

// Inside its route, as in the app: links to an offer are relative to the list. The card is a stand-in.
const renderList = (path: string) =>
  renderWithProviders(
    <Routes>
      <Route path="/housing/offers" element={<OfferList />} />
      <Route path="/housing/offers/:id" element={<p>offer card</p>} />
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
      "/housing/offers?page=3&pageSize=25&sort=AreaM2&direction=Ascending&size=SmallTown&role=MunicipalitySeat&priceMax=3000&pricePerM2Max=55.5&feature=Balcony&priceCut=true",
    );

    await screen.findByRole("link", { name: "Flat" });
    expect(lastQuery(requests)).toEqual({
      page: "3",
      pageSize: "25",
      sort: "AreaM2",
      direction: "Ascending",
      size: "SmallTown",
      role: "MunicipalitySeat",
      priceMax: "3000",
      pricePerM2Max: "55.5",
      feature: "Balcony",
      priceCut: "true",
    });
    expect(screen.getByRole("checkbox", { name: t("place.size.SmallTown") })).toBeChecked();
    expect(screen.getByRole("checkbox", { name: t("place.role.MunicipalitySeat") })).toBeChecked();
    expect(
      screen.getByRole("textbox", { name: t("filters.to", { what: t("filters.pricePerM2") }) }),
    ).toHaveValue("55,5");
  });

  it("shows the populations of each place size under the configured thresholds", async () => {
    stubApi({
      "/api/housing/offers": longList,
      "/api/housing/place-sizes": {
        status: 200,
        body: [
          { size: "SmallVillage", kind: "Village", minPopulation: 0, maxPopulation: 300 },
          { size: "SmallTown", kind: "Town", minPopulation: 5000, maxPopulation: 20000 },
          { size: "Metropolis", kind: "Town", minPopulation: 250000, maxPopulation: null },
        ],
      },
    });
    const number = new Intl.NumberFormat("pl").format;
    renderList("/housing/offers");

    await userEvent.click(await screen.findByRole("button", { name: t("filters.size") }));

    const size = (name: string, population: string) => `${name} ${population}`;
    expect(
      await screen.findByRole("checkbox", {
        name: size(t("place.size.SmallVillage"), t("filters.populationBelow", { to: number(300) })),
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("checkbox", {
        name: size(
          t("place.size.SmallTown"),
          t("filters.populationRange", { from: number(5000), to: number(20000) }),
        ),
      }),
    ).toBeInTheDocument();
    expect(
      screen.getByRole("checkbox", {
        name: size(
          t("place.size.Metropolis"),
          t("filters.populationFrom", { from: number(250000) }),
        ),
      }),
    ).toBeInTheDocument();
  });

  it("narrows the list by a ticked filter, from the first page", async () => {
    const requests = stubApi({ "/api/housing/offers": longList });
    renderList("/housing/offers?page=3");

    // Only Location and Price are open at first.
    await userEvent.click(await screen.findByRole("button", { name: t("filters.size") }));
    await userEvent.click(screen.getByRole("checkbox", { name: t("place.size.Metropolis") }));
    expect(lastQuery(requests)).toMatchObject({ size: "Metropolis", page: "1" });

    await userEvent.click(screen.getByRole("button", { name: t("filters.clear") }));
    expect(lastQuery(requests)).not.toHaveProperty("size");
  });

  it("picks any of the room counts, the last one with more", async () => {
    const requests = stubApi({ "/api/housing/offers": longList });
    renderList("/housing/offers");
    // The table has a "Pokoje" header too, so the section is looked up in the filter panel.
    const filters = within(await screen.findByRole("complementary", { name: t("filters.title") }));
    await userEvent.click(filters.getByRole("button", { name: t("filters.rooms") }));
    const rooms = within(screen.getByRole("group", { name: t("filters.rooms") }));

    await userEvent.click(rooms.getByRole("button", { name: "1" }));
    await userEvent.click(rooms.getByRole("button", { name: t("filters.orMore", { count: 4 }) }));
    const offers = requests.filter((r) => r.pathname === "/api/housing/offers");
    expect(offers.at(-1)!.searchParams.getAll("rooms")).toEqual(["1"]);
    expect(lastQuery(requests)).toMatchObject({ roomsMin: "4" });
    expect(rooms.getByRole("button", { name: "2" })).toHaveAttribute("aria-pressed", "false");
  });

  it("shows the offers found since the last visit", async () => {
    localStorage.setItem("housing.lastVisit", "2026-11-22T06:00:00.000Z");
    const requests = stubApi({ "/api/housing/offers": longList });
    renderList("/housing/offers");

    await userEvent.click(
      await screen.findByRole("button", { name: new RegExp(t("filters.sinceVisit")) }),
    );
    expect(lastQuery(requests)).toMatchObject({ addedSince: "2026-11-22T06:00:00.000Z" });
  });

  it("opens the offer marked with the arrow keys on Enter", async () => {
    stubApi({ "/api/housing/offers": longList });
    renderList("/housing/offers");
    await screen.findByRole("link", { name: "Flat" });

    await userEvent.keyboard("{ArrowDown}{Enter}");

    expect(await screen.findByText("offer card")).toBeInTheDocument();
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
    await userEvent.click(await screen.findByRole("button", { name: t("filters.features") }));
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

  it("shows the place of an offer, how big it is and its role", async () => {
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

    // Population and the cities around are on the offer card.
    const row = (await screen.findByRole("link", { name: "Flat in Pruszków" })).closest("tr")!;
    expect(row).toHaveTextContent("Pruszków");
    expect(row).toHaveTextContent(t("place.size.LargerTown"));
    expect(row).toHaveTextContent(t("place.roleShort.CountySeat"));
  });

  it("tells the user when nothing is stored yet", async () => {
    stubApi({ "/api/housing/offers": emptyList });

    renderList("/housing/offers");

    expect(await screen.findByText(t("list.empty"))).toBeInTheDocument();
  });
});
