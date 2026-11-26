import { useTranslation } from "react-i18next";
import { PageTitle } from "@shared/ui/PageTitle";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./CollectionPage.module.css";
import { JobList } from "./JobList";
import { JobRuns } from "./JobRuns";
import { sortJobs } from "./jobs";
import { useJobs } from "./useJobs";

// The jobs that collect and tidy the data, and the history of their runs. The state is the server's, so a run started on
// one device shows on every other one.
export function CollectionPage() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const { jobs, start, active } = useJobs();

  return (
    <div className={styles.page}>
      <PageTitle>{t("collection.title")}</PageTitle>
      <p className={styles.muted}>{t("collection.hint")}</p>
      {jobs.data && !jobs.data.scheduled && (
        <p className={styles.notice}>{t("collection.schedulesOff")}</p>
      )}
      {jobs.isError && (
        <p className={styles.error} role="alert">
          {errorMessage(jobs.error.message)}
        </p>
      )}
      {jobs.data && <JobList list={jobs.data} start={start} />}
      {jobs.data && (
        <JobRuns jobNames={sortJobs(jobs.data.jobs).map((job) => job.name)} active={active} />
      )}
    </div>
  );
}
