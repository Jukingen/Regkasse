import { useCallback, useEffect, useState } from 'react';

import { apiClient } from '../services/api/config';
import {
  setDevelopmentModeClientSnapshot,
  type DevelopmentModeSettings,
} from '../services/developmentModeClientCache';

export type { DevelopmentModeSettings } from '../services/developmentModeClientCache';

const POLL_MS = 30_000;

export function useDevelopmentMode() {
  const [settings, setSettings] = useState<DevelopmentModeSettings | null>(null);
  const [isLoading, setIsLoading] = useState(true);

  const refetch = useCallback(async () => {
    try {
      const data = await apiClient.get<DevelopmentModeSettings>('/api/system/development-mode');
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
    void refetch();
    const id = setInterval(() => void refetch(), POLL_MS);
    return () => clearInterval(id);
  }, [refetch]);

  return { settings, isLoading, refetch };
}
