import type { TFunction } from "i18next";
import type { components } from "@shared/api/schema";
import type { IconName } from "@shared/ui/Icon";

type JobListItem = components["schemas"]["JobListItem"];
type JobRun = components["schemas"]["JobRun"];
type JobRunStatus = components["schemas"]["JobRunStatus"];
type JobTrigger = components["schemas"]["JobTrigger"];

export type JobGroup = "offers" | "places" | "upkeep" | "other";

type KnownJob =
  | "ImportOffers"
  | "ScanOffers"
  | "FetchDetails"
  | "ReparseOffers"
  | "ImportPlaces"
  | "LinkPlaces"
  | "CalculateMonthlyCosts"
  | "RawCaptureRetention";

// The jobs the page knows by name, in the order it shows them. What a run did is told by the labels of its counts in the
// dictionary (jobs.<name>.counts), in their order there; keys: the counts that say most in one line, the first one the
// run reported wins (a scan stopped early reports no removed offers). A job the API lists but this list does not know
// shows under "other" with its own name.
const definitions: { name: KnownJob; group: JobGroup; icon: IconName; keys: string[] }[] = [
  { name: "ImportOffers", group: "offers", icon: "download", keys: ["added"] },
  { name: "ScanOffers", group: "offers", icon: "search", keys: ["removed", "added"] },
  { name: "FetchDetails", group: "offers", icon: "file", keys: ["fetched"] },
  { name: "ReparseOffers", group: "offers", icon: "refresh", keys: ["pages"] },
  { name: "ImportPlaces", group: "places", icon: "globe", keys: ["places"] },
  { name: "LinkPlaces", group: "places", icon: "pin", keys: ["linkedOffers"] },
  { name: "CalculateMonthlyCosts", group: "upkeep", icon: "calculator", keys: ["estimatedRents"] },
  { name: "RawCaptureRetention", group: "upkeep", icon: "trash", keys: ["deletedCaptures"] },
];

// What each run state and trigger looks like next to its name.
export const statusIcons: Record<JobRunStatus, IconName> = {
  Queued: "queued",
  Running: "running",
  Succeeded: "succeeded",
  Failed: "failed",
  Canceled: "canceled",
  Interrupted: "interrupted",
};

export const triggerIcons: Record<JobTrigger, IconName> = {
  Manual: "user",
  Schedule: "clock",
};

// A job the page does not know gets a plain list icon.
export function findJobIcon(name: string): IconName {
  return definitions.find((d) => d.name === name)?.icon ?? "list";
}

export const jobGroups: JobGroup[] = ["offers", "places", "upkeep", "other"];

export function findJobGroup(name: string): JobGroup {
  return definitions.find((d) => d.name === name)?.group ?? "other";
}

// The jobs of the API in the page's order; unknown ones last.
export function sortJobs(jobs: JobListItem[]): JobListItem[] {
  const position = (name: string) => {
    const index = definitions.findIndex((d) => d.name === name);
    return index < 0 ? definitions.length : index;
  };
  return [...jobs].sort((a, b) => position(a.name) - position(b.name));
}

export function nameJob(t: TFunction<"housing">, name: string): string {
  const definition = definitions.find((d) => d.name === name);
  return definition ? t(`jobs.${definition.name}.name`) : name;
}

export function describeJob(t: TFunction<"housing">, name: string): string | null {
  const definition = definitions.find((d) => d.name === name);
  return definition ? t(`jobs.${definition.name}.description`) : null;
}

// The one count that says most about a run, for a table cell: "Nowe: 4". Null without it.
export function describeKeyResult(
  t: TFunction<"housing">,
  formatNumber: (value: number) => string,
  run: JobRun,
): string | null {
  const definition = definitions.find((d) => d.name === run.job);
  const lines = describeResult(t, formatNumber, run);
  if (!definition || !lines) {
    return null;
  }
  const labels = t(`jobs.${definition.name}.counts`, { returnObjects: true }) as Record<
    string,
    string
  >;
  const key = definition.keys.find((k) => run.summary?.counts[k] != null);
  const line = key ? lines.find((l) => l.label === labels[key]) : undefined;
  return line ? t("jobs.keyResult", line) : null;
}

// What a finished run did: one labeled count per line, in the dictionary's order; a count the run did not report (an older
// version of the job, a scan stopped early) is left out. Null without counts.
export function describeResult(
  t: TFunction<"housing">,
  formatNumber: (value: number) => string,
  run: JobRun,
): { label: string; value: string }[] | null {
  const counts = run.summary?.counts;
  const definition = definitions.find((d) => d.name === run.job);
  if (!counts || !definition) {
    return null;
  }
  const labels = t(`jobs.${definition.name}.counts`, { returnObjects: true }) as Record<
    string,
    string
  >;
  return Object.entries(labels)
    .filter(([key]) => counts[key] != null)
    .map(([key, label]) => ({
      label,
      value:
        key === "freedBytes"
          ? t("jobs.megabytes", { value: Math.round(counts[key] / 1_000_000) })
          : formatNumber(counts[key]),
    }));
}

// "co 10 minut", "codziennie o 01:00", "tylko ręcznie". every is "hh:mm:ss" (or "d.hh:mm:ss"), dailyAt "hh:mm:ss".
export function describeSchedule(t: TFunction<"housing">, job: JobListItem): string {
  if (job.dailyAt) {
    return t("jobs.schedule.dailyAt", { time: job.dailyAt.slice(0, 5) });
  }
  if (job.every) {
    const minutes = Math.round(parseTimeSpan(job.every) / 60);
    const interval =
      minutes % 60 === 0
        ? t("jobs.hours", { count: minutes / 60 })
        : t("jobs.minutes", { count: minutes });
    return t("jobs.schedule.every", { interval });
  }
  return t("jobs.schedule.manual");
}

// "45 s", "3 min 12 s", "1 godz. 5 min".
export function describeDuration(t: TFunction<"housing">, from: string, to: string): string {
  const seconds = Math.max(0, Math.round((Date.parse(to) - Date.parse(from)) / 1000));
  if (seconds < 60) {
    return t("jobs.duration.seconds", { seconds });
  }
  if (seconds < 3600) {
    return t("jobs.duration.minutes", { minutes: Math.floor(seconds / 60), seconds: seconds % 60 });
  }
  return t("jobs.duration.hours", {
    hours: Math.floor(seconds / 3600),
    minutes: Math.floor((seconds % 3600) / 60),
  });
}

// .NET writes a TimeSpan as "hh:mm:ss", with "d." in front from a day on and ".fffffff" after it for a fraction.
function parseTimeSpan(value: string): number {
  const match = /^(?:(\d+)\.)?(\d+):(\d+):(\d+)/.exec(value);
  if (!match) {
    return 0;
  }
  const [, days = "0", hours, minutes, seconds] = match;
  return Number(days) * 86400 + Number(hours) * 3600 + Number(minutes) * 60 + Number(seconds);
}
