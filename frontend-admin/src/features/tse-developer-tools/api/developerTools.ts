import { customInstance } from '@/lib/axios';

import type {
  TseDevToolResult,
  TseDeveloperToolsAvailability,
} from '../types';

export async function getTseDeveloperToolsAvailability(
  signal?: AbortSignal
): Promise<TseDeveloperToolsAvailability> {
  return customInstance<TseDeveloperToolsAvailability>({
    url: '/api/admin/tse/developer-tools/availability',
    method: 'GET',
    signal,
  });
}

export async function runTseDiagnostics(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseDevToolResult> {
  return customInstance<TseDevToolResult>({
    url: '/api/admin/tse/developer-tools/diagnostics',
    method: 'POST',
    params: { tenantId },
    signal,
  });
}

export async function simulateTseTraffic(
  tenantId: string,
  transactionCount: number,
  signal?: AbortSignal
): Promise<TseDevToolResult> {
  return customInstance<TseDevToolResult>({
    url: '/api/admin/tse/developer-tools/simulate-traffic',
    method: 'POST',
    params: { tenantId },
    data: { transactionCount },
    signal,
  });
}

export async function validateTseConfig(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseDevToolResult> {
  return customInstance<TseDevToolResult>({
    url: '/api/admin/tse/developer-tools/validate-config',
    method: 'POST',
    params: { tenantId },
    signal,
  });
}

export async function generateTseTestData(
  tenantId: string,
  signal?: AbortSignal
): Promise<TseDevToolResult> {
  return customInstance<TseDevToolResult>({
    url: '/api/admin/tse/developer-tools/generate-test-data',
    method: 'POST',
    params: { tenantId },
    signal,
  });
}
