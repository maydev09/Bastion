import { useTranslation } from "react-i18next";

export function ModuleTitle({ namespace }: { namespace: string }) {
  const { i18n } = useTranslation();
  const title: unknown = i18n.getResource(i18n.resolvedLanguage ?? "pl", namespace, "title");
  return typeof title === "string" ? title : namespace;
}
