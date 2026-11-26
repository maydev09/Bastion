import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useMemo, useState } from "react";
import { useTranslation } from "react-i18next";
import { useSearchParams } from "react-router";
import { api } from "@shared/api/client";
import type { components } from "@shared/api/schema";
import { Button } from "@shared/ui/Button";
import { DataTable, type DataTableColumn } from "@shared/ui/DataTable";
import { DateInput } from "@shared/ui/DateInput";
import { Icon } from "@shared/ui/Icon";
import { Pagination } from "@shared/ui/Pagination";
import { Panel } from "@shared/ui/Panel";
import { Select } from "@shared/ui/Select";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./CollectionPage.module.css";
import { describeDuration, findJobIcon, nameJob, statusIcons, triggerIcons } from "./jobs";
import { jobRunsQueryKey } from "./queryKeys";
import { RunDetails } from "./RunDetails";
import { RunResult } from "./RunResult";
import { useFormat } from "./useFormat";

type JobRun = components["schemas"]["JobRun"];
type JobRunStatus = components["schemas"]["JobRunStatus"];
type JobTrigger = components["schemas"]["JobTrigger"];

const statuses: JobRunStatus[] = [
  "Running",
  "Succeeded",
  "Failed",
  "Interrupted",
  "Canceled",
  "Queued",
];
const triggers: JobTrigger[] = ["Manual", "Schedule"];
const pageSizes = [25, 50, 100];
const defaultPageSize = 25;
const noRuns: JobRun[] = [];

type Props = {
  // The job names to filter by, in the page's order.
  jobNames: string[];
  // A run is going, so the history is read more often.
  active: boolean;
};

// Every run of the jobs, newest first: the audit of what ran, when and with what result. A click on a run opens its details
// under it, and closes them again. The view (job, status, trigger, days, page, page size) is in the URL query, under the
// API's names; from and to are calendar days there (to included), sent to the API as the browser's midnights.
export function JobRuns({ jobNames, active }: Props) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const errorMessage = useErrorMessage();
  const [params, setParams] = useSearchParams();
  // The runs opened in place; any number of them at once.
  const [opened, setOpened] = useState<number[]>([]);
  const toggle = (id: number) =>
    setOpened((current) =>
      current.includes(id) ? current.filter((other) => other !== id) : [...current, id],
    );
  const job = jobNames.find((name) => name === params.get("job")) ?? null;
  const status = statuses.find((s) => s === params.get("status")) ?? null;
  const trigger = triggers.find((tr) => tr === params.get("trigger")) ?? null;
  const from = readDay(params.get("from"));
  const to = readDay(params.get("to"));
  const pageParam = Number.parseInt(params.get("page") ?? "", 10);
  const page = pageParam >= 1 ? pageParam : 1;
  const sizeParam = Number.parseInt(params.get("pageSize") ?? "", 10);
  const pageSize = pageSizes.includes(sizeParam) ? sizeParam : defaultPageSize;

  const runs = useQuery({
    queryKey: [...jobRunsQueryKey, job, status, trigger, from, to, page, pageSize],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/jobs/runs", {
        params: {
          query: {
            job: job ?? undefined,
            status: status ?? undefined,
            trigger: trigger ?? undefined,
            from: from ? startOfDay(from, 0) : undefined,
            to: to ? startOfDay(to, 1) : undefined,
            page,
            pageSize,
          },
        },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    placeholderData: keepPreviousData,
    refetchInterval: active ? 2000 : 10000,
  });

  // Default values stay out of the URL; a changed filter starts from the first page.
  const changeView = (changes: {
    job?: string | null;
    status?: string | null;
    trigger?: string | null;
    from?: string | null;
    to?: string | null;
    page?: number;
    pageSize?: number;
  }) => {
    const next = { job, status, trigger, from, to, page, pageSize, ...changes };
    const query = new URLSearchParams();
    for (const key of ["job", "status", "trigger", "from", "to"] as const) {
      const value = next[key];
      if (value) {
        query.set(key, value);
      }
    }
    if (next.page > 1) {
      query.set("page", String(next.page));
    }
    if (next.pageSize !== defaultPageSize) {
      query.set("pageSize", String(next.pageSize));
    }
    setParams(query);
  };

  const columns = useMemo(
    (): DataTableColumn<JobRun>[] => [
      {
        id: "job",
        header: t("jobs.history.job"),
        minWidth: 200,
        cell: (run) => (
          <button
            type="button"
            className={styles.runToggle}
            aria-expanded={opened.includes(run.id)}
            onClick={() => toggle(run.id)}
          >
            <Icon name={findJobIcon(run.job)} size={16} />
            {nameJob(t, run.job)}
          </button>
        ),
      },
      {
        id: "trigger",
        header: t("jobs.history.trigger"),
        cell: (run) => (
          <span className={styles.iconText}>
            <Icon name={triggerIcons[run.trigger]} size={16} />
            {t(`jobs.trigger.${run.trigger}`)}
          </span>
        ),
      },
      {
        id: "status",
        header: t("jobs.history.status"),
        cell: (run) => (
          <span className={styles.status} data-status={run.status}>
            <Icon name={statusIcons[run.status]} size={16} />
            {t(`jobs.status.${run.status}`)}
          </span>
        ),
      },
      {
        id: "started",
        header: t("jobs.history.started"),
        align: "end",
        cell: (run) => format.shortDate(run.startedAt ?? run.queuedAt),
      },
      {
        id: "duration",
        header: t("jobs.history.duration"),
        align: "end",
        cell: (run) =>
          run.startedAt && run.finishedAt
            ? describeDuration(t, run.startedAt, run.finishedAt)
            : "—",
      },
      {
        id: "result",
        header: t("jobs.history.result"),
        minWidth: 220,
        cell: (run) =>
          run.finishedAt ? <RunResult run={run} /> : <span className={styles.muted}>—</span>,
      },
    ],
    [t, format, opened],
  );

  return (
    <Panel title={t("jobs.history.title")} flush>
      <div className={styles.filters}>
        <Select
          label={t("jobs.history.job")}
          value={job ?? ""}
          items={[
            { value: "", label: t("jobs.history.allJobs"), icon: "list" },
            ...jobNames.map((name) => ({
              value: name,
              label: nameJob(t, name),
              icon: findJobIcon(name),
            })),
          ]}
          onChange={(value) => changeView({ job: value || null, page: 1 })}
        />
        <Select
          label={t("jobs.history.status")}
          value={status ?? ""}
          items={[
            { value: "", label: t("jobs.history.allStatuses"), icon: "filter" },
            ...statuses.map((s) => ({
              value: s,
              label: t(`jobs.status.${s}`),
              icon: statusIcons[s],
            })),
          ]}
          onChange={(value) => changeView({ status: value || null, page: 1 })}
        />
        <Select
          label={t("jobs.history.trigger")}
          value={trigger ?? ""}
          items={[
            { value: "", label: t("jobs.history.allTriggers"), icon: "filter" },
            ...triggers.map((tr) => ({
              value: tr,
              label: t(`jobs.trigger.${tr}`),
              icon: triggerIcons[tr],
            })),
          ]}
          onChange={(value) => changeView({ trigger: value || null, page: 1 })}
        />
        <DateInput
          label={t("jobs.history.from")}
          value={from}
          max={to ?? undefined}
          onChange={(value) => changeView({ from: value, page: 1 })}
        />
        <DateInput
          label={t("jobs.history.to")}
          value={to}
          min={from ?? undefined}
          onChange={(value) => changeView({ to: value, page: 1 })}
        />
        {(job || status || trigger || from || to) && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() =>
              changeView({ job: null, status: null, trigger: null, from: null, to: null, page: 1 })
            }
          >
            <Icon name="close" size={16} />
            {t("jobs.history.clear")}
          </Button>
        )}
      </div>
      {runs.isError ? (
        <p className={styles.error} role="alert">
          {errorMessage(runs.error.message)}
        </p>
      ) : runs.data?.totalCount === 0 ? (
        <p className={styles.empty}>{t("jobs.history.empty")}</p>
      ) : (
        <>
          <DataTable
            columns={columns}
            rows={runs.data?.items ?? noRuns}
            getRowId={(run) => String(run.id)}
            // Always newest first; no column sorts.
            sort={{ column: "started", descending: true }}
            onSortChange={() => {}}
            busy={runs.isPlaceholderData}
            loading={runs.isPending ? t("jobs.history.loading") : undefined}
            onRowClick={(run) => toggle(run.id)}
            expandedRowIds={opened.map(String)}
            renderExpanded={(run) => <RunDetails run={run} />}
          />
          {runs.data && (
            <div className={styles.paging}>
              <Pagination
                page={page}
                pageSize={pageSize}
                totalCount={runs.data.totalCount}
                pageSizes={pageSizes}
                onPageChange={(next) => changeView({ page: next })}
                onPageSizeChange={(next) => changeView({ pageSize: next, page: 1 })}
              />
            </div>
          )}
        </>
      )}
    </Panel>
  );
}

// A calendar day from the URL ("2026-11-25"), or null for anything else.
function readDay(value: string | null): string | null {
  return value && /^\d{4}-\d{2}-\d{2}$/.test(value) ? value : null;
}

// The browser's midnight at the start of the day, or of a later one, as an instant for the API.
function startOfDay(day: string, daysLater: number): string {
  const date = new Date(`${day}T00:00`);
  date.setDate(date.getDate() + daysLater);
  return date.toISOString();
}
