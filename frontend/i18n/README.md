# i18n (Internationalization) Yapısı

Bu proje, çok dilli destek için kategorize edilmiş ve düzenli bir i18n yapısı kullanır.

## 📁 Dosya Yapısı

```
frontend/i18n/
├── index.ts          # Ana i18n yapılandırması
├── helpers.ts        # Yardımcı fonksiyonlar ve sabitler
├── locales/          # Dil dosyaları
│   ├── de.json      # Almanca (varsayılan)
│   ├── en.json      # İngilizce
│   └── tr.json      # Türkçe
└── README.md         # Bu dosya
```

## 🌍 Desteklenen Diller

- **de** (Deutsch) - Varsayılan dil
- **en** (English) - İngilizce
- **tr** (Türkçe) - Türkçe

## 🏗️ Kategori Yapısı

### 1. Common (Genel)
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

### 2. Auth (Kimlik Doğrulama)
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

### 3. Cash Register (Kasa)
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

### 4. Payment (Ödeme)
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

### 5. Settings (Ayarlar)
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

## 🚀 Kullanım

### Temel Kullanım

```tsx
import { useTranslation } from 'react-i18next';

const MyComponent = () => {
  const { t } = useTranslation();
  
  return (
    <Text>{t('common.loading')}</Text>
  );
};
```

### Yardımcı Hook Kullanımı

```tsx
import { useI18n } from '../i18n/helpers';

const MyComponent = () => {
  const { t, getCurrentLanguage, changeLanguage } = useI18n();
  
  const handleLanguageChange = async (lang: string) => {
    await changeLanguage(lang);
  };
  
  return (
    <View>
      <Text>Mevcut Dil: {getCurrentLanguage()}</Text>
      <Text>{t('common.appName')}</Text>
      <Button onPress={() => handleLanguageChange('en')}>
        İngilizce'ye Geç
      </Button>
    </View>
  );
};
```

### Sabit Kullanımı

```tsx
import { I18N_KEYS } from '../i18n/helpers';

const MyComponent = () => {
  const { t } = useTranslation();
  
  return (
    <Text>{t(I18N_KEYS.COMMON.APP_NAME)}</Text>
  );
};
```

## 🔧 Yapılandırma

### Varsayılan Dil
```typescript
// frontend/i18n/index.ts
i18n.init({
  resources,
  lng: 'de', // Varsayılan dil Almanca
  fallbackLng: 'de',
  // ...
});
```

### Dil Değiştirme
```typescript
import { setLanguage } from '../i18n';

// Dil değiştir
await setLanguage('en');
```

## 📝 Yeni Çeviri Ekleme

### 1. Dil Dosyalarına Ekle
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

### 2. Sabitlere Ekle
```typescript
// frontend/i18n/helpers.ts
export const I18N_KEYS = {
  // ... mevcut kategoriler
  NEW_CATEGORY: {
    NEW_KEY: 'newCategory.newKey'
  }
};
```

### 3. Kullan
```tsx
const { t } = useTranslation();
<Text>{t('newCategory.newKey')}</Text>
// veya
<Text>{t(I18N_KEYS.NEW_CATEGORY.NEW_KEY)}</Text>
```

## 🧪 Test

### Çeviri Kontrolü
```typescript
import { useI18n } from '../i18n/helpers';

const { hasTranslation, getTranslation } = useI18n();

// Çeviri var mı kontrol et
if (hasTranslation('payment.title')) {
  console.log('Çeviri mevcut');
}

// Fallback ile çeviri al
const text = getTranslation('unknown.key', 'Varsayılan Metin');
```

## 📚 Best Practices

1. **Kategori Kullan**: Çevirileri mantıklı kategorilere ayırın
2. **Tutarlı İsimlendirme**: camelCase kullanın ve açıklayıcı isimler verin
3. **Sabitler**: Uzun çeviri anahtarları için I18N_KEYS sabitlerini kullanın
4. **Fallback**: Bilinmeyen anahtarlar için fallback değerler sağlayın
5. **Dil Kontrolü**: Çeviri mevcut olup olmadığını kontrol edin

## 🔍 Sorun Giderme

### Çeviri Görünmüyor
1. Dil dosyasında anahtarın mevcut olduğunu kontrol edin
2. JSON syntax'ını kontrol edin
3. Dosya import'larını kontrol edin

### Dil Değişmiyor
1. AsyncStorage izinlerini kontrol edin
2. i18n.changeLanguage çağrısını kontrol edin
3. Console hatalarını kontrol edin

## 📖 Ek Kaynaklar

- [react-i18next Dokümantasyonu](https://react.i18next.com/)
- [i18next Dokümantasyonu](https://www.i18next.com/)
- [Expo Localization](https://docs.expo.dev/versions/latest/sdk/localization/)
