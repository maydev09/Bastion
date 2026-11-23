import { useTranslation } from "react-i18next";
import { JobCard } from "./JobCard";

export function LinkPlaces() {
  const { t } = useTranslation("housing");
  return (
    <JobCard
      job="LinkPlaces"
      description={t("link.description")}
      run={t("link.run")}
      running={t("link.running")}
      result={(counts) =>
        t("link.result", { linked: counts.linkedOffers, unlinked: counts.unlinkedOffers })
      }
    />
  );
}
