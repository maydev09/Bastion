import {
  functionalUpdate,
  rowSortingFeature,
  tableFeatures,
  useTable,
  type ColumnDef,
  type RowData,
  type SortingState,
} from "@tanstack/react-table";
import {
  useEffect,
  useLayoutEffect,
  useMemo,
  useRef,
  type MouseEvent,
  type ReactNode,
} from "react";
import styles from "./DataTable.module.css";
import { Icon } from "./Icon";

const features = tableFeatures({ rowSortingFeature });

// About a screen of rows, so the table has its shape before the first rows arrive.
const placeholderRows = Array.from({ length: 12 }, (_, index) => `placeholder-${index}`);

// How far each view (scrollKey) was scrolled, for as long as the app is open.
const scrollPositions = new Map<string, number>();

export type DataTableColumn<Row> = {
  id: string;
  header: string;
  cell: (row: Row) => ReactNode;
  align?: "start" | "end";
  // In pixels; keeps text columns readable when the table is narrow.
  minWidth?: number;
  sortable?: boolean;
  // The first click sorts descending (newest or largest first) instead of ascending.
  descendingFirst?: boolean;
};

export type DataTableSort = { column: string; descending: boolean };

type Props<Row extends RowData> = {
  columns: DataTableColumn<Row>[];
  rows: Row[];
  getRowId: (row: Row) => string;
  // Sorting is done by the server: the table only shows and changes the chosen column.
  sort: DataTableSort;
  onSortChange: (sort: DataTableSort) => void;
  onRowClick?: (row: Row) => void;
  // The rows shown are stale while the next ones load: after a moment the table dims and a bar
  // runs along its top. A quick answer shows nothing, so the table does not blink on every click.
  busy?: boolean;
  // Given while there are no rows yet (the first load): placeholder rows stand in for them,
  // and this text (e.g. "Loading offers…") is read out by screen readers.
  loading?: string;
  // The table takes the height its flex container gives it and scrolls inside, e.g. to keep the paging
  // below it at the bottom of the page; otherwise it grows up to most of the window.
  fill?: boolean;
  // Names the view (e.g. the history entry of the page); coming back to it restores how far it was scrolled.
  scrollKey?: string;
  // The row to mark, e.g. the one the user opened last.
  markedRowId?: string;
};

export function DataTable<Row extends RowData>({
  columns,
  rows,
  getRowId,
  sort,
  onSortChange,
  onRowClick,
  busy = false,
  loading,
  fill = false,
  scrollKey,
  markedRowId,
}: Props<Row>) {
  const tableColumns = useMemo(
    () =>
      columns.map((column): ColumnDef<typeof features, Row> => ({
        id: column.id,
        header: column.header,
        // TanStack lets only columns with an accessor be sorted; the row itself is enough.
        accessorFn: (row) => row,
        cell: ({ row }) => column.cell(row.original),
        enableSorting: column.sortable ?? false,
        sortDescFirst: column.descendingFirst ?? false,
      })),
    [columns],
  );
  const byId = new Map(columns.map((c) => [c.id, c]));
  const frame = useRef<HTMLDivElement>(null);

  // New rows (another page or order) start at the top, unless this view was scrolled before:
  // then the user comes back to where they were (e.g. with the browser's back button).
  // The position is saved when the view is left, i.e. on new rows, another key or leaving the page.
  useLayoutEffect(() => {
    const element = frame.current;
    if (!element) {
      return;
    }
    element.scrollTop = (scrollKey && scrollPositions.get(scrollKey)) || 0;
    return () => {
      if (scrollKey) {
        scrollPositions.set(scrollKey, element.scrollTop);
      }
    };
  }, [rows, scrollKey]);
  // A marked row moved out of view (e.g. with the arrow keys) is scrolled back into it.
  useEffect(() => {
    if (markedRowId !== undefined) {
      frame.current?.querySelector("tr[data-marked]")?.scrollIntoView({ block: "nearest" });
    }
  }, [markedRowId]);
  const sorting: SortingState = [{ id: sort.column, desc: sort.descending }];

  const table = useTable({
    features,
    columns: tableColumns,
    data: rows,
    getRowId,
    state: { sorting },
    onSortingChange: (updater) => {
      const [next] = functionalUpdate(updater, sorting);
      if (next) {
        onSortChange({ column: next.id, descending: next.desc });
      }
    },
    manualSorting: true,
    enableMultiSort: false,
    enableSortingRemoval: false,
  });

  // A click anywhere on a row opens it, unless it hit a link or a button or selected text.
  const handleRowClick = (event: MouseEvent, row: Row) => {
    const target = event.target as HTMLElement;
    if (!onRowClick || target.closest("a, button") || window.getSelection()?.toString()) {
      return;
    }
    onRowClick(row);
  };

  return (
    <div ref={frame} className={fill ? `${styles.frame} ${styles.fill}` : styles.frame}>
      {busy && <div className={styles.progress} aria-hidden="true" />}
      {loading && (
        <p className={styles.hidden} role="status">
          {loading}
        </p>
      )}
      <table className={styles.table} aria-busy={busy || loading !== undefined} data-busy={busy}>
        <thead>
          {table.getHeaderGroups().map((group) => (
            <tr key={group.id}>
              {group.headers.map((header) => {
                const sorted = header.column.getIsSorted();
                return (
                  <th
                    key={header.id}
                    data-align={byId.get(header.column.id)?.align ?? "start"}
                    style={{ minWidth: byId.get(header.column.id)?.minWidth }}
                    aria-sort={
                      header.column.getCanSort()
                        ? sorted === "asc"
                          ? "ascending"
                          : sorted === "desc"
                            ? "descending"
                            : "none"
                        : undefined
                    }
                  >
                    {header.column.getCanSort() ? (
                      <button
                        type="button"
                        className={styles.sort}
                        data-sorted={sorted !== false}
                        onClick={header.column.getToggleSortingHandler()}
                      >
                        <table.FlexRender header={header} />
                        <Icon
                          name={
                            sorted === "asc"
                              ? "sortAscending"
                              : sorted === "desc"
                                ? "sortDescending"
                                : "sortable"
                          }
                          size={16}
                        />
                      </button>
                    ) : (
                      <table.FlexRender header={header} />
                    )}
                  </th>
                );
              })}
            </tr>
          ))}
        </thead>
        <tbody>
          {loading &&
            placeholderRows.map((key) => (
              <tr key={key} aria-hidden="true">
                {columns.map((column) => (
                  <td key={column.id} data-align={column.align ?? "start"}>
                    <span className={styles.placeholder} />
                  </td>
                ))}
              </tr>
            ))}
          {table.getRowModel().rows.map((row) => (
            <tr
              key={row.id}
              data-clickable={onRowClick !== undefined}
              data-marked={row.id === markedRowId || undefined}
              onClick={(event) => handleRowClick(event, row.original)}
            >
              {row.getAllCells().map((cell) => (
                <td key={cell.id} data-align={byId.get(cell.column.id)?.align ?? "start"}>
                  <table.FlexRender cell={cell} />
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
