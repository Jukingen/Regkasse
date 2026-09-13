import { useQuery } from '@tanstack/react-query';

import {
  getRestoreVerificationReportJson,
  getRestoreVerificationRun,
  getRestoreVerificationRunQueryKey,
  getRestoreVerificationRuns,
  getRestoreVerificationRunsQueryKey,
  type RestoreVerificationRunListParams,
} from '@/features/backup/logic/restoreVerificationApi';

export function useRestoreVerificationRuns(params: RestoreVerificationRunListParams, enabled = true) {
  return useQuery({
    queryKey: getRestoreVerificationRunsQueryKey(params),
    queryFn: () => getRestoreVerificationRuns(params),
    enabled,
  });
}

export function useRestoreVerificationRun(id: string | undefined, enabled = true) {
  return useQuery({
    queryKey: getRestoreVerificationRunQueryKey(id ?? ''),
    queryFn: () => getRestoreVerificationRun(id as string),
    enabled: Boolean(id) && enabled,
  });
}

export function useRestoreVerificationReport(id: string | undefined, enabled = true) {
  return useQuery({
    queryKey: [...getRestoreVerificationRunQueryKey(id ?? ''), 'report'] as const,
    queryFn: () => getRestoreVerificationReportJson(id as string),
    enabled: Boolean(id) && enabled,
  });
}
