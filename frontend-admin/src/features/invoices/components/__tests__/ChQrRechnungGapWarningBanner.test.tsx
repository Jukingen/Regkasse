import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ChQrRechnungGapWarningBanner } from '@/features/invoices/components/ChQrRechnungGapWarningBanner';
import { I18nProvider } from '@/i18n';

const tenantId = '11111111-1111-1111-1111-111111111111';
const mockUseGet = vi.fn();
const mockUseCompanySettings = vi.fn();

vi.mock('@/api/generated/admin/admin', () => ({
  useGetApiAdminTenantsTenantIdChQrGapAcceptance: (...args: unknown[]) => mockUseGet(...args),
}));

vi.mock('@/features/tenancy/providers/TenantProvider', () => ({
  useTenant: () => ({ tenant: { id: tenantId } }),
}));

vi.mock('@/features/settings/hooks/useCompanySettings', () => ({
  useCompanySettings: () => mockUseCompanySettings(),
}));

const catalog = [
  { id: 'official-swiss-cross', present: false },
  { id: 'font-embedding-liberation-arial', present: false },
  { id: 'pain001', present: false },
  { id: 'bank-scan', present: false },
  { id: 'perforation-line', present: true },
];

function payload(acceptedGaps: string[]) {
  return {
    data: { knownGaps: catalog, acceptance: { acceptedGaps } },
    isLoading: false,
    isError: false,
    isSuccess: true,
  };
}

function renderBanner(country: string, acceptedGaps: string[]) {
  mockUseCompanySettings.mockReturnValue({ data: { country } });
  mockUseGet.mockReturnValue(payload(acceptedGaps));
  const client = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(
    <I18nProvider>
      <QueryClientProvider client={client}>
        <ChQrRechnungGapWarningBanner />
      </QueryClientProvider>
    </I18nProvider>,
  );
}

describe('ChQrRechnungGapWarningBanner', () => {
  beforeEach(() => {
    mockUseGet.mockReset();
    mockUseCompanySettings.mockReset();
  });

  it('renders outstanding gap labels for a CH tenant and links to acceptance', () => {
    renderBanner('CH', ['official-swiss-cross']);

    expect(screen.getByTestId('ch-qr-gap-warning-banner')).toHaveTextContent(
      'QR-Rechnung: akzeptierte Druckfunktionen unvollständig',
    );
    expect(screen.getByTestId('ch-qr-gap-warning-pain001')).toHaveTextContent(
      'pain.001-Überweisungsdatei',
    );
    expect(screen.queryByTestId('ch-qr-gap-warning-official-swiss-cross')).not.toBeInTheDocument();
    expect(screen.queryByTestId('ch-qr-gap-warning-perforation-line')).not.toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Lückenbestätigung öffnen' })).toHaveAttribute(
      'href',
      `/admin/tenants/${tenantId}`,
    );
  });

  it('does not render when every open gap is accepted', () => {
    renderBanner('CH', [
      'official-swiss-cross',
      'font-embedding-liberation-arial',
      'pain001',
      'bank-scan',
    ]);

    expect(screen.queryByTestId('ch-qr-gap-warning-banner')).not.toBeInTheDocument();
  });

  it('does not render for AT or DE', () => {
    const { unmount } = renderBanner('AT', []);
    expect(screen.queryByTestId('ch-qr-gap-warning-banner')).not.toBeInTheDocument();
    unmount();

    renderBanner('DE', []);
    expect(screen.queryByTestId('ch-qr-gap-warning-banner')).not.toBeInTheDocument();
  });
});
