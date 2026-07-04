"use client";

/**
 * Backup admin API hooks — Orval-generated clients + shared query keys / invalidation.
 * Configuration health is derived from GET /api/admin/backup/status/latest (no dedicated health route).
 */

import { useMemo } from "react";
import { useQuery, useQueryClient } from "@tanstack/react-query";
import type { UseQueryOptions } from "@tanstack/react-query";
import {
  getGetApiAdminBackupRecoverabilitySummaryQueryKey,
  getGetApiAdminBackupRunsIdQueryKey,
  getGetApiAdminBackupRunsQueryKey,
  getGetApiAdminBackupStatusLatestQueryKey,
  useGetApiAdminBackupRuns,
  useGetApiAdminBackupRunsId,
  usePostApiAdminBackupTrigger,
} from "@/api/generated/admin-backup/admin-backup";
import type { BackupTriggerRequestDto } from "@/api/generated/model";
import type { GetApiAdminBackupRunsParams } from "@/api/generated/model/getApiAdminBackupRunsParams";
import type { BackupArtifactPipelinePolicyResponseDto } from "@/api/generated/model";
import type { BackupConfigurationHealthResponseDto } from "@/api/generated/model/backupConfigurationHealthResponseDto";
import { useGetApiAdminBackupStatusLatest } from "@/api/generated/admin-backup/admin-backup";
import {
  getBackupScheduleSettingsQueryKey,
  getBackupScheduleStatusQueryKey,
  getBackupScheduleSettings,
  putBackupScheduleSettings,
  type BackupSettingsPutRequestDto,
  type BackupSettingsResponseDto,
} from "@/features/backup-dr/logic/backupScheduleSettingsApi";
import { useMutation } from "@tanstack/react-query";
import { usePollRunDetailDashboardInterval } from "@/features/backup-dr/logic/backupDashboardQueryTiming";
import {
  BACKUP_DASHBOARD_STATS_POLL_MS,
  getBackupDashboardStatsQueryKey,
} from "@/features/backup/logic/backupDashboardStatsApi";
import { getGetApiAdminBackupListQueryKey } from "@/api/generated/admin/admin";

/** List/query params for GET /api/admin/backup/runs */
export type BackupRunsParams = GetApiAdminBackupRunsParams & {
  /**
   * Super Admin optional filter (query param when supported).
   * Runs are deployment-scoped; UI may client-filter by idempotency key until per-tenant runs ship.
   */
  tenantId?: string;
};

function toRunsQueryParams(params?: BackupRunsParams): GetApiAdminBackupRunsParams & { tenantId?: string } {
  if (!params) return {};
  const { tenantId, ...rest } = params;
  const trimmed = tenantId?.trim();
  return trimmed ? { ...rest, tenantId: trimmed } : rest;
}

export type BackupSettings = BackupSettingsResponseDto;

export type BackupConfigurationHealthView = BackupConfigurationHealthResponseDto & {
  externalArchiveRootConfigured?: boolean;
};

/** Stable query keys (Orval-aligned for cache sharing with generated hooks). */
export const backupQueryKeys = {
  all: ["/api/admin/backup"] as const,
  runs: (params?: BackupRunsParams) =>
    getGetApiAdminBackupRunsQueryKey(toRunsQueryParams(params) as GetApiAdminBackupRunsParams),
  run: (id: string) => getGetApiAdminBackupRunsIdQueryKey(id),
  settings: () => getBackupScheduleSettingsQueryKey(),
  scheduleStatus: () => getBackupScheduleStatusQueryKey(),
  latestStatus: () => getGetApiAdminBackupStatusLatestQueryKey(),
  configurationHealth: () => getGetApiAdminBackupStatusLatestQueryKey(),
  dashboardStats: () => getBackupDashboardStatsQueryKey(),
  recoverability: () => getGetApiAdminBackupRecoverabilitySummaryQueryKey(),
  list: () => getGetApiAdminBackupListQueryKey(),
} as const;

export async function invalidateBackupQueries(queryClient: ReturnType<typeof useQueryClient>) {
  await queryClient.invalidateQueries({ queryKey: backupQueryKeys.all });
  await queryClient.invalidateQueries({ queryKey: backupQueryKeys.dashboardStats() });
  await queryClient.invalidateQueries({ queryKey: backupQueryKeys.recoverability() });
  await queryClient.invalidateQueries({ queryKey: backupQueryKeys.list() });
}

export type UseBackupRunsOptions = {
  enabled?: boolean;
  refetchInterval?: number | false;
  staleTime?: number;
};

/** GET /api/admin/backup/runs */
export function useBackupRuns(params?: BackupRunsParams, options?: UseBackupRunsOptions) {
  const apiParams = toRunsQueryParams(params);
  return useGetApiAdminBackupRuns(apiParams as GetApiAdminBackupRunsParams, {
    query: {
      enabled: options?.enabled,
      refetchInterval: options?.refetchInterval,
      staleTime: options?.staleTime ?? 15_000,
      refetchOnWindowFocus: true,
    },
  });
}

export type UseBackupRunOptions = {
  enabled?: boolean;
};

/** GET /api/admin/backup/runs/{id} */
export function useBackupRun(
  id: string | null | undefined,
  options?: UseBackupRunOptions,
) {
  const pollRunDetail = usePollRunDetailDashboardInterval(id ?? undefined, undefined);
  const enabled = Boolean(id?.trim()) && options?.enabled !== false;

  return useGetApiAdminBackupRunsId(id ?? "", {
    query: {
      enabled,
      refetchInterval: enabled ? pollRunDetail : false,
      refetchOnWindowFocus: enabled,
    },
  });
}

export type TriggerBackupParams = {
  /** Encoded in idempotency key for operator traceability (backup is instance-scoped). */
  tenantId?: string;
  allTenants?: boolean;
  note?: string;
};

function buildIdempotencyKey(params: TriggerBackupParams): string {
  const stamp = Date.now();
  if (params.allTenants) {
    return `manual-all-tenants-${stamp}`;
  }
  if (params.tenantId?.trim()) {
    return `manual-tenant-${params.tenantId.trim()}-${stamp}`;
  }
  return `manual-${stamp}`;
}

function toTriggerRequestBody(params: TriggerBackupParams): BackupTriggerRequestDto {
  return {
    idempotencyKey: buildIdempotencyKey(params),
  };
}

/** POST /api/admin/backup/trigger */
export function useTriggerBackup() {
  const queryClient = useQueryClient();

  const mutation = usePostApiAdminBackupTrigger({
    mutation: {
      onSuccess: async () => {
        await invalidateBackupQueries(queryClient);
      },
    },
  });

  return {
    ...mutation,
    mutateAsync: (params: TriggerBackupParams = {}) =>
      mutation.mutateAsync({ data: toTriggerRequestBody(params) }),
    mutate: (params: TriggerBackupParams = {}) => mutation.mutate({ data: toTriggerRequestBody(params) }),
  };
}

/** GET /api/admin/backup/settings */
export function useBackupSettings(
  options?: Pick<UseQueryOptions<BackupSettingsResponseDto>, "enabled" | "staleTime">,
) {
  return useQuery({
    queryKey: backupQueryKeys.settings(),
    queryFn: getBackupScheduleSettings,
    staleTime: options?.staleTime ?? 20_000,
    refetchOnWindowFocus: true,
    enabled: options?.enabled,
  });
}

/** PUT /api/admin/backup/settings */
export function useUpdateBackupSettings() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (data: BackupSettingsPutRequestDto) => putBackupScheduleSettings(data),
    onSuccess: async () => {
      await queryClient.invalidateQueries({ queryKey: backupQueryKeys.settings() });
      await queryClient.invalidateQueries({ queryKey: backupQueryKeys.scheduleStatus() });
      await queryClient.invalidateQueries({ queryKey: backupQueryKeys.configurationHealth() });
      await invalidateBackupQueries(queryClient);
    },
  });
}

export type UseBackupConfigurationHealthOptions = {
  poll?: boolean;
};

/**
 * Configuration health from GET /api/admin/backup/status/latest (`configurationHealth` + pipeline policy).
 */
export function useBackupConfigurationHealth(options?: UseBackupConfigurationHealthOptions) {
  const query = useGetApiAdminBackupStatusLatest({
    query: {
      queryKey: backupQueryKeys.configurationHealth(),
      staleTime: 30_000,
      refetchOnWindowFocus: true,
      refetchInterval: options?.poll === false ? false : BACKUP_DASHBOARD_STATS_POLL_MS,
    },
  });

  const health = useMemo((): BackupConfigurationHealthView | undefined => {
    const config = query.data?.configurationHealth;
    if (!config) return undefined;
    return {
      ...config,
      externalArchiveRootConfigured:
        query.data?.artifactPipelinePolicy?.externalArchiveRootConfigured === true,
    };
  }, [query.data?.artifactPipelinePolicy?.externalArchiveRootConfigured, query.data?.configurationHealth]);

  const updatedAt =
    query.dataUpdatedAt > 0 ? new Date(query.dataUpdatedAt).toISOString() : undefined;

  return {
    ...query,
    health,
    configurationHealth: query.data?.configurationHealth,
    artifactPipelinePolicy: query.data?.artifactPipelinePolicy as
      | BackupArtifactPipelinePolicyResponseDto
      | undefined,
    updatedAt,
  };
}
