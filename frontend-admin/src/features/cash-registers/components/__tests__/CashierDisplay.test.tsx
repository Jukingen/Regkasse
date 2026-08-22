import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it } from 'vitest';

import { CashierDisplay } from '@/features/cash-registers/components/CashierDisplay';
import { I18nProvider } from '@/i18n';

function renderDisplay(props: React.ComponentProps<typeof CashierDisplay>) {
  return render(
    <I18nProvider>
      <CashierDisplay {...props} />
    </I18nProvider>
  );
}

describe('CashierDisplay', () => {
  it('shows display name, username, and email', () => {
    renderDisplay({
      user: {
        firstName: 'Anna',
        lastName: 'Berger',
        userName: 'cashier1',
        email: 'anna.berger@example.com',
      },
    });

    expect(screen.getByText('Anna Berger')).toBeInTheDocument();
    expect(screen.getByText('(cashier1)')).toBeInTheDocument();
    expect(screen.getByText('anna.berger@example.com')).toBeInTheDocument();
  });

  it('falls back to DTO fields when nested user is missing', () => {
    renderDisplay({
      displayName: 'Bruno Klein',
      userName: 'cashier2',
      email: 'bruno.klein@example.com',
    });

    expect(screen.getByText('Bruno Klein')).toBeInTheDocument();
    expect(screen.getByText('(cashier2)')).toBeInTheDocument();
    expect(screen.getByText('bruno.klein@example.com')).toBeInTheDocument();
  });

  it('does not repeat the username when it is the only identifier', () => {
    renderDisplay({ userName: 'cashier1' });

    expect(screen.getByText('cashier1')).toBeInTheDocument();
    expect(screen.queryByText('(cashier1)')).not.toBeInTheDocument();
  });

  it('shows the empty-state copy when no cashier is assigned', () => {
    renderDisplay({});

    expect(screen.getByText('Kein Kassierer')).toBeInTheDocument();
  });
});
