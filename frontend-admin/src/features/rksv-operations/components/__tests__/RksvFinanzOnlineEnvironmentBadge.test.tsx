import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { RksvFinanzOnlineEnvironmentBadge } from '@/features/rksv-operations/components/RksvFinanzOnlineEnvironmentStatus';
import { RksvPublicEnvironmentState } from '@/shared/config/rksvEnvironment';

vi.mock('@/i18n/I18nProvider', () => ({
  useI18n: () => ({
    t: (key: string) => {
      if (key === 'rksvHub.env.displayLabel.test') return 'TEST';
      if (key === 'rksvHub.env.releaseStage.displayLabel.dev') return 'DEVELOPMENT';
      if (key === 'rksvHub.env.releaseStage.displayLabel.staging') return 'STAGING';
      if (key === 'rksvHub.env.buildTimeBadgeTooltip') return 'build-time';
      if (key === 'rksvHub.env.releaseStageBadgeTooltip') return 'release-stage';
      return key;
    },
  }),
}));

vi.mock('@/shared/config/rksvEnvironment', async (importOriginal) => {
  const actual = await importOriginal<typeof import('@/shared/config/rksvEnvironment')>();
  return {
    ...actual,
    getReleaseStageFromConfig: () => 'staging' as const,
  };
});

describe('RksvFinanzOnlineEnvironmentBadge', () => {
  it('shows RKSV env badge next to release stage badge', () => {
    const { container } = render(
      <RksvFinanzOnlineEnvironmentBadge parsed={{ state: RksvPublicEnvironmentState.TEST }} />
    );

    expect(screen.getByText('TEST')).toBeInTheDocument();
    expect(screen.getByText('STAGING')).toBeInTheDocument();
    expect(container.querySelector('[data-rksv-environment-state="TEST"]')).not.toBeNull();
    expect(container.querySelector('[data-regkasse-release-stage="staging"]')).not.toBeNull();
  });
});
