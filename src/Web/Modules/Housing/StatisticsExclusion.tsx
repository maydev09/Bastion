import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { Icon } from "@shared/ui/Icon";
import styles from "./Housing.module.css";
import { roomsOrMore } from "./offerView";
import { useFormat } from "./useFormat";

type Offer = components["schemas"]["OfferListItem"];

// Under the title in the list: the offer stays, but the statistics leave it out, and why.
export function ExclusionMark({ offer }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  if (!offer.statisticsExclusion) {
    return null;
  }
  return (
    <small className={styles.exclusion} title={t("exclusion.markHint")}>
      <Icon name="flag" size={14} />
      {t("exclusion.mark", { reason: t(`exclusion.reason.${offer.statisticsExclusion.reason}`) })}
    </small>
  );
}

// On the offer card: why the statistics leave the offer out, and the usual prices per m² of the group it was compared with.
export function ExclusionNote({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const group = useExclusionGroup();
  const exclusion = o.statisticsExclusion;
  if (!exclusion) {
    return null;
  }
  const { fenceLowPerM2: low, fenceHighPerM2: high, currency, sampleSize } = exclusion;
  // Whole złoty: the fences are a rough range, grosze would only clutter it.
  const perM2 = (value: number, unit: string) => format.perM2(Math.round(value), unit);
  return (
    <div className={styles.exclusionNote} role="note">
      <Icon name="flag" size={16} />
      <p>
        {t(`exclusion.note.${exclusion.reason}`)}
        {low != null &&
          high != null &&
          currency &&
          sampleSize != null &&
          o.pricePerM2 != null &&
          ` ${t("exclusion.fences", {
            group: group(o),
            low: perM2(low, currency),
            high: perM2(high, currency),
            count: sampleSize,
            value: perM2(o.pricePerM2, currency),
          })}`}
      </p>
    </div>
  );
}

// The group by the offer's own place and rooms: "Katowice, 1 pokój", "małe miasto, woj. śląskie, 2 pokoje".
function useExclusionGroup() {
  const { t } = useTranslation("housing");
  return (o: Offer) => {
    const rooms =
      o.rooms == null
        ? t("exclusion.roomsUnknown")
        : o.rooms >= roomsOrMore
          ? t("market.roomsOrMore", { count: roomsOrMore })
          : t("market.rooms", { count: o.rooms });
    const size = o.placeSize ? t(`place.size.${o.placeSize}`) : null;
    const voivodeship = o.voivodeship
      ? t("cost.voivodeship", { name: t(`voivodeship.${o.voivodeship}`) })
      : null;
    const parts = (() => {
      switch (o.statisticsExclusion?.basis) {
        case "Place":
          return [o.placeName];
        case "VoivodeshipPlaceSize":
          return [size, voivodeship];
        case "PlaceSize":
          return [size, t("cost.country")];
        default:
          return [t("cost.country")];
      }
    })();
    return [...parts, rooms].filter(Boolean).join(", ");
  };
}
