import "@testing-library/jest-dom/vitest";
import { cleanup } from "@testing-library/react";
import { afterEach, vi } from "vitest";
import i18n from "../i18n";

await i18n.changeLanguage("pl");

// jsdom lays nothing out, so it has no scrolling into view; the browser has.
Element.prototype.scrollIntoView = () => {};

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  // Remembered state (open panels, the menu) must not leak from one test into the next.
  localStorage.clear();
  sessionStorage.clear();
});
