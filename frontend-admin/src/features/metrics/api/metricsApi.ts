import { customInstance } from '@/lib/axios';

export type SystemMetricsSummary = {
    totalRequests: number;
    avgResponseTime: number;
    errorRate: number;
    activeUsers: number;
    activeOrders: number;
    activeTenants: number;
    cacheHitRatio: number;
    uptime: number;
    environment: string;
};

export const metricsQueryKeys = {
    summary: ['admin', 'metrics'] as const,
};

export async function fetchSystemMetricsSummary(): Promise<SystemMetricsSummary> {
    return customInstance<SystemMetricsSummary>({
        url: '/api/admin/metrics',
        method: 'GET',
    });
}
