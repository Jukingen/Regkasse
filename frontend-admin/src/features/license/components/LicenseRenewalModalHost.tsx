'use client';

import { useEffect } from 'react';

import { LicenseRenewalModal } from '@/features/license/components/LicenseRenewalModal';
import { useLicenseRenewalModalStore } from '@/features/license/stores/licenseRenewalModalStore';
import {
  clearLicenseRenewalPending,
  markLicenseRenewalPending,
} from '@/features/license/utils/licenseRenewalRecoveryStorage';
import { useCurrentTenant } from '@/features/tenancy/hooks/useCurrentTenant';
import { useAuthorizationGate } from '@/hooks/useAuthorizedQuery';
import { useLicenseStatus } from '@/hooks/useLicenseStatus';
import { PERMISSIONS } from '@/shared/auth/permissions';

/**
 * Hosts {@link LicenseRenewalModal} for axios / guard-triggered open requests.
 * Mount once in the protected FA shell. Marks localStorage recovery pending while open.
 */
export function LicenseRenewalModalHost() {
  const open = useLicenseRenewalModalStore((s) => s.open);
  const closeModal = useLicenseRenewalModalStore((s) => s.closeModal);
  const tenant = useCurrentTenant();
  const { status } = useLicenseStatus();
  const { isAuthorized: canExtend } = useAuthorizationGate({
    requiredPermission: PERMISSIONS.LICENSE_MANAGE,
  });

  useEffect(() => {
    // Only track unfinished renewal for licenses that already need action.
    // Proactive renewals while Active must not leave a "Fortsetzen" recovery banner.
    if (open && tenant.tenantId && status && status.state !== 'Active') {
      markLicenseRenewalPending(tenant.tenantId);
    }
  }, [open, tenant.tenantId, status]);

  if (!canExtend || !tenant.tenantId) {
    return null;
  }

  return (
    <LicenseRenewalModal
      open={open}
      tenantId={tenant.tenantId}
      status={status}
      onClose={closeModal}
      onSuccess={() => {
        clearLicenseRenewalPending(tenant.tenantId);
      }}
    />
  );
}
