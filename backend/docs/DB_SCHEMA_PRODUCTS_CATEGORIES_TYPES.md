# products / categories kolon tipleri – teşhis, migration ve doğrulama

## 0) InvalidCastException: "Reading as System.Guid is not supported for DataTypeName 'text'"

**Patlayan kolon (kesin):** `products.category_id`

- Entity'de `Product.CategoryId` → `Guid`; EF/Npgsql kolonu uuid bekliyor.
- InitialCreate'te `category_id` **text** olarak oluşturuldu; migration (`FixProductsCategoryIdUuidAndFk`) uygulanmamışsa DB'de hâlâ text.
- **Çözüm:** Aşağıdaki migration'ı uygulayın: `dotnet ef database update` (veya "Manuel fix" SQL'i çalıştırın).

**Product tablosunda Guid map’lenen property’ler:** yalnızca `Id` (uuid), `CategoryId` (uuid). `CreatedBy` / `UpdatedBy` entity'de string; DB'de varchar(450).

---

## 1) DB şema tiplerinin tespiti

PostgreSQL’de aşağıdaki sorgu ile ilgili kolonların gerçek tipini kontrol edebilirsiniz:

```sql
SELECT table_name, column_name, data_type, udt_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND (
    (table_name = 'products' AND column_name IN ('id', 'category_id', 'category'))
    OR (table_name = 'categories' AND column_name = 'id')
  )
ORDER BY table_name, column_name;
```

### Beklenen sonuç (migration sonrası)

| table_name  | column_name  | data_type | udt_name |
|-------------|--------------|-----------|----------|
| categories  | id           | uuid      | uuid     |
| products    | category     | character varying | varchar  |
| products    | category_id  | uuid      | uuid     |
| products    | id           | uuid      | uuid     |

### Tarihçe (neden hata alındığı)

- **InitialCreate**: `products.CategoryId` → `text` olarak oluşturuldu.
- **FixRelationshipMappings**: `categories` tablosu `id uuid` ile oluşturuldu.
- **AddCategoryVatAndRequiredCategoryId**: Sadece `category_id` nullable → NOT NULL yapıldı; **tip text’ten uuid’e çevrilmedi**.
- EF tarafında `Product.CategoryId` ve `Category.Id` her zaman `Guid` (uuid) kullanıldığı için:
  - Okurken: "Reading as System.Guid is not supported for fields having DataTypeName text" (InvalidCastException)
  - JOIN’de: "operator does not exist: text = uuid" (42883)

---

## 2) Migration: FixProductsCategoryIdUuidAndFk (production-safe)

- **Uncategorized** deterministik: id = `00000000-0000-0000-0000-000000000001` (yoksa INSERT).
- Aynı isimle birden fazla "Uncategorized" varsa migration **fail-fast** (RAISE EXCEPTION); önce cleanup gerekir.
- Geçersiz `category_id` (NULL, boş, UUID regex’e uymayan) tek bir `UPDATE` ile Uncategorized’a set edilir; sonra `ALTER COLUMN ... TYPE uuid`.
- FK ve index: yoksa eklenir, varsa dokunulmaz.

---

## 3) Migration sonrası doğrulama (checklist)

Aşağıdaki SQL’leri migration sonrası çalıştırarak kontrol edin.

### 3.1) products.category_id tipi uuid mi?

```sql
SELECT column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'products' AND column_name = 'category_id';
-- Beklenen: data_type = 'uuid'
```

### 3.2) Geçersiz category_id kalan var mı? (olmamalı)

```sql
SELECT id, name, category_id
FROM products
WHERE category_id IS NULL
   OR category_id::text !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$';
-- Beklenen: 0 satır
```

### 3.3) FK çalışıyor mu?

```sql
SELECT conname, conrelid::regclass, confrelid::regclass
FROM pg_constraint
WHERE conrelid = 'public.products'::regclass AND contype = 'f';
-- Beklenen: FK_products_categories_category_id, products -> categories
```

### 3.4) JOIN sorgusu çalışıyor mu?

```sql
SELECT p.id, p.name, p.category_id, c."Name" AS category_name
FROM products p
INNER JOIN categories c ON p.category_id = c.id
LIMIT 5;
-- Hata vermemeli, satır dönmeli (ürün varsa)
```

---

## 4) Cleanup: Duplicate "Uncategorized" (migration fail etmeden önce)

Migration, aynı isimle birden fazla "Uncategorized" bulunursa **RAISE EXCEPTION** ile durur. Önce tek kayda indirmeniz gerekir.

### Raporlama: Aynı isimle kaç kategori var?

```sql
SELECT LOWER(TRIM("Name")) AS name_normalized, COUNT(*) AS cnt, array_agg(id) AS ids
FROM categories
GROUP BY LOWER(TRIM("Name"))
HAVING COUNT(*) > 1;
```

### Cleanup: "Uncategorized" duplicate’ları tek kayda indir

En eski kaydı tutup diğerlerini silmek (ürünleri önce o id’ye toplayın):

```sql
-- 1) Duplicate Uncategorized id'lerini gör
WITH dups AS (
  SELECT id, "Name", created_at,
         ROW_NUMBER() OVER (PARTITION BY LOWER(TRIM("Name")) ORDER BY created_at) AS rn
  FROM categories
  WHERE LOWER(TRIM("Name")) = 'uncategorized'
)
SELECT * FROM dups;

-- 2) products.category_id'yi en eski Uncategorized id'ye çek (category_id hâlâ text ise)
-- UPDATE products SET category_id = (SELECT id::text FROM dups WHERE rn = 1) WHERE category_id IN (SELECT id::text FROM dups WHERE rn > 1);

-- 3) Fazla Uncategorized kayıtlarını sil
WITH dups AS (
  SELECT id, ROW_NUMBER() OVER (PARTITION BY LOWER(TRIM("Name")) ORDER BY created_at) AS rn
  FROM categories
  WHERE LOWER(TRIM("Name")) = 'uncategorized'
)
DELETE FROM categories WHERE id IN (SELECT id FROM dups WHERE rn > 1);
```

Not: `category_id` zaten uuid ise adım 2’de `id::text` yerine sadece `id` kullanın ve `WHERE category_id IN (SELECT id FROM dups WHERE rn > 1)` yeterli.

---

## 5) EF Core ilişki (referans)

- `Product.CategoryId` (Guid, required) → `categories.id` (uuid)
- `Product.CategoryNavigation` → `Category`; `Category.Products` → `ICollection<Product>`
- Fluent: `HasOne(p => p.CategoryNavigation).WithMany(c => c.Products).HasForeignKey(p => p.CategoryId).OnDelete(Restrict).IsRequired()`

---

## 6) Diagnostic: products’ta uuid olması gerekirken text/varchar kalan kolonlar

Aşağıdaki SQL ile **products** tablosunda EF’in Guid beklediği halde text/varchar kalan kolonları listeleyin:

```sql
-- products: *_id ve entity'de Guid olan kolonlar; DB'de text/varchar mı?
SELECT column_name, data_type, udt_name
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name = 'products'
  AND data_type IN ('text', 'character varying')
  AND (column_name LIKE '%_id' OR column_name IN ('id', 'created_by', 'updated_by'))
ORDER BY column_name;
```

- **Beklenen (sorunlu):** `category_id` → `text` (migration uygulanmamışsa).
- **Beklenen (doğru):** `created_by`, `updated_by` → character varying (entity’de string).

---

## 7) Manuel fix (migration çalıştırılamıyorsa)

Migration uygulanamıyorsa (örn. yetki/ortam kısıtı), aşağıdaki SQL’i **tek seferde** çalıştırabilirsiniz. Önce `products.category_id` tipinin hâlâ `text` olduğunu yukarıdaki diagnostic ile doğrulayın.

### 7.1) Geçersiz category_id değerlerini Uncategorized’a çek (cast öncesi)

```sql
DO $$
DECLARE
  uncat_id uuid;
BEGIN
  SELECT id INTO uncat_id FROM categories
  WHERE LOWER(TRIM("Name")) = 'uncategorized' AND is_active = true
  LIMIT 1;
  IF uncat_id IS NULL THEN
    INSERT INTO categories (id, "Name", "Description", "Color", "Icon", "SortOrder", vat_rate, created_at, updated_at, is_active, created_by, updated_by)
    VALUES ('00000000-0000-0000-0000-000000000001'::uuid, 'Uncategorized', NULL, NULL, NULL, 999, 20, NOW(), NOW(), true, 'migration', 'migration');
    uncat_id := '00000000-0000-0000-0000-000000000001'::uuid;
  END IF;
  UPDATE products
  SET category_id = uncat_id::text
  WHERE category_id IS NULL
     OR TRIM(category_id) = ''
     OR category_id !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$';
END $$;
```

### 7.2) Kolonu uuid yap + index + FK

```sql
ALTER TABLE products
  ALTER COLUMN category_id TYPE uuid USING category_id::uuid;

CREATE INDEX IF NOT EXISTS "IX_products_category_id" ON products (category_id);

DO $$
BEGIN
  IF NOT EXISTS (
    SELECT 1 FROM information_schema.table_constraints
    WHERE table_schema = 'public' AND table_name = 'products' AND constraint_name = 'FK_products_categories_category_id'
  ) THEN
    ALTER TABLE products
    ADD CONSTRAINT FK_products_categories_category_id
    FOREIGN KEY (category_id) REFERENCES categories(id) ON DELETE RESTRICT;
  END IF;
END $$;
```

### 7.3) Cast edilemeyen satırları raporlama (UPDATE öncesi)

Geçersiz `category_id` (cast’e takılacak) satırları görmek için:

```sql
SELECT id, name, category_id
FROM products
WHERE category_id IS NULL
   OR TRIM(category_id) = ''
   OR category_id !~ '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$';
```

Bu satırlar 7.1’deki UPDATE ile Uncategorized’a çekilir; sonra 7.2 güvenle çalışır.

---

## 8) Endpoint kontrol listesi

- `GET /api/Product/all` → 200
- `GET /api/Product/catalog` → 200
