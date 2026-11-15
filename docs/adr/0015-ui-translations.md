# 0015. UI translations

- **Status:** Accepted
- **Date:** 2026-11-15

## Context

The repository is in English, but the users and the scraped portals are Polish.
The UI starts in Polish only. Adding another language later must not mean going
through every screen and replacing hardcoded text, so texts must be kept by key
from the start, like `.resx` resources in .NET.

## Decision

- The frontend uses **i18next** with **react-i18next**, the most widely used React
  translation library.
- Every user-visible text comes from a dictionary by key. No hardcoded texts in components.

```tsx
const { t } = useTranslation("flatOffers");
<h1>{t("offers.title")}</h1>
```

- Dictionaries are JSON files per language and module (one i18next namespace per module),
  so modules stay independent (ADR 0002):
  - `web/src/locales/pl/common.json`: shared texts (buttons, errors, paging).
  - `web/src/modules/<module>/locales/pl.json`: the module's texts.
- Keys are English and describe meaning, not wording (`offers.emptyState`).
- Polish is the only language and the default. TypeScript checks keys against the
  Polish dictionary, so a missing or mistyped key fails the build.
- Enum values and error codes from the API (ADR 0014) are translated by key
  (`status.Interested`, `errors.flatOffers.offerNotFound`). The backend does not
  translate texts shown in the UI.
- Dates, numbers and money are formatted with the built-in `Intl` API in the current
  language (`pl-PL`), not by hand.

## Alternatives considered

- **Hardcoded Polish texts:** fastest now, but adding a language later means touching every screen.
- **Backend `.resx` resources:** the UI is in React, so texts belong in the frontend;
  the backend only returns codes.
- **FormatJS (react-intl), Lingui:** good libraries, less common than i18next.

## Consequences

- Every new text costs one dictionary entry. Adding English later means adding `en` files.
- Texts are easy to review and change in one place.
