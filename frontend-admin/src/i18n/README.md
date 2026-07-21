# Admin i18n (frontend-admin)

## Source of truth (sıra)

1. **Runtime çeviri:** `src/i18n/config.ts` — `catalogs[locale]` altındaki **catalog anahtarları** (`AdminNamespace`) ve import edilen JSON dosyaları.
2. **`t('…')` çözümlemesi:** `I18nProvider` — ilk segment = catalog adı (aşağıdaki “Runtime namespace” sütunu).
3. **Araç / parity / CSV:** `localization/namespace-manifest.json` — `frontend-admin.namespaces` listesi **locale dosya adlarıyla** uyumlu olmalı (`de/<stem>.json`).

JSON kataloglar: `src/i18n/locales/{de,en,tr}/`. Varsayılan metin dili: **`de`** (`DEFAULT_TEXT_LOCALE`).

---

## Text locale vs format locale

- **Metin dili:** `de` | `en` | `tr` — `textLocale` (`I18nProvider`).
- **Format dili (Intl):** `de-AT` | `en-US` | `tr-TR` — `formatLocale`; `TEXT_TO_FORMAT_LOCALE` ile metin dilinden türetilir.

### Formatting (`src/i18n/formatting.ts`)

Tek yüzey: `formatCurrency`, `formatNumber`, `formatPercent`, `formatDate`, `formatDateTime` — hepsi `(…, formatLocale, …)` ile `useI18n().formatLocale` alır. Çok kullanımda `createIntlFormatters(formatLocale)` ile bağlı helper’lar üretilebilir.

**Örnek:**

```tsx
const { formatLocale } = useI18n();
const fmt = useMemo(() => createIntlFormatters(formatLocale), [formatLocale]);
return <span>{fmt.formatCurrency(row.amount)}</span>;
```

**Yüzde:** `formatPercent` Intl kurallarına uyar — değer **0–1 aralığında** oran (ör. `0,2` → %20).

**EUR:** `formatCurrency` varsayılan `currency: 'EUR'`, 2 ondalık.

**Kullanımdan kaçının:** `new Intl.NumberFormat('de-AT', …)` doğrudan; `toFixed(2) + '€'`; sabit `'de-DE'` / `'de-AT'` locale string’leri (formatLocale dışında).

---

## `t(key)` biçimi

- `namespace.path.to.leaf` — ilk nokta öncesi segment = **runtime namespace** (`config.ts` ile birebir).
- Alternatif: `namespace:path.to.leaf` (aynı anlama).

Örnek: `adminShell.hospitalityHub.title` → namespace `adminShell`, path `hospitalityHub.title`.

---

## Runtime namespace ↔ dosya adı (kebab / camel)

| Runtime (`t` ilk segmenti, `AdminNamespace`) | Locale dosyası (`de/…`)           | Not                                                                |
| -------------------------------------------- | --------------------------------- | ------------------------------------------------------------------ |
| `adminShell`                                 | `admin-shell.json`                | Tek istisna: dosya **kebab-case**, catalog anahtarı **camelCase**. |
| `common`                                     | `common.json`                     |                                                                    |
| `nav`                                        | `nav.json`                        |                                                                    |
| `users`                                      | `users.json`                      |                                                                    |
| `settings`                                   | `settings.json`                   |                                                                    |
| `products`                                   | `products.json`                   |                                                                    |
| `finanzOnlineOutbox`                         | `finanzOnlineOutbox.json`         |                                                                    |
| `finanzOnlineReconciliation`                 | `finanzOnlineReconciliation.json` |                                                                    |
| `rksvHub`                                    | `rksvHub.json`                    |                                                                    |

**`localization/namespace-manifest.json`** içindeki `frontend-admin.namespaces` değerleri **dosya kök adı**dır (`admin-shell`, `finanzOnlineOutbox`, …); runtime string ile karakter bazında her zaman aynı değildir — `admin-shell` ↔ `adminShell` eşlemesi validate ve import/export script’lerinde kebab/camel ile hizalanır.

---

## Kurallar

- Yalnızca UI metni; ürün/kategori gibi API alanlarını `t()` üzerinden geçirme.
- Anahtarları stabil tut; toplu rename için migration notu gerekir.
