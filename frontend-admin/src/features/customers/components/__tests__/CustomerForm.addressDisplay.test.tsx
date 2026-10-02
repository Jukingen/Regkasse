import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import CustomerForm from '@/features/customers/components/CustomerForm';
import { I18nProvider } from '@/i18n';

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { role: 'Manager', permissions: ['customer.view', 'customer.manage'] },
  }),
}));

describe('CustomerForm address display', () => {
  it('shows structured address fields from address_data', async () => {
    render(
      <I18nProvider>
        <App>
          <CustomerForm
            visible
            loading={false}
            onCancel={() => undefined}
            onSubmit={() => undefined}
            initialValues={{
              id: 'cust-1',
              name: 'Lena Mobil',
              tenantId: 'tenant-1',
              address: 'Feldweg 4, 5020 Salzburg',
              addressData: {
                street: 'Feldweg 4',
                postalCode: '5020',
                city: 'Salzburg',
                notes: 'Hinterhof',
              },
            }}
          />
        </App>
      </I18nProvider>
    );

    expect(await screen.findByLabelText('Straße')).toHaveValue('Feldweg 4');
    expect(screen.getByLabelText('PLZ')).toHaveValue('5020');
    expect(screen.getByLabelText('Ort')).toHaveValue('Salzburg');
    expect(screen.getByLabelText('Adresshinweis')).toHaveValue('Hinterhof');
  });
});
