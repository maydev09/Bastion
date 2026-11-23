import type { i18n as I18n } from "i18next";

// The shell knows a module's namespace only at run time, so the text is read without typed keys.
// key picks another text of the module, e.g. the title of one of its pages ("nav.offers").
export function readModuleText(i18n: I18n, namespace: string, key = "title"): string {
  const text: unknown = i18n.getResource(i18n.resolvedLanguage ?? "pl", namespace, key);
  return typeof text === "string" ? text : namespace;
}
