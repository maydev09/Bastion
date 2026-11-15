import { useTranslation } from "react-i18next";

export function useErrorMessage(namespace: string) {
  const { t, i18n } = useTranslation();

  return (code: string | null | undefined): string => {
    const language = i18n.resolvedLanguage ?? "pl";
    for (const ns of [namespace, "common"]) {
      const message: unknown = code && i18n.getResource(language, ns, `errors.${code}`);
      if (typeof message === "string") {
        return message;
      }
    }
    return t("errors.unknown");
  };
}
