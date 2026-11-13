import { customInstance } from '@/lib/axios';

const PITR_AVAILABILITY_PATH = '/api/admin/backup/pitr/availability';
const PITR_VALIDATE_PATH = '/api/admin/backup/pitr/validate';
const PITR_WAL_PATH = '/api/admin/backup/pitr/wal';
const PITR_CHAIN_PATH = '/api/admin/backup/pitr/chain';
const PITR_PRE_RESTORE_PATH = '/api/admin/backup/pitr/pre-restore-validate';
const PITR_DRY_RUN_PATH = '/api/admin/backup/pitr/dry-run';

export type PitrRecoveryMethod = 'PITR' | 'FullBackupOnly' | 'FullPlusIncremental';

export interface PitrAvailabilityResponse {
  earliestRestorePointUtc: string | null;
  latestRestorePointUtc: string | null;
  supportedTimePointsUtc: string[];
  walArchivingEnabled: boolean;
  walArchiveLagMinutes: number | null;
  walFileCount?: number;
  walRetentionDays?: number;
  walCoverageStartUtc?: string | null;
  walCoverageEndUtc?: string | null;
  message: string | null;
}

export interface ValidatePitrRestorePointRequest {
  targetTimeUtc: string;
}

export interface RestorePointValidationResult {
  isValid: boolean;
  message: string | null;
  baseBackupId: string | null;
  baseBackupTimeUtc: string | null;
  targetTimeUtc: string | null;
  estimatedDataLossSeconds: number | null;
  recoveryMethod: PitrRecoveryMethod | null;
  fullBackupId?: string | null;
  incrementalBackupIds?: string[];
}

export interface BackupChainItem {
  runId: string;
  packageKind: string;
  strategy: string;
  tenantId: string | null;
  tenantSlug: string | null;
  completedAtUtc: string | null;
  incrementalSinceUtc: string | null;
  parentRunId: string | null;
  coveredByWal: boolean;
}

export interface BackupChainResponse {
  tenantIdFilter: string | null;
  fullBackup: BackupChainItem | null;
  incrementals: BackupChainItem[];
  systemBackups: BackupChainItem[];
  walCoverageStartUtc: string | null;
  walCoverageEndUtc: string | null;
  walFileCount: number;
  restorePointAvailable: boolean;
  message: string | null;
}

export interface WalArchiveStatus {
  enabled: boolean;
  directoryExists: boolean;
  directory: string | null;
  fileCount: number;
  oldestFileUtc: string | null;
  newestFileUtc: string | null;
  retentionDays: number;
  switchIntervalMinutes: number;
  lagMinutes: number | null;
  hostArchiveCommandRequired: boolean;
  message: string | null;
}

export interface PitrCheckResult {
  name: string;
  passed: boolean;
  status: string;
  detail: string | null;
}

export interface PitrPreRestoreValidation {
  passed: boolean;
  targetTimeUtc: string | null;
  baseBackupId: string | null;
  recoveryMethod: PitrRecoveryMethod | null;
  estimatedDataLossSeconds: number;
  hash: PitrCheckResult;
  schema: PitrCheckResult;
  tseChain: PitrCheckResult;
  chain: BackupChainResponse | null;
  restorePoint: RestorePointValidationResult | null;
  message: string | null;
}

export interface PitrDryRunResponse {
  accepted: boolean;
  drillRunId: string | null;
  baseBackupId: string | null;
  targetTimeUtc: string | null;
  validation: PitrPreRestoreValidation | null;
  message: string | null;
}

export async function getPitrAvailability(): Promise<PitrAvailabilityResponse> {
  return customInstance<PitrAvailabilityResponse>({
    url: PITR_AVAILABILITY_PATH,
    method: 'GET',
  });
}

export async function validatePitrRestorePoint(
  body: ValidatePitrRestorePointRequest
): Promise<RestorePointValidationResult> {
  return customInstance<RestorePointValidationResult>({
    url: PITR_VALIDATE_PATH,
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: body,
  });
}

export async function getWalArchiveStatus(): Promise<WalArchiveStatus> {
  return customInstance<WalArchiveStatus>({
    url: PITR_WAL_PATH,
    method: 'GET',
  });
}

export async function getBackupChain(tenantId?: string | null): Promise<BackupChainResponse> {
  return customInstance<BackupChainResponse>({
    url: PITR_CHAIN_PATH,
    method: 'GET',
    params: tenantId ? { tenantId } : undefined,
  });
}

export async function validatePitrPreRestore(
  body: ValidatePitrRestorePointRequest
): Promise<PitrPreRestoreValidation> {
  return customInstance<PitrPreRestoreValidation>({
    url: PITR_PRE_RESTORE_PATH,
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: body,
  });
}

export async function requestPitrDryRun(body: {
  targetTimeUtc: string;
  tenantId?: string | null;
}): Promise<PitrDryRunResponse> {
  return customInstance<PitrDryRunResponse>({
    url: PITR_DRY_RUN_PATH,
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    data: body,
  });
}
