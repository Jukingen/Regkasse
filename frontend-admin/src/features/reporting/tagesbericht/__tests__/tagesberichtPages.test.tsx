import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeAll, beforeEach, describe, expect, it, vi } from 'vitest';

import TagesberichtListPage from '@/app/(protected)/reporting/tagesbericht/page';
import { TagesberichtSummaryCards } from '@/features/reporting/tagesbericht/TagesberichtSummaryCards';
import { I18nProvider } from '@/i18n';

const mockGet = vi.fn();
const mockPost = vi.fn();

vi.mock('@/lib/axios', () => ({
  AXIOS_INSTANCE: {
    get: (...args: unknown[]) => mockGet(...args),
    post: (...args: unknown[]) => mockPost(...args),
  },
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { success: vi.fn(), error: vi.fn(), warning: vi.fn(), info: vi.fn() },
    modal: { confirm: vi.fn() },
    notification: {},
  }),
}));

vi.mock('@/shared/auth/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: () => true }),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({ hasPermission: () => true }),
}));

vi.mock('@/components/ReportFilters', () => ({
  ReportFilters: ({ extra }: { extra?: React.ReactNode }) => <div>{extra}</div>,
}));

vi.mock('@/features/exports/components/ExportTemplateApplyBanner', () => ({
  ExportTemplateApplyBanner: () => null,
}));

vi.mock('@/components/reporting/FormalReportLanguageNotice', () => ({
  FormalReportLanguageNotice: () => null,
}));

vi.mock('@/features/auth/services/devTenant', () => ({
  getEffectiveTenantSlug: () => 'dev',
}));

beforeAll(() => {
  class ResizeObserverMock {
    observe() {}
    unobserve() {}
    disconnect() {}
  }
  vi.stubGlobal('ResizeObserver', ResizeObserverMock);
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      removeEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

const listRow = {
  id: 'aaaaaaaa-aaaa-aaaa-aaaa-aaaaaaaaaaaa',
  viennaBusinessDate: '2026-09-15',
  cashRegisterId: 'bbbbbbbb-bbbb-bbbb-bbbb-bbbbbbbbbbbb',
  registerNumber: 'Kasse-1',
  reportStatus: 'Provisional',
  correctionKind: 'None',
  grossSalesAmount: 128.5,
  createdAtUtc: '2026-09-15T18:00:00Z',
  submission: { lifecycle: 'not_submitted' },
};

function renderList() {
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <QueryClientProvider client={client}>
      <I18nProvider>
        <App>
          <TagesberichtListPage />
        </App>
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('Tagesbericht FA screens', () => {
  beforeEach(() => {
    mockGet.mockReset();
    mockPost.mockReset();
    mockGet.mockResolvedValue({ data: [listRow] });
  });

  it('renders list rows', async () => {
    renderList();
    expect(await screen.findByText('Kasse-1')).toBeInTheDocument();
    expect(screen.getByText('Provisorisch')).toBeInTheDocument();
    expect(screen.getByText('128,50')).toBeInTheDocument();
  });

  it('renders detail summary cards', () => {
    render(
      <I18nProvider>
        <TagesberichtSummaryCards
          grossLabel="Brutto"
          taxLabel="Steueraufschlüsselung"
          paymentsLabel="Zahlungsarten"
          grossAmount={250}
          taxTotalAmount={41.67}
          paymentRows={[{ methodKey: 'cash', displayLabel: 'Bar', rowCount: 3, totalAmount: 250 }]}
          taxRows={[{ taxBucketKey: '20', taxAmount: 41.67 }]}
          formatLocale="de-AT"
          methodColumn="Methode"
          linesColumn="Zeilen"
          sumColumn="Summe"
          taxBucketColumn="Steuergruppe"
          taxAmountColumn="Steuer"
        />
      </I18nProvider>
    );

    expect(screen.getByTestId('tagesbericht-summary-cards')).toBeInTheDocument();
    expect(screen.getByText('Brutto')).toBeInTheDocument();
    expect(screen.getByText('Steueraufschlüsselung')).toBeInTheDocument();
    expect(screen.getByText('Zahlungsarten')).toBeInTheDocument();
    expect(screen.getByText('Bar')).toBeInTheDocument();
    expect(screen.getByText('20')).toBeInTheDocument();
  });

  it('opens bulk finalize confirmation', async () => {
    renderList();
    expect(await screen.findByText('Kasse-1')).toBeInTheDocument();
    const checkboxes = screen.getAllByRole('checkbox');
    fireEvent.click(checkboxes[checkboxes.length - 1]);
    fireEvent.click(screen.getByTestId('tagesbericht-bulk-finalize'));
    await waitFor(() => {
      expect(screen.getByTestId('tagesbericht-bulk-finalize-modal')).toBeInTheDocument();
    });
    expect(screen.getByText('Tagesberichte finalisieren?')).toBeInTheDocument();
  });
});
