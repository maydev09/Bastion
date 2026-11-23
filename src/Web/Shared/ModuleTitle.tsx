import { useTranslation } from "react-i18next";
import { readModuleText } from "./moduleText";

export function ModuleTitle({ namespace }: { namespace: string }) {
  const { i18n } = useTranslation();
  return readModuleText(i18n, namespace);
}
