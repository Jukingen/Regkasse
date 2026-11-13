'use client';

/**
 * Binds auth user id + ephemeral session id into ambient structured log context.
 * Does not log tokens or passwords.
 */

import { useEffect } from 'react';

import { useAuth } from '@/features/auth/hooks/useAuth';
import { setLogContext } from '@/lib/logger';
import { getOrCreateClientSessionId } from '@/lib/logging/logContext';

export function LogContextBinder() {
  const { user, isAuthenticated } = useAuth();

  useEffect(() => {
    const sessionId = getOrCreateClientSessionId();
    setLogContext({
      sessionId,
      userId: isAuthenticated && user?.id ? user.id : null,
      tenantId: isAuthenticated && user?.tenantId ? user.tenantId : null,
    });
  }, [isAuthenticated, user?.id, user?.tenantId]);

  return null;
}
