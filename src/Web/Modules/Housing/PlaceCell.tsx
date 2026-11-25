import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import styles from "./Housing.module.css";
import { useFormat } from "./useFormat";

type Offer = components["schemas"]["OfferListItem"];

// "Złotów" (or "Warszawa, Mokotów") and below it "małe miasto · siedziba pow.". The neighborhood, the voivodeship,
// population and the big cities around show on hover and on the offer card.
export function PlaceCell({ offer }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();

  if (!offer.placeName) {
    return <span className={styles.muted}>—</span>;
  }

  const size = offer.placeSize
    ? t(`place.size.${offer.placeSize}`)
    : offer.placeKind && t(`place.kind.${offer.placeKind}`);
  // The list shows the role shortened ("stolica woj."), so the line fits the column; hover shows it whole.
  const kind = [size, offer.placeRole && t(`place.roleShort.${offer.placeRole}`)]
    .filter(Boolean)
    .join(" · ");
  const name = offer.district
    ? t("place.withDistrict", { place: offer.placeName, district: offer.district })
    : offer.placeName;
  const details = [
    [offer.district, offer.neighborhood].filter(Boolean).join(", "),
    offer.voivodeship
      ? t("place.name", {
          name: offer.placeName,
          voivodeship: t(`voivodeship.${offer.voivodeship}`),
        })
      : offer.placeName,
    [
      size,
      offer.placeRole && t(`place.role.${offer.placeRole}`),
      offer.population != null &&
        t(offer.populationYear ? "place.population" : "place.populationNoYear", {
          population: format.number(offer.population),
          year: offer.populationYear,
          source: offer.populationSource,
        }),
    ]
      .filter(Boolean)
      .join(" · "),
    (offer.nearby ?? [])
      .filter((n) => n.tier === "BigCity")
      .map((c) => t("place.nearestCity", { city: c.name, km: format.km(c.km) }))
      .join(", "),
  ].filter(Boolean);

  return (
    <span title={details.join("\n")}>
      {name}
      {kind && (
        <>
          <br />
          <small className={styles.muted}>{kind}</small>
        </>
      )}
    </span>
  );
}
