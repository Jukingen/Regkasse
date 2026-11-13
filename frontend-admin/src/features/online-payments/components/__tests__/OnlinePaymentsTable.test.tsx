import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, expect, it, vi } from 'vitest';

import type { AdminOnlinePaymentDto } from '@/features/online-payments/api/onlinePaymentsApi';
import { OnlinePaymentsTable } from '@/features/online-payments/components/OnlinePaymentsTable';
import { I18nProvider } from '@/i18n';

const rows: AdminOnlinePaymentDto[] = [
  {
    id: 'pay-1',
    tenantId: 'tenant-1',
    tenantName: 'Cafe Central',
    tenantSlug: 'cafe-central',
    amount: 12.5,
    currency: 'EUR',
    status: 'PENDING',
    paymentMethod: 'paypal',
    provider: 'Mock',
    isSynthetic: true,
    createdAtUtc: '2026-08-26T10:00:00.000Z',
  },
  {
    id: 'pay-2',
    tenantId: 'tenant-1',
    tenantName: 'Cafe Central',
    tenantSlug: 'cafe-central',
    amount: 4,
    currency: 'EUR',
    status: 'Succeeded',
    paymentMethod: 'card',
    provider: 'Mock',
    isSynthetic: false,
    createdAtUtc: '2026-08-26T09:00:00.000Z',
  },
];

describe('OnlinePaymentsTable', () => {
  it('renders tenant, status, and view-details action', async () => {
    const user = userEvent.setup();
    const onViewDetails = vi.fn();
    render(
      <I18nProvider>
        <OnlinePaymentsTable payments={rows} onViewDetails={onViewDetails} />
      </I18nProvider>
    );

    expect(screen.getAllByText('Cafe Central').length).toBeGreaterThan(0);
    expect(screen.getByText('Ausstehend')).toBeInTheDocument();
    expect(screen.getByText('Erfolgreich')).toBeInTheDocument();
    expect(screen.getByText('Test')).toBeInTheDocument();

    await user.click(screen.getAllByRole('button', { name: /Details anzeigen/ })[0]!);
    expect(onViewDetails).toHaveBeenCalledWith(expect.objectContaining({ id: 'pay-1' }));
  });

  it('shows the empty copy when there are no rows', () => {
    render(
      <I18nProvider>
        <OnlinePaymentsTable payments={[]} onViewDetails={vi.fn()} emptyText="Keine Online-Zahlungen vorhanden" />
      </I18nProvider>
    );
    expect(screen.getByText('Keine Online-Zahlungen vorhanden')).toBeInTheDocument();
  });
});
