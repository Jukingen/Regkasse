/**
 * Admin yedek çalıştırma modu API’si.
 *
 * Neden Orval dışı:
 * - `backend/swagger.json` şu an `GET/PUT /api/admin/backup/execution-mode` uçlarını yayınlamıyor;
 *   Orval yalnızca OpenAPI’de tanımlı yolları üretir.
 *
 * Geçiş (migration) — backend tarafı:
 * 1) Bu iki uç + `BackupExecutionModeResponseDto` gövdesini OpenAPI’ye ekle.
 * 2) `npm run generate:api` (veya projedeki Orval komutu) çalıştır.
 * 3) Bu dosyadaki `getBackupExecutionMode` / `putBackupExecutionMode` çağrılarını üretilen
 *    `getApiAdminBackupExecutionMode` / `putApiAdminBackupExecutionMode` (isimler spe’e göre)
 *    ile değiştir; `BackupExecutionModeResponseDto` tipini `@/api/generated/model` üzerinden al.
 * 4) Query key: `backupExecutionModeQueryKeys.ts` içindeki `getGetApiAdminBackupExecutionModeQueryKey`
 *    üretilen modüldeki anahtarla birebir kalmalı (şimdiden hizalı).
 *
 * `BackupExecutionModeCard` şu an React Query kullanmıyor (yerel state + imperatif yükleme);
 * pano `BackupDrDashboard` execution-mode için `useQuery` kullanır — query key `backupExecutionModeQueryKeys` üzerinden.
 */
import { BACKUP_EXECUTION_MODE_API_PATH } from '@/features/backup-dr/logic/backupExecutionModeQueryKeys';
import { customInstance } from '@/lib/axios';

export {
  BACKUP_EXECUTION_MODE_API_PATH,
  getGetApiAdminBackupExecutionModeQueryKey,
} from '@/features/backup-dr/logic/backupExecutionModeQueryKeys';

/** Seçilebilir mod satırı (`BackupExecutionModeResponseDto.selectableModes` öğesi). */
export interface BackupExecutionSelectableModeDto {
  userFacingMode: string;
  internalMode: string;
  selectable: boolean;
  blockReason?: string | null;
}

/** GET / PUT yanıt gövdesi (backend `BackupExecutionModeResponseDto`). */
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
    url: BACKUP_EXECUTION_MODE_API_PATH,
    method: 'GET',
  });
}

export async function putBackupExecutionMode(body: {
  mode: string;
  confirmSimulatedOnlyOperationalRiskInProduction: boolean;
}): Promise<BackupExecutionModeResponseDto> {
  return customInstance<BackupExecutionModeResponseDto>({
    url: BACKUP_EXECUTION_MODE_API_PATH,
    method: 'PUT',
    data: body,
  });
}
