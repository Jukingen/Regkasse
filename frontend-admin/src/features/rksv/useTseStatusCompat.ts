'use client';

/**
 * Legacy RKSV screens expected richer TSE status/device payloads than GET /api/tse/health exposes.
 * Maps the canonical health DTO to the older UI shape; optional fields stay undefined when unknown.
 */
import type { TseHealthResponseDto } from '@/api/generated/model';
import { useGetApiTseHealth } from '@/api/generated/tse/tse';

export type LegacyTseStatusDisplay = {
  isConnected: boolean;
  serialNumber?: string;
  kassenId?: string;
  certificateStatus?: string;
  memoryStatus?: string;
  lastSignatureTime?: string;
  canCreateInvoices?: boolean;
};

function mapHealthToLegacy(h: TseHealthResponseDto): LegacyTseStatusDisplay {
  const st = (h.status ?? '').trim();
  const isConnected = st === 'Online' || st === 'Degraded';
  return {
    isConnected,
    memoryStatus: h.lastErrorMessageSafe ?? undefined,
    lastSignatureTime: h.lastSuccessfulPingUtc ?? h.lastCheckUtc ?? undefined,
  };
}

/** @deprecated Prefer useGetApiTseHealth + explicit mapping; kept for RKSV status/CMC pages. */
export function useGetApiTseStatus() {
  const q = useGetApiTseHealth();
  const data = q.data ? mapHealthToLegacy(q.data) : undefined;
  return { ...q, data };
}

export type LegacyTseDeviceRow = {
  id?: string;
  serialNumber?: string | null;
  kassenId?: string | null;
};

/** No multi-device list in OpenAPI yet; returns empty until backend exposes it. */
export function useGetApiTseDevices() {
  return {
    data: [] as LegacyTseDeviceRow[],
    isLoading: false,
    error: undefined as unknown,
    isError: false,
    isSuccess: true,
    status: 'success' as const,
  };
}
