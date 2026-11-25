import { useState } from "react";
import { useTranslation } from "react-i18next";
import { Link } from "react-router";
import type { components } from "@shared/api/schema";
import { Panel } from "@shared/ui/Panel";
import { smallSample } from "./MarketSummary";
import styles from "./MarketPosition.module.css";
import { noFilter, readView, roomsOrMore, writeView, type OfferFilter } from "./offerView";
import { useFormat } from "./useFormat";
import { useSummary } from "./useSummary";

type Offer = components["schemas"]["OfferListItem"];
type Spread = components["schemas"]["Spread"];

const day = 24 * 60 * 60 * 1000;

// Within this share of the typical value an offer counts as typical, so noise does not show as a difference.
const typicalBand = 0.05;

// Where the offer stands among similar ones: the same room count in the same place, or in the voivodeship
// when the place has too few of them. Four values, each with a bar: the middle half of the similar offers,
// their median and this offer.
export function MarketPosition({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  // Fixed when the card opens, like the list's "last 24 h".
  const [now] = useState(() => Date.now());

  const rooms: Partial<OfferFilter> =
    o.rooms == null
      ? {}
      : o.rooms >= roomsOrMore
        ? { roomsMin: roomsOrMore }
        : { rooms: [o.rooms] };
  const placeFilter: OfferFilter = { ...noFilter, ...rooms, place: o.placeId ? [o.placeId] : [] };
  const voivodeshipFilter: OfferFilter = {
    ...noFilter,
    ...rooms,
    voivodeship: o.voivodeship ? [o.voivodeship] : [],
  };
  // The offer itself is left out of its group; a previous offer's values are never shown for this one.
  const local = useSummary({ ...placeFilter, excludeOffer: o.id }, o.placeId != null, false);
  const wider = local.data !== undefined && local.data.count < smallSample && o.voivodeship != null;
  const regional = useSummary({ ...voivodeshipFilter, excludeOffer: o.id }, wider, false);
  const summary = wider ? regional.data : local.data;
  const groupFilter = wider ? voivodeshipFilter : placeFilter;

  if (o.placeId == null) {
    return (
      <Panel title={t("market.title")}>
        <p className={styles.muted}>{t("market.noPlace")}</p>
      </Panel>
    );
  }
  if (summary === undefined) {
    return (
      <Panel title={t("market.title")}>
        <p className={styles.muted}>{t("market.loading")}</p>
      </Panel>
    );
  }

  const money = (value: number) => format.wholeMoney(value, "PLN");
  const days = (value: number) => t("market.days", { count: Math.round(value) });
  const listedSince = o.externalCreatedAt ?? o.createdAt;
  const rows = [
    {
      label: t("market.pricePerM2"),
      value: o.priceCurrency === "PLN" ? o.pricePerM2 : null,
      spread: summary.pricePerM2,
      show: money,
      better: "lower",
    },
    {
      label: t("market.rent"),
      value: o.rentCurrency === "PLN" ? o.rent : null,
      spread: summary.rent,
      show: money,
      better: "lower",
    },
    {
      label: t("market.area"),
      value: o.areaM2,
      spread: summary.areaM2,
      show: format.area,
      better: "higher",
    },
    {
      label: t("market.daysListed"),
      value: Math.floor((now - Date.parse(listedSince)) / day),
      spread: summary.daysListed,
      show: days,
      // A share of days says little ("−98%"); the bar and the typical value say it.
      better: null,
    },
  ] as const;
  const group = [
    o.rooms == null
      ? null
      : o.rooms >= roomsOrMore
        ? t("market.roomsOrMore", { count: roomsOrMore })
        : t("market.rooms", { count: o.rooms }),
    wider ? t(`voivodeship.${o.voivodeship!}`) : o.placeName,
  ]
    .filter(Boolean)
    .join(" · ");

  return (
    <Panel title={t("market.title")}>
      <div className={styles.rows} data-uncertain={summary.count < smallSample || undefined}>
        {rows.map(({ label, value, spread, show, better }) => (
          <div key={label} className={styles.row}>
            <span className={styles.label}>{label}</span>
            <span className={styles.value}>
              {value != null ? show(value) : "—"}
              {better && value != null && spread && spread.median !== 0 && (
                <span className={styles[judge(value, spread.median, better)]}>
                  {judge(value, spread.median, better) === "same"
                    ? t("market.same")
                    : formatDifference(value, spread.median)}
                </span>
              )}
            </span>
            {value != null && spread ? <Bar value={value} spread={spread} /> : <span />}
            <span className={styles.typicalValue}>
              {spread ? t("market.typical", { value: show(spread.median) }) : "—"}
            </span>
          </div>
        ))}
      </div>
      {/* Opens the list of these similar offers. */}
      <Link
        className={styles.group}
        to={{
          pathname: "..",
          search: `?${writeView({ ...readView(new URLSearchParams()), filter: groupFilter })}`,
        }}
        relative="path"
      >
        {summary.count < smallSample
          ? t("market.fewOffers", { group, count: summary.count })
          : t("market.group", { group, count: summary.count })}
      </Link>
    </Panel>
  );
}

// For the renter: "better" (e.g. a lower price per m², a bigger apartment), "worse", or "same" within the band.
// The colors are those of a price change: green better, red worse.
function judge(value: number, median: number, better: "lower" | "higher") {
  const difference = (value - median) / median;
  if (Math.abs(difference) <= typicalBand) {
    return "same";
  }
  return difference < 0 === (better === "lower") ? "better" : "worse";
}

// How far from the median: "+9%", "−12%".
function formatDifference(value: number, median: number) {
  const difference = (value - median) / median;
  return `${difference > 0 ? "+" : "−"}${Math.round(Math.abs(difference) * 100)}%`;
}

// The scale spans the middle half of the similar offers and this offer, with a margin on both sides.
function Bar({ value, spread }: { value: number; spread: Spread }) {
  const low = Math.min(spread.low, value);
  const high = Math.max(spread.high, value);
  const margin = (high - low) * 0.15 || 1;
  const at = (x: number) => `${((x - (low - margin)) / (high - low + 2 * margin)) * 100}%`;
  return (
    <span className={styles.bar} aria-hidden="true">
      <span
        className={styles.middle}
        style={{
          left: at(spread.low),
          width: `calc(${at(spread.high)} - ${at(spread.low)})`,
        }}
      />
      <span className={styles.median} style={{ left: at(spread.median) }} />
      <span className={styles.offer} style={{ left: at(value) }} />
    </span>
  );
}
