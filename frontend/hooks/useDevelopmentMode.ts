import { useCallback, useEffect, useState } from 'react';

import { useConditionalPolling } from './useConditionalPolling';
import { POS_DEVELOPMENT_MODE_POLL_MS } from '../constants/posPollingIntervals';
import { useAuth } from '../contexts/AuthContext';
import { apiClient } from '../services/api/config';
import {
  setDevelopmentModeClientSnapshot,
  type DevelopmentModeSettings,
} from '../services/developmentModeClientCache';

export type { DevelopmentModeSettings } from '../services/developmentModeClientCache';

export function useDevelopmentMode() {
  const { isAuthenticated, user, isAuthReady } = useAuth();
  const [settings, setSettings] = useState<DevelopmentModeSettings | null>(null);
  const [isLoading, setIsLoading] = useState(true);
  // Public endpoint, but only poll after auth bootstrap and never while password change is required.
  // Skip while unauthenticated to avoid attaching stale JWTs from storage (ASP.NET challenge → 401).
  const pollingEnabled =
    isAuthReady && isAuthenticated && user?.mustChangePasswordOnNextLogin !== true;

  const refetch = useCallback(async () => {
    try {
      const data = await apiClient.get<DevelopmentModeSettings>('/system/development-mode');
      setSettings(data);
      setDevelopmentModeClientSnapshot(data);
    } catch {
      setSettings(null);
      setDevelopmentModeClientSnapshot(null);
    } finally {
      setIsLoading(false);
    }
  }, []);

  useEffect(() => {
    if (!pollingEnabled) {
      setIsLoading(false);
    }
  }, [pollingEnabled]);

  useConditionalPolling(
    () => {
      void refetch();
    },
    POS_DEVELOPMENT_MODE_POLL_MS,
    pollingEnabled
  );

  return { settings, isLoading, refetch };
}
