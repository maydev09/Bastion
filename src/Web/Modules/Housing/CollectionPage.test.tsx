import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { CollectionPage } from "./CollectionPage";

const t = i18n.getFixedT("pl", "housing");

describe("CollectionPage", () => {
  it("shows what the place import saved", async () => {
    stubApi({
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
              },
            },
          },
          serverTime: "2026-11-20T10:02:00+00:00",
        },
      },
    });

    renderWithProviders(<CollectionPage />);

    expect(await screen.findByRole("status")).toHaveTextContent(
      t("places.result", { places: 65000, added: 65000, updated: 0 }),
    );
  });

  it("shows what the import saved", async () => {
    stubApi({
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
    renderWithProviders(<CollectionPage />);

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
      "/api/housing/import/OtoDom": { status, body: { title: "Import failed", code } },
    });
    renderWithProviders(<CollectionPage />);

    await userEvent.click(screen.getByRole("button", { name: t("import.run") }));

    expect(await screen.findByRole("alert")).toHaveTextContent(
      i18n.getResource("pl", ns, key) as string,
    );
  });

  it("shows what parsing the stored pages saved", async () => {
    stubApi({
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
    renderWithProviders(<CollectionPage />);

    await userEvent.click(screen.getByRole("button", { name: t("reparse.run") }));

    expect(await screen.findByRole("status")).toHaveTextContent(t("reparse.failed", { count: 1 }));
  });
});
