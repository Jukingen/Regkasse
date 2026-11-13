/**
 * Banner önceliği: yalnızca kritik varken bilgi notları gizlenir; uyarı + bilgi birlikte gösterilebilir (stub açıklamaları kaybolmasın).
 */
import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import React from 'react';
import { describe, expect, it } from 'vitest';

import { HealthBanner } from '@/features/backup-dr/components/HealthBanner';

const t = (k: string) => k;

describe('HealthBanner — severity dominates info', () => {
  it('does not render informational alert when critical items exist', () => {
    render(<HealthBanner critical={['x']} warn={[]} info={['stub note']} t={t} />);
    expect(screen.queryByText('backupDr.banner.informationalTitle')).not.toBeInTheDocument();
    expect(screen.getByText('backupDr.banner.criticalTitle')).toBeInTheDocument();
  });

  it('still renders informational alert when warn items exist but no critical', () => {
    render(<HealthBanner critical={[]} warn={['y']} info={['stub note']} t={t} />);
    expect(screen.getByText('backupDr.banner.informationalTitle')).toBeInTheDocument();
    expect(screen.getByText('stub note')).toBeInTheDocument();
  });

  it('renders informational alert when only info items exist', () => {
    render(<HealthBanner critical={[]} warn={[]} info={['stub note']} t={t} />);
    expect(screen.getByText('backupDr.banner.informationalTitle')).toBeInTheDocument();
  });
});
