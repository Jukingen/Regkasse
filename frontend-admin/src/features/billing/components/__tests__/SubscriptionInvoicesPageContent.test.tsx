import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React, { type ReactNode } from 'react';
import { beforeAll, describe, expect, it, vi } from 'vitest';

import { SubscriptionInvoicesPageContent } from '@/features/billing/components/SubscriptionInvoicesPageContent';
import { I18nProvider } from '@/i18n';

vi.mock('next/navigation', () => ({
  usePathname: () => '/admin/billing/subscription-invoices',
  useRouter: () => ({ push: vi.fn(), replace: vi.fn() }),
}));

vi.mock('@/features/billing/hooks/useBillingAccess', () => ({
  useBillingAccess: () => true,
}));

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({ isAuthInitializing: false, user: { role: 'SuperAdmin' } }),
}));

vi.mock('@/hooks/useNotify', () => ({
  useNotify: () => ({
    success: vi.fn(),
    error: vi.fn(),
    apiError: vi.fn(),
  }),
}));

vi.mock('@/hooks/useAntdApp', () => ({
  useAntdApp: () => ({
    message: { success: vi.fn(), error: vi.fn(), open: vi.fn() },
    notification: {},
    modal: {},
  }),
}));

vi.mock('@/features/billing/api/subscriptionInvoicesApi', () => ({
  listSubscriptionInvoices: vi.fn().mockResolvedValue([]),
  generateMonthlySubscriptionInvoices: vi.fn(),
  markSubscriptionInvoicePaid: vi.fn(),
  voidSubscriptionInvoice: vi.fn(),
  downloadSubscriptionInvoicePdf: vi.fn(),
}));

vi.mock('@/features/super-admin/api/adminTenants', () => ({
  listAdminTenants: vi.fn().mockResolvedValue([]),
}));

function wrapper({ children }: { children: ReactNode }) {
  const client = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return (
    <QueryClientProvider client={client}>
      <I18nProvider>{children}</I18nProvider>
    </QueryClientProvider>
  );
}

describe('SubscriptionInvoicesPageContent', () => {
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
        removeEventListener: vi.fn(),
        dispatchEvent: vi.fn(),
      })),
    });
  });

  it('renders Super Admin subscription invoice heading', async () => {
    render(<SubscriptionInvoicesPageContent />, { wrapper });
    expect(await screen.findByRole('heading', { name: 'Abonnement-Rechnungen' })).toBeInTheDocument();
  });
});
