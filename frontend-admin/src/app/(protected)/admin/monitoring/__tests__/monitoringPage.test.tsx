/**
 * @vitest-environment jsdom
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom/vitest';
import { render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { afterEach, describe, expect, it, vi } from 'vitest';

import { I18nProvider } from '@/i18n';

import AdminMonitoringPage from '../page';

vi.mock('@/api/generated/admin-restore-verification/admin-restore-verification', () => ({
  useGetApiAdminRestoreVerificationRuns: () => ({
    data: {
      items: [
        { id: 'drill-1', status: 2, requestedAt: '2026-10-20T06:00:00Z' },
      ],
    },
  }),
}));

vi.mock('@/api/generated/audit-log/audit-log', () => ({
  useGetApiAuditLog: () => ({
    data: {
      auditLogs: [{ createdAt: '2026-10-21T06:00:00Z', action: 'BACKUP_DRILL_REPORT_SENT' }],
    },
  }),
}));

function renderPage() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <App>
          <AdminMonitoringPage />
        </App>
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('admin monitoring health card', () => {
  afterEach(() => {
    vi.unstubAllGlobals();
    delete process.env.NEXT_PUBLIC_SENTRY_DSN;
    delete process.env.NEXT_PUBLIC_SENTRY_ENVIRONMENT;
  });

  it('renders the health card when the API responds', async () => {
    process.env.NEXT_PUBLIC_SENTRY_DSN = 'https://public@o0.ingest.sentry.io/1';
    process.env.NEXT_PUBLIC_SENTRY_ENVIRONMENT = 'Production';
    vi.stubGlobal(
      'fetch',
      vi.fn(async (input: RequestInfo | URL) => {
        const url = String(input);
        if (url.includes('/api/monitoring/health')) {
          return { ok: true, json: async () => ({ status: 'ok', uptimeSec: 12, env: 'test', checks: { sentryConfigured: true } }) };
        }
        return { ok: true, json: async () => ({ status: 'ok' }) };
      })
    );

    renderPage();

    expect(await screen.findByTestId('api-health-card')).toBeInTheDocument();
    await waitFor(() => {
      expect(screen.getByTestId('api-health-live')).toHaveTextContent('API live');
      expect(screen.getByTestId('api-health-ready')).toHaveTextContent('API ready');
    });
    expect(screen.getByTestId('restore-drills')).toHaveTextContent('Succeeded');
    expect(screen.getByTestId('drill-report-sent')).toHaveTextContent('2026-10-21T06:00:00Z');
    expect(screen.getByTestId('sentry-environment')).toHaveTextContent('Production');
  });

  it('asks to configure NEXT_PUBLIC_SENTRY_DSN when the env is missing', async () => {
    delete process.env.NEXT_PUBLIC_SENTRY_DSN;
    vi.stubGlobal(
      'fetch',
      vi.fn(async () => ({ ok: true, json: async () => ({ status: 'ok', uptimeSec: 1, env: 'test', checks: {} }) }))
    );

    renderPage();

    expect(await screen.findByTestId('sentry-dsn-missing')).toHaveTextContent(
      'Konfigurieren Sie NEXT_PUBLIC_SENTRY_DSN'
    );
  });
});
