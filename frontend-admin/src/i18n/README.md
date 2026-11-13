# Admin i18n (`frontend-admin`)

## Source of truth (order)

1. **Runtime translation:** `src/i18n/config.ts` — **catalog keys** under `catalogs[locale]` (`AdminNamespace`) and the imported JSON files.
2. **`t('…')` resolution:** `I18nProvider` — first segment = catalog name (see the “Runtime namespace” column below).
3. **Tooling / parity / CSV:** `localization/namespace-manifest.json` — the `frontend-admin.namespaces` list must match **locale file names** (`de/<stem>.json`).

JSON catalogs: `src/i18n/locales/{de,en,tr}/`. Default text locale: **`de`** (`DEFAULT_TEXT_LOCALE`).

---

## Text locale vs format locale

- **Text language:** `de` | `en` | `tr` — `textLocale` (`I18nProvider`).
- **Format locale (Intl):** `de-AT` | `en-US` | `tr-TR` — `formatLocale`; derived from the text language via `TEXT_TO_FORMAT_LOCALE`.

### Formatting (`src/i18n/formatting.ts`)

Single surface: `formatCurrency`, `formatNumber`, `formatPercent`, `formatDate`, `formatDateTime` — all take `(…, formatLocale, …)` from `useI18n().formatLocale`. For repeated use, build bound helpers with `createIntlFormatters(formatLocale)`.

**Example:**

```tsx
const { formatLocale } = useI18n();
const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);
return <span>{fmt.formatCurrency(row.amount)}</span>;
```

**Percent:** `formatPercent` follows Intl rules — value is a **0–1 ratio** (for example `0.2` → 20%).

**EUR:** `formatCurrency` defaults to `currency: 'EUR'`, 2 fraction digits.

**Avoid:** calling `new Intl.NumberFormat('de-AT', …)` directly; `toFixed(2) + '€'`; hardcoded `'de-DE'` / `'de-AT'` locale strings (except via `formatLocale`).

---

## `t(key)` format

- `namespace.path.to.leaf` — the segment before the first dot is the **runtime namespace** (must match `config.ts`).
- Alternative: `namespace:path.to.leaf` (same meaning).

Example: `adminShell.hospitalityHub.title` → namespace `adminShell`, path `hospitalityHub.title`.

---

## Runtime namespace ↔ file name (kebab / camel)

| Runtime (`t` first segment, `AdminNamespace`) | Locale file (`de/…`)              | Note                                                               |
| --------------------------------------------- | --------------------------------- | ------------------------------------------------------------------ |
| `adminShell`                                  | `admin-shell.json`                | Only exception: file is **kebab-case**, catalog key is **camelCase**. |
| `common`                                      | `common.json`                     |                                                                    |
| `nav`                                         | `nav.json`                        |                                                                    |
| `users`                                       | `users.json`                      |                                                                    |
| `settings`                                    | `settings.json`                   |                                                                    |
| `products`                                    | `products.json`                   |                                                                    |
| `finanzOnlineOutbox`                          | `finanzOnlineOutbox.json`         |                                                                    |
| `finanzOnlineReconciliation`                  | `finanzOnlineReconciliation.json` |                                                                    |
| `rksvHub`                                     | `rksvHub.json`                    |                                                                    |

Values in **`localization/namespace-manifest.json`** → `frontend-admin.namespaces` are **file stems** (`admin-shell`, `finanzOnlineOutbox`, …). They are not always character-identical to the runtime string — `admin-shell` ↔ `adminShell` is aligned in validate and import/export scripts via kebab/camel mapping.

---

## Rules

- UI copy only; do not run product/category API fields through `t()`.
- Keep keys stable; bulk renames need a migration note.
