import { customInstance } from '@/lib/axios';

export const RESTORE_VERIFICATION_RUNS_PATH = '/api/admin/restore-verification/runs' as const;

export type RestoreVerificationRunStatus = 0 | 1 | 2 | 3 | 'Queued' | 'Running' | 'Succeeded' | 'Failed';
export type RestoreVerificationTriggerSource = 0 | 1 | 'Manual' | 'Scheduled';
export type RestoreVerificationVerdict = 'pending' | 'passed' | 'failed';
export type RestoreVerificationCheckResult = 'passed' | 'failed' | 'skipped' | 'unavailable';

export interface RestoreVerificationCheckDto {
  id: string;
  result: RestoreVerificationCheckResult;
  detail?: string | null;
}

export interface RestoreVerificationRowCountDto {
  id: string;
  name: string;
  category: string;
  measured?: number | null;
  expectedAtLeast?: number | null;
  status: string;
}

export interface RestoreVerificationRunDto {
  id: string;
  status: RestoreVerificationRunStatus;
  triggerSource: RestoreVerificationTriggerSource;
  sourceBackupRunId?: string | null;
  sourceBackupArtifactId?: string | null;
  dumpInspectionPassed?: boolean | null;
  restoreAttemptExecuted?: boolean;
  restoreAttemptPassed?: boolean | null;
  restoreAttemptSkipReason?: string | null;
  fiscalSqlSkipped?: boolean;
  fiscalSqlSkipReason?: string | null;
  fiscalSqlPassed?: boolean | null;
  fiscalSqlFailCount?: number | null;
  fiscalSqlWarnCount?: number | null;
  integrityChecksPassed?: boolean | null;
  postRestoreContinuityChecksExecuted?: boolean;
  postRestoreContinuityChecksPassed?: boolean | null;
  fiscalContinuityLayerPassed?: boolean | null;
  requestedAt: string;
  startedAt?: string | null;
  completedAt?: string | null;
  failureCode?: string | null;
  failureDetail?: string | null;
  verdict?: RestoreVerificationVerdict | null;
  checks?: RestoreVerificationCheckDto[] | null;
  failedCheckIds?: string[] | null;
  sourceBackupStrategy?: string | null;
  sourceBackupTenantId?: string | null;
  restoreDrillReachedStage?: number | string | null;
  durationMs?: number | null;
}

export interface RestoreVerificationHistoryResponseDto {
  items: RestoreVerificationRunDto[];
  page: number;
  pageSize: number;
  totalCount: number;
}

export interface RestoreVerificationReportDto {
  run: RestoreVerificationRunDto;
  verdict: RestoreVerificationVerdict;
  checks: RestoreVerificationCheckDto[];
  failedCheckIds: string[];
  rowCounts: RestoreVerificationRowCountDto[];
  fiscalSqlResult?: string | null;
  verifiedAtUtc?: string | null;
}

export interface RestoreVerificationRunListParams {
  page?: number;
  pageSize?: number;
  status?: number | string;
  triggerSource?: number | string;
  fromUtc?: string;
  toUtc?: string;
  sourceBackupRunId?: string;
}

export function getRestoreVerificationRunsQueryKey(params: RestoreVerificationRunListParams) {
  return [RESTORE_VERIFICATION_RUNS_PATH, params] as const;
}

export function getRestoreVerificationRunQueryKey(id: string) {
  return [RESTORE_VERIFICATION_RUNS_PATH, id] as const;
}

export function getRestoreVerificationLatestQueryKey() {
  return [RESTORE_VERIFICATION_RUNS_PATH, 'latest'] as const;
}

export async function getRestoreVerificationLatest(): Promise<RestoreVerificationRunDto | null> {
  return customInstance<RestoreVerificationRunDto | null>({
    url: `${RESTORE_VERIFICATION_RUNS_PATH}/latest`,
    method: 'GET',
  });
}

export async function getRestoreVerificationRuns(
  params: RestoreVerificationRunListParams
): Promise<RestoreVerificationHistoryResponseDto> {
  return customInstance<RestoreVerificationHistoryResponseDto>({
    url: RESTORE_VERIFICATION_RUNS_PATH,
    method: 'GET',
    params: {
      page: params.page,
      pageSize: params.pageSize,
      status: params.status,
      triggerSource: params.triggerSource,
      fromUtc: params.fromUtc,
      toUtc: params.toUtc,
      sourceBackupRunId: params.sourceBackupRunId || undefined,
    },
  });
}

export async function getRestoreVerificationRun(id: string): Promise<RestoreVerificationRunDto> {
  return customInstance<RestoreVerificationRunDto>({
    url: `${RESTORE_VERIFICATION_RUNS_PATH}/${id}`,
    method: 'GET',
  });
}

export async function getRestoreVerificationReportJson(
  id: string
): Promise<RestoreVerificationReportDto> {
  return customInstance<RestoreVerificationReportDto>({
    url: `${RESTORE_VERIFICATION_RUNS_PATH}/${id}/report`,
    method: 'GET',
    params: { format: 'json' },
  });
}

export async function downloadRestoreVerificationReport(
  id: string,
  format: 'csv' | 'pdf'
): Promise<Blob> {
  return customInstance<Blob>({
    url: `${RESTORE_VERIFICATION_RUNS_PATH}/${id}/report`,
    method: 'GET',
    params: { format },
    responseType: 'blob',
  });
}
