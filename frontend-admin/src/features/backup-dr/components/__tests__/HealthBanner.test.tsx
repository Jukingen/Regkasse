/**
 * Banner önceliği: kritik/uyarı varken bilgi notları gizlenir (gürültü azaltma).
 */

import React from 'react';
import '@testing-library/jest-dom';
import { describe, expect, it } from 'vitest';
import { render, screen } from '@testing-library/react';
import { HealthBanner } from '@/features/backup-dr/components/HealthBanner';

const t = (k: string) => k;

describe('HealthBanner — severity dominates info', () => {
  it('does not render informational alert when critical items exist', () => {
    render(
      <HealthBanner critical={['x']} warn={[]} info={['stub note']} t={t} />,
    );
    expect(screen.queryByText('backupDr.banner.informationalTitle')).not.toBeInTheDocument();
    expect(screen.getByText('backupDr.banner.criticalTitle')).toBeInTheDocument();
  });

  it('does not render informational alert when warn items exist', () => {
    render(
      <HealthBanner critical={[]} warn={['y']} info={['stub note']} t={t} />,
    );
    expect(screen.queryByText('backupDr.banner.informationalTitle')).not.toBeInTheDocument();
  });

  it('renders informational alert when only info items exist', () => {
    render(<HealthBanner critical={[]} warn={[]} info={['stub note']} t={t} />);
    expect(screen.getByText('backupDr.banner.informationalTitle')).toBeInTheDocument();
  });
});
