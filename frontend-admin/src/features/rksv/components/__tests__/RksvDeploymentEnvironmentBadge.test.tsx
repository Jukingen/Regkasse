import '@testing-library/jest-dom';
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { RksvDeploymentEnvironmentBadge } from '@/features/rksv/components/RksvDeploymentEnvironmentStatus';

vi.mock('@/features/rksv/hooks/useRksvBackendEnvironment', () => ({
  useRksvStatus: () => ({
    data: null,
    isLoading: false,
    isDemo: false,
    isError: false,
  }),
}));

vi.mock('@/i18n/I18nProvider', () => ({
  useI18n: () => ({
    t: (key: string) => {
      if (key === 'rksvHub.env.backend.displayLabel.demo') return '🧪 DEMO';
      if (key === 'rksvHub.env.backend.displayLabel.production') return '🚀 Production';
      return key;
    },
  }),
}));

describe('RksvDeploymentEnvironmentBadge', () => {
  it('shows DEMO when isSimulated is true', () => {
    const { container } = render(
      <RksvDeploymentEnvironmentBadge
        status={{
          environment: 'Production',
          isSimulated: true,
          showDemoLabel: true,
          tseStatusDisplay: 'TSE: SIMULIERT (NUR TEST)',
          tseStatusBadge: 'TSE SIMULIERT',
          environmentDisplayName: '🧪 DEMO / TEST',
        }}
        isDemo
      />,
    );

    expect(screen.getByText('🧪 DEMO')).toBeInTheDocument();
    expect(container.querySelector('[data-rksv-deployment-simulated="true"]')).not.toBeNull();
  });

  it('shows Production when isSimulated is false', () => {
    const { container } = render(
      <RksvDeploymentEnvironmentBadge
        status={{
          environment: 'Production',
          isSimulated: false,
          showDemoLabel: false,
          tseStatusDisplay: 'TSE: AKTIV ✅',
          tseStatusBadge: 'TSE AKTIV',
          environmentDisplayName: '🚀 PRODUCTION',
        }}
        isDemo={false}
      />,
    );

    expect(screen.getByText('🚀 Production')).toBeInTheDocument();
    expect(container.querySelector('[data-rksv-deployment-simulated="false"]')).not.toBeNull();
  });
});
