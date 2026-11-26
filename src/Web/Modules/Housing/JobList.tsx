import type { UseMutationResult } from "@tanstack/react-query";
import { useMemo } from "react";
import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { Button } from "@shared/ui/Button";
import { DataTable, type DataTableColumn } from "@shared/ui/DataTable";
import { Icon } from "@shared/ui/Icon";
import { Panel } from "@shared/ui/Panel";
import { VisuallyHidden } from "@shared/ui/VisuallyHidden";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./CollectionPage.module.css";
import {
  describeDuration,
  describeJob,
  describeSchedule,
  findJobGroup,
  findJobIcon,
  jobGroups,
  nameJob,
  sortJobs,
  statusIcons,
} from "./jobs";
import { RunResult } from "./RunResult";
import { useFormat } from "./useFormat";

type JobList = components["schemas"]["JobList"];
type JobListItem = components["schemas"]["JobListItem"];
type JobRun = components["schemas"]["JobRun"];

type Props = {
  list: JobList;
  start: UseMutationResult<JobRun, Error, string>;
};

// The tasks by what they are for, one row each: what it is and does (the only place that says it; the history assumes the
// reader knows), when it runs on its own, how its last run went, its key
// result and a button to run it.
export function JobList({ list, start }: Props) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const errorMessage = useErrorMessage();
  const jobs = sortJobs(list.jobs);
  const serverTime = list.serverTime;

  const columns = useMemo(
    (): DataTableColumn<JobListItem>[] => [
      {
        id: "task",
        header: t("jobs.table.task"),
        minWidth: 280,
        cell: (job) => (
          <div className={styles.task}>
            <span className={styles.taskLine}>
              <Icon name={findJobIcon(job.name)} size={16} />
              <span className={styles.taskName}>{nameJob(t, job.name)}</span>
            </span>
            <span className={styles.summary}>{describeJob(t, job.name)}</span>
          </div>
        ),
      },
      {
        id: "schedule",
        header: t("jobs.table.schedule"),
        minWidth: 170,
        cell: (job) => <span className={styles.secondary}>{describeSchedule(t, job)}</span>,
      },
      {
        id: "last",
        header: t("jobs.table.last"),
        minWidth: 190,
        cell: (job) => {
          const run = job.latestRun;
          if (!run) {
            return <span className={styles.muted}>{t("jobs.neverRun")}</span>;
          }
          const started = run.startedAt ?? run.queuedAt;
          return (
            <span className={styles.status} data-status={run.status}>
              <Icon name={statusIcons[run.status]} size={16} />
              <VisuallyHidden>{t(`jobs.status.${run.status}`)}</VisuallyHidden>
              <span className={run.finishedAt ? styles.secondary : undefined}>
                {run.status === "Queued"
                  ? t("jobs.queued")
                  : !run.finishedAt
                    ? t("jobs.runningFor", { duration: describeDuration(t, started, serverTime) })
                    : `${format.ago(started, serverTime)} · ${describeDuration(t, started, run.finishedAt)}`}
              </span>
            </span>
          );
        },
      },
      {
        id: "result",
        header: t("jobs.table.result"),
        minWidth: 220,
        cell: (job) => {
          if (start.isError && start.variables === job.name) {
            return (
              <span className={styles.error} role="alert">
                {errorMessage(start.error.message)}
              </span>
            );
          }
          const run = job.latestRun;
          if (!run?.finishedAt) {
            return <span className={styles.muted}>—</span>;
          }
          return <RunResult run={run} />;
        },
      },
      {
        id: "run",
        header: "",
        align: "end",
        cell: (job) => {
          const run = job.latestRun;
          const going = run != null && !run.finishedAt;
          const starting = start.isPending && start.variables === job.name;
          const label =
            run?.status === "Queued"
              ? t("jobs.queued")
              : going || starting
                ? t("jobs.running")
                : t("jobs.run");
          return (
            <Button
              size="sm"
              onClick={() => start.mutate(job.name)}
              disabled={going || starting}
              // Every row has the same button text; the name tells them apart for a screen reader.
              aria-label={`${label} ${nameJob(t, job.name)}`}
            >
              {label}
            </Button>
          );
        },
      },
    ],
    [t, format, errorMessage, start, serverTime],
  );

  return (
    <>
      {jobGroups.map((group) => {
        const inGroup = jobs.filter((job) => findJobGroup(job.name) === group);
        return (
          inGroup.length > 0 && (
            <Panel key={group} title={t(`jobs.group.${group}`)} flush>
              <DataTable
                columns={columns}
                rows={inGroup}
                getRowId={(job) => job.name}
                // The tasks keep the page's order; no column sorts.
                sort={{ column: "task", descending: false }}
                onSortChange={() => {}}
              />
            </Panel>
          )
        );
      })}
    </>
  );
}
