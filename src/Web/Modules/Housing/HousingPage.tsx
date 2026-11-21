import { useTranslation } from "react-i18next";
import { Collapsible } from "@shared/ui/Collapsible";
import styles from "./Housing.module.css";
import { ImportOffers } from "./ImportOffers";
import { ImportPlaces } from "./ImportPlaces";
import { OfferList } from "./OfferList";
import { ReparseOffers } from "./ReparseOffers";

export function HousingPage() {
  const { t } = useTranslation("housing");

  return (
    <div className={styles.page}>
      <h1>{t("title")}</h1>
      <Collapsible title={t("tools.title")} hint={t("tools.hint")}>
        <div className={styles.tools}>
          <ImportOffers />
          <ReparseOffers />
          <ImportPlaces />
        </div>
      </Collapsible>
      <OfferList />
    </div>
  );
}
