import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { api } from "@shared/api/client";
import { Button } from "@shared/ui/Button";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./Housing.module.css";
import { offersQueryKey } from "./queryKeys";

export function OfferList() {
  const { t, i18n } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const [page, setPage] = useState(1);
  const offers = useQuery({
    queryKey: [...offersQueryKey, page],
    queryFn: async () => {
      const { data, error } = await api.GET("/api/housing/offers", {
        params: { query: { page } },
      });
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
    placeholderData: keepPreviousData,
  });

  const language = i18n.resolvedLanguage ?? "pl";
  const date = new Intl.DateTimeFormat(language, { dateStyle: "medium", timeStyle: "short" });
  const number = new Intl.NumberFormat(language);
  const money = (value: number, currency: string) =>
    new Intl.NumberFormat(language, { style: "currency", currency }).format(value);

  if (offers.isPending) {
    return <p className={styles.muted}>{t("list.loading")}</p>;
  }
  if (offers.isError) {
    return (
      <p className={styles.error} role="alert">
        {errorMessage(offers.error.message)}
      </p>
    );
  }

  const { items, pageSize, totalCount } = offers.data;
  const pageCount = Math.max(1, Math.ceil(totalCount / pageSize));

  return (
    <section className={styles.card}>
      <h2>{t("list.title", { total: totalCount })}</h2>
      {items.length === 0 ? (
        <p className={styles.muted}>{t("list.empty")}</p>
      ) : (
        <>
          <table className={styles.table}>
            <thead>
              <tr>
                <th>{t("list.offer")}</th>
                <th className={styles.number}>{t("list.price")}</th>
                <th className={styles.number}>{t("list.area")}</th>
                <th className={styles.number}>{t("list.rooms")}</th>
                <th>{t("list.firstSeen")}</th>
              </tr>
            </thead>
            <tbody>
              {items.map((offer) => (
                <tr key={offer.id}>
                  <td>
                    <a href={offer.url} target="_blank" rel="noreferrer">
                      {offer.title}
                    </a>
                  </td>
                  <td className={styles.number}>
                    {offer.price != null && offer.priceCurrency
                      ? money(offer.price, offer.priceCurrency)
                      : "—"}
                  </td>
                  <td className={styles.number}>
                    {offer.areaM2 != null ? `${number.format(offer.areaM2)} m²` : "—"}
                  </td>
                  <td className={styles.number}>{offer.rooms ?? "—"}</td>
                  <td>
                    {offer.externalCreatedAt ? date.format(new Date(offer.externalCreatedAt)) : "—"}
                  </td>
                </tr>
              ))}
            </tbody>
          </table>
          <div className={styles.pager}>
            <Button onClick={() => setPage(page - 1)} disabled={page <= 1}>
              {t("list.previous")}
            </Button>
            <span>{t("list.page", { page, pageCount })}</span>
            <Button onClick={() => setPage(page + 1)} disabled={page >= pageCount}>
              {t("list.next")}
            </Button>
          </div>
        </>
      )}
    </section>
  );
}
