import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React, { type ReactNode } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { TenantInvoicesPageContent } from '@/features/tenant-invoices/components/TenantInvoicesPageContent';
import { I18nProvider } from '@/i18n';

vi.mock('next/navigation', () => ({
  usePathname: () => '/tenant/invoices',
  useRouter: () => ({ push: vi.fn(), back: vi.fn() }),
}));

vi.mock('@/hooks/useAuthorizedQuery', () => ({
  useAuthorizedQuery: () => ({
    data: {
      items: [],
      totalCount: 0,
      page: 1,
      pageSize: 20,
      totalPages: 0,
      activeCount: 0,
      cancelledCount: 0,
    },
    isLoading: false,
    isAuthorized: true,
  }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { open: vi.fn(), success: vi.fn(), error: vi.fn() },
  }),
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

describe('TenantInvoicesPageContent', () => {
  it('renders the my-invoices page title', () => {
    render(
      <Wrapper>
        <TenantInvoicesPageContent />
      </Wrapper>
    );

    expect(screen.getByRole('heading', { name: 'Meine Rechnungen' })).toBeInTheDocument();
  });
});
