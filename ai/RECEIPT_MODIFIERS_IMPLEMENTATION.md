# Fiş/Receipt ve Modifier Uygulama Özeti

Bu dokümanda POS fatura ve receipt tarafında modifier (Extra Zutaten) görünümü için yapılan backend ve frontend değişiklikleri özetlenir.

---

## 1. Receipt modeli (backend)

### ReceiptItem

- **parent_item_id** (nullable Guid): Ana ürün satırında `null`; modifier satırlarında ana satırın `item_id`'si. Böylece fişte hiyerarşi korunur.
- Migration: `AddReceiptItemParentItemId` ile `receipt_items.parent_item_id` eklendi.

### ReceiptItemDTO

- **itemId**: Satır benzersiz ID (görüntüleme/key için).
- **parentItemId**: Varsa bu satır bir modifier satırı; ana ürün satırının ItemId'si.
- **isModifierLine**: true ise Extra Zutaten satırı (fişte "+ Name €Price" gösterilir).

### RKSV / TSE

- Modifier satırları satış kalemi olarak sayılır; vergi gruplaması tüm satırlardan (ürün + modifier) yapılır.

---

## 2. Payment API request formatı

**PaymentItemRequest** (CreatePaymentRequest.Items elemanı):

| Alan        | Tip         | Açıklama |
|------------|-------------|----------|
| productId  | Guid        | Ürün ID |
| quantity   | int         | Miktar  |
| taxType    | string      | "standard" \| "reduced" \| "special" |
| modifierIds| List\<Guid\>| (Opsiyonel) Seçilen Extra Zutaten ID'leri. Backend fiyat/vergi hesaplar. |

Örnek request body (items):

```json
"items": [
  {
    "productId": "product-uuid",
    "quantity": 1,
    "taxType": "standard",
    "modifierIds": ["modifier-ketchup-uuid", "modifier-extra-fleisch-uuid"]
  }
]
```

- **PaymentService**: Her item için `ModifierIds` varsa `ProductModifier` kayıtlarını yükler, `CartMoneyHelper.ComputeLine` ile satır brüt/net/vergi hesaplar; toplam ve vergi detayına ekler. `PaymentItem.Modifiers` listesine snapshot (ModifierId, Name, UnitPrice, TotalPrice, TaxType, TaxRate, TaxAmount, LineNet) yazılır.
- **ReceiptService**: `CreateReceiptFromPaymentAsync` içinde her `PaymentItem` için önce ana `ReceiptItem`, sonra her modifier için `ParentItemId` atanmış ek `ReceiptItem` oluşturulur. Vergi satırları tüm kalemlerden (ürün + modifier) gruplanır.

---

## 3. Receipt JSON içeriği

- `GET /api/Receipt/{id}` (veya ilgili endpoint) dönen **ReceiptDTO.Items**:
  - Ana ürün satırları: `parentItemId == null`, `isModifierLine == false`.
  - Modifier satırları: `parentItemId == <ana satır itemId>`, `isModifierLine == true`.
- Fiş/fatura JSON'unda modifier'lar ayrı kalem olarak listelenir; RKSV/TSE açısından satış kalemi sayılır.

---

## 4. Receipt rendering (POS / frontend)

- **ReceiptTemplate** (`frontend/components/ReceiptTemplate.tsx`): `ReceiptItemDTO.isModifierLine === true` olan satırlar ana ürünün altında girintili gösterilir.
- Görüntüleme örneği:
  - **Döner** — €6.90 (ana satır)
  - **+ Extra Fleisch** — €1.50 (modifier, girintili)
  - **+ Ketchup** — €0.30 (modifier, girintili)
- **Toplam**: Ürün + tüm modifier satırları toplamı (backend'in `GrandTotal` değeri).
- Frontend `ReceiptDTO.items` sırası backend'deki gibi (ana satır hemen ardından kendi modifier'ları) kullanılır; `isModifierLine` ile sadece stil (girinti, "+ " öneki) uygulanır.

---

## 5. İlgili dosyalar

| Bölüm        | Dosya |
|-------------|--------|
| Backend model | `backend/Models/ReceiptItem.cs`, `backend/Models/PaymentItem.cs` |
| DTO         | `backend/DTOs/ReceiptDTO.cs`, `backend/DTOs/PaymentDTOs.cs` |
| Payment     | `backend/Services/PaymentService.cs` (modifier resolve + toplam) |
| Receipt     | `backend/Services/ReceiptService.cs` (item + modifier satırları, vergi) |
| Migration   | `backend/Migrations/*AddReceiptItemParentItemId*` |
| Frontend tip | `frontend/types/ReceiptDTO.ts` |
| Fiş UI      | `frontend/components/ReceiptTemplate.tsx` |
| Ödeme isteği | `frontend/services/api/paymentService.ts` (PaymentItem.modifierIds), `frontend/components/PaymentModal.tsx`, `frontend/app/(tabs)/_layout.tsx` (cartItems.modifiers) |
