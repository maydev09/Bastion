import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import styles from "./Housing.module.css";
import { useFormat } from "./useFormat";

type Offer = components["schemas"]["OfferListItem"];
type RentBasis = components["schemas"]["RentBasis"];

// The fees in the list as the monthly total uses them, marked when they are not simply the stated ones: estimated (with
// the stated value when it looked like a mistake) or read from the description. A 0 or 1 zł with nothing in the description
// is simply no rent.
export function RentCell({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const cost = o.monthlyCost;
  const stated = o.rent != null && o.rentCurrency ? format.money(o.rent, o.rentCurrency) : null;
  if (!cost?.currency) {
    return stated ?? "—";
  }
  const used = format.money(cost.rent, cost.currency);
  const withNote = (value: string, note: string) => (
    <>
      {value}
      <br />
      <small className={styles.muted}>{note}</small>
    </>
  );
  switch (cost.rentSource) {
    case "Estimated":
      return withNote(
        `≈ ${used}`,
        stated ? t("cost.statedShort", { rent: stated }) : t("cost.estimatedShort"),
      );
    case "Description":
      return withNote(used, t("cost.descriptionShort"));
    case "Included":
      return withNote(used, t("cost.includedShort"));
    default:
      return used;
  }
}

// The monthly total in the list; an estimated fee makes it an estimate too.
export function TotalCostCell({ offer: o }: { offer: Offer }) {
  const format = useFormat();
  const cost = o.monthlyCost;
  if (!cost?.currency) {
    return "—";
  }
  const total = format.money(cost.total, cost.currency);
  return cost.rentSource === "Estimated" ? `≈ ${total}` : total;
}

// Where the fees in the monthly total of the offer card come from, when they are not simply the stated ones.
export function RentNote({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const group = useRentGroup();
  const cost = o.monthlyCost;
  if (!cost?.currency) {
    return null;
  }
  const currency = cost.currency;
  const used = format.money(cost.rent, currency);
  const stated = o.rent != null && o.rentCurrency ? format.money(o.rent, o.rentCurrency) : null;
  const estimate = cost.estimate;
  switch (cost.rentSource) {
    case "Description":
      return (
        <p className={styles.muted}>
          {t("cost.description", { rent: used, quote: cost.rentQuote })}
          {stated && ` ${t("cost.field", { rent: stated })}`}
        </p>
      );
    case "Included":
      return <p className={styles.muted}>{t("cost.included", { quote: cost.rentQuote })}</p>;
    case "Estimated": {
      if (!estimate || o.areaM2 == null) {
        return null;
      }
      const perM2 = (value: number) => format.perM2(value, currency);
      return (
        <div className={styles.muted}>
          <p>
            {stated && `${t("cost.implausible", { rent: stated })} `}
            {t("cost.estimate", {
              rent: format.wholeMoney(cost.rent, currency),
              rate: perM2(estimate.ratePerM2),
              area: format.area(o.areaM2),
            })}
          </p>
          <ul>
            {estimate.neighbors > 0 && estimate.neighborsPerM2 != null && (
              <li>
                {t(o.buildingType ? "cost.neighborsOfType" : "cost.neighbors", {
                  count: estimate.neighbors,
                  km: format.number(estimate.neighborsKm ?? 0),
                  rate: perM2(estimate.neighborsPerM2),
                  type: o.buildingType ? t(`detail.buildingType.${o.buildingType}`) : "",
                })}
              </li>
            )}
            {estimate.basisPerM2 != null && (
              <li>
                {t("cost.group", {
                  group: group(o, estimate.basis),
                  rate: perM2(estimate.basisPerM2),
                  count: estimate.sampleSize ?? 0,
                })}
              </li>
            )}
            <li>{t("cost.areaNote")}</li>
          </ul>
        </div>
      );
    }
    default:
      return null;
  }
}

// The most local group an estimated rent took a rate from, named by the offer's own place, district or neighborhood and
// building: "Warszawa, Mokotów · blok".
function useRentGroup() {
  const { t } = useTranslation("housing");
  return (o: Offer, basis: RentBasis | null | undefined) => {
    const building = o.buildingType ? t(`detail.buildingType.${o.buildingType}`) : null;
    const voivodeship = o.voivodeship
      ? t("cost.voivodeship", { name: t(`voivodeship.${o.voivodeship}`) })
      : null;
    const parts = (() => {
      switch (basis) {
        case "NeighborhoodBuildingType":
          return [[o.placeName, o.neighborhood].filter(Boolean).join(", "), building];
        case "Neighborhood":
          return [[o.placeName, o.neighborhood].filter(Boolean).join(", ")];
        case "DistrictBuildingType":
          return [[o.placeName, o.district].filter(Boolean).join(", "), building];
        case "District":
          return [[o.placeName, o.district].filter(Boolean).join(", ")];
        case "PlaceBuildingType":
          return [o.placeName, building];
        case "Place":
          return [o.placeName];
        case "VoivodeshipBuildingType":
          return [voivodeship, building];
        case "Voivodeship":
          return [voivodeship];
        default:
          return [t("cost.country")];
      }
    })();
    return parts.filter(Boolean).join(" · ");
  };
}
