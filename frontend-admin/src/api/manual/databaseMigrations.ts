import type { AxiosRequestConfig } from 'axios';

import { AXIOS_INSTANCE } from '@/lib/axios';

export interface MigrationEntryDto {
  id: string;
  productVersion?: string | null;
}

export interface AdminMigrationStatusDto {
  status: string;
  appliedCount: number;
  pendingCount: number;
  latestApplied?: string | null;
  pending: string[];
  recentApplied: MigrationEntryDto[];
  checkedAtUtc: string;
  strategyDoc?: string;
}

export async function fetchDatabaseMigrations(
  take = 50,
  signal?: AbortSignal,
): Promise<AdminMigrationStatusDto> {
  const config: AxiosRequestConfig = { signal, params: { take } };
  const { data } = await AXIOS_INSTANCE.get<AdminMigrationStatusDto>(
    '/api/admin/database/migrations',
    config,
  );
  return data;
}
