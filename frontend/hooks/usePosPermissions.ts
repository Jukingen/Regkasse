import { useMemo } from 'react';

import { useAuth } from '../contexts/AuthContext';
import {
  resolvePosPermissions,
  type PosPermissions,
} from '../utils/posPermissions';

/**
 * POS operation flags from the signed-in user's JWT permission claims.
 * SuperAdmin (role or compact `system.critical` token) is allowed for every flag.
 */
export function usePosPermissions(): PosPermissions {
  const { user } = useAuth();

  return useMemo(
    () => resolvePosPermissions(user),
    [user, user?.role, user?.roles, user?.permissions]
  );
}

export type { PosPermissions };
