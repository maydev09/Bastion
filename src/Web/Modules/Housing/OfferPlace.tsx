import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { Panel } from "@shared/ui/Panel";
import { PropertyList } from "@shared/ui/PropertyList";
import styles from "./OfferDetail.module.css";
import { useFormat } from "./useFormat";

type Offer = components["schemas"]["OfferListItem"];

// Where the offer is: its place with official data, the point on the map and the bigger places around.
export function OfferPlace({ offer: o }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();

  // One row per place: Wejherowo can be both the nearest town and the county seat.
  const byName = new Map<string, { name: string; km: number; tiers: string[] }>();
  for (const n of o.nearby ?? []) {
    const row = byName.get(n.name) ?? { name: n.name, km: n.km, tiers: [] };
    row.tiers.push(t(`detail.tier.${n.tier}`));
    byName.set(n.name, row);
  }
  const nearby = [...byName.values()].sort((a, b) => a.km - b.km);
  const ownFeatures = [
    o.placeRole && t(`place.role.${o.placeRole}`),
    o.placeSize && t(`place.size.${o.placeSize}`),
  ]
    .filter(Boolean)
    .join(", ");

  const map =
    o.latitude != null && o.longitude != null ? (
      <>
        <a
          href={`https://www.openstreetmap.org/?mlat=${o.latitude}&mlon=${o.longitude}#map=16/${o.latitude}/${o.longitude}`}
          target="_blank"
          rel="noreferrer"
        >
          {t("detail.map")} ↗
        </a>
        {o.locationRadiusM ? (
          <span className={styles.muted}>
            {" "}
            {t("detail.mapApproximate", {
              km: format.number(Math.max(o.locationRadiusM / 1000, 0.1)),
            })}
          </span>
        ) : null}
      </>
    ) : null;
  const population = o.population != null && (
    <>
      {format.number(o.population)}{" "}
      <span className={styles.muted}>
        ({[o.populationYear, o.populationSource].filter(Boolean).join(", ")})
      </span>
    </>
  );

  return (
    <>
      <Panel title={t("detail.location")} flush>
        {!o.placeName && <p className={styles.notice}>{t("detail.unknownPlace")}</p>}
        <PropertyList
          items={[
            { label: t("detail.place"), value: o.placeName },
            { label: t("detail.municipality"), value: o.municipality },
            { label: t("detail.county"), value: o.county },
            {
              label: t("detail.voivodeship"),
              value: o.voivodeship && t(`voivodeship.${o.voivodeship}`),
            },
            {
              label: t("detail.placeSize"),
              value: o.placeSize
                ? t(`place.size.${o.placeSize}`)
                : o.placeKind && t(`place.kind.${o.placeKind}`),
            },
            { label: t("detail.placeRole"), value: o.placeRole && t(`place.role.${o.placeRole}`) },
            { label: t("detail.population"), value: population },
            { label: t("detail.point"), value: map },
          ]}
        />
      </Panel>

      {o.placeName && (
        <Panel title={t("detail.nearby")} flush>
          {nearby.length > 0 ? (
            <>
              <ol className={styles.nearby}>
                {nearby.map((n) => (
                  <li key={n.name}>
                    <span>
                      {n.name}
                      <small className={styles.muted}>{n.tiers.join(", ")}</small>
                    </span>
                    <span className={styles.km}>{t("units.km", { value: format.km(n.km) })}</span>
                  </li>
                ))}
              </ol>
              <p className={styles.hint}>{t("detail.nearbyHint")}</p>
            </>
          ) : (
            <p className={styles.notice}>{t("detail.selfSufficient", { what: ownFeatures })}</p>
          )}
        </Panel>
      )}
    </>
  );
}
