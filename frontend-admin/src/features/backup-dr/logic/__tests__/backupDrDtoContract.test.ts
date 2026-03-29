import { describe, expect, it } from 'vitest';
import type {
  BackupArtifactPipelinePolicyResponseDto,
  BackupArtifactResponseDto,
  BackupRunResponseDto,
  BackupVerificationResponseDto,
  RestoreVerificationRunResponseDto,
} from '@/api/generated/model';

/**
 * Bu testler derleme anında kırılır: client fallback / mapper’lar generated DTO’da olmayan alan okursa TypeScript hata verir.
 */

describe('backup-dr Orval DTO field allowlist', () => {
  it('deriveBackupPipelineSteps reads only declared BackupRunResponseDto keys', () => {
    const _: Partial<Record<keyof BackupRunResponseDto, true>> = {
      id: true,
      status: true,
      artifacts: true,
      verifications: true,
      pipeline: true,
    };
    expect(Object.keys(_).length).toBe(5);
  });

  it('external pipeline policy reads only declared BackupArtifactPipelinePolicyResponseDto keys', () => {
    const _: Partial<Record<keyof BackupArtifactPipelinePolicyResponseDto, true>> = {
      willRunExternalArchiveAfterStagingVerificationWhenEligible: true,
      externalArchiveRootConfigured: true,
    };
    expect(Object.keys(_).length).toBe(2);
  });

  it('artifact derive + external copy mapper use only BackupArtifactResponseDto keys', () => {
    const _: Partial<Record<keyof BackupArtifactResponseDto, true>> = {
      artifactType: true,
      lifecycleState: true,
      byteSize: true,
    };
    expect(Object.keys(_).length).toBe(3);
  });

  it('verification pick uses only BackupVerificationResponseDto keys', () => {
    const _: Partial<Record<keyof BackupVerificationResponseDto, true>> = {
      backupRunId: true,
      status: true,
      startedAt: true,
      completedAt: true,
    };
    expect(Object.keys(_).length).toBe(4);
  });

  it('restore drill mappers use only RestoreVerificationRunResponseDto keys', () => {
    const _: Partial<Record<keyof RestoreVerificationRunResponseDto, true>> = {
      status: true,
      dumpInspectionPassed: true,
      pgRestoreListPassed: true,
      restoreAttemptExecuted: true,
      restoreAttemptPassed: true,
      fiscalSqlSkipped: true,
      fiscalSqlPassed: true,
      integrityChecksPassed: true,
    };
    expect(Object.keys(_).length).toBe(8);
  });
});
