import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import { Button } from "@shared/ui/Button";
import { useErrorMessage } from "@shared/useErrorMessage";
import { offersQueryKey } from "./queryKeys";
import styles from "./Housing.module.css";

export function ReparseOffers() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const queryClient = useQueryClient();
  const reparse = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/housing/reparse");
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: offersQueryKey }),
  });

  return (
    <section className={styles.card}>
      <p>{t("reparse.description")}</p>
      <Button onClick={() => reparse.mutate()} disabled={reparse.isPending}>
        {reparse.isPending ? t("reparse.running") : t("reparse.run")}
      </Button>
      {reparse.data && (
        <p className={styles.result} role="status">
          {t("reparse.result", {
            captures: reparse.data.captureCount,
            added: reparse.data.addedCount,
            updated: reparse.data.updatedCount,
            unchanged: reparse.data.unchangedCount,
          })}
          {reparse.data.failedCount > 0 &&
            ` ${t("reparse.failed", { count: reparse.data.failedCount })}`}
        </p>
      )}
      {reparse.error && (
        <p className={styles.error} role="alert">
          {errorMessage(reparse.error.message)}
        </p>
      )}
    </section>
  );
}
