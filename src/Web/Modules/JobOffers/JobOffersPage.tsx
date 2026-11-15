import { useTranslation } from "react-i18next";

export function JobOffersPage() {
  const { t } = useTranslation("jobOffers");

  return (
    <>
      <h1>{t("title")}</h1>
      <p>{t("empty")}</p>
    </>
  );
}
