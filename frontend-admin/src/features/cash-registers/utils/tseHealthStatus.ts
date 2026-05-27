import type { TseHealthStatus } from '@/features/cash-registers/types/enhancedCashRegister';

export function normalizeTseHealthStatus(value: unknown): TseHealthStatus {
    switch (value) {
        case 'healthy':
        case 'degraded':
        case 'offline':
        case 'notConfigured':
            return value;
        default:
            return 'notConfigured';
    }
}

export function tseHealthStatusIcon(status: TseHealthStatus): 'healthy' | 'degraded' | 'offline' | 'unknown' {
    switch (status) {
        case 'healthy':
            return 'healthy';
        case 'degraded':
            return 'degraded';
        case 'offline':
            return 'offline';
        case 'notConfigured':
        default:
            return 'unknown';
    }
}

export function tseHealthTagColor(status: TseHealthStatus): string | undefined {
    switch (status) {
        case 'healthy':
            return 'success';
        case 'degraded':
            return 'warning';
        case 'offline':
            return 'error';
        case 'notConfigured':
        default:
            return 'default';
    }
}
