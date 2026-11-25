# 02 — Backend (`backend/`)

> Part 2 of the Regkasse AI briefing. Index: [`../AI_PROJECT_BRIEFING.md`](../AI_PROJECT_BRIEFING.md).
> Verified at commit `e2be4d5b` (2026-11-24). Counts are file or line counts taken from the repository and are approximate.

## 1. Overview

ASP.NET Core 10 Web API (`KasseAPI_Final.csproj`, root namespace `KasseAPI_Final`). It is one layered monolith:

- `Program.cs` only boots. The host is built in `ApplicationHost.cs` (about 2,400 lines: DI registration, middleware pipeline, health checks, Swagger). There is no `Startup.cs`.
- Startup helpers: `StartupBootstrapRunner.cs`, `StartupMigrationGuard.cs`, `SwaggerHostFactory.cs`, `OpenApiExportConfiguration.cs` / `OpenApiExportMode.cs` (used to export `swagger.json` without running the full app).
- Config: `appsettings.json`, `appsettings.Development.json`, `appsettings.Production.json`, `appsettings.Staging.json` plus `*.example.json` templates. Real secrets go in user-secrets or environment variables. Full map: `backend/CONFIGURATION.md`.
- Container: multi-stage `Dockerfile`, port 8080.

Run locally:

```powershell
cd backend
dotnet user-secrets set "ConnectionStrings:DefaultConnection" "Host=localhost;Port=5432;Database=kasse_db;Username=postgres;Password=..."
dotnet user-secrets set "JwtSettings:SecretKey" "<random, 32+ chars>"
dotnet run --project KasseAPI_Final.csproj            # http://localhost:5184
# or from the repo root: npm run dev:backend
```

Swagger UI is Development only (`/swagger`). Health: `/health`, `/health/live`, `/health/ready`, `/api/health`, plus `/health/finanzonline`, `/health/fiskaly`, `/health/tse/mode`, `/health/backup/mode`, `/health/migrations`, `/health/elmah`.

## 2. Folder map (what lives where)

| Folder | Content (approximate size) |
|---|---|
| `Controllers/` | About 300 controller files (183 `Admin*`, 25 `Pos*`, the rest shared/public/dev; includes partial classes). Naming tells the API surface (see §3) |
| `Services/` | About 420 top-level service files plus about 70 feature subfolders (see §4) |
| `Models/` | About 390 files: entities, enums, `Countries/`, `Backup/`, `RestoreVerification/`, `Reports/`, `Inventory/`, `Export/` |
| `DTOs/` | Request/response contracts |
| `Data/` | `AppDbContext.cs` (about 220 `DbSet`s), seeds (`SeedData`, `RoleSeedData`, `UserSeedData`, `DemoTenantAdminSeed`, `demo-products.json`), interceptors, `DesignTimeDbContextFactory` |
| `Migrations/` | EF migrations (see §6) |
| `Middleware/` | 23 middleware classes (see §5) |
| `Authorization/` | `Roles`, `RolePermissionMatrix`, `AppPermissions`, `PermissionCatalog`, `HasPermissionAttribute`, handlers, `IndustryPermissionTemplates` |
| `Tenancy/` | `ICurrentTenantAccessor`, `CurrentTenantService`, `SubdomainTenantProvider`, `TenantHostNames`, login tenant resolution |
| `Tse/` | `ITseProvider`, `FakeTseProvider`, `RealTseProvider`, `SignaturePipeline`, `BelegdatenPayload*`, `RksvMachineCodeBuilder`, Fiskaly HTTP client and credential handling |
| `Fiscal/` | `FiscalSignatureRouter` (routes signing by country), `FiscalSigningRequest`, `FiscalTseSigning` |
| `Rksv/` | RKSV guards, QR payload builder/parser, Monatsbeleg gate/policy |
| `Hubs/` | SignalR: `KitchenHub`, `FiskalyOperationStatusHub`, `DemoImportProgressHub` |
| `Options/`, `Configuration/` | Strongly typed options |
| `Filters/`, `ModelBinding/`, `Validators/`, `Converters/`, `Helpers/`, `Constants/`, `Infrastructure/`, `Security/`, `Logging/`, `HealthChecks/`, `Localization/`, `Resources/` | Cross-cutting infrastructure |
| `KasseAPI_Final.Tests/` | xUnit test project (see §9) |
| `swagger.json` | OpenAPI document that drives the FA Orval client (about 1,245 paths, OpenAPI 3.0.4) |

Treat `bin/`, `obj/`, `publish/`, `_build_out*`, `tmp-*`, `.test-out*`, `report-pdfs/`, `coverage-payment/`, and similar folders as build artifacts, not source.

## 3. Controllers and route surfaces

Rule of thumb from file names and `[Route]` attributes:

| Surface | Controller naming / route | Used by |
|---|---|---|
| Admin | `Admin*Controller` → `/api/admin/...` (about 183 files) | FA |
| POS | `Pos*Controller` → `/api/pos/...`. `CartController` → `/api/pos/cart`. `PaymentController` → `/api/pos/payment`. `ProductController` → `/api/pos` (catalog under `/api/pos/...`) | POS |
| Shared / other | `AuthController` (`/api/Auth`), `ReceiptsController` (`/api/Receipts`), `RksvController` (`/api/rksv`), `RksvSpecialReceiptsController` (`/api/rksv/special-receipts`), `RksvMonatsbelegController`, `RksvJahresbelegController`, `OfflineTransactionsController` (`/api/offline-transactions`), `LicenseController` (`/api/license`), `HealthController` (`/api/health`), `VouchersController` (`/api/vouchers`) | POS and/or FA |
| `api/[controller]` style | `CashRegisterController`, `TagesabschlussController`, `TableController`, `InvoiceController`, `FinanzOnlineController` | POS and/or FA (check the actual clients before assuming) |
| Public (anonymous) | `PublicTenantsController` (`/api/public/tenants`), `PublicSitesController` (`/api/public/sites`), `PublicOnlineOrdersController` (`/api/public/online-orders`), `PublicCustomerController` (`/api/public/customer`), `Public/PublicDemoRequestsController` | Sites, customer app, marketing |
| Webhooks | `StripeWebhookController`, `PaymentWebhookController`, `OnlineOrderIntegrationWebhookController` | External providers |
| Dev / test | `Dev*Controller`, `*DevTestController`, `TestController`, `LicenseTestController`, `FiskalySignTestController` | Development only. Must not be reachable in production |

`CartController` endpoints (examples): `GET current`, `GET {cartId}`, `POST add-item`, `POST {cartId}/items`, `PUT items/{itemId}`, `DELETE {cartId}/items/{itemId}`, `POST split-items`, `POST merge-tables`, `POST {cartId}/reset-after-payment`, `POST {cartId}/complete`, `GET table-orders-recovery`.

`PaymentController` endpoints (examples): `POST /api/pos/payment` (create), `GET methods`, `GET history`, `GET {id}`, `POST storno`, `POST {id}/refund`, `POST {id}/cancel`, `GET {id}/receipt`, `GET {id}/qr.png`, `GET {id}/qr.svg`, `POST {id}/tse-signature`, `POST verify-signature`.

`TagesabschlussController`: `POST daily`, `POST monthly`, `POST yearly`, `POST {id}/submit-fiskaly`, `POST {id}/submit-finanzonline`, `GET closing/{closingId}/report.pdf`, `GET history`, `GET can-close/{cashRegisterId}`, auto-close settings.

`AuthController`: `POST login`, `POST verify-2fa`, `POST refresh`, `POST refresh-session`, `POST logout`, `POST logout-all`, `POST revoke`, `GET me`, `POST forgot-username`, `POST forgot-password`, `POST register`.

Removed legacy aliases `/api/Payment`, `/api/Cart`, `/api/Product` return 404 on purpose (removed 2026-08-13). Do not reintroduce them.

### Admin controller families (what the Admin controllers cover)

Tenants and users (`AdminTenants*`, `AdminUsers*`, `AdminSession`, `AdminStaff`), licensing and billing (`AdminLicense*`, `AdminBilling`, `AdminSubscriptionInvoices`, `AdminTenantBillingInvoices`, `AdminTrials`, `AdminGracePeriods`), permissions (`AdminPermission*`), catalog (`AdminProducts*`, `AdminTaxGroups`, `AdminPricingRules`, `AdminMwstRates`), RKSV (`AdminRksv*`, `AdminJahresbelegClosing`, `AdminMonatsbelegClosing`), TSE operations (27 `AdminTse*` controllers: health, failover, auto-healing, scaling, analytics, incidents, SLA, DR, ...), Fiskaly (`AdminFiskaly*`), backup/restore (`AdminBackup*`, `AdminRestore*`), reports and PDFs (`AdminReports`, `AdminReportPdf`, `AdminEnhancedReports`, `AdminTaxReports`), online orders and marketplaces (`AdminOnlineOrders`, `AdminWoltIntegration`, `AdminFoodoraCredentials`, `AdminLieferandoCredentials`, `AdminOnlineOrderIntegration*`), digital services (`AdminDigital`, `AdminWebsite`), vertical features (`AdminLodging`, `AdminTaxiTrips`, `AdminTicket*`, `AdminAppointments`, `AdminProductImei*`, `AdminKitchen`), support (`AdminSupportTickets`, `AdminSupportInbox`), data rights and GDPR (`AdminDataManagement`, `AdminTenantDataManagement`, `AdminTenantDataExport`), monitoring/ops (`AdminElmahErrors`, `AdminCache`, `AdminFeatureFlags`, `AdminDeployments`, `AdminDatabaseMigrations`), Peppol/e-invoicing (`AdminPeppol*`), country layer (`AdminCountries`, `AdminChQr*`, `AdminKassenSicherheit`).

## 4. Services (business logic)

Key top-level services (file names are the entry points):

- **Payment pipeline:** `PaymentService` (+ partials `PaymentService.CreatePaymentCore*.cs`) delegating to focused services: `PaymentSaleGateService`, `PaymentSettlementService`, `PaymentDocumentPersistenceService`, `PaymentPostCommitService`, `PaymentCommitFailureHandler` / `PaymentCommitFailureMapper`, `PaymentStornoRefundGate`, `PaymentStornoService`, `PaymentRefundService`, `PaymentVoucherLedgerService`, `PaymentBenefitService`, `PaymentOfflineIntentService`, `PaymentFinanzOnlineSubmissionService`, `PaymentQueryService`, `PaymentReceiptReadService`. See part 03.
- **Fiscal:** `TseService`, `ReceiptSequenceService`, `ReceiptService`, `InvoiceService`, `RksvSpecialReceiptService`, `RksvMonatsbelegService`, `RksvStartbelegPolicy`, `RksvReminderService`, `RksvDepExportService`, `RksvSignatureVerifyService`, `RksvEvidenceBundleService`, `NtpTimeSyncService`, `TseHealthCheckService`, `TseFailoverBackgroundService`.
- **Cart and tables:** `CartLifecycleService`, `CartMoneyHelper`, `PosCartTableOpsService`, `PosDailyClosingService`.
- **PDF generation (QuestPDF):** `ReceiptPdfService`, `InvoicePdfService`, `Receipt/SpecialReceiptPdfService`, `Reports/*PdfGenerator` (Tagesbericht, Monatsbeleg, Jahresbericht, DailyClosing, PeriodSales, PermissionAudit, AdminOperational), `FiscalExportPdfGenerator`, `RksvComplianceReportPdfGenerator`, `FiskalyStatisticsPdfGenerator`, `FiskalyErrorPdfGenerator`, `License/LicenseCertificatePdfGenerator`, `Billing/InvoicePdfGenerator`, `Countries/QrRechnung/QrRechnungPdf`, `Backup/RestoreDrillPdfRenderer`. Report PDFs are archived through `ReportPdfStorageService` (`report_pdfs` table).

Largest feature subfolders under `Services/` (file counts): `Backup` 193, `Tse` 94, `RestoreVerification` 75, `Countries` 73, `FinanzOnlineIntegration` 46, `Reports` 45, `Order` 40 (online orders and marketplace integrations, not POS orders), `AdminTenants` 30, `License` 27, `Billing` 26, `Email` 26, `Activity` 21, `Hosted` 19, `Tenancy` 17, `Deployment` 14, `Rksv` 14, `Extensions` 13, `TenantLimits` 13, `OperationalRuns` 12, `Website` 11, `DataExport` 10, `Vouchers` 9, `Offline` 9, `VerticalProfiles` 8, `Trial` 8, `Caching` 8, `Metrics` 8.

### Hosted / background services (about 60 classes)

Implemented via `BackgroundService` / `IHostedService`. Groups (names are class names; registration lives in `ApplicationHost.cs` and extension methods):

- Fiscal and compliance: `NtpTimeSyncService`, `TseHealthCheckService`, `TseFailoverBackgroundService`, `MonatsbelegSchedulerHostedService`, `JahresbelegReminderHostedService`, `AutoTagesabschlussHostedService`, `TagesabschlussReminderService`, `DepExport*HostedService`, `RksvDataCleanupHostedService`.
- FinanzOnline: `FinanzOnlineRetryHostedService`, `FinanzOnlineOutbox` (worker), `FinanzOnlineOutboxMonitorHostedService`.
- Offline: `OfflineReplayHostedService`, `OfflineAlertService`, `OfflineOrderCleanupHostedService`, `OrphanIntentCleanupService`, `PayloadHashRepairHostedService`, `PayloadHashGuardStartupCheck`.
- Backup/DR: `BackupSchedulerService`, `BackupOrchestratorHostedService`, `BackupReVerificationHostedService`, `BackupDrillReportHostedService`, `BackupRpoOverdueAlertService`, `StorageAlertService`, `AutomaticCleanupService`, `WalArchiveRetentionHostedService`, `IncrementalBackupScheduledEnqueueService`, `StaleRunReaperHostedService`.
- SaaS lifecycle: `LicenseComplianceHostedService`, `LicenseReminderHostedService`, `LicenseScheduledReportsHostedService`, `BillingReminderHostedService`, `SubscriptionInvoiceHostedService`, `GracePeriodExecutorHostedService`, `TenantLifecycleArchiveHostedService`, Trial reminder/cleanup, `AutoPurgeService`, `DataRightsExportProcessorService`.
- Operations: `ActivityMonitoringHostedService`, `BusinessMetricsRefreshHostedService`, `ElmahRetentionHostedService`, `ShiftAutoCloseHostedService`, `CartLifecycleService`, `OnlineOrderIntegrationRetryHostedService`, `PeppolAckPollingService`, `CanaryTenantMonitorHostedService`, `ExportEmailSchedulerHostedService`, `MaintenanceNotificationScheduler`.

Hosted services have **no HTTP request** and no ambient tenant. They must use `IServiceScopeFactory.CreateScope()` before touching EF (see part 04).

## 5. Middleware pipeline (order in `ApplicationHost.cs`, lines about 2147–2277)

Approximate order (verify in code before relying on the exact sequence):

1. `SentryExceptionCaptureMiddleware`
2. (Production) forwarded headers, HTTPS redirection; response compression
3. `SecurityHeadersMiddleware`
4. `MetricsMiddleware`, `PrometheusMetricsAccessMiddleware`
5. `LanguageMiddleware`, `CorrelationIdMiddleware`
6. `RateLimitingMiddleware`
7. `CsrfMiddleware`
8. **`TenantResolutionMiddleware`** (host/header/query resolution; production ignores header/query)
9. CORS, static files
10. `TokenValidationMiddleware`, then `UseAuthentication()`
11. **`TenantContextMiddleware`** (JWT `tenant_id` binds the ambient tenant)
12. `RequestLoggingScopeMiddleware`
13. **`TenantValidationMiddleware`** (404 when a tenant is required but missing; SuperAdmin platform-prefix exemptions)
14. `SessionActivityMiddleware`
15. `LicenseLockdownMiddleware`, `TenantOperationalGateMiddleware`, `LicenseMiddleware`, `MaintenanceMiddleware`
16. `UseAuthorization()`
17. `MustChangePasswordMiddleware`
18. `PaymentSecurityMiddleware`
19. `CriticalActionMiddleware`
20. `OperationLimitMiddleware`

Do not reorder tenant middleware (`TenantResolution` → auth → `TenantContext`) without a dedicated task. This is listed in `ai/07_DO_NOT_TOUCH.md`.

## 6. Database

- PostgreSQL via Npgsql. `AppDbContext` has about 220 `DbSet`s. About 84 model files implement `ITenantEntity` (non-null `tenant_id uuid`, indexed, with an EF global query filter).
- Not tenant-scoped (examples): `tenants`, ASP.NET Identity tables, `auth_sessions`, `refresh_tokens`, `activated_licenses` (deployment-local), `finanz_online_outbox_messages` (string `tenant_id`).
- Two `AppDbContext` constructors: a runtime one marked `[ActivatorUtilitiesConstructor]` that takes `ICurrentTenantAccessor`, and a design-time one used by `dotnet ef`. Do not add another constructor that DI could resolve.
- **Migrations:** the chain was rebased on 2026-10-28. `Migrations/_archive/2026-10-28/` holds the older history. The active chain starts at `20261028121002_InitialCreate` and has 14 follow-ups to date (online-order integrations and OAuth tokens, Wolt/Foodora credentials, cross-tenant invoice number disambiguation, per-tenant Fiskaly credentials, payment storno unique index, cash tender columns, product recipes and suppliers, audit-log search vector). Rules: additive only, never edit a committed migration, data backfill in a separate step.
- Column baseline: `id uuid`, `created_at timestamptz NOT NULL DEFAULT now()`, `updated_at`, `is_active`, `tenant_id` on tenant-scoped tables, indexes on all foreign keys.
- Country lives in exactly one place: `company_settings.country` (`NOT NULL DEFAULT 'AT'`). Do not add a `CountryCode` column. A test (`CountryLayerBypassGateTests`) enforces this.
- Add a migration: `dotnet ef migrations add <Name> --project backend`.

## 7. Caching

- Use `ICacheService` (`backend/Services/Caching/`), never Redis or `IMemoryCache` directly in domain code. Redis in Staging/Production, in-memory fallback in Development or when Redis fails.
- Keys come from `CacheKeys.*` (include tenant or user id). TTLs come from `CacheSettings`: license 5 min, products 15 min, permissions 30 min, tenant settings 60 min, tenant limits 5 min, TSE health 30 s.
- Invalidate after writes with `CacheInvalidationHelper`. Never cache secrets, voucher codes, payment details, or live stock/cart data.
- Super Admin emergency clear: `POST /api/admin/cache/clear`.

## 8. Cross-cutting patterns to follow

- **DI scoping:** `AppDbContext` and `ICurrentTenantAccessor` are scoped. Singletons (hosted services, `LicenseService`) create a scope per operation.
- **Validation:** all input validation happens on the backend. Client validation is UX only.
- **Errors:** API error messages in English; user-facing messages localized by the clients. Use domain-specific error codes for guardrail failures (for example `RksvGuardErrorCodes`).
- **Limits:** tenant caps return HTTP 409 with `LimitErrorDto` (`LIMIT_EXCEEDED`).
- **Audit:** sensitive operations write audit events (`AuditEventType.*`). Never put passwords, voucher codes, or TSE secrets into logs or audit metadata.
- **Logging and observability:** structured logging with a correlation id (`CorrelationIdMiddleware`, `RequestLoggingScopeMiddleware`); Sentry and ElmahCore (PostgreSQL) for errors; Prometheus metrics. Serilog is not used (no reference in the project).
- **Security:** CSRF double-submit (enabled in Production), security headers, rate limiting, 2FA (TOTP) for SuperAdmin in Production.

## 9. Tests

- Test project `backend/KasseAPI_Final.Tests/` (xUnit, FluentAssertions, Moq, `Microsoft.AspNetCore.Mvc.Testing`, Testcontainers for PostgreSQL). About 900 `*Tests.cs` files. Subfolders: `Billing`, `Contract`, `Countries`, `CountryBaseline`, `Extensions`, `Fixtures`, `Integration`, `OnlinePayment`, `Security`, `Services`, `Support`.
- Test categories: PostgreSQL integration tests are tagged `Category=PostgreSql` and run in a separate CI workflow. Unit workflow excludes that category.
- Useful commands:

```bash
dotnet build backend/KasseAPI_Final.csproj -c Debug
dotnet test backend/KasseAPI_Final.sln
dotnet test backend/KasseAPI_Final.Tests/KasseAPI_Final.Tests.csproj --filter "FullyQualifiedName~TenantIsolation"
dotnet test backend --filter "RksvDepExportServiceTests"
```

- Notable guard tests: `TenantIsolationTests`, `CountryLayerBypassGateTests`, `WorkingHoursPosFaNonGatingContractTests`, golden-master tests around `PaymentService`, RKSV special receipt service tests, voucher redemption tests, role/permission matrix tests.

## 10. Backend work checklist (for any task)

1. Which surface (Admin, POS, public, shared)? Put the endpoint in the right controller family and route prefix.
2. Tenant-scoped data? Check `ITenantEntity`, the 404 rule, and whether the endpoint needs an ambient tenant.
3. Permissions: use `[HasPermission(...)]` with an existing key from `AppPermissions` / `RolePermissionMatrix`. Do not invent new roles.
4. Schema change: additive migration only. No edits to committed migrations.
5. New or changed endpoints/DTOs: regenerate `swagger.json`, then the FA client, then run `verify-api-client`.
6. Tests: add or update unit tests; add a PostgreSQL integration test when query behavior matters.
7. High-risk area? Read `ai/07_DO_NOT_TOUCH.md`, plan first, and get explicit scope.
