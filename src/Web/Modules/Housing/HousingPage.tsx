import { useTranslation } from "react-i18next";
import { ImportOffers } from "./ImportOffers";
import { OfferList } from "./OfferList";
import { ReparseOffers } from "./ReparseOffers";

export function HousingPage() {
  const { t } = useTranslation("housing");

  return (
    <>
      <h1>{t("title")}</h1>
      <ImportOffers />
      <ReparseOffers />
      <OfferList />
    </>
  );
}
