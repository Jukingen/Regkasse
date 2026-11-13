import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it } from 'vitest';

import { ImeiTable } from '@/features/imeis/ImeiTable';
import { I18nProvider } from '@/i18n';

describe('ImeiTable', () => {
  it('renders imei, product, status, sold date, and warranty', () => {
    render(
      <I18nProvider>
        <ImeiTable
          rows={[
            {
              id: 'i1',
              imei: '490154203237518',
              productName: 'Pixel 9',
              status: 'Sold',
              soldAt: '2026-08-12',
              warrantyMonths: 24,
            },
          ]}
        />
      </I18nProvider>
    );

    expect(screen.getByText('490154203237518')).toBeInTheDocument();
    expect(screen.getByText('Pixel 9')).toBeInTheDocument();
    expect(screen.getByText('Sold')).toBeInTheDocument();
    expect(screen.getByText('2026-08-12')).toBeInTheDocument();
    expect(screen.getByText('24')).toBeInTheDocument();
  });
});
