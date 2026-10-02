import { customInstance } from '@/lib/axios';

export type VerticalProfileSource = 'seed' | 'override' | 'custom';

export type VerticalProfileRecord = {
  id: string;
  name: string;
  posFeatures: Record<string, boolean>;
  requiredFields: Record<string, string[]>;
  optionalFields: Record<string, string[]>;
  posLayout: string;
  featureCount: number;
  tenantCount: number;
  source: VerticalProfileSource | string;
};

export type VerticalProfileTenantSummary = {
  id: string;
  name: string;
  slug: string;
  overridesCount: number;
};

export type VerticalProfileTenantGroup = {
  profileId: string;
  name: string;
  source: string;
  tenantCount: number;
  tenants: VerticalProfileTenantSummary[];
};

export type VerticalProfileMutationResult = {
  profile: VerticalProfileRecord;
  removedFeatures: string[];
  affectedTenants: VerticalProfileTenantSummary[];
};

export type UpsertVerticalProfileBody = {
  id?: string;
  name?: string;
  cloneFrom?: string;
  posFeatures?: Record<string, boolean>;
  requiredFields?: Record<string, string[]>;
  optionalFields?: Record<string, string[]>;
  posLayout?: string;
};

const profiles = '/api/admin/vertical-profiles';

export function listVerticalProfiles(): Promise<VerticalProfileRecord[]> {
  return customInstance<VerticalProfileRecord[]>({ url: profiles, method: 'GET' });
}

export function createVerticalProfile(
  body: UpsertVerticalProfileBody
): Promise<VerticalProfileMutationResult> {
  return customInstance<VerticalProfileMutationResult>({
    url: profiles,
    method: 'POST',
    data: body,
  });
}

export function updateVerticalProfile(
  id: string,
  body: UpsertVerticalProfileBody
): Promise<VerticalProfileMutationResult> {
  return customInstance<VerticalProfileMutationResult>({
    url: `${profiles}/${encodeURIComponent(id)}`,
    method: 'PATCH',
    data: body,
  });
}

export function updateVerticalProfileFeatures(
  id: string,
  posFeatures: Record<string, boolean>
): Promise<VerticalProfileMutationResult> {
  return customInstance<VerticalProfileMutationResult>({
    url: `${profiles}/${encodeURIComponent(id)}/features`,
    method: 'PUT',
    data: { posFeatures },
  });
}

export function cloneVerticalProfile(
  id: string,
  body: { id: string; name?: string }
): Promise<VerticalProfileMutationResult> {
  return customInstance<VerticalProfileMutationResult>({
    url: `${profiles}/${encodeURIComponent(id)}/clone`,
    method: 'POST',
    data: body,
  });
}

export function deleteVerticalProfile(id: string): Promise<void> {
  return customInstance<void>({
    url: `${profiles}/${encodeURIComponent(id)}`,
    method: 'DELETE',
  });
}

export function listVerticalProfileTenants(
  id: string
): Promise<VerticalProfileTenantSummary[]> {
  return customInstance<VerticalProfileTenantSummary[]>({
    url: `${profiles}/${encodeURIComponent(id)}/tenants`,
    method: 'GET',
  });
}

export function listTenantsByVerticalProfile(): Promise<VerticalProfileTenantGroup[]> {
  return customInstance<VerticalProfileTenantGroup[]>({
    url: '/api/admin/tenants/by-profile',
    method: 'GET',
  });
}
