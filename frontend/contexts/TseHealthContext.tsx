/**
 * Polls TSE health for POS banner + offline queue counter (German UI elsewhere).
 */
import React, {
  createContext,
  useCallback,
  useContext,
  useEffect,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Alert } from 'react-native';

import { fetchTseHealth, type TseHealthApiResponse, type TseOperationalHealthStatus } from '../services/api/tseHealthApi';
import { getUserSettings } from '../services/api/userSettingsService';
import { isDevSimulateTseUnavailable } from '../constants/devSimulatePosOffline';

export type TseBannerVariant = 'online' | 'slow' | 'offline';

export interface TseHealthContextValue {
  /** Raw backend status string */
  status: TseOperationalHealthStatus | string;
  /** Normalized banner colors/messages */
  bannerVariant: TseBannerVariant;
  /** Last GET /api/tse/health round-trip time (ms) */
  lastLatencyMs: number | null;
  pendingOfflineQueueCount: number | null;
  estimatedRecoveryTimeUtc: string | null;
  lastErrorMessageSafe: string | null;
  loading: boolean;
  refresh: () => Promise<void>;
}

const TseHealthContext = createContext<TseHealthContextValue | null>(null);

const POLL_MS = 10_000;
const SLOW_MS = 3000;

function normalizeBannerVariant(
  apiStatus: string,
  latencyMs: number | null
): TseBannerVariant {
  const s = (apiStatus || '').trim();
  if (s === 'Offline') return 'offline';
  if (s === 'Degraded') return 'slow';
  if (s === 'Online' && latencyMs != null && latencyMs > SLOW_MS) return 'slow';
  return 'online';
}

export function TseHealthProvider({ children }: { children: React.ReactNode }) {
  const [payload, setPayload] = useState<TseHealthApiResponse | null>(null);
  const [lastLatencyMs, setLastLatencyMs] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const prevQueueRef = useRef<number | null>(null);

  const cashRegisterIdRef = useRef<string | null>(null);

  const loadRegisterId = useCallback(async () => {
    try {
      const s = await getUserSettings();
      const id = s.cashRegisterId?.trim();
      cashRegisterIdRef.current =
        id && id !== '00000000-0000-0000-0000-000000000000' ? id : null;
    } catch {
      cashRegisterIdRef.current = null;
    }
  }, []);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      await loadRegisterId();
      const { body, latencyMs } = await fetchTseHealth(cashRegisterIdRef.current);
      setPayload(body);
      setLastLatencyMs(latencyMs);

      const q =
        typeof body.nonFiscalPendingQueueCount === 'number'
          ? body.nonFiscalPendingQueueCount
          : null;
      const prev = prevQueueRef.current;
      if (
        prev != null &&
        q != null &&
        prev > 0 &&
        q < prev
      ) {
        Alert.alert(
          'TSE',
          'Ausstehende Offline-Zahlungen wurden signiert oder aktualisiert.'
        );
      }
      prevQueueRef.current = q;
    } catch {
      setPayload(null);
      setLastLatencyMs(null);
    } finally {
      setLoading(false);
    }
  }, [loadRegisterId]);

  useEffect(() => {
    void refresh();
    const id = setInterval(() => void refresh(), POLL_MS);
    return () => clearInterval(id);
  }, [refresh]);

  const value = useMemo<TseHealthContextValue>(() => {
    const rawStatus = (payload?.status ?? 'Degraded') as TseOperationalHealthStatus | string;
    const status = isDevSimulateTseUnavailable() ? 'Offline' : rawStatus;
    const lat = lastLatencyMs;
    const bannerVariant = normalizeBannerVariant(String(status), lat);
    return {
      status,
      bannerVariant,
      lastLatencyMs: lat,
      pendingOfflineQueueCount:
        typeof payload?.nonFiscalPendingQueueCount === 'number'
          ? payload.nonFiscalPendingQueueCount
          : null,
      estimatedRecoveryTimeUtc: payload?.estimatedRecoveryTimeUtc ?? null,
      lastErrorMessageSafe: isDevSimulateTseUnavailable()
        ? 'Entwicklungssimulation: TSE wird als offline behandelt.'
        : payload?.lastErrorMessageSafe ?? null,
      loading,
      refresh,
    };
  }, [payload, lastLatencyMs, loading, refresh]);

  return <TseHealthContext.Provider value={value}>{children}</TseHealthContext.Provider>;
}

export function useTseHealth(): TseHealthContextValue {
  const ctx = useContext(TseHealthContext);
  if (!ctx) {
    return {
      status: 'Online',
      bannerVariant: 'online',
      lastLatencyMs: null,
      pendingOfflineQueueCount: null,
      estimatedRecoveryTimeUtc: null,
      lastErrorMessageSafe: null,
      loading: false,
      refresh: async () => {},
    };
  }
  return ctx;
}
