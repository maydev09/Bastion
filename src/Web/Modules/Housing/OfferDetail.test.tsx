import { screen, within } from "@testing-library/react";
import i18n from "i18next";
import { Route, Routes } from "react-router";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { OfferDetail } from "./OfferDetail";

const t = i18n.getFixedT("pl", "housing");

function renderDetail() {
  renderWithProviders(
    <Routes>
      <Route path="/housing/offers/:id" element={<OfferDetail />} />
    </Routes>,
    "/housing/offers/1",
  );
}

describe("OfferDetail", () => {
  it("shows the place and the nearest bigger places, one row per place", async () => {
    stubApi({
      "/api/housing/offers/1": {
        status: 200,
        body: {
          id: 1,
          source: "OtoDom",
          title: "Flat in Złotów",
          url: "https://www.otodom.pl/pl/oferta/flat-ID1",
          propertyType: "Flat",
          transaction: "Rent",
          price: 1800,
          priceCurrency: "PLN",
          createdAt: "2026-11-16T10:00:00+00:00",
          updatedAt: "2026-11-16T10:00:00+00:00",
          placeName: "Złotów",
          placeKind: "Town",
          placeSize: "SmallTown",
          placeRole: "CountySeat",
          voivodeship: "Wielkopolskie",
          county: "złotowski",
          population: 18768,
          populationYear: 2021,
          populationSource: "Wikidata",
          nearby: [
            { tier: "VoivodeshipCapital", name: "Poznań", km: 106.1 },
            { tier: "Metropolis", name: "Poznań", km: 106.1 },
            { tier: "BigCity", name: "Bydgoszcz", km: 69.3 },
          ],
        },
      },
    });

    renderDetail();

    expect(await screen.findByRole("heading", { name: "Flat in Złotów" })).toBeInTheDocument();
    expect(screen.getByRole("link", { name: /OtoDom/ })).toHaveAttribute(
      "href",
      "https://www.otodom.pl/pl/oferta/flat-ID1",
    );
    expect(screen.getByText(t("place.size.SmallTown"))).toBeInTheDocument();
    expect(screen.getByText(t("place.role.CountySeat"))).toBeInTheDocument();
    const rows = screen.getAllByRole("listitem").map((li) => li.textContent);
    expect(rows).toHaveLength(2);
    expect(rows[0]).toContain("Bydgoszcz");
    expect(rows[1]).toContain(t("detail.tier.VoivodeshipCapital"));
    expect(rows[1]).toContain(t("detail.tier.Metropolis"));
  });

  it("says everything is in place when the place meets every tier", async () => {
    stubApi({
      "/api/housing/offers/1": {
        status: 200,
        body: {
          id: 1,
          source: "OtoDom",
          title: "Flat in Warszawa",
          url: "https://www.otodom.pl/pl/oferta/flat-ID1",
          propertyType: "Flat",
          transaction: "Rent",
          createdAt: "2026-11-16T10:00:00+00:00",
          updatedAt: "2026-11-16T10:00:00+00:00",
          placeName: "Warszawa",
          placeKind: "Town",
          placeSize: "Metropolis",
          placeRole: "VoivodeshipCapital",
          nearby: [],
        },
      },
    });

    renderDetail();

    expect(
      await screen.findByText(
        t("detail.selfSufficient", {
          what: `${t("place.role.VoivodeshipCapital")}, ${t("place.size.Metropolis")}`,
        }),
      ),
    ).toBeInTheDocument();
  });

  it("tells the user when the offer does not exist", async () => {
    stubApi({});

    renderDetail();

    expect(await screen.findByText(t("detail.notFound"))).toBeInTheDocument();
  });

  it("shows the details, with the description as plain text", async () => {
    const offer = {
      id: 1,
      source: "OtoDom",
      title: "Flat in Gdańsk",
      url: "https://www.otodom.pl/pl/oferta/flat-ID1",
      propertyType: "Flat",
      transaction: "Rent",
      createdAt: "2026-11-16T10:00:00+00:00",
      updatedAt: "2026-11-16T10:00:00+00:00",
      nearby: [],
    };
    stubApi({
      "/api/housing/offers/1": {
        status: 200,
        body: {
          ...offer,
          floor: 2,
          buildingFloors: 5,
          buildingType: "Block",
          heating: "Gas",
          features: ["Balcony", "Lift"],
          latitude: 54.41982,
          longitude: 18.60517,
          locationRadiusM: 0,
          description: "<p>Quiet flat.</p><script>alert(1)</script><p>Pets welcome.</p>",
          detailsFetchedAt: "2026-11-20T10:00:00+00:00",
        },
      },
    });

    renderDetail();

    expect(await screen.findByText(t("detail.feature.Balcony"))).toBeInTheDocument();
    expect(screen.getByText(t("detail.feature.Lift"))).toBeInTheDocument();
    expect(screen.getByText(t("detail.floorOf", { floor: "2", floors: 5 }))).toBeInTheDocument();
    expect(screen.getByText(/Quiet flat\.\s+Pets welcome\./)).toBeInTheDocument();
    expect(screen.getByRole("link", { name: new RegExp(t("detail.map")) })).toHaveAttribute(
      "href",
      expect.stringContaining("mlat=54.41982"),
    );
    expect(screen.queryByText(/alert/)).not.toBeInTheDocument();
  });

  it("says when the details are not fetched yet", async () => {
    stubApi({
      "/api/housing/offers/1": {
        status: 200,
        body: {
          id: 1,
          source: "OtoDom",
          title: "Flat in Gdańsk",
          url: "https://www.otodom.pl/pl/oferta/flat-ID1",
          propertyType: "Flat",
          transaction: "Rent",
          createdAt: "2026-11-16T10:00:00+00:00",
          updatedAt: "2026-11-16T10:00:00+00:00",
          nearby: [],
        },
      },
    });

    renderDetail();

    expect(await screen.findByText(t("detail.notFetched"))).toBeInTheDocument();
    expect(screen.getByText(t("changes.none"))).toBeInTheDocument();
  });

  it("shows the changes newest first with translated fields and values", async () => {
    stubApi({
      "/api/housing/offers/1": {
        status: 200,
        body: {
          id: 1,
          source: "OtoDom",
          title: "Flat in Gdańsk",
          url: "https://www.otodom.pl/pl/oferta/flat-ID1",
          propertyType: "Flat",
          transaction: "Rent",
          price: 2000,
          priceCurrency: "PLN",
          createdAt: "2026-11-16T10:00:00+00:00",
          updatedAt: "2026-11-21T10:00:00+00:00",
          nearby: [],
          changes: [
            {
              id: 2,
              changedAt: "2026-11-21T10:00:00+00:00",
              changes: [
                { field: "features", before: ["Lift"], after: ["Balcony"] },
                { field: "heating", before: "Gas", after: "District" },
              ],
            },
            {
              id: 1,
              changedAt: "2026-11-20T10:00:00+00:00",
              changes: [{ field: "price", before: 2200, after: 2000 }],
            },
          ],
        },
      },
    });

    renderDetail();

    const history = await screen.findByRole("region", { name: t("changes.title") });
    const days = within(history)
      .getAllByRole("time")
      .map((time) => time.getAttribute("dateTime"));
    expect(days).toEqual(["2026-11-21T10:00:00+00:00", "2026-11-20T10:00:00+00:00"]);
    expect(within(history).getByText(t("changes.field.heating"))).toBeInTheDocument();
    expect(within(history).getByText(t("detail.heating.District"))).toBeInTheDocument();
    expect(history).toHaveTextContent(t("changes.added", { list: t("detail.feature.Balcony") }));
    expect(history).toHaveTextContent(t("changes.removed", { list: t("detail.feature.Lift") }));
    expect(within(history).getByText(t("changes.field.price"))).toBeInTheDocument();
  });
});
