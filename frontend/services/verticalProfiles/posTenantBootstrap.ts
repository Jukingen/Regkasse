/**
 * Persist a deep-linked tenant slug so the next POS API call and the login
 * screen use that mandant. Does not touch the payment flow.
 */
import { API_PATHS } from '../api/apiPaths';
import { apiClient } from '../api/config';
import { normalizeCustomerTenantSlug } from '../customerApp/customerTenantSlug';
import { setDevTenantSlugOverride } from '../tenant/devTenant';
import { tenantStorage } from '../tenant/tenantStorage';

export type PublicPosTenantProfile = {
  slug: string;
  displayName: string | null;
  verticalProfileId: string | null;
};

export async function bootstrapPosTenantSlug(slug: string): Promise<string | null> {
  const normalized = normalizeCustomerTenantSlug(slug);
  if (!normalized) return null;

  await tenantStorage.persistBootstrap({ tenantSlug: normalized });
  await setDevTenantSlugOverride(normalized);
  return normalized;
}

/** Anonymous public profile. Used on the login screen before a POS session exists. */
export async function loadPublicPosTenantProfile(
  slug: string
): Promise<PublicPosTenantProfile | null> {
  const normalized = normalizeCustomerTenantSlug(slug);
  if (!normalized) return null;

  try {
    const data = await apiClient.get<{
      slug?: string;
      displayName?: string | null;
      verticalProfileId?: string | null;
    }>(API_PATHS.PUBLIC_TENANTS.BY_SLUG(normalized));

    const profileId = data.verticalProfileId?.trim();
    return {
      slug: normalizeCustomerTenantSlug(data.slug) ?? normalized,
      displayName: data.displayName?.trim() || null,
      verticalProfileId: profileId && profileId.length > 0 ? profileId : null,
    };
  } catch {
    return null;
  }
}
