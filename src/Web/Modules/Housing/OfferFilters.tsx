import { useTranslation } from "react-i18next";
import { Button } from "@shared/ui/Button";
import { Checkbox } from "@shared/ui/Checkbox";
import { IconButton } from "@shared/ui/IconButton";
import { MultiSelect } from "@shared/ui/MultiSelect";
import { NumberInput } from "@shared/ui/NumberInput";
import { Select } from "@shared/ui/Select";
import styles from "./OfferFilters.module.css";
import {
  answers,
  countFilters,
  noFilter,
  townSizes,
  villageSizes,
  voivodeships,
  type OfferFilter,
} from "./offerView";
import { PlaceFilter } from "./PlaceFilter";
import { useFeatureGroups } from "./useFeatures";

type Props = {
  filter: OfferFilter;
  onChange: (filter: OfferFilter) => void;
  onHide: () => void;
};

// Every change applies at once; the list and the link follow it.
export function OfferFilters({ filter, onChange, onHide }: Props) {
  const { t } = useTranslation("housing");
  const set = (changes: Partial<OfferFilter>) => onChange({ ...filter, ...changes });
  const toggle = <T,>(values: T[], value: T, on: boolean) =>
    on ? [...values, value] : values.filter((v) => v !== value);
  const count = countFilters(filter);
  const featureGroups = useFeatureGroups();
  const voivodeshipItems = voivodeships.map((v) => ({ value: v, label: t(`voivodeship.${v}`) }));

  return (
    <aside className={styles.panel} aria-label={t("filters.title")}>
      <div className={styles.header}>
        <IconButton icon="filter" label={t("filters.hide")} aria-expanded onClick={onHide} />
        <h2 className={styles.title}>{t("filters.title")}</h2>
        <Button variant="ghost" size="sm" disabled={count === 0} onClick={() => onChange(noFilter)}>
          {t("filters.clear")}
        </Button>
      </div>

      <fieldset className={styles.section}>
        <legend>{t("filters.location")}</legend>
        <PlaceFilter value={filter.place} onChange={(place) => set({ place })} />
        <MultiSelect
          label={t("filters.voivodeship")}
          placeholder={t("filters.voivodeshipPlaceholder")}
          items={voivodeshipItems}
          value={voivodeshipItems.filter((item) => filter.voivodeship.includes(item.value))}
          onChange={(items) => set({ voivodeship: items.map((item) => item.value) })}
          itemToKey={(item) => item.value}
          itemToLabel={(item) => item.label}
          emptyText={t("filters.voivodeshipNone")}
          removeLabel={(name) => t("filters.remove", { name })}
        />
        <p className={styles.hint}>{t("filters.locationHint")}</p>
      </fieldset>

      <fieldset className={styles.section}>
        <legend>{t("filters.size")}</legend>
        <div className={styles.group}>
          <span className={styles.groupTitle}>{t("filters.towns")}</span>
          {townSizes.map((size) => (
            <Checkbox
              key={size}
              checked={filter.size.includes(size)}
              onChange={(on) => set({ size: toggle(filter.size, size, on) })}
            >
              {t(`place.size.${size}`)}
            </Checkbox>
          ))}
        </div>
        <div className={styles.group}>
          <span className={styles.groupTitle}>{t("filters.villages")}</span>
          {villageSizes.map((size) => (
            <Checkbox
              key={size}
              checked={filter.size.includes(size)}
              onChange={(on) => set({ size: toggle(filter.size, size, on) })}
            >
              {t(`place.size.${size}`)}
            </Checkbox>
          ))}
        </div>
      </fieldset>

      <Range
        title={t("filters.price")}
        min={filter.priceMin}
        max={filter.priceMax}
        onChange={(priceMin, priceMax) => set({ priceMin, priceMax })}
      />
      <Range
        title={t("filters.area")}
        min={filter.areaMin}
        max={filter.areaMax}
        onChange={(areaMin, areaMax) => set({ areaMin, areaMax })}
      />
      <Range
        title={t("filters.rooms")}
        min={filter.roomsMin}
        max={filter.roomsMax}
        step={1}
        onChange={(roomsMin, roomsMax) => set({ roomsMin, roomsMax })}
      />

      <fieldset className={styles.section}>
        <legend>{t("field.furnished")}</legend>
        <Select
          label={t("field.furnished")}
          value={filter.furnished ?? ""}
          items={[
            { value: "", label: t("filters.any") },
            { value: "Yes", label: t("answer.Yes") },
            { value: "No", label: t("answer.No") },
          ]}
          onChange={(furnished) => set({ furnished: answers.find((a) => a === furnished) ?? null })}
        />
        <p className={styles.hint}>{t("filters.furnishedHint")}</p>
      </fieldset>

      <fieldset className={styles.section}>
        <legend>{t("filters.features")}</legend>
        {featureGroups.map((group) => {
          const category = t(`featureCategory.${group.category}`);
          const allPicked = group.features.every((f) => filter.feature.includes(f));
          return (
            <div key={group.category} className={styles.group}>
              {/* Ticks or unticks the whole category; partly ticked when only some of it is picked. */}
              <Checkbox
                checked={allPicked}
                indeterminate={!allPicked && group.features.some((f) => filter.feature.includes(f))}
                onChange={() =>
                  set({
                    feature: allPicked
                      ? filter.feature.filter((f) => !group.features.includes(f))
                      : [
                          ...filter.feature,
                          ...group.features.filter((f) => !filter.feature.includes(f)),
                        ],
                  })
                }
              >
                <span className={styles.groupTitle}>{category}</span>
              </Checkbox>
              <div className={styles.columns}>
                {group.features.map((feature) => (
                  <Checkbox
                    key={feature}
                    checked={filter.feature.includes(feature)}
                    onChange={(on) => set({ feature: toggle(filter.feature, feature, on) })}
                  >
                    {t(`detail.feature.${feature}`)}
                  </Checkbox>
                ))}
              </div>
            </div>
          );
        })}
        <p className={styles.hint}>{t("filters.featuresHint")}</p>
      </fieldset>

      <fieldset className={styles.section}>
        <legend>{t("filters.history")}</legend>
        <Select
          label={t("filters.changed")}
          value={filter.changed == null ? "" : String(filter.changed)}
          items={[
            { value: "", label: t("filters.changedAny") },
            { value: "true", label: t("filters.changedYes") },
            { value: "false", label: t("filters.changedNo") },
          ]}
          onChange={(changed) =>
            set({ changed: changed === "true" ? true : changed === "false" ? false : null })
          }
        />
        <Checkbox checked={filter.priceCut} onChange={(priceCut) => set({ priceCut })}>
          {t("filters.priceCut")}
        </Checkbox>
        <p className={styles.hint}>{t("filters.priceCutHint")}</p>
      </fieldset>
    </aside>
  );
}

type RangeProps = {
  title: string;
  min: number | null;
  max: number | null;
  step?: number;
  onChange: (min: number | null, max: number | null) => void;
};

function Range({ title, min, max, step, onChange }: RangeProps) {
  const { t } = useTranslation("housing");
  return (
    <fieldset className={styles.section}>
      <legend>{title}</legend>
      <div className={styles.range}>
        <NumberInput
          label={t("filters.from", { what: title })}
          placeholder={t("filters.min")}
          value={min}
          step={step}
          onChange={(next) => onChange(next, max)}
        />
        <span aria-hidden="true">–</span>
        <NumberInput
          label={t("filters.to", { what: title })}
          placeholder={t("filters.max")}
          value={max}
          step={step}
          onChange={(next) => onChange(min, next)}
        />
      </div>
    </fieldset>
  );
}
