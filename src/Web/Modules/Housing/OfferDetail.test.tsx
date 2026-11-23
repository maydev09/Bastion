import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { Route, Routes } from "react-router";
import { describe, expect, it, vi } from "vitest";
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
    const nearby = screen.getByRole("region", { name: t("detail.nearby") });
    const rows = within(nearby)
      .getAllByRole("listitem")
      .map((li) => li.textContent);
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

  it("shows the details, with the description's formatting but never its code or links", async () => {
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
          furnished: "No",
          latitude: 54.41982,
          longitude: 18.60517,
          locationRadiusM: 0,
          description:
            '<p>Quiet flat.</p><script>alert(1)</script><p><b>Pets</b> welcome.</p><ul><li>Lift</li></ul><a href="https://example.com">Call us</a>',
          detailsFetchedAt: "2026-11-20T10:00:00+00:00",
        },
      },
      "/api/housing/features": {
        status: 200,
        body: [
          { feature: "Balcony", category: "Outdoor" },
          { feature: "Lift", category: "Building" },
          { feature: "Garden", category: "Outdoor" },
        ],
      },
    });

    renderDetail();

    // Amenities are grouped by category, and furnished is an attribute with a "no".
    expect(await screen.findByText(t("detail.feature.Balcony"))).toBeInTheDocument();
    expect(screen.getByText(t("featureCategory.Building"))).toBeInTheDocument();
    expect(screen.getByText(t("detail.feature.Lift"))).toBeInTheDocument();
    expect(screen.queryByText(t("detail.feature.Garden"))).not.toBeInTheDocument();
    expect(screen.getByText(t("answer.No"))).toBeInTheDocument();
    expect(screen.getByText(t("detail.floorOf", { floor: "2", floors: 5 }))).toBeInTheDocument();
    expect(screen.getByText("Quiet flat.")).toBeInTheDocument();
    expect(screen.getByText("Pets").tagName).toBe("STRONG");
    expect(screen.getByRole("listitem")).toHaveTextContent("Lift");
    expect(screen.getByText("Call us")).not.toHaveAttribute("href");
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

    expect((await screen.findAllByText(t("detail.notFetched"))).length).toBeGreaterThan(0);
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

    // The history is open at first: its table is on the card.
    const history = await screen.findByRole("table");
    const days = within(history)
      .getAllByRole("time")
      .map((time) => time.getAttribute("dateTime"));
    expect(days).toEqual(["2026-11-21T10:00:00+00:00", "2026-11-20T10:00:00+00:00"]);
    expect(within(history).getByText(t("field.heating"))).toBeInTheDocument();
    expect(within(history).getByText(t("detail.heating.District"))).toBeInTheDocument();
    expect(history).toHaveTextContent(t("changes.added", { list: t("detail.feature.Balcony") }));
    expect(history).toHaveTextContent(t("changes.removed", { list: t("detail.feature.Lift") }));
    expect(within(history).getByText(t("field.price"))).toBeInTheDocument();
  });

  it("moves to the next offer of the list it was opened from", async () => {
    const offer = (id: number, title: string) => ({
      id,
      source: "OtoDom",
      title,
      url: `https://www.otodom.pl/pl/oferta/flat-ID${id}`,
      propertyType: "Flat",
      transaction: "Rent",
      createdAt: "2026-11-16T10:00:00+00:00",
      updatedAt: "2026-11-16T10:00:00+00:00",
    });
    stubApi({
      "/api/housing/offers": {
        status: 200,
        body: {
          items: [offer(1, "First"), offer(2, "Second")],
          page: 1,
          pageSize: 50,
          totalCount: 2,
        },
      },
      "/api/housing/offers/1": { status: 200, body: offer(1, "First") },
      "/api/housing/offers/2": { status: 200, body: offer(2, "Second") },
    });
    renderWithProviders(
      <Routes>
        <Route path="/housing/offers/:id" element={<OfferDetail />} />
      </Routes>,
      { pathname: "/housing/offers/1", state: { from: "?sort=Price" } },
    );
    await screen.findByRole("heading", { name: "First" });
    await screen.findByRole("button", { name: t("detail.next") });
    // The list behind the card loads on its own; the move waits for it.
    await vi.waitFor(() =>
      expect(screen.getByRole("button", { name: t("detail.next") })).toBeEnabled(),
    );

    await userEvent.keyboard("{ArrowRight}");

    expect(await screen.findByRole("heading", { name: "Second" })).toBeInTheDocument();
  });
});
