import { useTranslation } from "react-i18next";
import { ImportOffers } from "./ImportOffers";
import { OfferList } from "./OfferList";

export function FlatOffersPage() {
  const { t } = useTranslation("flatOffers");

  return (
    <>
      <h1>{t("title")}</h1>
      <ImportOffers />
      <OfferList />
    </>
  );
}
