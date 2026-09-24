import { customInstance } from '@/lib/axios';

export const mwstCanaryQueryKey = ['admin', 'mwst-canary'] as const;

export type MwstCanaryStatus = {
  canaryTenantId: string;
  flagEnabled: boolean;
  useTestEndpoint: boolean;
  kassenSicherheitProvider: string;
};

export function fetchMwstCanary(signal?: AbortSignal): Promise<MwstCanaryStatus> {
  return customInstance<MwstCanaryStatus>({
    url: '/api/admin/mwst/canary',
    method: 'GET',
    signal,
  });
}

export function rollbackMwstCanary(): Promise<MwstCanaryStatus> {
  return customInstance<MwstCanaryStatus>({
    url: '/api/admin/mwst/canary/rollback',
    method: 'POST',
  });
}
