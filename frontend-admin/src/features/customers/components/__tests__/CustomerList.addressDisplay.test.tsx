import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it, vi } from 'vitest';

import CustomerList from '@/features/customers/components/CustomerList';
import { I18nProvider } from '@/i18n';

vi.mock('@/features/auth/hooks/useAuth', () => ({
  useAuth: () => ({
    user: { role: 'Manager', permissions: ['customer.view'] },
  }),
}));

vi.mock('@/hooks/usePermissions', () => ({
  usePermissions: () => ({
    hasPermission: () => true,
  }),
}));

describe('CustomerList address display', () => {
  it('renders structured address_data in the address column', () => {
    render(
      <I18nProvider>
        <CustomerList
          loading={false}
          onEdit={() => undefined}
          onDelete={() => undefined}
          data={[
            {
              id: 'cust-1',
              name: 'Lena Mobil',
              tenantId: 'tenant-1',
              email: 'lena@example.test',
              addressData: {
                street: 'Feldweg 4',
                postalCode: '5020',
                city: 'Salzburg',
              },
            },
          ]}
        />
      </I18nProvider>
    );

    expect(screen.getByText('Feldweg 4, 5020, Salzburg')).toBeTruthy();
  });
});
