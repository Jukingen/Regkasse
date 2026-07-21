'use client';

import { useMutation, useQueryClient } from '@tanstack/react-query';

import {
  type LicenseTestSnapshot,
  type LicenseTestUpdateRequest,
  applyLicenseTestScenario,
  licenseTestQueryKey,
  updateLicenseTest,
} from '@/features/license/api/licenseTest';
import type { LicenseTestScenarioPreset } from '@/features/license/constants/licenseTestScenarios';
import { invalidateTenantLicenseQueries } from '@/features/license/utils/invalidateTenantLicenseQueries';
import {
  getLicenseTestManualSuccessMessage,
  getLicenseTestScenarioSuccessMessage,
} from '@/features/license/utils/licenseTestMessages';
import { syncLicenseTestSnapshotToCache } from '@/features/license/utils/syncLicenseTestSnapshotToCache';
import { useAntdApp } from '@/hooks/useAntdApp';
import { useI18n } from '@/i18n';

export type { LicenseTestUpdateRequest };

type ApplyScenarioVariables = {
  tenantId: string;
  scenario: LicenseTestScenarioPreset;
};

function applySnapshot(
  queryClient: ReturnType<typeof useQueryClient>,
  tenantId: string,
  snapshot: LicenseTestSnapshot
): void {
  queryClient.setQueryData(licenseTestQueryKey(tenantId), snapshot);
}

export function useUpdateLicenseTest() {
  const { message } = useAntdApp();
  const { t } = useI18n();
  const queryClient = useQueryClient();

  const updateMutation = useMutation<LicenseTestSnapshot, unknown, LicenseTestUpdateRequest>({
    mutationFn: (payload) => updateLicenseTest(payload),
    onSuccess: async (snapshot, variables) => {
      applySnapshot(queryClient, variables.tenantId, snapshot);
      syncLicenseTestSnapshotToCache(queryClient, snapshot);
      await invalidateTenantLicenseQueries(queryClient, variables.tenantId);
      message.success(getLicenseTestManualSuccessMessage(variables.validUntil, t));
    },
    onError: () => message.error(t('license.testPanel.error')),
  });

  const scenarioMutation = useMutation<LicenseTestSnapshot, unknown, ApplyScenarioVariables>({
    mutationFn: ({ tenantId, scenario }) =>
      applyLicenseTestScenario({
        tenantId,
        scope: 'Tenant',
        scenario,
      }),
    onSuccess: async (snapshot, variables) => {
      applySnapshot(queryClient, variables.tenantId, snapshot);
      syncLicenseTestSnapshotToCache(queryClient, snapshot);
      await invalidateTenantLicenseQueries(queryClient, variables.tenantId);
      message.success(getLicenseTestScenarioSuccessMessage(variables.scenario, t));
    },
    onError: () => message.error(t('license.testPanel.error')),
  });

  return {
    updateMutation,
    scenarioMutation,
    isPending: updateMutation.isPending || scenarioMutation.isPending,
  };
}
