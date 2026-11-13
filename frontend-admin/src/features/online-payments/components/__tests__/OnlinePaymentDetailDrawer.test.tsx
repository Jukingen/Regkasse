import { describe, expect, it } from 'vitest';

import { OnlinePaymentDetailDrawer } from '@/features/online-payments/components/OnlinePaymentDetailDrawer';
import { I18nProvider } from '@/i18n';
import { render, screen } from '@testing-library/react';

describe('OnlinePaymentDetailDrawer', () => {
  it('renders payment details when open', () => {
    render(
      <I18nProvider>
        <OnlinePaymentDetailDrawer
          payment={{
            id: 'pay-detail',
            tenantId: 'tenant-1',
            tenantName: 'Cafe Central',
            tenantSlug: 'cafe-central',
            amount: 12.34,
            currency: 'EUR',
            status: 'FAILED',
            paymentMethod: 'card',
            provider: 'Mock',
            paymentIntentId: 'pi_detail',
            isSynthetic: true,
            errorMessage: 'Simulated webhook failure.',
            lastWebhookEvent: 'payment_intent.payment_failed',
            createdAtUtc: '2026-08-26T10:00:00.000Z',
          }}
          onClose={() => undefined}
        />
      </I18nProvider>
    );

    expect(screen.getByText('Online-Zahlung')).toBeInTheDocument();
    expect(screen.getByText('pay-detail')).toBeInTheDocument();
    expect(screen.getByText('Cafe Central')).toBeInTheDocument();
    expect(screen.getByText('Fehlgeschlagen')).toBeInTheDocument();
    expect(screen.getByText('pi_detail')).toBeInTheDocument();
    expect(screen.getByText('Simulated webhook failure.')).toBeInTheDocument();
  });
});
