import { useTranslation } from "react-i18next";
import { PageTitle } from "@shared/ui/PageTitle";
import styles from "./Housing.module.css";
import { ImportOffers } from "./ImportOffers";
import { ImportPlaces } from "./ImportPlaces";
import { LinkPlaces } from "./LinkPlaces";
import { ReparseOffers } from "./ReparseOffers";

// Starting the jobs by hand; they also run on their own on a schedule.
export function CollectionPage() {
  const { t } = useTranslation("housing");

  return (
    <div className={styles.page}>
      <PageTitle>{t("collection.title")}</PageTitle>
      <p className={styles.muted}>{t("collection.hint")}</p>
      <div className={styles.tools}>
        <ImportOffers />
        <ReparseOffers />
        <ImportPlaces />
        <LinkPlaces />
      </div>
    </div>
  );
}
