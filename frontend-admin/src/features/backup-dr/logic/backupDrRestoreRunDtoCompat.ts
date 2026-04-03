/**
 * Orval şeması güncellenene kadar API’nin dönebileceği ek restore-drill alanlarını güvenli okur (yanıltıcı anlam üretmez).
 */

import type { RestoreVerificationRunResponseDto } from '@/api/generated/model';

export interface RestoreVerificationRunDtoExtended {
  evidenceJson?: string | null;
  sourceBackupArtifactId?: string | null;
  durationMs?: number | null;
  postRestoreContinuityChecksExecuted?: boolean;
  postRestoreContinuityChecksPassed?: boolean | null;
  /** API: L4 izole geri yükleme sonrası süreklilik SQL (null = çalıştırılmadı / kapsam dışı). */
  postRestoreL4ContinuityProofState?: 'notExecuted' | 'failed' | 'passed' | 'inconclusive' | null;
  restoredDatabaseApplicationSmokeExecuted?: boolean;
  restoredDatabaseApplicationSmokePassed?: boolean | null;
  restoreDrillReachedStage?: number | null;
  failureCategory?: number | null;
  /** API: L4 bileşik (fiscal + klon sürekliliği kapsamı). */
  fiscalContinuityLayerPassed?: boolean | null;
  applicationSmokeProbeExecuted?: boolean;
  applicationSmokeProbePassed?: boolean | null;
  externalDependencyProofOutcome?: string | null;
}

function readString(r: Record<string, unknown>, key: string): string | null | undefined {
  const v = r[key];
  return typeof v === 'string' ? v : undefined;
}

function readBool(r: Record<string, unknown>, key: string): boolean | undefined {
  const v = r[key];
  if (typeof v === 'boolean') return v;
  return undefined;
}

function readNumber(r: Record<string, unknown>, key: string): number | null | undefined {
  const v = r[key];
  if (typeof v === 'number' && !Number.isNaN(v)) return v;
  return undefined;
}

/** Sunucu yanıtındaki ek alanları (varsa) okur. */
export function extendRestoreVerificationRunDto(run: RestoreVerificationRunResponseDto | undefined | null): RestoreVerificationRunDtoExtended {
  if (!run) return {};
  const r = run as RestoreVerificationRunResponseDto & Record<string, unknown>;
  return {
    evidenceJson: readString(r, 'evidenceJson'),
    sourceBackupArtifactId: readString(r, 'sourceBackupArtifactId'),
    durationMs: readNumber(r, 'durationMs'),
    postRestoreContinuityChecksExecuted: readBool(r, 'postRestoreContinuityChecksExecuted'),
    postRestoreContinuityChecksPassed: readBool(r, 'postRestoreContinuityChecksPassed'),
    postRestoreL4ContinuityProofState: readString(r, 'postRestoreL4ContinuityProofState') as
      | 'notExecuted'
      | 'failed'
      | 'passed'
      | 'inconclusive'
      | undefined,
    restoredDatabaseApplicationSmokeExecuted: readBool(r, 'restoredDatabaseApplicationSmokeExecuted'),
    restoredDatabaseApplicationSmokePassed: readBool(r, 'restoredDatabaseApplicationSmokePassed'),
    restoreDrillReachedStage: readNumber(r, 'restoreDrillReachedStage'),
    failureCategory: readNumber(r, 'failureCategory'),
    fiscalContinuityLayerPassed: readBool(r, 'fiscalContinuityLayerPassed'),
    applicationSmokeProbeExecuted: readBool(r, 'applicationSmokeProbeExecuted'),
    applicationSmokeProbePassed: readBool(r, 'applicationSmokeProbePassed'),
    externalDependencyProofOutcome: readString(r, 'externalDependencyProofOutcome'),
  };
}
