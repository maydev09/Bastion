import { screen, within } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { CollectionPage } from "./CollectionPage";

const t = i18n.getFixedT("pl", "housing");

const run = (job: string, extra: object) => ({
  id: 1,
  job,
  trigger: "Schedule",
  status: "Succeeded",
  queuedAt: "2026-11-26T10:00:00+00:00",
  startedAt: "2026-11-26T10:00:00+00:00",
  finishedAt: "2026-11-26T10:00:42+00:00",
  errorCode: null,
  summary: null,
  ...extra,
});

const jobList = (jobs: object[]) => ({
  status: 200,
  body: {
    jobs,
    scheduled: true,
    timeZone: "Europe/Warsaw",
    serverTime: "2026-11-26T10:05:00+00:00",
  },
});

const noRuns = { status: 200, body: { items: [], page: 1, pageSize: 25, totalCount: 0 } };

describe("CollectionPage", () => {
  it("shows each task by when it runs and how its last run went", async () => {
    stubApi({
      "/api/housing/jobs": jobList([
        {
          name: "ImportNewOffers",
          every: "00:10:00",
          latestRun: run("ImportNewOffers", {
            summary: { counts: { pages: 1, added: 3, updated: 1, unchanged: 68, skipped: 0 } },
          }),
        },
        {
          name: "ImportPlaces",
          latestRun: run("ImportPlaces", {
            status: "Failed",
            errorCode: "housing.placesUnavailable",
          }),
        },
        { name: "ReparseOffers", latestRun: null },
      ]),
      "/api/housing/jobs/runs": noRuns,
    });

    renderWithProviders(<CollectionPage />);

    const importRow = within(
      (await screen.findByText(t("jobs.ImportNewOffers.name"))).closest("tr")!,
    );
    expect(
      importRow.getByText(t("jobs.schedule.every", { interval: t("jobs.minutes", { count: 10 }) })),
    ).toBeVisible();
    // The row shows only the key count.
    expect(
      importRow.getByText(
        t("jobs.keyResult", { label: t("jobs.ImportNewOffers.counts.added"), value: "3" }),
      ),
    ).toBeVisible();
    const placesRow = within(screen.getByText(t("jobs.ImportPlaces.name")).closest("tr")!);
    expect(placesRow.getByText(t("errors.placesUnavailable"))).toBeVisible();
    const reparseRow = within(screen.getByText(t("jobs.ReparseOffers.name")).closest("tr")!);
    expect(reparseRow.getByText(t("jobs.neverRun"))).toBeVisible();
  });

  it("shows a run started elsewhere as running and does not let it start twice", async () => {
    stubApi({
      "/api/housing/jobs": jobList([
        {
          name: "ReparseOffers",
          latestRun: run("ReparseOffers", {
            trigger: "Manual",
            status: "Running",
            finishedAt: null,
          }),
        },
      ]),
      "/api/housing/jobs/runs": noRuns,
    });

    renderWithProviders(<CollectionPage />);

    const button = await screen.findByRole("button", {
      name: `${t("jobs.running")} ${t("jobs.ReparseOffers.name")}`,
    });
    expect(button).toBeDisabled();
  });

  it("starts a task on the server", async () => {
    const requests = stubApi({
      "/api/housing/jobs": jobList([{ name: "ReparseOffers", latestRun: null }]),
      "/api/housing/jobs/runs": noRuns,
      "/api/housing/jobs/ReparseOffers/runs": {
        status: 202,
        body: run("ReparseOffers", {
          trigger: "Manual",
          status: "Queued",
          startedAt: null,
          finishedAt: null,
        }),
      },
    });
    renderWithProviders(<CollectionPage />);

    await userEvent.click(
      await screen.findByRole("button", {
        name: `${t("jobs.run")} ${t("jobs.ReparseOffers.name")}`,
      }),
    );

    expect(requests.some((r) => r.pathname === "/api/housing/jobs/ReparseOffers/runs")).toBe(true);
  });

  it("reads the history view from the link", async () => {
    const requests = stubApi({
      "/api/housing/jobs": jobList([
        { name: "ImportNewOffers", every: "00:10:00", latestRun: null },
      ]),
      "/api/housing/jobs/runs": {
        status: 200,
        body: {
          items: [
            run("ImportNewOffers", { status: "Failed", errorCode: "housing.pageLimitReached" }),
          ],
          page: 2,
          pageSize: 25,
          totalCount: 26,
        },
      },
    });

    renderWithProviders(
      <CollectionPage />,
      "/?job=ImportNewOffers&status=Failed&trigger=Manual&from=2026-11-25&to=2026-11-25&page=2",
    );

    expect(await screen.findByText(t("errors.pageLimitReached"))).toBeVisible();
    const query = Object.fromEntries(
      requests.filter((r) => r.pathname === "/api/housing/jobs/runs").at(-1)!.searchParams,
    );
    expect(query).toMatchObject({
      job: "ImportNewOffers",
      status: "Failed",
      trigger: "Manual",
      page: "2",
    });
    // One whole day, the "to" day included.
    expect(Date.parse(query.to) - Date.parse(query.from)).toBe(24 * 60 * 60 * 1000);
  });

  it("opens a run of the history in place with everything it counted", async () => {
    stubApi({
      "/api/housing/jobs": jobList([
        { name: "ImportNewOffers", every: "00:10:00", latestRun: null },
      ]),
      "/api/housing/jobs/runs": {
        status: 200,
        body: {
          items: [
            run("ImportNewOffers", {
              summary: { counts: { pages: 1, added: 3, updated: 1, unchanged: 68, skipped: 0 } },
            }),
          ],
          page: 1,
          pageSize: 25,
          totalCount: 1,
        },
      },
    });
    renderWithProviders(<CollectionPage />);
    const history = within(await screen.findByRole("region", { name: t("jobs.history.title") }));

    const toggle = await history.findByRole("button", { name: t("jobs.ImportNewOffers.name") });
    await userEvent.click(toggle);

    expect(toggle).toHaveAttribute("aria-expanded", "true");
    expect(
      history.getByText(t("jobs.ImportNewOffers.counts.unchanged")).nextSibling,
    ).toHaveTextContent("68");
  });
});
