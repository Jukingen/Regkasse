import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { configDefaults, defineConfig } from 'vitest/config';

const rootDir = path.dirname(fileURLToPath(import.meta.url));

/**
 * Unit/component test config (Vitest).
 * Playwright E2E lives under `tests/e2e` and must stay out of this runner.
 */
export default defineConfig({
  resolve: {
    // Mirrors tsconfig paths: `@/*` -> `./src/*`
    alias: {
      '@': path.resolve(rootDir, './src'),
    },
  },
  // Prefer Oxc (Vitest 4 default). JSX runtime comes from tsconfig `jsx: "react-jsx"`.
  oxc: {
    jsx: {
      runtime: 'automatic',
    },
  },
  test: {
    globals: true,
    environment: 'jsdom',
    setupFiles: ['./src/test/vitest.setup.ts'],
    // Explicit discovery — do not rely on defaults that also match Playwright `*.spec.ts`.
    include: ['src/**/*.{test,spec}.{ts,tsx}'],
    exclude: [
      ...configDefaults.exclude,
      'tests/e2e/**',
      '**/.next/**',
      '**/node_modules/**',
      '**/coverage/**',
      '**/playwright-report/**',
      '**/test-results/**',
    ],
    testTimeout: 15_000,
    hookTimeout: 15_000,
    clearMocks: true,
    // Do not enable restoreMocks — many suites spy on shared modules and re-bind in beforeEach only.
    env: {
      // axios.ts requires a base URL when NODE_ENV is not "development" (vitest uses "test")
      NEXT_PUBLIC_API_BASE_URL: 'http://127.0.0.1:5184',
    },
    coverage: {
      provider: 'v8',
      reporter: ['text', 'text-summary', 'json-summary', 'html'],
      reportsDirectory: './coverage',
      // Still emit HTML/JSON when some suites fail (baseline measurement).
      reportOnFailure: true,
      /**
       * Coverage gate (CI-enforced): pure logic + shared helpers — target ≥80% lines.
       * Page/feature UI components stay outside this gate (RTL + Playwright separately).
       * See README “Testing strategy” and docs/TESTING.md.
       */
      include: [
        'src/features/**/utils/**/*.{ts,tsx}',
        'src/features/**/logic/**/*.{ts,tsx}',
        'src/shared/utils/**/*.{ts,tsx}',
        'src/shared/auth/canAccessPath.ts',
        'src/shared/auth/permissions.ts',
        'src/shared/auth/permissionImplication.ts',
        'src/shared/auth/routePermissions.ts',
        'src/lib/monitoring/**/*.{ts,tsx}',
        'src/lib/logging/**/*.{ts,tsx}',
        'src/lib/validations/**/*.{ts,tsx}',
        'src/lib/httpCancellation.ts',
        'src/lib/dateFormatter.ts',
        'src/hooks/usePermissions.ts',
        'src/hooks/useCanAccessPath.ts',
        'src/hooks/useDebounce.ts',
        'src/i18n/formatting.ts',
      ],
      exclude: [
        ...configDefaults.coverage.exclude,
        '**/*.{test,spec}.{ts,tsx}',
        '**/__tests__/**',
        '**/generated/**',
        '**/*.d.ts',
        '**/types.ts',
        '**/types/**',
        'src/test/**',
        'src/api/generated/**',
        'src/lib/logging/serverLogger.ts',
        /**
         * Deferred from the 80% gate (large presentation/mapper surfaces or Sentry SDK I/O).
         * See docs/TESTING.md.
         */
        'src/features/backup-dr/logic/**',
        'src/features/backup/logic/backupProgressPresentation.ts',
        'src/features/backup/logic/backupDiffPresentation.ts',
        'src/features/backup/logic/backupRunTablePresentation.ts',
        'src/features/backup/logic/backupRunDetailPresentation.ts',
        'src/features/backup/logic/backupVerificationReportPdfExport.ts',
        'src/features/backup/logic/backupDashboardStatsMapper.ts',
        'src/features/backup/logic/backupExecutionModeFormMapping.ts',
        'src/features/license/utils/licenseStatus.ts',
        'src/features/license/utils/licensePreviewDisplay.ts',
        'src/features/products/utils/productFilterUrl.ts',
        'src/features/payments/utils/paymentFilterUrl.ts',
        'src/features/payments/utils/paymentFiltersToApiParams.ts',
        'src/features/audit-logs/utils/exportAuditLogs.ts',
        'src/features/receipts/utils/rksvFinanzOnlineSubmissionUi.ts',
        'src/features/super-admin/utils/tenantHeaderSwitcher.ts',
        'src/lib/monitoring/reportToSentry.ts',
        'src/lib/monitoring/reportWebVitalToSentry.ts',
        'src/lib/monitoring/reportWebVitalBeacon.ts',
        'src/lib/monitoring/sentryInitOptions.ts',
        'src/lib/monitoring/reportApiMetric.ts',
      ],
      thresholds: {
        lines: 80,
        statements: 75,
        functions: 70,
        branches: 60,
      },
    },
  },
});
