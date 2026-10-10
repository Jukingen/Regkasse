'use client';

import { Select } from 'antd';
import { useSyncExternalStore } from 'react';

import { isSuperAdmin } from '@/features/auth/constants/roles';
import { useAuth } from '@/features/auth/hooks/useAuth';
import { useTenantContext } from '@/features/tenancy/hooks/useTenantContext';
import {
  SUPER_ADMIN_PROFILE_SIMULATION_ACTUAL,
  SUPER_ADMIN_PROFILE_SIMULATION_ALL,
  readSuperAdminProfileSimulation,
  subscribeSuperAdminProfileSimulation,
  writeSuperAdminProfileSimulation,
} from '@/features/vertical-profiles/superAdminProfileSimulation';
import { VERTICAL_PROFILE_CATALOG } from '@/features/vertical-profiles/verticalProfileCatalog';
import { useI18n } from '@/i18n';
import { getAdminHeaderPopupContainer } from '@/shared/layout/adminHeaderDropdown';

/**
 * Super Admin navigation override. Hidden while a tenant impersonation token is active.
 */
export function SuperAdminProfileSimulationSelect() {
  const { t } = useI18n();
  const { user } = useAuth();
  const { isImpersonating } = useTenantContext();
  const simulation = useSyncExternalStore(
    subscribeSuperAdminProfileSimulation,
    readSuperAdminProfileSimulation,
    () => SUPER_ADMIN_PROFILE_SIMULATION_ALL
  );

  if (!isSuperAdmin(user?.role) || isImpersonating) {
    return null;
  }

  return (
    <Select
      size="small"
      value={simulation}
      aria-label={t('adminShell.header.profileSimulationAria')}
      data-testid="super-admin-profile-simulation"
      popupMatchSelectWidth={false}
      getPopupContainer={getAdminHeaderPopupContainer}
      onChange={(value) => writeSuperAdminProfileSimulation(value)}
      options={[
        {
          value: SUPER_ADMIN_PROFILE_SIMULATION_ALL,
          label: t('adminShell.header.profileSimulationAll'),
        },
        {
          value: SUPER_ADMIN_PROFILE_SIMULATION_ACTUAL,
          label: t('adminShell.header.profileSimulationActual'),
        },
        ...VERTICAL_PROFILE_CATALOG.map((entry) => ({
          value: entry.id,
          label: t(entry.labelKey),
        })),
      ]}
      style={{ minWidth: 180 }}
    />
  );
}
