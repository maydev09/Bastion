import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect } from "react";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import { Button } from "@shared/ui/Button";
import { useErrorMessage } from "@shared/useErrorMessage";
import { offersQueryKey, placesRunQueryKey } from "./queryKeys";
import styles from "./Housing.module.css";

const job = "ImportPlaces";

export function ImportPlaces() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const queryClient = useQueryClient();

  // The import runs in the background for a minute or two; the latest run is read until it ends.
  const latest = useQuery({
    queryKey: placesRunQueryKey,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/jobs/{job}/runs/latest", {
        params: { path: { job } },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data.run;
    },
    refetchInterval: (query) => (query.state.data && !query.state.data.finishedAt ? 2000 : false),
  });

  const start = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/housing/jobs/{job}/runs", {
        params: { path: { job } },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    onSuccess: (run) => queryClient.setQueryData(placesRunQueryKey, run),
  });

  const run = latest.data;
  const running = start.isPending || (run != null && run.finishedAt == null);
  const succeeded = run?.status === "Succeeded" ? run.summary?.counts : undefined;

  useEffect(() => {
    if (succeeded) {
      void queryClient.invalidateQueries({ queryKey: offersQueryKey });
    }
  }, [succeeded, queryClient]);

  return (
    <section className={styles.card}>
      <p>{t("places.description")}</p>
      <Button onClick={() => start.mutate()} disabled={running}>
        {running ? t("places.running") : t("places.run")}
      </Button>
      {!running && succeeded && (
        <p className={styles.result} role="status">
          {t("places.result", {
            places: succeeded.places,
            added: succeeded.added,
            updated: succeeded.updated,
            linked: succeeded.linkedOffers,
            unlinked: succeeded.unlinkedOffers,
          })}
        </p>
      )}
      {!running && run?.status === "Failed" && (
        <p className={styles.error} role="alert">
          {errorMessage(run.errorCode)}
        </p>
      )}
      {start.error && (
        <p className={styles.error} role="alert">
          {errorMessage(start.error.message)}
        </p>
      )}
    </section>
  );
}
