import { useTranslation } from "react-i18next";
import { SmokeTest } from "./SmokeTest";

export function FlatOffersPage() {
  const { t } = useTranslation("flatOffers");

  return (
    <>
      <h1>{t("title")}</h1>
      <SmokeTest />
    </>
  );
}
