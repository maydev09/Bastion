import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { api } from "@shared/api/client";
import { Button } from "@shared/ui/Button";
import { useErrorMessage } from "@shared/useErrorMessage";
import { latestRunQueryKey, offersQueryKey } from "./queryKeys";
import styles from "./Housing.module.css";

type Props = {
  job: string;
  description: string;
  run: string;
  running: string;
  // The result of the latest successful run, from its summary counts.
  result: (counts: Record<string, number>) => string;
};

// A job started by hand. It runs in the background, so its latest run is read until it ends.
export function JobCard({ job, description, run: runLabel, running: runningLabel, result }: Props) {
  const errorMessage = useErrorMessage();
  const queryClient = useQueryClient();
  const queryKey = latestRunQueryKey(job);

  const latest = useQuery({
    queryKey,
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
    onSuccess: (run) => queryClient.setQueryData(queryKey, run),
  });

  const run = latest.data;
  const running = start.isPending || (run != null && run.finishedAt == null);
  const succeeded = run?.status === "Succeeded" ? run.summary?.counts : undefined;

  // The offers are read again only when a run seen running here finishes, not for an old run on page load.
  const watched = useRef<number | null>(null);
  useEffect(() => {
    if (run && !run.finishedAt) {
      watched.current = run.id;
    } else if (run && run.id === watched.current) {
      watched.current = null;
      void queryClient.invalidateQueries({ queryKey: offersQueryKey });
    }
  }, [run, queryClient]);

  return (
    <section className={styles.card}>
      <p>{description}</p>
      <Button onClick={() => start.mutate()} disabled={running}>
        {running ? runningLabel : runLabel}
      </Button>
      {!running && succeeded && (
        <p className={styles.result} role="status">
          {result(succeeded)}
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
