# 0011. Frontend stack

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

ADR 0003 sets React 19 with TypeScript but nothing else about the frontend. Before
Phase 1 we need a build tool, routing, data fetching, a way to keep frontend types in
sync with the API, and a decision on how the frontend is served. We want the most
standard React choices, not a custom setup.

The UI is mostly lists and forms (offers, filters, statuses, schedules). It must be
functional and easy for a non-technical person, without visual extras, and styled
our own way.

## Decision

### Tooling and libraries
- **Vite** builds and serves the app in development. **npm** is the package manager.
- **React Router** for routing (library mode, no framework/SSR features).
- **TanStack Query** for all server data (loading, caching, refetching). No global
  state library; local state uses React state.
- **Headless components:** **Base UI** (`@base-ui/react`) provides behavior and
  accessibility of dialogs, menus, selects, comboboxes etc., with no styles.
  **TanStack Table** does the same for data tables (sorting, filtering, paging).
  Our own styled components built on them live in `web/src/ui/`; modules use only those.
- **Styling:** plain, native CSS in **CSS Modules** (`Button.module.css` next to
  `Button.tsx`, built into Vite). Colors, spacing and fonts are CSS custom properties
  (`--color-primary`) defined once. No Tailwind, no SCSS: native CSS already has
  variables and nesting.
- **ESLint** and **Prettier** for linting and formatting; **Vitest** with Testing
  Library for tests.

### API types generated from OpenAPI
- The Host publishes an OpenAPI document with the built-in ASP.NET Core OpenAPI
  support. It is written to a file during the build.
- **openapi-typescript** generates TypeScript types from that file into
  `web/src/api/schema.ts`; **openapi-fetch** is a small typed `fetch` wrapper that uses
  them. No generated client classes.
- The generated file is committed. CI regenerates it and fails if it differs, so a
  backend change cannot silently break the frontend.

```ts
const { data } = await api.GET("/api/flat-offers/offers", { params: { query: { page: 1 } } });
//      ^ typed from the C# endpoint's response
```

### One container
- In production the Host serves the built frontend as static files from `wwwroot`,
  with a fallback to `index.html` for client-side routes. Bastion is one container
  (plus the database).
- In development Vite runs its own dev server with hot reload and proxies `/api`
  to the backend.
- The Docker image is built in stages: Node builds the frontend, the .NET SDK
  publishes the Host, the runtime image contains both.

## Alternatives considered

- **Next.js / React Router framework mode:** server rendering and file routing we do
  not need for a single-user app behind one API; more moving parts.
- **Full component libraries (MUI, Mantine, Ant Design, DevExpress):** fast to start,
  but their look is hard to change and they are heavy. Headless keeps behavior and lets us style.
- **shadcn/ui with Tailwind:** very popular (State of React 2025) and gives ready
  component code on top of Base UI, but requires Tailwind's utility classes in markup,
  which we find noisy. **Radix UI** and **React Aria:** good headless alternatives;
  Base UI is newer, has a combobox and is now the default base of shadcn/ui.
- **SCSS:** its main features (variables, nesting) are now in native CSS.
- **Redux / Zustand:** global state is not needed when server data lives in TanStack Query.
- **NSwag, Kiota, Orval (generated clients):** more generated code and configuration
  than types plus a thin `fetch` wrapper.
- **Separate frontend container (nginx):** one more container and CORS or proxy
  setup, with no benefit for one user.

## Consequences

- Standard, well-documented tools; most React developers know them.
- Frontend and backend types cannot drift apart unnoticed.
- One image to build and deploy; the build needs both Node and the .NET SDK.
- We style every component ourselves, so the first screens take a bit longer than with
  ready-made components.
