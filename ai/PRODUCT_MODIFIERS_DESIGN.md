# Product Modifiers (Extra Zutaten) – Tasarım Dokümanı

Bu dokümanda POS için **Product Modifier / Extra Zutaten** özelliğinin domain modeli, veritabanı şeması, entity yapısı ve API payload formatı önerilir. Mevcut `Product`, `CartItem`, `OrderItem`, `PaymentItem`, `ReceiptItem` ve `PaymentService`/`ReceiptService` akışları dikkate alınmıştır.

---

## 1. Domain model özeti

- **Product**  
  Mevcut ürün. Hangi modifier gruplarının bu ürüne uygulanabileceği **Product ↔ ProductModifierGroup** çoktan çoğa ilişki ile tanımlanır.

- **ProductModifierGroup** (örn. "Saucen", "Extras")  
  Grup adı, zorunluluk, min/max seçim sayısı ve sıra bilgisi. Bir grupta birden fazla modifier olur.

- **ProductModifier** (örn. "Ketchup", "Mayo", "Extra Fleisch")  
  Grupa bağlı; ad, fiyat, vergi tipi, sıra. İsteğe bağlı / çoklu seçim grup ayarına göre uygulanır.

İlişkiler:

- **Product** ↔ **ProductModifierGroup**: M:N (bir ürün birden fazla gruba sahip olabilir; bir grup birden fazla ürüne atanabilir).
- **ProductModifierGroup** → **ProductModifier**: 1:N (bir grupta birden fazla modifier).

Kurallar:

- Her modifier isteğe bağlı **fiyat** içerebilir (0 veya pozitif).
- Grup bazında: **optional** (seçim zorunlu değil), **required** (en az 1 seçim zorunlu), **min/max selections** (çoklu seçim sınırı).
- Modifier’lar da **item** gibi vergilendirilir (TaxType/TaxRate; RKSV uyumu).

---

## 2. Önerilen veritabanı modeli (PostgreSQL)

Tablo isimleri ve sütunlar mevcut proje stilinde (snake_case, `decimal(18,2)` para alanları) tutulmuştur.

### 2.1 `product_modifier_groups`

| Sütun           | Tip             | Açıklama |
|-----------------|-----------------|----------|
| id              | uuid, PK        | |
| name            | varchar(100)    | Grup adı (örn. "Saucen", "Extras") |
| min_selections  | int, default 0  | En az kaç modifier seçilmeli |
| max_selections  | int, nullable   | En fazla kaç modifier (null = sınırsız) |
| is_required     | boolean, default false | Grup zorunlu mu (en az 1 seçim) |
| sort_order      | int, default 0  | Sıralama |
| created_at      | timestamptz     | |
| updated_at      | timestamptz     | |
| is_active       | boolean, default true | |

### 2.2 `product_modifiers`

| Sütun           | Tip             | Açıklama |
|-----------------|-----------------|----------|
| id              | uuid, PK        | |
| modifier_group_id | uuid, FK → product_modifier_groups | |
| name            | varchar(200)    | Modifier adı (Ketchup, Extra Fleisch, vb.) |
| price           | decimal(18,2), default 0 | Ek fiyat (0 = ücretsiz) |
| tax_type        | int             | Product.TaxType ile uyumlu (1=Standard, 2=Reduced, 3=Special, 4=ZeroRate) |
| sort_order      | int, default 0  | Grupta sıra |
| created_at      | timestamptz     | |
| updated_at      | timestamptz     | |
| is_active       | boolean, default true | |

- FK: `modifier_group_id` → `product_modifier_groups(id)` ON DELETE CASCADE (veya RESTRICT, iş kuralına göre).

### 2.3 `product_modifier_group_assignments` (Product ↔ ModifierGroup M:N)

| Sütun             | Tip   | Açıklama |
|-------------------|-------|----------|
| product_id        | uuid, FK → products | |
| modifier_group_id | uuid, FK → product_modifier_groups | |
| sort_order        | int, default 0 | Üründe grupların sırası |

- PK: (product_id, modifier_group_id).  
- Böylece admin panelde “Döner” için hangi grupların (Saucen, Extras) gösterileceği atanır.

### 2.4 `cart_item_modifiers` (sepet satırına seçilen modifier’lar)

| Sütun      | Tip   | Açıklama |
|------------|-------|----------|
| id         | uuid, PK | |
| cart_item_id | uuid, FK → cart_items | |
| modifier_id  | uuid, FK → product_modifiers | |
| created_at | timestamptz | |

- Unique: (cart_item_id, modifier_id) – aynı satırda aynı modifier tekrar seçilemez.  
- Sepete ürün eklerken/ödeme oluştururken bu tablo “bu satırda hangi modifier’lar seçildi” bilgisini taşır.

---

## 3. Entity sınıf yapısı (backend)

Mevcut `BaseEntity` (Id, CreatedAt, UpdatedAt, CreatedBy, UpdatedBy, IsActive) kullanılabilir; gruplar ve modifier’lar için uygun.

### 3.1 ProductModifierGroup

```csharp
[Table("product_modifier_groups")]
public class ProductModifierGroup : BaseEntity
{
    [Required]
    [Column("name")]
    [MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Column("min_selections")]
    public int MinSelections { get; set; }

    [Column("max_selections")]
    public int? MaxSelections { get; set; }

    [Column("is_required")]
    public bool IsRequired { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    public virtual ICollection<ProductModifier> Modifiers { get; set; } = new List<ProductModifier>();
    public virtual ICollection<ProductModifierGroupAssignment> ProductAssignments { get; set; } = new List<ProductModifierGroupAssignment>();
}
```

### 3.2 ProductModifier

```csharp
[Table("product_modifiers")]
public class ProductModifier : BaseEntity
{
    [Required]
    [Column("modifier_group_id")]
    public Guid ModifierGroupId { get; set; }

    [Required]
    [Column("name")]
    [MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Column("price", TypeName = "decimal(18,2)")]
    public decimal Price { get; set; }

    [Required]
    [Column("tax_type")]
    public int TaxType { get; set; } = 1;

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [ForeignKey("ModifierGroupId")]
    public virtual ProductModifierGroup ModifierGroup { get; set; } = null!;
}
```

### 3.3 ProductModifierGroupAssignment

```csharp
[Table("product_modifier_group_assignments")]
public class ProductModifierGroupAssignment
{
    [Column("product_id")]
    public Guid ProductId { get; set; }

    [Column("modifier_group_id")]
    public Guid ModifierGroupId { get; set; }

    [Column("sort_order")]
    public int SortOrder { get; set; }

    [ForeignKey("ProductId")]
    public virtual Product Product { get; set; } = null!;
    [ForeignKey("ModifierGroupId")]
    public virtual ProductModifierGroup ModifierGroup { get; set; } = null!;
}
```

Product entity’sine navigation:

```csharp
public virtual ICollection<ProductModifierGroupAssignment> ModifierGroupAssignments { get; set; } = new List<ProductModifierGroupAssignment>();
```

### 3.4 CartItemModifier (sepet satırı – seçilen modifier)

```csharp
[Table("cart_item_modifiers")]
public class CartItemModifier
{
    [Key]
    [Column("id")]
    public Guid Id { get; set; } = Guid.NewGuid();

    [Required]
    [Column("cart_item_id")]
    public Guid CartItemId { get; set; }

    [Required]
    [Column("modifier_id")]
    public Guid ModifierId { get; set; }

    [Column("created_at")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    [ForeignKey("CartItemId")]
    public virtual CartItem CartItem { get; set; } = null!;
    [ForeignKey("ModifierId")]
    public virtual ProductModifier Modifier { get; set; } = null!;
}
```

CartItem’a:

```csharp
public virtual ICollection<CartItemModifier> SelectedModifiers { get; set; } = new List<CartItemModifier>();
```

---

## 4. PaymentItem (JSON) ve fiş tarafı

Ödeme tek kaynağı `payment_details.PaymentItems` (JSON). Modifier’ları da burada snapshot olarak tutmak gerekir (fiyat/vergi değişse bile fişte aynı kalır).

### 4.1 PaymentItem’a eklenecek alan

- **Modifiers** (liste): Satır bazında seçilen her modifier için bir kayıt; fişte “+ Extra Fleisch €1.50” gibi satırlar ve vergi toplamları buna göre üretilir.

Önerilen DTO (JSON’da saklanacak):

```csharp
public class PaymentItemModifierSnapshot
{
    public Guid ModifierId { get; set; }
    public string Name { get; set; } = string.Empty;
    public decimal UnitPrice { get; set; }      // modifier unit price (e.g. 1.50)
    public decimal TotalPrice { get; set; }     // unit price * line quantity
    public int TaxType { get; set; }
    public decimal TaxRate { get; set; }
    public decimal TaxAmount { get; set; }
    public decimal LineNet { get; set; }
}
```

PaymentItem’a:

```csharp
public List<PaymentItemModifierSnapshot> Modifiers { get; set; } = new();
```

Hesaplama:  
Satır toplamı = (Product.UnitPrice * Quantity) + Sum(Modifiers[*].TotalPrice).  
Her modifier için vergi ayrı hesaplanır (CartMoneyHelper ile); toplam vergi = ürün vergisi + tüm modifier vergileri.

### 4.2 ReceiptItem ve hiyerarşi

Fişte şu görünüm isteniyor:

- Döner €6.90  
- + Extra Fleisch €1.50  
- + Ketchup €0.30  

Bunun için:

- Ana satır: ürün adı, miktar, birim fiyat, toplam (sadece ürün kısmı).
- Alt satırlar: her modifier için bir `ReceiptItem`; **parent_item_id** ile ana satıra bağlı (opsiyonel FK).

Öneri: `receipt_items` tablosuna nullable `parent_item_id` (FK → receipt_items.item_id) eklemek. Böylece:

- Vergi: Her ReceiptItem kendi TaxRate/TaxAmount ile mevcut vergi gruplamasına dahil edilir (değişiklik gerekmez).
- Toplam: Tüm satırların (ana + modifier) toplamı grand total’a gider.
- Görüntüleme: UI/PDF’de parent_item_id dolu olan satırlar ana satırın altında girintili “+ …” olarak gösterilir.

ReceiptItem entity’ye eklenecek:

```csharp
[Column("parent_item_id")]
public Guid? ParentItemId { get; set; }
[ForeignKey("ParentItemId")]
public virtual ReceiptItem? ParentItem { get; set; }
public virtual ICollection<ReceiptItem> ChildItems { get; set; } = new List<ReceiptItem>();
```

ReceiptService’te `CreateReceiptFromPaymentAsync` içinde:  
Her PaymentItem için önce ana ReceiptItem (ürün) oluşturulur; sonra Modifiers listesindeki her öğe için bir ReceiptItem oluşturulup ParentItemId = ana satırın ItemId atanır.

---

## 5. API payload formatı

### 5.1 Payment API – satırda modifier’lar

Mevcut `CreatePaymentRequest` ve `PaymentItemRequest` yapısına **modifiers** alanı eklenir. Backend, modifier ID’lerini resolve edip fiyat/vergi snapshot’ını oluşturur.

Önerilen request (mevcut alanlar + modifiers):

```json
{
  "customerId": "uuid",
  "items": [
    {
      "productId": "123",
      "quantity": 1,
      "taxType": "standard",
      "modifiers": [
        { "modifierId": "uuid-ketchup" },
        { "modifierId": "uuid-extra-meat" }
      ]
    }
  ],
  "payment": { "method": "cash", "tseRequired": true },
  "tableNumber": 0,
  "cashierId": "...",
  "totalAmount": 8.70,
  "steuernummer": "ATU12345678",
  "kassenId": "...",
  "notes": null
}
```

Modifier nesnesi sadece `modifierId` içerebilir (fiyat/vergi sunucuda ProductModifier’dan alınır). İleride miktar veya override fiyat gerekirse genişletilebilir:

```csharp
public class PaymentItemModifierRequest
{
    public Guid ModifierId { get; set; }
    // optional: public int Quantity { get; set; } = 1;
}
```

PaymentItemRequest’e:

```csharp
public List<PaymentItemModifierRequest> Modifiers { get; set; } = new();
```

### 5.2 Ürün + modifier grupları (GET, Admin / POS)

Ürün detayında hangi grupların ve modifier’ların geldiğini döndürmek için örnek response:

```json
{
  "id": "product-uuid",
  "name": "Döner",
  "price": 6.90,
  "taxType": 1,
  "modifierGroups": [
    {
      "id": "group-saucen",
      "name": "Saucen",
      "minSelections": 0,
      "maxSelections": null,
      "isRequired": false,
      "sortOrder": 0,
      "modifiers": [
        { "id": "mod-ketchup", "name": "Ketchup", "price": 0.30, "taxType": 1, "sortOrder": 0 },
        { "id": "mod-mayo", "name": "Mayo", "price": 0.30, "taxType": 1, "sortOrder": 1 }
      ]
    },
    {
      "id": "group-extras",
      "name": "Extras",
      "minSelections": 0,
      "maxSelections": 3,
      "isRequired": false,
      "sortOrder": 1,
      "modifiers": [
        { "id": "mod-extra-meat", "name": "Extra Fleisch", "price": 1.50, "taxType": 1, "sortOrder": 0 },
        { "id": "mod-kaese", "name": "Käse", "price": 0.50, "taxType": 1, "sortOrder": 1 }
      ]
    }
  ]
}
```

Bunu mevcut Product API’ye yeni endpoint (örn. `GET /api/products/{id}/with-modifiers`) veya Product DTO genişlemesi ile verebilirsiniz.

---

## 6. Fiyat ve vergi hesaplama özeti

- **Satır brüt** = (Product.Price × Quantity) + Σ(Modifier.UnitPrice × Quantity)  
  (Modifier miktarı şu an her zaman satır miktarıyla aynı kabul edilebilir; ileride modifier quantity eklenirse formül genişletilir.)
- Her satır (ürün + her modifier) için `CartMoneyHelper.ComputeLine(...)` ile LineNet, LineTax, LineGross üretilir; modifier’lar da aynı vergi mantığıyla item gibi vergilendirilir.
- Toplam vergi = tüm PaymentItem (ürün + modifier) vergileri toplamı; mevcut ReceiptTaxLine gruplaması aynen kullanılır.

---

## 7. Uygulama adımları (kısa)

1. **Migration**: `product_modifier_groups`, `product_modifiers`, `product_modifier_group_assignments`, `cart_item_modifiers` tabloları; `receipt_items.parent_item_id` eklenmesi.  
2. **Entity & DbContext**: Yukarıdaki entity’ler ve Product/CartItem/ReceiptItem ilişkileri; AppDbContext’te DbSet ve Fluent API.  
3. **PaymentItem (JSON)**: `PaymentItem.Modifiers` ve `PaymentItemModifierSnapshot`; `PaymentItemRequest.Modifiers` ve `PaymentItemModifierRequest`.  
4. **PaymentService**: CreatePaymentAsync içinde her item için modifier’ları resolve edip fiyat/vergi hesaplayıp PaymentItem.Modifiers’ı doldurmak; toplam ve stok mantığını modifier’sız senaryoyla uyumlu tutmak.  
5. **ReceiptService**: CreateReceiptFromPaymentAsync’te her PaymentItem için önce ana ReceiptItem, sonra her modifier için child ReceiptItem (ParentItemId set) oluşturmak.  
6. **Cart**: Sepete ürün eklerken/güncellerken `cart_item_modifiers` yazmak; GET cart’ta modifier bilgisini dönmek.  
7. **Admin**: ProductModifierGroup ve ProductModifier CRUD; ürün düzenlemede modifier grup ataması (product_modifier_group_assignments).  
8. **POS / Frontend**: Ürün seçildiğinde atanmış grupları/modifier’ları göstermek; seçimleri cart item ile birlikte göndermek.

Bu yapı, RKSV ve mevcut fiş/vergi akışıyla uyumlu, “Extra Zutaten” için genişletilebilir bir temel sağlar.
