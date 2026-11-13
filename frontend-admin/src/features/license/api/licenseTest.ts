import { AXIOS_INSTANCE } from '@/lib/axios';

export type LicenseTestTenantStatus = {
  tenantId: string;
  slug: string;
  name: string;
  licenseKey?: string | null;
  validUntilUtc?: string | null;
  status: string;
  daysRemaining: number;
  daysOverdue: number;
  isActive: boolean;
  isInGracePeriod: boolean;
  canAccess: boolean;
  canTransact: boolean;
  statusMessage: string;
};

export type LicenseTestDeploymentStatus = {
  isValid: boolean;
  isTrial: boolean;
  isExpired: boolean;
  daysRemaining: number;
  expiryDateUtc?: string | null;
  licenseKey?: string | null;
  isDevelopmentBypass: boolean;
  mode: string;
};

export type LicenseTestSnapshot = {
  tenant?: LicenseTestTenantStatus | null;
  deployment: LicenseTestDeploymentStatus;
  developmentModeBypassActive: boolean;
  refreshedAtUtc: string;
};

export type LicenseTestSetExpiryPayload = {
  validUntilUtc?: string | null;
  setExpired?: boolean;
  setActive?: boolean;
};

export type LicenseTestTenantPayload = LicenseTestSetExpiryPayload & {
  tenantId: string;
};

export type LicenseTestScenario = 'Days1' | 'Days7' | 'Days30' | 'Expired';
export type LicenseTestScope = 'Tenant' | 'Deployment' | 'Both';

export type LicenseTestScenarioPayload = {
  tenantId?: string | null;
  scope: LicenseTestScope;
  scenario: LicenseTestScenario;
};

/** POST /api/admin/license/test/update — unified dev-only tenant license update. */
export type LicenseTestUpdateRequest = {
  tenantId: string;
  validUntil: string;
};

export async function fetchLicenseTestSnapshot(
  tenantId?: string | null
): Promise<LicenseTestSnapshot> {
  const { data } = await AXIOS_INSTANCE.get<LicenseTestSnapshot>('/api/admin/license/test', {
    params: tenantId ? { tenantId } : undefined,
  });
  return data;
}

export async function refreshLicenseTestSnapshot(
  tenantId?: string | null
): Promise<LicenseTestSnapshot> {
  const { data } = await AXIOS_INSTANCE.post<LicenseTestSnapshot>(
    '/api/admin/license/test/refresh',
    null,
    { params: tenantId ? { tenantId } : undefined }
  );
  return data;
}

export const licenseTestQueryKey = (tenantId?: string | null) =>
  ['license-test', tenantId ?? 'none'] as const;

export async function updateLicenseTest(
  payload: LicenseTestUpdateRequest
): Promise<LicenseTestSnapshot> {
  const { data } = await AXIOS_INSTANCE.post<LicenseTestSnapshot>(
    '/api/admin/license/test/update',
    payload
  );
  return data;
}

export async function setLicenseTestTenantExpiry(
  payload: LicenseTestTenantPayload
): Promise<LicenseTestSnapshot> {
  const { data } = await AXIOS_INSTANCE.post<LicenseTestSnapshot>(
    '/api/admin/license/test/tenant',
    payload
  );
  return data;
}

export async function setLicenseTestDeploymentExpiry(
  payload: LicenseTestSetExpiryPayload
): Promise<LicenseTestSnapshot> {
  const { data } = await AXIOS_INSTANCE.post<LicenseTestSnapshot>(
    '/api/admin/license/test/deployment',
    payload
  );
  return data;
}

export async function applyLicenseTestScenario(
  payload: LicenseTestScenarioPayload
): Promise<LicenseTestSnapshot> {
  const { data } = await AXIOS_INSTANCE.post<LicenseTestSnapshot>(
    '/api/admin/license/test/scenario',
    payload
  );
  return data;
}
