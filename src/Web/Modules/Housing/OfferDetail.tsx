import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useParams } from "react-router";
import { api } from "@shared/api/client";
import { Icon } from "@shared/ui/Icon";
import { Panel } from "@shared/ui/Panel";
import { PropertyList } from "@shared/ui/PropertyList";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./OfferDetail.module.css";
import { OfferChanges } from "./OfferChanges";
import { OfferDescription } from "./OfferDescription";
import { OfferDetails } from "./OfferDetails";
import { OfferPlace } from "./OfferPlace";
import { offersQueryKey } from "./queryKeys";
import { useFormat } from "./useFormat";

export function OfferDetail() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const id = Number(useParams().id);
  const location = useLocation();
  const offer = useQuery({
    queryKey: [...offersQueryKey, "detail", id],
    queryFn: async () => {
      const { data, error, response } = await api.GET("/api/housing/offers/{id}", {
        params: { path: { id } },
      });
      if (response.status === 404) {
        return null;
      }
      if (error) {
        throw new Error((error as { code?: string }).code ?? "");
      }
      return data;
    },
  });

  const format = useFormat();

  // Back to the list view (page, sort order) the user came from.
  const from: unknown = (location.state as { from?: unknown } | null)?.from;
  const back = (
    <Link
      to={{ pathname: "../..", search: typeof from === "string" ? from : "" }}
      relative="path"
      className={styles.back}
    >
      <Icon name="previous" size={14} />
      {t("detail.back")}
    </Link>
  );

  if (offer.isPending) {
    return <p className={styles.muted}>{t("detail.loading")}</p>;
  }
  if (offer.isError) {
    return (
      <p className={styles.error} role="alert">
        {errorMessage(offer.error.message)}
      </p>
    );
  }
  if (!offer.data) {
    return (
      <>
        {back}
        <p>{t("detail.notFound")}</p>
      </>
    );
  }

  const o = offer.data;
  const money = (value: number | null | undefined, currency: string | null | undefined) =>
    value != null && currency ? format.money(value, currency) : "—";
  const summary = [
    { label: t("field.price"), value: money(o.price, o.priceCurrency) },
    {
      label: t("field.pricePerM2"),
      value:
        o.pricePerM2 != null && o.priceCurrency
          ? format.wholeMoney(o.pricePerM2, o.priceCurrency)
          : "—",
    },
    { label: t("field.rent"), value: money(o.rent, o.rentCurrency) },
    { label: t("field.deposit"), value: money(o.deposit, o.depositCurrency) },
    { label: t("field.areaM2"), value: o.areaM2 != null ? format.area(o.areaM2) : "—" },
    { label: t("field.rooms"), value: o.rooms ?? "—" },
  ];
  const place =
    o.placeName && o.voivodeship
      ? t("place.name", { name: o.placeName, voivodeship: t(`voivodeship.${o.voivodeship}`) })
      : o.placeName;
  const date = (iso: string | null | undefined) => iso && format.date(iso);

  return (
    <article className={styles.page}>
      {back}
      <header className={styles.header}>
        <div className={styles.heading}>
          <h1>{o.title}</h1>
          {place && <p className={styles.subtitle}>{place}</p>}
        </div>
        <a className={styles.portal} href={o.url} target="_blank" rel="noreferrer">
          {t("detail.portal", { source: o.source })}
          <Icon name="external" size={14} />
        </a>
      </header>

      <dl className={styles.summary}>
        {summary.map((item) => (
          <div key={item.label}>
            <dt>{item.label}</dt>
            <dd>{item.value}</dd>
          </div>
        ))}
      </dl>

      <div className={styles.layout}>
        <div className={styles.column}>
          <OfferChanges offer={o} />
          <OfferDescription offer={o} />
        </div>
        <aside className={styles.column}>
          <OfferDetails offer={o} />
          <OfferPlace offer={o} />
          <Panel title={t("detail.listing")} flush>
            <PropertyList
              items={[
                { label: t("detail.source"), value: o.source },
                { label: t("detail.addedOnPortal"), value: date(o.externalCreatedAt) },
                { label: t("detail.updatedOnPortal"), value: date(o.externalUpdatedAt) },
                { label: t("detail.firstFetched"), value: date(o.createdAt) },
                { label: t("detail.detailsFetched"), value: date(o.detailsFetchedAt) },
              ]}
            />
          </Panel>
        </aside>
      </div>
    </article>
  );
}
