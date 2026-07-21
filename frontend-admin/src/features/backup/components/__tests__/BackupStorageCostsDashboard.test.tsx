/**
 * @vitest-environment jsdom
 */
import '@testing-library/jest-dom/vitest';
import { render, screen } from '@testing-library/react';
import { App } from 'antd';
import React from 'react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { BackupStorageCostsDashboard } from '@/features/backup/components/BackupStorageCostsDashboard';

const useStorageCostsMock = vi.fn();

vi.mock('@/features/backup/hooks/useStorageCosts', () => ({
  useStorageCosts: (...args: unknown[]) => useStorageCostsMock(...args),
}));

vi.mock('@/i18n', () => ({
  useI18n: () => ({
    t: (key: string) => key,
    formatLocale: 'de-AT',
  }),
}));

describe('BackupStorageCostsDashboard', () => {
  beforeEach(() => {
    useStorageCostsMock.mockReset();
  });

  it('shows capacity warning and tier table when usage is high', () => {
    useStorageCostsMock.mockReturnValue({
      data: {
        totalStorageGb: 9,
        budgetGb: 10,
        usagePercentage: 90,
        monthlyCostEur: 0.2,
        costPerGbEur: 0.022,
        backupCount: 12,
        averageSizeMb: 250,
        retentionSavingsPercent: 18,
        projectedMonthlyEur: 0.23,
        smartRetentionEnabled: false,
        storageTierManagementEnabled: true,
        tiers: [
          {
            name: 'Hot',
            sizeGb: 5,
            costEur: 0.115,
            access: 'fast',
            retention: '≤7d',
            artifactCount: 7,
          },
        ],
        recommendations: [
          {
            code: 'storage_pressure',
            title: 'Storage budget nearly full',
            description: '…',
            savingsPercent: 15,
          },
        ],
      },
      isLoading: false,
      isError: false,
    });

    render(
      <App>
        <BackupStorageCostsDashboard />
      </App>
    );

    expect(screen.getByText('backupDr.costs.capacityWarningTitle')).toBeInTheDocument();
    expect(screen.getByText('backupDr.costs.tiersTitle')).toBeInTheDocument();
    expect(screen.getByText('backupDr.costs.recommendationsTitle')).toBeInTheDocument();
  });
});
