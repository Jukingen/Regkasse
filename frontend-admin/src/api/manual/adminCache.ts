/**
 * Super Admin cache troubleshooting API.
 */
import { AXIOS_INSTANCE } from '@/lib/axios';

export type ClearCacheRequest = {
  tenantId?: string;
  prefix?: string;
  clearAll?: boolean;
};

export type ClearCacheResult = {
  success: boolean;
  mode: string;
  detail?: string | null;
};

export async function clearAdminCache(
  body: ClearCacheRequest,
): Promise<ClearCacheResult> {
  const { data } = await AXIOS_INSTANCE.post<ClearCacheResult>(
    '/api/admin/cache/clear',
    body,
  );
  return data;
}
