import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import type { ReactNode } from 'react';
import { describe, expect, it, vi } from 'vitest';

import { BillingSalesBulkBar } from '@/features/billing/components/BillingSalesBulkBar';
import { I18nProvider } from '@/i18n';

vi.mock('@/features/auth/services/authStorage', () => ({
  authStorage: {
    getAccessToken: () => null,
    getRefreshToken: () => null,
  },
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

describe('BillingSalesBulkBar', () => {
  it('renders nothing when nothing is selected', () => {
    const { container } = render(
      <BillingSalesBulkBar selectedCount={0} onAction={vi.fn()} />,
      { wrapper }
    );
    expect(container).toBeEmptyDOMElement();
  });

  it('shows selected count and emits bulk actions', async () => {
    const user = userEvent.setup();
    const onAction = vi.fn();
    render(<BillingSalesBulkBar selectedCount={3} onAction={onAction} />, { wrapper });

    expect(screen.getByText(/3 Lizenz\(en\) ausgewählt/i)).toBeInTheDocument();

    await user.click(screen.getByRole('button', { name: /Massenaktionen/i }));
    await user.click(await screen.findByText(/Lizenz verlängern \(\+30 Tage\)/i));
    expect(onAction).toHaveBeenCalledWith('extend30');
  });
});
