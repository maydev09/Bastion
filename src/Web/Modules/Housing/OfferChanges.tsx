import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import type pl from "./locales/pl.json";
import styles from "./OfferDetail.module.css";
import { useFormat } from "./useFormat";

type Schemas = components["schemas"];
type FieldChange = Schemas["FieldChange"];
type ChangedField = keyof typeof pl.changes.field;

export function OfferChanges({ offer: o }: { offer: Schemas["OfferListItem"] }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const changes = o.changes ?? [];

  // Money changes are shown in the offer's currency; null for other fields.
  function findCurrency(field: string) {
    switch (field) {
      case "price":
        return o.priceCurrency ?? "PLN";
      case "rent":
        return o.rentCurrency ?? "PLN";
      case "deposit":
        return o.depositCurrency ?? "PLN";
      default:
        return null;
    }
  }

  function formatValue(field: string, value: unknown) {
    if (value == null) {
      return "—";
    }
    const currency = findCurrency(field);
    if (currency) {
      return format.money(Number(value), currency);
    }
    switch (field) {
      case "areaM2":
        return format.area(Number(value));
      case "availableFrom":
        return format.day(String(value));
      case "floor":
        return format.floor(Number(value));
      case "locationRadiusM":
        return t("units.m", { value: format.number(Number(value)) });
      case "buildingType":
        return t(`detail.buildingType.${value as NonNullable<Schemas["BuildingType"]>}`);
      case "heating":
        return t(`detail.heating.${value as NonNullable<Schemas["Heating"]>}`);
      case "condition":
        return t(`detail.condition.${value as NonNullable<Schemas["Condition"]>}`);
      default:
        return String(value);
    }
  }

  function describeFeatures(before: Schemas["Feature"][], after: Schemas["Feature"][]) {
    const added = after.filter((f) => !before.includes(f)).map((f) => t(`detail.feature.${f}`));
    const removed = before.filter((f) => !after.includes(f)).map((f) => t(`detail.feature.${f}`));
    return [
      added.length > 0 && t("changes.added", { list: added.join(", ") }),
      removed.length > 0 && t("changes.removed", { list: removed.join(", ") }),
    ]
      .filter(Boolean)
      .join(" · ");
  }

  function renderChange(change: FieldChange) {
    if (change.field === "description") {
      return <span className={styles.muted}>{t("changes.descriptionChanged")}</span>;
    }
    if (change.field === "features") {
      return describeFeatures(
        change.before as Schemas["Feature"][],
        (change.after as Schemas["Feature"][] | null) ?? [],
      );
    }
    const currency = findCurrency(change.field);
    const difference =
      currency && typeof change.before === "number" && typeof change.after === "number"
        ? change.after - change.before
        : 0;
    return (
      <>
        <span className={styles.muted}>{formatValue(change.field, change.before)}</span> →{" "}
        <strong>{formatValue(change.field, change.after)}</strong>
        {currency && difference !== 0 && (
          <span className={difference < 0 ? styles.down : styles.up}>
            {difference > 0 ? "+" : "−"}
            {format.money(Math.abs(difference), currency)}
          </span>
        )}
      </>
    );
  }

  return (
    <section className={styles.card} aria-labelledby="changes">
      <h2 id="changes">{t("changes.title")}</h2>
      {changes.length === 0 ? (
        <p className={styles.muted}>{t("changes.none")}</p>
      ) : (
        <ol className={styles.changes}>
          {changes.map((change) => (
            <li key={change.id}>
              <time dateTime={change.changedAt} className={styles.changedAt}>
                {format.date(change.changedAt)}
              </time>
              <dl>
                {change.changes.map((c) => (
                  <div key={c.field}>
                    <dt>{t(`changes.field.${c.field as ChangedField}`)}</dt>
                    <dd>{renderChange(c)}</dd>
                  </div>
                ))}
              </dl>
            </li>
          ))}
        </ol>
      )}
    </section>
  );
}
