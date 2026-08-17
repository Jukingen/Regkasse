import type { DemoImportProductOverride } from '@/api/admin/products';
import { getApiAdminTenantsTenantIdDeleteDependencies } from '@/api/generated/admin/admin';
import type { TenantDeleteDependenciesDto } from '@/api/generated/model';
import { authStorage } from '@/features/auth/services/authStorage';
import { writeDevTenantSlug } from '@/features/auth/services/devTenant';
import { beginTenantSwitch } from '@/features/auth/services/tenantSwitchController';
import {
  TenantPermanentDeleteBlockedError,
  parseTenantPermanentDeleteError,
} from '@/features/super-admin/utils/parseTenantPermanentDeleteError';
import { buildTenantSubdomainOrigin } from '@/lib/auth/impersonationHandoff';
import {
  buildImpersonationRedirectUrl,
  shouldUseProductionImpersonationRedirect,
} from '@/lib/auth/tokenHandler';
import { AXIOS_INSTANCE } from '@/lib/axios';

export type AdminTenantListItem = {
  id: string;
  name: string;
  slug: string;
  email?: string | null;
  phone?: string | null;
  status: string;
  isActive: boolean;
  licenseKey?: string | null;
  licenseValidUntilUtc?: string | null;
  /** Server-computed; same day math as POS mandant license status. */
  licenseDaysRemaining?: number | null;
  createdAt: string;
  updatedAt?: string | null;
  ownerAdminEmail?: string | null;
  isDemoPreset?: boolean;
  /**
   * Package tier from license_sales (Trial | Starter | Business | Plus).
   * Tenants without an active sale default to Trial.
   */
  licenseType?: 'Trial' | 'Starter' | 'Business' | 'Plus' | null;
  registerCount?: number;
  userCount?: number;
  lastActivityAtUtc?: string | null;
  trialStatus?: string | null;
  trialEndsAtUtc?: string | null;
  trialDaysRemaining?: number | null;
};

export type TenantProvisioning = {
  cashRegisterId: string;
  cashRegisterNumber: string;
  adminUserId: string;
  adminEmail: string;
  generatedPassword: string;
  categoryId: string;
  productIds: string[];
  trialLicenseValidUntilUtc?: string | null;
  welcomeEmailSent?: boolean;
  forcePasswordChangeOnNextLogin?: boolean;
};

/** Public tenant FA URL for onboarding hints (https://{slug}.regkasse.at). */
export function buildTenantPortalUrl(slug: string): string {
  return buildTenantSubdomainOrigin(slug);
}

/** Copy-paste block for handing off provisioned admin credentials (German operator text). */
export function formatTenantProvisioningHandoff(
  tenantName: string,
  slug: string,
  provisioning: TenantProvisioning,
  contactEmail?: string
): string {
  const portalUrl = buildTenantPortalUrl(slug);
  const notifyEmail = contactEmail?.trim() || provisioning.adminEmail;
  return [
    `Kunde "${tenantName}" wurde erfolgreich angelegt!`,
    '',
    'Zugangsdaten:',
    `Admin E-Mail: ${provisioning.adminEmail}`,
    `Passwort: ${provisioning.generatedPassword}`,
    '',
    'Erste Schritte:',
    `1. Kunde einloggen: ${portalUrl}`,
    '2. Passwort ändern',
    '3. Produkte anpassen',
    '4. Kasse mit Drucker verbinden',
    '5. Test-Transaktion durchführen',
    '',
    `E-Mail mit Zugangsdaten: ${notifyEmail}`,
  ].join('\n');
}

export type TenantOperationMode = 'active' | 'readonly' | 'maintenance';

export type AdminTenantDetail = AdminTenantListItem & {
  address?: string | null;
  deletedAtUtc?: string | null;
  activeUserCount?: number;
  cashRegisterCount?: number;
  lastActivityAtUtc?: string | null;
  provisioning?: TenantProvisioning | null;
  operationMode?: TenantOperationMode | string;
  maintenanceMessage?: string | null;
  maintenanceStartedAt?: string | null;
  maintenanceEndsAt?: string | null;
  trialStartedAtUtc?: string | null;
  trialGracePeriodEndsAtUtc?: string | null;
  trialConvertedAtUtc?: string | null;
  trialReminder7dSent?: boolean;
  trialReminder3dSent?: boolean;
  trialReminder1dSent?: boolean;
};

export type UpdateTenantOperationModeRequest = {
  operationMode: TenantOperationMode;
  maintenanceMessage?: string | null;
  maintenanceStartedAt?: string | null;
  maintenanceEndsAt?: string | null;
};

export type TenantSlugAvailability = {
  normalizedSlug: string;
  isValid: boolean;
  available: boolean;
};

export type CreateAdminTenantRequest = {
  name: string;
  slug: string;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
  licenseKey?: string | null;
  licenseValidUntilUtc?: string | null;
  adminEmail?: string | null;
  adminPassword?: string | null;
  grantTrialLicense?: boolean;
  /** Override trial length (14 / 30 / 60 / 90). Backend default is Trial:DefaultDurationDays. */
  trialDurationDays?: number;
  /** When true, imports full demo menu (Salate, Pizzas, …) instead of three generic demo products. */
  importDemoMenu?: boolean;
  /** Optional cash register number (default KASSE-001). */
  cashRegisterNumber?: string | null;
  /** Industry permission template id (restaurant | retail | hotel | none). */
  industryTemplateId?: string | null;
  /** When true, seed starter users for industry template slots. */
  seedIndustryStarterUsers?: boolean;
};

export type UpdateAdminTenantRequest = {
  name?: string | null;
  email?: string | null;
  phone?: string | null;
  address?: string | null;
  status?: string | null;
  licenseKey?: string | null;
  licenseValidUntilUtc?: string | null;
  isActive?: boolean | null;
};

export type TenantImpersonationResponse = {
  token: string;
  expiresIn: number;
  refreshToken?: string | null;
  refreshTokenExpiresAtUtc?: string | null;
  tenantId: string;
  tenantSlug: string;
  tenantDisplayName?: string | null;
  impersonation: boolean;
};

export async function checkAdminTenantSlugAvailability(
  slug: string
): Promise<TenantSlugAvailability> {
  const { data } = await AXIOS_INSTANCE.get<TenantSlugAvailability>(
    '/api/admin/tenants/slug-availability',
    {
      params: { slug },
    }
  );
  return data;
}

export async function getAdminTenantSlugSuggestions(
  companyName?: string,
  preferredSlug?: string,
  max = 5
): Promise<string[]> {
  const { data } = await AXIOS_INSTANCE.get<{ suggestions: string[] }>(
    '/api/admin/tenants/slug-suggestions',
    {
      params: { name: companyName, slug: preferredSlug, max },
    }
  );
  return data.suggestions ?? [];
}

export type AdminTenantListQuery = {
  includeDeleted?: boolean;
  status?: string;
  licenseType?: 'Trial' | 'Starter' | 'Business' | 'Plus';
  search?: string;
  sortBy?: string;
  sortOrder?: 'asc' | 'desc' | 'Asc' | 'Desc';
  page?: number;
  pageSize?: number;
};

export type AdminTenantListPagedResult = {
  items: AdminTenantListItem[];
  totalCount: number;
  page: number;
  pageSize: number;
  totalPages: number;
};

export async function listAdminTenantsPaged(
  query: AdminTenantListQuery = {}
): Promise<AdminTenantListPagedResult> {
  const { data } = await AXIOS_INSTANCE.get<AdminTenantListPagedResult>('/api/admin/tenants', {
    params: {
      includeDeleted: query.includeDeleted ?? false,
      status: query.status,
      licenseType: query.licenseType,
      search: query.search,
      sortBy: query.sortBy,
      sortOrder: query.sortOrder,
      page: query.page ?? 1,
      pageSize: query.pageSize ?? 20,
    },
  });
  return data;
}

/** Backward-compatible full list (fetches all pages, max 100 per request). */
export async function listAdminTenants(includeDeleted = false): Promise<AdminTenantListItem[]> {
  const pageSize = 100;
  let page = 1;
  let totalPages = 1;
  const all: AdminTenantListItem[] = [];

  do {
    const result = await listAdminTenantsPaged({
      includeDeleted,
      page,
      pageSize,
      sortBy: 'Name',
      sortOrder: 'Asc',
    });
    all.push(...(result.items ?? []));
    totalPages = Math.max(1, result.totalPages ?? 1);
    page += 1;
  } while (page <= totalPages);

  return all;
}

export async function getAdminTenantById(tenantId: string): Promise<AdminTenantDetail> {
  const { data } = await AXIOS_INSTANCE.get<AdminTenantDetail>(`/api/admin/tenants/${tenantId}`);
  return data;
}

export async function createAdminTenant(
  body: CreateAdminTenantRequest
): Promise<AdminTenantDetail> {
  const { data } = await AXIOS_INSTANCE.post<AdminTenantDetail>('/api/admin/tenants', body);
  return data;
}

export async function updateAdminTenant(
  tenantId: string,
  body: UpdateAdminTenantRequest
): Promise<AdminTenantDetail> {
  const { data } = await AXIOS_INSTANCE.put<AdminTenantDetail>(
    `/api/admin/tenants/${tenantId}`,
    body
  );
  return data;
}

/** Super Admin: set lifecycle status (lead | in_onboarding | active | suspended | cancelled | archived). */
export async function updateTenantStatus(
  tenantId: string,
  status: string
): Promise<AdminTenantDetail> {
  return updateAdminTenant(tenantId, { status });
}

export async function exportTenantsCsv(query: AdminTenantListQuery = {}): Promise<Blob> {
  const { data } = await AXIOS_INSTANCE.get<Blob>('/api/admin/tenants/export', {
    params: {
      includeDeleted: query.includeDeleted ?? false,
      status: query.status,
      licenseType: query.licenseType,
      search: query.search,
      sortBy: query.sortBy,
      sortOrder: query.sortOrder,
    },
    responseType: 'blob',
  });
  return data;
}

export type PlanDistributionDto = {
  trial: number;
  starter: number;
  business: number;
  plus: number;
};

export type CustomerAnalyticsDto = {
  totalTenants: number;
  activeTenants: number;
  inOnboardingTenants: number;
  suspendedTenants: number;
  trialTenants: number;
  paidTenants: number;
  expiringSoon: number;
  expiredTenants: number;
  mrr: number;
  newTenantsLast30Days: number;
  churnRate: number;
  arpu: number;
  planDistribution: PlanDistributionDto;
  customerLtv: number | null;
};

export async function getCustomerAnalytics(): Promise<CustomerAnalyticsDto> {
  const { data } = await AXIOS_INSTANCE.get<CustomerAnalyticsDto>('/api/admin/analytics/customers');
  return data;
}

export async function updateTenantOperationMode(
  tenantId: string,
  body: UpdateTenantOperationModeRequest
): Promise<AdminTenantDetail> {
  const { data } = await AXIOS_INSTANCE.patch<AdminTenantDetail>(
    `/api/admin/tenants/${tenantId}/operation-mode`,
    body
  );
  return data;
}

/** Soft-delete tenant (status=deleted, memberships deactivated). */
export async function softDeleteAdminTenant(tenantId: string): Promise<void> {
  await AXIOS_INSTANCE.delete(`/api/admin/tenants/${tenantId}`);
}

/** @deprecated Use {@link softDeleteAdminTenant}. */
export const deleteAdminTenant = softDeleteAdminTenant;

export async function restoreAdminTenant(tenantId: string): Promise<void> {
  await AXIOS_INSTANCE.post(`/api/admin/tenants/${tenantId}/restore`);
}

/** Read-only dependency summary for permanent delete UI. */
export async function fetchTenantDeleteDependencies(
  tenantId: string
): Promise<TenantDeleteDependenciesDto> {
  return getApiAdminTenantsTenantIdDeleteDependencies(tenantId);
}

/** Permanent delete; requires prior soft-delete and matching {@link confirmSlug}. */
export async function hardDeleteAdminTenant(tenantId: string, confirmSlug: string): Promise<void> {
  try {
    await AXIOS_INSTANCE.delete(`/api/admin/tenants/${tenantId}/permanent`, {
      data: { confirmSlug },
    });
  } catch (error) {
    const structured = parseTenantPermanentDeleteError(error);
    if (structured && (structured.dependencies || structured.code)) {
      throw new TenantPermanentDeleteBlockedError(structured);
    }
    throw error;
  }
}

/** Development-only shortcut: immediately soft-deletes and permanently deletes the tenant server-side. */
export async function hardDeleteAdminTenantDevelopment(tenantId: string): Promise<void> {
  await AXIOS_INSTANCE.delete(`/api/admin/tenants/${tenantId}/hard`);
}

export async function impersonateAdminTenant(
  tenantId: string
): Promise<TenantImpersonationResponse> {
  const { data } = await AXIOS_INSTANCE.post<TenantImpersonationResponse>(
    `/api/admin/tenants/${tenantId}/impersonate`
  );
  return data;
}

export type DemoProductImportResult = {
  success: boolean;
  created: number;
  updated: number;
  skipped: number;
  selectedCategoryCount?: number;
  totalProductCount?: number;
  categorySummaries?: Array<{
    categoryName: string;
    productCount: number;
    created: number;
    skipped: number;
  }>;
  errorMessage?: string | null;
  categoryIds?: string[];
  productIds?: string[];
};

export type DemoImportRequest = {
  overwriteExisting?: boolean;
  selectedCategories?: string[];
  excludedCategories?: string[];
  selectedProductIds?: string[];
  priceAdjustmentMode?: string;
  priceAdjustmentPercent?: number;
  priceRoundIncrement?: number;
  imageMode?: string;
  productOverrides?: DemoImportProductOverride[];
};

/** Super-admin: import demo menu into a specific tenant. */
export async function importDemoProductsForTenant(
  tenantId: string,
  request: DemoImportRequest = {}
): Promise<DemoProductImportResult> {
  const { data } = await AXIOS_INSTANCE.post<DemoProductImportResult>(
    `/api/admin/tenants/${tenantId}/demo-products/import`,
    request
  );
  return data;
}

/**
 * Applies impersonation session.
 * Production: redirect to tenant subdomain with JWT in URL fragment (not stored on admin host).
 * Development: store token + dev tenant override on same origin and reload.
 */
export function applyTenantImpersonationSession(res: TenantImpersonationResponse): void {
  if (typeof window === 'undefined') {
    return;
  }

  beginTenantSwitch();

  if (shouldUseProductionImpersonationRedirect()) {
    window.location.assign(buildImpersonationRedirectUrl(res));
    return;
  }

  authStorage.setToken(res.token);
  if (res.refreshToken) {
    authStorage.setRefreshToken(res.refreshToken);
  }
  if (!writeDevTenantSlug(res.tenantSlug)) {
    window.location.reload();
  }
}

export { TenantPermanentDeleteBlockedError } from '@/features/super-admin/utils/parseTenantPermanentDeleteError';
