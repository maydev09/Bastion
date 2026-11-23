import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate, useParams } from "react-router";
import { api } from "@shared/api/client";
import { Icon } from "@shared/ui/Icon";
import { LinkButton } from "@shared/ui/LinkButton";
import { PageTitle } from "@shared/ui/PageTitle";
import { Panel } from "@shared/ui/Panel";
import { PropertyList } from "@shared/ui/PropertyList";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./OfferDetail.module.css";
import { OfferChanges } from "./OfferChanges";
import { OfferDescription } from "./OfferDescription";
import { OfferDetails } from "./OfferDetails";
import { OfferFeatures } from "./OfferFeatures";
import { OfferPlace } from "./OfferPlace";
import { offersQueryKey } from "./queryKeys";
import { useFormat } from "./useFormat";

export function OfferDetail() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const id = Number(useParams().id);
  const location = useLocation();
  const navigate = useNavigate();
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

  // Back to the list view (page, sort order) the user came from. Coming from the list, it is the browser's
  // back step, so the list is scrolled as it was; the link still works when opened in a new tab.
  const from: unknown = (location.state as { from?: unknown } | null)?.from;
  const back = (
    <LinkButton
      to={{ pathname: "..", search: typeof from === "string" ? from : "" }}
      relative="path"
      onClick={(event) => {
        if (typeof from === "string") {
          event.preventDefault();
          void navigate(-1);
        }
      }}
    >
      <Icon name="previous" size={18} />
      {t("detail.back")}
    </LinkButton>
  );

  if (offer.isPending) {
    return <p className={styles.muted}>{t("detail.loading")}</p>;
  }
  if (offer.isError) {
    return (
      <>
        <PageTitle before={back}>{t("detail.title")}</PageTitle>
        <p className={styles.error} role="alert">
          {errorMessage(offer.error.message)}
        </p>
      </>
    );
  }
  if (!offer.data) {
    return (
      <>
        <PageTitle before={back}>{t("detail.title")}</PageTitle>
        <p>{t("detail.notFound")}</p>
      </>
    );
  }

  const o = offer.data;
  const money = (value: number | null | undefined, currency: string | null | undefined) =>
    value != null && currency ? format.money(value, currency) : "—";
  const summary = [
    // What is paid each month first: the price and the fees next to it.
    { label: t("field.price"), value: money(o.price, o.priceCurrency) },
    { label: t("field.rent"), value: money(o.rent, o.rentCurrency) },
    {
      label: t("field.pricePerM2"),
      value:
        o.pricePerM2 != null && o.priceCurrency
          ? format.wholeMoney(o.pricePerM2, o.priceCurrency)
          : "—",
    },
    { label: t("field.deposit"), value: money(o.deposit, o.depositCurrency) },
    { label: t("field.areaM2"), value: o.areaM2 != null ? format.area(o.areaM2) : "—" },
    { label: t("field.rooms"), value: o.rooms ?? "—" },
  ];
  const place =
    o.placeName && o.voivodeship
      ? t("place.name", { name: o.placeName, voivodeship: t(`voivodeship.${o.voivodeship}`) })
      : o.placeName;
  const date = (iso: string | null | undefined) => iso && format.date(iso);

  // The page fills the space under the top bar: the key numbers stay in view, and the two columns
  // below them (description on the left, the facts on the right) scroll each on its own.
  return (
    <article className={styles.page}>
      <PageTitle before={back} aside={place}>
        {o.title}
      </PageTitle>

      <div className={styles.bar}>
        <dl className={styles.summary}>
          {summary.map((item) => (
            <div key={item.label}>
              <dt>{item.label}</dt>
              <dd>{item.value}</dd>
            </div>
          ))}
        </dl>
        <a className={styles.portal} href={o.url} target="_blank" rel="noreferrer">
          {t("detail.portal", { source: o.source })}
          <Icon name="external" size={16} />
        </a>
      </div>

      <div className={styles.layout}>
        <div className={styles.main}>
          <OfferChanges offer={o} />
          <OfferDescription offer={o} />
        </div>
        <aside className={styles.facts}>
          <OfferDetails offer={o} />
          <OfferFeatures offer={o} />
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
