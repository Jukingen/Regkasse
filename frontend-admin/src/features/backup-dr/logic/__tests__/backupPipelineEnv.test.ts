import { afterEach, describe, expect, it, vi } from 'vitest';

import { isBackupPipelineClientFallbackEnabled } from '@/features/backup-dr/logic/backupPipelineEnv';

describe('isBackupPipelineClientFallbackEnabled', () => {
  afterEach(() => {
    vi.unstubAllEnvs();
  });

  it('is false when env unset', () => {
    vi.stubEnv('NEXT_PUBLIC_BACKUP_PIPELINE_CLIENT_FALLBACK', '');
    expect(isBackupPipelineClientFallbackEnabled()).toBe(false);
  });

  it('is false for "false" and other non-true strings', () => {
    vi.stubEnv('NEXT_PUBLIC_BACKUP_PIPELINE_CLIENT_FALLBACK', 'false');
    expect(isBackupPipelineClientFallbackEnabled()).toBe(false);
    vi.stubEnv('NEXT_PUBLIC_BACKUP_PIPELINE_CLIENT_FALLBACK', 'TRUE');
    expect(isBackupPipelineClientFallbackEnabled()).toBe(false);
  });

  it('is true only when env is exactly "true"', () => {
    vi.stubEnv('NEXT_PUBLIC_BACKUP_PIPELINE_CLIENT_FALLBACK', 'true');
    expect(isBackupPipelineClientFallbackEnabled()).toBe(true);
  });
});
