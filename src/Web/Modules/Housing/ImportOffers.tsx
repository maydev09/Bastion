import { useMutation, useQueryClient } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import { Button } from "@shared/ui/Button";
import { useErrorMessage } from "@shared/useErrorMessage";
import { offersQueryKey } from "./queryKeys";
import styles from "./Housing.module.css";

export function ImportOffers() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const queryClient = useQueryClient();
  const importOffers = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/housing/import/{source}", {
        params: { path: { source: "OtoDom" } },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    onSuccess: () => queryClient.invalidateQueries({ queryKey: offersQueryKey }),
  });

  return (
    <section className={styles.card}>
      <p>{t("import.description")}</p>
      <Button
        variant="success"
        onClick={() => importOffers.mutate()}
        disabled={importOffers.isPending}
      >
        {importOffers.isPending ? t("import.running") : t("import.run")}
      </Button>
      {importOffers.data && (
        <p className={styles.result} role="status">
          {t("import.result", {
            source: importOffers.data.source,
            added: importOffers.data.addedCount,
            updated: importOffers.data.updatedCount,
            unchanged: importOffers.data.unchangedCount,
          })}
          {importOffers.data.skippedCount > 0 &&
            ` ${t("import.skipped", { count: importOffers.data.skippedCount })}`}
        </p>
      )}
      {importOffers.error && (
        <p className={styles.error} role="alert">
          {errorMessage(importOffers.error.message)}
        </p>
      )}
    </section>
  );
}
