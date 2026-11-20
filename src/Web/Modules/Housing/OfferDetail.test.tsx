import { screen } from "@testing-library/react";
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
});
