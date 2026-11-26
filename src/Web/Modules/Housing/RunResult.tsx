import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./CollectionPage.module.css";
import { describeKeyResult } from "./jobs";
import { useFormat } from "./useFormat";

type JobRun = components["schemas"]["JobRun"];

// The short result of a finished run for a table cell: its key count, the error, or that it was interrupted.
export function RunResult({ run }: { run: JobRun }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const errorMessage = useErrorMessage();
  if (run.status === "Failed") {
    return <span className={styles.error}>{errorMessage(run.errorCode)}</span>;
  }
  if (run.status === "Interrupted" || run.status === "Canceled") {
    return <span className={styles.warning}>{t(`jobs.status.${run.status}`)}</span>;
  }
  return describeKeyResult(t, format.number, run) ?? <span className={styles.muted}>—</span>;
}
