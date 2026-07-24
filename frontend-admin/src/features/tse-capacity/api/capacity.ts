import { customInstance } from '@/lib/axios';

import type { TseCapacityAlert, TseCapacityReport, TseForecastResult } from '../types';

export async function getTseCapacityReport(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseCapacityReport> {
  return customInstance<TseCapacityReport>({
    url: '/api/admin/tse/capacity/report',
    method: 'GET',
    params: { tenantId },
    signal,
  });
}

export async function getTseCapacityForecast(
  tenantId: string,
  forecastDays = 30,
  signal?: AbortSignal
): Promise<TseForecastResult> {
  return customInstance<TseForecastResult>({
    url: '/api/admin/tse/capacity/forecast',
    method: 'GET',
    params: { tenantId, forecastDays },
    signal,
  });
}

export async function checkTseCapacityAlerts(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseCapacityAlert> {
  return customInstance<TseCapacityAlert>({
    url: '/api/admin/tse/capacity/check-alerts',
    method: 'POST',
    params: { tenantId },
    signal,
  });
}
