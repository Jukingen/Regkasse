'use client';

import { useAuthorizedQuery } from '@/hooks/useAuthorizedQuery';
import {
  fetchUserPreferences,
  userPreferencesQueryKey,
} from '@/lib/personalization/userPreferencesApi';

/** GET /api/admin/user/preferences — any authenticated admin user (own row only). */
export const useUserPreferences = () => {
  return useAuthorizedQuery({
    queryKey: userPreferencesQueryKey,
    queryFn: fetchUserPreferences,
    requiredRole: [],
    staleTime: 60_000,
  });
};
