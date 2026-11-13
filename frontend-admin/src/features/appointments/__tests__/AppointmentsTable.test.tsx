import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it } from 'vitest';

import { AppointmentsTable } from '@/features/appointments/AppointmentsTable';
import { I18nProvider } from '@/i18n';

describe('AppointmentsTable', () => {
  it('renders appointment rows', () => {
    render(
      <I18nProvider>
        <AppointmentsTable
          rows={[
            {
              id: 'a1',
              startUtc: '2026-10-03T09:00:00Z',
              endUtc: '2026-10-03T09:30:00Z',
              staffId: 'stylist-1',
              status: 'Booked',
              notes: 'Schnitt',
            },
          ]}
        />
      </I18nProvider>
    );

    expect(screen.getByText('stylist-1')).toBeInTheDocument();
    expect(screen.getByText('Booked')).toBeInTheDocument();
    expect(screen.getByText('Schnitt')).toBeInTheDocument();
  });
});
