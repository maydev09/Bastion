import { useTranslation } from "react-i18next";
import { JobCard } from "./JobCard";

export function CalculateMonthlyCosts() {
  const { t } = useTranslation("housing");
  return (
    <JobCard
      job="CalculateMonthlyCosts"
      description={t("costs.description")}
      run={t("costs.run")}
      running={t("costs.running")}
      result={(counts) =>
        t("costs.result", {
          rates: counts.rentRates,
          stated: counts.statedRents,
          description: counts.descriptionRents,
          included: counts.includedRents,
          nominal: counts.nominalRents,
          estimated: counts.estimatedRents,
          without: counts.withoutCost,
        })
      }
    />
  );
}
