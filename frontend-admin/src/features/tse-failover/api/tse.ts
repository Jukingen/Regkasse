import { customInstance } from '@/lib/axios';

import type {
  ManualTseFailoverRequest,
  RevertTseFailoverRequest,
  TseFailoverActionResponse,
  TseFailoverDevice,
  TseFailoverHistoryItem,
  TseFailoverStatus,
  TseHealthReport,
  TseHealthTrendPoint,
  TsePerformanceAlert,
  TsePerformanceMetrics,
  TseComplianceReport,
  TseComplianceStatus,
} from '../types';

export async function getTseDevices(signal?: AbortSignal): Promise<TseFailoverDevice[]> {
  return customInstance<TseFailoverDevice[]>({
    url: '/api/admin/tse/failover/devices',
    method: 'GET',
    signal,
  });
}

export async function getFailoverStatus(signal?: AbortSignal): Promise<TseFailoverStatus> {
  return customInstance<TseFailoverStatus>({
    url: '/api/admin/tse/failover/status',
    method: 'GET',
    signal,
  });
}

export async function getFailoverHistory(
  take = 50,
  signal?: AbortSignal
): Promise<TseFailoverHistoryItem[]> {
  return customInstance<TseFailoverHistoryItem[]>({
    url: '/api/admin/tse/failover/history',
    method: 'GET',
    params: { take },
    signal,
  });
}

export async function getTseHealthReport(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseHealthReport> {
  return customInstance<TseHealthReport>({
    url: '/api/admin/tse/failover/health-report',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function getTseHealthTrend(
  tenantId: string,
  days = 7,
  deviceId?: string,
  signal?: AbortSignal
): Promise<TseHealthTrendPoint[]> {
  return customInstance<TseHealthTrendPoint[]>({
    url: '/api/admin/tse/failover/health-trend',
    method: 'GET',
    params: {
      tenantId,
      days,
      ...(deviceId ? { deviceId } : {}),
    },
    signal,
  });
}

export async function getTsePerformanceMetrics(
  deviceId: string,
  days = 7,
  signal?: AbortSignal
): Promise<TsePerformanceMetrics> {
  const toUtc = new Date();
  const fromUtc = new Date(toUtc.getTime() - days * 24 * 60 * 60 * 1000);
  return customInstance<TsePerformanceMetrics>({
    url: '/api/admin/tse/failover/performance',
    method: 'GET',
    params: {
      deviceId,
      fromUtc: fromUtc.toISOString(),
      toUtc: toUtc.toISOString(),
    },
    signal,
  });
}

export async function getTsePerformanceAnomalies(
  deviceId: string,
  signal?: AbortSignal
): Promise<TsePerformanceAlert> {
  return customInstance<TsePerformanceAlert>({
    url: '/api/admin/tse/failover/performance-anomalies',
    method: 'GET',
    params: { deviceId },
    signal,
  });
}

export async function getTseComplianceReport(
  tenantId: string,
  fromUtc: string,
  toUtc: string,
  signal?: AbortSignal
): Promise<TseComplianceReport> {
  return customInstance<TseComplianceReport>({
    url: '/api/admin/tse/compliance/report',
    method: 'GET',
    params: { tenantId, fromUtc, toUtc },
    signal,
  });
}

export async function getTseComplianceStatus(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseComplianceStatus> {
  return customInstance<TseComplianceStatus>({
    url: '/api/admin/tse/compliance/status',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function manualFailover(
  body: ManualTseFailoverRequest,
  signal?: AbortSignal
): Promise<TseFailoverActionResponse> {
  return customInstance<TseFailoverActionResponse>({
    url: '/api/admin/tse/failover/manual',
    method: 'POST',
    data: body,
    signal,
  });
}

export async function revertFailover(
  primaryDeviceId: string,
  signal?: AbortSignal
): Promise<TseFailoverActionResponse> {
  const body: RevertTseFailoverRequest = { primaryDeviceId };
  return customInstance<TseFailoverActionResponse>({
    url: '/api/admin/tse/failover/revert',
    method: 'POST',
    data: body,
    signal,
  });
}
