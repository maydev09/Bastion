import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import styles from "./Shell.module.css";

// The date of the commit the app was built from and the pull request it merged, e.g. "25 lis 2026 · #42",
// linked to the commit; a local build is the development version.
export function AppVersion() {
  const { t, i18n } = useTranslation();
  const { data } = useQuery({
    queryKey: ["platform", "version"],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/platform/version");
      if (error) {
        throw new Error("version");
      }
      return data;
    },
    staleTime: Infinity,
  });
  if (!data) {
    return null;
  }

  if (!data.commitDate || !data.commitUrl) {
    return <p className={styles.version}>{t("version.dev")}</p>;
  }

  const date = new Intl.DateTimeFormat(i18n.resolvedLanguage, { dateStyle: "medium" }).format(
    new Date(data.commitDate),
  );
  return (
    <a
      className={styles.version}
      href={data.commitUrl}
      target="_blank"
      rel="noreferrer"
      title={t("version.commit", { commit: data.commit })}
    >
      {data.pullRequest
        ? t("version.withPullRequest", { date, pullRequest: data.pullRequest })
        : date}
    </a>
  );
}
