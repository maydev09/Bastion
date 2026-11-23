import { useState, type ReactNode, type RefObject } from "react";
import { useTranslation } from "react-i18next";
import { Button } from "@shared/ui/Button";
import { Checkbox } from "@shared/ui/Checkbox";
import type { components } from "@shared/api/schema";
import { Collapsible } from "@shared/ui/Collapsible";
import { Histogram as HistogramChart } from "@shared/ui/Histogram";
import { IconButton } from "@shared/ui/IconButton";
import { MultiSelect } from "@shared/ui/MultiSelect";
import { NumberInput } from "@shared/ui/NumberInput";
import { Select } from "@shared/ui/Select";
import { ToggleGroup } from "@shared/ui/ToggleGroup";
import styles from "./OfferFilters.module.css";
import {
  answers,
  countFilters,
  noFilter,
  roomCounts,
  roomsOrMore,
  townSizes,
  villageSizes,
  voivodeships,
  type OfferFilter,
} from "./offerView";
import { FiltersHelp } from "./FiltersHelp";
import { NewOffers } from "./NewOffers";
import { PlaceFilter } from "./PlaceFilter";
import { useFacets } from "./useFacets";
import { useFeatureGroups } from "./useFeatures";
import { useFormat } from "./useFormat";

type Histogram = components["schemas"]["Histogram"];

type Props = {
  filter: OfferFilter;
  onChange: (filter: OfferFilter) => void;
  onHide: () => void;
  // When the user last looked at the offers; null on the first visit in this browser.
  previousVisit: string | null;
  // Holds the place search, so a shortcut can put the cursor in it.
  searchRef: RefObject<HTMLDivElement | null>;
};

type SectionId =
  "new" | "location" | "size" | "price" | "area" | "rooms" | "furnished" | "features" | "history";

// Open until the user closes them; the other sections open while they hold a filter.
const openAtFirst: SectionId[] = ["new", "location", "price"];

// Every change applies at once; the list and the link follow it.
// Sections join with AND; the choices inside a section with OR, except amenities, which match all or any.
export function OfferFilters({ filter, onChange, onHide, previousVisit, searchRef }: Props) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  const set = (changes: Partial<OfferFilter>) => onChange({ ...filter, ...changes });
  const toggle = <T,>(values: T[], value: T, on: boolean) =>
    on ? [...values, value] : values.filter((v) => v !== value);
  const featureGroups = useFeatureGroups();
  const facets = useFacets(filter);
  // A count shows once the counts have loaded; a choice missing from them matches no offer.
  const shownCount = (counts: Record<string, number> | undefined, key: string) =>
    facets && format.number(counts?.[key] ?? 0);
  const withCount = (label: string, count: number | undefined) =>
    count == null ? label : t("filters.withCount", { label, count });
  const voivodeshipItems = voivodeships.map((v) => ({ value: v, label: t(`voivodeship.${v}`) }));

  const counts: Record<SectionId, number> = {
    new: filter.addedSince ? 1 : 0,
    location: filter.place.length + filter.voivodeship.length + (filter.withoutPlace ? 1 : 0),
    size: filter.size.length,
    price: [filter.priceMin, filter.priceMax].filter((v) => v != null).length,
    area: [filter.areaMin, filter.areaMax].filter((v) => v != null).length,
    rooms: filter.rooms.length + (filter.roomsMin != null ? 1 : 0),
    furnished: filter.furnished ? 1 : 0,
    features: filter.feature.length,
    history: (filter.changed != null ? 1 : 0) + (filter.priceCut ? 1 : 0),
  };
  const [choices, setChoices] = useState(readSectionChoices);
  const [helpOpen, setHelpOpen] = useState(false);
  const section = (id: SectionId, title: string, children: ReactNode, hint?: string) => (
    <Collapsible
      plain
      title={title}
      count={counts[id]}
      countLabel={t("filters.inUse", { count: counts[id] })}
      hint={hint}
      open={choices[id] ?? (openAtFirst.includes(id) || counts[id] > 0)}
      onOpenChange={(isOpen) => {
        const next = { ...choices, [id]: isOpen };
        setChoices(next);
        rememberSectionChoices(next);
      }}
    >
      <div className={styles.fields}>{children}</div>
    </Collapsible>
  );
  // Said in the header once two or more choices could be joined either way.
  const anyOf = (picked: number) => (picked >= 2 ? t("filters.anyOf") : undefined);

  return (
    <aside className={styles.panel} aria-label={t("filters.title")}>
      <div className={styles.header}>
        <IconButton icon="filter" label={t("filters.hide")} aria-expanded onClick={onHide} />
        <h2 className={styles.title}>{t("filters.title")}</h2>
        {/* How the filters join is explained on demand, not with words between the fields. */}
        <IconButton icon="info" label={t("filtersHelp.show")} onClick={() => setHelpOpen(true)} />
        <FiltersHelp open={helpOpen} onOpenChange={setHelpOpen} />
        <Button
          variant="ghost"
          size="sm"
          disabled={countFilters(filter) === 0}
          onClick={() => onChange(noFilter)}
        >
          {t("filters.clear")}
        </Button>
      </div>

      {/* First: a daily look at the offers starts with what is new. */}
      {section(
        "new",
        t("filters.newOffers"),
        <NewOffers filter={filter} onChange={onChange} previousVisit={previousVisit} />,
      )}

      {section(
        "location",
        t("filters.location"),
        <>
          <div ref={searchRef}>
            <PlaceFilter value={filter.place} onChange={(place) => set({ place })} />
          </div>
          <MultiSelect
            label={t("filters.voivodeship")}
            placeholder={t("filters.voivodeshipPlaceholder")}
            items={voivodeshipItems}
            value={voivodeshipItems.filter((item) => filter.voivodeship.includes(item.value))}
            onChange={(items) => set({ voivodeship: items.map((item) => item.value) })}
            itemToKey={(item) => item.value}
            itemToLabel={(item) => item.label}
            itemToHint={(item) => shownCount(facets?.voivodeships, item.value) ?? ""}
            emptyText={t("filters.voivodeshipNone")}
            removeLabel={(name) => t("filters.remove", { name })}
          />
          <Checkbox
            checked={filter.withoutPlace}
            onChange={(withoutPlace) => set({ withoutPlace })}
            hint={facets && format.number(facets.withoutPlace)}
          >
            {t("filters.withoutPlace")}
          </Checkbox>
        </>,
        anyOf(counts.location),
      )}

      {section(
        "size",
        t("filters.size"),
        [
          { title: t("filters.towns"), sizes: townSizes },
          { title: t("filters.villages"), sizes: villageSizes },
        ].map((group) => (
          <div key={group.title} className={styles.group}>
            <span className={styles.groupTitle}>{group.title}</span>
            {group.sizes.map((size) => (
              <Checkbox
                key={size}
                checked={filter.size.includes(size)}
                onChange={(on) => set({ size: toggle(filter.size, size, on) })}
                hint={shownCount(facets?.placeSizes, size)}
              >
                {t(`place.size.${size}`)}
              </Checkbox>
            ))}
          </div>
        )),
        anyOf(counts.size),
      )}

      {section(
        "price",
        t("filters.price"),
        <Range
          title={t("filters.price")}
          unit={t("units.pln")}
          min={filter.priceMin}
          max={filter.priceMax}
          onChange={(priceMin, priceMax) => set({ priceMin, priceMax })}
          histogram={facets?.price}
          show={(value) => format.wholeMoney(value, "PLN")}
        />,
      )}

      {section(
        "area",
        t("filters.area"),
        <Range
          title={t("filters.area")}
          unit={t("units.m2")}
          min={filter.areaMin}
          max={filter.areaMax}
          onChange={(areaMin, areaMax) => set({ areaMin, areaMax })}
          histogram={facets?.areaM2}
          show={format.area}
        />,
      )}

      {section(
        "rooms",
        t("filters.rooms"),
        <ToggleGroup
          label={t("filters.rooms")}
          items={[
            ...roomCounts.map((rooms) => ({ value: String(rooms), label: String(rooms) })),
            { value: "more", label: t("filters.orMore", { count: roomsOrMore }) },
          ]}
          pressed={[
            ...filter.rooms.map(String),
            ...(filter.roomsMin === roomsOrMore ? ["more"] : []),
          ]}
          onToggle={(value) =>
            value === "more"
              ? set({ roomsMin: filter.roomsMin == null ? roomsOrMore : null })
              : set({
                  rooms: toggle(filter.rooms, Number(value), !filter.rooms.includes(Number(value))),
                })
          }
        />,
        anyOf(counts.rooms),
      )}

      {section(
        "furnished",
        t("field.furnished"),
        <>
          <Select
            label={t("field.furnished")}
            value={filter.furnished ?? ""}
            items={[
              { value: "", label: t("filters.any") },
              ...(["Yes", "No"] as const).map((answer) => ({
                value: answer,
                label: withCount(t(`answer.${answer}`), facets && (facets.furnished[answer] ?? 0)),
              })),
            ]}
            onChange={(furnished) =>
              set({ furnished: answers.find((a) => a === furnished) ?? null })
            }
          />
          <p className={styles.hint}>{t("filters.furnishedHint")}</p>
        </>,
      )}

      {section(
        "features",
        t("filters.features"),
        <>
          {filter.feature.length >= 2 && (
            <div className={styles.match}>
              <span>{t("filters.match")}</span>
              <ToggleGroup
                label={t("filters.match")}
                items={[
                  { value: "All", label: t("filters.matchAll") },
                  { value: "Any", label: t("filters.matchAny") },
                ]}
                pressed={[filter.featureMatch]}
                onToggle={(featureMatch) => set({ featureMatch })}
              />
            </div>
          )}
          {featureGroups.map((group) => {
            const allPicked = group.features.every((f) => filter.feature.includes(f));
            return (
              <div key={group.category} className={styles.group}>
                {/* Ticks or unticks the whole category; partly ticked when only some of it is picked. */}
                <Checkbox
                  checked={allPicked}
                  indeterminate={
                    !allPicked && group.features.some((f) => filter.feature.includes(f))
                  }
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
                  <span className={styles.groupTitle}>
                    {t(`featureCategory.${group.category}`)}
                  </span>
                </Checkbox>
                <div className={styles.columns}>
                  {group.features.map((feature) => (
                    <Checkbox
                      key={feature}
                      checked={filter.feature.includes(feature)}
                      onChange={(on) => set({ feature: toggle(filter.feature, feature, on) })}
                      hint={shownCount(facets?.features, feature)}
                    >
                      {t(`detail.feature.${feature}`)}
                    </Checkbox>
                  ))}
                </div>
              </div>
            );
          })}
        </>,
        counts.features >= 2
          ? t(filter.featureMatch === "Any" ? "filters.anyOf" : "filters.allOf")
          : undefined,
      )}

      {section(
        "history",
        t("filters.history"),
        <>
          <Select
            label={t("filters.changed")}
            value={filter.changed == null ? "" : String(filter.changed)}
            items={[
              { value: "", label: t("filters.changedAny") },
              { value: "true", label: withCount(t("filters.changedYes"), facets?.changed) },
              { value: "false", label: withCount(t("filters.changedNo"), facets?.unchanged) },
            ]}
            onChange={(changed) =>
              set({ changed: changed === "true" ? true : changed === "false" ? false : null })
            }
          />
          <Checkbox
            checked={filter.priceCut}
            onChange={(priceCut) => set({ priceCut })}
            hint={facets && format.number(facets.priceCut)}
          >
            {t("filters.priceCut")}
          </Checkbox>
          <p className={styles.hint}>{t("filters.priceCutHint")}</p>
        </>,
      )}
    </aside>
  );
}

type RangeProps = {
  title: string;
  unit: string;
  min: number | null;
  max: number | null;
  onChange: (min: number | null, max: number | null) => void;
  // The spread of the value under the other filters; the chosen range is marked on it.
  histogram: Histogram | null | undefined;
  show: (value: number) => string;
};

function Range({ title, unit, min, max, onChange, histogram, show }: RangeProps) {
  const { t } = useTranslation("housing");
  const edge = (index: number) => histogram!.from + index * histogram!.width;
  // A click picks the bar's range. With one bar picked, a click on another bar stretches the range
  // to it, and a click on the same bar clears the range.
  const pickBar = (index: number) => {
    const picked = histogram!.counts.findIndex((_, i) => min === edge(i) && max === edge(i + 1));
    if (picked === index) {
      onChange(null, null);
    } else if (picked >= 0) {
      onChange(edge(Math.min(picked, index)), edge(Math.max(picked, index) + 1));
    } else {
      onChange(edge(index), edge(index + 1));
    }
  };
  return (
    <>
      {histogram && (
        <HistogramChart
          label={t("filters.histogram", {
            what: title,
            from: show(edge(0)),
            to: show(edge(histogram.counts.length)),
          })}
          counts={histogram.counts}
          // A bar is picked when its range overlaps the chosen one.
          isPicked={(index) =>
            (max == null || edge(index) < max) && (min == null || edge(index + 1) > min)
          }
          // The last bar also holds the unusually high offers.
          barLabel={(index) =>
            index === histogram.counts.length - 1
              ? t("filters.barFrom", { from: show(edge(index)), count: histogram.counts[index] })
              : t("filters.bar", {
                  from: show(edge(index)),
                  to: show(edge(index + 1)),
                  count: histogram.counts[index],
                })
          }
          onPick={pickBar}
          start={show(edge(0))}
          end={show(edge(histogram.counts.length))}
        />
      )}
      <div className={styles.range}>
        <NumberInput
          label={t("filters.from", { what: title })}
          placeholder={t("filters.min")}
          unit={unit}
          value={min}
          onChange={(next) => onChange(next, max)}
        />
        <span aria-hidden="true">–</span>
        <NumberInput
          label={t("filters.to", { what: title })}
          placeholder={t("filters.max")}
          unit={unit}
          value={max}
          onChange={(next) => onChange(min, next)}
        />
      </div>
    </>
  );
}

// The sections the user opened or closed stay so for the browser; the others follow openAtFirst.
const sectionChoicesKey = "housing.filterSectionsOpen";

type SectionChoices = Partial<Record<SectionId, boolean>>;

function readSectionChoices(): SectionChoices {
  try {
    return JSON.parse(localStorage.getItem(sectionChoicesKey) ?? "{}") as SectionChoices;
  } catch {
    // Without storage (or with a broken value) the sections open as at first.
    return {};
  }
}

function rememberSectionChoices(choices: SectionChoices) {
  try {
    localStorage.setItem(sectionChoicesKey, JSON.stringify(choices));
  } catch {
    // Without storage the sections open as at first next time.
  }
}
