import { describe, expect, it } from "vitest";
import { allModules } from "./modules";
import enCommon from "./locales/en/common.json";
import plCommon from "./locales/pl/common.json";

function keys(dictionary: object, prefix = ""): string[] {
  return Object.entries(dictionary).flatMap(([key, value]) =>
    typeof value === "object" && value !== null
      ? keys(value, `${prefix}${key}.`)
      : [`${prefix}${key.replace(/_(zero|one|two|few|many|other)$/, "")}`],
  );
}

const unique = (dictionary: object) => [...new Set(keys(dictionary))].sort();

describe("translations", () => {
  it.each([
    { namespace: "common", pl: plCommon, en: enCommon },
    ...allModules.map((m) => ({ namespace: m.namespace, ...m.locales })),
  ])("$namespace has the same keys in pl and en", ({ pl, en }) => {
    expect(unique(en)).toEqual(unique(pl));
  });
});
