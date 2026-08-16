import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React, { type ReactNode } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { TenantPortalPageContent } from '@/features/tenant-portal/components/TenantPortalPageContent';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const mockUseAuthorizedQuery = vi.fn();
const mockUseLicenseStatus = vi.fn();
const mockUseAuth = vi.fn();
const mockUseCurrentTenant = vi.fn();

vi.mock('next/navigation', () => ({
  usePathname: () => '/tenant/portal',
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizedQuery: (...args: unknown[]) => mockUseAuthorizedQuery(...args),
}));

vi.mock('@/hooks/useLicenseStatus', () => ({
  useLicenseStatus: () => mockUseLicenseStatus(),
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => mockUseAuth(),
}));

vi.mock('@/features/tenancy/hooks/useCurrentTenant', () => ({
  useCurrentTenant: () => mockUseCurrentTenant(),
}));

function Wrapper({ children }: { children: ReactNode }) {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return (
    <QueryClientProvider client={queryClient}>
      <I18nProvider>{children}</I18nProvider>
    </QueryClientProvider>
  );
}

beforeAll(() => {
  Object.defineProperty(window, 'matchMedia', {
    writable: true,
    value: vi.fn().mockImplementation((query: string) => ({
      matches: false,
      media: query,
      onchange: null,
      addListener: vi.fn(),
      removeListener: vi.fn(),
      addEventListener: vi.fn(),
      dispatchEvent: vi.fn(),
    })),
  });
});

describe('TenantPortalPageContent', () => {
  it('renders status cards, remaining days, and quick links', () => {
    mockUseAuth.mockReturnValue({
      user: { firstName: 'Anna', lastName: 'Huber', userName: 'ahuber' },
    });
    mockUseCurrentTenant.mockReturnValue({ tenantId: 'tenant-1' });
    mockUseLicenseStatus.mockReturnValue({
      status: {
        state: 'Active',
        daysUntilExpiry: 14,
        graceDaysRemaining: 0,
        daysOverdue: 0,
      },
      isLoading: false,
    });
    mockUseAuthorizedQuery.mockImplementation((options: { queryKey: unknown[] }) => {
      const key = JSON.stringify(options.queryKey);
      if (key.includes('tenant-invoices')) {
        return {
          data: { items: [], totalCount: 3, activeCount: 3, cancelledCount: 0 },
          isLoading: false,
          isAuthorized: true,
        };
      }
      if (key.includes('open-count')) {
        return {
          data: { openCount: 2 },
          isLoading: false,
          isAuthorized: true,
        };
      }
      return {
        data: { completedCount: 2, totalCount: 4, isFullyComplete: false, steps: [] },
        isLoading: false,
        isAuthorized: true,
      };
    });

    render(
      <Wrapper>
        <TenantPortalPageContent />
      </Wrapper>
    );

    expect(screen.getByRole('heading', { name: 'Mein Konto' })).toBeInTheDocument();
    expect(screen.getByText('Willkommen zurück, Anna Huber!')).toBeInTheDocument();
    expect(screen.getByText('Aktiv')).toBeInTheDocument();
    expect(screen.getByText('Noch 14 Tage')).toBeInTheDocument();
    expect(screen.getByText('3 Rechnungen')).toBeInTheDocument();
    expect(screen.getByText('0 offen')).toBeInTheDocument();
    expect(screen.getByText('2 offene Tickets')).toBeInTheDocument();
    expect(screen.getByText('Profil unvollständig')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: 'Zur Lizenz' })).toHaveAttribute(
      'href',
      '/license/dashboard'
    );
    expect(screen.getByRole('link', { name: 'Meine Rechnungen' })).toHaveAttribute(
      'href',
      '/tenant/invoices'
    );
    expect(screen.getByRole('link', { name: 'Mein Profil' })).toHaveAttribute('href', '/profile');
    const supportLinks = screen.getAllByRole('link', { name: 'Support' });
    expect(supportLinks.length).toBeGreaterThanOrEqual(1);
    for (const link of supportLinks) {
      expect(link).toHaveAttribute('href', '/tenant/support');
    }
    expect(mockUseAuthorizedQuery).toHaveBeenCalledWith(
      expect.objectContaining({
        requiredPermission: [PERMISSIONS.LICENSE_MANAGE],
      })
    );
  });
});
