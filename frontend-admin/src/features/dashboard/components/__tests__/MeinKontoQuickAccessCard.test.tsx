import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React, { type ReactNode } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { MeinKontoQuickAccessCard } from '@/features/dashboard/components/MeinKontoQuickAccessCard';
import { I18nProvider } from '@/i18n';
import { PERMISSIONS } from '@/shared/auth/permissions';

const mockHasPermission = vi.fn();
const mockUseAuthorizedQuery = vi.fn();
const mockUseLicenseStatus = vi.fn();

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({
    hasPermission: (permission: string) => mockHasPermission(permission),
  }),
}));

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizedQuery: (...args: unknown[]) => mockUseAuthorizedQuery(...args),
}));

vi.mock('@/hooks/useLicenseStatus', () => ({
  useLicenseStatus: () => mockUseLicenseStatus(),
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

describe('MeinKontoQuickAccessCard', () => {
  it('hides the card without license.manage', () => {
    mockHasPermission.mockReturnValue(false);
    mockUseLicenseStatus.mockReturnValue({ status: null });
    mockUseAuthorizedQuery.mockReturnValue({ data: null, isLoading: false });

    const { container } = render(
      <Wrapper>
        <MeinKontoQuickAccessCard />
      </Wrapper>
    );

    expect(container).toBeEmptyDOMElement();
    expect(mockHasPermission).toHaveBeenCalledWith(PERMISSIONS.LICENSE_MANAGE);
  });

  it('shows license status, invoice badge, and portal link', () => {
    mockHasPermission.mockReturnValue(true);
    mockUseLicenseStatus.mockReturnValue({
      status: { state: 'Grace', daysUntilExpiry: 0, graceDaysRemaining: 5, daysOverdue: 2 },
    });
    mockUseAuthorizedQuery.mockImplementation((options: { queryKey?: unknown[] }) => {
      const key = JSON.stringify(options.queryKey ?? []);
      if (key.includes('open-count')) {
        return { data: { openCount: 2 }, isLoading: false, isAuthorized: true };
      }
      return {
        data: { totalCount: 4, activeCount: 4, cancelledCount: 0 },
        isLoading: false,
        isAuthorized: true,
      };
    });

    render(
      <Wrapper>
        <MeinKontoQuickAccessCard />
      </Wrapper>
    );

    expect(screen.getByText('Mein Konto')).toBeInTheDocument();
    expect(screen.getByText('Gnadenfrist')).toBeInTheDocument();
    expect(screen.getByText('4')).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /Zum Konto/i })).toHaveAttribute(
      'href',
      '/tenant/portal'
    );
    expect(screen.getByRole('link', { name: 'Support' })).toHaveAttribute(
      'href',
      '/tenant/support'
    );
    expect(screen.getByLabelText('2 offene Tickets')).toBeInTheDocument();
  });
});
