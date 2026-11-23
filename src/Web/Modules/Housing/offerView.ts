import type { components } from "@shared/api/schema";

type Voivodeship = NonNullable<components["schemas"]["Voivodeship"]>;
type PlaceSize = NonNullable<components["schemas"]["PlaceSize"]>;
type Feature = components["schemas"]["Feature"];
type OfferSort = components["schemas"]["OfferSort"];
type Answer = NonNullable<components["schemas"]["Answer"]>;

export const voivodeships: Voivodeship[] = [
  "Dolnoslaskie",
  "KujawskoPomorskie",
  "Lubelskie",
  "Lubuskie",
  "Lodzkie",
  "Malopolskie",
  "Mazowieckie",
  "Opolskie",
  "Podkarpackie",
  "Podlaskie",
  "Pomorskie",
  "Slaskie",
  "Swietokrzyskie",
  "WarminskoMazurskie",
  "Wielkopolskie",
  "Zachodniopomorskie",
];
export const townSizes: PlaceSize[] = [
  "Metropolis",
  "VeryLargeCity",
  "LargeCity",
  "LargerTown",
  "MediumTown",
  "SmallTown",
  "VerySmallTown",
];
export const villageSizes: PlaceSize[] = [
  "VeryLargeVillage",
  "LargeVillage",
  "MediumVillage",
  "SmallVillage",
];
export const answers: Answer[] = ["Yes", "No", "NotStated"];
export const sorts: OfferSort[] = ["FirstSeen", "Price", "PricePerM2", "AreaM2", "Rooms"];
export const pageSizes = [25, 50, 100];
const defaultPageSize = 50;

// The URL keys are the API's query parameters, so a list link reads like the request behind it.
export type OfferFilter = {
  voivodeship: Voivodeship[];
  place: number[];
  size: PlaceSize[];
  feature: Feature[];
  priceMin: number | null;
  priceMax: number | null;
  areaMin: number | null;
  areaMax: number | null;
  roomsMin: number | null;
  roomsMax: number | null;
  furnished: Answer | null;
  // With (true) or without (false) a recorded change.
  changed: boolean | null;
  priceCut: boolean;
};

export type OfferView = {
  page: number;
  pageSize: number;
  sort: OfferSort;
  direction: "Ascending" | "Descending";
  filter: OfferFilter;
};

export const noFilter: OfferFilter = {
  voivodeship: [],
  place: [],
  size: [],
  feature: [],
  priceMin: null,
  priceMax: null,
  areaMin: null,
  areaMax: null,
  roomsMin: null,
  roomsMax: null,
  furnished: null,
  changed: null,
  priceCut: false,
};

const numberKeys = ["priceMin", "priceMax", "areaMin", "areaMax", "roomsMin", "roomsMax"] as const;

// Unknown or broken values in a link are ignored, so an old link still opens a list.
export function readView(params: URLSearchParams): OfferView {
  const page = Number.parseInt(params.get("page") ?? "", 10);
  const pageSize = Number.parseInt(params.get("pageSize") ?? "", 10);
  const filter: OfferFilter = {
    ...noFilter,
    voivodeship: voivodeships.filter((v) => params.getAll("voivodeship").includes(v)),
    place: params
      .getAll("place")
      .map(Number)
      .filter((id) => Number.isInteger(id) && id > 0),
    size: [...townSizes, ...villageSizes].filter((s) => params.getAll("size").includes(s)),
    // The amenities come from the API; one it does not know makes the list fail, so only names pass here.
    feature: params.getAll("feature").filter((f): f is Feature => /^[A-Za-z]+$/.test(f)),
    furnished: answers.find((a) => a === params.get("furnished")) ?? null,
    changed:
      params.get("changed") === "true" ? true : params.get("changed") === "false" ? false : null,
    priceCut: params.get("priceCut") === "true",
  };
  for (const key of numberKeys) {
    const value = params.get(key);
    filter[key] = value && Number.isFinite(Number(value)) ? Number(value) : null;
  }
  return {
    page: page >= 1 ? page : 1,
    pageSize: pageSizes.includes(pageSize) ? pageSize : defaultPageSize,
    sort: sorts.find((s) => s === params.get("sort")) ?? "FirstSeen",
    direction: params.get("direction") === "Ascending" ? "Ascending" : "Descending",
    filter,
  };
}

// Default values stay out of the URL, so the plain list has a plain link.
export function writeView(view: OfferView): URLSearchParams {
  const params = new URLSearchParams();
  const { filter } = view;
  filter.voivodeship.forEach((v) => params.append("voivodeship", v));
  filter.place.forEach((id) => params.append("place", String(id)));
  filter.size.forEach((s) => params.append("size", s));
  filter.feature.forEach((f) => params.append("feature", f));
  for (const key of numberKeys) {
    const value = filter[key];
    if (value != null) {
      params.set(key, String(value));
    }
  }
  if (filter.furnished) {
    params.set("furnished", filter.furnished);
  }
  if (filter.changed != null) {
    params.set("changed", String(filter.changed));
  }
  if (filter.priceCut) {
    params.set("priceCut", "true");
  }
  if (view.page > 1) {
    params.set("page", String(view.page));
  }
  if (view.pageSize !== defaultPageSize) {
    params.set("pageSize", String(view.pageSize));
  }
  if (view.sort !== "FirstSeen") {
    params.set("sort", view.sort);
  }
  if (view.direction !== "Descending") {
    params.set("direction", view.direction);
  }
  return params;
}

// The query of GET /api/housing/offers; filters not in use are left out.
export function toQuery(view: OfferView) {
  const { filter } = view;
  const list = <T>(values: T[]) => (values.length > 0 ? values : undefined);
  return {
    page: view.page,
    pageSize: view.pageSize,
    sort: view.sort,
    direction: view.direction,
    voivodeship: list(filter.voivodeship),
    place: list(filter.place),
    size: list(filter.size),
    feature: list(filter.feature),
    priceMin: filter.priceMin ?? undefined,
    priceMax: filter.priceMax ?? undefined,
    areaMin: filter.areaMin ?? undefined,
    areaMax: filter.areaMax ?? undefined,
    roomsMin: filter.roomsMin ?? undefined,
    roomsMax: filter.roomsMax ?? undefined,
    furnished: filter.furnished ?? undefined,
    changed: filter.changed ?? undefined,
    priceCut: filter.priceCut || undefined,
  };
}

// Every picked value counts: two voivodeships and a top price are three filters.
export function countFilters(filter: OfferFilter): number {
  return (
    filter.voivodeship.length +
    filter.place.length +
    filter.size.length +
    filter.feature.length +
    numberKeys.filter((key) => filter[key] != null).length +
    (filter.furnished ? 1 : 0) +
    (filter.changed != null ? 1 : 0) +
    (filter.priceCut ? 1 : 0)
  );
}
