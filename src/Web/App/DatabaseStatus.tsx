import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import { Button } from "@shared/ui/Button";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./DatabaseStatus.module.css";

export function DatabaseStatus() {
  const { t } = useTranslation();
  const errorMessage = useErrorMessage();
  const check = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.GET("/api/platform/database");
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
  });

  return (
    <section className={styles.card}>
      <h2>{t("database.title")}</h2>
      <p>{t("database.description")}</p>
      <Button variant="primary" onClick={() => check.mutate()} disabled={check.isPending}>
        {check.isPending ? t("database.running") : t("database.run")}
      </Button>
      {check.data &&
        (check.data.canChangeSchema ? (
          <p className={styles.error} role="alert">
            {t("database.canChangeSchema")}
          </p>
        ) : (
          <p className={styles.result} role="status">
            {t("database.ok")}
          </p>
        ))}
      {check.error && (
        <p className={styles.error} role="alert">
          {errorMessage(check.error.message)}
        </p>
      )}
    </section>
  );
}
