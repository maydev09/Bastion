import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import styles from "./Housing.module.css";
import { useFormat } from "./useFormat";

type Offer = components["schemas"]["OfferListItem"];

// "Złotów, wielkopolskie" and below "małe miasto · siedziba powiatu · 18 768 mieszk. (2021, Wikidata)",
// then the big cities around: "Sosnowiec – 5 km, Katowice – 17 km, Chorzów – 18 km".
export function PlaceCell({ offer }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();

  if (!offer.placeName) {
    return <span className={styles.muted}>—</span>;
  }

  const details = [
    offer.placeSize
      ? t(`place.size.${offer.placeSize}`)
      : offer.placeKind && t(`place.kind.${offer.placeKind}`),
    offer.placeRole && t(`place.role.${offer.placeRole}`),
    offer.population != null &&
      t(offer.populationYear ? "place.population" : "place.populationNoYear", {
        population: format.number(offer.population),
        year: offer.populationYear,
        source: offer.populationSource,
      }),
  ].filter(Boolean);
  const cities = (offer.nearby ?? [])
    .filter((n) => n.tier === "BigCity")
    .map((c) => t("place.nearestCity", { city: c.name, km: format.km(c.km) }))
    .join(", ");

  return (
    <>
      {offer.voivodeship
        ? t("place.name", {
            name: offer.placeName,
            voivodeship: t(`voivodeship.${offer.voivodeship}`),
          })
        : offer.placeName}
      {details.length > 0 && (
        <>
          <br />
          <small className={styles.muted}>{details.join(" · ")}</small>
        </>
      )}
      {cities && (
        <>
          <br />
          <small className={styles.muted}>{cities}</small>
        </>
      )}
    </>
  );
}
