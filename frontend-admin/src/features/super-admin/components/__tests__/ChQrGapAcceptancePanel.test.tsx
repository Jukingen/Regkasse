/**
 * ChQrGapAcceptancePanel — Super Admin CH QR print-gap acknowledgement.
 */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import type { ChQrGapAcceptanceResponse } from '@/api/generated/model';
import type { AdminTenantDetail } from '@/features/super-admin/api/adminTenants';
import { ChQrGapAcceptancePanel } from '@/features/super-admin/components/ChQrGapAcceptancePanel';
import { I18nProvider } from '@/i18n';

const mockUseGet = vi.fn();
const mockUsePost = vi.fn();
const mutateAsync = vi.fn();
const mockSuccess = vi.fn();
const mockError = vi.fn();
const mockUseAuth = vi.fn();

vi.mock('@/api/generated/admin/admin', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/api/generated/admin/admin')>();
  return {
    ...actual,
    useGetApiAdminTenantsTenantIdChQrGapAcceptance: (...args: unknown[]) => mockUseGet(...args),
    usePostApiAdminTenantsTenantIdChQrGapAcceptance: (...args: unknown[]) => mockUsePost(...args),
  };
});

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: mockSuccess,
    error: mockError,
    apiError: vi.fn(),
  }),
}));

const TENANT_ID = '11111111-1111-1111-1111-111111111111';

const KNOWN_GAPS = [
  { id: 'official-swiss-cross', present: false },
  { id: 'font-embedding-liberation-arial', present: false },
  { id: 'pain001', present: false },
  { id: 'bank-scan', present: false },
  { id: 'perforation-line', present: false },
];

function acceptanceResponse(acceptedGaps: string[] | null): ChQrGapAcceptanceResponse {
  return {
    knownGaps: KNOWN_GAPS,
    acceptance: acceptedGaps
      ? {
          acceptedGaps,
          acceptedBy: 'super-admin',
          acceptedAtUtc: '2026-09-30T05:00:00Z',
        }
      : undefined,
  };
}

function tenant(country: string): AdminTenantDetail {
  return {
    id: TENANT_ID,
    name: 'Cafe Bern',
    slug: 'cafe-bern',
    status: 'active',
    isActive: true,
    createdAt: '2026-01-01T00:00:00Z',
    country,
  };
}

function loadedQuery(data: ChQrGapAcceptanceResponse | undefined, error: unknown = null) {
  return {
    data,
    error,
    isLoading: false,
    isSuccess: error == null,
    isError: error != null,
  };
}

function renderPanel(country = 'CH') {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
  });
  return render(
    <QueryClientProvider client={queryClient}>
      <I18nProvider>
        <ChQrGapAcceptancePanel tenant={tenant(country)} />
      </I18nProvider>
    </QueryClientProvider>
  );
}

describe('ChQrGapAcceptancePanel', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mutateAsync.mockResolvedValue(acceptanceResponse(['official-swiss-cross']));
    mockUsePost.mockReturnValue({ mutateAsync, isPending: false });
    mockUseGet.mockReturnValue(loadedQuery(acceptanceResponse(null)));
    mockUseAuth.mockReturnValue({
      user: { role: 'SuperAdmin', permissions: ['system.critical'] },
    });
  });

  it('renders known gaps for a CH tenant when the caller is Super Admin', () => {
    renderPanel('CH');

    expect(screen.getByTestId('ch-qr-gap-acceptance-panel')).toBeInTheDocument();
    expect(screen.getByText('Offizielles 7-mm-Schweizerkreuz mit weißem Rand')).toBeInTheDocument();
    expect(screen.getByText('pain.001-Überweisungsdatei')).toBeInTheDocument();
    expect(screen.getByTestId('ch-qr-gap-outstanding-warning')).toHaveTextContent(
      'Diese Druckfunktionen sind nicht Teil der aktuellen QR-Rechnung-Ausgabe.'
    );
    expect(screen.getByTestId('ch-qr-gap-save')).toBeInTheDocument();
    expect(mockUseGet).toHaveBeenCalledWith(
      TENANT_ID,
      expect.objectContaining({
        query: expect.objectContaining({ enabled: true }),
      })
    );
  });

  it('does not render for AT, DE, or EU_DEFAULT tenants', () => {
    for (const country of ['AT', 'DE', 'EU_DEFAULT']) {
      const view = renderPanel(country);
      expect(screen.queryByTestId('ch-qr-gap-acceptance-panel')).not.toBeInTheDocument();
      expect(mockUseGet).toHaveBeenCalledWith(
        TENANT_ID,
        expect.objectContaining({
          query: expect.objectContaining({ enabled: false }),
        })
      );
      view.unmount();
    }
  });

  it('does not render for Mandanten-Admin', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'Manager', permissions: ['settings.manage'] },
    });
    renderPanel('CH');

    expect(screen.queryByTestId('ch-qr-gap-acceptance-panel')).not.toBeInTheDocument();
  });

  it('hides the save action without system.critical', () => {
    mockUseAuth.mockReturnValue({
      user: { role: 'SuperAdmin', permissions: [] },
    });
    renderPanel('CH');

    expect(screen.getByTestId('ch-qr-gap-acceptance-panel')).toBeInTheDocument();
    expect(screen.queryByTestId('ch-qr-gap-save')).not.toBeInTheDocument();
  });

  it('pre-checks gaps from an existing acceptance record', () => {
    mockUseGet.mockReturnValue(
      loadedQuery(acceptanceResponse(['official-swiss-cross', 'perforation-line']))
    );
    renderPanel('CH');

    expect(screen.getByRole('checkbox', { name: /Schweizerkreuz/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /Perforationsmarke/ })).toBeChecked();
    expect(screen.getByRole('checkbox', { name: /pain\.001/ })).not.toBeChecked();
  });

  it('saves the checked gap ids', async () => {
    mockUseGet.mockReturnValue(loadedQuery(acceptanceResponse(['official-swiss-cross'])));
    renderPanel('CH');

    fireEvent.click(screen.getByRole('checkbox', { name: /pain\.001/ }));
    fireEvent.click(screen.getByTestId('ch-qr-gap-save'));

    await waitFor(() => {
      expect(mutateAsync).toHaveBeenCalledWith({
        tenantId: TENANT_ID,
        data: { acceptedGaps: ['official-swiss-cross', 'pain001'] },
      });
    });
    expect(mockSuccess).toHaveBeenCalledWith('Lückenbestätigung gespeichert.');
  });

  it('highlights invalid gap ids from a 400 response', async () => {
    mockUseGet.mockReturnValue(loadedQuery(acceptanceResponse(['pain001'])));
    mutateAsync.mockRejectedValueOnce({
      response: {
        status: 400,
        data: { message: 'Unknown gap.', invalidGapIds: ['pain001', 'not-a-real-gap'] },
      },
    });
    renderPanel('CH');

    fireEvent.click(screen.getByTestId('ch-qr-gap-save'));

    await waitFor(() => {
      expect(mockError).toHaveBeenCalledWith(
        'Eine oder mehrere Lücken-IDs gehören nicht zum bekannten Katalog.'
      );
    });
    expect(screen.getByTestId('ch-qr-gap-invalid-pain001')).toBeInTheDocument();
    expect(screen.getByTestId('ch-qr-gap-pain001')).toHaveAttribute('data-invalid', 'true');
    expect(screen.getByTestId('ch-qr-gap-invalid-alert')).toHaveTextContent('not-a-real-gap');
  });

  it('hides the panel when the acceptance endpoint returns 404', () => {
    mockUseGet.mockReturnValue(loadedQuery(undefined, { response: { status: 404 } }));
    renderPanel('CH');

    expect(screen.queryByTestId('ch-qr-gap-acceptance-panel')).not.toBeInTheDocument();
  });
});
