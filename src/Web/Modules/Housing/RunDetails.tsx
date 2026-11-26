import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { PropertyList } from "@shared/ui/PropertyList";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./CollectionPage.module.css";
import { describeResult } from "./jobs";
import { useFormat } from "./useFormat";

type JobRun = components["schemas"]["JobRun"];

// One run opened in the history: why it failed or stopped, and everything it counted in a framed label/number table on
// the left.
export function RunDetails({ run }: { run: JobRun }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const errorMessage = useErrorMessage();
  const counts = describeResult(t, format.number, run) ?? [];
  return (
    <div className={styles.run}>
      {run.status === "Failed" && <p className={styles.error}>{errorMessage(run.errorCode)}</p>}
      {run.status === "Interrupted" && <p className={styles.secondary}>{t("jobs.interrupted")}</p>}
      {counts.length > 0 ? (
        <div className={styles.counts}>
          <PropertyList items={counts} />
        </div>
      ) : (
        run.finishedAt && <p className={styles.muted}>{t("jobs.history.noCounts")}</p>
      )}
    </div>
  );
}
