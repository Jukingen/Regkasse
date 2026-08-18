/**
 * Polls POS TSE status for the header chip + offline queue counter (German UI).
 */
import React, { createContext, useCallback, useContext, useMemo, useRef, useState } from 'react';
import { Alert } from 'react-native';

import { usePosRegisterReadiness } from './PosRegisterReadinessContext';
import { isDevSimulateTseUnavailable } from '../constants/devSimulatePosOffline';
import { POS_TSE_HEALTH_POLL_MS } from '../constants/posPollingIntervals';
import { useConditionalPolling } from '../hooks/useConditionalPolling';
import {
  fetchPosTseStatus,
  toOperationalHealthFromPosTse,
  type PosTseIndicatorStatus,
  type PosTseStatusApiResponse,
  type TseOperationalHealthStatus,
} from '../services/api/tseHealthApi';

export type TseBannerVariant = 'online' | 'slow' | 'offline';

export interface TseHealthContextValue {
  /** Process health used by payment offline routing: Online | Degraded | Offline */
  status: TseOperationalHealthStatus | string;
  /** Cashier indicator: Active | Degraded | Inactive */
  indicatorStatus: PosTseIndicatorStatus | string;
  message: string | null;
  lastCheck: string | null;
  scuId: string | null;
  certificateValidUntil: string | null;
  cached: boolean;
  /** Normalized banner colors/messages */
  bannerVariant: TseBannerVariant;
  /** Last GET /api/pos/tse/status round-trip time (ms) */
  lastLatencyMs: number | null;
  pendingOfflineQueueCount: number | null;
  estimatedRecoveryTimeUtc: string | null;
  lastErrorMessageSafe: string | null;
  /** Fiskaly SIGN AT: TEST | LIVE */
  environment: string | null;
  loading: boolean;
  refresh: () => Promise<PosTseStatusApiResponse | null>;
}

const TseHealthContext = createContext<TseHealthContextValue | null>(null);

const SLOW_MS = 3000;

function normalizeBannerVariant(
  indicator: string,
  operationalHealth: string,
  latencyMs: number | null
): TseBannerVariant {
  if (indicator === 'Inactive' || operationalHealth === 'Offline') return 'offline';
  if (indicator === 'Degraded' || operationalHealth === 'Degraded') return 'slow';
  if (operationalHealth === 'Online' && latencyMs != null && latencyMs > SLOW_MS) return 'slow';
  return 'online';
}

export function TseHealthProvider({ children }: { children: React.ReactNode }) {
  const posReadiness = usePosRegisterReadiness();
  const [payload, setPayload] = useState<PosTseStatusApiResponse | null>(null);
  const [lastLatencyMs, setLastLatencyMs] = useState<number | null>(null);
  const [loading, setLoading] = useState(false);
  const prevQueueRef = useRef<number | null>(null);

  const cashRegisterId = useMemo(() => {
    const id = posReadiness.data?.effectiveRegisterId?.trim();
    return id && id !== '00000000-0000-0000-0000-000000000000' ? id : null;
  }, [posReadiness.data?.effectiveRegisterId]);

  const refresh = useCallback(async (): Promise<PosTseStatusApiResponse | null> => {
    setLoading(true);
    try {
      const { body, latencyMs } = await fetchPosTseStatus(cashRegisterId);
      setPayload(body);
      setLastLatencyMs(latencyMs);

      const q =
        typeof body.nonFiscalPendingQueueCount === 'number'
          ? body.nonFiscalPendingQueueCount
          : null;
      const prev = prevQueueRef.current;
      if (prev != null && q != null && prev > 0 && q < prev) {
        Alert.alert('TSE', 'Ausstehende Offline-Zahlungen wurden signiert oder aktualisiert.');
      }
      prevQueueRef.current = q;
      return body;
    } catch {
      setPayload(null);
      setLastLatencyMs(null);
      return null;
    } finally {
      setLoading(false);
    }
  }, [cashRegisterId]);

  useConditionalPolling(() => {
    void refresh();
  }, POS_TSE_HEALTH_POLL_MS);

  const value = useMemo<TseHealthContextValue>(() => {
    const indicatorRaw = (payload?.status ?? 'Inactive').toString();
    const indicator = isDevSimulateTseUnavailable() ? 'Inactive' : indicatorRaw;
    const operationalHealth = isDevSimulateTseUnavailable()
      ? 'Offline'
      : toOperationalHealthFromPosTse(indicator, payload?.operationalHealth);
    const lat = lastLatencyMs;
    const bannerVariant = normalizeBannerVariant(indicator, operationalHealth, lat);
    const scuId = payload?.scuId?.trim() || payload?.tssId?.trim() || null;
    return {
      status: operationalHealth,
      indicatorStatus: indicator,
      message: payload?.message?.trim() || null,
      lastCheck: payload?.lastCheck ?? null,
      scuId,
      certificateValidUntil: payload?.certificateValidUntil ?? null,
      cached: Boolean(payload?.cached),
      bannerVariant,
      lastLatencyMs: lat,
      pendingOfflineQueueCount:
        typeof payload?.nonFiscalPendingQueueCount === 'number'
          ? payload.nonFiscalPendingQueueCount
          : null,
      estimatedRecoveryTimeUtc: payload?.estimatedRecoveryTimeUtc ?? null,
      lastErrorMessageSafe: isDevSimulateTseUnavailable()
        ? 'Entwicklungssimulation: TSE wird als offline behandelt.'
        : (payload?.lastErrorMessageSafe ?? null),
      environment: payload?.environment?.trim() || null,
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
      indicatorStatus: 'Active',
      message: null,
      lastCheck: null,
      scuId: null,
      certificateValidUntil: null,
      cached: false,
      bannerVariant: 'online',
      lastLatencyMs: null,
      pendingOfflineQueueCount: null,
      estimatedRecoveryTimeUtc: null,
      lastErrorMessageSafe: null,
      environment: null,
      loading: false,
      refresh: async () => null,
    };
  }
  return ctx;
}
