# Tenant data export

Operational CSV and Excel downloads for a mandant's catalog and sales lists. This is not a fiscal DEP export and it does not include TSE signatures, JWS payloads, or passwords.

**Last updated:** 2026-11-13

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

`format` is `csv` (UTF-8 with BOM) or `xlsx`. Any other type or format returns HTTP 400. A caller without the matching view permission receives HTTP 403.

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

`/products`, `/customers`, and `/receipts` each have an **Export** menu (CSV and Excel) that calls this endpoint. The older CSV/JSON buttons on products and customers still call the buffered catalog exports.

## Tests

```bash
dotnet test backend/KasseAPI_Final.sln --filter FullyQualifiedName~TenantDataExport
```

The tests check the CSV header, the row count for the ambient tenant, and that a second tenant appears only when the export is opened for every mandant.
