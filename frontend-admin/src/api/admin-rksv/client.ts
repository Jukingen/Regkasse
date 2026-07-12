import { AXIOS_INSTANCE } from '@/lib/axios';
import { normalizeRksvBackendEnvironment } from '@/features/rksv/normalizeRksvBackendEnvironment';
import type { RksvBackendEnvironmentStatus } from '@/features/rksv/types/rksvBackendEnvironment';

/** GET /api/rksv/status — canonical RKSV environment status for POS and Admin. */
export async function getRksvBackendEnvironment(signal?: AbortSignal): Promise<RksvBackendEnvironmentStatus> {
  const response = await AXIOS_INSTANCE.get<unknown>('/api/rksv/status', { signal });
  const normalized = normalizeRksvBackendEnvironment(response.data);
  if (!normalized) {
    throw new Error('RKSV environment response was empty or invalid');
  }
  return normalized;
}
