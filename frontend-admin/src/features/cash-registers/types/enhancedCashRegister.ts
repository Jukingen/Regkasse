import type { CashRegister } from '@/api/generated/model';

export type TseHealthStatus = 'healthy' | 'degraded' | 'offline' | 'notConfigured';

export type CashRegisterDeviceInfo = {
  model: string | null;
  osVersion: string | null;
  appVersion: string | null;
};

/** Admin list projection with operational telemetry from GET /api/admin/cash-registers. */
export type EnhancedCashRegister = CashRegister & {
  /** Admin list rows always have a stable register id. */
  id: string;
  /** Admin list rows always carry the owning mandant id. */
  tenantId: string;
  tenantName?: string | null;
  tenantSlug?: string | null;
  lastMonatsbelegUtc?: string | null;
  lastJahresbelegUtc?: string | null;
  /** Admin DTO field; prefer via `readStartbelegCreatedAt` / normalized `startbelegCreatedAt`. */
  startbelegCreatedAtUtc?: string | null;
  tseHealthStatus?: TseHealthStatus | string | null;
  offlineQueueCount?: number;
  lastSyncAtUtc?: string | null;
  currentCashierName?: string | null;
  deviceInfo?: CashRegisterDeviceInfo | null;
};

export type CashRegisterTseHealthResponse = {
  status: TseHealthStatus | string;
  lastCheckUtc?: string | null;
  message?: string | null;
  offlineQueueCount?: number;
};
