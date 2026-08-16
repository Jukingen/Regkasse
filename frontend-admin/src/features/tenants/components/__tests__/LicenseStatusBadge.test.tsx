import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';

import { LicenseStatusBadge } from '../LicenseStatusBadge';
import { render, screen } from '@testing-library/react';

describe('LicenseStatusBadge', () => {
  beforeEach(() => {
    vi.useFakeTimers();
    vi.setSystemTime(new Date('2026-05-20T12:00:00.000Z'));
  });

  afterEach(() => {
    vi.useRealTimers();
  });

  it('shows unlimited active when validUntil is missing', () => {
    render(<LicenseStatusBadge validUntil={null} />);
    expect(screen.getByText('Aktiv (unbegrenzt)')).toBeTruthy();
  });

  it('shows grace period state', () => {
    render(
      <LicenseStatusBadge
        validUntil="2026-05-18T00:00:00Z"
        isInGracePeriod
        daysRemaining={-2}
        gracePeriodRemaining={5}
      />
    );
    expect(screen.getByText('Grace Period (2 Tage überfällig)')).toBeTruthy();
  });

  it('does not treat future ValidUntil horizon as overdue during grace', () => {
    render(
      <LicenseStatusBadge
        validUntil="2029-04-13T00:00:00Z"
        isInGracePeriod
        daysRemaining={997}
        gracePeriodRemaining={5}
      />
    );
    expect(screen.getByText('Grace Period (2 Tage überfällig)')).toBeTruthy();
    expect(screen.queryByText(/997/)).toBeNull();
  });

  it('shows expiring soon from validUntil even if daysRemaining is stale', () => {
    render(<LicenseStatusBadge validUntil="2026-05-30T12:00:00Z" daysRemaining={92} />);
    expect(screen.getByText(/Läuft bald ab \(10 Tage\)/)).toBeTruthy();
  });

  it('shows lockdown state', () => {
    render(<LicenseStatusBadge validUntil="2026-04-01T00:00:00Z" daysRemaining={-60} isLockdown />);
    expect(screen.getByText('Gesperrt')).toBeTruthy();
  });

  it('shows active with remaining days computed from validUntil', () => {
    render(<LicenseStatusBadge validUntil="2026-08-20T12:00:00Z" daysRemaining={10} />);
    expect(screen.getByText(/Aktiv \(92 Tage\)/)).toBeTruthy();
  });
});
