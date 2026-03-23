# Admin i18n (frontend-admin)

Runtime catalogs live under `src/i18n/locales/{de,en,tr}/`. Default text locale is **`de`** (`config.ts`).

## Text locale vs formatting locale

- **Translation locale** (catalog keys): `de` | `en` | `tr` — `textLocale` in `I18nProvider`.
- **Formatting locale** (`Intl`, dates, numbers): `de-AT` | `en-US` | `tr-TR` — `formatLocale`, default derived from text locale via `TEXT_TO_FORMAT_LOCALE` in `config.ts`.
- Use `formatLocale` from `useI18n()` for `toLocaleString` / `Intl.*`; use `formatting.ts` helpers where convenient.

## `t(key)` shape

The first dot segment is the **namespace** (matches the JSON file base name, camelCase in catalog):

- `nav.overview` → namespace `nav`, path `overview`
- `settings.page.title` → namespace `settings`, path `page.title`

Namespaces registered in `config.ts`: `adminShell`, `common`, `nav`, `users`, `settings`, `products`.

## Rules

- UI copy only; do not pass API/domain catalog data (product names, categories, etc.) through `t()`.
- Prefer stable semantic keys; avoid renaming keys without a migration note.
