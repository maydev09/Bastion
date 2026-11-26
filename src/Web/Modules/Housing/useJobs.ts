import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { useEffect, useRef } from "react";
import { api } from "@shared/api/client";
import { jobRunsQueryKey, jobsQueryKey, offersQueryKey } from "./queryKeys";

// Every few seconds while a run is going, else now and then: a run started on another device shows here too.
const activeInterval = 2000;
const idleInterval = 10000;

// The jobs of the module with their latest runs, as the server keeps them, and starting one.
export function useJobs() {
  const queryClient = useQueryClient();
  const jobs = useQuery({
    queryKey: jobsQueryKey,
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/jobs");
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    refetchInterval: (query) =>
      query.state.data?.jobs.some((job) => job.latestRun && !job.latestRun.finishedAt)
        ? activeInterval
        : idleInterval,
  });

  const start = useMutation({
    mutationFn: async (job: string) => {
      const { data, error } = await api.POST("/api/housing/jobs/{job}/runs", {
        params: { path: { job } },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: jobsQueryKey });
      void queryClient.invalidateQueries({ queryKey: jobRunsQueryKey });
    },
  });

  // When a run seen going here ends, the offers and the history are read again (not for old runs on page load).
  const going = useRef(new Set<number>());
  useEffect(() => {
    let finished = false;
    for (const job of jobs.data?.jobs ?? []) {
      const run = job.latestRun;
      if (run && !run.finishedAt) {
        going.current.add(run.id);
      } else if (run && going.current.delete(run.id)) {
        finished = true;
      }
    }
    if (finished) {
      void queryClient.invalidateQueries({ queryKey: offersQueryKey });
      void queryClient.invalidateQueries({ queryKey: jobRunsQueryKey });
    }
  }, [jobs.data, queryClient]);

  const active = jobs.data?.jobs.some((job) => job.latestRun && !job.latestRun.finishedAt) ?? false;
  return { jobs, start, active };
}
