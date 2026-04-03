# Regkasse AI Context Pack

Bu klasör, AI destekli geliştirme için repo-gerçeklerine dayalı kısa operasyon rehberidir.

## Monorepo özeti
- `backend/`: ASP.NET Core API (`net10.0`), EF Core + PostgreSQL, Swagger (`backend/swagger.json`).
- `frontend/`: Expo Router tabanlı mobil POS (React Native).
- `frontend-admin/`: Next.js 14 (App Router) + Ant Design + TanStack Query.
- `localization/`: i18n doğrulama/import-export scriptleri.
- `scripts/`: OpenAPI/Orval ve kritik contract doğrulama scriptleri.

## AI için çalışma önceliği
1. En yakın uygulama kodu ve testler.
2. Paket config dosyaları (`*.csproj`, `package.json`, `orval.config.ts`).
3. CI workflow’ları (`.github/workflows/*`).
4. Bu `/ai` dokümanları.

## Değişiklik güvenlik kuralları
- Küçük ve geri alınabilir değişiklik yap.
- `Cart → Payment → Receipt → DailyClosing` akışını yüksek riskli kabul et.
- TSE/RKSV/FinanzOnline alanlarında davranış değişikliği yapma; sadece isteneni uygula.
- API boundary kuralını koru: Admin için `/api/admin/*`, POS için `/api/pos/*` (istisnalar dokümante edilmelidir).

## Hızlı doğrulama komutları
- `node scripts/verify-api-client.mjs`
- `node scripts/validate-critical-openapi-paths.mjs`
- `node localization/scripts/validate-translations.mjs --app frontend-admin --strictMissing true --orphanPolicy error`
- `node localization/scripts/check-translation-boundary.mjs --app frontend-admin`
- `node localization/scripts/check-localization-usage.mjs --app frontend-admin --strictMissing true --budgetFile localization/i18n-ci-budgets.json`
