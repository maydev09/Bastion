import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import styles from "./MarketSummary.module.css";
import { type OfferFilter } from "./offerView";
import { useSummary } from "./useSummary";
import { useFormat } from "./useFormat";

type Spread = components["schemas"]["Spread"];

// Fewer offers than this make a typical value a guess, so it is shown as uncertain.
export const smallSample = 10;

// How many offers a value comes from is said only when more than a tenth of the offers lack it (e.g. the rent).
const mostOffers = 0.9;

// What the offers of the current filters typically cost and offer: the median, and the range
// of the middle half of them. Changing a filter shows at once how the market moves.
export function MarketSummary({ filter }: { filter: OfferFilter }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const summary = useSummary(filter);
  if (!summary.data || summary.data.count === 0) {
    return null;
  }

  const { count } = summary.data;
  const money = (value: number) => format.wholeMoney(value, "PLN");
  const whole = (value: number) => format.number(Math.round(value));
  // The low end of a range is a bare number and the high end carries the unit: "2 300–3 000 zł".
  const tiles = [
    { title: t("summary.price"), spread: summary.data.price, show: money, low: whole },
    { title: t("summary.pricePerM2"), spread: summary.data.pricePerM2, show: money, low: whole },
    { title: t("summary.rent"), spread: summary.data.rent, show: money, low: whole },
    {
      title: t("summary.totalCost"),
      spread: summary.data.totalCost,
      show: money,
      low: whole,
      // How many totals add a rent that is not the stated one.
      note: t("summary.rentSources", {
        estimated: summary.data.rentSources.Estimated ?? 0,
        description: summary.data.rentSources.Description ?? 0,
      }),
    },
    {
      title: t("summary.area"),
      spread: summary.data.areaM2,
      show: format.area,
      low: format.number,
    },
  ];

  return (
    <dl className={styles.summary} data-busy={summary.isPlaceholderData}>
      {tiles.map(({ title, spread, show, low, note }) => (
        <div key={title} className={styles.tile} data-uncertain={isUncertain(spread) || undefined}>
          <dt>{title}</dt>
          <dd className={styles.value}>{spread ? show(spread.median) : "—"}</dd>
          {spread && (
            <dd className={styles.detail}>
              {spread.count < smallSample
                ? t("summary.few", { count: spread.count })
                : t("summary.middle", { low: low(spread.low), high: show(spread.high) })}
              {spread.count >= smallSample &&
                spread.count < count * mostOffers &&
                ` · ${t("summary.from", { count: spread.count })}`}
            </dd>
          )}
          {spread && note && <dd className={styles.detail}>{note}</dd>}
        </div>
      ))}
    </dl>
  );
}

function isUncertain(spread: Spread | null | undefined) {
  return spread != null && spread.count < smallSample;
}
