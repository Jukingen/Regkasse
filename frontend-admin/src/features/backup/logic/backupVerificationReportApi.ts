import { customInstance } from '@/lib/axios';

export type BackupVerificationReportStatus = 'Verified' | 'PartiallyVerified' | 'NotVerified';

export interface BackupTableRowCount {
  schemaName: string;
  tableName: string;
  rowCount: number;
  estimatedSizeBytes: number;
  tableExists: boolean;
}

export interface BackupSourceDatabaseStatistics {
  analyzedAtUtc: string;
  tables: BackupTableRowCount[];
  totalRowCount: number;
}

export interface BackupTableStatistics {
  schemaName: string;
  tableName: string;
  rowCount: number;
  estimatedSizeBytes: number;
  presentInLogicalDump: boolean;
  isVerified: boolean;
  verificationMessage: string | null;
}

export interface BackupVerificationReport {
  backupRunId: string;
  generatedAtUtc: string;
  backupCompletedAtUtc: string | null;
  artifactCount: number;
  totalSizeBytes: number;
  totalSizeFormatted: string;
  logicalDumpAnalyzed: boolean;
  logicalDumpAnalysisMessage: string | null;
  tableStatistics: BackupTableStatistics[];
  sourceStatistics: BackupSourceDatabaseStatistics | null;
  verificationScore: number;
  status: BackupVerificationReportStatus;
}

export function getBackupVerificationReportQueryKey(runId: string) {
  return ['/api/admin/backup/runs', runId, 'verification-report'] as const;
}

export async function getBackupVerificationReport(runId: string): Promise<BackupVerificationReport> {
  return customInstance<BackupVerificationReport>({
    url: `/api/admin/backup/runs/${runId}/verification-report`,
    method: 'GET',
  });
}
