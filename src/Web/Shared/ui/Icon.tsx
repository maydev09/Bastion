// Small stroke icons drawn on a 16×16 grid; they inherit the text color.
const paths = {
  first: "M11.5 4 7.5 8l4 4M4.5 4v8",
  previous: "M10 4 6 8l4 4",
  next: "M6 4l4 4-4 4",
  last: "M4.5 4l4 4-4 4M11.5 4v8",
  sortAscending: "M8 12.5v-9M4.5 7 8 3.5 11.5 7",
  sortDescending: "M8 3.5v9M4.5 9 8 12.5 11.5 9",
  sortable: "M5 6.5 8 3.5l3 3M5 9.5l3 3 3-3",
  chevronDown: "M4.5 6.5 8 10l3.5-3.5",
  check: "M3.5 8.5 6.5 11.5 12.5 4.5",
  menu: "M2.5 4h11M2.5 8h11M2.5 12h11",
  external: "M7 3.5H3.5v9h9V9M9.5 3.5h3v3M12.5 3.5 7.5 8.5",
  sun: "M8 1.5V3M8 13v1.5M1.5 8H3M13 8h1.5M3.4 3.4l1 1M11.6 11.6l1 1M3.4 12.6l1-1M11.6 4.4l1-1M10.5 8a2.5 2.5 0 1 1-5 0 2.5 2.5 0 0 1 5 0Z",
  moon: "M13 9.5A5.5 5.5 0 1 1 6.5 3a4.5 4.5 0 0 0 6.5 6.5Z",
  system: "M2.5 3h11v7.5h-11ZM6 13.5h4M8 10.5v3",
};

export type IconName = keyof typeof paths;

export function Icon({ name, size = 16 }: { name: IconName; size?: number }) {
  return (
    <svg
      viewBox="0 0 16 16"
      width={size}
      height={size}
      fill="none"
      stroke="currentColor"
      strokeWidth="1.5"
      strokeLinecap="round"
      strokeLinejoin="round"
      aria-hidden="true"
      style={{ display: "block", flexShrink: 0 }}
    >
      <path d={paths[name]} />
    </svg>
  );
}
