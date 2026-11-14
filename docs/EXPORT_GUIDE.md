# Tenant data export

Operational CSV and Excel downloads for a mandant's catalog, sales, online orders, audit rows, and stock. This is not a fiscal DEP export. JWS payloads, signature bytes, and passwords are omitted. The `tse_signatures` type lists receipt number, signature time, and TSE device id only.

**Last updated:** 2026-11-14

## Endpoint

```http
GET /api/admin/export/{type}?format=csv|xlsx
```

| `type` | Permission | Rows |
|---|---|---|
| `products` | `product.view` | Id, tenant, name, price, tax rate, barcode, category, stock, active |
| `customers` | `customer.view` | Id, tenant, name, number, email, phone, active. System customers (walk-in) are included only for Super Admin |
| `receipts` | `sale.view` | Receipt id, tenant, number, issued time, register, payment, subtotal, tax, grand total. No signature or QR payload |
| `payments` | `payment.view` | Id, tenant (from the cash register), register, customer name, totals, method, receipt number, created time, storno flag |
| `users` | `user.view` | Active membership: user id, tenant, email, username, name, role, active. No password hash or security stamp |
| `orders` | `order.view` | Online order id, status, total, customer name, created time, tenant. Not a fiscal receipt |
| `audit_logs` | `audit.view` | Action, entity type, user id, timestamp, tenant. Old/new values and any stored signature text are omitted |
| `inventory` | `inventory.view` | Product name, stock quantity, reorder level (`MinStockLevel`), tenant |
| `tse_signatures` | `system.critical` and the `SuperAdmin` role | Receipt number (`InvoiceNumber`), signature time, TSE device id, tenant. No signature, JWS, or certificate material |

`format` is `csv` (UTF-8 with BOM) or `xlsx`. Any other type or format returns HTTP 400. A caller without the matching permission receives HTTP 403. `tse_signatures` also returns HTTP 403 when the caller is not a Super Admin.

## Who sees which tenants

| Caller | Scope |
|---|---|
| Super Admin (`SuperAdmin`) | Every tenant. The query uses `IgnoreQueryFilters()` for that request only |
| Tenant Admin and every other permitted role | The ambient tenant only. EF global filters stay on. With no ambient tenant the file contains the header and no rows |

Payments have no `tenant_id` of their own. The export joins `payment_details` to `cash_registers` and keeps a payment only when that register is in scope.

## Streaming

The handler writes the response body as rows are read (`IAsyncEnumerable` / `AsAsyncEnumerable`). CSV is written with CsvHelper onto the response stream. Excel is a minimal xlsx package whose worksheet XML is written row by row. The API does not load the full table into a byte array first.

The older catalog downloads stay in place:

| Existing route | Formats |
|---|---|
| `GET /api/admin/products/export` | `csv`, `json` (buffered, 50,000 row cap) |
| `GET /api/Customer/export` | `csv`, `json` (buffered) |

## Admin UI

`/products`, `/customers`, and `/receipts` each have an **Export** menu (CSV and Excel) that calls this endpoint. The same menu is on `/orders/online` (`orders`), `/audit-logs/activity` (`audit_logs`), `/inventory` (`inventory`), and `/admin/tse` (`tse_signatures`, Super Admin). The older CSV/JSON buttons on products and customers still call the buffered catalog exports.

## Tests

```bash
dotnet test backend/KasseAPI_Final.sln --filter FullyQualifiedName~TenantDataExport
```

The tests check the CSV header, the row count for the ambient tenant, and that a second tenant appears only when the export is opened for every mandant.
