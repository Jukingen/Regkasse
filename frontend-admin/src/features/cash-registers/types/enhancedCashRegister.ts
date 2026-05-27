import type { CashRegister } from '@/api/generated/model';

export type TseHealthStatus = 'healthy' | 'degraded' | 'offline' | 'notConfigured';

export type CashRegisterDeviceInfo = {
    model: string | null;
    osVersion: string | null;
    appVersion: string | null;
};

/** Admin list projection with operational telemetry from GET /api/admin/cash-registers. */
export type EnhancedCashRegister = CashRegister & {
    tenantId?: string | null;
    tenantName?: string | null;
    tenantSlug?: string | null;
    lastMonatsbelegUtc?: string | null;
    lastJahresbelegUtc?: string | null;
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
