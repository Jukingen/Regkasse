/**
 * Polls TSE health for POS banner + offline queue counter (German UI elsewhere).
 */
import React, {
  createContext,
  useCallback,
  useContext,
  useMemo,
  useRef,
  useState,
} from 'react';
import { Alert } from 'react-native';

import { POS_TSE_HEALTH_POLL_MS } from '../constants/posPollingIntervals';
import { useConditionalPolling } from '../hooks/useConditionalPolling';
import { fetchTseHealth, type TseHealthApiResponse, type TseOperationalHealthStatus } from '../services/api/tseHealthApi';
import { isDevSimulateTseUnavailable } from '../constants/devSimulatePosOffline';
import { usePosRegisterReadiness } from './PosRegisterReadinessContext';

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
  const posReadiness = usePosRegisterReadiness();
  const [payload, setPayload] = useState<TseHealthApiResponse | null>(null);
  const [lastLatencyMs, setLastLatencyMs] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const prevQueueRef = useRef<number | null>(null);

  const cashRegisterId = useMemo(() => {
    const id = posReadiness.data?.effectiveRegisterId?.trim();
    return id && id !== '00000000-0000-0000-0000-000000000000' ? id : null;
  }, [posReadiness.data?.effectiveRegisterId]);

  const refresh = useCallback(async () => {
    setLoading(true);
    try {
      const { body, latencyMs } = await fetchTseHealth(cashRegisterId);
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
  }, [cashRegisterId]);

  useConditionalPolling(() => {
    void refresh();
  }, POS_TSE_HEALTH_POLL_MS);

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
