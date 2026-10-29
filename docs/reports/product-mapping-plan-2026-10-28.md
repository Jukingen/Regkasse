# Product mapping — design note (2026-10-28)

Approved smallest slice. No fiscal code, no `PaymentService`, no TSE, no `RolePermissionMatrix` change, no menu sync.

## Table

`online_order_product_mappings` (migration `20261028230039_AddOnlineOrderProductMappings` only; existing migrations were not edited).

| Column | Notes |
|--------|--------|
| `id` | uuid primary key |
| `tenant_id` | required, FK → `tenants`, restrict |
| `provider` | `varchar(32)`, stored lowercased |
| `external_product_id` | `varchar(128)`, trimmed, case-sensitive |
| `product_id` | FK → `products(id)`, restrict |
| `created_at` | required |
| `updated_at` | nullable |
| `is_active` | repository baseline, default `true`. Inactive rows are not used at intake. Upsert turns the row back on. |

Unique: `(tenant_id, provider, external_product_id)`.

The product foreign key is `products(id)`. Tenant scope is enforced in the service: the product must be an active row of the ambient tenant. A composite `(tenant_id, product_id)` foreign key is not added; `products` has no unique `(tenant_id, id)` key, and this slice does not change `products`.

Fee lines stay on the location row (`delivery_fee_product_id`, `service_fee_product_id`). They are not rows in this table.

## Endpoint

Permission `digital.manage` (Super Admin). Ambient tenant is required. No new permission.

| Method | Path |
|--------|------|
| `GET` | `/api/admin/online-order-integrations/product-mappings?provider=` |
| `PUT` | `/api/admin/online-order-integrations/product-mappings` |

`GET` lists active mappings for the ambient tenant. `provider` is optional. A missing ambient tenant, or a tenant id that is not an active tenant, returns **404**. Another tenant’s rows are not returned.

`PUT` upserts one mapping. **400** `PRODUCTS_INVALID` when the product is missing or inactive in that tenant. **400** `VALIDATION_ERROR` when provider, external id, or product id is empty. The same external id on this tenant updates `product_id`.

## FA screen

`/admin/online-order-integrations`, under Einstellungen → Digitale Dienste. Same permission as the route. The page lists mappings for the typed provider and saves a mapping with a catalog product picker (`GET /api/admin/products`, active products, first 100). No menu sync.

## Mapping point

`OnlineOrderIntegrationProcessor.TryBuildLinesAsync`, after the location row is loaded and before the catalog existence check.

Each webhook item must carry `externalProductId`. The processor loads `(tenant_id, provider, external_product_id)` from this table and sets the line’s internal `ProductId`. A missing or blank external id, or no active mapping, calls the existing `MarkFailedAsync`: status `failed`, no online order, no cart. The channel is not rejected.

Channel gross, fee products, and the total check are unchanged. The cart still copies the resolved `ProductId`. VAT stays on the catalog product’s `TaxType` when a cashier later calls `POST /api/pos/payment`.

## Platform priority (operator)

The table does not rank Foodora, Lieferando, or Wolt. A row is keyed by `provider`, not by marketplace channel.

Today the webhook provider is `deliverect`. Lieferando and Wolt are `external_channel` values on the order, not separate mapping keys. One Deliverect PLU map is shared by every channel that uses that provider. A later direct `lieferando` or `wolt` provider would be a different key and would need its own rows.

Foodora is not a provider or channel value in this slice.

The operator still chooses which marketplace to map first. That choice does not change the schema.

## Test status

PostgreSQL integration: Failed 0, Passed 57, Skipped 0, Total 57 (2026-10-29, Docker Desktop running).
