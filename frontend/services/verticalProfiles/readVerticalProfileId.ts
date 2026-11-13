import { apiClient } from '../api/config';
import { secureStorage } from '../secureStorage';
import { tenantStorage } from '../tenant/tenantStorage';
import { verticalProfileCacheKey } from './posOnboarding';

function readProfileId(raw: string | null): string | null {
  if (!raw) return null;
  try {
    const parsed = JSON.parse(raw) as { profileId?: unknown };
    return typeof parsed.profileId === 'string' && parsed.profileId.trim().length > 0
      ? parsed.profileId.trim()
      : null;
  } catch {
    return null;
  }
}

/**
 * Profile id for the post-login route. Cache first, then GET /pos/vertical-profile.
 * Null when the profile is not known yet — callers must not guess a deck.
 */
export async function resolveProfileIdForOnboardingGate(
  scopeHint?: string | null
): Promise<string | null> {
  try {
    const hinted = scopeHint?.trim();
    const scope =
      (hinted && hinted.length > 0 ? hinted : null) ??
      (await tenantStorage.getTenantId()) ??
      (await tenantStorage.getTenantSlug());
    if (scope) {
      const cached = readProfileId(await secureStorage.getItem(verticalProfileCacheKey(scope)));
      if (cached) return cached;
    }

    const fresh = await apiClient.get<{ profileId?: unknown }>('/pos/vertical-profile');
    return typeof fresh?.profileId === 'string' && fresh.profileId.trim().length > 0
      ? fresh.profileId.trim()
      : null;
  } catch {
    return null;
  }
}
