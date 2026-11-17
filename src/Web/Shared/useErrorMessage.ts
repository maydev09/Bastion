import { useTranslation } from "react-i18next";

// An API error code is "<area>.<name>". When the area is a module, the text is in that
// module's dictionary under errors.<name>; shared areas (scraping, database) are in common.
export function useErrorMessage() {
  const { t, i18n } = useTranslation();

  return (code: string | null | undefined): string => {
    const language = i18n.resolvedLanguage ?? "pl";
    const [area, ...rest] = (code ?? "").split(".");
    const name = rest.join(".");

    const message: unknown = i18n.hasResourceBundle(language, area)
      ? i18n.getResource(language, area, `errors.${name}`)
      : code && i18n.getResource(language, "common", `errors.${code}`);

    return typeof message === "string" ? message : t("errors.unknown");
  };
}
