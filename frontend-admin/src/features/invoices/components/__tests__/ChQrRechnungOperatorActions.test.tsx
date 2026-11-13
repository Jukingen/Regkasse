/**
 * CH QR-Rechnung operator actions on the invoice detail.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { ChQrRechnungOperatorActions } from '@/features/invoices/components/ChQrRechnungOperatorActions';
import { I18nProvider } from '@/i18n';

const mockUseGet = vi.fn();
const mockUseAuth = vi.fn();
const mockUseTenant = vi.fn();
const mockUseCompanySettings = vi.fn();

vi.mock('@/api/generated/admin/admin', () => ({
  useGetApiAdminTenantsTenantIdChQrGapAcceptance: (...args: unknown[]) => mockUseGet(...args),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/features/tenancy/providers/TenantProvider', () => ({
  useTenant: () => mockUseTenant(),
}));

vi.mock('@/features/settings/hooks/useCompanySettings', () => ({
  useCompanySettings: () => mockUseCompanySettings(),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({ success: vi.fn(), error: vi.fn() }),
}));

vi.mock('@/features/invoices/api/chQrRechnungOperatorApi', () => ({
  downloadChQrInvoicePdf: vi.fn(),
  confirmChQrBankUpload: vi.fn(),
}));

const GAPS = [
  { id: 'official-swiss-cross', present: false },
  { id: 'pain001', present: false },
];

function renderActions() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <ChQrRechnungOperatorActions invoiceId="invoice-1" />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('ChQrRechnungOperatorActions', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockUseAuth.mockReturnValue({
      user: { role: 'SuperAdmin', permissions: ['system.critical'], userName: 'ops' },
    });
    mockUseTenant.mockReturnValue({ tenant: { id: 'tenant-ch' } });
    mockUseCompanySettings.mockReturnValue({ data: { country: 'CH' } });
    mockUseGet.mockReturnValue({
      isSuccess: true,
      data: {
        knownGaps: GAPS,
        acceptance: { acceptedGaps: ['official-swiss-cross', 'pain001'] },
      },
    });
  });

  it('renders download and upload actions for a CH tenant when gaps are accepted', () => {
    renderActions();

    expect(screen.getByTestId('ch-qr-operator-download')).toBeInTheDocument();
    expect(screen.getByTestId('ch-qr-operator-upload')).toBeInTheDocument();
    expect(screen.queryByTestId('ch-qr-operator-outstanding')).not.toBeInTheDocument();
  });

  it('hides the actions for an AT tenant', () => {
    mockUseCompanySettings.mockReturnValue({ data: { country: 'AT' } });
    renderActions();

    expect(screen.queryByTestId('ch-qr-operator-download')).not.toBeInTheDocument();
    expect(screen.queryByTestId('ch-qr-operator-upload')).not.toBeInTheDocument();
  });

  it('shows the outstanding warning and hides the actions when a gap is not accepted', () => {
    mockUseGet.mockReturnValue({
      isSuccess: true,
      data: {
        knownGaps: GAPS,
        acceptance: { acceptedGaps: ['official-swiss-cross'] },
      },
    });
    renderActions();

    expect(screen.getByTestId('ch-qr-operator-outstanding')).toHaveTextContent('pain001');
    expect(screen.queryByTestId('ch-qr-operator-download')).not.toBeInTheDocument();
    expect(screen.queryByTestId('ch-qr-operator-upload')).not.toBeInTheDocument();
  });
});
