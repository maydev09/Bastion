import { useTranslation } from "react-i18next";
import { JobCard } from "./JobCard";

export function ImportPlaces() {
  const { t } = useTranslation("housing");
  return (
    <JobCard
      job="ImportPlaces"
      description={t("places.description")}
      run={t("places.run")}
      running={t("places.running")}
      result={(counts) =>
        t("places.result", { places: counts.places, added: counts.added, updated: counts.updated })
      }
    />
  );
}
