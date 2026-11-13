/**
 * Ambient tenant vertical profile – GET /api/admin/vertical-profile.
 */
import type { UseQueryOptions, UseQueryResult } from '@tanstack/react-query';
import { useQuery } from '@tanstack/react-query';

import { SecondParameter, unwrapData } from '@/api/admin/httpHelpers';
import { customInstance } from '@/lib/axios';

export interface AmbientVerticalProfile {
  profileId: string;
  name: string;
  posFeatures: Record<string, boolean>;
  requiredFields: Record<string, string[]>;
  optionalFields: Record<string, string[]>;
  posLayout: string;
  overrides: Record<string, unknown>;
}

const ADMIN_VERTICAL_PROFILE = '/api/admin/vertical-profile';

export const ambientVerticalProfileQueryKeys = {
  all: ['admin', 'vertical-profile'] as const,
  current: () => [...ambientVerticalProfileQueryKeys.all, 'current'] as const,
};

export function getAmbientVerticalProfile(
  options?: SecondParameter<typeof customInstance>,
  signal?: AbortSignal
): Promise<AmbientVerticalProfile> {
  return customInstance<AmbientVerticalProfile>(
    { url: ADMIN_VERTICAL_PROFILE, method: 'GET', signal },
    options
  ).then((res) => unwrapData<AmbientVerticalProfile>(res));
}

export function useAmbientVerticalProfile(
  enabled = true,
  options?: Partial<UseQueryOptions<AmbientVerticalProfile, Error, AmbientVerticalProfile>>
): UseQueryResult<AmbientVerticalProfile, Error> {
  return useQuery({
    queryKey: ambientVerticalProfileQueryKeys.current(),
    queryFn: ({ signal }) => getAmbientVerticalProfile(undefined, signal),
    enabled,
    ...options,
  });
}
