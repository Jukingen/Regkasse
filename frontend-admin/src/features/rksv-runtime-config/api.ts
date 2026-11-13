import { customInstance } from '@/lib/axios';

export const rksvRuntimeConfigQueryKey = ['admin', 'rksv-runtime-config'] as const;

export type RksvRuntimeConfigValues = {
  mode: string;
  tseMode: string;
  finanzOnlineMode: string;
  showDemoLabel: boolean;
  bypassTseInDevelopment: boolean;
};

export type RksvRuntimeConfigDto = RksvRuntimeConfigValues & {
  source: string;
  overlayPersisted: boolean;
  hostEnvironment: string;
  productionLockApplies: boolean;
  productionLockOk: boolean;
  productionLockReasons: string[];
  restartRequired: boolean;
  canSetDemoOnThisHost: boolean;
  tseHealthBypassEffective: boolean;
  tseHealthBypassBlockedByRealTseMode: boolean;
  updatedAtUtc: string | null;
  updatedBy: string | null;
  appsettingsFallback: RksvRuntimeConfigValues;
};

export type RksvRuntimeConfigPostDto = RksvRuntimeConfigValues & {
  reason?: string;
};

export function fetchRksvRuntimeConfig(signal?: AbortSignal): Promise<RksvRuntimeConfigDto> {
  return customInstance<RksvRuntimeConfigDto>({
    url: '/api/admin/rksv/config',
    method: 'GET',
    signal,
  });
}

export function postRksvRuntimeConfig(
  body: RksvRuntimeConfigPostDto,
  signal?: AbortSignal
): Promise<RksvRuntimeConfigDto> {
  return customInstance<RksvRuntimeConfigDto>({
    url: '/api/admin/rksv/config',
    method: 'POST',
    data: body,
    signal,
  });
}
