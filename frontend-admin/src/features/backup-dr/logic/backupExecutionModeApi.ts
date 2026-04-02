/**
 * Admin yedek çalıştırma modu API’si (orval dışı — swagger senkronu sonrası generate ile değiştirilebilir).
 */

import { customInstance } from '@/lib/axios';

/** GET /api/admin/backup/execution-mode yanıtı (backend BackupExecutionModeResponseDto). */
export interface BackupExecutionSelectableModeDto {
  userFacingMode: string;
  internalMode: string;
  selectable: boolean;
  blockReason?: string | null;
}

/** GET /api/admin/backup/execution-mode yanıtı (backend BackupExecutionModeResponseDto). */
export interface BackupExecutionModeResponseDto {
  storedMode: string;
  requestedUserFacingMode: string;
  configurationDefaultUserFacingMode: string;
  effectiveUserFacingMode: string;
  recommendedFallbackUserFacingMode?: string | null;
  adapterKindIfConfigurationDefaultOnly: string;
  effectiveModeResolutionSummaryEnglish: string;
  configurationExecutionAdapterKind: string;
  effectiveExecutionAdapterKind: string;
  effectiveModeRunnable: boolean;
  /** Sağlık seviyesi, yalnızca PostgreSqlPgDump admin modu varsayıldığında (Real seçilebilirlik ile uyumlu). Eski API’lerde eksik olabilir. */
  hypotheticalPgDumpHealthLevel?: string;
  blockers: string[];
  realModeBlockingDiagnostics: Array<{
    code: string;
    severity: string;
    message: string;
    relatedConfigurationKeys?: string[] | null;
  }>;
  selectableModes: BackupExecutionSelectableModeDto[];
  effectiveConfigurationHealth: Record<string, unknown>;
}

export async function getBackupExecutionMode(): Promise<BackupExecutionModeResponseDto> {
  return customInstance<BackupExecutionModeResponseDto>({
    url: '/api/admin/backup/execution-mode',
    method: 'GET',
  });
}

export async function putBackupExecutionMode(body: {
  mode: string;
  confirmSimulatedOnlyOperationalRiskInProduction: boolean;
}): Promise<BackupExecutionModeResponseDto> {
  return customInstance<BackupExecutionModeResponseDto>({
    url: '/api/admin/backup/execution-mode',
    method: 'PUT',
    data: body,
  });
}
