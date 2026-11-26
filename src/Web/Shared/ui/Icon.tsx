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
  calendar: "M2.5 3.5h11v10h-11ZM2.5 6.5h11M5.5 2v3M10.5 2v3",
  clock: "M8 14.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13ZM8 4.5V8l2.5 1.5",
  user: "M8 8a2.5 2.5 0 1 0 0-5 2.5 2.5 0 0 0 0 5ZM3 13.5c.5-2.5 2.5-3.5 5-3.5s4.5 1 5 3.5",
  search: "M7 12a5 5 0 1 0 0-10 5 5 0 0 0 0 10ZM10.5 10.5 14 14",
  file: "M4 1.5h5.5l3 3v10H4ZM9.5 1.5v3h3M6 8h4.5M6 10.5h4.5",
  refresh: "M13 2.5v3.5H9.5M3 13.5V10h3.5M12.6 6A5 5 0 0 0 3.6 5M3.4 10a5 5 0 0 0 9 1",
  globe:
    "M8 14.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13ZM1.5 8h13M8 1.5c1.8 1.9 2.5 4.2 2.5 6.5s-.7 4.6-2.5 6.5M8 1.5C6.2 3.4 5.5 5.7 5.5 8s.7 4.6 2.5 6.5",
  pin: "M8 14.5s4.5-4 4.5-8a4.5 4.5 0 0 0-9 0c0 4 4.5 8 4.5 8ZM8 8.25a1.75 1.75 0 1 0 0-3.5 1.75 1.75 0 0 0 0 3.5Z",
  calculator:
    "M3.5 1.5h9v13h-9ZM5.5 4h5v2.5h-5ZM5.5 9h.01M8 9h.01M10.5 9h.01M5.5 11.5h.01M8 11.5h.01M10.5 11.5h.01",
  trash: "M2.5 4h11M6 4V2.5h4V4M4 4l.75 10h6.5L12 4M6.75 6.5v5M9.25 6.5v5",
  // Run states: a ring with what happened inside.
  succeeded: "M8 14.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13ZM5.5 8.25l1.75 1.75 3.25-3.5",
  failed: "M8 14.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13ZM6 6l4 4M10 6l-4 4",
  running: "M8 14.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13ZM6.75 5.5v5l4-2.5Z",
  queued: "M4.5 2h7M4.5 14h7M5 2c0 3 3 3.5 3 6s-3 3-3 6M11 2c0 3-3 3.5-3 6s3 3 3 6",
  interrupted: "M8 14.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13ZM6.5 6v4M9.5 6v4",
  canceled: "M8 14.5a6.5 6.5 0 1 0 0-13 6.5 6.5 0 0 0 0 13ZM3.4 12.6l9.2-9.2",
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
