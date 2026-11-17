# 0015. UI translations

- **Status:** Accepted
- **Date:** 2026-11-15, updated 2026-11-17 (error keys per module)

## Context

The repository is in English, but the users and the scraped portals are Polish.
The UI is in Polish and English from the start. Keeping a second language up to
date is cheap when every new text is translated in the same change (with AI help),
and expensive when done later for all screens at once. Texts are kept by key,
like `.resx` resources in .NET.

## Decision

- The frontend uses **i18next** with **react-i18next**, the most widely used React
  translation library.
- Every user-visible text comes from a dictionary by key. No hardcoded texts in components.

```tsx
const { t } = useTranslation("housing");
<h1>{t("offers.title")}</h1>
```

- Dictionaries are JSON files per language and module (one i18next namespace per module),
  so modules stay independent (ADR 0002):
  - `src/Web/App/locales/{pl,en}/common.json`: shared texts (buttons, errors, paging).
  - `src/Web/Modules/<Module>/locales/{pl,en}.json`: the module's texts.
- Keys are English and describe meaning, not wording (`offers.emptyState`).
- Languages: **Polish** (default) and **English**. Every new or changed text is added
  to both dictionaries in the same change.
- TypeScript checks keys against the Polish dictionary, so a mistyped key fails the
  build. A test fails when the two languages do not have the same set of keys.
- The user switches the language in the UI; the choice is remembered in the browser.
  Without a choice, the browser language is used if supported, otherwise Polish.
- Enum values and error codes from the API (ADR 0014) are translated by key
  (`status.<member name>`). An error code is `<area>.<name>`: a module's error is
  in that module's dictionary under `errors.<name>`, a shared one (scraping, database)
  in `common` under `errors.<area>.<name>`. The backend does not translate texts
  shown in the UI.
- Dates, numbers and money are formatted with the built-in `Intl` API in the current
  language (`pl-PL`, `en-GB`), not by hand.

## Alternatives considered

- **Hardcoded Polish texts:** fastest now, but adding a language later means touching every screen.
- **Polish only, English later:** less work now, but the keys and the second
  dictionary would drift and need a big catch-up.
- **Backend `.resx` resources:** the UI is in React, so texts belong in the frontend;
  the backend only returns codes.
- **FormatJS (react-intl), Lingui:** good libraries, less common than i18next.

## Consequences

- Every new text costs one entry in each language. Adding a third language means adding its files.
- Texts are easy to review and change in one place.
