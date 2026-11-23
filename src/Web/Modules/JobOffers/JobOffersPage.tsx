import { useTranslation } from "react-i18next";
import { PageTitle } from "@shared/ui/PageTitle";

export function JobOffersPage() {
  const { t } = useTranslation("jobOffers");

  return (
    <>
      <PageTitle>{t("title")}</PageTitle>
      <p>{t("empty")}</p>
    </>
  );
}
