import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it } from 'vitest';

import { TaxiTripsTable } from '@/features/taxi-trips/TaxiTripsTable';
import { I18nProvider } from '@/i18n';

describe('TaxiTripsTable', () => {
  it('renders trip route, distance, amount, and register', () => {
    render(
      <I18nProvider>
        <TaxiTripsTable
          rows={[
            {
              id: 't1',
              occurredAt: '2026-10-02T18:00:00Z',
              customerName: 'Max Fahrer',
              routeFrom: 'Hauptbahnhof',
              routeTo: 'Flughafen',
              routeKm: '18.5',
              amount: '42.00',
              cashRegister: 'K1',
            },
          ]}
        />
      </I18nProvider>
    );

    expect(screen.getByText('Max Fahrer')).toBeInTheDocument();
    expect(screen.getByText('Hauptbahnhof')).toBeInTheDocument();
    expect(screen.getByText('Flughafen')).toBeInTheDocument();
    expect(screen.getByText('18.5')).toBeInTheDocument();
    expect(screen.getByText('42.00')).toBeInTheDocument();
    expect(screen.getByText('K1')).toBeInTheDocument();
  });
});
