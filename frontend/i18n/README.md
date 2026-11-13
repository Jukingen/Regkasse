# i18n structure

This project uses a categorized, consistent i18n layout.

## Stabilization notes (POS)

- **Canonical resources**: JSON files under `locales/{de,en,tr}/*.json` (namespace per file). CSV is not source of truth.
- **Text vs formatting locale**: UI language codes are `de` | `en` | `tr` (`localeUtils.ts`). `Intl` / `toLocaleString` should use `getFormattingLocaleForTextLocale(...)` (maps e.g. `de` → `de-AT`, `en` → `en-US`). Shared helpers: `formatting.ts` (`formatDateTime`, `formatNumber`, …).
- **`products` vs `catalog_ui`**: Both namespaces point to the same `products.json` bundle (alias for future rename); prefer `products:*` keys in code.
- **Domain data**: Category names and modifier group labels are not passed through `t()`. **Product names/descriptions** use API fields `nameDe` / `nameEn` / `nameTr` (and descriptions) resolved via `utils/productLocalization.ts` and the language chosen in Settings (`LanguageSelector`).

## File layout

```
frontend/i18n/
├── index.ts          # Main i18n configuration
├── helpers.ts        # Helper functions and constants
├── locales/          # Locale files
│   ├── de.json      # German (default)
│   ├── en.json      # English
│   └── tr.json      # Turkish
└── README.md         # This file
```

## Supported languages

- **de** (Deutsch) — default
- **en** (English)
- **tr** (Türkçe)

## Category structure

### 1. Common

```json
{
  "common": {
    "appName": "KasseAPP",
    "loading": "Laden...",
    "error": "Fehler",
    "success": "Erfolg",
    "save": "Speichern",
    "cancel": "Abbrechen",
    "delete": "Löschen",
    "edit": "Bearbeiten",
    "search": "Suchen...",
    "back": "Zurück",
    "continue": "Weiter",
    "confirm": "Bestätigen",
    "close": "Schließen",
    "yes": "Ja",
    "no": "Nein",
    "step": "Schritt"
  }
}
```

### 2. Auth

```json
{
  "auth": {
    "login": "Anmelden",
    "logout": "Abmelden",
    "email": "E-Mail",
    "password": "Passwort",
    "forgotPassword": "Passwort vergessen?",
    "loginError": "Anmeldung fehlgeschlagen",
    "invalidEmail": "Ungültige E-Mail-Adresse",
    "invalidPassword": "Ungültiges Passwort",
    "required": "{{field}} ist erforderlich"
  }
}
```

### 3. Cash register

```json
{
  "cashRegister": {
    "title": "Kasse",
    "cart": "Warenkorb",
    "total": "Gesamt",
    "checkout": "Bezahlen",
    "addToCart": "In den Warenkorb",
    "removeFromCart": "Aus Warenkorb entfernen",
    "quantity": "Menge",
    "price": "Preis",
    "product": "Produkt",
    "products": "Produkte",
    "stock": "Lager",
    "outOfStock": "Nicht verfügbar",
    "subtotal": "Zwischensumme",
    "tax": "Steuer",
    "discount": "Rabatt"
  }
}
```

### 4. Payment

```json
{
  "payment": {
    "title": "Zahlungsvorgang",
    "customerSelection": "Kundenauswahl",
    "paymentMethod": "Zahlungsmethode",
    "paymentAmount": "Zahlungsbetrag",
    "tseVerification": "TSE-Verifizierung",
    "confirmation": "Bestätigung",
    "receipt": "Beleg",

    "stepTitles": { ... },
    "customer": { ... },
    "methods": { ... },
    "amount": { ... },
    "tse": { ... },
    "confirmation": { ... },
    "receipt": { ... },
    "buttons": { ... },
    "errors": { ... },
    "cancellation": { ... }
  }
}
```

### 5. Settings

```json
{
  "settings": {
    "title": "Einstellungen",
    "language": "Sprache",
    "theme": "Design",
    "notifications": "Benachrichtigungen",
    "darkMode": "Dunkelmodus",
    "lightMode": "Hellmodus",
    "systemTheme": "System",
    "german": "Deutsch",
    "english": "Englisch",
    "turkish": "Türkçe"
  }
}
```

## Usage

### Basic usage

```tsx
import { useTranslation } from 'react-i18next';

const MyComponent = () => {
  const { t } = useTranslation();

  return <Text>{t('common.loading')}</Text>;
};
```

### Helper hook

```tsx
import { useI18n } from '../i18n/helpers';

const MyComponent = () => {
  const { t, getCurrentLanguage, changeLanguage } = useI18n();

  const handleLanguageChange = async (lang: string) => {
    await changeLanguage(lang);
  };

  return (
    <View>
      <Text>Current language: {getCurrentLanguage()}</Text>
      <Text>{t('common.appName')}</Text>
      <Button onPress={() => handleLanguageChange('en')}>Switch to English</Button>
    </View>
  );
};
```

### Using constants

```tsx
import { I18N_KEYS } from '../i18n/helpers';

const MyComponent = () => {
  const { t } = useTranslation();

  return <Text>{t(I18N_KEYS.COMMON.APP_NAME)}</Text>;
};
```

## Configuration

### Default language

```typescript
// frontend/i18n/index.ts
i18n.init({
  resources,
  lng: 'de', // Default language: German
  fallbackLng: 'de',
  // ...
});
```

### Changing language

```typescript
import { setLanguage } from '../i18n';

await setLanguage('en');
```

## Adding a new translation

### 1. Add to locale files

```json
// frontend/i18n/locales/de.json
{
  "newCategory": {
    "newKey": "Neuer Wert"
  }
}

// frontend/i18n/locales/en.json
{
  "newCategory": {
    "newKey": "New Value"
  }
}

// frontend/i18n/locales/tr.json
{
  "newCategory": {
    "newKey": "Yeni Değer"
  }
}
```

### 2. Add constants

```typescript
// frontend/i18n/helpers.ts
export const I18N_KEYS = {
  // ... existing categories
  NEW_CATEGORY: {
    NEW_KEY: 'newCategory.newKey',
  },
};
```

### 3. Use it

```tsx
const { t } = useTranslation();
<Text>{t('newCategory.newKey')}</Text>
// veya
<Text>{t(I18N_KEYS.NEW_CATEGORY.NEW_KEY)}</Text>
```

## Tests

### Translation check

```typescript
import { useI18n } from '../i18n/helpers';

const { hasTranslation, getTranslation } = useI18n();

if (hasTranslation('payment.title')) {
  console.log('Translation exists');
}

const text = getTranslation('unknown.key', 'Default text');
```

## Best practices

1. **Use categories:** Split translations into logical groups.
2. **Consistent naming:** camelCase and descriptive keys.
3. **Constants:** Use `I18N_KEYS` for long translation keys.
4. **Fallback:** Provide fallback values for unknown keys.
5. **Existence check:** Verify a translation exists before relying on it.

## Troubleshooting

### Translation not shown

1. Confirm the key exists in the locale file.
2. Validate JSON syntax.
3. Check file imports.

### Language does not change

1. Check AsyncStorage permissions.
2. Confirm `i18n.changeLanguage` is called.
3. Check console errors.

## Further reading

- [react-i18next documentation](https://react.i18next.com/)
- [i18next documentation](https://www.i18next.com/)
- [Expo Localization](https://docs.expo.dev/versions/latest/sdk/localization/)
