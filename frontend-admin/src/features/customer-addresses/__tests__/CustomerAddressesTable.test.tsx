import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it } from 'vitest';

import { CustomerAddressesTable } from '@/features/customer-addresses/CustomerAddressesTable';
import { I18nProvider } from '@/i18n';

describe('CustomerAddressesTable', () => {
  it('renders structured address fields', () => {
    render(
      <I18nProvider>
        <CustomerAddressesTable
          rows={[
            {
              id: 'c1',
              customerName: 'Lena Mobil',
              street: 'Feldweg 4',
              postalCode: '5020',
              city: 'Salzburg',
              contact: 'lena@example.test',
            },
          ]}
        />
      </I18nProvider>
    );

    expect(screen.getByText('Lena Mobil')).toBeInTheDocument();
    expect(screen.getByText('Feldweg 4')).toBeInTheDocument();
    expect(screen.getByText('5020')).toBeInTheDocument();
    expect(screen.getByText('Salzburg')).toBeInTheDocument();
  });
});
