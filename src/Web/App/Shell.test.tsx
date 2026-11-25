import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import { beforeEach, describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import i18n from "./i18n";
import { Shell } from "./Shell";

describe("Shell", () => {
  beforeEach(() => {
    stubApi({
      "/api/platform/modules": {
        status: 200,
        body: [{ name: "Housing", routePrefix: "housing" }],
      },
    });
  });

  it("shows only enabled modules in the menu", async () => {
    renderWithProviders(<Shell />);

    const nav = screen.getByRole("navigation");
    await within(nav).findByText(i18n.t("title", { ns: "housing" }));

    const targets = within(nav)
      .getAllByRole("link")
      .map((link) => link.getAttribute("href"));
    expect(targets).toEqual(["/", "/housing/offers", "/housing/collection"]);
  });

  it("does not open the page of a disabled module", async () => {
    renderWithProviders(<Shell />, "/job-offers");

    expect(await screen.findByText(i18n.t("status.notFound"))).toBeInTheDocument();
  });

  it("collapses the menu to icons after going to a page; the menu button expands it again", async () => {
    renderWithProviders(<Shell />);
    const toggle = screen.getByRole("button", { name: i18n.t("nav.toggle") });
    expect(toggle).toHaveAttribute("aria-expanded", "true");

    const nav = screen.getByRole("navigation");
    const collection = i18n.t("nav.collection", { ns: "housing" });
    await userEvent.click(await within(nav).findByRole("link", { name: collection }));
    expect(toggle).toHaveAttribute("aria-expanded", "false");
    // The icons still lead to the pages and keep their names.
    expect(within(nav).getByRole("link", { name: collection })).toHaveAttribute(
      "href",
      "/housing/collection",
    );

    await userEvent.click(toggle);
    expect(toggle).toHaveAttribute("aria-expanded", "true");
  });

  it("in development shows a disabled module grayed out, without a link", async () => {
    renderWithProviders(<Shell />);

    const nav = screen.getByRole("navigation");
    const disabled = await within(nav).findByText(i18n.t("title", { ns: "jobOffers" }));

    expect(disabled.closest("[aria-disabled='true']")).not.toBeNull();
    expect(disabled.closest("a")).toBeNull();
  });
});
