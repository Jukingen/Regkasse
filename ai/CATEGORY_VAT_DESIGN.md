# Category VAT & Product–Category Binding – Design

## 1. Entity design

### Standard: VAT stored as **percentage** (10, 20)
- DB and API: `vat_rate` / `VatRate` = **decimal percentage** (e.g. 10m, 20m).
- Calculations: use **fraction** = `VatRate / 100m` (0.10m, 0.20m).
- Single place for rounding: `CartMoneyHelper.Round()` (2 decimals, MidpointRounding.AwayFromZero).

### Category
| Property    | Type    | DB              | Notes                          |
|------------|--------|------------------|--------------------------------|
| Id         | Guid   | PK               | BaseEntity                     |
| Name       | string | required, 100    | Unique                         |
| SortOrder  | int    | default 0        |                                |
| IsActive   | bool   | default true     | BaseEntity                     |
| **VatRate**| decimal| decimal(5,2) 0–100 | VAT percentage (10, 20)   |
| Description| string?| 500              | Optional                       |
| Color, Icon| string?| optional         | UI                             |

### Product
| Property    | Type   | DB        | Notes                                   |
|------------|--------|-----------|-----------------------------------------|
| CategoryId | Guid   | FK NOT NULL | Required; references categories(id)  |
| Category   | string | 100       | Denormalized display; sync from Category.Name on save |

- Relationship: Product **has one** Category (required). Delete behavior: Restrict (no delete category with products).

---

## 2. Migration plan

1. **categories**
   - Add column `vat_rate` decimal(5,2) NOT NULL DEFAULT 20.
   - (Table already exists; only add column.)

2. **products**
   - Ensure `category_id` exists (already exists, nullable).
   - Data: for each product set `category_id` by matching `Product.Category` (string) to `Category.Name`; if no match, create category with that name and VatRate 20, or assign to a default “Uncategorized” category (create if missing).
   - Alter `category_id` to NOT NULL.

3. **Seed (optional, no “Alle”)**
   - Do **not** seed “Alle” (UI-only concept).
   - Optionally ensure categories “Speisen” (VatRate 10) and “Getränke” (VatRate 20) exist for demo; if they already exist, update their `vat_rate`.

---

## 3. API design (implemented)

| Method | Endpoint | Description |
|--------|----------|-------------|
| GET    | /api/Categories | Active categories, sorted by SortOrder then Name. Response includes VatRate. |
| GET    | /api/Categories/{id} | Single category. |
| POST   | /api/Categories | Create (admin). Body: Name, Description?, Color?, Icon?, SortOrder, **VatRate** (0–100). |
| PUT    | /api/Categories/{id} | Update (admin). Same body. |
| DELETE | /api/Categories/{id} | Soft delete (admin). |
| GET    | /api/Product?pageNumber=&pageSize=&**categoryId=** | List products; optional **categoryId** filter. |
| GET    | /api/Product/catalog | Categories (Id, Name, **VatRate**) + products with CategoryId from Category table. |
| PUT    | /api/Product/{id} | Update product; body may include **categoryId**. Backend syncs Product.Category from Category.Name. |

### DTOs (Categories)
- **Category** (response): Id, Name, Description, Color, Icon, SortOrder, IsActive, **VatRate**, CreatedAt, UpdatedAt.
- **CreateCategoryRequest**: Name, Description?, Color?, Icon?, SortOrder, **VatRate** (required, 0–100).
- **UpdateCategoryRequest**: Same as create.

---

## 4. Tax calculation (receipt / POS)

- **Source of VAT:** Category’s **VatRate** (percentage). Product no longer drives tax; category does.
- **Flow:**
  1. Load product with category: `Product` + `Category.VatRate`.
  2. Line: `CartMoneyHelper.ComputeLine(unitGross, quantity, category.VatRate)` (new overload with percentage).
  3. Receipt/receipt items: store TaxRate (and derived TaxType for RKSV). Rounding only in `CartMoneyHelper.Round()` (2 decimals).
- **Money precision:** decimal; all final amounts 2 decimals; single rounding point: `CartMoneyHelper`.

---

## 5. CartMoneyHelper

- Add overload: `ComputeLine(decimal unitGross, int quantity, decimal vatRatePercent)`.
  - `vatRatePercent` = 10, 20 (percentage).
  - Internal rate = `vatRatePercent / 100m`.
  - Return `LineAmounts` with TaxRate = fraction (0.10, 0.20) and TaxType = derived from rate for RKSV (20→1, 10→2, 13→3, 0→4).
- Keep existing `ComputeLine(unitGross, quantity, taxType)` for backward compatibility (e.g. modifiers if they still use TaxType).
