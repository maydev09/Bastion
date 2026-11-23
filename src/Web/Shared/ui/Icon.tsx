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
  close: "M4.5 4.5l7 7M11.5 4.5l-7 7",
  minus: "M4 8h8",
  filter: "M2.5 3.5h11L9.5 8.5v4l-3 1.5V8.5Z",
  menu: "M2.5 4h11M2.5 8h11M2.5 12h11",
  // A rounded window with an arrow leaving its corner: opens somewhere else.
  external:
    "M12.5 9.5v2a1.5 1.5 0 0 1-1.5 1.5H4.5A1.5 1.5 0 0 1 3 11.5V5a1.5 1.5 0 0 1 1.5-1.5h2M9.5 2.5h4v4M13.5 2.5 8 8",
  sun: "M8 1.5V3M8 13v1.5M1.5 8H3M13 8h1.5M3.4 3.4l1 1M11.6 11.6l1 1M3.4 12.6l1-1M11.6 4.4l1-1M10.5 8a2.5 2.5 0 1 1-5 0 2.5 2.5 0 0 1 5 0Z",
  moon: "M13 9.5A5.5 5.5 0 1 1 6.5 3a4.5 4.5 0 0 0 6.5 6.5Z",
  system: "M2.5 3h11v7.5h-11ZM6 13.5h4M8 10.5v3",
  dashboard: "M2.5 2.5h4.5V7H2.5ZM9 2.5h4.5V7H9ZM2.5 9H7v4.5H2.5ZM9 9h4.5v4.5H9Z",
  house: "M2.5 7.5 8 3l5.5 4.5M4 6.5v7h3v-4h2v4h3v-7",
  list: "M6 4h7.5M6 8h7.5M6 12h7.5M2.5 4h.01M2.5 8h.01M2.5 12h.01",
  download: "M8 2.5v8M4.5 7 8 10.5 11.5 7M3 13.5h10",
  briefcase: "M2.5 5.5h11v7.5h-11ZM6 5.5v-2h4v2M2.5 9h11",
  info: "M8 14.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13ZM8 7.25v4M8 4.75h.01",
  keyboard: "M1.5 4.5h13v7h-13ZM4 7h.01M6.5 7h.01M9 7h.01M11.5 7h.01M5 9.5h6",
};

export type IconName = keyof typeof paths;

export function Icon({ name, size = 18 }: { name: IconName; size?: number }) {
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
