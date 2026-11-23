import { useQuery, useQueryClient } from "@tanstack/react-query";
import { useState } from "react";
import { useTranslation } from "react-i18next";
import { useLocation, useNavigate, useParams } from "react-router";
import { api } from "@shared/api/client";
import { Icon } from "@shared/ui/Icon";
import { IconButton } from "@shared/ui/IconButton";
import { LinkButton } from "@shared/ui/LinkButton";
import { PageTitle } from "@shared/ui/PageTitle";
import { Panel } from "@shared/ui/Panel";
import { PropertyList } from "@shared/ui/PropertyList";
import { useErrorMessage } from "@shared/useErrorMessage";
import { useShortcuts } from "@shared/useShortcuts";
import styles from "./OfferDetail.module.css";
import { MarketPosition } from "./MarketPosition";
import { OfferChanges } from "./OfferChanges";
import { OfferDescription } from "./OfferDescription";
import { OfferDetails } from "./OfferDetails";
import { OfferFeatures } from "./OfferFeatures";
import { OfferPlace } from "./OfferPlace";
import { offersQuery } from "./offersQuery";
import { readView, writeView } from "./offerView";
import { rememberOpenedOffer } from "./openedOffer";
import { offersQueryKey } from "./queryKeys";
import { ShortcutsHelp } from "./ShortcutsHelp";
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
  const queryClient = useQueryClient();
  const [helpOpen, setHelpOpen] = useState(false);

  // from: the list view (filters, page, sort order) the card belongs to. listBehind: the list view of the
  // history entry before the card; moving to the previous or next offer replaces the card's entry, so it
  // differs from "from" only when that move crossed into another page of the list.
  const state = location.state as { from?: unknown; listBehind?: unknown } | null;
  const from = typeof state?.from === "string" ? state.from : null;
  const listBehind = typeof state?.listBehind === "string" ? state.listBehind : from;

  // Back to the list. When the entry before the card is that list view, it is the browser's back step, so
  // the list is scrolled as it was; the link still works when the card was opened in a new tab.
  const goBack = () => {
    if (from === null) {
      void navigate("..", { relative: "path" });
    } else if (from === listBehind) {
      void navigate(-1);
    } else {
      void navigate({ pathname: "..", search: from }, { relative: "path", replace: true });
    }
  };
  const back = (
    <LinkButton
      to={{ pathname: "..", search: from ?? "" }}
      relative="path"
      onClick={(event) => {
        event.preventDefault();
        goBack();
      }}
    >
      <Icon name="previous" size={18} />
      {t("detail.back")}
    </LinkButton>
  );

  // The previous and the next offer of the list the card was opened from, also across its pages.
  // The list's page is usually cached already, so moving needs no request.
  const listView = from !== null ? readView(new URLSearchParams(from)) : null;
  const list = useQuery({
    ...offersQuery(listView ?? readView(new URLSearchParams())),
    enabled: listView !== null,
  });
  const index = list.data?.items.findIndex((item) => item.id === id) ?? -1;
  const canGo = (step: -1 | 1) =>
    listView !== null &&
    list.data !== undefined &&
    index >= 0 &&
    (list.data.items[index + step] !== undefined ||
      (step < 0 ? listView.page > 1 : listView.page * listView.pageSize < list.data.totalCount));
  const go = async (step: -1 | 1) => {
    if (!canGo(step) || listView === null || list.data === undefined) {
      return;
    }
    let view = listView;
    // Only the id is needed; the next page is read when the move leaves this one.
    let target: { id: number } | undefined = list.data.items[index + step];
    if (target === undefined) {
      view = { ...listView, page: listView.page + step };
      const page = await queryClient.fetchQuery(offersQuery(view));
      target = step > 0 ? page.items[0] : page.items.at(-1);
    }
    if (target !== undefined) {
      rememberOpenedOffer(target.id);
      void navigate(`../${target.id}`, {
        relative: "path",
        replace: true,
        state: { from: `?${writeView(view).toString()}`, listBehind },
      });
    }
  };
  const neighbours = (
    <>
      <IconButton
        icon="previous"
        label={t("detail.previous")}
        disabled={!canGo(-1)}
        onClick={() => void go(-1)}
      />
      <IconButton
        icon="next"
        label={t("detail.next")}
        disabled={!canGo(1)}
        onClick={() => void go(1)}
      />
    </>
  );
  const help = (
    <>
      <IconButton icon="keyboard" label={t("shortcuts.show")} onClick={() => setHelpOpen(true)} />
      <ShortcutsHelp open={helpOpen} onOpenChange={setHelpOpen} />
    </>
  );

  useShortcuts(
    {
      ArrowLeft: () => void go(-1),
      ArrowRight: () => void go(1),
      Escape: goBack,
      o: () => offer.data && window.open(offer.data.url, "_blank", "noopener"),
      "?": () => setHelpOpen(true),
    },
    !helpOpen,
  );

  if (offer.isPending) {
    return <p className={styles.muted}>{t("detail.loading")}</p>;
  }
  if (offer.isError) {
    return (
      <>
        <PageTitle before={back} actions={help}>
          {t("detail.title")}
        </PageTitle>
        <p className={styles.error} role="alert">
          {errorMessage(offer.error.message)}
        </p>
      </>
    );
  }
  if (!offer.data) {
    return (
      <>
        <PageTitle before={back} actions={help}>
          {t("detail.title")}
        </PageTitle>
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
      <PageTitle
        before={
          <>
            {back}
            {neighbours}
          </>
        }
        aside={place}
        actions={help}
      >
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
          <MarketPosition offer={o} />
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
