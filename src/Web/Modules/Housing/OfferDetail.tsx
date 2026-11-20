import { useQuery } from "@tanstack/react-query";
import { useTranslation } from "react-i18next";
import { Link, useParams } from "react-router";
import { api } from "@shared/api/client";
import { useErrorMessage } from "@shared/useErrorMessage";
import styles from "./OfferDetail.module.css";
import { offersQueryKey } from "./queryKeys";

export function OfferDetail() {
  const { t, i18n } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const id = Number(useParams().id);
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

  const language = i18n.resolvedLanguage ?? "pl";
  const number = new Intl.NumberFormat(language);
  const km = new Intl.NumberFormat(language, { maximumFractionDigits: 0 });
  const date = new Intl.DateTimeFormat(language, { dateStyle: "medium", timeStyle: "short" });
  const money = (value: number, currency: string) =>
    new Intl.NumberFormat(language, { style: "currency", currency }).format(value);

  const back = (
    <Link to="../.." relative="path" className={styles.back}>
      ← {t("detail.back")}
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
  // One row per place: Wejherowo can be both the nearest town and the county seat.
  const byName = new Map<string, { name: string; km: number; tiers: string[] }>();
  for (const n of o.nearby ?? []) {
    const row = byName.get(n.name) ?? { name: n.name, km: n.km, tiers: [] };
    row.tiers.push(t(`detail.tier.${n.tier}`));
    byName.set(n.name, row);
  }
  const nearby = [...byName.values()].sort((a, b) => a.km - b.km);
  const farthest = Math.max(...nearby.map((n) => n.km), 1);
  const ownFeatures = [
    o.placeRole && t(`place.role.${o.placeRole}`),
    o.placeSize && t(`place.size.${o.placeSize}`),
  ]
    .filter(Boolean)
    .join(", ");
  const facts = [
    {
      label: t("list.price"),
      value: o.price != null && o.priceCurrency ? money(o.price, o.priceCurrency) : "—",
    },
    { label: t("list.area"), value: o.areaM2 != null ? `${number.format(o.areaM2)} m²` : "—" },
    { label: t("list.rooms"), value: o.rooms ?? "—" },
    {
      label: t("list.firstSeen"),
      value: o.externalCreatedAt ? date.format(new Date(o.externalCreatedAt)) : "—",
    },
  ];

  return (
    <article className={styles.detail}>
      {back}
      <header className={styles.header}>
        <h1>{o.title}</h1>
        <a className={styles.portal} href={o.url} target="_blank" rel="noreferrer">
          {t("detail.portal", { source: o.source })} ↗
        </a>
      </header>

      <dl className={styles.facts}>
        {facts.map((f) => (
          <div key={f.label} className={styles.fact}>
            <dt>{f.label}</dt>
            <dd>{f.value}</dd>
          </div>
        ))}
      </dl>

      <div className={styles.columns}>
        <section className={styles.card} aria-labelledby="place">
          <h2 id="place">{t("detail.place")}</h2>
          {o.placeName ? (
            <>
              <p className={styles.placeName}>{o.placeName}</p>
              <p className={styles.muted}>
                {[
                  o.voivodeship && t(`voivodeship.${o.voivodeship}`),
                  o.county && t("detail.county", { name: o.county }),
                  o.municipality && t("detail.municipality", { name: o.municipality }),
                ]
                  .filter(Boolean)
                  .join(" · ")}
              </p>
              <p className={styles.badges}>
                {o.placeSize ? (
                  <span className={styles.badge}>{t(`place.size.${o.placeSize}`)}</span>
                ) : (
                  o.placeKind && (
                    <span className={styles.badge}>{t(`place.kind.${o.placeKind}`)}</span>
                  )
                )}
                {o.placeRole && (
                  <span className={styles.badgeRole}>{t(`place.role.${o.placeRole}`)}</span>
                )}
              </p>
              {o.population != null && (
                <p>
                  <strong>{number.format(o.population)}</strong> {t("detail.residents")}{" "}
                  <span className={styles.muted}>
                    ({[o.populationYear, o.populationSource].filter(Boolean).join(", ")})
                  </span>
                </p>
              )}
            </>
          ) : (
            <p className={styles.muted}>{t("detail.unknownPlace")}</p>
          )}
        </section>

        <section className={styles.card} aria-labelledby="nearby">
          <h2 id="nearby">{t("detail.nearby")}</h2>
          {nearby.length > 0 ? (
            <>
              <ol className={styles.cities}>
                {nearby.map((n) => (
                  <li key={n.name}>
                    <span>
                      <span className={styles.cityName}>{n.name}</span>
                      <br />
                      <small className={styles.muted}>{n.tiers.join(", ")}</small>
                    </span>
                    <span className={styles.bar} aria-hidden="true">
                      <span style={{ width: `${(n.km / farthest) * 100}%` }} />
                    </span>
                    <span className={styles.km}>{km.format(n.km)} km</span>
                  </li>
                ))}
              </ol>
              <p className={styles.hint}>{t("detail.nearbyHint")}</p>
            </>
          ) : (
            <p className={styles.muted}>
              {o.placeName
                ? t("detail.selfSufficient", { what: ownFeatures })
                : t("detail.unknownPlace")}
            </p>
          )}
        </section>
      </div>
    </article>
  );
}
