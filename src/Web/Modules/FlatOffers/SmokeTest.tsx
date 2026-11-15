import { useMutation } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import { Button } from "@shared/ui/Button";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./SmokeTest.module.css";

export function SmokeTest() {
  const { t } = useTranslation("flatOffers");
  const errorMessage = useErrorMessage("flatOffers");
  const smokeTest = useMutation({
    mutationFn: async () => {
      const { data, error } = await api.POST("/api/flat-offers/smoke-test");
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
  });

  return (
    <section className={styles.card}>
      <p>{t("smokeTest.description")}</p>
      <Button variant="success" onClick={() => smokeTest.mutate()} disabled={smokeTest.isPending}>
        {smokeTest.isPending ? t("smokeTest.running") : t("smokeTest.run")}
      </Button>
      {smokeTest.data && (
        <p className={styles.result} role="status">
          {t("smokeTest.result", {
            source: smokeTest.data.source,
            count: smokeTest.data.offerCount,
          })}
          {smokeTest.data.skippedCount > 0 &&
            ` ${t("smokeTest.skipped", { count: smokeTest.data.skippedCount })}`}
        </p>
      )}
      {smokeTest.error && (
        <p className={styles.error} role="alert">
          {errorMessage(smokeTest.error.message)}
        </p>
      )}
    </section>
  );
}
