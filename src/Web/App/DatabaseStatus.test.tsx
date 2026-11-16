import { screen } from "@testing-library/react";
import userEvent from "@testing-library/user-event";
import i18n from "i18next";
import { describe, expect, it } from "vitest";
import { renderWithProviders, stubApi } from "@shared/test/render";
import { DatabaseStatus } from "./DatabaseStatus";

async function runCheck(response: { status: number; body: unknown }) {
  stubApi({ "/api/platform/database": response });
  renderWithProviders(<DatabaseStatus />);
  await userEvent.click(screen.getByRole("button"));
}

const connected = {
  user: "bastion_app",
  serverVersion: "18.7",
  canChangeSchema: false,
  migrations: [{ schema: "flat_offers", version: "20261116120000" }],
};

describe("DatabaseStatus", () => {
  it("says the connection works", async () => {
    await runCheck({ status: 200, body: connected });

    expect(await screen.findByRole("status")).toHaveTextContent(i18n.t("database.ok"));
  });

  it("warns when the application can change the schema", async () => {
    await runCheck({ status: 200, body: { ...connected, canChangeSchema: true } });

    expect(await screen.findByRole("alert")).toHaveTextContent(i18n.t("database.canChangeSchema"));
  });

  it("shows the translated message when the database is unavailable", async () => {
    await runCheck({
      status: 503,
      body: { title: "Database unavailable", code: "database.unavailable" },
    });

    expect(await screen.findByRole("alert")).toHaveTextContent(
      i18n.getResource("pl", "common", "errors.database.unavailable") as string,
    );
  });
});
