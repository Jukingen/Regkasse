import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it } from 'vitest';

import { PatientsTable } from '@/features/patients/PatientsTable';
import { I18nProvider } from '@/i18n';

describe('PatientsTable', () => {
  it('renders pet and owner columns', () => {
    render(
      <I18nProvider>
        <PatientsTable
          rows={[
            {
              id: 'p1',
              customerName: 'Anna Hofer',
              petName: 'Bello',
              petSpecies: 'Hund',
              petBreed: 'Mix',
              petBirthDate: '2020-04-01',
              lastVisit: '2026-09-01',
              phone: '+431234',
              email: 'anna@example.test',
            },
          ]}
        />
      </I18nProvider>
    );

    expect(screen.getByText('Anna Hofer')).toBeInTheDocument();
    expect(screen.getByText('Bello')).toBeInTheDocument();
    expect(screen.getByText('Hund')).toBeInTheDocument();
    expect(screen.getByText('2026-09-01')).toBeInTheDocument();
    expect(screen.getByText('+431234 · anna@example.test')).toBeInTheDocument();
  });

  it('renders doctor columns for a human patient', () => {
    render(
      <I18nProvider>
        <PatientsTable
          kind="doctor"
          rows={[
            {
              id: 'p2',
              customerName: 'Eva Berger',
              petName: '—',
              petSpecies: '—',
              petBreed: '—',
              petBirthDate: '1984-02-11',
              allergies: 'Penicillin',
              chronicConditions: 'Asthma',
              insurance: 'ÖGK',
              lastVisit: '2026-10-01',
              phone: '+431111',
              email: 'eva@example.test',
            },
          ]}
        />
      </I18nProvider>
    );

    expect(screen.getByText('Patient')).toBeInTheDocument();
    expect(screen.getByText('Eva Berger')).toBeInTheDocument();
    expect(screen.getByText('Penicillin')).toBeInTheDocument();
    expect(screen.getByText('ÖGK')).toBeInTheDocument();
    expect(screen.queryByText('Tiername')).not.toBeInTheDocument();
  });
});
