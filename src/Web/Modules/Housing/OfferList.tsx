import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { useEffect, useMemo, useRef, useState } from "react";
import { useTranslation } from "react-i18next";
import { Link, useLocation, useNavigate, useSearchParams } from "react-router";
import type { components } from "@shared/api/schema";
import { Button } from "@shared/ui/Button";
import { DataTable, type DataTableColumn, type DataTableSort } from "@shared/ui/DataTable";
import { Icon } from "@shared/ui/Icon";
import { IconButton } from "@shared/ui/IconButton";
import { PageTitle } from "@shared/ui/PageTitle";
import { Pagination } from "@shared/ui/Pagination";
import { useErrorMessage } from "@shared/useErrorMessage";
import { useShortcuts } from "@shared/useShortcuts";
import { FilterChips } from "./FilterChips";
import { MarketSummary } from "./MarketSummary";
import { RentCell, TotalCostCell } from "./MonthlyCost";
import styles from "./Housing.module.css";
import { ExclusionMark } from "./StatisticsExclusion";
import { OfferFilters } from "./OfferFilters";
import { offersQuery } from "./offersQuery";
import {
  countFilters,
  noFilter,
  pageSizes,
  readView,
  sorts,
  writeView,
  type OfferView,
} from "./offerView";
import { readOpenedOffer, rememberOpenedOffer } from "./openedOffer";
import { PlaceCell } from "./PlaceCell";
import { readPreviousVisit, rememberVisit } from "./previousVisit";
import { ShortcutsHelp } from "./ShortcutsHelp";
import { useFormat } from "./useFormat";

type Offer = components["schemas"]["OfferListItem"];

const noOffers: Offer[] = [];

export function OfferList() {
  const { t } = useTranslation("housing");
  const errorMessage = useErrorMessage();
  const format = useFormat();
  const navigate = useNavigate();
  const location = useLocation();
  const [params, setParams] = useSearchParams();
  const [showFilters, setShowFilters] = useState(readShowFilters);
  // The marked row: at first the offer opened last, then moved with the arrow keys.
  const [marked, setMarked] = useState(readOpenedOffer);
  const [previousVisit] = useState(readPreviousVisit);
  // Looking at the list is a visit: it moves to now while the list is shown and when it is left.
  useEffect(() => {
    rememberVisit();
    const timer = setInterval(rememberVisit, 60 * 1000);
    return () => {
      clearInterval(timer);
      rememberVisit();
    };
  }, []);
  const [helpOpen, setHelpOpen] = useState(false);
  // "/" asks for the place search; the field gets the focus once the filters are shown.
  const searchRef = useRef<HTMLDivElement>(null);
  const [searchAsked, setSearchAsked] = useState(0);
  useEffect(() => {
    if (searchAsked > 0) {
      searchRef.current?.querySelector("input")?.focus();
    }
  }, [searchAsked]);
  const view = readView(params);
  const filterCount = countFilters(view.filter);

  const offers = useQuery({ ...offersQuery(view), placeholderData: keepPreviousData });
  const items = offers.data?.items ?? noOffers;
  const lastPage = offers.data ? Math.max(1, Math.ceil(offers.data.totalCount / view.pageSize)) : 1;

  const changeView = (changes: Partial<OfferView>) => setParams(writeView({ ...view, ...changes }));
  const showOrHideFilters = (show: boolean) => {
    setShowFilters(show);
    rememberShowFilters(show);
  };
  const openOffer = (offer: Offer) => {
    rememberOpenedOffer(offer.id);
    void navigate(String(offer.id), { relative: "path", state: { from: location.search } });
  };

  // The arrows move the mark within the page; it starts at the top when no row of the page is marked.
  const markedIndex = items.findIndex((offer) => String(offer.id) === marked);
  const markedOffer = markedIndex >= 0 ? items[markedIndex] : undefined;
  const moveMark = (step: number) => {
    const index = markedIndex < 0 ? 0 : Math.min(Math.max(markedIndex + step, 0), items.length - 1);
    if (items[index]) {
      setMarked(String(items[index].id));
    }
  };
  useShortcuts(
    {
      ArrowDown: () => moveMark(1),
      ArrowUp: () => moveMark(-1),
      j: () => moveMark(1),
      k: () => moveMark(-1),
      Enter: () => markedOffer && openOffer(markedOffer),
      o: () => markedOffer && window.open(markedOffer.url, "_blank", "noopener"),
      ArrowLeft: () => view.page > 1 && changeView({ page: view.page - 1 }),
      ArrowRight: () => view.page < lastPage && changeView({ page: view.page + 1 }),
      "/": () => {
        showOrHideFilters(true);
        setSearchAsked((asked) => asked + 1);
      },
      f: () => showOrHideFilters(!showFilters),
      "?": () => setHelpOpen(true),
    },
    !helpOpen,
  );

  const columns = useMemo(
    (): DataTableColumn<Offer>[] => [
      {
        id: "title",
        header: t("list.offer"),
        minWidth: 200,
        cell: (offer) => (
          <>
            <OfferLink offer={offer} />
            <ExclusionMark offer={offer} />
          </>
        ),
      },
      {
        id: "place",
        header: t("list.place"),
        minWidth: 170,
        cell: (offer) => <PlaceCell offer={offer} />,
      },
      {
        id: "Price",
        header: t("list.price"),
        align: "end",
        sortable: true,
        cell: (offer) => <PriceCell offer={offer} />,
      },
      {
        id: "Rent",
        header: t("list.rent"),
        align: "end",
        sortable: true,
        cell: (offer) => <RentCell offer={offer} />,
      },
      {
        id: "TotalCost",
        header: t("list.totalCost"),
        align: "end",
        sortable: true,
        cell: (offer) => <TotalCostCell offer={offer} />,
      },
      {
        id: "PricePerM2",
        header: t("list.pricePerM2"),
        align: "end",
        sortable: true,
        cell: (offer) =>
          offer.pricePerM2 != null && offer.priceCurrency
            ? format.wholeMoney(offer.pricePerM2, offer.priceCurrency)
            : "—",
      },
      {
        id: "AreaM2",
        header: t("list.area"),
        align: "end",
        sortable: true,
        cell: (offer) => (offer.areaM2 != null ? format.area(offer.areaM2) : "—"),
      },
      {
        id: "Rooms",
        header: t("list.rooms"),
        align: "end",
        sortable: true,
        cell: (offer) => offer.rooms ?? "—",
      },
      {
        id: "furnished",
        header: t("list.furnished"),
        // Not stated and not known yet both read as no answer here; the offer card tells them apart.
        cell: (offer) =>
          offer.furnished === "Yes" || offer.furnished === "No"
            ? t(`answer.${offer.furnished}`)
            : "—",
      },
      {
        id: "details",
        header: t("list.details"),
        // Fetched once is enough: a changed offer keeps its old details until the next run fetches the new ones.
        cell: (offer) => t(offer.detailsFetchedAt ? "answer.Yes" : "answer.No"),
      },
      {
        id: "FirstSeen",
        header: t("list.firstSeen"),
        align: "end",
        sortable: true,
        descendingFirst: true,
        cell: (offer) =>
          offer.externalCreatedAt ? (
            <>
              {format.shortDay(offer.externalCreatedAt)}
              <br />
              <small className={styles.muted}>{format.time(offer.externalCreatedAt)}</small>
            </>
          ) : (
            "—"
          ),
      },
    ],
    [t, format],
  );

  const sort: DataTableSort = { column: view.sort, descending: view.direction === "Descending" };
  const changeSort = (next: DataTableSort) =>
    changeView({
      page: 1,
      sort: sorts.find((s) => s === next.column) ?? "FirstSeen",
      direction: next.descending ? "Descending" : "Ascending",
    });

  const renderOffers = () => {
    if (offers.isError) {
      return (
        <p className={styles.error} role="alert">
          {errorMessage(offers.error.message)}
        </p>
      );
    }

    if (offers.data?.totalCount === 0) {
      return filterCount > 0 ? (
        <div className={styles.empty}>
          <p>{t("filters.noMatch")}</p>
          <Button onClick={() => changeView({ page: 1, filter: noFilter })}>
            {t("filters.clear")}
          </Button>
        </div>
      ) : (
        <div className={styles.empty}>
          <p>{t("list.empty")}</p>
          <Link to="../collection" relative="path">
            {t("list.goToCollection")}
          </Link>
        </div>
      );
    }

    // One table from the first load on: placeholder rows until the offers arrive, so its headers
    // stay the same elements and a click on one while loading is not lost.
    return (
      <>
        <DataTable
          columns={columns}
          rows={items}
          getRowId={(offer) => String(offer.id)}
          sort={sort}
          onSortChange={changeSort}
          onRowClick={openOffer}
          busy={offers.isPlaceholderData}
          loading={offers.isPending ? t("list.loading") : undefined}
          fill
          // Each view of the list is its own history entry, so back restores how far it was scrolled.
          scrollKey={location.key}
          markedRowId={marked ?? undefined}
        />
        {offers.data && (
          <>
            {items.length === 0 && <p className={styles.empty}>{t("list.pageEmpty")}</p>}
            <Pagination
              page={view.page}
              pageSize={view.pageSize}
              totalCount={offers.data.totalCount}
              pageSizes={pageSizes}
              onPageChange={(page) => changeView({ page })}
              // The first offer of the current page stays on the new page.
              onPageSizeChange={(pageSize) =>
                changeView({
                  pageSize,
                  page: Math.floor(((view.page - 1) * view.pageSize) / pageSize) + 1,
                })
              }
            />
          </>
        )}
      </>
    );
  };

  return (
    <section className={styles.list}>
      <PageTitle
        aside={offers.data && t("list.count", { count: offers.data.totalCount })}
        actions={
          <IconButton
            icon="keyboard"
            label={t("shortcuts.show")}
            onClick={() => setHelpOpen(true)}
          />
        }
      >
        {t("list.title")}
      </PageTitle>
      <ShortcutsHelp open={helpOpen} onOpenChange={setHelpOpen} />
      <div className={styles.body} data-filters={showFilters ? "open" : "closed"}>
        {showFilters ? (
          <div className={styles.filters}>
            {/* A changed filter starts from the first page. */}
            <OfferFilters
              filter={view.filter}
              onChange={(filter) => changeView({ page: 1, filter })}
              previousVisit={previousVisit}
              searchRef={searchRef}
              onHide={() => showOrHideFilters(false)}
            />
          </div>
        ) : (
          // Hidden, the filters leave a strip with their button and how many are in use.
          <div className={styles.filtersStrip}>
            <IconButton
              icon="filter"
              label={t("filters.show")}
              aria-expanded={false}
              onClick={() => showOrHideFilters(true)}
            />
            {filterCount > 0 && (
              <span className={styles.badge} title={t("filters.inUse", { count: filterCount })}>
                {filterCount}
              </span>
            )}
          </div>
        )}
        <div className={styles.offers}>
          <MarketSummary filter={view.filter} />
          <FilterChips
            filter={view.filter}
            onChange={(filter) => changeView({ page: 1, filter })}
          />
          {renderOffers()}
        </div>
      </div>
    </section>
  );
}

// The offer card links back to the list view the user came from.
// Next to the title, the offer on its portal opens in a new tab without going through the card.
function OfferLink({ offer }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const location = useLocation();
  const portal = t("detail.portal", { source: offer.source });
  return (
    <div className={styles.offerCell}>
      <Link
        to={String(offer.id)}
        relative="path"
        state={{ from: location.search }}
        className={styles.title}
        // The list shows two lines of a long title; the whole one on hover.
        title={offer.title}
        onClick={() => rememberOpenedOffer(offer.id)}
      >
        {offer.title}
      </Link>
      <a
        className={styles.portalLink}
        href={offer.url}
        target="_blank"
        rel="noreferrer"
        aria-label={portal}
        title={portal}
      >
        <Icon name="external" size={18} />
      </a>
    </div>
  );
}

// The price and, under it, how the last recorded change moved it: green down, red up, like the change history.
function PriceCell({ offer }: { offer: Offer }) {
  const { t } = useTranslation("housing");
  const format = useFormat();
  if (offer.price == null || !offer.priceCurrency) {
    return "—";
  }
  const currency = offer.priceCurrency;
  const difference = offer.previousPrice != null ? offer.price - offer.previousPrice : 0;
  return (
    <>
      {format.money(offer.price, currency)}
      {difference !== 0 && (
        <>
          <br />
          <small
            className={difference < 0 ? styles.down : styles.up}
            title={t("list.previousPrice", { price: format.money(offer.previousPrice!, currency) })}
          >
            {difference > 0 ? "+" : "−"}
            {format.money(Math.abs(difference), currency)}
          </small>
        </>
      )}
    </>
  );
}

// Whether the filter panel is shown stays for the browser.
const showFiltersKey = "housing.showFilters";

function readShowFilters(): boolean {
  try {
    return localStorage.getItem(showFiltersKey) !== "false";
  } catch {
    return true;
  }
}

function rememberShowFilters(show: boolean) {
  try {
    localStorage.setItem(showFiltersKey, String(show));
  } catch {
    // Without storage the panel is shown again next time.
  }
}
