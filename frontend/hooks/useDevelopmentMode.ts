import { useCallback, useState } from 'react';

import { POS_DEVELOPMENT_MODE_POLL_MS } from '../constants/posPollingIntervals';
import { useConditionalPolling } from './useConditionalPolling';
import { apiClient } from '../services/api/config';
import {
  setDevelopmentModeClientSnapshot,
  type DevelopmentModeSettings,
} from '../services/developmentModeClientCache';

export type { DevelopmentModeSettings } from '../services/developmentModeClientCache';

export function useDevelopmentMode() {
  const [settings, setSettings] = useState<DevelopmentModeSettings | null>(null);
  const [isLoading, setIsLoading] = useState(true);

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

  useConditionalPolling(() => {
    void refetch();
  }, POS_DEVELOPMENT_MODE_POLL_MS);

  return { settings, isLoading, refetch };
}
