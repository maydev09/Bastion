import { useTranslation } from "react-i18next";
import type { components } from "@shared/api/schema";
import { Collapsible } from "@shared/ui/Collapsible";
import type pl from "./locales/pl.json";
import styles from "./OfferDetail.module.css";
import { useFormat } from "./useFormat";

type Schemas = components["schemas"];
type FieldChange = Schemas["FieldChange"];
type ChangedField = keyof typeof pl.field;

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
      case "furnished":
        return t(`answer.${value as NonNullable<Schemas["Answer"]>}`);
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

  // Description and features changes take the before, after and difference cells together.
  function renderValues(change: FieldChange) {
    if (change.field === "description") {
      return (
        <td colSpan={3} className={styles.muted}>
          {t("changes.descriptionChanged")}
        </td>
      );
    }
    if (change.field === "features") {
      return (
        <td colSpan={3}>
          {describeFeatures(
            change.before as Schemas["Feature"][],
            (change.after as Schemas["Feature"][] | null) ?? [],
          )}
        </td>
      );
    }
    const currency = findCurrency(change.field);
    const difference =
      currency && typeof change.before === "number" && typeof change.after === "number"
        ? change.after - change.before
        : 0;
    return (
      <>
        <td className={styles.muted}>{formatValue(change.field, change.before)}</td>
        <td>{formatValue(change.field, change.after)}</td>
        <td className={styles.number}>
          {currency && difference !== 0 && (
            <span className={difference < 0 ? styles.down : styles.up}>
              {difference > 0 ? "+" : "−"}
              {format.money(Math.abs(difference), currency)}
            </span>
          )}
        </td>
      </>
    );
  }

  return (
    // Open at first; it can be closed to give the description more room.
    <Collapsible
      title={t("changes.title")}
      hint={changes.length > 0 ? t("changes.count", { count: changes.length }) : undefined}
      defaultOpen
      flush
    >
      {changes.length === 0 ? (
        <p className={styles.notice}>{t("changes.none")}</p>
      ) : (
        <div className={styles.changesScroll}>
          <table className={styles.changes}>
            <thead>
              <tr>
                <th>{t("changes.column.date")}</th>
                <th>{t("changes.column.field")}</th>
                <th>{t("changes.column.before")}</th>
                <th>{t("changes.column.after")}</th>
                <th className={styles.number}>{t("changes.column.difference")}</th>
              </tr>
            </thead>
            {changes.map((change) => (
              <tbody key={change.id}>
                {change.changes.map((c, index) => (
                  <tr key={c.field}>
                    {index === 0 && (
                      <td rowSpan={change.changes.length} className={styles.changedAt}>
                        <time dateTime={change.changedAt}>
                          {format.shortDate(change.changedAt)}
                        </time>
                      </td>
                    )}
                    <td>{t(`field.${c.field as ChangedField}`)}</td>
                    {renderValues(c)}
                  </tr>
                ))}
              </tbody>
            ))}
          </table>
        </div>
      )}
    </Collapsible>
  );
}
