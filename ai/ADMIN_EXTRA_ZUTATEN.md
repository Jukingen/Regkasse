# Admin: Extra Zutaten (Modifier) Yönetimi – API & UI Tasarımı

Next.js 14 (App Router) + Ant Design kullanan Admin panelde Products sayfasında **Extra Zutaten** yönetimi: Modifier Group CRUD, Modifier CRUD, ürünlere grup ataması ve Product Form içinde UI.

---

## 1. API Tasarımı

Base URL: `/api` (mevcut backend ile uyumlu).

### 1.1 Modifier Groups

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | `/api/modifier-groups` | Tüm modifier gruplarını listele (aktif; modifiers ile). |
| POST | `/api/modifier-groups` | Yeni modifier group oluştur. Body: `{ name, minSelections?, maxSelections?, isRequired?, sortOrder? }`. |
| PUT | `/api/modifier-groups/{id}` | Grup güncelle. |
| DELETE | `/api/modifier-groups/{id}` | Grup sil (soft delete veya cascade policy’e göre). |

**GET response (liste):**
```json
[
  {
    "id": "uuid",
    "name": "Saucen",
    "minSelections": 0,
    "maxSelections": null,
    "isRequired": false,
    "sortOrder": 0,
    "isActive": true,
    "modifiers": [
      { "id": "uuid", "name": "Ketchup", "price": 0.30, "taxType": 1, "sortOrder": 0 },
      { "id": "uuid", "name": "Mayo", "price": 0.30, "taxType": 1, "sortOrder": 1 }
    ]
  }
]
```

### 1.2 Modifiers (tekil modifier ekleme/güncelleme)

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| POST | `/api/modifier-groups/{groupId}/modifiers` | Gruba yeni modifier ekle. Body: `{ name, price?, taxType?, sortOrder? }`. |
| PUT | `/api/modifiers/{id}` | Modifier güncelle. |
| DELETE | `/api/modifiers/{id}` | Modifier sil. |

### 1.3 Ürüne atanmış modifier grupları

| Method | Endpoint | Açıklama |
|--------|----------|----------|
| GET | `/api/Product/{productId}/modifier-groups` | Ürüne atanmış modifier group ID listesi veya grup detayları. |
| POST | `/api/Product/{productId}/modifier-groups` | Ürüne modifier gruplarını ata. Body: `{ modifierGroupIds: string[] }`. Mevcut atamalar replace edilir. |

**GET response (ürüne atanmış gruplar):**
```json
[
  { "id": "group-uuid", "name": "Saucen", "modifiers": [...] },
  { "id": "group-uuid", "name": "Extras", "modifiers": [...] }
]
```

**POST body:**
```json
{
  "modifierGroupIds": ["group-uuid-1", "group-uuid-2"]
}
```

---

## 2. Admin UI Bileşen Önerisi

### 2.1 Yerleşim

- **Products** sayfası: Mevcut Product listesi + “New Product” / Edit modal.
- **Product Form** (modal) içinde yeni bölüm: **“Extra Zutaten”** (Collapse veya benzeri bir section).

### 2.2 Extra Zutaten Section (Product Form içinde)

- **Başlık:** "Extra Zutaten" (veya "Zulässige Modifier-Gruppen").
- **İçerik:** Tüm modifier grupları listelenir; her grup bir **satır** veya **Collapse panel**.
  - Grup adı yanında **checkbox**: Bu ürüne bu grup atanacak mı? (✔ Saucen, ✔ Extras, ☐ Beilagen.)
  - Grup **açıldığında** (expand): O gruptaki modifier’lar listelenir, sadece bilgi amaçlı (checkbox yok, sadece “bu grupta neler var” gösterimi).
  - Modifier satırı: `[ ] Ketchup €0.30` formatında (read-only; seçim ürün–grup ataması ile yapılıyor, modifier seçimi POS tarafında).

Özet:

- **Ürün formunda:** Sadece “hangi modifier **grupları** bu ürüne izinli” seçiliyor (checkbox list: Saucen, Extras, Beilagen).
- Grup expand edilince: O gruptaki modifier’lar ad + fiyat ile read-only listelenir.

### 2.3 Modifier Group & Modifier Yönetimi (ayrı sayfa veya modal)

Admin ayrıca:

1. **Modifier Group** oluşturabilmeli: Saucen, Extras, Beilagen (isim, isRequired, min/max selections, sortOrder).
2. **Modifier** ekleyebilmeli: Gruba bağlı; ad, fiyat (örn. Ketchup €0.30), taxType, sortOrder.

Bu, ayrı bir **“Modifier Groups”** veya **“Extra Zutaten”** sayfasında (Settings altında veya Products yanında) yapılabilir:

- Liste: Tüm gruplar; her satırda grup adı, modifier sayısı, düzenle/sil.
- “Add Group” → Modal: name, minSelections, maxSelections, isRequired, sortOrder.
- Grup satırında “Modifiers” veya expand: Modifier listesi; “Add Modifier” → name, price, taxType, sortOrder.

İlk aşamada sadece Product Form’daki “Extra Zutaten” section’ı (grup checkbox + expand’da modifier listesi) zorunlu; grup/modifier CRUD ayrı bir sayfada olabilir.

### 2.4 Form state ve submit

- Product Form’da alan adı örn. `modifierGroupIds: string[]`.
- Kaydet’te:
  - Ürün create/update sonrası (veya aynı anda) `POST /api/Product/{id}/modifier-groups` ile `{ modifierGroupIds }` gönderilir.
- Açılışta (edit): `GET /api/Product/{id}/modifier-groups` ile mevcut atamalar alınır, checkbox’lar işaretlenir.

---

## 3. Önerilen Dosya Yapısı (Admin)

```
frontend-admin/src/
├── app/(protected)/products/page.tsx          # Mevcut (ProductForm kullanır)
├── features/products/components/
│   ├── ProductForm.tsx                        # ExtraZutatenSection eklenir
│   └── ExtraZutatenSection.tsx                # Yeni: grup listesi, checkbox, expand modifiers
├── features/modifier-groups/                  # Opsiyonel: grup CRUD sayfası
│   ├── hooks/useModifierGroups.ts
│   └── components/ModifierGroupForm.tsx
└── api/ (veya lib/api)
    └── modifierGroups.ts                      # getModifierGroups, setProductModifierGroups
```

---

## 4. Özet

- **API:** `GET/POST/PUT/DELETE /api/modifier-groups`, `POST /api/modifier-groups/{id}/modifiers`, `GET/POST /api/Product/{id}/modifier-groups`.
- **Product Form:** “Extra Zutaten” section; grup bazlı checkbox; expand’da modifier listesi (read-only).
- **Grup/Modifier CRUD:** İstersen ayrı sayfa (Modifier Groups) ile; Product Form sadece atama yapar.

Bu doküman, backend controller’lar ve Admin UI bileşenleri implementasyonu için referans alınır.

---

## 5. Uygulama Özeti (Yapılanlar)

- **Backend**
  - `ModifierGroupsController`: `GET/POST/PUT/DELETE /api/modifier-groups`, `POST /api/modifier-groups/{groupId}/modifiers`
  - `ProductController`: `GET /api/Product/{id}/modifier-groups`, `POST /api/Product/{id}/modifier-groups` (body: `{ modifierGroupIds: string[] }`)
  - Entity: `ProductModifierGroup`, `ProductModifier`, `ProductModifierGroupAssignment`; migration: `AddProductModifiers`
- **Admin**
  - `src/lib/api/modifierGroups.ts`: `getModifierGroups`, `getProductModifierGroups`, `setProductModifierGroups`
  - `ExtraZutatenSection`: Product Form içinde grup checkbox + expand’da modifier listesi (read-only)
  - Product Form submit: `modifierGroupIds` sayfa tarafında create/update sonrası `setProductModifierGroups` ile kaydedilir
- **Modifier Group / Modifier CRUD**: API hazır; gruplar ve modifier’lar için ayrı bir “Modifier Groups” admin sayfası (liste, ekleme, düzenleme) istenirse eklenebilir (örn. Settings veya Products yanında).
